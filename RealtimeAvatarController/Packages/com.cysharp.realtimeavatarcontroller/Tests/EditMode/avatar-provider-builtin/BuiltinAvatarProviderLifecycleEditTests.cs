using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProvider のライフサイクル EditMode 単体テスト
    /// (tasks.md T-6-5 / design.md §3.2・§8 エラーパターン表)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="BuiltinAvatarProvider.ReleaseAvatar"/> が未管理の <see cref="GameObject"/>
    ///     を受け取った場合に <see cref="Debug.LogError"/> を出力し、対象を破棄しないこと
    ///     (T-6-5 ReleaseAvatar_UnmanagedObject_LogsErrorAndDoesNotDestroy / Req 4 AC 4)。
    ///   - <see cref="BuiltinAvatarProvider.Dispose"/> 後に <see cref="BuiltinAvatarProvider.RequestAvatar"/>
    ///     を呼び出すと <see cref="ObjectDisposedException"/> がスローされること
    ///     (T-6-5 Dispose_MarksProviderAsDisposed / Req 4 AC 5)。
    ///   - Dispose 後の <see cref="BuiltinAvatarProvider.RequestAvatar"/> 呼び出しでは
    ///     <see cref="ISlotErrorChannel"/> への発行が行われないこと
    ///     (T-6-5 RequestAvatar_AfterDispose_ThrowsObjectDisposedException /
    ///     design.md §8 エラーパターン表: Disposed 遷移は ErrorChannel を経由しない)。
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md T-6 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - <see cref="ISlotErrorChannel"/> モック <see cref="FakeSlotErrorChannel"/> を
    ///     <see cref="BuiltinAvatarProvider"/> のコンストラクタに直接注入し、
    ///     Dispose 後の発行有無 (本テストでは発行されないこと) を検証する。
    ///   - EditMode では Prefab インスタンス化は行わず、
    ///     <see cref="Debug.LogError"/> の発行は <see cref="LogAssert.Expect(LogType, Regex)"/> で確認する。
    ///     PlayMode ランタイムに依存しない形式で EditMode での振る舞いを保証する。
    /// </para>
    ///
    /// Requirements: Req 4 AC 4, Req 4 AC 5, Req 9 AC 2
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderLifecycleEditTests
    {
        private BuiltinAvatarProviderConfig _config;
        private FakeSlotErrorChannel _fakeChannel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _fakeChannel = new FakeSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(_fakeChannel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void ReleaseAvatar_UnmanagedObject_LogsErrorAndDoesNotDestroy()
        {
            // Provider が供給していない (_managedAvatars に未登録の) GameObject を渡した場合、
            // ReleaseAvatar は Debug.LogError を出力し、対象を破棄せずに早期リターンする
            // (BuiltinAvatarProvider.ReleaseAvatar の契約 / Req 4 AC 4)。
            var provider = new BuiltinAvatarProvider(_config, _fakeChannel);
            var unmanaged = new GameObject("UnmanagedAvatar_T-6-5");
            try
            {
                // UnityEngine.TestTools.LogAssert により期待するエラーログを登録する。
                // このログが Unity Console に出力されないと TearDown で Assert 失敗となるため
                // "Debug.LogError が呼び出されたこと" の検証として機能する。
                LogAssert.Expect(
                    LogType.Error,
                    new Regex(@"\[BuiltinAvatarProvider\] ReleaseAvatar: 未管理の GameObject"));

                provider.ReleaseAvatar(unmanaged);

                // 早期リターンにより unmanaged は破棄されない。
                // GameObject の Unity オーバーロード null 判定 ("!unmanaged") および
                // C# 参照 null 判定の双方で生存を確認する。
                Assert.IsNotNull(unmanaged,
                    "未管理 GameObject を渡した ReleaseAvatar は対象を破棄してはならない。");
                Assert.IsTrue(unmanaged, "未管理 GameObject は Unity 側でも生存している必要がある。");
            }
            finally
            {
                if (unmanaged != null)
                {
                    UnityEngine.Object.DestroyImmediate(unmanaged);
                }
                provider.Dispose();
            }
        }

        [Test]
        public void Dispose_MarksProviderAsDisposed()
        {
            // Dispose() 後の Provider は Disposed 状態に遷移し、
            // 以降の RequestAvatar 呼び出しは ObjectDisposedException をスローする
            // (BuiltinAvatarProvider.ThrowIfDisposed の契約 / Req 4 AC 5)。
            var provider = new BuiltinAvatarProvider(_config, _fakeChannel);

            provider.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => provider.RequestAvatar(_config),
                "Dispose() 後の RequestAvatar() は ObjectDisposedException をスローするべき。");
        }

        [Test]
        public void RequestAvatar_AfterDispose_ThrowsObjectDisposedException()
        {
            // design.md §8 エラーパターン表:
            //   Disposed 状態での RequestAvatar は ThrowIfDisposed() により
            //   try ブロック前段で ObjectDisposedException をスローするため、
            //   ErrorChannel への SlotError Publish は行われない。
            //   ここでは例外のスローと ErrorChannel 未発行の双方を確認する。
            var provider = new BuiltinAvatarProvider(_config, _fakeChannel);
            provider.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => provider.RequestAvatar(_config),
                "Dispose() 後の RequestAvatar() は ObjectDisposedException をスローするべき。");

            Assert.IsFalse(_fakeChannel.HasReceived(SlotErrorCategory.InitFailure),
                "Disposed 遷移では ErrorChannel に InitFailure を発行してはならない (design.md §8)。");
            Assert.AreEqual(0, _fakeChannel.Received.Count,
                "Disposed 遷移ではいかなる SlotError も ErrorChannel に発行されてはならない。");
        }

        // --- テストヘルパー ---

        /// <summary>
        /// <see cref="ISlotErrorChannel"/> のテスト用スタブ。発行された <see cref="SlotError"/> を
        /// 順序付きリストに保持し、<see cref="HasReceived"/> で特定カテゴリの有無を検証する。
        /// UniRx 非依存とするため <see cref="Errors"/> は購読されない no-op 実装を返す。
        /// </summary>
        private sealed class FakeSlotErrorChannel : ISlotErrorChannel
        {
            private readonly List<SlotError> _received = new List<SlotError>();

            public IReadOnlyList<SlotError> Received => _received;

            public IObservable<SlotError> Errors => NoOpObservable.Instance;

            public void Publish(SlotError error)
            {
                _received.Add(error);
            }

            public bool HasReceived(SlotErrorCategory category)
            {
                foreach (var e in _received)
                {
                    if (e.Category == category) return true;
                }
                return false;
            }

            private sealed class NoOpObservable : IObservable<SlotError>
            {
                public static readonly NoOpObservable Instance = new NoOpObservable();
                public IDisposable Subscribe(IObserver<SlotError> observer) => NoOpDisposable.Instance;
            }

            private sealed class NoOpDisposable : IDisposable
            {
                public static readonly NoOpDisposable Instance = new NoOpDisposable();
                public void Dispose() { }
            }
        }
    }
}
