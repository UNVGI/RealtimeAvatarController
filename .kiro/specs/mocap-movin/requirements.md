# Requirements Document

## Project Description (Input)
mocap-movin: MOVIN モーションキャプチャシステムを新規 UPM パッケージとして追加する。本体パッケージ jp.co.unvgi.realtimeavatarcontroller には一切手を入れず、`jp.co.unvgi.realtimeavatarcontroller.movin` という別 UPM パッケージで自己完結させる。

## 背景
本リポジトリは VTuber 向けのリアルタイムモーション受信・アバター切替ツール (jp.co.unvgi.realtimeavatarcontroller) の社内フォーク。本体は IMoCapSource / IMoCapSourceFactory / MoCapSourceConfigBase / MoCapSourceDescriptor / RegistryLocator (registry-based dispatch by string typeId) という拡張点を備えており、外部 UPM パッケージから MoCap ソースを追加できる設計になっている。本 spec はその拡張点を使って MOVIN サポートを追加する初の事例となる。

## 対象 MoCap デバイス: MOVIN
内部プロトコルは VMC (OSC over UDP) だが、以下が標準 VMC / EVMC4U と異なる:
- デフォルトポート 11235 (標準 VMC の 39539 ではない)
- Humanoid リターゲットではなく **Generic リグの Transform 直接書き込み**で適用 (NeoMOVINMan のような非 Humanoid キャラクタ前提)
- 骨は `prefix:boneName` 形式 (例 `mixamorig:Hips`, `MOVIN:Spine`) で送られ、Unity 側で同名 Transform に 1:1 適用
- `boneClass` prefix によるフィルタ機能あり
- `/VMC/Ext/Root/Pos` の v2.1 拡張で localScale / localOffset を毎フレーム送出

参考資料:
- 公式 Unity ガイド: https://help.movin3d.com/movin-studio-usage-guide/live-streaming/streaming-mocap-data-into-unity
- 同梱サンプル: `RealtimeAvatarController/Assets/MOVIN/` (MocapReceiver.cs / VMCReceiver.cs / NeoMOVINMan_Unity.prefab / Sample_Ch14・Ch29・MOVINman シーン)

## スコープ
### 本 spec で実装するもの
- 新 UPM パッケージ `jp.co.unvgi.realtimeavatarcontroller.movin` の雛形 (package.json / Runtime / Editor / Tests / Samples~)
- `MovinMoCapSource : IMoCapSource` — uOSC で 11235 に bind、VMC OSC 受信、メインスレッドで emit
- `MovinMoCapSourceFactory : IMoCapSourceFactory` — typeId="MOVIN" で属性ベース自己登録 (Runtime / Editor の二経路)
- `MovinMoCapSourceConfig : MoCapSourceConfigBase` — port / bindAddress / rootBoneName / boneClass (将来追加プロパティ余地)
- MOVIN 専用 MotionFrame 型 (Generic Transform ベース、name キーで localPosition/Rotation/Scale を保持)
- MOVIN 専用 Applier — Avatar Transform ツリーを走査して name 一致で直接書き込み (本体側 HumanoidMotionApplier には乗せない、自己完結)
- 自己登録パターンは VMC 実装 (`Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs` / `Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs`) を踏襲
- Samples~/MOVIN: NeoMOVINMan を使ったデモシーン
- EditMode / PlayMode テスト

### 本 spec で実装しないもの (out of scope)
- Humanoid リターゲット経路のサポート (将来拡張、別 spec)
- 本体パッケージ (jp.co.unvgi.realtimeavatarcontroller) の改変
- 既存 HumanoidMotionApplier / GenericMotionFrame の流用
- MOVIN Studio 側の設定自動化
- 表情 (Blend) / カメラ / HMD / Controller / Tracker 系 OSC アドレスの処理 (将来拡張)

## 制約
- 本体パッケージは一切改変しない (拡張点経由のみ)
- Unity 6000.3.10f1
- 依存: RealtimeAvatarController.Core / RealtimeAvatarController.Motion (asmdef name 参照) / com.hidano.uosc / UniRx / UniTask
- Player Build / Editor の両環境で動作 (Domain Reload OFF も考慮)
- 既存 VMC (typeId="VMC") との並行稼動可 (異なるポートで同居)

## アーキ方針
1. **完全自己完結**: 本パッケージ内で Source → MotionFrame → Applier まで一貫して提供。本体側の MotionFrame / Applier には依存しない (継承元として `RealtimeAvatarController.Core.MotionFrame` のみ参照)。
2. **typeId = "MOVIN"**: SlotSettings の MoCapSourceDescriptor.SourceTypeId から一意に dispatch される。
3. **Slot との結線**: Sample 側で SlotManager.TryGetSlotResources を介して Source と Avatar を取得し、本パッケージ提供の MovinMotionApplier を駆動する (or 上位層で同等の連携を提供)。具体的な結線方式は requirements / design で詰める。

## 利用シナリオ
1. 利用者は Unity プロジェクトの manifest.json に本パッケージと本体パッケージを追加
2. SlotSettings アセットを作成し、MoCapSourceDescriptor.SourceTypeId に "MOVIN" を選択 (Sample SlotSettingsEditor が GetRegisteredTypeIds で動的列挙)
3. MovinMoCapSourceConfig アセットを作成 (port=11235, rootBoneName, boneClass を設定)
4. MOVIN Studio で Platform=Unity, Port=11235 を設定して Start Streaming
5. アバターに対してリアルタイムにモーションが適用される

## スコープ境界 (補足)

- **スコープ内**:
  - 新規外部 UPM パッケージ `jp.co.unvgi.realtimeavatarcontroller.movin` の雛形整備 (package.json / asmdef / ディレクトリ構成)
  - `IMoCapSource` を実装する `MovinMoCapSource` 具象クラス
  - `IMoCapSourceFactory` を実装する `MovinMoCapSourceFactory` (typeId="MOVIN") と Runtime / Editor 二経路の自己登録
  - `MoCapSourceConfigBase` を継承する `MovinMoCapSourceConfig` (port / bindAddress / rootBoneName / boneClass)
  - MOVIN 専用 MotionFrame 型 (Generic Transform ツリーを bone 名キーで保持)
  - MOVIN 専用 Applier (Generic Transform への直接書き込み)
  - MOVIN 受信用 OSC アドレス (`/VMC/Ext/Root/Pos`, `/VMC/Ext/Bone/Pos`) の取り扱い
  - `Samples~/MOVIN` にデモシーン (`NeoMOVINMan` を使ったサンプル)
  - EditMode / PlayMode テスト

- **スコープ外**:
  - 本体パッケージ `jp.co.unvgi.realtimeavatarcontroller` のソースコード改変 (拡張点経由のみ)
  - Humanoid リターゲット経路 (`HumanoidMotionApplier` / `HumanoidMotionFrame`) の利用・改変
  - VMC 標準ポート (39539) / EVMC4U との差し替え (typeId="VMC" は変更しない)
  - 表情 (Blend) / カメラ / HMD / Controller / Tracker 系 OSC アドレスの処理
  - MOVIN Studio 送信側の設定自動化
  - VRM 0.x / 1.x ベースの Humanoid アバター対応 (本 spec は Generic 専用)

- **隣接 Spec / コンポーネントとの関係**:
  - `slot-core`: `IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` / `MoCapSourceDescriptor` / `RegistryLocator` / `ISlotErrorChannel` を参照する (本体パッケージ提供 IF を非改変で利用)
  - `motion-pipeline`: 基底 `MotionFrame` 型のみ参照する。`HumanoidMotionFrame` には依存しない
  - `mocap-vmc`: 別 typeId ("VMC") として並行稼動。命名・粒度・自己登録パターンの参考とするが、コードを共有しない
  - `Assets/MOVIN/`: 同梱サンプル `MocapReceiver` / `VMCReceiver` / `NeoMOVINMan` 等の既存資産は実装ヒントおよび `Samples~` 配置元として扱い、ランタイム本体への直接依存は持たない
  - `com.hidano.uosc`: OSC 受信実装の依存先

---

## Requirements

### Requirement 1: 外部 UPM パッケージ化と本体非改変制約

**目的:** As a パッケージ管理者, I want MOVIN サポートを独立した UPM パッケージとして配布できること, so that 本体パッケージを改変せず、利用プロジェクトで MOVIN 機能を任意に追加・削除できる。

#### Acceptance Criteria

1. The `mocap-movin` Spec shall 新規 UPM パッケージ `jp.co.unvgi.realtimeavatarcontroller.movin` をリポジトリ内に作成し、`package.json` / Runtime / Editor / Tests / `Samples~` のディレクトリ構成を持たせる。
2. The `mocap-movin` Spec shall 本体パッケージ `jp.co.unvgi.realtimeavatarcontroller` の既存ファイル (Runtime / Editor / Tests / Samples~) を一切改変せず、本体側の公開拡張点 (`IMoCapSource` / `IMoCapSourceFactory` / `MoCapSourceConfigBase` / `MoCapSourceDescriptor` / `RegistryLocator`) のみを通じて統合する。
3. The `package.json` shall `name="jp.co.unvgi.realtimeavatarcontroller.movin"`, `unity="6000.3"`, および本体パッケージ (`jp.co.unvgi.realtimeavatarcontroller`) と `com.hidano.uosc` への依存を `dependencies` に宣言する。
4. The Runtime asmdef shall `RealtimeAvatarController.Core` および `RealtimeAvatarController.Motion` を name 参照で持ち、GUID 参照を使用しない (本体パッケージのリネーム耐性を確保する)。
5. If 本体パッケージのソースコードを改変する変更が要求されたとき, then the `mocap-movin` Spec shall 当該変更を本 spec のスコープ外として扱い、別 spec 化または上流フィードバックとして分離する。
6. The `mocap-movin` package shall 本パッケージを利用プロジェクトの `Packages/manifest.json` に追加するだけで、シーン側に追加コードを書かずに MOVIN typeId が `RegistryLocator.MoCapSourceRegistry` に登録される状態を提供する。

---

### Requirement 2: 完全自己完結アーキテクチャ (Source → MotionFrame → Applier)

**目的:** As a Spec 設計者, I want Source・MotionFrame・Applier の 3 要素を本パッケージ内で一貫して提供すること, so that 本体側 `HumanoidMotionApplier` / `HumanoidMotionFrame` への依存・改変を発生させずに MOVIN 固有の Generic Transform 適用経路を完結できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall MOVIN 専用の MotionFrame 派生型 (例: `MovinMotionFrame`) を本パッケージ内に定義し、基底型として `RealtimeAvatarController.Core.MotionFrame` のみを継承元とする。
2. The `MovinMotionFrame` shall ボーン名 (string) をキーとして `localPosition` / `localRotation` / `localScale` を保持できるイミュータブルなデータ構造を提供する (具体的なフィールド形状は design フェーズで確定する)。
3. The `mocap-movin` package shall MOVIN 専用 Applier (例: `MovinMotionApplier`) を本パッケージ内に定義し、`MovinMotionFrame` の内容を Avatar の Transform ツリーへ直接書き込む責務を負う。
4. The `MovinMotionApplier` shall 本体側 `HumanoidMotionApplier` を継承・流用せず、`HumanoidMotionFrame` も参照しない。
5. The `MovinMoCapSource.MotionStream` shall `IObservable<MotionFrame>` として公開し、発行する具象型は `MovinMotionFrame` とする。
6. While 本パッケージの Runtime コード, the Runtime shall 本体パッケージの `Humanoid*` 名前を持つ型 (例: `HumanoidMotionFrame`, `HumanoidMotionApplier`) に対する型参照を持たない。

---

### Requirement 3: MOVIN プロトコル受信ソース (`MovinMoCapSource`)

**目的:** As a ランタイム統合者, I want `IMoCapSource` を実装した MOVIN 受信ソースが提供されること, so that Slot に MOVIN データソースを割り当てて MOVIN Studio からのライブモーションを受信・配信できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall `MovinMoCapSource : IMoCapSource, IDisposable` を 1 クラス定義する。
2. The `MovinMoCapSource.SourceType` プロパティ shall 定数文字列 `"MOVIN"` を返す。
3. When `Initialize(MoCapSourceConfigBase config)` が呼ばれたとき, the `MovinMoCapSource` shall `config` を `MovinMoCapSourceConfig` にキャストし、キャスト失敗時は受け取った型名を含む `ArgumentException` をスローする。
4. When `Initialize()` が成功したとき, the `MovinMoCapSource` shall `MovinMoCapSourceConfig.port` (既定 11235) および `bindAddress` を用いて uOSC で UDP ポートをバインドし、`/VMC/Ext/Root/Pos` および `/VMC/Ext/Bone/Pos` の OSC アドレスを購読する。
5. The `MovinMoCapSource` shall 受信したボーンを Tick 時点でスナップショット化し、`MovinMotionFrame` を組み立てて `MotionStream.OnNext()` に発行する (受信スレッドから直接 Subject を駆動するか LateUpdate 同期化するかは design フェーズで確定する)。
6. The `MovinMoCapSource.MotionStream` shall `OnError()` を発行せず、受信エラーはストリームを継続したまま内部処理する (本体側 `IMoCapSource` 契約の継続)。
7. While `Initialize()` 未完了の状態, the `MovinMoCapSource` shall `MotionStream` 購読に対してフレームを発行せず、購読自体は許容する (空ストリームとしてふるまう)。
8. When `Shutdown()` または `Dispose()` が呼ばれたとき, the `MovinMoCapSource` shall UDP ソケットを解放し、Tick 駆動を停止し、冪等な破棄操作として動作する (二重呼び出しで例外をスローしない)。
9. If 同一プロセス内で typeId="VMC" (本体側 EVMC4U ベース) と typeId="MOVIN" が異なる port で同時にバインドされたとき, the 両ソース shall 互いに干渉せずに独立動作する (port 競合がない限り並行稼動を保証する)。
10. The `MovinMoCapSource` shall MOVIN プロトコルが採用する OSC アドレス体系 (VMC 互換) に従い、`/VMC/Ext/Bone/Pos` のボーン名は `prefix:boneName` 形式 (例: `mixamorig:Hips`) のままフレームへ伝播する (Humanoid マッピング変換は行わない)。
11. Where `/VMC/Ext/Root/Pos` の v2.1 拡張 (localScale / localOffset) を受信したとき, the `MovinMoCapSource` shall localScale を `MovinMotionFrame` に格納し、Applier 側で適用できる形で伝播する。

---

### Requirement 4: 設定 (`MovinMoCapSourceConfig`)

**目的:** As a エディタユーザー, I want MOVIN 受信パラメータを ScriptableObject アセットとして編集できること, so that シーンを変更せずに通信設定や骨フィルタを切り替えられる。

#### Acceptance Criteria

1. The `mocap-movin` package shall `MovinMoCapSourceConfig : MoCapSourceConfigBase` を `RealtimeAvatarController.MoCap.Movin` (またはこれに準ずる) 名前空間で定義する。
2. The `MovinMoCapSourceConfig` shall 次の public フィールド (またはプロパティ) を保持する: `port` (int, 既定 11235), `bindAddress` (string, 既定空), `rootBoneName` (string), `boneClass` (string)。
3. The `MovinMoCapSourceConfig` shall `[CreateAssetMenu]` 属性を付与し、Project ビューから `.asset` として作成可能にする。
4. The `MovinMoCapSourceConfig` shall `ScriptableObject.CreateInstance<MovinMoCapSourceConfig>()` によるランタイム動的生成を許容し、エディタアセット編集 (シナリオ X) と動的生成 (シナリオ Y) の両方をサポートする。
5. The `MovinMoCapSourceConfig` shall `port` の有効範囲として 1〜65535 を維持し、範囲外の値が設定された場合は `MovinMoCapSource.Initialize()` で例外をスローする (具体的な例外型は design フェーズで確定する)。
6. The `MovinMoCapSourceConfig` shall 将来の拡張プロパティ (例: 受信ログ詳細度、フィルタ条件追加等) を追加できる構造を持つ (現時点で実装する必須項目は本要件の 2 のみ)。

---

### Requirement 5: Factory (`MovinMoCapSourceFactory`) と typeId="MOVIN"

**目的:** As a Slot ランタイム統合者, I want `IMoCapSourceFactory` を実装した MOVIN Factory が typeId="MOVIN" で提供されること, so that `MoCapSourceDescriptor.SourceTypeId="MOVIN"` を持つ Slot から `MovinMoCapSource` を一意に解決できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall `MovinMoCapSourceFactory : IMoCapSourceFactory` を 1 クラス定義する。
2. The `MovinMoCapSourceFactory` shall 定数 `MovinSourceTypeId = "MOVIN"` を public または internal の定数として保持する。
3. When `Create(MoCapSourceConfigBase config)` が呼ばれたとき, the `MovinMoCapSourceFactory` shall `config` を `MovinMoCapSourceConfig` にキャストし、キャスト失敗時は受け取った型名を含む `ArgumentException` をスローする。
4. When `Create()` が成功したとき, the `MovinMoCapSourceFactory` shall `MovinMoCapSource` の新規インスタンスを生成して返す (生成時の `slotId` / `errorChannel` 引数の取り回しは design フェーズで確定する)。
5. The `MovinMoCapSourceFactory` shall 同一 `MovinMoCapSourceConfig` インスタンスに対する `Resolve()` 呼び出しに対して、`MoCapSourceRegistry` の参照共有メカニズム (contracts.md §1.4) を妨げない実装とする (Factory 自身では参照キャッシュを行わない)。

---

### Requirement 6: 属性ベース自己登録 (Runtime / Editor)

**目的:** As a 利用者, I want 本パッケージを `manifest.json` に追加するだけで `MovinMoCapSourceFactory` が `RegistryLocator.MoCapSourceRegistry` に typeId="MOVIN" で自動登録されること, so that シーン側で追加コードを書かずに Slot から MOVIN を解決できる。

#### Acceptance Criteria

1. The `MovinMoCapSourceFactory` shall `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 属性を持つ静的メソッドから `RegistryLocator.MoCapSourceRegistry.Register("MOVIN", new MovinMoCapSourceFactory())` を呼び、Player ビルドおよびランタイム起動時に自己登録する。
2. The `mocap-movin` package shall Editor 専用 asmdef (例: `RealtimeAvatarController.MoCap.Movin.Editor`) 内に `[UnityEditor.InitializeOnLoadMethod]` 属性付きの登録エントリを配置し、Editor 起動時 (Inspector UI 候補列挙向け) にも同一の登録処理を実行する。
3. The Editor 自己登録エントリ shall `Editor/MoCap/Movin/MovinMoCapSourceFactoryEditorRegistrar.cs` または同等パスに分離配置し、Runtime 経路と物理的に別ファイルで管理する (本体側 VMC 実装の構成に倣う)。
4. If 同一 typeId="MOVIN" が既に登録されている状態で `Register()` が呼ばれて `RegistryConflictException` が発生したとき, then the 自己登録メソッド shall 例外を握り潰さずに `RegistryLocator.ErrorChannel.Publish(new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を呼ぶ。
5. Where Unity の Domain Reload が無効化されている (Enter Play Mode 最適化) 環境, the `RegistryLocator.ResetForTest()` shall `SubsystemRegistration` タイミングで先行実行されるため、本パッケージ側で追加の二重登録回避処理を要しない。
6. When 自己登録が成功したとき, the `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` shall 文字列 `"MOVIN"` を含むリストを返す。

---

### Requirement 7: MOVIN MotionFrame 型 (Generic Transform ベース)

**目的:** As a motion-pipeline 統合者, I want MOVIN 受信結果を保持する専用 MotionFrame 型が提供されること, so that Humanoid に依存しない Generic リグの Transform 直接適用経路を上流から下流まで一貫して扱える。

#### Acceptance Criteria

1. The `MovinMotionFrame` shall `RealtimeAvatarController.Core.MotionFrame` を継承するイミュータブル型として定義する。
2. The `MovinMotionFrame` shall ボーン名 (string) をキーとして `localPosition` (Vector3) / `localRotation` (Quaternion) / `localScale` (Vector3?) を保持できるコレクションを持つ (具体的な内部表現 — Dictionary / 並列配列等 — は design フェーズで確定する)。
3. The `MovinMotionFrame` shall ルートボーン用に `/VMC/Ext/Root/Pos` 由来の localScale / localOffset を任意 (nullable) として保持する。
4. The `MovinMotionFrame` shall `Timestamp` を `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻し、MOVIN 送信側の `/VMC/Ext/T` を直接タイムスタンプとして採用しない (タイムスタンプは Adapter ローカルで打刻する)。
5. The `MovinMotionFrame` shall 1 フレーム発行時点での bone 集合をスナップショット化し、フレーム外部からの mutate を許容しない (発行後に内容が変わらないことを保証する)。
6. If 受信したボーンが 1 件もない (初期化直後など) 状態で Tick が呼ばれたとき, then the `MovinMoCapSource` shall `MovinMotionFrame` を発行せず、空フレームの連続発行を避ける。

---

### Requirement 8: MOVIN Applier (Generic Transform 直接書き込み)

**目的:** As a ランタイム統合者, I want MOVIN 受信フレームを Avatar の Generic Transform ツリーへ直接適用する Applier が提供されること, so that Humanoid リターゲット経路を経由せずに `prefix:boneName` 一致による直書き込みでアバターを駆動できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall `MovinMotionApplier` (具体クラス名は design フェーズで確定する) を Runtime に定義し、`MovinMotionFrame` の内容を Avatar の Transform ツリーへ書き込む責務を持つ。
2. The `MovinMotionApplier` shall アバターのルート Transform を起点に再帰探索して name 一致テーブル (`Dictionary<string, Transform>`) を構築する初期化処理を提供する。
3. The `MovinMotionApplier` shall `MovinMoCapSourceConfig.rootBoneName` が指定された場合は、その名前を持つ Transform を armature ルートとして優先採用する。
4. The `MovinMotionApplier` shall `MovinMoCapSourceConfig.boneClass` が指定された場合は、`{boneClass}:` で始まる名前の Transform のみを適用対象とする (空文字または null の場合は全 Transform を対象にする)。
5. When 受信フレームが届いたとき, the `MovinMotionApplier` shall フレームに含まれる各ボーン名についてテーブル lookup を行い、一致した Transform に対して `SetLocalPositionAndRotation(localPos, localRotation)` を呼び、`localScale` が含まれる場合は `localScale` も書き込む。
6. If 受信したボーン名が name 一致テーブルに存在しないとき, then the `MovinMotionApplier` shall そのボーンの書き込みをスキップし、例外をスローしない (ログ出力は警告レベルで 1 ボーン 1 回まで、または design フェーズで確定する抑制ロジックに従う)。
7. The `MovinMotionApplier` shall `MovinMotionFrame.RootLocalScale` (`/VMC/Ext/Root/Pos` v2.1 拡張) を root Transform の `localScale` に書き込む。
8. The `MovinMotionApplier` shall `IDisposable` または同等の解放 API を提供し、購読解除と name テーブル参照解放を行う。
9. While Avatar が破棄された (Transform が null になった) 状態, the `MovinMotionApplier` shall 該当ボーンへの書き込みをスキップし、フレーム適用を継続する。

---

### Requirement 9: Slot との結線とライフサイクル

**目的:** As a ランタイム統合者, I want Slot が MoCapSourceDescriptor.SourceTypeId="MOVIN" を持つ場合に、MOVIN Source と MOVIN Applier が正しく結線・駆動されること, so that エンドユーザーが Slot 設定だけで MOVIN を動かせる。

#### Acceptance Criteria

1. When `SlotManager` (本体側) が `MoCapSourceDescriptor { SourceTypeId="MOVIN", Config=MovinMoCapSourceConfig }` を `IMoCapSourceRegistry.Resolve(descriptor)` に渡したとき, the Registry shall `MovinMoCapSourceFactory` 経由で `MovinMoCapSource` インスタンスを返す。
2. The `mocap-movin` package shall Sample または Runtime コンポーネント (例: `MovinSlotBridge` / 駆動 MonoBehaviour) を提供し、`SlotManager.TryGetSlotResources` 等の本体提供 API 経由で取得した `IMoCapSource` と `Avatar` を結線して `MovinMotionApplier` を駆動する (具体的な結線クラスの配置・名称は design フェーズで確定する)。
3. The 駆動コンポーネント shall `MovinMoCapSource.MotionStream` を `.ObserveOnMainThread()` 等で Unity メインスレッドへ同期したうえで `MovinMotionApplier.Apply()` を呼ぶ。
4. When Slot が `SlotRegistry.RemoveSlot()` 経由で解放されたとき, the 駆動コンポーネント shall 購読を解除し、`MovinMotionApplier` を Dispose する。`IMoCapSource` 自身の解放は本体側 `MoCapSourceRegistry.Release()` に委ね、駆動コンポーネントが直接 `Dispose()` / `Shutdown()` を呼ばない。
5. When 同一 `MovinMoCapSourceConfig` インスタンスを参照する複数 Slot が存在するとき, the `MoCapSourceRegistry` shall 1 つの `MovinMoCapSource` を参照共有させ、結果的に 1 つの UDP バインドで複数 Slot へフレームを配信する状態を維持する (本体側 Registry の参照共有契約に準拠)。
6. While 既存 typeId="VMC" Slot と typeId="MOVIN" Slot が同一シーンに共存しているとき, the system shall 互いに独立してフレームを配信し、port が異なる限り通信競合を発生させない。
7. If 駆動コンポーネントが初期化に失敗したとき (Avatar が見つからない・name テーブル構築失敗等), then the system shall `RegistryLocator.ErrorChannel` 経由で `SlotErrorCategory` (具体カテゴリは design フェーズで確定) として通知し、シーン全体の停止に至らせない。

---

### Requirement 10: スレッドモデルとエラー処理

**目的:** As a 開発者, I want 受信処理と Frame 発行処理のスレッドモデルが明確で、異常状態が `ISlotErrorChannel` 経由で通知されること, so that フレームレートに同期した安定動作と、購読側にエラー回復ロジックを持たせない運用監視を両立できる。

#### Acceptance Criteria

1. The `MovinMoCapSource` shall uOSC の `onDataReceived` が Unity メインスレッドで発火することを前提とし、受信スレッドから直接 `Subject.OnNext()` を呼ばずに内部キャッシュへ書き込み、Tick 駆動で snapshot 発行する設計を採用する (具体的な Tick 駆動方法は design フェーズで確定する)。
2. The `MovinMoCapSource` shall `Subject.OnNext()` 呼び出しを Unity メインスレッド上で行うことを保証する (`Subject.Synchronize()` 等の追加同期は任意採用可だが必須としない)。
3. The `MovinMoCapSource.MotionStream` shall `OnError()` を一切発行しない (本体 `IMoCapSource` 契約に準拠)。
4. If MOVIN 受信処理中に Adapter 側で捕捉可能な例外が発生したとき, then the `MovinMoCapSource` shall `RegistryLocator.ErrorChannel.Publish()` を呼んで通知し、`MotionStream` をエラー終端させない (`SlotErrorCategory` の具体値は design フェーズで確定する)。
5. If UDP ソケットのバインドに失敗したとき (ポート競合・権限不足等), then the `MovinMoCapSource.Initialize()` shall 例外をスローして呼び出し元に伝播する。本体側 `SlotManager` が捕捉して `ISlotErrorChannel` に通知する経路に乗せる。
6. The `MovinMoCapSource` shall `Debug.LogError` の抑制制御を自身で持たず、`DefaultSlotErrorChannel` が担う 1 フレーム抑制ロジックに委ねる。
7. The `MovinMoCapSource` shall マルチキャスト Observable として `MotionStream` を公開し、複数 Slot / 複数購読者から同一インスタンスを共有できる (`Publish().RefCount()` 等の具体実装は design フェーズで確定)。

---

### Requirement 11: アセンブリ・名前空間・依存関係

**目的:** As a パッケージ設計者, I want 本パッケージの成果物が独立した asmdef に整理され、依存方向が一方向に保たれること, so that 本体パッケージとの境界を保ちつつ uOSC への依存を本パッケージ内に閉じ込められる。

#### Acceptance Criteria

1. The Runtime コード shall `RealtimeAvatarController.MoCap.Movin` (またはこれに準ずる名前空間) に配置し、asmdef 名も同名とする。
2. The Runtime asmdef shall `RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `com.hidano.uosc` (またはそれに相当する uOSC asmdef) / `UniRx` への参照を持つ (本体パッケージ側 asmdef 参照は name 参照とする)。
3. The Editor 自己登録用 asmdef (例: `RealtimeAvatarController.MoCap.Movin.Editor`) shall `Editor` プラットフォーム限定とし、Runtime asmdef および `UnityEditor` への参照を持つ。
4. The Tests asmdef (EditMode / PlayMode) shall Runtime asmdef および本体側 Test ユーティリティ (公開されている範囲) を参照し、Runtime / Editor asmdef は Tests asmdef に逆参照しない。
5. The `mocap-movin` package shall 本体パッケージ側 asmdef (`RealtimeAvatarController.Core` 等) への破壊的変更 (シグネチャ変更 / 名前変更) を行わない。
6. While 本パッケージのコード, the package shall `RealtimeAvatarController.MoCap.VMC` (本体側 VMC 実装) への型参照を持たない (typeId 文字列の重複や登録競合のみ run-time で扱う)。

---

### Requirement 12: サンプルとドキュメント

**目的:** As a エンドユーザー, I want `Samples~/MOVIN` に動作確認可能なデモシーンが同梱されていること, so that MOVIN Studio との接続・アバター適用の流れを最小手順で再現できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall `Samples~/MOVIN` 配下にデモシーンを 1 つ以上同梱し、`NeoMOVINMan` (または同等のサンプルアバター) と `MovinMoCapSourceConfig` アセット、Slot 設定アセット、駆動 MonoBehaviour を含めて 1 シーン再生で MOVIN 受信〜適用が動作する状態にする。
2. The Sample SlotSettings アセット shall `MoCapSourceDescriptor.SourceTypeId="MOVIN"` を持ち、Sample 同梱の `MovinMoCapSourceConfig.asset` (port=11235 既定、`rootBoneName` / `boneClass` 設定済み) を参照する。
3. The `package.json` shall `samples` セクションを持ち、`Samples~/MOVIN` を Package Manager UI 上から Import 可能にする。
4. The `mocap-movin` package shall サンプル側で本体パッケージの公開 API のみを利用し、本体側 internal 型に依存しない。
5. Where ドキュメント整備が必要な場合, the `mocap-movin` package shall README.md またはサンプル付随ドキュメントに、MOVIN Studio 側の Platform=Unity / Port=11235 の設定手順、`SlotSettings` への typeId="MOVIN" 設定手順、`rootBoneName` / `boneClass` の役割を最小限記載する。

---

### Requirement 13: テスト戦略

**目的:** As a 開発者, I want 本パッケージの主要構成要素がテストでカバーされること, so that 本体パッケージ更新時や MOVIN プロトコル拡張時の回帰を早期検知できる。

#### Acceptance Criteria

1. The `mocap-movin` testing shall EditMode / PlayMode の 2 系統の asmdef を保持する (`RealtimeAvatarController.MoCap.Movin.Tests.EditMode` / `RealtimeAvatarController.MoCap.Movin.Tests.PlayMode`)。
2. The EditMode テスト shall 次をカバーする:
   - `MovinMoCapSourceConfig` の `MoCapSourceConfigBase` からのキャスト成功 / 型不一致時の `ArgumentException` スロー
   - `MovinMoCapSourceFactory.Create()` が `MovinMoCapSourceConfig` を受け取って `MovinMoCapSource` を返すこと
   - `MovinMoCapSourceFactory.Create()` が誤った Config 型に対して `ArgumentException` をスローすること
   - 属性ベース自己登録によって `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に `"MOVIN"` が含まれること
   - 同一 typeId="MOVIN" の二重登録時に `RegistryConflictException` が発生し `ErrorChannel` へ通知されること
3. The PlayMode テスト shall 次をカバーする:
   - `MovinMotionApplier` がテスト用 Avatar Transform ツリーに対して name 一致テーブルを構築できること
   - `MovinMotionFrame` を直接注入したとき、対象 Transform の `localPosition` / `localRotation` / `localScale` が想定値に変わること
   - `boneClass` フィルタが機能し、対象外の Transform が書き換わらないこと
   - 受信ボーン名がテーブルに存在しないとき例外をスローしないこと
4. The PlayMode テスト shall `MovinMoCapSource` の Observable 発行をテストハーネスから検証できる構造を持つ (実 OSC over UDP を用いた統合テストは任意とし、必須ではない)。
5. While PlayMode テスト実行中, the test shall `RegistryLocator.ResetForTest()` を `[SetUp]` / `[TearDown]` で呼び出し、テスト間の Registry 状態を独立させる。
6. The `mocap-movin` testing shall コードカバレッジの数値目標を初期版では設定しない (将来のリリースサイクルで必要に応じて追加する)。

---

### Requirement 14: 互換性と並行稼動

**目的:** As a エンドユーザー, I want 既存の typeId="VMC" Slot と typeId="MOVIN" Slot を同一プロジェクト・同一シーンで共存させられること, so that VMC 標準系 (VSeeFace 等) と MOVIN Studio を併用する配信構成を構築できる。

#### Acceptance Criteria

1. The `mocap-movin` package shall typeId="VMC" を一切上書き・干渉せず、`MoCapSourceRegistry` における VMC エントリと並行存在する。
2. While typeId="VMC" の Slot と typeId="MOVIN" の Slot が同一シーンに存在するとき, the system shall それぞれ独立した `IMoCapSource` インスタンスを保持し、フレーム発行・適用が互いに干渉しない。
3. If 両 Slot が同一 UDP ポートを指定したとき, then the system shall `MovinMoCapSource.Initialize()` 段階でポートバインド失敗例外をスローし、`SlotManager` が `ISlotErrorChannel` 経由で通知する (port が一致しない限り正常稼動する)。
4. The `mocap-movin` package shall 本体側 `mocap-vmc` 実装 (EVMC4U ベース) のソースを変更せずに、同 prefab / 同シーン内で並行稼動できる構成を実現する。
5. Where MOVIN Studio が VMC v2.0 / v2.1 / v2.5 / v2.7 のいずれを送出する場合でも, the `MovinMoCapSource` shall `/VMC/Ext/Root/Pos` の追加引数 (localScale / localOffset) の有無を許容し、欠損時は localScale / localOffset を null として扱う。
