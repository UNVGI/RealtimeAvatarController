# Research & Design Decisions — mocap-vmc-package-split

## Summary

- **Feature**: `mocap-vmc-package-split`
- **Discovery Scope**: Extension (Packaging Refactor) — Light Discovery
- **Key Findings**:
  - VMC 関連コードのコア外部参照は Samples~/UI/Runtime/RealtimeAvatarController.Samples.UI.asmdef の `references` 1 か所のみ。Sample C# コードは `RealtimeAvatarController.MoCap.VMC` 名前空間の型を一切 `using` していない (Grep 確認)。
  - コア側 Runtime/Editor の他 asmdef (`Core` / `Motion` / `Avatar.Builtin` / `Core.Editor` / 既存 Tests) は VMC 名前参照ゼロ。Reflection 化を待たずに asmdef ファイル移動のみで物理切り離しが成立する。
  - `Samples~/UI/Data/SlotSettings_Shared_Slot1/2.asset` の `moCapSourceDescriptor.Config` は `VMCMoCapSourceConfig_Shared.asset` (guid `5c4569b4a17944fba4667acebe26c25f`) を参照しているため、UI サンプルから VMC 依存を抜くにはこの参照を Stub に差し替える必要がある。
  - `MoCapSourceRegistryDebugLabel` は provider 非依存に書かれており typeId 依存もない。Stub Source 経由でも動作可能。
  - `.kiro/steering/` ディレクトリは現時点で空。Req 9.5 を満たすには `structure.md` を新規作成する必要がある。

## Research Log

### Topic: VMC コードからコア側 asmdef への逆参照有無

- **Context**: コア独立を成立させるには、コア側 asmdef の `references` から VMC を完全除去できることを確認する必要がある。
- **Sources Consulted**:
  - `Packages/com.hidano.realtimeavatarcontroller/**/*.asmdef` (Glob)
  - `Samples~/UI/Runtime/RealtimeAvatarController.Samples.UI.asmdef`
  - `Samples~` 配下 `.cs` 全件 (Grep `RealtimeAvatarController.MoCap.VMC|EVMC4U|VMCMoCapSourceConfig`)
  - コア Runtime / Editor 配下 `.cs` 全件 (Grep 同上)
- **Findings**:
  - コア Runtime/Editor の asmdef は VMC 名前参照を含まず、C# コードからも `RealtimeAvatarController.MoCap.VMC` の型を `using` していない (`HumanoidMotionApplier.cs` のヒットはコメント文中の "EVMC4U" 文字列のみで実コード依存なし)。
  - Samples~/UI 側で唯一 `RealtimeAvatarController.MoCap.VMC` を asmdef references に持つが、Runtime コードでは型を直接参照していない。Inspector 側からの SO 参照経由でのみ VMC Config を扱っている。
- **Implications**:
  - asmdef references から `"RealtimeAvatarController.MoCap.VMC"` を 1 行削除し、VMCMoCapSourceConfig の SO 参照を Stub Config に差し替えるだけで UI Sample のコンパイル独立が成立する。
  - Editor/MoCap/VMC の `VmcMoCapSourceFactoryEditorRegistrar.cs` は完全に VMC namespace 配下のため、ファイル移動以外の追加処置不要。

### Topic: 既存 SO 参照 GUID 保全戦略

- **Context**: SlotSettings_Shared_Slot1/2.asset の `Config` フィールドが指す GUID と、`VMCMoCapSourceConfig_Shared.asset` の GUID を破壊しないこと。
- **Sources Consulted**:
  - `Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset.meta` (guid `5c4569b4a17944fba4667acebe26c25f`)
  - `Samples~/UI/Data/SlotSettings_Shared_Slot1.asset` / `SlotSettings_Shared_Slot2.asset` (Config 参照 guid `5c4569b4a17944fba4667acebe26c25f`)
  - CLAUDE.md グローバル規則 (新規 GUID は `[guid]::NewGuid().ToString('N')` でランダム生成、シーケンス・1 文字シフト禁止)
- **Findings**:
  - `VMCMoCapSourceConfig_Shared.asset` は新パッケージ Samples~/VMC/Data へ GUID 据置で移動する (Hybrid Option C)。
  - SlotSettings_Shared_Slot1/2 の Config 参照は新規 Stub Config の新規 GUID に in-place で書き換える。
  - Stub Config の新規 GUID は PowerShell `[guid]::NewGuid().ToString('N')` で生成し、既存 21 GUID とも、相互間でも、シフトパターンを取らないようにする。
- **Implications**:
  - 利用者がインポート済みの古い Sample アセット (UI Sample) でも、`VMCMoCapSourceConfig_Shared.asset` を参照していたシーンは新パッケージ Samples~/VMC/Data 側の同 GUID asset にマッピングされる (Samples~/ 内のため利用者プロジェクトには Assets/ に展開された後の改変責任は残るが、本 spec のスコープ内では参照保全要件は満たされる)。

### Topic: Stub MoCap Source 設計の最小要件

- **Context**: UI Sample の動作検証 (Slot 追加・Fallback 切替・エラー表示・参照共有可視化) を VMC 不要で再現するため、Stub Source の動作仕様を確定する必要がある。
- **Sources Consulted**:
  - `Runtime/Core/Interfaces/IMoCapSource.cs` (`SourceType` / `Initialize` / `MotionStream` / `Shutdown`)
  - `Runtime/Core/Factory/IMoCapSourceFactory.cs` (`Create(MoCapSourceConfigBase)`)
  - `Samples~/UI/Runtime/SlotManagerBehaviour.cs` (購読側パイプライン)
  - `Runtime/Motion/Cache/MotionCache.cs` (`SetSource` で MotionStream を保持)
  - 既存 `EVMC4UMoCapSource` の構造 (`Subject<MotionFrame>` + `Synchronize().Publish().RefCount()`)
- **Findings**:
  - Slot UI 検証 (追加・削除・Fallback 切替・エラー表示) は MotionFrame の中身に依存しない。空ストリーム (一切 OnNext しない) でも UI 状態遷移は機能する。
  - `MoCapSourceRegistryDebugLabel` は Registry 内 RefCount を表示するだけで、Stream 内容に依存しない。
  - `HumanoidMotionApplier.Apply` は `frame == null` または bone Dictionary が空の場合、無処理で return する設計のため、Stub が emit しない設計でも例外は発生しない。
- **Implications**:
  - Stub Source は **空ストリームを返す最小実装** で UI 検証要件を完全に満たす。固定ポーズ送信やループ送信は YAGNI。
  - 将来動作確認のためにポーズを emit したい需要が出た場合は別 spec で `StubFixedPoseConfig` を派生実装する余地を残す。
  - typeId は `"Stub"` を採用 (`"NoOp"` は意味的に "何もしない" を強調しすぎ、UI 検証用ダミーという用途に対し情報量が低いため不採用)。

### Topic: 新パッケージ初期バージョン

- **Context**: コア 0.1.0 と整合する初期バージョンを決定する。
- **Sources Consulted**:
  - コア側 `package.json` (`version: "0.1.0"`)
  - コア側 `CHANGELOG.md` (0.1.0 — 2026-05-08 初回リリース)
- **Findings**:
  - 新パッケージは初期 `0.1.0` を採用し、コア側のバージョンに同期する。
  - コア側 `0.1.0` 初回リリースに本 spec の VMC 分離を含めるか、コア `0.2.0` でリリースするかは tasks フェーズの決定事項。requirements 6 項では「コア側のバージョンと整合する初期バージョン」と記載があり、設計上はバンドルアップグレード方針 (コア側もバージョンを上げる) を採用する余地を残す。
- **Implications**:
  - 新パッケージ `package.json.dependencies` の `com.hidano.realtimeavatarcontroller` は固定バージョン `"0.1.0"` (またはアップグレード後のバージョン) を記述する。
  - 利用者は両パッケージのバージョンが一致していることを README 表で確認できる状態を保つ。

### Topic: VMC サンプルのシーン構成

- **Context**: VMC サンプルが独立シーンを持つか、Data + README のみで構成するかを判断する。
- **Sources Consulted**:
  - 既存 `SlotManagementDemo.unity` の構成 (UI Sample に同梱、VMC 専用シーンは存在しない)
  - 旧 UI サンプルが「VMC 受信デモ」として機能していた経緯 (`SlotSettings_Shared_Slot1/2` が VMCMoCapSourceConfig_Shared を参照)
  - 受け入れ条件 4: 「VMC サンプルが新パッケージ側で動作し、旧 UI サンプル相当の VMC 受信デモが再現できる」
- **Findings**:
  - 旧 UI サンプルは「UI 検証」と「VMC 受信デモ」を 1 シーンに同居させていた。本 spec で UI Sample を Stub 化すると、VMC 受信デモは別途用意が必要。
  - 新規 Scene を VMC サンプル側に持つ場合、利用者は VMC サンプルだけインポートして動作確認できる。一方、UI Sample と VMC サンプル両方をインポートしたとき、UI Sample 側の SlotSettings_Shared_Slot1/2 は Stub 参照になっているため、VMC 受信デモ用には別途 SlotSettings (VMC Config 参照) を VMC サンプル内に持つ必要がある。
- **Implications**:
  - VMC サンプル側に **`Data/` (VMCMoCapSourceConfig_Shared.asset 移動) + 新規 SlotSettings_VMC_Slot1.asset (VMC Config 参照) + 新規 Scene (`VMCReceiveDemo.unity`)** を持たせる構成を採用する。
  - 新規 Scene の GUID は `[guid]::NewGuid().ToString('N')` でランダム生成する。
  - 新規 SlotSettings の GUID も同様にランダム生成する。

### Topic: Tests/EditMode/mocap-vmc/ 中間ディレクトリの扱い

- **Context**: コア側 `Tests/EditMode/{slot-core,motion-pipeline,avatar-provider-builtin,mocap-vmc}/` のように spec 名を中間ディレクトリにする慣行があるが、新パッケージ単一 spec 配下ではこの中間階層が不要かを判断する。
- **Sources Consulted**:
  - コア側 `Tests/EditMode/` のディレクトリ命名 (spec 名ベース)
  - 新パッケージ受け入れ条件: テスト asmdef 名は据置、テストのパスはどこでも可
- **Findings**:
  - 新パッケージは VMC 単一 spec のため、`Tests/EditMode/` 直下にテスト .cs と asmdef を平置きする方が階層が浅くなり可読性が高い。
  - asmdef GUID と asmdef name を据置するため、ディレクトリ階層変更によるテスト実行への影響はない (Unity は asmdef name で照合)。
- **Implications**:
  - 新パッケージは `Tests/EditMode/` 直下、`Tests/PlayMode/` 直下に直接テストファイルを配置する (中間 `mocap-vmc/` ディレクトリを再現しない)。

### Topic: UniRx をコア README から「必須」と表現してよいか

- **Context**: UniRx は `RealtimeAvatarController.Core.asmdef` で `references` に入っているため、VMC 分離後もコア依存は維持される。README の記述変更は不要かを確認する。
- **Sources Consulted**:
  - `Runtime/Core/RealtimeAvatarController.Core.asmdef` (`references: ["UniRx", "UniTask"]`)
  - 既存コア README (UniRx 必須の記述あり)
- **Findings**:
  - UniRx はコア (`Subject` ベースの ErrorChannel・SlotStateChanged) で利用されており、新 spec のスコープでもコアからの UniRx 依存は外せない。
  - README の「UniRx 必須」記述は据置で正確。
- **Implications**:
  - コア README の UniRx 関連記述変更は本 spec のスコープ外。VMC 分離の記述追加 (manifest.json 例から VMC 関連は Optional であるという注釈追加) のみで対応する。

### Topic: 検証シナリオ A の合否基準

- **Context**: 受け入れ条件 1 (「コアのみでコンパイルが通る」) と検証シナリオ A (「重要警告無く SlotManagementDemo.unity が開ける」) の合否基準を明文化する。
- **Sources Consulted**:
  - 要件 7.3: `MissingReferenceException` / `Could not load type` 等のエラー警告を発生させない
  - 要件 10.1: 「コンパイルエラー / 重要警告無く SlotManagementDemo.unity が開ける」
- **Findings**:
  - Unity Editor のシーンロード時に発生する警告は: (a) Missing Script (m_Script GUID 不一致 → `MissingReferenceException`) / (b) `Could not load type 'X' from assembly 'Y'` (asmdef 不整合) / (c) Asset 内 fileID 解決失敗。
  - これらは **Error Console** に出力されるため、Console をクリアした状態で `SlotManagementDemo.unity` を開き、Errors のみゼロ件であることを合否基準にする (Warnings はパッケージ周辺ライブラリの古い API 使用警告等、本 spec 起因でないものが発生する可能性があるため対象外)。
- **Implications**:
  - 検証シナリオ A の合否基準: `Console -> Clear -> Open SlotManagementDemo.unity -> Errors == 0`。
  - 上記に加え、Compile Errors ゼロ件 (`UnityEditor.Compilation.CompilationPipeline` 経由) も合否基準に含める。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| A. Pure Move (asmdef + GUID 据置) | 全ファイルを物理的に移動し、GUID と asmdef name を据置。コア側に空 dir / asmdef を残さない。 | 既存テスト・既存 SO 参照を完全保全。最小変更で受け入れ条件達成。 | 利用者プロジェクトが旧パスを直接参照していた場合は壊れる (本 spec では未公開のため不問)。 | **採用**。要件 2/3/4 が GUID・asmdef 据置を明示。 |
| B. Pure Copy + Deprecate | 旧パッケージにファイルを残し、新パッケージにコピー、旧 asmdef を `[Obsolete]` 化。 | 段階移行ができる。 | 二重実装による登録衝突 (`RegistryConflictException`)、ビルド肥大、未公開状態では過剰。 | 不採用。 |
| C. Hybrid (Move VMC + 新設 Stub + In-place 編集 SlotSettings) | VMC ファイルは GUID 据置で MOVE、Stub は新設、SlotSettings の Config 参照は in-place で書き換え。 | UI Sample の VMC 非依存化と、VMC サンプル独立化を 1 spec 内で達成。 | SlotSettings_Shared_Slot1/2.asset の YAML 編集が必要 (機械的に置換可能)。 | **採用** (gap-analysis 既決定)。 |
| D. Reflection-first | Reflection で EVMC4U 名前参照を消してから移動 (option ⑤ 先行)。 | 利用者側 EVMC4U asmdef 不要にできる。 | Reflection 化は別 spec とすることが要件で明示。本 spec のスコープ外。 | 不採用 (将来 spec)。 |

## Design Decisions

### Decision: Hybrid 移行戦略 (Option C) 採用

- **Context**: VMC 関連を新パッケージへ分離しつつ、UI Sample を VMC 非依存化、VMC サンプルを新パッケージで再現する必要がある。
- **Alternatives Considered**:
  1. A — Pure Move (UI Sample 内 VMC 参照を解決する追加策が必要、結果的に Hybrid 相当になる)
  2. B — Copy + Deprecate (未公開状態で過剰)
  3. C — Hybrid (Move VMC + 新設 Stub + In-place 編集)
  4. D — Reflection 化先行 (本 spec スコープ外)
- **Selected Approach**: Option C。VMC ランタイム/Editor/Tests/Samples Data は GUID 据置で MOVE、Stub MoCap Source / StubMoCapSourceConfig は Samples~/UI 内に新設、SlotSettings_Shared_Slot1/2.asset の `moCapSourceDescriptor.Config` は in-place で Stub Config に書き換える。
- **Rationale**:
  - 既存 SO 参照 (`VMCMoCapSourceConfig_Shared.asset` の GUID `5c4569b4a17944fba4667acebe26c25f`) を据置で保全することで、新パッケージ側 VMC サンプルの SlotSettings 新規作成時に過去資産を再利用できる。
  - UI Sample 側の Stub 化は asmdef references 1 行削除と SlotSettings 2 件の Config 参照書き換えで完結し、変更点が明確。
  - 検証シナリオ A/B の独立性が高く、合否切り分けが容易。
- **Trade-offs**:
  - SlotSettings_Shared_Slot1/2.asset の YAML を編集する必要がある (機械的に置換可能、リスク低)。
  - Stub の C# クラス 2 個 (Source + Config) を新設する追加コストがある (各 50 行未満)。
- **Follow-up**:
  - Stub Source の typeId を `"Stub"` で確定 (本 design で確定済み)。
  - Stub Source emit 仕様を「空ストリーム」で確定 (本 design で確定済み)。

### Decision: Stub MoCap Source の typeId を `"Stub"` とする

- **Context**: Stub Source が `MoCapSourceRegistry` に自己登録する際の typeId 文字列を確定する必要がある。
- **Alternatives Considered**:
  1. `"Stub"` — 用途を端的に表現 (UI 検証用ダミー)
  2. `"NoOp"` — 「何もしない」を強調 (空ストリームの仕様と整合)
  3. `"Mock"` — テストモック寄り
- **Selected Approach**: `"Stub"`。
- **Rationale**:
  - "Stub" は UI/E2E テストのダミー実装として一般的な命名で、用途 (本物の代わり) を表現する。
  - "NoOp" は emit 仕様にバイアスがかかり、将来 emit ありの Stub に拡張する場合に名称が齟齬を起こす。
  - "Mock" は Test Double の文脈が強く、Inspector ドロップダウンに並ぶ typeId としては UI サンプル目的を伝えにくい。
- **Trade-offs**: なし。
- **Follow-up**: なし。

### Decision: Stub MoCap Source の emit 動作を「空ストリーム」とする

- **Context**: Stub Source の `MotionStream` がどの程度のデータを emit すべきか確定する必要がある。
- **Alternatives Considered**:
  1. 空ストリーム (一切 OnNext しない、Subject だけ用意して購読は許容)
  2. 固定ポーズ (T-Pose) を 1 回 emit
  3. ループ (固定ポーズを 30 fps で繰り返し emit)
- **Selected Approach**: 空ストリーム。`Subject<MotionFrame>` を内部に持つが OnNext は呼ばず、Shutdown 時に `OnCompleted()`。
- **Rationale**:
  - UI Sample の検証要件 (Slot 追加・削除・Fallback 切替・エラー表示・RefCount 表示) は MotionFrame の到達に依存しない。
  - 固定ポーズ・ループは Avatar の見た目に影響を与えるが、UI Sample の Avatar 表示は UI 検証のスコープ外 (BuiltinAvatarProvider の動作確認は別 spec)。
  - YAGNI 原則。将来 emit ありの Stub が必要になったら別 typeId (`"StubFixedPose"` 等) で派生実装する。
- **Trade-offs**:
  - VMC 不在状態で Avatar が動かないため、利用者は「動いていない」のか「Stub だから動かない」のか混乱する可能性。README に明記して回避する。
- **Follow-up**:
  - Stub Source README ノート: 「UI 検証用のダミー実装で、Avatar Pose は変化しません。VMC 受信デモは新パッケージ `Samples~/VMC/` を参照してください。」

### Decision: 新パッケージ初期バージョン `0.1.0`

- **Context**: 新パッケージの初期バージョン番号と、コア側との整合性を確定する。
- **Alternatives Considered**:
  1. `0.1.0` — コア初期バージョンと一致
  2. `0.0.1` — Pre-release 表現
  3. `0.1.0-pre.1` — 実験リリース表現
- **Selected Approach**: `0.1.0`。コア側 `package.json.version` と同期する。
- **Rationale**:
  - 両パッケージの version 同期方針 (要件 1.6) を採用すると、利用者は両者のバージョンを揃えることで動作確認バージョンの整合性を担保できる。
  - 本 spec 完了時点でコア側もマイナーアップ (0.2.0) する選択肢は tasks フェーズで検討する余地を残す。
- **Trade-offs**:
  - コア側に既に 0.1.0 がリリースされているため、この時点で 0.1.0 を「両者対応バージョン」として再定義することになる。CHANGELOG にこの整合性ポリシーを明記して回避する。
- **Follow-up**:
  - tasks フェーズ: 「コア側を 0.2.0 にバンプして VMC 分離を反映するか、0.1.0 のまま VMC 分離を後発反映するか」を最終決定。

### Decision: VMC サンプルに独立シーン (`VMCReceiveDemo.unity`) を新設する

- **Context**: VMC サンプルが Data のみ (旧 UI サンプル併用前提) か、独立シーンを持つかを確定する。
- **Alternatives Considered**:
  1. Data + README のみ (UI Sample 併用が前提)
  2. 独立シーン `VMCReceiveDemo.unity` 新設
- **Selected Approach**: 独立シーン `VMCReceiveDemo.unity` を新設し、Data (`VMCMoCapSourceConfig_Shared.asset` 移動 + 新規 `SlotSettings_VMC_Slot1.asset`) + Scene の構成。
- **Rationale**:
  - 受け入れ条件 4「VMC サンプルが新パッケージ側で動作し、旧 UI サンプル相当の VMC 受信デモが再現できる」は、VMC サンプル単独で完結することを要請している。
  - UI Sample 併用前提だと、利用者は両サンプルをインポートし SlotSettings の Config 参照を手動で書き換える必要があり、手順が煩雑。
  - 独立シーンを持てば「VMC サンプルだけインポート」で動作確認できる。
- **Trade-offs**:
  - 新規 Scene 作成コスト (UI Sample SlotManagementDemo.unity の VMC 関連部分を抜粋・整理した最小構成)。
  - シーンに含める UI Prefab は UI Sample 側に依存しないよう、最小の Slot 表示 / Pose 表示 UI を新設する余地があるが、本 spec のスコープでは「最小構成 = SlotManagerBehaviour + Camera + Avatar Prefab Placeholder」で十分。
- **Follow-up**:
  - 新規 Scene の GUID は `[guid]::NewGuid().ToString('N')` でランダム生成。
  - tasks フェーズ: シーンに含める GameObject 構成 (Camera / Light / SlotManagerBehaviour / 必要最小 UI) を確定。

### Decision: `.kiro/steering/structure.md` を新規作成 (最小スコープ)

- **Context**: `.kiro/steering/` ディレクトリは現時点で空。要件 9.5 は `structure.md` 更新を要請しているが、既存ファイルが無いため新規作成となる。
- **Alternatives Considered**:
  1. 全 spec 横断の包括的 structure.md
  2. 本 spec で必要な「パッケージ依存マップ」のみの最小 structure.md
- **Selected Approach**: 最小 structure.md。`Packages/` 配下のパッケージ一覧、各パッケージの責務、依存方向 (新パッケージ → コア、一方向)、Samples~/ の構成を簡潔に記述。
- **Rationale**:
  - スコープ外の他 spec (slot-core, motion-pipeline 等) の構造記述は、それぞれの spec が steering を更新する責務を持つべき。
  - 本 spec は「VMC 分離後の現在のパッケージ構造」を steering に反映する責務のみを持つ。
- **Trade-offs**:
  - 将来 steering を拡張する際に、本 spec 由来の記述スタイルが基準として残る。記述は意図的にシンプルにし、後続 spec が追記しやすい構造を採る。
- **Follow-up**:
  - tasks フェーズ: structure.md の具体的な見出し構成を確定。

### Decision: 検証シナリオ A の合否基準を「Errors == 0 (Compile + Console)」とする

- **Context**: 受け入れ条件 1 (コンパイル通過) と要件 10.1 (重要警告なし) の客観的合否基準を確定する。
- **Alternatives Considered**:
  1. Errors のみゼロ件 (Warnings は不問)
  2. Errors + Warnings 両方ゼロ件
  3. 特定 Warning パターンのみ拒否 (`MissingReferenceException` / `Could not load type` 等)
- **Selected Approach**: **Compile Errors == 0 かつ Console Errors == 0** を必須条件、Warnings は不問。ただし `MissingReferenceException` / `Could not load type` が発生した場合は Errors として検出される (Unity 仕様) ため、要件 7.3 を自動的にカバー。
- **Rationale**:
  - Unity の `Could not load type 'X' from assembly 'Y'` はコンソールに **Error** として出力されるため、Errors==0 で検出される。
  - `MissingReferenceException` も同様に Error カテゴリに分類されるため、Errors==0 で検出される。
  - 全 Warnings ゼロ件は、サードパーティ asmdef (UniRx 等) や Unity 標準の Obsolete API 警告でゼロ達成が現実的でないため対象外。
- **Trade-offs**:
  - Warnings 経由でのみ表面化する遅延的な参照不整合は検出できない。これは tasks フェーズで明示的なテストケース (`VmcReferenceAbsentTests.cs` 等) を追加して補完する余地を残す。
- **Follow-up**:
  - tasks フェーズ: 検証シナリオ A の手順スクリプト化 (Unity Editor batchmode `-runTests` 経由 + Console 走査) を検討。

## Risks & Mitigations

- **Risk 1: SlotSettings_Shared_Slot1/2.asset の YAML in-place 編集ミス** — 機械的置換 (sed / PowerShell `Set-Content`) ではなく、Unity Editor で開いて Inspector 経由でドラッグ&ドロップ差し替えを行う方が GUID 不整合リスクが低い。tasks フェーズで Editor 経由の手順を採用する。
- **Risk 2: コア側 0.1.0 が既にリリース済みであり、本 spec で破壊的変更が入る場合のバージョンポリシー** — コア側 README の記述では 0.1.0 が「初回 UPM パッケージリリース」となっているため、利用者がこの 0.1.0 を取得済みの可能性は低い (CHANGELOG 記載日 2026-05-08, 本 spec 開始 2026-05-09)。tasks フェーズでコア側を 0.2.0 にバンプし、CHANGELOG に「VMC 分離による破壊的変更」を明記する方針を推奨。
- **Risk 3: 利用者プロジェクトが既に `Assets/EVMC4U/` 用 asmdef を作成済みでない場合、新パッケージ導入時に EVMC4U 名前参照不足でコンパイルエラー** — README 利用者準備手順で明示し、Pull Request テンプレートにも明記する。これは要件 8.2 で「利用者側準備不足を示すエラー」と明記されており、本 spec のスコープ外。
- **Risk 4: 既存 21 GUID のローテーション/シフトパターンとの衝突** — 新規 GUID (Stub Config / 新規 SlotSettings_VMC_Slot1 / 新規 VMCReceiveDemo.unity / Stub Source `.cs` / Stub Config `.cs`) は `[guid]::NewGuid().ToString('N')` で個別に生成し、生成後に既存 GUID と一致しないことを Grep で検証する手順を tasks フェーズに含める。
- **Risk 5: Subagent からの `git rm` / `rm` が許可されないため、コア側 `Runtime/MoCap/VMC/` 等の削除が implementation 中に親セッションへエスカレーションされる** — tasks フェーズで「親セッション必須」マーカーを該当タスクに付与する。MEMORY 制約に従う。

## References

- [`Samples~/UI/Data/SlotSettings_Shared_Slot1.asset`](D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController\Packages\com.hidano.realtimeavatarcontroller\Samples~\UI\Data\SlotSettings_Shared_Slot1.asset) — Config 参照書き換え対象。
- [`Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset.meta`](D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController\Packages\com.hidano.realtimeavatarcontroller\Samples~\UI\Data\VMCMoCapSourceConfig_Shared.asset.meta) — GUID 据置移動対象 (`5c4569b4a17944fba4667acebe26c25f`)。
- [`Runtime/Core/Interfaces/IMoCapSource.cs`](D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController\Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Interfaces\IMoCapSource.cs) — Stub Source が実装する契約。
- [`Runtime/Core/Factory/IMoCapSourceFactory.cs`](D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController\Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Factory\IMoCapSourceFactory.cs) — Stub Factory が実装する契約。
- [CLAUDE.md (project root)](D:\Personal\Repositries\RealtimeAvatarController\CLAUDE.md) — Unity GUID 生成ルール、Subagent 削除制約。
- [User global CLAUDE.md (`Unity .meta GUID` セクション)](C:\Users\Hidano\.claude\CLAUDE.md) — 新規 GUID は `[guid]::NewGuid().ToString('N')` で生成、シーケンス・1 文字シフト禁止。
