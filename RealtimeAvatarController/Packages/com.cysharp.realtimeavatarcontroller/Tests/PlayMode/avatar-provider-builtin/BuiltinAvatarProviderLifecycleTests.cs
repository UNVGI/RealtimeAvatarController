using System.Collections;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProvider のライフサイクル PlayMode テスト
    /// (tasks.md T-7-4 / design.md §11.2 テストケース一覧 / Req 4 AC 1・Req 4 AC 2・Req 9 AC 3)。
    ///
    /// <para>
    /// 検証対象 (T-7-4 ReleaseAvatar_DestroysGameObject):
    ///   - <see cref="BuiltinAvatarProvider.RequestAvatar"/> で取得したアバター
    ///     <see cref="GameObject"/> に対して <see cref="BuiltinAvatarProvider.ReleaseAvatar"/> を
    ///     呼び出した場合に、対象 <see cref="GameObject"/> が
    ///     <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> 経由で Scene から
    ///     除去されていること (Req 4 AC 1) を実 Unity ランタイム環境で検証する。
    ///   - <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> の反映は end-of-frame まで
    ///     遅延されるため、<c>yield return null</c> で 1 フレーム経過させた上で
    ///     Unity の overloaded operator== (<c>gameObject == null</c>) および implicit
    ///     <see cref="bool"/> 変換 (<c>!gameObject</c>) の双方で破棄済み状態を確認する
    ///     (tasks.md T-7-4 明示要件: 「<c>gameObject == null</c> または <c>!gameObject</c>
    ///     で確認」)。
    ///   - 内部追跡セット (<c>_managedAvatars</c>) からの除去は後続の
    ///     <see cref="BuiltinAvatarProvider.Dispose"/> 呼び出し時に例外や二重破棄が
    ///     発生しないことで間接的に確認する (Req 4 AC 2 の観測可能副作用)。
    /// </para>
    ///
    /// <para>
    /// テストダブル戦略 (tasks.md T-7 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - Prefab 実体は <see cref="GameObject.CreatePrimitive"/> で動的生成した軽量
    ///     <see cref="GameObject"/> を使用する (design.md §11.2 テストセットアップ)。
    ///   - <see cref="BuiltinAvatarProviderConfig"/> は
    ///     <see cref="ScriptableObject.CreateInstance{T}"/> で生成し、AssetDatabase 非依存と
    ///     することで PlayMode / Player ビルドの双方で安定動作させる。
    ///   - T-7-4 では <see cref="ISlotErrorChannel"/> を検証対象としないため、コンストラクタ
    ///     引数に null を渡して <see cref="RegistryLocator.ErrorChannel"/> フォールバック
    ///     経路を通す (design.md §5 コンストラクタ仕様)。
    /// </para>
    ///
    /// Requirements: Req 4 AC 1, Req 4 AC 2, Req 9 AC 3
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderLifecycleTests
    {
        private GameObject _prefab;
        private BuiltinAvatarProviderConfig _config;
        private BuiltinAvatarProvider _provider;
        private GameObject _instantiatedAvatar;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            // tasks.md T-7 前提: Prefab 生成には CreatePrimitive で動的生成した軽量オブジェクトを使用する。
            // Unity の Object.Instantiate は Scene 上の GameObject も複製可能なため
            // Prefab アセットの代替として成立する。
            _prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _prefab.name = "T74_Prefab";

            _config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _config.avatarPrefab = _prefab;
        }

        [TearDown]
        public void TearDown()
        {
            if (_provider != null)
            {
                _provider.Dispose();
                _provider = null;
            }

            // _instantiatedAvatar は正常系では ReleaseAvatar 経由で既に破棄済みのはず。
            // Unity の overloaded operator== により destroyed オブジェクトは null 相当となり
            // 以下の条件は false に評価されるため二重破棄は発生しない。
            if (_instantiatedAvatar != null)
            {
                UnityEngine.Object.DestroyImmediate(_instantiatedAvatar);
                _instantiatedAvatar = null;
            }

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            RegistryLocator.ResetForTest();
        }

        [UnityTest]
        public IEnumerator ReleaseAvatar_DestroysGameObject()
        {
            // Arrange: Provider を構築して RequestAvatar でアバターを生成する。
            _provider = new BuiltinAvatarProvider(_config, errorChannel: null);
            _instantiatedAvatar = _provider.RequestAvatar(_config);

            Assert.IsNotNull(_instantiatedAvatar,
                "前提: RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsTrue(_instantiatedAvatar,
                "前提: 返却された GameObject は Unity の生存判定 (operator true) で true であるべき。");

            // 参照を保持したまま Release 後の状態を検証するため、ローカル変数へコピーする。
            // _instantiatedAvatar は TearDown のクリーンアップ対象としての役割を引き続き持つ。
            var releasedAvatar = _instantiatedAvatar;

            // Act: ReleaseAvatar を呼び出す。
            _provider.ReleaseAvatar(releasedAvatar);

            // Object.Destroy は end-of-frame で反映されるため、1 フレーム経過させる
            // (design.md §5 ReleaseAvatar の契約 / Req 4 AC 1)。
            yield return null;

            // Assert 1: Unity の overloaded operator== による null 判定で破棄済みであること。
            Assert.IsTrue(releasedAvatar == null,
                "ReleaseAvatar 後、GameObject は Unity の operator== による null 判定で true になるべき (Req 4 AC 1)。");

            // Assert 2: implicit bool 変換 (!gameObject) でも破棄済みであること
            //          (tasks.md T-7-4 明示要件: 「gameObject == null または !gameObject で確認」)。
            Assert.IsFalse((bool)releasedAvatar,
                "ReleaseAvatar 後、GameObject は Unity の implicit bool 変換で false になるべき (!gameObject が true)。");

            // Assert 3: 内部追跡セットから除去されていること (Req 4 AC 2) は、
            //          同一 GameObject を再度 ReleaseAvatar に渡した際に
            //          「未管理オブジェクト」として扱われることで間接的に確認できる
            //          (BuiltinAvatarProvider.ReleaseAvatar の未管理判定ロジック / design.md §5)。
            //          二度目の ReleaseAvatar が例外を投げずに早期リターンすることを確認する。
            LogAssert.Expect(LogType.Error,
                "[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
            Assert.DoesNotThrow(() => _provider.ReleaseAvatar(releasedAvatar),
                "管理セットから除去された GameObject への再 ReleaseAvatar は例外を投げずに早期リターンするべき (Req 4 AC 2)。");
        }
    }
}
