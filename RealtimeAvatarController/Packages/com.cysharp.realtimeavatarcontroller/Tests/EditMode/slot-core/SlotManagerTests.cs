using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Core.Tests.Mocks;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// SlotManager のコア実装 EditMode テスト (タスク 12.2 範囲)。
    /// 観点:
    ///   - AddSlotAsync 成功 (Created → Active 遷移、OnSlotStateChanged 通知)
    ///   - 同一 slotId の重複追加 → InvalidOperationException
    ///   - RemoveSlotAsync 成功 (Active → Disposed 遷移、OnSlotStateChanged 通知)
    ///   - 未登録 slotId の RemoveSlotAsync → InvalidOperationException
    ///   - GetSlots / GetSlot
    ///   - OnSlotStateChanged の通知内容 (SlotId / PreviousState / NewState)
    ///   - weight クランプ (タスク 12.3 / Req 1.5): 1.5f → 1.0f、-0.25f → 0.0f、0.5f → 0.5f
    /// InitFailure・ApplyFailure・全 Slot 解放 Dispose は後続タスク 12.4〜12.8 で検証する。
    /// Requirements: 1.5, 2.6, 2.7, 2.8, 3.1, 3.2, 3.4
    /// </summary>
    [TestFixture]
    public class SlotManagerTests
    {
        private const string ProviderTypeId = "MockProvider";
        private const string SourceTypeId = "MockSource";

        private DefaultProviderRegistry _providerRegistry;
        private DefaultMoCapSourceRegistry _moCapSourceRegistry;
        private DefaultSlotErrorChannel _errorChannel;
        private MockAvatarProviderFactory _providerFactory;
        private MockMoCapSourceFactory _moCapSourceFactory;
        private SlotManager _manager;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _providerRegistry = new DefaultProviderRegistry();
            _moCapSourceRegistry = new DefaultMoCapSourceRegistry();
            _errorChannel = new DefaultSlotErrorChannel();

            _providerFactory = new MockAvatarProviderFactory();
            _moCapSourceFactory = new MockMoCapSourceFactory();

            _providerRegistry.Register(ProviderTypeId, _providerFactory);
            _moCapSourceRegistry.Register(SourceTypeId, _moCapSourceFactory);

            _manager = new SlotManager(_providerRegistry, _moCapSourceRegistry, _errorChannel);
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();

            foreach (var so in _createdAssets)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _createdAssets.Clear();

            RegistryLocator.ResetForTest();
        }

        // --- AddSlotAsync 成功 (Created → Active 遷移) ---

        [Test]
        public async Task AddSlotAsync_ValidSettings_TransitionsToActive()
        {
            var settings = CreateSettings("slot-1", "Slot 1");

            await _manager.AddSlotAsync(settings).ToTask();

            var handle = _manager.GetSlot("slot-1");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.State, Is.EqualTo(SlotState.Active));
            Assert.That(handle.SlotId, Is.EqualTo("slot-1"));
            Assert.That(handle.DisplayName, Is.EqualTo("Slot 1"));
        }

        [Test]
        public async Task AddSlotAsync_ResolvesProviderAndSourceViaRegistries()
        {
            var settings = CreateSettings("slot-1", "Slot 1");

            await _manager.AddSlotAsync(settings).ToTask();

            Assert.That(_providerFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(_moCapSourceFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(_providerFactory.LastCreatedProvider.RequestAvatarAsyncCallCount, Is.EqualTo(1));
            Assert.That(_moCapSourceFactory.LastCreatedSource.InitializeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddSlotAsync_EmitsStateChangedEvent_CreatedToActive()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            SlotStateChangedEvent received = null;
            using (_manager.OnSlotStateChanged.Subscribe(e => received = e))
            {
                await _manager.AddSlotAsync(settings).ToTask();
            }

            Assert.That(received, Is.Not.Null);
            Assert.That(received.SlotId, Is.EqualTo("slot-1"));
            Assert.That(received.PreviousState, Is.EqualTo(SlotState.Created));
            Assert.That(received.NewState, Is.EqualTo(SlotState.Active));
        }

        // --- AddSlotAsync 重複 slotId ---

        [Test]
        public async Task AddSlotAsync_DuplicateSlotId_ThrowsInvalidOperationException()
        {
            var settings1 = CreateSettings("slot-1", "Slot 1");
            var settings2 = CreateSettings("slot-1", "Slot 1 duplicate");

            await _manager.AddSlotAsync(settings1).ToTask();

            InvalidOperationException caught = null;
            try
            {
                await _manager.AddSlotAsync(settings2).ToTask();
            }
            catch (InvalidOperationException ex)
            {
                caught = ex;
            }

            Assert.That(caught, Is.Not.Null, "重複 slotId の AddSlotAsync は InvalidOperationException をスローすべき");

            // 元の Slot は Active のまま残る
            var handle = _manager.GetSlot("slot-1");
            Assert.That(handle.State, Is.EqualTo(SlotState.Active));
            Assert.That(handle.DisplayName, Is.EqualTo("Slot 1"));
        }

        // --- RemoveSlotAsync 成功 (Active → Disposed 遷移) ---

        [Test]
        public async Task RemoveSlotAsync_ExistingSlot_TransitionsToDisposedAndRemovesFromRegistry()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            await _manager.RemoveSlotAsync("slot-1").ToTask();

            Assert.That(_manager.GetSlot("slot-1"), Is.Null);
            Assert.That(_manager.GetSlots(), Is.Empty);
        }

        [Test]
        public async Task RemoveSlotAsync_EmitsStateChangedEvent_ActiveToDisposed()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            var events = new List<SlotStateChangedEvent>();
            using (_manager.OnSlotStateChanged.Subscribe(events.Add))
            {
                await _manager.RemoveSlotAsync("slot-1").ToTask();
            }

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].SlotId, Is.EqualTo("slot-1"));
            Assert.That(events[0].PreviousState, Is.EqualTo(SlotState.Active));
            Assert.That(events[0].NewState, Is.EqualTo(SlotState.Disposed));
        }

        // --- RemoveSlotAsync 未登録 slotId ---

        [Test]
        public void RemoveSlotAsync_UnknownSlotId_ThrowsInvalidOperationException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.RemoveSlotAsync("unknown"));
        }

        // --- GetSlots / GetSlot ---

        [Test]
        public async Task GetSlots_AfterMultipleAdds_ReturnsAllHandles()
        {
            var settings1 = CreateSettings("slot-1", "Slot 1");
            var settings2 = CreateSettings("slot-2", "Slot 2");

            await _manager.AddSlotAsync(settings1).ToTask();
            await _manager.AddSlotAsync(settings2).ToTask();

            var slots = _manager.GetSlots();
            Assert.That(slots, Has.Count.EqualTo(2));
            var ids = new List<string>();
            foreach (var s in slots) ids.Add(s.SlotId);
            Assert.That(ids, Contains.Item("slot-1"));
            Assert.That(ids, Contains.Item("slot-2"));
        }

        [Test]
        public void GetSlot_UnknownSlotId_ReturnsNull()
        {
            Assert.That(_manager.GetSlot("unknown"), Is.Null);
        }

        [Test]
        public async Task GetSlot_ExistingSlot_ReturnsHandleWithActiveState()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            var handle = _manager.GetSlot("slot-1");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.State, Is.EqualTo(SlotState.Active));
            Assert.That(handle.Settings, Is.SameAs(settings));
        }

        // --- weight クランプ (タスク 12.3 / Req 1.5) ---

        [Test]
        public async Task AddSlotAsync_WeightAboveOne_ClampsToOne()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            settings.weight = 1.5f;

            await _manager.AddSlotAsync(settings).ToTask();

            Assert.That(settings.weight, Is.EqualTo(1.0f));
            Assert.That(_manager.GetSlot("slot-1").Settings.weight, Is.EqualTo(1.0f));
        }

        [Test]
        public async Task AddSlotAsync_WeightBelowZero_ClampsToZero()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            settings.weight = -0.25f;

            await _manager.AddSlotAsync(settings).ToTask();

            Assert.That(settings.weight, Is.EqualTo(0.0f));
            Assert.That(_manager.GetSlot("slot-1").Settings.weight, Is.EqualTo(0.0f));
        }

        [Test]
        public async Task AddSlotAsync_WeightInRange_RemainsUnchanged()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            settings.weight = 0.5f;

            await _manager.AddSlotAsync(settings).ToTask();

            Assert.That(settings.weight, Is.EqualTo(0.5f));
        }

        // --- コンストラクタ引数 null チェック ---

        [Test]
        public void Constructor_NullProviderRegistry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new SlotManager(null, _moCapSourceRegistry, _errorChannel));
        }

        [Test]
        public void Constructor_NullMoCapSourceRegistry_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new SlotManager(_providerRegistry, null, _errorChannel));
        }

        [Test]
        public void Constructor_NullErrorChannel_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new SlotManager(_providerRegistry, _moCapSourceRegistry, null));
        }

        // --- ヘルパー ---

        private SlotSettings CreateSettings(string slotId, string displayName)
        {
            var providerConfig = ScriptableObject.CreateInstance<TestProviderConfig>();
            _createdAssets.Add(providerConfig);

            var sourceConfig = ScriptableObject.CreateInstance<TestMoCapSourceConfig>();
            _createdAssets.Add(sourceConfig);

            var settings = ScriptableObject.CreateInstance<SlotSettings>();
            _createdAssets.Add(settings);

            settings.slotId = slotId;
            settings.displayName = displayName;
            settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = ProviderTypeId,
                Config = providerConfig,
            };
            settings.moCapSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = SourceTypeId,
                Config = sourceConfig,
            };
            return settings;
        }

        private sealed class TestProviderConfig : ProviderConfigBase { }
        private sealed class TestMoCapSourceConfig : MoCapSourceConfigBase { }
    }
}
