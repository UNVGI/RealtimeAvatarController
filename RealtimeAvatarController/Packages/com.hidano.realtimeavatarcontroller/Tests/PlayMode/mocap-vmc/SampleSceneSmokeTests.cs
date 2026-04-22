using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// Phase G (Sample 実機スモークテスト) タスク 7.1 の PlayMode スモーク
    /// (tasks.md §7.1 / requirements.md 要件 7.1, 7.2, 7.3, 7.4, 7.6, 11.3)。
    ///
    /// <para>
    /// 検証観点 (3 ケース):
    ///   1. <see cref="SlotSettings"/> に <see cref="VMCMoCapSourceConfig"/> を指定して
    ///      <see cref="SlotManager.AddSlotAsync"/> を呼ぶと Slot が Active に遷移し、
    ///      <see cref="EVMC4UMoCapSource"/> が Resolve され <c>MotionStream</c> を購読できる (要件 7.1)。
    ///   2. 同一 <see cref="VMCMoCapSourceConfig"/> を共有する 2 つの Slot を追加すると、
    ///      参照共有により同一 Adapter インスタンスが両 Slot に供給される (要件 5.6 / 7.1)。
    ///   3. 1 つの Slot を Remove → 別 Config の Slot を Add する差替フローが例外なく完了する (要件 7.3 / 7.4)。
    /// </para>
    ///
    /// <para>
    /// テスト独立性: <see cref="RegistryLocator.ResetForTest"/> と
    /// <see cref="EVMC4USharedReceiver.ResetForTest"/> を <c>[SetUp]</c>/<c>[TearDown]</c> で呼び出す。
    /// 実機 Sample Scene の目視確認はタスク 7.2 (DEFERRED TO USER) に委ねる。
    /// </para>
    /// </summary>
    [TestFixture]
    public class SampleSceneSmokeTests
    {
        private const string ProviderTypeId = "SmokeMockProvider";

        private SlotManager _manager;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            EVMC4USharedReceiver.ResetForTest();

            RegistryLocator.ProviderRegistry.Register(
                ProviderTypeId, new SmokeAvatarProviderFactory(_createdAvatars));
            RegistryLocator.MoCapSourceRegistry.Register(
                VMCMoCapSourceFactory.VmcSourceTypeId, new VMCMoCapSourceFactory());

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

            EVMC4USharedReceiver.ResetForTest();
            RegistryLocator.ResetForTest();
        }

        // --- ケース 1: SlotSettings + VMCMoCapSourceConfig で Slot 追加 → EVMC4UMoCapSource が Resolve される ---

        [UnityTest]
        public IEnumerator AddSlot_WithVMCConfig_ResolvesEVMC4USourceAndMotionStreamIsSubscribable()
            => UniTask.ToCoroutine(async () =>
            {
                var vmcConfig = CreateVmcConfig(port: 49521);
                var settings = CreateSettings("slot-smoke-1", "Smoke Slot 1", vmcConfig);

                await _manager.AddSlotAsync(settings);

                var handle = _manager.GetSlot("slot-smoke-1");
                Assert.That(handle, Is.Not.Null,
                    "AddSlotAsync 後に SlotHandle が取得できるべき (要件 7.1)。");
                Assert.That(handle.State, Is.EqualTo(SlotState.Active),
                    "Slot は Active に遷移しているべき (要件 7.1)。");

                Assert.That(_manager.TryGetSlotResources("slot-smoke-1", out var source, out _), Is.True,
                    "TryGetSlotResources が IMoCapSource を返すべき (要件 7.1)。");
                Assert.That(source, Is.InstanceOf<EVMC4UMoCapSource>(),
                    "VMCMoCapSourceFactory 経由で Resolve される型は EVMC4UMoCapSource であるべき。");
                Assert.That(source.SourceType, Is.EqualTo("VMC"),
                    "SourceType は \"VMC\" であるべき (要件 1.3)。");

                using (source.MotionStream.Subscribe(new NoopObserver()))
                {
                    // 購読できること自体が検証対象 (要件 7.1 / 4.7)。ここでは OnNext 到達は要求しない。
                }

                await _manager.RemoveSlotAsync("slot-smoke-1");
            });

        // --- ケース 2: 同一 Config を参照する 2 Slot は同一 Adapter を共有 (要件 5.6) ---

        [UnityTest]
        public IEnumerator AddTwoSlots_WithSameVMCConfig_ShareSameEVMC4UMoCapSourceInstance()
            => UniTask.ToCoroutine(async () =>
            {
                var vmcConfig = CreateVmcConfig(port: 49522);
                var settings1 = CreateSettings("slot-smoke-a", "Smoke Slot A", vmcConfig);
                var settings2 = CreateSettings("slot-smoke-b", "Smoke Slot B", vmcConfig);

                await _manager.AddSlotAsync(settings1);
                await _manager.AddSlotAsync(settings2);

                Assert.That(_manager.TryGetSlotResources("slot-smoke-a", out var sourceA, out _), Is.True);
                Assert.That(_manager.TryGetSlotResources("slot-smoke-b", out var sourceB, out _), Is.True);

                Assert.That(sourceA, Is.InstanceOf<EVMC4UMoCapSource>());
                Assert.That(sourceB, Is.SameAs(sourceA),
                    "同一 VMCMoCapSourceConfig を参照する 2 つの Slot は同一 Adapter を共有するべき (要件 5.6)。");

                await _manager.RemoveSlotAsync("slot-smoke-a");
                await _manager.RemoveSlotAsync("slot-smoke-b");
            });

        // --- ケース 3: Release Slot → Resolve 別 Slot の差替フロー (要件 7.3 / 7.4) ---

        [UnityTest]
        public IEnumerator RemoveSlot_ThenAddAnotherSlot_SwapFlowCompletesWithoutException()
            => UniTask.ToCoroutine(async () =>
            {
                var vmcConfigX = CreateVmcConfig(port: 49523);
                var settingsX = CreateSettings("slot-smoke-x", "Smoke Slot X", vmcConfigX);

                await _manager.AddSlotAsync(settingsX);
                await _manager.RemoveSlotAsync("slot-smoke-x");
                Assert.That(_manager.GetSlot("slot-smoke-x"), Is.Null,
                    "RemoveSlotAsync 後に SlotHandle は解放されているべき (要件 7.2)。");

                var vmcConfigY = CreateVmcConfig(port: 49524);
                var settingsY = CreateSettings("slot-smoke-y", "Smoke Slot Y", vmcConfigY);

                await _manager.AddSlotAsync(settingsY);

                var handleY = _manager.GetSlot("slot-smoke-y");
                Assert.That(handleY, Is.Not.Null,
                    "Release → Resolve 差替後も新 Slot が Active に遷移するべき (要件 7.3)。");
                Assert.That(handleY.State, Is.EqualTo(SlotState.Active));
                Assert.That(_manager.TryGetSlotResources("slot-smoke-y", out var sourceY, out _), Is.True);
                Assert.That(sourceY, Is.InstanceOf<EVMC4UMoCapSource>());

                await _manager.RemoveSlotAsync("slot-smoke-y");
            });

        // --- ヘルパー ---

        private VMCMoCapSourceConfig CreateVmcConfig(int port)
        {
            var cfg = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            cfg.port = port;
            cfg.bindAddress = "0.0.0.0";
            _createdAssets.Add(cfg);
            return cfg;
        }

        private SlotSettings CreateSettings(string slotId, string displayName, VMCMoCapSourceConfig vmcConfig)
        {
            var providerConfig = ScriptableObject.CreateInstance<SmokeProviderConfig>();
            _createdAssets.Add(providerConfig);

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
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = vmcConfig,
            };
            return settings;
        }

        // --- PlayMode 専用 Provider Mock (mocap-vmc スコープ外のため最小実装) ---

        private sealed class SmokeProviderConfig : ProviderConfigBase { }

        private sealed class SmokeAvatarProviderFactory : IAvatarProviderFactory
        {
            private readonly List<GameObject> _avatarSink;

            public SmokeAvatarProviderFactory(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public IAvatarProvider Create(ProviderConfigBase config) => new SmokeAvatarProvider(_avatarSink);
        }

        private sealed class SmokeAvatarProvider : IAvatarProvider
        {
            private readonly List<GameObject> _avatarSink;

            public SmokeAvatarProvider(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public string ProviderType => "SmokeMock";

            public GameObject RequestAvatar(ProviderConfigBase config) => Make();

            public UniTask<GameObject> RequestAvatarAsync(
                ProviderConfigBase config, CancellationToken cancellationToken = default)
                => UniTask.FromResult(Make());

            public void ReleaseAvatar(GameObject avatar)
            {
                if (avatar != null) UnityEngine.Object.Destroy(avatar);
            }

            public void Dispose() { }

            private GameObject Make()
            {
                var go = new GameObject("SmokeAvatar");
                _avatarSink.Add(go);
                return go;
            }
        }

        private sealed class NoopObserver : IObserver<MotionFrame>
        {
            public void OnNext(MotionFrame value) { }
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }
    }
}
