using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProvider の null Prefab 時エラー発行 EditMode 単体テスト
    /// (tasks.md T-6-6 / design.md §5 RequestAvatar・§8 エラーパターン表 /
    /// validation-design.md Minor #2 `_errorChannel` null 安全性)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="BuiltinAvatarProviderConfig.avatarPrefab"/> が null の状態で
    ///     <see cref="BuiltinAvatarProvider.RequestAvatar"/> を呼び出した場合に
    ///     <see cref="InvalidOperationException"/> がスローされ、かつ
    ///     <see cref="ISlotErrorChannel"/> へ <see cref="SlotErrorCategory.InitFailure"/> が
    ///     発行されること (T-6-6 RequestAvatar_NullPrefab_PublishesInitFailureAndThrows /
    ///     Req 2 AC 5・Req 3 AC 4)。
    ///   - <see cref="BuiltinAvatarProvider"/> の内部フィールド <c>_errorChannel</c> が null であっても
    ///     <see cref="NullReferenceException"/> を発生させずに
    ///     <see cref="InvalidOperationException"/> を呼び出し元へ再スローすること
    ///     (<c>_errorChannel?.Publish(...)</c> の null 条件演算子による保護 /
    ///     validation-design.md Minor #2 対応)。
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md T-6 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - <see cref="ISlotErrorChannel"/> モック <see cref="FakeSlotErrorChannel"/> を
    ///     <see cref="BuiltinAvatarProvider"/> のコンストラクタに直接注入して発行内容を検証する。
    ///   - EditMode では <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> を
    ///     経由せず、try ブロック先頭の null Prefab ガードのみを検証する。
    ///     (PlayMode での統合検証は tasks.md T-7-3 で実施。)
    /// </para>
    ///
    /// Requirements: Req 2 AC 5, Req 3 AC 4, Req 9 AC 2
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderNullPrefabEditTests
    {
        private BuiltinAvatarProviderConfig _configWithNullPrefab;
        private FakeSlotErrorChannel _fakeChannel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            // CreateInstance 直後の avatarPrefab は既定で null。
            // 明示的な null 代入により「Prefab 未設定」のシナリオを確実に再現する。
            _configWithNullPrefab = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _configWithNullPrefab.avatarPrefab = null;
            _fakeChannel = new FakeSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(_fakeChannel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_configWithNullPrefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_configWithNullPrefab);
                _configWithNullPrefab = null;
            }
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void RequestAvatar_NullPrefab_PublishesInitFailureAndThrows()
        {
            // design.md §5 RequestAvatar (同期 API):
            //   try ブロック先頭の null Prefab ガードで InvalidOperationException をスロー →
            //   catch ブロックで _errorChannel.Publish(InitFailure) → 再スロー。
            // 本テストは「例外のスロー」と「InitFailure の発行」の双方を確認する。
            var provider = new BuiltinAvatarProvider(_configWithNullPrefab, _fakeChannel);

            // 1. InvalidOperationException が呼び出し元にスローされること。
            var ex = Assert.Throws<InvalidOperationException>(
                () => provider.RequestAvatar(_configWithNullPrefab),
                "avatarPrefab が null の場合は InvalidOperationException をスローするべき。");

            // 例外メッセージに avatarPrefab が null である旨が含まれていることで、
            // どの段階で失敗したかを診断可能な契約を保証する (design.md §5 擬似コード準拠)。
            StringAssert.Contains("avatarPrefab", ex.Message,
                "例外メッセージには avatarPrefab が null である旨を含めるべき。");

            // 2. FakeSlotErrorChannel に SlotErrorCategory.InitFailure が発行されていること。
            Assert.IsTrue(_fakeChannel.HasReceived(SlotErrorCategory.InitFailure),
                "null Prefab 時には catch ブロックで SlotErrorCategory.InitFailure が発行されるべき。");

            // 発行された SlotError の Exception が元の InvalidOperationException と一致し、
            // catch ブロックが例外情報を正しく搬送していることを確認する。
            var firstInitFailure = default(SlotError);
            foreach (var received in _fakeChannel.Received)
            {
                if (received.Category == SlotErrorCategory.InitFailure)
                {
                    firstInitFailure = received;
                    break;
                }
            }
            Assert.AreSame(ex, firstInitFailure.Exception,
                "発行された SlotError.Exception はスローされた InvalidOperationException と同一であるべき。");
        }

        [Test]
        public void RequestAvatar_NullErrorChannel_DoesNotThrowNullReferenceException()
        {
            // validation-design.md Minor #2:
            //   BuiltinAvatarProvider コンストラクタは `errorChannel ?? RegistryLocator.ErrorChannel`
            //   のフォールバックで _errorChannel を解決するが、理論上 _errorChannel が null に
            //   なりうるケース (Locator の ErrorChannel も null に差し替えられた状態等) でも
            //   `_errorChannel?.Publish(...)` の null 条件演算子により NullReferenceException が
            //   発生しないことを確認する。
            //
            // 実装上はコンストラクタ時点で必ず非 null が代入されるため、
            // 本テストは Reflection により _errorChannel フィールドを強制的に null へ差し替え、
            // null Prefab ガードが NRE を誘発しないことを検証する。
            var provider = new BuiltinAvatarProvider(_configWithNullPrefab, _fakeChannel);
            SetPrivateErrorChannelToNull(provider);

            // _errorChannel が null でも InvalidOperationException (NullReferenceException ではない) が
            // 呼び出し元にスローされること。
            Assert.Throws<InvalidOperationException>(
                () => provider.RequestAvatar(_configWithNullPrefab),
                "_errorChannel が null であっても InvalidOperationException のみがスローされるべき。" +
                " NullReferenceException が発生した場合は _errorChannel?.Publish の null 条件演算子保護が機能していない。");

            // _errorChannel が null であるため FakeSlotErrorChannel に発行は行われない。
            // Minor #2 の保護コード (`_errorChannel?.Publish(...)`) が no-op となることを確認する。
            Assert.IsFalse(_fakeChannel.HasReceived(SlotErrorCategory.InitFailure),
                "_errorChannel が null の場合、差し替え前に登録した FakeSlotErrorChannel には発行されないはず。");
        }

        /// <summary>
        /// <see cref="BuiltinAvatarProvider"/> の private readonly フィールド <c>_errorChannel</c> を
        /// Reflection 経由で null に差し替える。validation-design.md Minor #2 の null 条件演算子保護を
        /// EditMode で検証するためのテストダブル技法。
        /// </summary>
        private static void SetPrivateErrorChannelToNull(BuiltinAvatarProvider provider)
        {
            var field = typeof(BuiltinAvatarProvider).GetField(
                "_errorChannel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field,
                "BuiltinAvatarProvider._errorChannel フィールドが見つからない。Reflection 契約を再確認すること。");
            field.SetValue(provider, null);
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
