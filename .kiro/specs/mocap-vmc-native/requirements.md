# Requirements Document

## Project Description (Input)
VMC プロトコルを EVMC4U に依存せず自前で受信する MoCap source 実装。

`com.hidano.realtimeavatarcontroller.mocap-vmc` パッケージから EVMC4U 依存を撤廃し、uOSC のみを利用して VMC OSC メッセージ (`/VMC/Ext/Bone/Pos` および `/VMC/Ext/Root/Pos` を最低限) を直接パースする `IMoCapSource` 実装を新設する。

これにより利用者は以下の手作業から解放される:
- `Assets/EVMC4U/` への EVMC4U `.unitypackage` import
- `EVMC4U.asmdef` の自作
- `Assets/EVMC4U/ExternalReceiver.cs` に対する `evmc4u.patch` の `git apply` 適用

利用者が用意する依存は **uOSC のみ** となる。

### 背景

本 spec は当初 `mocap-vmc-reflection-loading` として「Reflection 化により EVMC4U asmdef 自作を不要化する」方針で着手したが、深掘り (`dig.md`) の結果以下が判明した:

1. EVMC4U 依存を残したまま `EVMC4U.asmdef` 自作だけを解消しても、`evmc4u.patch` の `git apply` 工程は残存する (Reflection では private method 内のロジック改変を逃せないため)。
2. EVMC4U 機能の実利用率は 5〜10% 以下であり、残り 90% の未使用コードが `Model==null` 早期 return 等で我々のデータ経路を妨害して patch を必要とさせている。
3. VMC プロトコル受信に必要な実装規模は ~210 行程度で自作可能と見積もれた。

これらを踏まえ、Reflection 化路線を破棄して「VMC 受信の自前実装」に spec の射程を再定義する。

## Boundary Context

- **In scope**:
  - `com.hidano.realtimeavatarcontroller.mocap-vmc` パッケージ内に VMC OSC 受信ロジックを自前実装する (`/VMC/Ext/Bone/Pos` および `/VMC/Ext/Root/Pos` を基本対応として完全カバー)
  - uOSC (`com.hidano.uosc`) の `uOscServer` を唯一の OSC 受信エンジンとして利用
  - VMC bone 名文字列を `UnityEngine.HumanBodyBones` へマッピングするテーブルの自前定義
  - `EVMC4UMoCapSource` / `EVMC4USharedReceiver` を後継クラス (`VmcMoCapSource` / `VmcSharedReceiver` 等、最終命名は design フェーズ) へ置換
  - 既存 `VMCMoCapSourceConfig` (`bindAddress` / `port`) およびその `.meta` GUID 不変性の維持
  - typeId `"VMC"` / `HumanoidMotionFrame` 形状 / 属性ベース自己登録 / 共有 receiver 参照カウント / Tick 経路 / `ISlotErrorChannel` 連携といった既存 `mocap-vmc` および `mocap-vmc-package-split` Spec 確定の不変条件
  - 既存テストの整理 (削除 / リネーム / 書換) と新規 OSC パーステストの追加
  - README / CHANGELOG の更新および `.kiro/steering/structure.md` の見直し
  - 移行クリーンアップ (旧 `Assets/EVMC4U/` の取り扱い、`evmc4u.patch` の旧 artifact 取り扱い) の方針確定

- **Out of scope**:
  - VMC Sender (送信側) 実装 (前 Spec 同様、本 Spec でも対象外)
  - VMC v2.1 拡張 (BlendShape `/VMC/Ext/Blend/Val`、表情、Camera、Light、Tracker Status 等) — 本 Spec は基本部 (Bone/Pos と Root/Pos) のみ対応する。VRM blend shape / Camera / Device 受信は対象外
  - その他 MoCap source (Mediapipe / Webcam / 物理センサー等) の追加
  - VRM 1.x 完全互換検証 (`mocap-vmc` Spec の方針を踏襲し、VRM 0.x 主対象、VRM 1.x は後続検討)
  - `_shared/contracts.md` § で定義された `IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` 等の抽象 API への変更
  - 上流 EVMC4U リポジトリへの PR や fork 作成
  - 動的アセンブリ読込 (`Assembly.LoadFrom` / `Assembly.Load(byte[])` 等) の機構

- **Adjacent expectations**:
  - **`mocap-vmc` Spec**: 本 Spec は当該 Spec で確定した `IMoCapSource` 契約 (`HumanoidMotionFrame` 発行・受信と Tick の分離・共有 receiver モデル・`ISlotErrorChannel` 通知方針) を実装手段を変えて満たす。
  - **`mocap-vmc-package-split` Spec**: パッケージ配置 (`com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime` 配下) と asmdef 名 (`RealtimeAvatarController.MoCap.VMC` 系列) を据置く。`VMCMoCapSourceConfig_Shared.asset` の GUID `5c4569b4a17944fba4667acebe26c25f` と `VMCMoCapSourceConfig.bindAddress` / `port` フィールドの serialized 形式を不変に保つ。
  - **`slot-core` / `motion-pipeline`**: 抽象 API (`IMoCapSource` / `MoCapSourceConfigBase` / `HumanoidMotionFrame` / `RegistryLocator` / `ISlotErrorChannel`) を本 Spec から参照する一方向依存を維持する。
  - **`com.hidano.uosc`**: `uOscServer` の `port` / `autoStart` プロパティ・`StartServer()` / `StopServer()` メソッド・`onDataReceived` UnityEvent を本 Spec の唯一の外部依存として直接型参照で利用する (Reflection 不要)。
  - **VMC 仕様 / プロトコル**: `https://protocol.vmc.info/` で公開される VMC プロトコル仕様 (基本部) を実装の準拠先とする。

---

## Requirements

### Requirement 1: 自前 VMC OSC 受信エンジン (uOSC 直接購読)

**Objective:** As a パッケージ実装者, I want EVMC4U に依存せず uOSC の `uOscServer` から直接 VMC OSC メッセージを受信できる経路, so that 利用者プロジェクト側の EVMC4U インポート / asmdef 自作 / `evmc4u.patch` 適用といった手作業を完全に撤廃できる。

#### Acceptance Criteria

1. The VMC native receiver shall シーン上に `uOscServer` コンポーネントを 1 個生成または取得し、その `onDataReceived` UnityEvent に直接購読する OSC メッセージハンドラを登録する。
2. When `Initialize(VMCMoCapSourceConfig)` が完了したとき, the VMC native receiver shall `uOscServer.port` に `VMCMoCapSourceConfig.port` を、`uOscServer.autoStart` に `false` を設定したうえで `StartServer()` を呼び出して受信を開始する。
3. The VMC native receiver shall OSC メッセージのアドレス文字列が `/VMC/Ext/Bone/Pos` の場合に bone OSC ハンドラへ、`/VMC/Ext/Root/Pos` の場合に root OSC ハンドラへ振り分ける。
4. The VMC native receiver shall 上記以外の OSC アドレス (`/VMC/Ext/Blend/Val`、`/VMC/Ext/Cam`、`/VMC/Ext/OK`、`/VMC/Ext/T` 等) を受信した場合は無視 (parse もデータ更新も行わない) し、例外を発生させない。
5. The VMC native receiver shall uOSC `uOscServer.Update` 内 dequeue により `onDataReceived` が MainThread で発火するという uOSC 2.x 系列の構造的保証 (`uOscServer.cs` の `Update`→`UpdateReceive`→`onDataReceived.Invoke` 経路、 dig-native.md N-C2 で実コード確認済み) に依存して受信処理を MainThread で完結させ、追加スレッドを生成しない。
6. When `Shutdown()` または `Dispose()` が呼ばれたとき, the VMC native receiver shall `uOscServer.StopServer()` を呼び、`onDataReceived` 購読を解除し、関連する内部辞書 / バッファをクリアする。
7. The VMC native receiver shall いかなる runtime / editor / tests コードからも `EVMC4U` 名前空間および `Assets/EVMC4U/` 配下のクラスへ参照を持たない。

---

### Requirement 2: `/VMC/Ext/Bone/Pos` および `/VMC/Ext/Root/Pos` メッセージのパース

**Objective:** As a パッケージ実装者, I want VMC プロトコル基本部の Bone / Root OSC メッセージを正しくパースして内部状態に反映できること, so that VSeeFace / VMagicMirror / VirtualMotionCapture 等の標準的な VMC 送信アプリから送られた骨情報を欠損なく受け取れる。

#### Acceptance Criteria

1. When OSC アドレス `/VMC/Ext/Bone/Pos` のメッセージを受信したとき, the VMC native receiver shall 引数列を `[string boneName, float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW]` (計 8 引数) として解釈する。
2. When OSC アドレス `/VMC/Ext/Root/Pos` のメッセージを受信したとき, the VMC native receiver shall 引数列を `[string rootName, float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW]` (計 8 引数) として解釈する。
3. When Bone メッセージのパースが成功したとき, the VMC native receiver shall `boneName` を `UnityEngine.HumanBodyBones` 値へマッピング (Requirement 3 参照) し、対応する `Quaternion(rotX, rotY, rotZ, rotW)` を内部 `Dictionary<HumanBodyBones, Quaternion>` の対応キーへ上書き格納する。
4. When Root メッセージのパースが成功したとき, the VMC native receiver shall `Vector3(posX, posY, posZ)` を最新 Root Position として、`Quaternion(rotX, rotY, rotZ, rotW)` を最新 Root Rotation として内部フィールドへ上書き格納する。
5. The VMC native receiver shall Bone メッセージの `posX` / `posY` / `posZ` 引数を受信時点で破棄してよい (現行 `EVMC4UMoCapSource` と同じく Bone Local Rotation のみ下流に伝播する設計)。
6. The VMC native receiver shall VMC v2.1 拡張で増える追加引数 (Root の 14 引数版等) を受信した場合、先頭 8 引数のみを基本部として解釈し残余を破棄する。残余引数を読まない経路で例外を発生させてはならない。
7. The VMC native receiver shall パース処理内で OSC メッセージの引数オブジェクト配列に対する追加コピーを行わず、必要な値だけを直接読み取るよう実装する (allocation 抑制のため。具体的な実装手段は design フェーズで確定)。

---

### Requirement 3: VMC bone 名 → `HumanBodyBones` マッピングテーブル

**Objective:** As a パッケージ実装者, I want VMC プロトコルが送出する bone 名文字列を Unity の `HumanBodyBones` enum 値へ確定的にマッピングできるテーブル, so that 受信した bone 名を任意の humanoid avatar に対して `HumanoidMotionApplier` 経由で適用できる。

#### Acceptance Criteria

1. The VMC native receiver shall VMC プロトコルで送出される標準 bone 名 (`Hips` / `Spine` / `Chest` / `UpperChest` / `Neck` / `Head` / `LeftShoulder` / `RightShoulder` / `LeftUpperArm` / `RightUpperArm` / `LeftLowerArm` / `RightLowerArm` / `LeftHand` / `RightHand` / `LeftUpperLeg` / `RightUpperLeg` / `LeftLowerLeg` / `RightLowerLeg` / `LeftFoot` / `RightFoot` / `LeftToes` / `RightToes` / `LeftEye` / `RightEye` / `Jaw` および 30 本の指 bone 群 `LeftThumbProximal` … `RightLittleDistal`) を `UnityEngine.HumanBodyBones` の対応する enum 値へ静的なテーブルでマッピングする。
2. The VMC native receiver shall マッピングテーブルが `HumanBodyBones.LastBone` を含まないこと (`LastBone` は terminator のため avatar 適用対象外) を保証する。
3. If 受信した bone 名がマッピングテーブルに存在しない場合, then the VMC native receiver shall 当該 OSC メッセージを無視 (内部辞書を更新せず) し、例外をスローしない。
4. While VMC native receiver の Tick 経路実行中, the マッピング検索 shall 各 bone について最大 1 回の辞書ルックアップで完了し、文字列の `Split` / `ToLower` 等の追加文字列操作を行わない。
5. The VMC native receiver shall マッピングテーブルを静的読み取り専用領域に保持し、インスタンス毎の再構築を行わない。
6. The mapping definition shall 引用元 (VMC 公式仕様 / EVMC4U 既存実装) を design フェーズで明記し、license / credit 上の取り扱いを README で謳う。

---

### Requirement 4: 共有受信コンポーネントのライフサイクル (refCount + DontDestroyOnLoad)

**Objective:** As a ランタイム統合者, I want 複数 Slot が単一の `VMCMoCapSourceConfig` を参照する場合に共有受信コンポーネントが 1 個だけ生存し、参照カウントで自動破棄されること, so that ポート競合を回避し、`MoCapSourceRegistry` の参照共有モデルとも整合する。

#### Acceptance Criteria

1. The VMC native shared receiver shall 静的辞書 (`VMCMoCapSourceConfig` インスタンスをキー) で参照カウントを管理し、最初の `Acquire(config)` でシーン上に共有 GameObject を 1 個生成する。
2. While Play Mode 実行中, the VMC native shared receiver GameObject shall `DontDestroyOnLoad` 属性を付与され、シーン遷移時に破棄されない。
3. When 参照カウントが 0 へ減少したとき, the VMC native shared receiver shall 共有 GameObject を `Destroy` し、内部辞書から該当エントリを除去し、`uOscServer.StopServer()` を呼び出してポートを解放する。
4. When `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` のタイミングが到来したとき, the VMC native shared receiver shall 静的辞書をクリアし、Domain Reload OFF 構成下でも前回 Play 状態の残留を持ち越さない。
5. While Edit Mode (Play Mode に入っていない状態), the VMC native shared receiver shall 共有 GameObject を `HideFlags.HideAndDontSave` 等で隠蔽し、シーン保存時のリーク (Hierarchy への漏出) を発生させない (具体的な隠蔽手段は design フェーズで確定)。
6. The VMC native shared receiver shall 既存 `EVMC4USharedReceiver` の参照カウント / 生成タイミング / 破棄タイミングと **同一の振る舞い** を維持する (新クラス名は Requirement 8 で扱う)。

---

### Requirement 5: typeId・Factory 自己登録・`HumanoidMotionFrame` 形状の不変性

**Objective:** As a 既存利用者・上位 Spec 設計者, I want EVMC4U 撤廃を行っても上位 (Slot / Registry / Motion Pipeline) から見た VMC ソースの公開契約が変わらないこと, so that 既存の `SlotSettings` / `VMCMoCapSourceConfig.asset` / UI Inspector / `MoCapSourceDescriptor` 構成を一切変更せずに新実装へ移行できる。

#### Acceptance Criteria

1. The VMC native MoCap source shall `IMoCapSource.SourceType` プロパティとして文字列 `"VMC"` を返す (typeId 据置)。
2. The VMC native MoCap source shall `IMoCapSource.MotionStream` から `IObservable<MotionFrame>` を発行し、具象型は `HumanoidMotionFrame` とする。`HumanoidMotionFrame` のフィールド構成 (`BoneLocalRotations` / `Muscles` / `RootPosition` / `RootRotation` / `Timestamp`) は変更しない。
3. The VMC native factory shall `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` を持つ静的メソッドにより `RegistryLocator.MoCapSourceRegistry.Register("VMC", new <FactoryClass>())` を Player / Standalone 起動時に呼ぶ。
4. The VMC native factory shall `[UnityEditor.InitializeOnLoadMethod]` を持つ静的メソッド (`RealtimeAvatarController.MoCap.VMC.Editor` asmdef 内) により Editor 起動時にも同等の自己登録を行う。
5. The VMC native MoCap source shall 各 Tick で `BoneLocalRotations` を内部 Dictionary の **snapshot コピー** として渡し、参照を直接渡さない (`HumanoidMotionFrame` のイミュータビリティ要件に準拠)。
6. The VMC native MoCap source shall `HumanoidMotionFrame.Muscles` には `Array.Empty<float>()` を渡し、適用経路を `BoneLocalRotations` のみに限定する。
7. The VMC native MoCap source shall `HumanoidMotionFrame.Timestamp` を `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で受信時点で打刻する。OSC bundle 層の timestamp (uOSC `Timestamp` 構造体) は使用しない (VMC アプリ層基本部の OSC メッセージには送信側 timestamp 引数自体が存在しない / dig-native.md N-F1 参照)。
8. If 内部 bone Dictionary がまだ空の状態で Tick が発火したとき, then the VMC native MoCap source shall `HumanoidMotionFrame` を発行しない (空 frame の連続発行を抑制)。
9. While 既存テストおよび既存 `VMCMoCapSourceConfig.asset` の serialized 形式, the VMC native implementation shall `VMCMoCapSourceConfig` の public フィールド `bindAddress` / `port` を同一名・同一型・同一既定値で維持する。

---

### Requirement 6: `VMCMoCapSourceConfig_Shared.asset` および関連 .meta GUID の保全

**Objective:** As a 既存サンプル利用者, I want EVMC4U 撤廃に伴うリネーム / 移動が起きても既存の SlotSettings の参照解決 GUID が破壊されないこと, so that VMC サンプルおよび既存 `SlotSettings_VMC_Slot1.asset` 等が `Missing Reference` 状態にならない。

#### Acceptance Criteria

1. The VMC native implementation shall `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Samples~/VMC/Data/VMCMoCapSourceConfig_Shared.asset` の `.meta` GUID を `5c4569b4a17944fba4667acebe26c25f` のまま据置く。
2. The VMC native implementation shall `VMCMoCapSourceConfig` クラスの `.cs` ファイル `.meta` GUID を変更しない (移動・リネームが発生する場合も既存 GUID を継承する)。
3. If 既存 Runtime クラスファイル (`EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `VMCMoCapSourceFactory.cs` / `VMCMoCapSourceConfig.cs` / `AssemblyInfo.cs`) のいずれかをリネームまたは置換する場合, then the VMC native implementation shall 旧 `.meta` GUID を新ファイル `.meta` に **移植** し、Inspector / asset bundle / ProjectSettings からの GUID 参照を保つ。
4. When 検証用に Unity Editor を再オープンしたとき, the VMC sample (`VMCReceiveDemo.unity` 等) shall `MissingReferenceException` / `The associated script can not be loaded` 系警告を出さずに開ける。
5. The VMC native implementation shall 新規追加ファイル (新 OSC パーサクラスや新 mapping テーブルクラス等) の `.meta` GUID を `[guid]::NewGuid().ToString('N')` 等で生成した乱数 32 桁 hex とし、既存 GUID をコピーしない / 連続パターンや 1 文字シフトのローテーション系列を使用しない。

---

### Requirement 7: EVMC4U 依存および `evmc4u.patch` の完全撤廃

**Objective:** As a パッケージ利用者, I want EVMC4U の `.unitypackage` 取得・asmdef 作成・`git apply evmc4u.patch` のいずれもが本パッケージ利用に不要となること, so that 利用者の手元準備が「uOSC を入れるだけ」に縮約される。

#### Acceptance Criteria

1. The `RealtimeAvatarController.MoCap.VMC` Runtime asmdef shall `references` から `"EVMC4U"` を削除し、`"uOSC.Runtime"` のみを VMC 受信のための外部依存として保持する (`RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `UniRx` 等のコア参照は維持)。
2. The `RealtimeAvatarController.MoCap.VMC` Editor asmdef shall `references` から `"EVMC4U"` を削除する。
3. The `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` および `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` asmdef shall `references` から `"EVMC4U"` を削除し、`"uOSC.Runtime"` を維持する。
4. The VMC native implementation shall ランタイム / Editor / Tests のいずれの `.cs` においても `using EVMC4U;` ステートメントを含まない。
5. If 利用者プロジェクトの `Assets/EVMC4U/` ディレクトリが存在しない場合, then the VMC native implementation shall コンパイル / Play Mode / Test Runner のいずれも追加のセットアップを要求せず正常動作する (utility としての uOSC 導入のみで完結)。
6. README 改訂 (EVMC4U `.unitypackage` インポート手順 / `EVMC4U.asmdef` 作成手順 / `evmc4u.patch` 適用手順の削除を含む) は Requirement 12 (R-12.1) に集約する。本 Requirement 7 では「これらの手順が利用者作業として不要となる」 という事実宣言のみを行い、文書改訂タスクとしての分担は重複しない (dig-native.md N-H1 参照)。
7. Where VMC プロトコル仕様の理解として EVMC4U の解読 / 設計から借用した知見 (例: 共有 receiver パターン / refCount 生存管理 / `SubsystemRegistration` リセット) があれば, the VMC native implementation shall README / CHANGELOG / コード内コメントで EVMC4U (MIT) への credit / inspiration を明記する。bone 名 ↔ `HumanBodyBones` マッピングテーブルそのものは Unity 公開 `HumanBodyBones` enum の機械的列挙であり、EVMC4U 由来のオリジナリティを借用しないため license credit 義務は発生しない (dig-native.md N-C3 / N-F2 で確定)。

---

### Requirement 8: クラス命名と内部参照のリネーム方針

**Objective:** As a パッケージ実装者, I want EVMC4U が無くなった以上、命名から `EVMC4U` を取り除いた新クラス名で内部実装を表現したい, so that 名前と実体の一致が保たれ、新規参加者が混乱しない。

#### Acceptance Criteria

1. The VMC native implementation shall 旧クラス `EVMC4UMoCapSource` を `VmcMoCapSource` (もしくは design フェーズで確定する `VMCMoCapSource` 等) へリネームする。最終命名は `_shared/contracts.md` の命名規約と整合させる。
2. The VMC native implementation shall 旧クラス `EVMC4USharedReceiver` を `VmcSharedReceiver` (もしくは design フェーズで確定する別名) へリネームする。
3. When クラスをリネームしたとき, the VMC native implementation shall 旧 `.cs` の `.meta` GUID を新 `.cs` ファイルへ移植し (Requirement 6.3 に従う)、既存 ProjectSettings / Inspector からの GUID 参照を保つ。
4. The VMC native implementation shall 旧クラス名を後方互換 type alias / partial / `[MovedFrom]` 属性で残すか否かを design フェーズで決定し、決定理由 (互換不要 / 互換必要) を design.md に明記する。
5. The VMC native MoCap source class shall public な API シグネチャ (`SourceType` / `MotionStream` / `Initialize(MoCapSourceConfigBase)` / `Shutdown()` / `Dispose()`) を旧クラスから変更しない (`IMoCapSource` 契約準拠のため)。
6. Where 旧 `EVMC4USharedReceiver.Receiver` プロパティのような EVMC4U 型を直接公開する API が存在する場合, the VMC native implementation shall 当該プロパティを削除または `internal` 化し、テスト経路は `[InternalsVisibleTo]` で扱う。
7. The VMC native implementation shall public 型・public メンバ・public field のいずれにおいても、宣言型として `EVMC4U.*` を含まないことを保証する。

---

### Requirement 9: テストスイートの整理 (削除・リネーム・新規追加)

**Objective:** As a 開発者, I want EVMC4U 撤廃に伴って意味を失うテストを整理し、新たに OSC パース・bone マッピング・異常系を検証する単体テストを追加できること, so that 自前 VMC 受信実装の品質を CI で継続的に担保できる。

#### Acceptance Criteria

1. The VMC native implementation shall 既存 `Tests/EditMode/ExternalReceiverPatchTests.cs` を削除する (`evmc4u.patch` が存在しなくなるため検証対象が消滅)。
2. The VMC native implementation shall 既存 `Tests/EditMode/EVMC4USharedReceiverTests.cs` を **削除** し、新規 `Tests/EditMode/VmcSharedReceiverTests.cs` (もしくは design フェーズで確定する後継クラス名に対応する新規テストファイル) を新規追加する。新規テストは refCount / DontDestroyOnLoad / `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` での static リセット / 重複 `Acquire` の挙動を新実装に対して検証する。 git rename での history 継承は行わない (テスト対象が `EVMC4U.ExternalReceiver` 依存から uOSC 直接購読モデルへ抜本的に変わるため、 dig-native.md N-H2 参照)。
3. The VMC native implementation shall 既存 `Tests/PlayMode/EVMC4UMoCapSourceIntegrationTests.cs` を、実 OSC パケットを `uOscServer` 経由で投入する (もしくは内部公開された注入 API を経由する) 統合テストとして書き換える。
4. The VMC native implementation shall 既存 `Tests/EditMode/EVMC4UMoCapSourceTests.cs` / `Tests/PlayMode/EVMC4UMoCapSourceSharingTests.cs` / `Tests/EditMode/VmcConfigCastTests.cs` / `Tests/EditMode/VmcFactoryRegistrationTests.cs` / `Tests/PlayMode/SampleSceneSmokeTests.cs` を新クラス名へのリネーム / using 削除 / API 不変性 (`SourceType=="VMC"` / `Initialize` 動作 / Factory 登録) の再検証に絞って維持する。
5. The VMC native implementation shall 新規 EditMode テストとして以下を追加する:
   - `/VMC/Ext/Bone/Pos` メッセージのパース成功時に内部 Dictionary が期待通り更新されることを検証
   - `/VMC/Ext/Root/Pos` メッセージのパース成功時に内部 Root Position / Rotation が更新されることを検証
   - bone 名 → `HumanBodyBones` マッピング完全性 (Requirement 3.1 で列挙した全 bone 名がマッピング可能であること) を検証
6. The VMC native implementation shall 新規 EditMode テストとして異常系を追加する:
   - 引数数不足 (8 引数未満) のメッセージ受信時に例外を投げず処理を継続する
   - 引数型不一致 (string が来るべき位置に float が来る等) を受信時に例外を投げず当該メッセージのみ無視する
   - 未知の bone 名を受信時に内部 Dictionary を更新せず処理を継続する
   - 未知の OSC アドレスを受信時に処理を継続する
7. If `Receiver` プロパティのような EVMC4U 直公開型を経由していた既存テストが存在した場合, then the VMC native implementation shall 当該テストを `[InternalsVisibleTo]` を活用した internal アクセスへ書き換えるか、新たな internal な注入 API (例: `InjectBoneRotationForTest(HumanBodyBones, Quaternion)`) を新クラス側に持たせる。
8. The Tests asmdef shall `references` から `"EVMC4U"` を撤廃し、`"uOSC.Runtime"` を保持する (Requirement 7.3 と整合)。

---

### Requirement 10: 性能・アロケーション要件 (Tick ホットパス、 アプリケーション層スコープ)

**Objective:** As a ランタイム統合者, I want アプリケーション層 (本パッケージ Runtime) が Tick あたりに発生させる managed allocation を限りなくゼロに保てる実装方針, so that GC スパイクによる avatar アニメーションの揺らぎが発生せず、複数 Slot 同時稼働時もフレームレートが安定する。

> **本要件のスコープ境界 (dig-native.md N-C1 で確定)**:
> uOSC `Parser.ParseData` は受信メッセージごとに `new object[n]` + `float` boxing 等の structural alloc を **必ず発生させる** (uOSC 2.x 系列の構造的特性、現行 `Library/PackageCache/com.hidano.uosc@*/Runtime/Core/Parser.cs` で確認済み)。 これは uOSC の API 仕様に組込まれた所与の挙動であり、 本パッケージは uOSC を fork せずに利用するため (Out of Scope)、 本要件は uOSC layer 内の structural alloc を **対象外** とする。 計測の対象範囲は「`onDataReceived` ハンドラ受領後 (= `uOSC.Message message` を受け取って以降)」 から「`HumanoidMotionFrame` を `MotionStream` に OnNext する直前まで」 のアプリケーション層内に限定する。

#### Acceptance Criteria

1. The VMC native MoCap source shall アプリケーション層内 Tick (`HumanoidMotionFrame` 1 回発行) あたりの追加 managed allocation を、`HumanoidMotionFrame` インスタンス生成 / `BoneLocalRotations` 用の snapshot コンテナ提供 (再利用バッファ ロテーション戦略を採るため初期化後は alloc 0、 詳細は R-A) に伴う避けられないものに限定する。
2. The VMC native MoCap source shall OSC ハンドラのアプリケーション層処理 (1 メッセージ受信あたり、 uOSC `Parser` 完了後) において、`new` による `Dictionary` 生成 / `List` 生成 / `string` 連結 / `string.Split` / `ToLower` 等の追加 allocation を行わない。 受信した bone name string および `object[] values` (uOSC 由来) は読み取りのみ行い、 コピーは取らない。
3. The VMC native MoCap source shall bone 名 → `HumanBodyBones` 検索を 1 回の `Dictionary<string, HumanBodyBones>.TryGetValue` のみで完結させる。 マッピング辞書は `Enum.GetValues(typeof(HumanBodyBones))` ベースの static readonly 辞書として起動時 1 回のみ確保する (dig-native.md N-C3)。
4. The VMC native MoCap source shall snapshot 用の Dictionary バッファをダブルバッファリング (受信書込側 + Tick 読み出し側、 詳細は R-A) で再利用する設計を採用する。 `HumanoidMotionFrame.BoneLocalRotations` の所有権契約 (Applier への移譲) と両立する具体的な参照寿命管理は design フェーズで確定し、 同 frame 完結性前提を `HumanoidMotionApplier` の実コードで確認する (dig-native.md N-C4)。
5. While CI 上の性能測定が利用可能な場合, the VMC native implementation shall アプリケーション層内 Tick あたり target allocation を `0 byte` (起動時の固定バッファ確保および `HumanoidMotionFrame` インスタンス生成は除外) として設計し、IL2CPP / Mono 双方で達成可能な範囲を design / validation で実測する。 計測区間は本要件冒頭の「スコープ境界」 で示した範囲とする。
6. The VMC native MoCap source shall uOSC `Message.values` (`object[]` 形式 / float boxing 済み) からの読み取り経路を boxing 前提として受け入れ、 アプリケーション層側で追加の boxing / unboxing コピーを発生させない実装に留める。 uOSC 側 API 改修 (生 byte buffer の expose 等) は本 Spec の Out of Scope (uOSC fork は実施しない / dig-native.md N-C1)。

---

### Requirement 11: エラー処理・耐障害性

**Objective:** As a ランタイム統合者, I want 不正 OSC パケット・ポート競合・受信中の例外といった異常系で `MotionStream` 購読者にエラーを伝播させず、診断情報のみを `ISlotErrorChannel` 経由で通知する設計, so that 上位側にエラーリカバリロジックを持たせずに運用監視ができる。

#### Acceptance Criteria

1. The VMC native MoCap source shall `MotionStream` の `IObserver<MotionFrame>.OnError()` を一切発行しない (旧 `mocap-vmc` Spec 要件 8.1 を継続)。
2. If OSC パース処理 (`/VMC/Ext/Bone/Pos` 等) 内で型変換 / cast 失敗 / 引数数不足 / 不正 quaternion 値などの例外が発生したとき, then the VMC native receiver shall 当該メッセージのみ捨てて処理を継続し、例外を `onDataReceived` 購読チェーンの外へ伝播させない。
3. If OSC パース処理内部で予期しない例外 (NullReferenceException / IndexOutOfRangeException 等) が発生したとき, then the VMC native receiver shall `RegistryLocator.ErrorChannel.Publish(SlotError(slotId, SlotErrorCategory.VmcReceive, ex, UtcNow))` を呼び、Debug.LogError の抑制制御は `DefaultSlotErrorChannel` に委ねる。
4. If `uOscServer.StartServer()` がポートバインドに失敗 (ポート使用中・権限不足等) したとき, then the VMC native MoCap source shall `Initialize()` から例外を呼び出し元へスローし、`SlotManager` が `SlotErrorCategory.InitFailure` として `ISlotErrorChannel` へ通知する経路に乗せる。
5. The VMC native MoCap source shall `Shutdown()` / `Dispose()` の冪等性を保証し、二重呼び出しで例外を発生させない。
6. While 受信タイムアウトの実装, the VMC native MoCap source shall 初期版で実装しない (前 Spec 要件 8.6 を継続。design フェーズで必要性を再検討する)。
7. The VMC native MoCap source shall 二重 `Initialize()` 呼び出しを `InvalidOperationException` で拒否する (前 Spec 要件 7.5 を継続)。

---

### Requirement 12: ドキュメント・移行クリーンアップ・サンプル整合性

**Objective:** As a パッケージ利用者・メンテナ, I want EVMC4U 撤廃に伴う README / CHANGELOG / steering の更新および旧 artifact の整理が完了していること, so that 移行後の利用者がセットアップ手順を迷わず、リポジトリ内に陳腐化した artifact が残らない。

#### Acceptance Criteria

1. The VMC native implementation shall パッケージ README (`Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/README.md`) を全面改訂し、利用者準備手順を「uOSC を導入する。これだけ。」に縮約する。EVMC4U / `evmc4u.patch` への参照は credit / 歴史的経緯セクション以外から削除する。
2. The VMC native implementation shall パッケージ CHANGELOG (`Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/CHANGELOG.md`) に EVMC4U 撤廃を記録し、`IMoCapSource` 公開契約は不変であるが内部実装が breaking change である旨を明記する。
3. The VMC native implementation shall リポジトリルート README (もしくはコアパッケージ README) で VMC パッケージの利用者準備が EVMC4U 不要に変わった旨を反映する。
4. The VMC native implementation shall `.kiro/steering/structure.md` の VMC パッケージ説明を「EVMC4U 連携による VMC 受信」から「自前実装による VMC 受信 (uOSC のみ依存)」へ更新する。
5. The VMC native implementation shall `.kiro/specs/mocap-vmc/evmc4u.patch` を削除するか、または冒頭に **OBSOLETE** マーカと obsolete 化日時 (`2026-05-09` 以降) を記載した上で履歴用に残すかの判断を design フェーズで確定し、決定をその場で実行する。
6. The VMC native implementation shall リポジトリ内の `RealtimeAvatarController/Assets/EVMC4U/` ディレクトリを **削除** することを既定方針とし (旧テスト用に保持していたため)、historical reference として残す必要があれば `.kiro/specs/mocap-vmc/handover-*.md` 等に移動する。実際の削除は親セッションでのみ実施可能 (子 Agent は rm 不可) であることを tasks.md で明示する。
7. While 既存 VMC サンプル (`Samples~/VMC/`) 内のシーンおよびアセット, the VMC native implementation shall サンプルを開いた際の `.asset` GUID 参照が破壊されないことを検証シナリオで確認する。
8. The VMC native implementation shall design フェーズで以下を research item として明示する:
   - 旧クラス向け `[MovedFrom]` 属性 (Unity の `UnityEngine.Scripting.APIUpdating.MovedFromAttribute`) の必要性と適用範囲 (dig-native.md N-R2 で「不要」 が暫定結論、design で実コード確認のうえ確定)
   - VMC v2.1 拡張 (Root 14 引数版・BlendShape 等) の将来サポート可否
   - uOSC `DotNet/Udp.StartServer` 同期/非同期挙動と R-11.4 `SocketException` 伝播経路の妥当性確認 (dig-native.md N-H3)
   - VMC 公式仕様での bone 名 PascalCase 規定確認と case-sensitive matching の妥当性 (dig-native.md N-F3)

---

## 設計フェーズへ繰り越す research items

設計フェーズで明示的に決定すべき技術的論点 (要件レベルでは方針のみ記載):

- R-A. snapshot Dictionary の再利用戦略 (推奨方向: ダブルバッファリング + 同 frame 完結性前提による契約緩和): `Dictionary<HumanBodyBones, Quaternion>` をフィールドに 2 個保持して交互に書込/発行する案を本命とし、 `HumanoidMotionApplier` が同 frame 内で消費完了する前提を実コード読解で確認する (dig-native.md N-C4)。 確認できなければ毎 Tick 新規 `Dictionary` 生成 (= 現行実装と同等、 alloc 0 不可) に retreat する。 `MotionCache` 等の他購読者が frame をまたいで参照保持する経路がないかも要確認。
- R-C. 旧クラス名 (`EVMC4UMoCapSource` / `EVMC4USharedReceiver`) を `[MovedFrom]` で残すか否か。 旧 `EVMC4UMoCapSource` は plain C# (MonoBehaviour ではない / SerializeReference 参照無し)、 `EVMC4USharedReceiver` は MonoBehaviour だが factory 経由で動的生成のみでシーンに配置されない。 これらが本当に外部参照を持たないかを design で実コード確認の上、 結論として `[MovedFrom]` 不要と確定する見込み (dig-native.md N-R2)。
- R-E. VMC v2.1 拡張 (Root 14 引数版・`/VMC/Ext/Blend/Val` 等) のサポート可否を「本 Spec で実装する / 後続 Spec へ繰り越す」 で明確化。 現状方針は「基本部のみ」 だが、 実装難易度が低い拡張は同梱するかを design で再検討。
- R-F. `evmc4u.patch` および `Assets/EVMC4U/` の削除 / 保持方針 (Requirement 12.5 / 12.6) の最終確定。 推奨方向: 全削除 + uOSC / VRM / UniGLTF credit を `THIRD_PARTY_NOTICES.md` (新規) へ転記 (dig-native.md N-M1)。
- R-G. uOSC `DotNet/Udp.StartServer` のポートバインドが MainThread 同期かワーカー非同期かを `Library/PackageCache/com.hidano.uosc@*/Runtime/Core/DotNet/Udp.cs` で実コード確認し、 R-11.4 の `SocketException` MainThread 伝播経路の妥当性を確定する (dig-native.md N-H3)。 非同期だった場合は `uOscServer.onServerStarted` UnityEvent + 別 callback による bind 失敗検出経路へ再設計する。
- R-H. VMC 公式仕様 (https://protocol.vmc.info/) で bone 名が PascalCase 規定かを文書レベルで確認し、 case-sensitive matching (R-3.4 「ToLower 等を行わない」) が安全な選択かを確定する (dig-native.md N-F3)。 規定が緩い場合は case-insensitive 経路を予備実装として残す可能性。

> 解消済み research items (本 dig 中に解決):
> - 旧 R-B (uOSC `onDataReceived` のメインスレッド発火再検証): uOSC 2.x 系列で `uOscServer.UpdateReceive`→`onDataReceived.Invoke` 経路が MainThread から発火することを構造的に保証することを実コード確認済み (dig-native.md N-C2)。
> - 旧 R-D (bone 名マッピング license credit): マッピングテーブルは Unity 公開 `HumanBodyBones` enum の機械的列挙であり EVMC4U 由来のオリジナリティを借用しないため license credit 義務なし。 設計借用元としての EVMC4U credit は R-7.7 で別途明記する (dig-native.md N-C3 / N-F2)。
