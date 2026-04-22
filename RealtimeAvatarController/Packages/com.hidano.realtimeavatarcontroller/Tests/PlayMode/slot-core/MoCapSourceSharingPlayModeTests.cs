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
    /// MoCap ソース参照共有の PlayMode 統合テスト (タスク 14.2)。
    /// Unity エンジン起動を伴う <see cref="UnityTestAttribute"/> として
    /// <see cref="IMoCapSourceRegistry"/> の参照カウント共有挙動を <see cref="SlotManager"/> 経由で検証する。
    /// <para>
    /// 検証観点 (requirements.md Req 10.1 / 10.3 / 14.2):
    /// 1. 同一 <see cref="MoCapSourceDescriptor"/> で複数 Slot を生成した場合、
    ///    各 Slot には同一 <see cref="IMoCapSource"/> インスタンスが割り当てられる。
    /// 2. 最後の Slot が削除された時点で <see cref="IMoCapSource.Dispose"/> が 1 回だけ呼ばれる。
    ///    途中経過 (1 つ目の Slot 削除時) では Dispose は呼ばれない。
    /// </para>
    /// <para>
    /// Mock 実装はテスト asmdef 内にクローズドに定義する (requirements.md Req 14.6 / design.md §10.2)。
    /// EditMode テストの <c>Mocks/</c> は <c>internal</c> 可視性のため参照せず、本 PlayMode ファイル内で
    /// 必要最小限の Mock を再定義する。<see cref="SlotLifecyclePlayModeTests"/> との Mock 重複は
    /// スコープがそれぞれ <c>private</c> ネスト型に閉じているため許容する。
    /// </para>
    /// Requirements: 10.1, 10.3, 14.2
    /// </summary>
    [TestFixture]
    public class MoCapSourceSharingPlayModeTests
    {
        private const string ProviderTypeId = "SharingMockProvider";
        private const string SourceTypeId = "SharingMockSource";

        private SlotManager _manager;
        private SharingMockMoCapSourceFactory _sourceFactory;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        // 複数 Slot で共有するための単一 MoCapSourceConfig 参照。
        // MoCapSourceDescriptor の等価判定は Config の参照等価であるため、
        // 同一インスタンスを両 Descriptor から参照させる。
        private SharingMockMoCapSourceConfig _sharedSourceConfig;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _sourceFactory = new SharingMockMoCapSourceFactory();

            RegistryLocator.ProviderRegistry.Register(
                ProviderTypeId, new SharingMockAvatarProviderFactory(_createdAvatars));
            RegistryLocator.MoCapSourceRegistry.Register(
                SourceTypeId, _sourceFactory);

            _manager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);

            _sharedSourceConfig = ScriptableObject.CreateInstance<SharingMockMoCapSourceConfig>();
            _createdAssets.Add(_sharedSourceConfig);
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
            _sharedSourceConfig = null;
            _sourceFactory = null;

            RegistryLocator.ResetForTest();
        }

        // --- 参照共有検証 (Req 10.1) ---

        [UnityTest]
        public IEnumerator AddTwoSlotsWithSameDescriptor_SharesSingleMoCapSourceInstance()
            => UniTask.ToCoroutine(async () =>
            {
                var s1 = CreateRuntimeSettings("slot-share-1", "Share 1");
                var s2 = CreateRuntimeSettings("slot-share-2", "Share 2");

                await _manager.AddSlotAsync(s1);
                await _manager.AddSlotAsync(s2);

                Assert.That(_sourceFactory.CreatedSources, Has.Count.EqualTo(1),
                    "同一 MoCapSourceDescriptor に対して Factory.Create は 1 回だけ呼ばれること (Req 10.1)");

                // 内部解決を直接 Registry に問い合わせて共有インスタンスを確認する。
                // Resolve は参照カウントをインクリメントするため、検証後に同数の Release を呼ぶ必要がある。
                var descriptor = s1.moCapSourceDescriptor;
                var first = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
                var second = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
                try
                {
                    Assert.That(first, Is.SameAs(second),
                        "同一 Descriptor から取得した IMoCapSource は同一インスタンスであること (Req 10.1)");
                    Assert.That(first, Is.SameAs(_sourceFactory.CreatedSources[0]),
                        "共有インスタンスは Factory.Create が最初に返したインスタンスであること");
                }
                finally
                {
                    RegistryLocator.MoCapSourceRegistry.Release(first);
                    RegistryLocator.MoCapSourceRegistry.Release(second);
                }
            });

        // --- Dispose 発行タイミング検証 (Req 10.3) ---

        [UnityTest]
        public IEnumerator RemoveLastSlot_DisposesMoCapSourceExactlyOnce()
            => UniTask.ToCoroutine(async () =>
            {
                var s1 = CreateRuntimeSettings("slot-share-1", "Share 1");
                var s2 = CreateRuntimeSettings("slot-share-2", "Share 2");

                await _manager.AddSlotAsync(s1);
                await _manager.AddSlotAsync(s2);

                Assert.That(_sourceFactory.CreatedSources, Has.Count.EqualTo(1));
                var sharedSource = _sourceFactory.CreatedSources[0];
                Assert.That(sharedSource.DisposeCount, Is.EqualTo(0),
                    "Slot 追加フェーズでは Dispose は呼ばれないこと");

                // 1 つ目の Slot を削除しても参照カウントが残るため Dispose は呼ばれない (Req 10.3)。
                await _manager.RemoveSlotAsync("slot-share-1");
                Assert.That(sharedSource.DisposeCount, Is.EqualTo(0),
                    "最後の Slot 以外の削除では Dispose は呼ばれないこと (refCount > 0 を維持)");

                // 最後の Slot を削除した時点で参照カウントが 0 になり Dispose が 1 回呼ばれる (Req 10.3)。
                await _manager.RemoveSlotAsync("slot-share-2");
                Assert.That(sharedSource.DisposeCount, Is.EqualTo(1),
                    "最後の Slot 削除で Dispose がちょうど 1 回呼ばれること (Req 10.3)");
            });

        // --- ヘルパー ---

        private SlotSettings CreateRuntimeSettings(string slotId, string displayName)
        {
            // Provider 側の Config は Slot ごとに異なっていても良い (共有対象は MoCapSource)。
            var providerConfig = ScriptableObject.CreateInstance<SharingMockProviderConfig>();
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
                // _sharedSourceConfig を両 Descriptor から参照させることで
                // MoCapSourceDescriptor.Equals を成立させ、Registry 上でキー共有させる。
                Config = _sharedSourceConfig,
            };
            return settings;
        }

        // --- PlayMode 専用 Config / Mock 実装 ---

        private sealed class SharingMockProviderConfig : ProviderConfigBase { }
        private sealed class SharingMockMoCapSourceConfig : MoCapSourceConfigBase { }

        private sealed class SharingMockAvatarProviderFactory : IAvatarProviderFactory
        {
            private readonly List<GameObject> _avatarSink;

            public SharingMockAvatarProviderFactory(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public IAvatarProvider Create(ProviderConfigBase config)
                => new SharingMockAvatarProvider(_avatarSink);
        }

        private sealed class SharingMockAvatarProvider : IAvatarProvider
        {
            private readonly List<GameObject> _avatarSink;

            public SharingMockAvatarProvider(List<GameObject> avatarSink)
            {
                _avatarSink = avatarSink;
            }

            public string ProviderType => "SharingMock";

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
                var go = new GameObject("SharingMockAvatar");
                _avatarSink.Add(go);
                return go;
            }
        }

        private sealed class SharingMockMoCapSourceFactory : IMoCapSourceFactory
        {
            public List<SharingMockMoCapSource> CreatedSources { get; }
                = new List<SharingMockMoCapSource>();

            public IMoCapSource Create(MoCapSourceConfigBase config)
            {
                var source = new SharingMockMoCapSource();
                CreatedSources.Add(source);
                return source;
            }
        }

        private sealed class SharingMockMoCapSource : IMoCapSource
        {
            private readonly Subject<MotionFrame> _subject = new Subject<MotionFrame>();

            public int DisposeCount { get; private set; }

            public string SourceType => "SharingMock";
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
