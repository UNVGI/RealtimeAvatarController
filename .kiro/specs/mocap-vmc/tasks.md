# mocap-vmc 実装タスク (EVMC4U 全面置換版)

> **フェーズ**: tasks (regenerate)
> **言語**: ja
> **対応 Spec バージョン**: requirements.md (要件 1〜12, `updated_at=2026-04-22`) + design.md (§1〜§13, EVMC4U 採用版)
> **実行方式**: `/kiro:spec-run mocap-vmc` で全タスクをバッチ実行
> **前版との関係**: 前版 tasks.md は自前 VMC 実装前提だったため全面書換。EVMC4U 採用に伴い `Vmc*` 自前クラス群の実装タスクは削除対象タスクに置き換え。

---

## タスク方針

- **TDD 駆動**: 本番コード変更の前に失敗するテストを必ず先行作成する。EditMode で完結する純ロジックは EditMode、Unity コンポーネント / `MonoBehaviour` / uOSC が絡むものは PlayMode を用いる。
- **タグ**: 各タスクの見出しまたは冒頭に `[Test]` / `[Impl]` / `[Refactor]` / `[Docs]` / `[Delete]` / `[Research]` を付与して作業性格を明示する。
- **アトミック**: 1 サブタスクは独立に commit / レビューできる粒度 (目安 1〜3 時間)。
- **依存順**: Contracts 文言訂正 → EVMC4U パッチ → 共有 Receiver → Adapter → Factory → 旧実装削除 → Sample 動作確認 → ドキュメント整備。
- **要件トレース**: 各サブタスク末尾に該当要件 ID を `_Requirements: X.X, Y.Y_` 形式で記載する。
- **テスト注入**: UDP を介さずに Dictionary を直接注入できる public テスト API を EVMC4U 改変および共有 Receiver に導入する (要件 12.3 / 12.7)。
- **並列マーカー**: 本タスク群は asmdef 変更と EVMC4U 改変の影響範囲が広く互いに密結合するため、`--sequential` 指定に準じて `(P)` 付与を最小化する。独立可能な箇所 (純ロジックテスト) のみに `(P)` を付ける。

---

## タスク実行順序の俯瞰

```
Phase A (Contracts 文言整備)         → Task 1
Phase B (EVMC4U ローカルパッチ)      → Task 2
Phase C (共有 Receiver)              → Task 3
Phase D (Adapter 本体)               → Task 4
Phase E (Factory 再配線)             → Task 5
Phase F (旧自前実装の撤去)           → Task 6
Phase G (Sample 実機スモークテスト)  → Task 7
Phase H (ドキュメント整備・回帰確認) → Task 8
```

---

## 1. Foundation: 共有 contracts の文言訂正と前提検証

- [ ] 1. 契約文書の「VMC 受信はワーカースレッド」記述を訂正する前提整備
  - 旧実装前提の記述を整理し、以降のタスクが整合する Single Source of Truth を確立する。

- [x] 1.1 [Research] UniVRM / UniGLTF の manifest 依存が維持されていることを確認する
  - `Packages/manifest.json` を読み、`com.vrmc.gltf` / `com.vrmc.univrm` / `com.vrmc.vrm` が現行バージョンで残っていること (EVMC4U asmdef の GUID 参照が解決できる前提) を確認する。
  - 欠落があれば追加 PR を切る必要があるため、本タスクの完了時点で「存在する」または「追加すべき行」が明確に記録される。
  - 観測可能な完了条件: 本タスクの出力として、manifest 上の 3 パッケージのバージョン文字列を含む短い確認メモが task 実行記録 (commit message / PR description) に残る。
  - _Requirements: 10.2, 10.5_
  - _Boundary: Packages/manifest.json_

- [x] 1.2 [Docs] `_shared/contracts.md` のワーカースレッド記述を訂正する
  - 以下の箇所で「受信ワーカースレッド」「ワーカースレッド (`VmcFrameBuilder`)」と書かれた記述を、「uOSC の `onDataReceived` は Unity MainThread で発火する」「`mocap-vmc` は MainThread 受信 + LateUpdate Tick emit のモデルを取る」旨に修正する。
    - `Timestamp` 欄 (打刻タイミング): 「受信スレッド上で」と表現を一般化し、VMC では MainThread である旨を補記。
    - `BoneLocalRotations` の経緯パラグラフ: 「変換責務をワーカースレッドから MainThread に移動」の表現を、M-3 追補と整合する形に置き換える。
    - §2.2 末尾の M-3 追補 (1行) は既存のまま保持 (MainThread OnNext 許容、`Interlocked.Exchange` が過剰同期として残るだけの旨)。
  - §2.2 のフィールド形状 (`BoneLocalRotations` 等) は変更しない (要件 11.4)。
  - 観測可能な完了条件: `contracts.md` 上で "ワーカースレッド" の出現箇所が VMC 受信/emit 文脈から消え、MainThread モデルが明示される。
  - _Requirements: 11.4, 11.5_
  - _Boundary: .kiro/specs/_shared/contracts.md_

---

## 2. EVMC4U ローカルパッチ (最小改変 4 点)

- [ ] 2. `Assets/EVMC4U/ExternalReceiver.cs` に対する最小限のソース改変
  - 受信のみで動作可能化・内部状態の read API 公開・Root キャッシュ・テスト注入口の 4 点に限定する (design.md §6)。ファイル先頭に `// [RealtimeAvatarController mocap-vmc local patch] - see .kiro/specs/mocap-vmc/design.md §6` コメントを配置する。

- [x] 2.1 [Test] Patch 後の受信ガード緩和に対するテスト先行作成
  - EditMode テストを追加し、ProcessMessage 相当のガードが Model=null でも Bone Dictionary への蓄積を止めないことを検証する。
    - `ExternalReceiver` インスタンスを生成 (GameObject 経由) → `Model = null` のまま後述の「テスト用 Setter」経由で `/VMC/Ext/Bone/Pos` 相当のボーン回転を注入 → `GetBoneRotationsView()` に要素が入ることを確認。
    - 既存挙動との非互換性がないこと (Model 有りの場合は従来通り Transform 書込が抑止されないこと) を回帰テストとして別ケースで担保。
  - 実装が未だ存在しないため、このテストは赤 (失敗 or コンパイルエラー) でよい。
  - 観測可能な完了条件: EditMode テストファイル (新規) が存在し、テスト名から「Model=null でも Bone Dictionary が蓄積される」意図が読み取れる。
  - _Requirements: 1.8, 2.5, 3.1, 12.3, 12.7_
  - _Boundary: Tests/EditMode/mocap-vmc/_

- [x] 2.2 [Impl] ProcessMessage の Model=null 早期リターン緩和
  - `ExternalReceiver.cs` の `ProcessMessage` 内、現行の early-return (`Model == null || Model.transform == null || RootPositionTransform == null || RootRotationTransform == null` ガード) を削除または条件リオーダーし、`/VMC/Ext/Bone/Pos` および `/VMC/Ext/Root/Pos` の受信時に Bone / Root Dictionary 蓄積が Model=null でも継続するようにする (design.md §6.1)。
  - Transform 書込パス (`RootPositionSynchronize` / `RootRotationSynchronize` / `BoneSynchronizeByTable` 経由) は既存の null ガードで保護されているため、これらは動作を変えない。
  - ファイル冒頭の local patch マーカーコメントを記載する。
  - 観測可能な完了条件: タスク 2.1 で先行作成したテストの「Model=null でも Dictionary 蓄積が行われる」ケースが green になる (既存の Model 有りケースも依然 green)。
  - _Requirements: 2.4, 2.5, 1.8_
  - _Boundary: Assets/EVMC4U/ExternalReceiver.cs_

- [x] 2.3 [Impl] 内部 Dictionary / shutdown の読取アクセサを追加
  - 以下の read API を `ExternalReceiver` に追加する (design.md §6.2):
    - `public IReadOnlyDictionary<HumanBodyBones, Quaternion> GetBoneRotationsView()` → `HumanBodyBonesRotationTable` をそのまま返す
    - `public IReadOnlyDictionary<HumanBodyBones, Vector3> GetBonePositionsView()` → `HumanBodyBonesPositionTable` をそのまま返す
    - `public bool IsShutdown => shutdown;`
  - 既存の private フィールドの宣言は変更しない (同一インスタンスを使い続ける)。アップストリーム API との互換性を壊さない (要件 10.5)。
  - 観測可能な完了条件: Adapter 実装 (後続 Task 4) 側で public アクセサを呼び出す形にコンパイル可能になる。
  - _Requirements: 3.1, 3.6, 8.2, 10.5_
  - _Boundary: Assets/EVMC4U/ExternalReceiver.cs_

- [x] 2.4 [Impl] `/VMC/Ext/Root/Pos` 受信時に Root を public プロパティへキャッシュ
  - `ExternalReceiver` に `public Vector3 LatestRootLocalPosition { get; private set; }` と `public Quaternion LatestRootLocalRotation { get; private set; } = Quaternion.identity;` を追加 (design.md §6.2)。
  - `ProcessMessage` の `/VMC/Ext/Root/Pos` ケース内部 (ローカル変数 `pos` / `rot` が計算された直後) で、上記プロパティへ代入する 1 行を追加する。
  - 既存の `RootPositionTransform.localPosition = pos` / `RootRotationTransform.localRotation = rot` の書込は `RootPositionSynchronize` / `RootRotationSynchronize` ガード配下で動作するため、本 Spec ではいずれも `false` に固定される (Task 3.x 参照)。
  - 観測可能な完了条件: `/VMC/Ext/Root/Pos` 受信シミュレーション時に `LatestRootLocalPosition` / `LatestRootLocalRotation` が受信値を反映することを EditMode テスト (Task 2.1 拡張) で確認できる。
  - _Requirements: 3.4_
  - _Boundary: Assets/EVMC4U/ExternalReceiver.cs_

- [x] 2.5 [Impl] テスト専用 Bone Dictionary 注入 Setter を追加
  - `ExternalReceiver` に PlayMode / EditMode テストから Dictionary を直接書ける public Setter を追加する:
    - `public void InjectBoneRotationForTest(HumanBodyBones bone, Quaternion rot)` → `HumanBodyBonesRotationTable[bone] = rot`
    - 必要に応じて `public void InjectBonePositionForTest(HumanBodyBones bone, Vector3 pos)` も追加
  - 出荷ビルド混入を避けるため、`#if UNITY_INCLUDE_TESTS || DEVELOPMENT_BUILD` ガード内で定義する (design.md §10.2)。
  - 観測可能な完了条件: Task 2.1 のテストがこの Setter を用いて UDP を介さず Dictionary を注入・検証できる (赤 → 緑)。
  - _Requirements: 12.3, 12.7_
  - _Boundary: Assets/EVMC4U/ExternalReceiver.cs_

---

## 3. 共有 Receiver (`EVMC4USharedReceiver`) の実装

- [ ] 3. プロセスワイド単一の EVMC4U 受信コンポーネントを管理するヘルパー
  - `DontDestroyOnLoad` GameObject 1 個を refcount で生存管理する MonoBehaviour (design.md §4.3)。`RootPositionSynchronize=false` / `RootRotationSynchronize=false` を初期化時に強制する。

- [x] 3.1 [Test] 参照カウント / 単一性 / リセットに関する EditMode テスト先行作成
  - 以下のテストを追加する (実装未着手のため赤でよい):
    - `EnsureInstance()` を 2 回呼ぶと refCount が 2 になり、同一 `ExternalReceiver` インスタンスを返す。
    - `Release()` を 2 回呼ぶと GameObject が Destroy される (次フレームまでに `(object)instance == null` となる)。
    - `EnsureInstance()` 返却時点で `RootPositionSynchronize == false` / `RootRotationSynchronize == false` が確定している。
    - `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 相当のクリア処理呼び出し後は refCount が 0 / instance が null にリセットされる。
  - 観測可能な完了条件: テストファイルが存在し、未実装のため `NullReferenceException` 等で失敗する。
  - _Requirements: 2.1, 2.2, 2.3, 4.4_
  - _Boundary: Tests/EditMode/mocap-vmc/ (EVMC4USharedReceiverTests)_

- [x] 3.2 [Impl] `EVMC4USharedReceiver` MonoBehaviour の実装
  - 次の仕様で新規ファイル `Runtime/MoCap/VMC/EVMC4USharedReceiver.cs` を作成する:
    - `public static EVMC4USharedReceiver EnsureInstance()` — 初回呼び出しで GameObject `[EVMC4U Shared Receiver]` を生成し `DontDestroyOnLoad` を適用、`uOscServer` と `ExternalReceiver` を AddComponent。refCount を +1 する。
    - `public void Release()` — refCount を -1。0 到達時に `Destroy(gameObject)` を呼ぶ。
    - `public ExternalReceiver Receiver { get; }` — 内部 `ExternalReceiver` への read-only アクセス。
    - 初期化時に `Receiver.RootPositionSynchronize = false` / `Receiver.RootRotationSynchronize = false` / `Receiver.Model = null` を設定 (要件 2.4)。
    - `OnDestroy()` で static 参照を null に戻す。
  - 観測可能な完了条件: Task 3.1 のテストが全て緑になる。
  - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - _Boundary: Runtime/MoCap/VMC/EVMC4USharedReceiver.cs_

- [x] 3.3 [Impl] Domain Reload OFF 下の静的クリア処理を追加
  - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 属性のメソッドで `s_instance = null` / `s_refCount = 0` を強制初期化する (design.md §4.3)。
  - 同じクラス内の他の静的クリアタイミングと競合しないよう、static クリアのみを行い GameObject への参照は触らない (静的値は Unity 側が既にシーン破棄している前提)。
  - 観測可能な完了条件: PlayMode 停止 → 再開を複数回行っても `EnsureInstance()` が毎回新しい GameObject を生成でき、古い参照が漏れない (後続 PlayMode テストで検証)。
  - _Requirements: 2.1, 2.3_
  - _Boundary: Runtime/MoCap/VMC/EVMC4USharedReceiver.cs_

- [x] 3.4 [Research] `RegistryLocator.ResetForTest()` と静的クリアの実行順確認
  - `SubsystemRegistration` タイミングで登録される複数の `RuntimeInitializeOnLoadMethod` の順序は Unity 仕様上保証されない点を前提に、本 Spec では「どちらが先でも安全」であるように設計する (両者とも static を null に戻すだけで、相互依存がない)。
  - 実機で `RegistryLocator.ResetForTest()` 実装内容 (slot-core) と `EVMC4USharedReceiver` の静的クリアが同一フレームで起きた場合に不具合が出ないことを PlayMode テスト (Task 4.7 または 7 系統) で軽く観測する。
  - 観測可能な完了条件: コードコメントまたはタスク実行記録に「両静的クリアは互いに独立で実行順に依存しない」旨を明記し、PlayMode テスト 1 ケースでリセット→新規 EnsureInstance→Resolve→Release の一連フローが green であることを確認する。
  - _Requirements: 6.4, 12.5_
  - _Depends: 3.3_
  - _Boundary: Runtime/MoCap/VMC/EVMC4USharedReceiver.cs, slot-core RegistryLocator (read-only)_

- [x] 3.5 [Impl] Subscribe/Unsubscribe と LateUpdate による Adapter Tick 駆動
  - `public void Subscribe(EVMC4UMoCapSource adapter)` / `public void Unsubscribe(EVMC4UMoCapSource adapter)` を追加し、内部 `HashSet<EVMC4UMoCapSource>` で管理する。
  - `LateUpdate()` で登録済み Adapter 全件の `internal void Tick()` を呼び出す (要件 4.3 / 4.4)。
  - Adapter Tick 中の例外は `try/catch` で全捕捉し、該当 Adapter の `PublishError(VmcReceive, ex)` に委譲する (要件 8.3)。他の Adapter の Tick が止まらないようにする。
  - 観測可能な完了条件: PlayMode テストで Adapter が Tick で `OnNext` を起こすようになる (Task 4 系統で使用)。
  - _Requirements: 4.3, 4.4, 8.3_
  - _Boundary: Runtime/MoCap/VMC/EVMC4USharedReceiver.cs_

- [x] 3.6 [Impl] port 反映と StopServer/StartServer による再バインド
  - `public void ApplyReceiverSettings(int port)` (または相当のメソッド) を追加し、内部 `uOscServer.port` を更新後 `StopServer()` / `StartServer()` を呼んで明示的に再バインドする (design.md §4.3)。
  - SocketException は呼び出し元 (Adapter.Initialize) へ伝播させる (要件 8.4)。
  - `bindAddress` は uOSC が bindAddress を公開していないため「情報フィールドとして受け取るが現時点では全インターフェース bind」に留める旨のコメントを残す (design.md §4.4)。
  - 観測可能な完了条件: ポート変更フロー (例: 39539 → 40000) で SocketException が発生せずに再バインドできることを PlayMode テストで 1 ケース検証。
  - _Requirements: 1.7, 5.3, 8.4_
  - _Boundary: Runtime/MoCap/VMC/EVMC4USharedReceiver.cs_

---

## 4. Adapter (`EVMC4UMoCapSource`) 本体の実装

- [ ] 4. `IMoCapSource` 実装としての Adapter
  - EVMC4U 内部 Dictionary を LateUpdate で snapshot し `HumanoidMotionFrame` を発行する (design.md §4.2)。状態遷移 `Uninitialized → Running → Disposed`、`MotionStream.OnError` 非発行、BoneLocalRotations 経路。

- [x] 4.1 [Test] Adapter 基本契約の EditMode テスト先行作成
  - 以下のテストを追加する (コンパイル失敗・赤でよい):
    - `SourceType` が `"VMC"` を返す。
    - `Initialize` 2 回目で `InvalidOperationException`。
    - `Initialize(config)` に `MoCapSourceConfigBase` 派生の別型を渡すと `ArgumentException` (メッセージに実型名を含む)。
    - `Initialize` に port=0/1024/65536 を渡すと `ArgumentOutOfRangeException`。
    - `Dispose` 後の `Shutdown` は no-op (例外を投げない)。
    - `MotionStream` は `IObservable<MotionFrame>` として公開される (型検査のみ)。
  - 観測可能な完了条件: 上記テストクラスが存在し、実装がないため赤。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.3, 7.5_
  - _Boundary: Tests/EditMode/mocap-vmc/ (EVMC4UMoCapSourceTests)_

- [x] 4.2 [Test] Adapter の Dictionary 注入 → OnNext 発行を PlayMode で検証するテスト先行作成
  - 以下の PlayMode テストを追加する (Task 2.5 の Setter / Task 3 の Shared Receiver を利用):
    - `EVMC4USharedReceiver.EnsureInstance()` → `InjectBoneRotationForTest(HumanBodyBones.LeftHand, q)` → 1 フレーム待機 (`yield return null`) → Adapter の `MotionStream` Subscription が `HumanoidMotionFrame` を 1 件受け取り、`BoneLocalRotations[LeftHand]` が注入値と一致する。
    - 再注入なしで次フレームが進んだ場合は OnNext が発行されない (要件 3.5 `_dirty` 判定)。
    - 55 bone を全て注入した 1 フレームで、全ボーンが frame に含まれる。
    - `Muscles.Length == 0` / `IsValid == true` を確認 (要件 3.2)。
    - `MotionStream` の `OnError` が一度も呼ばれないこと (Subscription の onError ハンドラでカウント 0)。
    - `Shutdown()` 後に `OnCompleted` が発行されること (要件 4.5)。
  - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼ぶ (要件 12.5)。
  - 観測可能な完了条件: テストクラスが存在し、実装未完のため赤。
  - _Requirements: 3.1, 3.2, 3.5, 3.6, 4.5, 4.6, 4.7, 8.1, 12.3, 12.5, 12.7_
  - _Boundary: Tests/PlayMode/mocap-vmc/ (EVMC4UMoCapSourceIntegrationTests)_

- [x] 4.3 [Impl] Adapter クラス骨格と状態機械
  - 新規ファイル `Runtime/MoCap/VMC/EVMC4UMoCapSource.cs` を作成する:
    - `public sealed class EVMC4UMoCapSource : IMoCapSource, IDisposable`
    - `internal EVMC4UMoCapSource(string slotId, ISlotErrorChannel errorChannel)` コンストラクタ
    - `public string SourceType => "VMC"` (要件 1.3)
    - 状態列挙 `enum State { Uninitialized, Running, Disposed }` を private で保持 (要件 7.5)
  - 観測可能な完了条件: Task 4.1 の `SourceType` / 二重 `Initialize` / `Dispose` 後 `Shutdown` no-op ケースが green になる。
  - _Requirements: 1.1, 1.2, 1.3, 7.5_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

- [x] 4.4 [Impl] UniRx Subject による MotionStream 公開
  - `Subject<MotionFrame>` を生成し、`Synchronize().Publish().RefCount()` で Hot Observable として公開する (design.md §4.2):
    - `public IObservable<MotionFrame> MotionStream { get; }`
    - `Initialize` 完了前に購読されても例外にならず、Running 後に OnNext が始まる (要件 1.9, Q4 案 a)。
  - 観測可能な完了条件: Task 4.1 のストリーム型検査ケースが green。購読者複数からの購読でも 1 つの Subject が共有されることを別ケースで確認可能。
  - _Requirements: 1.4, 1.9, 4.6, 4.7_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

- [x] 4.5 [Impl] `Initialize(MoCapSourceConfigBase)` 実装
  - 処理フロー (design.md §4.2):
    1. 状態チェック (`Uninitialized` 以外なら `InvalidOperationException`)。
    2. `config as VMCMoCapSourceConfig`、失敗時 `ArgumentException($"VMCMoCapSourceConfig が必要ですが {config?.GetType().Name ?? "null"} が渡されました")` 。
    3. `port` を 1025〜65535 で範囲検査、範囲外は `ArgumentOutOfRangeException`。
    4. `EVMC4USharedReceiver.EnsureInstance()` を呼び Receiver を取得する (要件 1.6 / 2.1 / 2.2)。
    5. `sharedReceiver.ApplyReceiverSettings(config.port)` を呼び uOSC を該当 port で起動する (要件 1.7)。
    6. `sharedReceiver.Subscribe(this)` で LateUpdate Tick 対象に追加する。
    7. 状態を `Running` に遷移。
  - 観測可能な完了条件: Task 4.1 の Port 範囲例外ケース、型不一致例外ケースが green。PlayMode で実際に port を変えた初期化が成功する (Task 4.2 のセットアップが通る)。
  - _Requirements: 1.5, 1.6, 1.7, 5.3, 5.4, 8.4_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

- [x] 4.6 [Impl] `Tick()` による Bone Dictionary の snapshot と OnNext 発行
  - 処理フロー (design.md §5.1):
    - `Receiver.GetBoneRotationsView()` (Task 2.3) を走査し、**新規 `Dictionary<HumanBodyBones, Quaternion>` を allocate して値コピー** (要件 3.6)。Count が 0 なら emit しない (要件 3.5)。
    - `_dirty` フラグで前 Tick 以降に Bone 注入があったかを判定 (Task 2.5 のテスト注入 Setter 経路 + 実 OSC 受信経路の両方で dirty を立てるため、EVMC4U 側に専用フラグを持たせない簡易実装として「前 Tick の参照等価性」では不可)。シンプル案: Receiver に `public int RotationWriteCounter { get; internal set; }` 相当のバージョン番号を追加し (Task 2.3 のパッチ拡張) Adapter が差分判定する。軽量にするため、初期版では「Dictionary の Count + 代表値の変化」での近似判定を許容するが、将来的にカウンタ方式へ移行可能な形に設計する。
    - `Timestamp` は `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻 (要件 3.3)。
    - Root は `Receiver.LatestRootLocalPosition` / `Receiver.LatestRootLocalRotation` を optional に格納 (要件 3.4)。
    - `Muscles` は `Array.Empty<float>()` (要件 3.2)。
    - `HumanoidMotionFrame` を new し `_subject.OnNext(frame)` を呼ぶ。
    - BlendShape / 表情は参照しない (要件 3.7)。
  - Tick 内例外は `try/catch` して `PublishError(VmcReceive, ex)` へ (要件 8.3)。OnError は呼ばない (要件 8.1)。
  - 観測可能な完了条件: Task 4.2 の 1 bone 注入→OnNext ケース、55 bone 注入ケース、`Muscles.Length==0` ケースが green。
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.3, 4.6, 8.1, 8.3_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

- [x] 4.7 [Impl] `Shutdown()` / `Dispose()` 実装
  - 処理フロー (design.md §5.2):
    1. 状態 `Disposed` なら即 return (冪等、要件 1.2)。
    2. `sharedReceiver.Unsubscribe(this)` で Tick 駆動から除外。
    3. `sharedReceiver.Release()` で refCount を戻す (最後の Adapter だったら Shared GameObject ごと破棄される)。
    4. `_subject.OnCompleted()` → `_subject.Dispose()` (要件 4.5)。
    5. 状態 `Disposed` に遷移。
  - 観測可能な完了条件: Task 4.2 の `Shutdown` 後 `OnCompleted` ケースが green。`Dispose` の冪等ケースが green。
  - _Requirements: 1.2, 4.5_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

- [ ] 4.8 [Impl] `PublishError` ヘルパと ErrorChannel 連携
  - `private void PublishError(SlotErrorCategory category, Exception ex)` を追加し `_errorChannel.Publish(new SlotError(_slotId, category, ex, DateTime.UtcNow))` を呼ぶ (要件 8.3)。
  - `MotionStream.OnError` は絶対に呼ばない (要件 8.1)。EVMC4U 側の `Debug.LogError` には干渉しない (要件 8.2)。Log 抑制は `DefaultSlotErrorChannel` 責務 (要件 8.5)。
  - 観測可能な完了条件: Task 4.2 の OnError 非発行ケースが green。Tick 例外を人工的に起こしたケースで `ISlotErrorChannel` に `VmcReceive` が 1 件 publish される (別 PlayMode テスト 1 ケース追加)。
  - _Requirements: 8.1, 8.2, 8.3, 8.5_
  - _Boundary: Runtime/MoCap/VMC/EVMC4UMoCapSource.cs_

---

## 5. Factory の再配線と参照共有の検証

- [ ] 5. `VMCMoCapSourceFactory` を `EVMC4UMoCapSource` 生成に差し替える
  - typeId `"VMC"` 維持・属性ベース自己登録維持・`VMCMoCapSourceConfig` 保持 (design.md §4.5)。

- [ ] 5.1 [Test] Factory の Adapter 生成型差替テスト先行作成
  - 既存 `VmcConfigCastTests.cs` / `VmcFactoryRegistrationTests.cs` を再利用して次をカバーする:
    - `VMCMoCapSourceFactory.Create(VMCMoCapSourceConfig)` の戻り値型が `EVMC4UMoCapSource` であること (型アサーション)。
    - 別型の `MoCapSourceConfigBase` 派生を渡すと `ArgumentException` (メッセージに実型名を含む)。
    - `ScriptableObject.CreateInstance<VMCMoCapSourceConfig>()` による動的生成 Config でも成功する (要件 5.2)。
    - `typeId "VMC"` が `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に含まれる。
    - 同一 typeId 二重登録で `RegistryConflictException` (Factory の自己登録メソッドは try/catch で捕捉し `RegistryConflict` カテゴリで publish する、要件 6.3)。
  - 観測可能な完了条件: 既存テストが、生成される具象型が `VmcMoCapSource` から `EVMC4UMoCapSource` に変わったことで一時的に赤になり、Task 5.2 実装後に green に戻る。
  - _Requirements: 5.2, 5.4, 5.5, 5.6, 6.1, 6.2, 6.3, 6.4, 12.2_
  - _Boundary: Tests/EditMode/mocap-vmc/_

- [ ] 5.2 [Impl] `VMCMoCapSourceFactory.Create` の差替
  - `Create(MoCapSourceConfigBase)` 内の `new VmcMoCapSource(...)` を `new EVMC4UMoCapSource(slotId: string.Empty, errorChannel: RegistryLocator.ErrorChannel)` に差し替える。
  - `const string VmcSourceTypeId = "VMC"` を維持する (要件 5.4 / 11.3)。
  - `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` / Editor 側 `[InitializeOnLoadMethod]` 自己登録のコードは既存のままとし、`RegistryConflictException` の握り潰さない挙動を維持する (要件 6.1 / 6.2 / 6.3)。
  - Editor 登録は既存 `Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs` を維持 (要件 6.2)。
  - 観測可能な完了条件: Task 5.1 のテスト群が全て green。
  - _Requirements: 5.4, 5.5, 6.1, 6.2, 6.3, 11.3_
  - _Boundary: Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs_

- [ ] 5.3 [Test] 参照共有 (同一 Config → 同一 Adapter) の PlayMode テスト
  - `MoCapSourceRegistry.Resolve()` を用いて、同一 `VMCMoCapSourceConfig` インスタンスに対する 2 回の Resolve が同一 `EVMC4UMoCapSource` を返すことを確認する (要件 2.1 / 5.6 / 12.4)。
  - 別インスタンスの `VMCMoCapSourceConfig` (port 違い) に対しては別 Adapter が返るが、`EVMC4USharedReceiver` のインスタンスは 1 つのみ保持されていることを確認する。
  - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼ぶ (要件 12.5)。
  - 観測可能な完了条件: テストクラスが存在し、Task 3〜5 の実装により全ケースが green。
  - _Requirements: 2.1, 5.6, 12.4, 12.5_
  - _Boundary: Tests/PlayMode/mocap-vmc/ (EVMC4UMoCapSourceSharingTests)_

---

## 6. 旧自前実装の撤去

- [ ] 6. 旧 VMC 自前実装ファイルを削除・関連テストを再編
  - 置き換えが安全に行えることを 5 系統までで担保してから削除する (要件 11.1 / 11.2)。

- [ ] 6.1 [Delete] 旧自前実装ソースを削除
  - 以下ファイルを `.meta` とともに削除する (design.md §7.2):
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/VmcMoCapSource.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/AssemblyInfo.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcOscAdapter.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcMessageRouter.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcFrameBuilder.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcBoneMapper.cs`
    - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcTickDriver.cs`
    - 空になった `Internal/` ディレクトリごと削除。
  - `InternalsVisibleTo` は不要になるため `AssemblyInfo.cs` も削除 (要件 11.1)。
  - 観測可能な完了条件: Unity コンパイルが通り、削除後にビルドエラーが発生しない。
  - _Requirements: 11.1_
  - _Boundary: Runtime/MoCap/VMC/_

- [ ] 6.2 [Delete] 旧自前実装に対応する EditMode / PlayMode テストを削除・再編
  - 以下のテストを削除する (design.md §10.3, 要件 11.2):
    - `Tests/EditMode/mocap-vmc/VmcBoneMapperTests.cs`
    - `Tests/EditMode/mocap-vmc/VmcOscParserTests.cs`
    - `Tests/PlayMode/mocap-vmc/UdpOscSenderTestDouble.cs`
    - `Tests/PlayMode/mocap-vmc/VmcMoCapSourceIntegrationTests.cs` (UDP 送信ダブル利用の旧統合テスト)
  - 既存 `VmcConfigCastTests.cs` / `VmcFactoryRegistrationTests.cs` は Task 5.1 で内容を更新済みのため「削除せずリネーム検討」で残す (`EVMC4UConfigCastTests.cs` 等への改名は任意、typeId `"VMC"` に関する命名は残す)。
  - 観測可能な完了条件: Unity TestRunner で EditMode / PlayMode テストが全て実行可能であり、旧自前実装に対応するテストは残存しない。
  - _Requirements: 11.2_
  - _Boundary: Tests/EditMode/mocap-vmc/, Tests/PlayMode/mocap-vmc/_

- [ ] 6.3 [Refactor] asmdef 依存の再整理
  - `RealtimeAvatarController.MoCap.VMC.asmdef` の `references` から `com.hidano.uosc` 直接参照を撤去し、代わりに `EVMC4U` asmdef を追加する (design.md §7.1)。
  - UniRx は必要 (Subject / Publish / RefCount) のため残す。
  - 観測可能な完了条件: アセンブリ定義 Inspector 上で `com.hidano.uosc` 直接参照が解除され、EVMC4U asmdef GUID が参照一覧に追加されている。コンパイルが通る。
  - _Requirements: 10.1, 10.2, 10.3_
  - _Boundary: Runtime/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.asmdef_

---

## 7. UI Sample 実機スモークテスト

- [ ] 7. `SlotManagementDemo` シーン上で新 Adapter が動作することを確認
  - 旧 Spec コードパスがなくなった状態で既存 Sample が動作し続けることを確認する (要件 7.6 / 11.3)。

- [ ] 7.1 [Test] PlayMode スモークテスト (Sample Scene 相当)
  - Sample を含めるか別 PlayMode テストで代用するかは実装者判断で良いが、以下を最低限確認する:
    - `VMCMoCapSourceConfig.asset` を `MoCapSourceDescriptor.Config` として持つ `SlotSettings` から `SlotManager` を作成 → Slot 追加 → `EVMC4UMoCapSource` が Resolve され、`MotionStream` を購読できる。
    - 同一 Config を持つ 2 つの Slot を追加した場合、参照共有で同一 Adapter が返る。
    - Slot を Release → 別 Slot を Resolve の差替フローで例外が発生しない (要件 7.3 / 7.4)。
  - 実機 Sample Scene は本タスクの範囲外 (手動動作確認) だが、上記 3 ケースを PlayMode テストとして実装する。
  - 観測可能な完了条件: PlayMode テスト 3 件が green。
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.6, 11.3_
  - _Boundary: Tests/PlayMode/mocap-vmc/_

- [ ] 7.2 [Research] Sample Scene の `SlotManagementDemo` が無改修で動作することの目視確認
  - `Assets/Samples/Realtime Avatar Controller/0.1.0/UI Sample/Scenes/SlotManagementDemo.unity` および `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Scenes/SlotManagementDemo.unity` の両方で、Play 時に `VMCMoCapSourceConfig.asset` / `SlotSettings.asset` 無改修でも Slot 追加・削除・差替が動作することを目視確認する。
  - 改修が必要な箇所があれば「本 Spec スコープ外」として別 Spec / 別 Issue 化し、本タスクはその旨を記録するのみとする (要件 7.6 / 11.3)。
  - 観測可能な完了条件: 動作確認メモ (green/red + スクリーンショットまたは手順ログ) が残り、red の場合はフォローアップ Issue が作成されている。
  - _Requirements: 7.6, 11.3_
  - _Boundary: Assets/Samples/.../SlotManagementDemo.unity, Samples~/UI/Scenes/SlotManagementDemo.unity_

---

## 8. ドキュメント整備と最終回帰確認

- [ ] 8. mocap-vmc Spec 内ドキュメントと Traceability の更新
  - HANDOVER.md の更新は本 Spec のスコープ外 (ユーザー指示)。

- [ ] 8.1 [Docs] `mocap-vmc` Spec 内ドキュメントの微修正
  - `design.md` の §6 以降で EVMC4U ソース改変の行番号が変わったら、実際の ExternalReceiver.cs 行番号に合わせて更新する (Task 2 系列の結果に依存)。
  - Requirements Traceability (§8) 上で、Task 側との対応関係が取れていることを確認する (マッピング漏れがあれば design.md §8 を補記、または本 tasks.md 側でフォロータスクを追加)。
  - 観測可能な完了条件: `design.md` 上の行番号参照が現在のソース状態と一致し、Requirements Traceability に空欄がない。
  - _Requirements: 10.5, 11.4_
  - _Boundary: .kiro/specs/mocap-vmc/design.md_

- [ ] 8.2 [Test] 全体回帰: EditMode + PlayMode 全テスト green を確認
  - `mocap-vmc` asmdef 配下の EditMode / PlayMode テストを全て実行し、全 green を確認する。
  - 並行して `slot-core` / `motion-pipeline` の既存テストに回帰が入っていないこと (Adapter 差替で `HumanoidMotionApplier` や `MotionCache` の契約に影響が出ていないこと) を確認する。
  - 観測可能な完了条件: Unity TestRunner のレポートで `mocap-vmc` 配下 + 隣接 Spec の EditMode / PlayMode 両方がすべて green。
  - _Requirements: 11.3, 11.4, 12.1, 12.2, 12.3, 12.4, 12.5_
  - _Boundary: Tests/EditMode/, Tests/PlayMode/_

- [ ] 8.3 [Research] 開発時に判明したがスコープ外とする事項の記録
  - 本 Spec 作業中に発見された issue (EVMC4U アップデート時の改変再適用手順・BlendShape 経由の将来課題・port 再バインドコスト) を、本 Spec の `design.md` §13 または別の内部メモに記録する (design.md 既存記述の範囲内で十分であれば追記不要)。
  - 観測可能な完了条件: 追加が必要な懸念が出た場合に `design.md §13` に 1〜3 行で反映されている、または追記不要と判断されたログがある。
  - _Requirements: 11.1, 11.5_
  - _Boundary: .kiro/specs/mocap-vmc/design.md_

---

## 付録: Requirements Coverage 一覧

| 要件 | 対応タスク |
|------|-----------|
| 1.1 Adapter クラスを定義 | 4.3 |
| 1.2 Dispose 冪等 | 4.7 |
| 1.3 SourceType="VMC" | 4.3 |
| 1.4 MotionStream 型 | 4.4 |
| 1.5 型キャスト例外 | 4.5, 5.1, 5.2 |
| 1.6 Shared Receiver 確保 | 3.2, 4.5 |
| 1.7 Config を Receiver へ反映 | 3.6, 4.5 |
| 1.8 OSC 自前実装禁止 | 2.2, 6.1 |
| 1.9 Init 前購読時の空ストリーム | 4.4 |
| 2.1 Receiver 1 個 | 3.1, 3.2, 5.3 |
| 2.2 既存 Receiver 再利用 | 3.1, 3.2 |
| 2.3 Release で GameObject 破棄 | 3.1, 3.2 |
| 2.4 Model 書換禁止 | 2.2, 3.2 |
| 2.5 Model=null で受信可 | 2.1, 2.2 |
| 3.1 Dictionary → BoneLocalRotations | 2.1, 4.6 |
| 3.2 Muscles は空配列 | 4.6, 4.2 |
| 3.3 Timestamp は Stopwatch | 4.6 |
| 3.4 Root 格納 | 2.4, 4.6 |
| 3.5 空フレーム抑制 | 4.6 |
| 3.6 snapshot コピー | 2.3, 4.6 |
| 3.7 BlendShape 含めない | 4.6 |
| 4.1 MainThread 受信 | 1.2, 3.5 |
| 4.2 コールバックで OnNext しない | 4.6 (Dictionary 蓄積のみ) |
| 4.3 LateUpdate Tick で emit | 3.5, 4.6 |
| 4.4 Tick 駆動は MonoBehaviour | 3.1, 3.5 |
| 4.5 Shutdown で Tick 停止 | 4.7 |
| 4.6 OnNext は MainThread | 3.5, 4.4 |
| 4.7 マルチキャスト | 4.4 |
| 5.1 Config 継承維持 | 既存維持 (6.1 で削除しない) |
| 5.2 SO アセット + 動的生成 | 5.1 |
| 5.3 Port 範囲検証 | 3.6, 4.5 |
| 5.4 Factory キャスト失敗例外 | 5.1, 5.2 |
| 5.5 Factory が Adapter 生成 | 5.2 |
| 5.6 同一 Config 共有 | 5.3 |
| 6.1 Runtime 自己登録 | 5.2 |
| 6.2 Editor 自己登録 | 5.2 (既存 Editor 側維持) |
| 6.3 二重登録は RegistryConflict | 5.1, 5.2 |
| 6.4 Domain Reload OFF 対応 | 3.3, 3.4 |
| 7.1 Resolve で Adapter 取得 | 7.1 |
| 7.2 Release で解放 | 7.1 |
| 7.3 差替は Release → Resolve | 7.1 |
| 7.4 差替中の例外耐性 | 7.1 |
| 7.5 状態遷移管理 | 4.3 |
| 7.6 Sample 無改修で動作 | 7.1, 7.2 |
| 8.1 OnError 非発行 | 4.8 |
| 8.2 EVMC4U 内部エラー不介入 | 4.8 |
| 8.3 Adapter 例外を ErrorChannel へ | 3.5, 4.8 |
| 8.4 Port bind 失敗は Init 例外 | 3.6, 4.5 |
| 8.5 LogError 抑制は Channel 担当 | 4.8 |
| 8.6 受信タイムアウト未実装 | 実装しない (スコープ外) |
| 8.7 診断カウンタは拡張余地のみ | 実装しない (スコープ外) |
| 9.1 主要 VMC 送信アプリ互換 | EVMC4U に委譲 (2.x, 7.2) |
| 9.2 VRM 0.x で動作 | 7.2 |
| 9.3 VRM 1.x は EVMC4U 準拠 | 7.2 (範囲外として記録) |
| 9.4 OSC アドレス範囲は EVMC4U 準拠 | EVMC4U に委譲 |
| 10.1 asmdef 配置 | 6.3 |
| 10.2 asmdef 依存構成 | 1.1, 6.3 |
| 10.3 UniRx 参照 | 6.3 |
| 10.4 Editor asmdef 構成 | 既存維持 (5.2) |
| 10.5 EVMC4U asmdef API 破壊禁止 | 2.3, 2.4, 2.5, 8.1 |
| 11.1 旧自前実装削除 | 6.1, 8.3 |
| 11.2 旧テストを置換 | 6.2 |
| 11.3 上位コード無改修動作 | 5.2, 7.1, 7.2, 8.2 |
| 11.4 HumanoidMotionFrame 形状不変 | 1.2, 8.1, 8.2 |
| 11.5 contracts.md §13.1 訂正 | 1.2, 8.3 |
| 12.1 EditMode/PlayMode 2 系統 | 8.2 |
| 12.2 EditMode カバレッジ | 2.1, 4.1, 5.1 |
| 12.3 PlayMode Dictionary 注入 | 2.5, 4.2 |
| 12.4 参照共有テスト | 5.3 |
| 12.5 SetUp/TearDown で ResetForTest | 4.2, 5.3 |
| 12.6 カバレッジ数値未設定 | 実装しない (スコープ外) |
| 12.7 public state を使って注入 | 2.5, 4.2 |

---

## 付録: 設計フェーズから引き継いだ Open Items の結論

| # | Open Item | 本 tasks.md での扱い |
|---|-----------|---------------------|
| 1 | `EVMC4USharedReceiver` 静的クリアと `RegistryLocator.ResetForTest()` の実行順 | Task 3.3 で独立な静的クリアを実装し、Task 3.4 (Research) で「両者は相互依存なし」と結論。PlayMode テスト 1 ケースで観測。 |
| 2 | UniVRM / UniGLTF が `Packages/manifest.json` に残存することの確認 | Task 1.1 で明示的に確認する Research タスクとして含めた。 |
| 3 | port 再バインド (StopServer→StartServer) コストのベンチマーク | **スキップ**: UI Sample 上のポート変更頻度が低いため初期版では不要と判断 (design.md §13 「port 再バインドコスト」と整合)。将来必要になれば別 Spec で扱う。 |
| 4 | `BlendShapeSynchronize` 等の副作用フラグの明示的無効化 | **含める (最小限)**: Task 3.2 で `Model = null` に固定しているため、EVMC4U 側 `BlendShapeSynchronize` ループも Model ガードで自然に停止する。ただし副作用フラグを明示的に `false` にすることは本 Spec では追加実装しない (Model=null で十分、design.md §4.3 と整合)。特別なタスクは作らない。 |

## 付録: リスクの追加 (design 提出時に明示されていなかったもの)

- **R-1 (Tick の dirty 判定方式)**: Task 4.6 の dirty 判定について、Dictionary の「参照等価」では MainThread 上で同一インスタンスが書き換えられるため機能しない。`ExternalReceiver` に「書込カウンタ」を持たせる改変 (Task 2.3 の拡張として) を推奨するが、初期実装では「Count + 簡易 Hash」での近似で代替し、ベンチマークが問題化した時点で書込カウンタ方式へ移行できるように構造を残す。
- **R-2 (EVMC4U アップデート時の改変再適用)**: `Assets/EVMC4U/` はプロジェクトアセット配下のため Unity 自動更新は起きないが、将来 `.unitypackage` を再インポートする場合は local patch が失われる。design.md §6.5 の「ファイル先頭マーカーコメント」+ 本 tasks.md の Phase B (Task 2) の手順書が再適用時の参考となる。
- **R-3 (PlayMode テスト並列実行)**: `EVMC4USharedReceiver` はプロセスワイド singleton であるため、PlayMode テストを並列実行するフレームワーク (現時点では未導入) を導入する場合に排他制御が必要。初期版では `[SetUp]` / `[TearDown]` + `RegistryLocator.ResetForTest()` で逐次実行を前提とする。
