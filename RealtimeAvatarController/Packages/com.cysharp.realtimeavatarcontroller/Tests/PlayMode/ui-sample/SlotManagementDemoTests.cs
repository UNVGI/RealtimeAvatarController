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

namespace RealtimeAvatarController.Samples.UI.Tests.PlayMode
{
    /// <summary>
    /// SlotManagementDemo の PlayMode テスト (tasks.md T17-2 / design.md §11.2)。
    /// テスト観点:
    ///   - デモシーン起動確認: <see cref="SlotManagerBehaviour"/> が Awake で初期化エラーなく
    ///     <see cref="SlotManager"/> を構築できること。
    ///   - 参照共有シナリオ再現: 同一 <see cref="MoCapSourceDescriptor"/> (同一 Config SO 参照) を持つ
    ///     2 件の <see cref="SlotSettings"/> を <see cref="SlotManager.AddSlotAsync"/> した後、
    ///     <see cref="IMoCapSourceRegistry"/> が同一の <see cref="IMoCapSource"/> インスタンスを返すこと。
    ///   - Slot 削除後のエラーチャンネル: 正常系の Slot 追加・削除では
    ///     <see cref="ISlotErrorChannel.Errors"/> に不要なエラーが発行されないこと。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>シーンアセットの扱い</b>: デモシーン本体 (<c>SlotManagementDemo.unity</c>) は UPM の
    /// <c>Samples~/</c> 配下に配置されているため Unity の <see cref="UnityEngine.SceneManagement.SceneManager"/>
    /// / AssetDatabase からは不可視である。本テストは <see cref="SlotManagerBehaviour"/> を
    /// プログラム的に生成し、デモシーンで検証すべき初期化経路 (Awake → <see cref="SlotManager"/> 構築)
    /// を同等に再現する。参照共有シナリオは
    /// <see cref="MoCapSourceSharingPlayModeTests"/> と同じモック注入パターンに従い、
    /// ui-sample 側から SlotManager API を呼び出す統合ビューとして検証する。
    /// </para>
    /// <para>
    /// <b>Registry 汚染対策</b>: <c>[SetUp]</c>/<c>[TearDown]</c> で
    /// <see cref="RegistryLocator.ResetForTest"/> を呼び、他 PlayMode テストや
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 経由の自己登録からの状態汚染を排除する (Req 10-5)。
    /// </para>
    /// Requirements: 5, 11, 12
    /// </remarks>
    [TestFixture]
    public class SlotManagementDemoTests
    {
        private const string ProviderTypeId = "DemoMockProvider";
        private const string SourceTypeId = "DemoMockSource";

        private List<GameObject> _createdGameObjects;
        private List<ScriptableObject> _createdAssets;
        private List<GameObject> _spawnedAvatars;
        private List<SlotManager> _managersToDispose;
        private DemoMockMoCapSourceFactory _sourceFactory;
        private DemoMockAvatarProviderFactory _providerFactory;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _createdGameObjects = new List<GameObject>();
            _createdAssets = new List<ScriptableObject>();
            _spawnedAvatars = new List<GameObject>();
            _managersToDispose = new List<SlotManager>();

            _providerFactory = new DemoMockAvatarProviderFactory(_spawnedAvatars);
            _sourceFactory = new DemoMockMoCapSourceFactory();

            RegistryLocator.ProviderRegistry.Register(ProviderTypeId, _providerFactory);
            RegistryLocator.MoCapSourceRegistry.Register(SourceTypeId, _sourceFactory);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var manager in _managersToDispose)
            {
                try { manager?.Dispose(); }
                catch { /* TearDown の後始末: Dispose 内例外は握り潰し */ }
            }
            _managersToDispose.Clear();

            foreach (var go in _createdGameObjects)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _createdGameObjects.Clear();

            foreach (var avatar in _spawnedAvatars)
            {
                if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            }
            _spawnedAvatars.Clear();

            foreach (var so in _createdAssets)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _createdAssets.Clear();

            RegistryLocator.ResetForTest();
        }

        // --- Test 1: デモシーン起動確認 (design.md §11.2) ---

        /// <summary>
        /// <see cref="SlotManagerBehaviour"/> が <c>Awake</c> で初期化エラーなく
        /// <see cref="SlotManager"/> を構築すること。
        /// <c>initialSlots</c> が未設定の素の状態でも Awake が成功する経路を検証する
        /// (デモシーン起動確認の同等検証)。
        /// </summary>
        [UnityTest]
        public IEnumerator SlotManagerBehaviour_Awake_InitializesSlotManagerWithoutErrors()
        {
            var host = new GameObject("[SlotManager] (Test Host)");
            _createdGameObjects.Add(host);

            SlotManagerBehaviour behaviour = null;
            Assert.DoesNotThrow(
                () => behaviour = host.AddComponent<SlotManagerBehaviour>(),
                "AddComponent<SlotManagerBehaviour>() (= Awake) は例外なく完了するべき");

            // Unity は AddComponent で Awake を同期実行するが、フレーム境界を跨いだ副作用がある
            // 場合に備えて 1 フレーム進めてから検証する。
            yield return null;

            Assert.That(behaviour, Is.Not.Null,
                "AddComponent の結果 SlotManagerBehaviour インスタンスが取得できるべき");
            Assert.That(behaviour.SlotManager, Is.Not.Null,
                "Awake 完了後に SlotManager プロパティが初期化されているべき (design.md §11.2)");
        }

        // --- Test 2: 参照共有シナリオ再現 (design.md §11.2 / §10 / Req 10.1) ---

        /// <summary>
        /// 同一 <see cref="MoCapSourceDescriptor"/> (同一 Config SO アセット参照) を持つ 2 件の
        /// <see cref="SlotSettings"/> を <see cref="SlotManager.AddSlotAsync"/> した後、
        /// <see cref="IMoCapSourceRegistry"/> が同一 <see cref="IMoCapSource"/> インスタンスを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator SameMoCapSourceDescriptor_AddTwoSlots_SharesMoCapSourceInstance()
            => UniTask.ToCoroutine(async () =>
            {
                var manager = new SlotManager(
                    RegistryLocator.ProviderRegistry,
                    RegistryLocator.MoCapSourceRegistry,
                    RegistryLocator.ErrorChannel);
                _managersToDispose.Add(manager);

                // 参照共有の成立には MoCapSourceDescriptor.Equals (Config の参照等価) を成立させる
                // 必要があるため、両 Descriptor から単一 Config SO インスタンスを参照させる。
                var sharedSourceConfig = ScriptableObject.CreateInstance<DemoMockMoCapSourceConfig>();
                _createdAssets.Add(sharedSourceConfig);

                var s1 = CreateRuntimeSettings("shared-slot-01", "Share Slot 1 (AvatarA)", sharedSourceConfig);
                var s2 = CreateRuntimeSettings("shared-slot-02", "Share Slot 2 (AvatarB)", sharedSourceConfig);

                await manager.AddSlotAsync(s1);
                await manager.AddSlotAsync(s2);

                Assert.That(_sourceFactory.CreatedSources, Has.Count.EqualTo(1),
                    "同一 MoCapSourceDescriptor に対して Factory.Create は 1 回だけ呼ばれるべき (Req 10.1)");

                // Registry に直接問い合わせて参照共有を確認する。
                // Resolve は参照カウントをインクリメントするため、後続で同数の Release を呼ぶ必要がある。
                var descriptor = s1.moCapSourceDescriptor;
                var first = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
                var second = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
                try
                {
                    Assert.That(first, Is.SameAs(second),
                        "同一 Descriptor から取得した IMoCapSource は同一インスタンスであるべき");
                    Assert.That(first, Is.SameAs(_sourceFactory.CreatedSources[0]),
                        "共有インスタンスは Factory.Create が最初に返したインスタンスであるべき");
                }
                finally
                {
                    RegistryLocator.MoCapSourceRegistry.Release(first);
                    RegistryLocator.MoCapSourceRegistry.Release(second);
                }
            });

        // --- Test 3: Slot 削除後のエラーチャンネル (design.md §11.2) ---

        /// <summary>
        /// 正常な <see cref="SlotManager.AddSlotAsync"/> → <see cref="SlotManager.RemoveSlotAsync"/>
        /// の一連シーケンス中に <see cref="ISlotErrorChannel.Errors"/> へ不要なエラーが発行されないこと。
        /// </summary>
        [UnityTest]
        public IEnumerator RemoveSlot_DoesNotPublishUnexpectedErrors()
            => UniTask.ToCoroutine(async () =>
            {
                var collectedErrors = new List<SlotError>();
                IDisposable subscription = RegistryLocator.ErrorChannel.Errors.Subscribe(collectedErrors.Add);
                try
                {
                    var manager = new SlotManager(
                        RegistryLocator.ProviderRegistry,
                        RegistryLocator.MoCapSourceRegistry,
                        RegistryLocator.ErrorChannel);
                    _managersToDispose.Add(manager);

                    var sourceConfig = ScriptableObject.CreateInstance<DemoMockMoCapSourceConfig>();
                    _createdAssets.Add(sourceConfig);

                    var settings = CreateRuntimeSettings("slot-remove-01", "Slot to Remove", sourceConfig);
                    await manager.AddSlotAsync(settings);

                    Assert.That(collectedErrors, Is.Empty,
                        "正常な AddSlotAsync 完了時点で ErrorChannel にエラーは発行されないはず");

                    await manager.RemoveSlotAsync("slot-remove-01");

                    Assert.That(collectedErrors, Is.Empty,
                        "Slot 削除後も ISlotErrorChannel.Errors に不要なエラーが発行されてはならない (design.md §11.2)");
                }
                finally
                {
                    subscription?.Dispose();
                }
            });

        // --- Helpers ---

        private SlotSettings CreateRuntimeSettings(
            string slotId, string displayName, DemoMockMoCapSourceConfig sourceConfig)
        {
            var providerConfig = ScriptableObject.CreateInstance<DemoMockProviderConfig>();
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
                SourceTypeId = SourceTypeId,
                Config = sourceConfig,
            };
            return settings;
        }

        // --- PlayMode 専用 Mock 群 (テスト asmdef に閉じる) ---

        private sealed class DemoMockProviderConfig : ProviderConfigBase { }
        private sealed class DemoMockMoCapSourceConfig : MoCapSourceConfigBase { }

        private sealed class DemoMockAvatarProviderFactory : IAvatarProviderFactory
        {
            private readonly List<GameObject> _avatarSink;

            public DemoMockAvatarProviderFactory(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public IAvatarProvider Create(ProviderConfigBase config)
                => new DemoMockAvatarProvider(_avatarSink);
        }

        private sealed class DemoMockAvatarProvider : IAvatarProvider
        {
            private readonly List<GameObject> _avatarSink;

            public DemoMockAvatarProvider(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public string ProviderType => "DemoMock";

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
                var go = new GameObject("DemoMockAvatar");
                _avatarSink.Add(go);
                return go;
            }
        }

        private sealed class DemoMockMoCapSourceFactory : IMoCapSourceFactory
        {
            public List<DemoMockMoCapSource> CreatedSources { get; }
                = new List<DemoMockMoCapSource>();

            public IMoCapSource Create(MoCapSourceConfigBase config)
            {
                var source = new DemoMockMoCapSource();
                CreatedSources.Add(source);
                return source;
            }
        }

        private sealed class DemoMockMoCapSource : IMoCapSource
        {
            private readonly Subject<MotionFrame> _subject = new Subject<MotionFrame>();

            public int DisposeCount { get; private set; }
            public string SourceType => "DemoMock";
            public IObservable<MotionFrame> MotionStream => _subject;

            public void Initialize(MoCapSourceConfigBase config) { }
            public void Shutdown() { }

            public void Dispose()
            {
                DisposeCount++;
                _subject.OnCompleted();
                _subject.Dispose();
            }
        }
    }
}
