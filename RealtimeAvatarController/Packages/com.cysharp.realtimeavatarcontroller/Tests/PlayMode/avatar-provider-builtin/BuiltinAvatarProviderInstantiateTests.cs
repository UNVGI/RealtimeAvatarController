using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProvider の Prefab インスタンス化 PlayMode テスト
    /// (tasks.md T-7-1 / T-7-2 / design.md §4 シナリオ X・シナリオ Y・§5 RequestAvatar)。
    ///
    /// <para>
    /// 検証対象 (T-7-1 RequestAvatar_ScenarioX_InstantiatesPrefab):
    ///   - design.md §4 シナリオ X (Inspector / SO アセット経由で作成された Config) を
    ///     使用して <see cref="BuiltinAvatarProvider.RequestAvatar"/> を呼び出し、
    ///     Prefab が <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> 経由で
    ///     Scene 上に生成されること (Req 3 AC 1・Req 3 AC 2)。
    ///   - 返却された <see cref="GameObject"/> が null でなく、有効な Scene に配置されており
    ///     <see cref="Resources.FindObjectsOfTypeAll{T}"/> で列挙可能な状態にあること
    ///     (Req 9 AC 3)。
    /// </para>
    ///
    /// <para>
    /// 検証対象 (T-7-2 RequestAvatar_ScenarioY_InstantiatesPrefab):
    ///   - design.md §4 シナリオ Y (<see cref="ScriptableObject.CreateInstance{T}"/> による
    ///     ランタイム動的生成 Config + <c>config.avatarPrefab</c> への直接代入) を使用して
    ///     <see cref="BuiltinAvatarProvider.RequestAvatar"/> を呼び出し、Prefab が
    ///     <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> 経由で
    ///     Scene 上に生成されること (Req 2 AC 3・Req 3 AC 1)。
    ///   - シナリオ X と同一の結果 (null でない Scene 上の GameObject / Prefab 複製) が
    ///     得られることを確認し、Factory が生成経路に依存せず一貫した動作を示すことを
    ///     検証する (Req 9 AC 3)。
    /// </para>
    ///
    /// <para>
    /// テストダブル戦略 (tasks.md T-7 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - Prefab 実体は <see cref="GameObject.CreatePrimitive"/> で動的生成した軽量
    ///     <see cref="GameObject"/> を使用する (tasks.md T-7 前提: Unity の
    ///     <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> は Prefab に限らず
    ///     Scene 上の GameObject も複製可能なため代替として成立する)。
    ///   - シナリオ X は「Inspector で Prefab をドラッグ&ドロップした後の
    ///     <see cref="BuiltinAvatarProviderConfig"/> インスタンス」を表現する。
    ///     Inspector 編集の結果は実行時には単一の <see cref="BuiltinAvatarProviderConfig"/>
    ///     オブジェクトとして同一のメモリ表現を持つため、本テストでは
    ///     <see cref="ScriptableObject.CreateInstance{T}"/> 生成後に
    ///     Inspector 相当の Prefab 設定を施すことでシナリオ X の実行時状態を再現する。
    ///   - シナリオ Y は tasks.md T-7-2 の明示要件に従い、
    ///     <see cref="ScriptableObject.CreateInstance{T}"/> でランタイム動的生成した
    ///     Config の <c>avatarPrefab</c> フィールドに
    ///     <see cref="GameObject.CreatePrimitive"/> 相当のオブジェクトを
    ///     直接代入して構築する (design.md §4 シナリオ Y のコード例と同一経路)。
    /// </para>
    ///
    /// Requirements: Req 2 AC 3, Req 3 AC 1, Req 3 AC 2, Req 9 AC 3
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderInstantiateTests
    {
        private GameObject _scenarioXPrefab;
        private BuiltinAvatarProviderConfig _scenarioXConfig;
        private GameObject _scenarioYPrefab;
        private BuiltinAvatarProviderConfig _scenarioYConfig;
        private BuiltinAvatarProvider _provider;
        private GameObject _instantiatedAvatar;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            // tasks.md T-7 前提: Prefab 生成には CreatePrimitive で動的生成した軽量オブジェクトを使用する。
            // Unity の Object.Instantiate は Scene 上の GameObject も複製可能なため
            // Prefab アセットの代替として成立する。
            _scenarioXPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _scenarioXPrefab.name = "T71_ScenarioXPrefab";

            // シナリオ X: Inspector で Prefab をドラッグ&ドロップした直後の SO アセットと
            // 実行時同等の状態を再現する (design.md §4 シナリオ X)。
            // AssetDatabase 非依存とすることで Player ビルド・PlayMode の双方で安定動作する。
            _scenarioXConfig = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _scenarioXConfig.avatarPrefab = _scenarioXPrefab;
        }

        [TearDown]
        public void TearDown()
        {
            if (_provider != null)
            {
                _provider.Dispose();
                _provider = null;
            }

            if (_instantiatedAvatar != null)
            {
                UnityEngine.Object.DestroyImmediate(_instantiatedAvatar);
                _instantiatedAvatar = null;
            }

            if (_scenarioXPrefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_scenarioXPrefab);
                _scenarioXPrefab = null;
            }

            if (_scenarioXConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_scenarioXConfig);
                _scenarioXConfig = null;
            }

            if (_scenarioYPrefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_scenarioYPrefab);
                _scenarioYPrefab = null;
            }

            if (_scenarioYConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_scenarioYConfig);
                _scenarioYConfig = null;
            }

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void RequestAvatar_ScenarioX_InstantiatesPrefab()
        {
            // 前提: SetUp 時点でシナリオ X Config と Prefab が設定済みであること。
            Assert.IsNotNull(_scenarioXConfig,
                "シナリオ X 用の BuiltinAvatarProviderConfig が SetUp で初期化されていない。");
            Assert.IsNotNull(_scenarioXConfig.avatarPrefab,
                "シナリオ X Config の avatarPrefab が SetUp で正しく設定されていない。");

            // errorChannel は null を渡し、RegistryLocator.ErrorChannel にフォールバックさせる
            // (design.md §5 コンストラクタ仕様)。T-7-1 では ErrorChannel の検証対象外であるため、
            // 明示的に null を渡してフォールバック経路を通すことで本番同等の構成を再現する。
            _provider = new BuiltinAvatarProvider(_scenarioXConfig, errorChannel: null);

            // 実行: シナリオ X Config で RequestAvatar() を呼び出す。
            _instantiatedAvatar = _provider.RequestAvatar(_scenarioXConfig);

            // 1. 返却 GameObject が有効であること (C# null / Unity null 両判定)。
            Assert.IsNotNull(_instantiatedAvatar,
                "RequestAvatar は null でない GameObject を返すべき (Req 3 AC 1)。");
            Assert.IsTrue(_instantiatedAvatar,
                "返却された GameObject は Unity の生存判定 (operator true) で true であるべき。");

            // 2. Prefab 参照そのものではなく Object.Instantiate の複製であること。
            Assert.AreNotSame(_scenarioXConfig.avatarPrefab, _instantiatedAvatar,
                "RequestAvatar は Prefab 参照そのものではなく Object.Instantiate による複製を返すべき。");

            // 3. Scene 上に配置されていること: Unity が Scene に追加した GameObject は
            //    有効な Scene ハンドルを持つ (design.md §5 RequestAvatar の契約 / Req 3 AC 2)。
            Assert.IsTrue(_instantiatedAvatar.scene.IsValid(),
                "インスタンス化された GameObject は有効な Scene に配置されているべき。");

            // 4. Resources.FindObjectsOfTypeAll でも列挙可能であること
            //    (tasks.md T-7-1 明示要件: 「Resources.FindObjectsOfTypeAll 等で Scene に存在することを確認する」)。
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var foundInAllObjects = false;
            for (var i = 0; i < allGameObjects.Length; i++)
            {
                if (ReferenceEquals(allGameObjects[i], _instantiatedAvatar))
                {
                    foundInAllObjects = true;
                    break;
                }
            }
            Assert.IsTrue(foundInAllObjects,
                "Resources.FindObjectsOfTypeAll でインスタンス化された GameObject を列挙できるべき。");
        }

        [Test]
        public void RequestAvatar_ScenarioY_InstantiatesPrefab()
        {
            // シナリオ Y: ランタイム動的生成 (design.md §4 コード例)。
            //   var config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            //   config.avatarPrefab = someAvatarPrefab;
            // 上記と同一経路で Config を構築し、Factory / Provider が生成経路に依存せず
            // 同一の RequestAvatar 動作を示すことを確認する (Req 2 AC 3 / Req 9 AC 3)。
            _scenarioYPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _scenarioYPrefab.name = "T72_ScenarioYPrefab";

            _scenarioYConfig = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _scenarioYConfig.avatarPrefab = _scenarioYPrefab;

            // 前提: シナリオ Y Config と Prefab が正しく構築されたこと。
            Assert.IsNotNull(_scenarioYConfig,
                "シナリオ Y 用の BuiltinAvatarProviderConfig は ScriptableObject.CreateInstance で生成できるべき。");
            Assert.IsNotNull(_scenarioYConfig.avatarPrefab,
                "シナリオ Y Config の avatarPrefab はランタイム直接代入で設定されているべき。");

            _provider = new BuiltinAvatarProvider(_scenarioYConfig, errorChannel: null);

            // 実行: シナリオ Y Config で RequestAvatar() を呼び出す。
            _instantiatedAvatar = _provider.RequestAvatar(_scenarioYConfig);

            // 1. 返却 GameObject が有効であること (C# null / Unity null 両判定)。
            Assert.IsNotNull(_instantiatedAvatar,
                "RequestAvatar はシナリオ Y Config に対しても null でない GameObject を返すべき (Req 3 AC 1)。");
            Assert.IsTrue(_instantiatedAvatar,
                "返却された GameObject は Unity の生存判定 (operator true) で true であるべき。");

            // 2. Prefab 参照そのものではなく Object.Instantiate の複製であること
            //    (シナリオ X と同一の契約: Req 3 AC 1 Instantiate 経路)。
            Assert.AreNotSame(_scenarioYConfig.avatarPrefab, _instantiatedAvatar,
                "RequestAvatar は Prefab 参照そのものではなく Object.Instantiate による複製を返すべき。");

            // 3. Scene 上に配置されていること (design.md §5 RequestAvatar の契約)。
            Assert.IsTrue(_instantiatedAvatar.scene.IsValid(),
                "インスタンス化された GameObject は有効な Scene に配置されているべき。");

            // 4. Resources.FindObjectsOfTypeAll でも列挙可能であること
            //    (シナリオ X と同一の結果が得られることの確認: tasks.md T-7-2)。
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var foundInAllObjects = false;
            for (var i = 0; i < allGameObjects.Length; i++)
            {
                if (ReferenceEquals(allGameObjects[i], _instantiatedAvatar))
                {
                    foundInAllObjects = true;
                    break;
                }
            }
            Assert.IsTrue(foundInAllObjects,
                "Resources.FindObjectsOfTypeAll でシナリオ Y 由来の GameObject を列挙できるべき。");
        }
    }
}
