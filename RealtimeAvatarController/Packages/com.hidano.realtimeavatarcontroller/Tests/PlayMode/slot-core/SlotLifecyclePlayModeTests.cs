using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// Slot 動的ライフサイクルの PlayMode 統合テスト (タスク 14.1)。
    /// Unity エンジン起動を伴う <see cref="UnityTestAttribute"/> として
    /// <see cref="SlotManager"/> を介した Slot の動的追加 (Created → Active) と
    /// 削除 (Active → Disposed) の一連ライフサイクルを検証する。
    /// <para>
    /// 検証シナリオ Y (ランタイム動的生成):
    /// SlotSettings / ProviderConfig / MoCapSourceConfig いずれも
    /// <see cref="ScriptableObject.CreateInstance"/> で動的生成し、
    /// <see cref="RegistryLocator.ProviderRegistry"/> /
    /// <see cref="RegistryLocator.MoCapSourceRegistry"/> 経由で Factory を登録した状態を
    /// PlayMode 上で成立させる (requirements.md Req 1.8 / 2.8 / 14.2)。
    /// </para>
    /// <para>
    /// Mock 実装はテスト asmdef 内にクローズドに定義する (requirements.md Req 14.6 /
    /// design.md §10.2)。EditMode テストの <c>Mocks/</c> は <c>internal</c> 可視性のため
    /// 参照せず、本 PlayMode asmdef 内で必要最小限の Mock を再定義する。
    /// </para>
    /// Requirements: 1.8, 2.8, 14.2
    /// </summary>
    [TestFixture]
    public class SlotLifecyclePlayModeTests
    {
        private const string ProviderTypeId = "PlayModeMockProvider";
        private const string SourceTypeId = "PlayModeMockSource";

        private SlotManager _manager;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            RegistryLocator.ProviderRegistry.Register(
                ProviderTypeId, new PlayModeMockAvatarProviderFactory(_createdAvatars));
            RegistryLocator.MoCapSourceRegistry.Register(
                SourceTypeId, new PlayModeMockMoCapSourceFactory());

            _manager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            _manager = null;

            foreach (var avatar in _createdAvatars)
            {
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            }
            _createdAvatars.Clear();

            foreach (var so in _createdAssets)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _createdAssets.Clear();

            RegistryLocator.ResetForTest();
        }

        // --- ライフサイクル統合検証 (Created → Active → Disposed) ---

        [UnityTest]
        public IEnumerator AddSlotAsync_ThenRemoveSlotAsync_TransitionsCreatedToActiveToDisposed()
            => UniTask.ToCoroutine(async () =>
            {
                var settings = CreateRuntimeSettings("slot-1", "Slot 1");

                var events = new List<SlotStateChangedEvent>();
                using (_manager.OnSlotStateChanged.Subscribe(events.Add))
                {
                    await _manager.AddSlotAsync(settings);

                    var active = _manager.GetSlot("slot-1");
                    Assert.That(active, Is.Not.Null);
                    Assert.That(active.State, Is.EqualTo(SlotState.Active));
                    Assert.That(active.SlotId, Is.EqualTo("slot-1"));
                    Assert.That(active.DisplayName, Is.EqualTo("Slot 1"));
                    Assert.That(events, Has.Count.EqualTo(1));
                    Assert.That(events[0].SlotId, Is.EqualTo("slot-1"));
                    Assert.That(events[0].PreviousState, Is.EqualTo(SlotState.Created));
                    Assert.That(events[0].NewState, Is.EqualTo(SlotState.Active));

                    await _manager.RemoveSlotAsync("slot-1");

                    Assert.That(_manager.GetSlot("slot-1"), Is.Null);
                    Assert.That(_manager.GetSlots(), Is.Empty);
                    Assert.That(events, Has.Count.EqualTo(2));
                    Assert.That(events[1].SlotId, Is.EqualTo("slot-1"));
                    Assert.That(events[1].PreviousState, Is.EqualTo(SlotState.Active));
                    Assert.That(events[1].NewState, Is.EqualTo(SlotState.Disposed));
                }
            });

        [UnityTest]
        public IEnumerator AddSlotAsync_RuntimeCreatedSettings_ResolvesFactoriesAndProducesAvatar()
            => UniTask.ToCoroutine(async () =>
            {
                // シナリオ Y: SlotSettings / Config を ScriptableObject.CreateInstance で動的生成。
                // PlayMode 上で Registry 経由で Factory → Provider → Avatar の解決が成立することを検証する。
                var settings = CreateRuntimeSettings("slot-runtime", "Runtime Slot");

                await _manager.AddSlotAsync(settings);

                var handle = _manager.GetSlot("slot-runtime");
                Assert.That(handle, Is.Not.Null);
                Assert.That(handle.State, Is.EqualTo(SlotState.Active));
                Assert.That(handle.Settings, Is.SameAs(settings),
                    "ランタイム生成された SlotSettings が SlotHandle に保持されること");
                Assert.That(_createdAvatars, Has.Count.EqualTo(1),
                    "AddSlotAsync により Provider 経由でアバター GameObject が 1 体生成されること");
                Assert.That(_createdAvatars[0], Is.Not.Null);
            });

        [UnityTest]
        public IEnumerator AddMultipleSlots_ThenRemoveEach_AllSlotsTransitionToDisposed()
            => UniTask.ToCoroutine(async () =>
            {
                var s1 = CreateRuntimeSettings("slot-a", "Slot A");
                var s2 = CreateRuntimeSettings("slot-b", "Slot B");

                await _manager.AddSlotAsync(s1);
                await _manager.AddSlotAsync(s2);

                Assert.That(_manager.GetSlots(), Has.Count.EqualTo(2));
                Assert.That(_manager.GetSlot("slot-a").State, Is.EqualTo(SlotState.Active));
                Assert.That(_manager.GetSlot("slot-b").State, Is.EqualTo(SlotState.Active));

                await _manager.RemoveSlotAsync("slot-a");
                Assert.That(_manager.GetSlot("slot-a"), Is.Null);
                Assert.That(_manager.GetSlot("slot-b").State, Is.EqualTo(SlotState.Active));

                await _manager.RemoveSlotAsync("slot-b");
                Assert.That(_manager.GetSlots(), Is.Empty);
            });

        // --- ヘルパー ---

        private SlotSettings CreateRuntimeSettings(string slotId, string displayName)
        {
            var providerConfig = ScriptableObject.CreateInstance<PlayModeTestProviderConfig>();
            _createdAssets.Add(providerConfig);

            var sourceConfig = ScriptableObject.CreateInstance<PlayModeTestMoCapSourceConfig>();
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

        // --- PlayMode 専用 Config / Mock 実装 ---

        private sealed class PlayModeTestProviderConfig : ProviderConfigBase { }
        private sealed class PlayModeTestMoCapSourceConfig : MoCapSourceConfigBase { }

        private sealed class PlayModeMockAvatarProviderFactory : IAvatarProviderFactory
        {
            private readonly List<GameObject> _avatarSink;

            public PlayModeMockAvatarProviderFactory(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public IAvatarProvider Create(ProviderConfigBase config)
                => new PlayModeMockAvatarProvider(_avatarSink);
        }

        private sealed class PlayModeMockAvatarProvider : IAvatarProvider
        {
            private readonly List<GameObject> _avatarSink;

            public PlayModeMockAvatarProvider(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public string ProviderType => "PlayModeMock";

            public GameObject RequestAvatar(ProviderConfigBase config) => CreateAvatar();

            public UniTask<GameObject> RequestAvatarAsync(
                ProviderConfigBase config, CancellationToken cancellationToken = default)
                => UniTask.FromResult(CreateAvatar());

            public void ReleaseAvatar(GameObject avatar)
            {
                if (avatar != null) UnityEngine.Object.Destroy(avatar);
            }

            public void Dispose() { }

            private GameObject CreateAvatar()
            {
                var go = new GameObject("PlayModeMockAvatar");
                _avatarSink.Add(go);
                return go;
            }
        }

        private sealed class PlayModeMockMoCapSourceFactory : IMoCapSourceFactory
        {
            public IMoCapSource Create(MoCapSourceConfigBase config) => new PlayModeMockMoCapSource();
        }

        private sealed class PlayModeMockMoCapSource : IMoCapSource
        {
            private readonly Subject<MotionFrame> _subject = new Subject<MotionFrame>();

            public string SourceType => "PlayModeMock";
            public IObservable<MotionFrame> MotionStream => _subject;

            public void Initialize(MoCapSourceConfigBase config) { }
            public void Shutdown() { }

            public void Dispose()
            {
                _subject.OnCompleted();
                _subject.Dispose();
            }
        }
    }
}
