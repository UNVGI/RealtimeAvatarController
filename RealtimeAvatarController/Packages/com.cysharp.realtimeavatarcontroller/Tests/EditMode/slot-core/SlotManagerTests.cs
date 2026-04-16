using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Core.Tests.Mocks;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// SlotManager の EditMode テスト (タスク 12.2〜12.8 網羅)。
    /// タスク 12.8 時点で SlotManager 全挙動を EditMode で検証する最終形となる。
    /// 観点:
    ///   - AddSlotAsync 成功 (Created → Active 遷移、OnSlotStateChanged 通知)
    ///   - 同一 slotId の重複追加 → InvalidOperationException
    ///   - RemoveSlotAsync 成功 (Active → Disposed 遷移、OnSlotStateChanged 通知)
    ///   - 未登録 slotId の RemoveSlotAsync → InvalidOperationException
    ///   - GetSlots / GetSlot
    ///   - OnSlotStateChanged の通知内容 (SlotId / PreviousState / NewState)
    ///   - weight クランプ (タスク 12.3 / Req 1.5): 1.5f → 1.0f、-0.25f → 0.0f、0.5f → 0.5f
    ///   - 初期化失敗 (タスク 12.4 / Req 3.7, 3.8, 12.4): Provider / Source Resolve 失敗・
    ///     RequestAvatarAsync 失敗時に Created → Disposed 遷移 + InitFailure 発行、
    ///     例外は呼び出し側に伝播しない、部分取得済みリソースは解放される。
    ///   - RemoveSlotAsync リソース解放 (タスク 12.5 / Req 3.2, 3.5, 3.6, 10.2):
    ///     Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release の厳密順序、
    ///     共有 MoCapSource の参照カウント管理、各解放ステップの例外耐性。
    ///   - ApplyWithFallback (タスク 12.6 / Req 13.3, 13.4, 12.4):
    ///     HoldLastPose / Hide / TPose 各フォールバック挙動 + ApplyFailure 発行、
    ///     未登録 slotId の no-op、例外非伝播。
    ///   - Dispose による全 Slot 解放 (タスク 12.7 / Req 3.2, 3.5):
    ///     各 Slot への Active → Disposed 通知、Subject Complete、冪等性、
    ///     Dispose 後 AddSlotAsync / RemoveSlotAsync は ObjectDisposedException、
    ///     途中例外時の残余 Slot 解放継続。
    ///   - コンストラクタ引数 null チェック (ArgumentNullException)。
    /// Requirements: 1.5, 2.1, 2.2, 2.3, 2.6, 2.7, 2.8, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8,
    ///               10.2, 12.4, 13.3, 13.4, 14.1, 14.3
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

        // --- 初期化失敗時の Created → Disposed 遷移 (タスク 12.4 / Req 3.7, 3.8, 12.4) ---

        [Test]
        public async Task AddSlotAsync_ProviderResolveThrows_TransitionsToDisposedAndPublishesInitFailure()
        {
            var providerException = new InvalidOperationException("provider boom");
            _providerFactory.CreateException = providerException;

            var settings = CreateSettings("slot-1", "Slot 1");
            var stateEvents = new List<SlotStateChangedEvent>();
            var errors = new List<SlotError>();
            using (_manager.OnSlotStateChanged.Subscribe(stateEvents.Add))
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
                await _manager.AddSlotAsync(settings).ToTask();
            }

            Assert.That(_manager.GetSlot("slot-1"), Is.Null, "初期化失敗 Slot は Registry から除去されること");
            Assert.That(stateEvents, Has.Count.EqualTo(1));
            Assert.That(stateEvents[0].SlotId, Is.EqualTo("slot-1"));
            Assert.That(stateEvents[0].PreviousState, Is.EqualTo(SlotState.Created));
            Assert.That(stateEvents[0].NewState, Is.EqualTo(SlotState.Disposed));

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].SlotId, Is.EqualTo("slot-1"));
            Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.InitFailure));
            Assert.That(errors[0].Exception, Is.SameAs(providerException));
            Assert.That(errors[0].Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public async Task AddSlotAsync_MoCapSourceResolveThrows_TransitionsToDisposedAndPublishesInitFailure()
        {
            var sourceException = new InvalidOperationException("source boom");
            _moCapSourceFactory.CreateException = sourceException;

            var settings = CreateSettings("slot-1", "Slot 1");
            var errors = new List<SlotError>();
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
                await _manager.AddSlotAsync(settings).ToTask();
            }

            Assert.That(_manager.GetSlot("slot-1"), Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.InitFailure));
            Assert.That(errors[0].Exception, Is.SameAs(sourceException));

            // Provider が先に Resolve されているため、部分的に取得した Provider は Dispose / ReleaseAvatar されること。
            Assert.That(_providerFactory.LastCreatedProvider.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddSlotAsync_RequestAvatarAsyncThrows_TransitionsToDisposedAndPublishesInitFailure()
        {
            var settings = CreateSettings("slot-1", "Slot 1");

            var avatarException = new InvalidOperationException("avatar boom");
            _providerFactory.CreateFunc = _ =>
            {
                var provider = new MockAvatarProvider { RequestAvatarException = avatarException };
                return provider;
            };

            var errors = new List<SlotError>();
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
                await _manager.AddSlotAsync(settings).ToTask();
            }

            Assert.That(_manager.GetSlot("slot-1"), Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.InitFailure));
            Assert.That(errors[0].Exception, Is.SameAs(avatarException));

            // RequestAvatarAsync 失敗時も MoCapSource は Release され、参照カウント 0 で Dispose される。
            Assert.That(_moCapSourceFactory.LastCreatedSource.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddSlotAsync_InitFailure_SubsequentAddWithSameSlotIdSucceeds()
        {
            _providerFactory.CreateException = new InvalidOperationException("first try fails");

            var first = CreateSettings("slot-1", "Slot 1");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
            await _manager.AddSlotAsync(first).ToTask();

            _providerFactory.CreateException = null;

            var second = CreateSettings("slot-1", "Slot 1 retry");
            await _manager.AddSlotAsync(second).ToTask();

            var handle = _manager.GetSlot("slot-1");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.State, Is.EqualTo(SlotState.Active));
            Assert.That(handle.DisplayName, Is.EqualTo("Slot 1 retry"));
        }

        // --- RemoveSlotAsync リソース解放順序 / 例外耐性 (タスク 12.5 / Req 3.2, 3.5, 3.6, 10.2) ---

        [Test]
        public async Task RemoveSlotAsync_ReleasesResourcesInOrder_ReleaseAvatarThenProviderDisposeThenRegistryRelease()
        {
            // タスク 12.5 / Req 3.2, 10.2:
            // Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release の順で解放する。
            // Registry.Release 経由で MoCapSource.Dispose が呼ばれる (SlotManager が直接呼ばない)。
            var order = new List<string>();
            var mockRegistry = new MockMoCapSourceRegistry { CallOrderRecorder = order };
            mockRegistry.Register(SourceTypeId, _moCapSourceFactory);

            _manager.Dispose();
            _manager = new SlotManager(_providerRegistry, mockRegistry, _errorChannel);

            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            _providerFactory.LastCreatedProvider.CallOrderRecorder = order;
            _moCapSourceFactory.LastCreatedSource.CallOrderRecorder = order;

            await _manager.RemoveSlotAsync("slot-1").ToTask();

            Assert.That(order, Is.EqualTo(new[]
            {
                "Provider.ReleaseAvatar",
                "Provider.Dispose",
                "Registry.Release",
                "MoCapSource.Dispose",
            }));
        }

        [Test]
        public async Task RemoveSlotAsync_DoesNotCallMoCapSourceDisposeDirectly_RegistryControlsLifecycle()
        {
            // Req 3.6 / 10.2: 複数 Slot が同一 MoCapSource を共有している場合、
            // 参照カウント > 0 の間は Dispose が呼ばれない。SlotManager が直接 Dispose を呼ばないことを保証する。
            var providerConfig = ScriptableObject.CreateInstance<TestProviderConfig>();
            _createdAssets.Add(providerConfig);
            var sourceConfig = ScriptableObject.CreateInstance<TestMoCapSourceConfig>();
            _createdAssets.Add(sourceConfig);

            var sharedSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = SourceTypeId,
                Config = sourceConfig,
            };

            var settings1 = CreateSharedSettings("slot-1", "Slot 1", providerConfig, sharedSourceDescriptor);
            var settings2 = CreateSharedSettings("slot-2", "Slot 2", providerConfig, sharedSourceDescriptor);

            await _manager.AddSlotAsync(settings1).ToTask();
            await _manager.AddSlotAsync(settings2).ToTask();

            var sharedSource = _moCapSourceFactory.LastCreatedSource;
            Assert.That(_moCapSourceFactory.CreateCallCount, Is.EqualTo(1),
                "同一 Descriptor の 2 Slot は MoCapSource インスタンスを共有すること");

            await _manager.RemoveSlotAsync("slot-1").ToTask();
            Assert.That(sharedSource.DisposeCallCount, Is.EqualTo(0),
                "参照カウント > 0 の間は MoCapSource.Dispose は呼ばれてはならない");

            await _manager.RemoveSlotAsync("slot-2").ToTask();
            Assert.That(sharedSource.DisposeCallCount, Is.EqualTo(1),
                "最後の Slot 削除で Registry が MoCapSource.Dispose を 1 回だけ呼ぶこと");
        }

        [Test]
        public async Task RemoveSlotAsync_ReleaseAvatarThrows_ContinuesToProviderDisposeAndRegistryRelease()
        {
            // Req 3.5: 破棄中の例外を catch してログに記録し、残余リソースの解放を継続する。
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            _providerFactory.LastCreatedProvider.ReleaseAvatarException =
                new InvalidOperationException("release avatar boom");

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[SlotManager\].*ReleaseAvatar"));

            var events = new List<SlotStateChangedEvent>();
            using (_manager.OnSlotStateChanged.Subscribe(events.Add))
            {
                await _manager.RemoveSlotAsync("slot-1").ToTask();
            }

            Assert.That(_providerFactory.LastCreatedProvider.DisposeCallCount, Is.EqualTo(1),
                "ReleaseAvatar 失敗後も Provider.Dispose は実行されること");
            Assert.That(_moCapSourceFactory.LastCreatedSource.DisposeCallCount, Is.EqualTo(1),
                "ReleaseAvatar 失敗後も Registry.Release 経由で MoCapSource.Dispose が呼ばれること");
            Assert.That(_manager.GetSlot("slot-1"), Is.Null);
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].NewState, Is.EqualTo(SlotState.Disposed));
        }

        [Test]
        public async Task RemoveSlotAsync_ProviderDisposeThrows_ContinuesToRegistryRelease()
        {
            // Req 3.5: Provider.Dispose で例外が発生しても MoCapSourceRegistry.Release を継続実行する。
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            _providerFactory.LastCreatedProvider.DisposeException =
                new InvalidOperationException("provider dispose boom");

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[SlotManager\].*Provider\.Dispose"));

            await _manager.RemoveSlotAsync("slot-1").ToTask();

            Assert.That(_moCapSourceFactory.LastCreatedSource.DisposeCallCount, Is.EqualTo(1),
                "Provider.Dispose 失敗後も Registry.Release 経由で MoCapSource.Dispose が呼ばれること");
            Assert.That(_manager.GetSlot("slot-1"), Is.Null);
        }

        [Test]
        public async Task RemoveSlotAsync_RegistryReleaseThrows_StillEmitsDisposedAndRemovesSlot()
        {
            // Req 3.5: MoCapSourceRegistry.Release で例外が発生しても
            // SlotRegistry からの除去と Disposed 状態通知は継続する。
            var mockRegistry = new MockMoCapSourceRegistry
            {
                ReleaseException = new InvalidOperationException("registry release boom"),
            };
            mockRegistry.Register(SourceTypeId, _moCapSourceFactory);

            _manager.Dispose();
            _manager = new SlotManager(_providerRegistry, mockRegistry, _errorChannel);

            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[SlotManager\].*Release"));

            var events = new List<SlotStateChangedEvent>();
            using (_manager.OnSlotStateChanged.Subscribe(events.Add))
            {
                await _manager.RemoveSlotAsync("slot-1").ToTask();
            }

            Assert.That(_manager.GetSlot("slot-1"), Is.Null,
                "Registry.Release 失敗時も Slot は SlotRegistry から除去されること");
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].NewState, Is.EqualTo(SlotState.Disposed));
            Assert.That(mockRegistry.ReleaseCallCount, Is.EqualTo(1));
        }

        // --- ApplyWithFallback / ApplyFailure フォールバック (タスク 12.6 / Req 13.3, 13.4, 12.4) ---

        [Test]
        public async Task ApplyWithFallback_ActionSucceeds_ExecutesActionAndPublishesNoError()
        {
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            var invoked = 0;
            var errors = new List<SlotError>();
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                _manager.ApplyWithFallback("slot-1", () => invoked++);
            }

            Assert.That(invoked, Is.EqualTo(1));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public async Task ApplyWithFallback_HoldLastPoseOnException_PublishesApplyFailureAndDoesNotModifyAvatar()
        {
            // Req 13.3: HoldLastPose は直前フレームのポーズを維持し続ける (アバターを変更しない)。
            // Req 13.4 / 12.4: フォールバック後に SlotErrorCategory.ApplyFailure を ErrorChannel に発行する。
            var avatar = new GameObject("TestAvatar");
            try
            {
                var renderer = avatar.AddComponent<MeshRenderer>();
                _providerFactory.CreateFunc = _ =>
                    new MockAvatarProvider { AvatarFactory = () => avatar };

                var settings = CreateSettings("slot-1", "Slot 1");
                settings.fallbackBehavior = FallbackBehavior.HoldLastPose;
                await _manager.AddSlotAsync(settings).ToTask();

                var boom = new InvalidOperationException("apply boom");
                var errors = new List<SlotError>();
                using (_errorChannel.Errors.Subscribe(errors.Add))
                {
                    LogAssert.Expect(LogType.Error,
                        new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*ApplyFailure"));
                    _manager.ApplyWithFallback("slot-1", () => throw boom);
                }

                Assert.That(renderer.enabled, Is.True, "HoldLastPose はアバターの Renderer を無効化してはならない");
                Assert.That(avatar.activeSelf, Is.True);
                Assert.That(errors, Has.Count.EqualTo(1));
                Assert.That(errors[0].SlotId, Is.EqualTo("slot-1"));
                Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.ApplyFailure));
                Assert.That(errors[0].Exception, Is.SameAs(boom));
                Assert.That(errors[0].Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public async Task ApplyWithFallback_HideOnException_DisablesAllRenderersAndKeepsGameObjectActive()
        {
            // Req 13.3 (Hide) / validation-design.md 引き継ぎ事項 #3:
            // Hide は Renderer.enabled = false で実装する。GameObject.SetActive(false) は使用しない。
            // 子階層の Renderer (SkinnedMeshRenderer / MeshRenderer 等) も網羅的に無効化する。
            var avatar = new GameObject("TestAvatar");
            try
            {
                var rootRenderer = avatar.AddComponent<MeshRenderer>();
                var child = new GameObject("child");
                child.transform.SetParent(avatar.transform);
                var childRenderer = child.AddComponent<MeshRenderer>();

                _providerFactory.CreateFunc = _ =>
                    new MockAvatarProvider { AvatarFactory = () => avatar };

                var settings = CreateSettings("slot-1", "Slot 1");
                settings.fallbackBehavior = FallbackBehavior.Hide;
                await _manager.AddSlotAsync(settings).ToTask();

                var boom = new InvalidOperationException("apply boom");
                var errors = new List<SlotError>();
                using (_errorChannel.Errors.Subscribe(errors.Add))
                {
                    LogAssert.Expect(LogType.Error,
                        new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*ApplyFailure"));
                    _manager.ApplyWithFallback("slot-1", () => throw boom);
                }

                Assert.That(rootRenderer.enabled, Is.False);
                Assert.That(childRenderer.enabled, Is.False);
                Assert.That(avatar.activeSelf, Is.True,
                    "Hide フォールバックは GameObject.SetActive(false) を使ってはならない (validation-design.md 引き継ぎ事項 #3)");
                Assert.That(errors, Has.Count.EqualTo(1));
                Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.ApplyFailure));
                Assert.That(errors[0].Exception, Is.SameAs(boom));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public async Task ApplyWithFallback_TPoseOnException_PublishesApplyFailure()
        {
            // Req 13.3 (TPose): 具体 API (Humanoid リセット) は motion-pipeline 合意後に実装予定。
            // 12.6 の段階ではスケルトン実装 (例外を飲み込み ApplyFailure を発行する) を検証する。
            var settings = CreateSettings("slot-1", "Slot 1");
            settings.fallbackBehavior = FallbackBehavior.TPose;
            await _manager.AddSlotAsync(settings).ToTask();

            var boom = new InvalidOperationException("apply boom");
            var errors = new List<SlotError>();
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*ApplyFailure"));
                _manager.ApplyWithFallback("slot-1", () => throw boom);
            }

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Category, Is.EqualTo(SlotErrorCategory.ApplyFailure));
            Assert.That(errors[0].Exception, Is.SameAs(boom));
        }

        [Test]
        public async Task ApplyWithFallback_UnknownSlotId_DoesNothingAndPublishesNoError()
        {
            // 未登録 slotId が渡された場合は安全に no-op とし、ErrorChannel にも発行しない。
            // (想定外の呼び出しに対して SlotManager を安定させる)
            var settings = CreateSettings("slot-1", "Slot 1");
            await _manager.AddSlotAsync(settings).ToTask();

            var invoked = 0;
            var errors = new List<SlotError>();
            using (_errorChannel.Errors.Subscribe(errors.Add))
            {
                _manager.ApplyWithFallback("unknown", () => invoked++);
            }

            Assert.That(invoked, Is.EqualTo(0));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public async Task ApplyWithFallback_ExceptionDoesNotPropagateToCaller()
        {
            // ApplyWithFallback は applyAction の例外を呼び出し側に伝播させず、
            // フォールバックと ErrorChannel 発行で処理を完結させる。
            var settings = CreateSettings("slot-1", "Slot 1");
            settings.fallbackBehavior = FallbackBehavior.HoldLastPose;
            await _manager.AddSlotAsync(settings).ToTask();

            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*ApplyFailure"));
            Assert.DoesNotThrow(
                () => _manager.ApplyWithFallback("slot-1", () => throw new InvalidOperationException("boom")));
        }

        // --- Dispose で全 Slot 解放 (タスク 12.7 / Req 3.2, 3.5) ---

        [Test]
        public async Task Dispose_WithMultipleSlots_ReleasesAllProviderAndMoCapSourceResources()
        {
            // タスク 12.7 / Req 3.2: Dispose は全 Slot に対して RemoveSlotAsync 相当の解放処理を実行する。
            // Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release 経由の
            // MoCapSource.Dispose が各 Slot で 1 回ずつ呼ばれることを確認する。
            var providers = new List<MockAvatarProvider>();
            _providerFactory.CreateFunc = _ =>
            {
                var p = new MockAvatarProvider();
                providers.Add(p);
                return p;
            };
            var sources = new List<MockMoCapSource>();
            _moCapSourceFactory.CreateFunc = _ =>
            {
                var s = new MockMoCapSource();
                sources.Add(s);
                return s;
            };

            await _manager.AddSlotAsync(CreateSettings("slot-1", "Slot 1")).ToTask();
            await _manager.AddSlotAsync(CreateSettings("slot-2", "Slot 2")).ToTask();

            Assert.That(providers, Has.Count.EqualTo(2));
            Assert.That(sources, Has.Count.EqualTo(2));

            _manager.Dispose();

            foreach (var p in providers)
            {
                Assert.That(p.ReleaseAvatarCallCount, Is.EqualTo(1),
                    "Dispose は各 Slot の Provider.ReleaseAvatar を呼ぶこと");
                Assert.That(p.DisposeCallCount, Is.EqualTo(1),
                    "Dispose は各 Slot の Provider.Dispose を呼ぶこと");
            }
            foreach (var s in sources)
            {
                Assert.That(s.DisposeCallCount, Is.EqualTo(1),
                    "Dispose は Registry 経由で各 MoCapSource を Dispose すること");
            }

            Assert.That(_manager.GetSlots(), Is.Empty, "Dispose 後は登録 Slot が空であること");
        }

        [Test]
        public async Task Dispose_EmitsDisposedStateChangeForEachSlotAndCompletesStateChangedSubject()
        {
            // タスク 12.7: 各 Slot の Active → Disposed 遷移を OnSlotStateChanged に通知し、
            // その後に Subject を Complete する (design.md §4.1)。
            await _manager.AddSlotAsync(CreateSettings("slot-1", "Slot 1")).ToTask();
            await _manager.AddSlotAsync(CreateSettings("slot-2", "Slot 2")).ToTask();

            var events = new List<SlotStateChangedEvent>();
            var completed = false;
            using (_manager.OnSlotStateChanged.Subscribe(events.Add, () => completed = true))
            {
                _manager.Dispose();
            }

            Assert.That(events, Has.Count.EqualTo(2),
                "Dispose は各 Slot に対して 1 回ずつ OnSlotStateChanged を発行すること");
            Assert.That(events.TrueForAll(e => e.NewState == SlotState.Disposed), Is.True);
            Assert.That(events.TrueForAll(e => e.PreviousState == SlotState.Active), Is.True);
            var ids = new List<string>();
            foreach (var e in events) ids.Add(e.SlotId);
            Assert.That(ids, Contains.Item("slot-1"));
            Assert.That(ids, Contains.Item("slot-2"));
            Assert.That(completed, Is.True,
                "Dispose は OnSlotStateChanged Subject を OnCompleted で終端すること");
        }

        [Test]
        public void Dispose_NoSlots_CompletesStateChangedSubjectWithoutEmittingEvents()
        {
            var events = new List<SlotStateChangedEvent>();
            var completed = false;
            using (_manager.OnSlotStateChanged.Subscribe(events.Add, () => completed = true))
            {
                _manager.Dispose();
            }

            Assert.That(events, Is.Empty);
            Assert.That(completed, Is.True);
        }

        [Test]
        public async Task Dispose_Idempotent_SecondCallDoesNotReleaseResourcesAgain()
        {
            await _manager.AddSlotAsync(CreateSettings("slot-1", "Slot 1")).ToTask();

            var provider = _providerFactory.LastCreatedProvider;
            var source = _moCapSourceFactory.LastCreatedSource;

            _manager.Dispose();

            Assert.That(provider.DisposeCallCount, Is.EqualTo(1));
            Assert.That(source.DisposeCallCount, Is.EqualTo(1));

            _manager.Dispose();

            Assert.That(provider.DisposeCallCount, Is.EqualTo(1),
                "Dispose は冪等であり、2 回目の呼び出しで追加解放を行ってはならない");
            Assert.That(source.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Dispose_OneSlotProviderDisposeThrows_ContinuesToReleaseRemainingSlots()
        {
            // Req 3.5: 破棄中に例外が発生した場合は catch してログに記録し、残余リソースの解放を継続する。
            var providers = new List<MockAvatarProvider>();
            _providerFactory.CreateFunc = _ =>
            {
                var p = new MockAvatarProvider();
                providers.Add(p);
                return p;
            };
            var sources = new List<MockMoCapSource>();
            _moCapSourceFactory.CreateFunc = _ =>
            {
                var s = new MockMoCapSource();
                sources.Add(s);
                return s;
            };

            await _manager.AddSlotAsync(CreateSettings("slot-1", "Slot 1")).ToTask();
            await _manager.AddSlotAsync(CreateSettings("slot-2", "Slot 2")).ToTask();

            providers[0].DisposeException =
                new InvalidOperationException("first provider dispose boom");

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[SlotManager\].*Provider\.Dispose"));

            _manager.Dispose();

            Assert.That(providers[0].DisposeCallCount, Is.EqualTo(1));
            Assert.That(providers[1].DisposeCallCount, Is.EqualTo(1),
                "先行 Slot の Provider.Dispose で例外が発生しても残余 Slot の Provider.Dispose は実行されること");
            Assert.That(sources[0].DisposeCallCount, Is.EqualTo(1),
                "先行 Slot の Provider.Dispose で例外が発生しても MoCapSource の Dispose は継続すること");
            Assert.That(sources[1].DisposeCallCount, Is.EqualTo(1));
            Assert.That(_manager.GetSlots(), Is.Empty);
        }

        [Test]
        public async Task Dispose_AfterDispose_AddSlotAsyncThrowsObjectDisposedException()
        {
            _manager.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _manager.AddSlotAsync(CreateSettings("slot-x", "x")).ToTask());
        }

        [Test]
        public async Task Dispose_AfterDispose_RemoveSlotAsyncThrowsObjectDisposedException()
        {
            await _manager.AddSlotAsync(CreateSettings("slot-1", "Slot 1")).ToTask();

            _manager.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await _manager.RemoveSlotAsync("slot-1").ToTask());
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

        /// <summary>
        /// 2 つ以上の Slot で同一 Provider Config と同一 MoCapSource Descriptor を共有するための
        /// SlotSettings を生成する。タスク 12.5 の参照共有テスト専用。
        /// </summary>
        private SlotSettings CreateSharedSettings(
            string slotId,
            string displayName,
            ProviderConfigBase sharedProviderConfig,
            MoCapSourceDescriptor sharedSourceDescriptor)
        {
            var settings = ScriptableObject.CreateInstance<SlotSettings>();
            _createdAssets.Add(settings);

            settings.slotId = slotId;
            settings.displayName = displayName;
            settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = ProviderTypeId,
                Config = sharedProviderConfig,
            };
            settings.moCapSourceDescriptor = sharedSourceDescriptor;
            return settings;
        }

        private sealed class TestProviderConfig : ProviderConfigBase { }
        private sealed class TestMoCapSourceConfig : MoCapSourceConfigBase { }
    }
}
