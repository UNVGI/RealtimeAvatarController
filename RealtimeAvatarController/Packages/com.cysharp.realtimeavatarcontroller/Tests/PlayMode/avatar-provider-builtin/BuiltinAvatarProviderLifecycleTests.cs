using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProvider のライフサイクル PlayMode テスト
    /// (tasks.md T-7-4 / T-7-5 / T-7-6 / T-7-7 / design.md §5 Dispose・§5 RequestAvatarAsync・
    /// §2 1 Slot 1 インスタンス原則・§11.2 テストケース一覧 /
    /// Req 4 AC 1・Req 4 AC 2・Req 4 AC 3・Req 4 AC 6・Req 5 AC 4・
    /// Req 6 AC 1・Req 6 AC 2・Req 9 AC 3)。
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
    /// 検証対象 (T-7-5 Dispose_DestroysAllManagedAvatars):
    ///   - 複数回の <see cref="BuiltinAvatarProvider.RequestAvatar"/> で取得した
    ///     <see cref="GameObject"/> が内部追跡セット (<c>_managedAvatars</c>) に蓄積された
    ///     状態で <see cref="BuiltinAvatarProvider.Dispose"/> を呼び出した場合に、
    ///     追跡中の全 <see cref="GameObject"/> が
    ///     <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> 経由で破棄される
    ///     こと (Req 4 AC 3 / design.md §5 Dispose) を実 Unity ランタイム環境で検証する。
    ///   - <see cref="BuiltinAvatarProvider.Dispose"/> は
    ///     <c>_managedAvatars.ToArray()</c> でスナップショットを取得してから
    ///     <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> をループ発行するため、
    ///     <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> の end-of-frame 遅延を
    ///     考慮して <c>yield return null</c> で 1 フレーム経過させた上で全参照が
    ///     Unity の overloaded operator== および implicit <see cref="bool"/> 変換で
    ///     破棄済みとなることを確認する。
    ///   - 1 Slot 1 インスタンス原則 (design.md §5 / Req 5 AC 4) に反しない形で、
    ///     同一 Provider インスタンスが複数のアバターを同時管理するケースを模擬する
    ///     (本テストでは意図的に同一 Provider から複数の <see cref="GameObject"/> を
    ///     生成し、<c>_managedAvatars</c> の一括破棄挙動のみに焦点を当てる)。
    /// </para>
    ///
    /// <para>
    /// 検証対象 (T-7-6 RequestAvatarAsync_ReturnsInstantiatedPrefab):
    ///   - <see cref="BuiltinAvatarProvider.RequestAvatarAsync"/> を <c>await</c> した
    ///     結果として、同期版 <see cref="BuiltinAvatarProvider.RequestAvatar"/> と
    ///     同様にインスタンス化された有効な <see cref="GameObject"/> が返ること
    ///     (Req 6 AC 1) を実 Unity ランタイム環境で検証する。
    ///   - design.md §5 RequestAvatarAsync の実装契約
    ///     (<c>UniTask.FromResult(RequestAvatar(config))</c>) に従い、非同期 API が
    ///     <see cref="UniTask.FromResult{T}(T)"/> によって即時完了すること
    ///     (Req 6 AC 2) を確認する。具体的には
    ///     <see cref="UniTask{T}.Status"/> が
    ///     <see cref="UniTaskStatus.Succeeded"/> 相当となり、追加フレームを消費せずに
    ///     完了することを <c>yield return null</c> を挟まない同一フレーム完了として観測する。
    ///   - 返却 <see cref="GameObject"/> が Prefab 参照そのものではなく
    ///     <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> による複製で
    ///     あること・<c>_managedAvatars</c> に追跡登録されていること
    ///     (同期版 <see cref="BuiltinAvatarProvider.RequestAvatar"/> と完全に同一の
    ///     副作用を持つこと) を、後続の
    ///     <see cref="BuiltinAvatarProvider.ReleaseAvatar"/> が未管理エラーを
    ///     発生させずに破棄できることで間接的に確認する。
    /// </para>
    ///
    /// <para>
    /// 検証対象 (T-7-7 MultipleSlots_ReceiveIndependentInstances):
    ///   - 2 つの独立した <see cref="BuiltinAvatarProvider"/> インスタンスを構築し、
    ///     それぞれから <see cref="BuiltinAvatarProvider.RequestAvatar"/> を呼び出した場合に、
    ///     返却される <see cref="GameObject"/> が互いに異なる参照となる
    ///     (<see cref="Assert.AreNotSame(object, object)"/>) ことを検証する
    ///     (design.md §2 / Req 4 AC 6 / Req 5 AC 4: 1 Slot 1 インスタンス原則)。
    ///   - 各 Slot が独立した <see cref="BuiltinAvatarProvider"/> インスタンスを保有する
    ///     アーキテクチャ (<c>IMoCapSource</c> の参照共有モデルを採用しない設計)
    ///     において、各 Provider の <c>_managedAvatars</c> 追跡セットが独立している
    ///     ことを間接的に検証する — 他 Provider が生成したアバターを
    ///     <see cref="BuiltinAvatarProvider.ReleaseAvatar"/> に渡した場合に
    ///     「未管理の GameObject」エラーログが発行され、破棄されずに早期リターンする
    ///     ことを <see cref="LogAssert.Expect(LogType, string)"/> で確認する
    ///     (design.md §5 ReleaseAvatar 契約)。
    ///   - 本テストは 1 Slot 1 インスタンス原則を観測可能副作用として検証する目的で、
    ///     同一 <see cref="BuiltinAvatarProviderConfig"/> を共有する 2 Provider 構成を
    ///     意図的に採用する (Config 共有は Provider インスタンスの独立性を損なわない
    ///     ことを確認するため: design.md §5 コンストラクタ仕様)。
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
    ///   - T-7-4 / T-7-5 / T-7-6 / T-7-7 では <see cref="ISlotErrorChannel"/> を検証対象としないため、
    ///     コンストラクタ引数に null を渡して
    ///     <see cref="RegistryLocator.ErrorChannel"/> フォールバック経路を通す
    ///     (design.md §5 コンストラクタ仕様)。
    ///   - T-7-6 の async / await 実行は
    ///     <see cref="UniTask.ToCoroutine(System.Func{UniTask})"/> 経由で
    ///     <see cref="UnityTestAttribute"/> の <see cref="IEnumerator"/> に橋渡しし、
    ///     Unity Test Runner の PlayMode 環境で UniTask を正しくハンドリングする。
    /// </para>
    ///
    /// Requirements: Req 4 AC 1, Req 4 AC 2, Req 4 AC 3, Req 4 AC 6, Req 5 AC 4,
    /// Req 6 AC 1, Req 6 AC 2, Req 9 AC 3
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

        [UnityTest]
        public IEnumerator Dispose_DestroysAllManagedAvatars()
        {
            // Arrange: Provider を構築し、複数の RequestAvatar 呼び出しで
            //          _managedAvatars に複数アバターを蓄積する (Req 4 AC 3 前提状態)。
            _provider = new BuiltinAvatarProvider(_config, errorChannel: null);

            var managedAvatar1 = _provider.RequestAvatar(_config);
            var managedAvatar2 = _provider.RequestAvatar(_config);
            var managedAvatar3 = _provider.RequestAvatar(_config);

            Assert.IsNotNull(managedAvatar1,
                "前提: 1 回目の RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsNotNull(managedAvatar2,
                "前提: 2 回目の RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsNotNull(managedAvatar3,
                "前提: 3 回目の RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsTrue(managedAvatar1,
                "前提: managedAvatar1 は Unity の生存判定 (operator true) で true であるべき。");
            Assert.IsTrue(managedAvatar2,
                "前提: managedAvatar2 は Unity の生存判定 (operator true) で true であるべき。");
            Assert.IsTrue(managedAvatar3,
                "前提: managedAvatar3 は Unity の生存判定 (operator true) で true であるべき。");
            Assert.AreNotSame(managedAvatar1, managedAvatar2,
                "前提: 各 RequestAvatar は独立した GameObject インスタンスを返すべき (1,2)。");
            Assert.AreNotSame(managedAvatar2, managedAvatar3,
                "前提: 各 RequestAvatar は独立した GameObject インスタンスを返すべき (2,3)。");
            Assert.AreNotSame(managedAvatar1, managedAvatar3,
                "前提: 各 RequestAvatar は独立した GameObject インスタンスを返すべき (1,3)。");

            // Act: Dispose() を呼び出し、追跡セット内の全 GameObject を破棄する
            //      (design.md §5 Dispose / Req 4 AC 3)。
            _provider.Dispose();

            // Object.Destroy は end-of-frame で反映されるため、1 フレーム経過させる。
            yield return null;

            // Assert 1: 追跡中の全 GameObject が Unity の overloaded operator== による
            //           null 判定で破棄済みとなっていること (Req 4 AC 3)。
            Assert.IsTrue(managedAvatar1 == null,
                "Dispose 後、managedAvatar1 は Unity operator== による null 判定で true になるべき (Req 4 AC 3)。");
            Assert.IsTrue(managedAvatar2 == null,
                "Dispose 後、managedAvatar2 は Unity operator== による null 判定で true になるべき (Req 4 AC 3)。");
            Assert.IsTrue(managedAvatar3 == null,
                "Dispose 後、managedAvatar3 は Unity operator== による null 判定で true になるべき (Req 4 AC 3)。");

            // Assert 2: implicit bool 変換でも破棄済みであること
            //           (T-7-4 と同一の二段検証パターン)。
            Assert.IsFalse((bool)managedAvatar1,
                "Dispose 後、managedAvatar1 は Unity の implicit bool 変換で false になるべき (!gameObject が true)。");
            Assert.IsFalse((bool)managedAvatar2,
                "Dispose 後、managedAvatar2 は Unity の implicit bool 変換で false になるべき (!gameObject が true)。");
            Assert.IsFalse((bool)managedAvatar3,
                "Dispose 後、managedAvatar3 は Unity の implicit bool 変換で false になるべき (!gameObject が true)。");

            // Assert 3: 追跡セットがクリアされていることの間接確認 —
            //           破棄済み GameObject に対する再 ReleaseAvatar が
            //           「未管理」として早期リターンすること
            //           (Dispose 内で _managedAvatars.Clear() が呼ばれた結果 / design.md §5)。
            LogAssert.Expect(LogType.Error,
                "[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
            Assert.DoesNotThrow(() => _provider.ReleaseAvatar(managedAvatar1),
                "Dispose 後に同一 GameObject を ReleaseAvatar しても例外を投げずに早期リターンするべき (追跡セットがクリアされているため未管理扱い)。");
        }

        [UnityTest]
        public IEnumerator RequestAvatarAsync_ReturnsInstantiatedPrefab() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: Provider を構築する。
            // errorChannel は T-7-4 / T-7-5 と同一の null 指定 (RegistryLocator.ErrorChannel フォールバック) を用い、
            // ライフサイクルフィクスチャ全体で一貫したコンストラクタ構成を維持する (design.md §5)。
            _provider = new BuiltinAvatarProvider(_config, errorChannel: null);

            // Act: RequestAvatarAsync を await し、同一フレーム内で即時完了することを検証する。
            // design.md §5 RequestAvatarAsync: UniTask.FromResult(RequestAvatar(config)) による
            // 即時完了ラップのため、async / await 境界を超えても追加フレームは消費されない (Req 6 AC 2)。
            var task = _provider.RequestAvatarAsync(_config);

            // UniTask.Status が Succeeded となっていること (即時完了の観測可能副作用: Req 6 AC 2)。
            // UniTask.FromResult は完了済みの UniTask を返すため、await 前の時点で Succeeded となる。
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status,
                "RequestAvatarAsync は UniTask.FromResult で即時完了するため、Status は Succeeded であるべき (Req 6 AC 2)。");

            _instantiatedAvatar = await task;

            // Assert 1: 返却 GameObject が C# null / Unity null の双方で有効であること (Req 6 AC 1)。
            Assert.IsNotNull(_instantiatedAvatar,
                "RequestAvatarAsync は await 後に null でない GameObject を返すべき (Req 6 AC 1)。");
            Assert.IsTrue(_instantiatedAvatar,
                "返却された GameObject は Unity の生存判定 (operator true) で true であるべき。");

            // Assert 2: 同期版と同様に Prefab 参照そのものではなく Object.Instantiate の複製であること
            //           (design.md §5 RequestAvatarAsync は内部で RequestAvatar を呼び出す実装のため、
            //            同期版と同一の Instantiate 経路の副作用を持つ / Req 6 AC 1)。
            Assert.AreNotSame(_config.avatarPrefab, _instantiatedAvatar,
                "RequestAvatarAsync は Prefab 参照そのものではなく Object.Instantiate による複製を返すべき。");

            // Assert 3: Scene 上に配置されていること (Unity Instantiate のデフォルト動作 /
            //           同期版と同一の Scene 配置副作用を持つこと)。
            Assert.IsTrue(_instantiatedAvatar.scene.IsValid(),
                "インスタンス化された GameObject は有効な Scene に配置されているべき。");

            // Assert 4: _managedAvatars への追跡登録が行われていることの間接確認 —
            //           ReleaseAvatar に渡した際に「未管理エラー」が発生せずに正常に破棄されること
            //           (同期版 RequestAvatar と同一の追跡セット登録副作用: design.md §5 RequestAvatarAsync)。
            //           LogAssert.NoUnexpectedReceived() と組み合わせることで
            //           未管理エラーログが出力されないことを確定的に検証する。
            Assert.DoesNotThrow(() => _provider.ReleaseAvatar(_instantiatedAvatar),
                "RequestAvatarAsync で取得した GameObject は _managedAvatars に登録されているため ReleaseAvatar が未管理エラーを出さずに破棄できるべき (Req 6 AC 1 / 同期版と同等の副作用)。");

            // 参照はローカルで破棄済みとして扱う。TearDown の DestroyImmediate は
            // Unity の overloaded operator== で null 判定され二重破棄を発生させない。
            // (T-7-4 TearDown と同一の安全パターン)。
        });

        [UnityTest]
        public IEnumerator MultipleSlots_ReceiveIndependentInstances()
        {
            // Arrange: 2 つの独立した BuiltinAvatarProvider インスタンスを構築する。
            // design.md §2 / Req 4 AC 6 / Req 5 AC 4 の 1 Slot 1 インスタンス原則に従い、
            // 各 Slot は独立した Provider インスタンスを保有し、参照共有を行わない。
            // 同一 _config を共有しても各 Provider の _managedAvatars / _disposed は独立する
            // (design.md §5 コンストラクタ仕様)。
            //
            // providerA は _provider フィールドに登録して TearDown の既存クリーンアップ経路
            // (_provider.Dispose() 呼び出し) を活用する。
            // providerB は本テスト末尾で明示的に Dispose する。
            var providerA = new BuiltinAvatarProvider(_config, errorChannel: null);
            var providerB = new BuiltinAvatarProvider(_config, errorChannel: null);
            _provider = providerA;

            // Act: 各 Provider から独立に RequestAvatar を呼び出す。
            var avatarA = providerA.RequestAvatar(_config);
            var avatarB = providerB.RequestAvatar(_config);

            // Object.Instantiate は同期完了するが、Scene 反映と後続フレーム処理の整合性のため
            // 1 フレーム経過させる (T-7-4 / T-7-5 と同一の遅延パターン)。
            yield return null;

            // Assert 1: いずれの返却値も null でない有効な GameObject であること。
            Assert.IsNotNull(avatarA,
                "前提: providerA.RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsNotNull(avatarB,
                "前提: providerB.RequestAvatar は null でない GameObject を返すべき。");
            Assert.IsTrue(avatarA,
                "前提: avatarA は Unity の生存判定 (operator true) で true であるべき。");
            Assert.IsTrue(avatarB,
                "前提: avatarB は Unity の生存判定 (operator true) で true であるべき。");

            // Assert 2: Provider インスタンス自体が独立した参照であること
            //          (design.md §2 / 参照共有モデルを採用しないことの構造的前提)。
            Assert.AreNotSame(providerA, providerB,
                "2 つの BuiltinAvatarProvider は独立したインスタンスでなければならない (design.md §2 1 Slot 1 Provider 原則)。");

            // Assert 3: 返却された GameObject が異なるインスタンス参照であること
            //          (Req 4 AC 6 / Req 5 AC 4: 1 Slot 1 インスタンス原則の主要観測点)。
            //          同一 Prefab を元にしていても、Object.Instantiate は都度新規 GameObject を
            //          生成するため、参照は必ず異なる (design.md §5 RequestAvatar 契約)。
            Assert.AreNotSame(avatarA, avatarB,
                "独立した BuiltinAvatarProvider は RequestAvatar で異なる GameObject インスタンスを返すべき (Req 4 AC 6 / Req 5 AC 4)。");

            // Assert 4: 各 Provider の _managedAvatars 追跡セットが独立していること
            //          (design.md §5 ReleaseAvatar 契約の未管理判定ロジックを介した観測可能副作用)。
            //          providerA は avatarB を追跡していないため、ReleaseAvatar(avatarB) は
            //          「未管理 GameObject」として早期リターンし、エラーログを発行する。
            //          providerB と avatarA についても同様。
            LogAssert.Expect(LogType.Error,
                "[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
            Assert.DoesNotThrow(() => providerA.ReleaseAvatar(avatarB),
                "providerA は providerB が生成した avatarB を追跡していないため、ReleaseAvatar は未管理エラーとして早期リターンするべき (追跡セット独立性)。");

            LogAssert.Expect(LogType.Error,
                "[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
            Assert.DoesNotThrow(() => providerB.ReleaseAvatar(avatarA),
                "providerB は providerA が生成した avatarA を追跡していないため、ReleaseAvatar は未管理エラーとして早期リターンするべき (追跡セット独立性)。");

            // Assert 5: 未管理分岐は早期リターンのため、対象 GameObject は破棄されないこと
            //          (design.md §5 ReleaseAvatar 契約: 未管理時は Object.Destroy を呼ばない)。
            Assert.IsTrue(avatarA,
                "avatarA は providerB の未管理分岐では破棄されてはならない (未管理時は早期リターン)。");
            Assert.IsTrue(avatarB,
                "avatarB は providerA の未管理分岐では破棄されてはならない (未管理時は早期リターン)。");

            // Cleanup: providerB を Dispose し、追跡中の avatarB を破棄する。
            //          providerA / avatarA は TearDown の _provider.Dispose() 経路が処理する。
            providerB.Dispose();
        }
    }
}
