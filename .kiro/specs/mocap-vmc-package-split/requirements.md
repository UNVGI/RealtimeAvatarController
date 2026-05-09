# Requirements Document

## Project Description (Input)
EVMC4U 依存を持つ MoCap.VMC モジュールを、現行の com.hidano.realtimeavatarcontroller パッケージから新規 UPM パッケージ com.hidano.realtimeavatarcontroller.mocap-vmc に分離する。

【背景・動機】
- 本パッケージは読み取り専用 UPM として配布する方針だが、EVMC4U の本家 UnityPackage は asmdef を含まずに配布されており、現行 RealtimeAvatarController.MoCap.VMC.asmdef は名前参照 "EVMC4U" を要求するためそのままではコンパイルが通らない。
- VMC 以外の MoCap 手段（Mediapipe、Webカメラ、Sensorベース等）を使う利用者にとって、EVMC4U 関連のコードと依存解決の手間は不要なノイズである。
- 長期的には EVMC4U への参照を Reflection 経由 (Type.GetType("EVMC4U.ExternalReceiver, Assembly-CSharp")) に置き換えてコンパイル時依存を消す方針 (option ⑤) を採るが、その前段として影響範囲を独立パッケージに閉じ込めたい。

【目的】
1. VMC を使わない利用者がコアパッケージのみで完結できるようにする (EVMC4U 関連コード・依存・サンプルアセットを切り離す)。
2. VMC を使う利用者は新パッケージを追加で導入することで、現状と同等の機能を引き続き利用できる。
3. 将来の Reflection 化リファクタ (option ⑤) を新パッケージ内で完結させられる構造にする。

【スコープ】
A. 新パッケージ com.hidano.realtimeavatarcontroller.mocap-vmc の新設
   - package.json: dependencies に com.hidano.realtimeavatarcontroller を固定バージョンで記述
   - displayName, samples 定義 (VMC Sample)
B. ファイル移動 (asmdef 名・namespace・GUID は据置)
   - 旧 Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/* → 新パッケージ Runtime/
   - 旧 Editor/MoCap/VMC/* → 新パッケージ Editor/
   - 旧 Tests/EditMode/mocap-vmc/* → 新パッケージ Tests/EditMode/
   - 旧 Tests/PlayMode/mocap-vmc/* → 新パッケージ Tests/PlayMode/
C. 既存サンプル (Samples~/UI) の provider-agnostic 化
   - RealtimeAvatarController.Samples.UI.asmdef から "RealtimeAvatarController.MoCap.VMC" 参照を削除
   - Stub MoCap Source / StubMoCapSourceConfig を Samples~/UI 内に新設 (UI 動作検証用ダミー)
   - 既存 Samples~/UI/Data の VMCMoCapSourceConfig_Shared.asset を新パッケージ Samples~/VMC/Data/ へ移動
   - SlotSettings_Shared_Slot1/2.asset の Config 参照を Stub Config に差し替え (新 GUID 発行)
D. 新パッケージ Samples~/VMC/ の新設
   - VMC を使った最小構成のデモ (旧 UI サンプルから移植した VMC 関連アセット + 必要に応じて簡易シーン)
E. README / CHANGELOG 更新 (両パッケージ)
   - core 側: VMC が別パッケージに分離された旨、移行手順
   - 新パッケージ側: 導入方法、EVMC4U と uOSC の利用者側準備手順 (asmdef 作成手順含む)
F. .kiro/steering/structure.md の更新 (新パッケージの存在を反映)

【スコープ外 (将来別 spec)】
- Reflection 化による EVMC4U asmdef references 削除 (option ⑤): 本 spec 完了後の別タスク。
- 本家 EVMC4U への asmdef 追加 PR (option ④): プロジェクト外の活動。
- VMC 以外の新規 MoCap source 実装。

【非機能要件】
- 既存テストは asmdef 名 "RealtimeAvatarController.MoCap.VMC" 参照を維持するため、移動後もそのままパスする想定 (Editor/PlayMode 双方)。
- 既存利用者シーン (もしあれば) の SO 参照 GUID は変更しない。
- 互換性: 本 spec 完了時点では旧パスで使っていた利用者は存在しない (まだ未公開) ため、deprecation 配慮は不要。

【受け入れ条件】
1. com.hidano.realtimeavatarcontroller を Unity プロジェクトに導入し、mocap-vmc を導入しない状態でコンパイルが通る (EVMC4U/uOSC 不要)。
2. 上記に加えて mocap-vmc を導入し、利用者が用意した EVMC4U.asmdef + uOSC が存在する状態で、既存 VMC 関連テスト (EditMode/PlayMode) が全てパスする。
3. UI サンプルが Stub MoCap Source 経由で動作し、Slot Inspector / SlotErrorPanel 等の UI 検証が VMC 不要で行える。
4. VMC サンプルが新パッケージ側で動作し、旧 UI サンプル相当の VMC 受信デモが再現できる。

---

## はじめに

本ドキュメントは `mocap-vmc-package-split` Spec の要件を定義する。本 Spec の目的は、EVMC4U 依存を抱える VMC MoCap 実装一式を、現行コアパッケージ `com.hidano.realtimeavatarcontroller` から新規 UPM パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` に分離し、VMC を使わない利用者がコアパッケージ単体でコンパイル可能な状態を実現することである。

本 Spec は新規 MoCap 機能の追加や Adapter 内部ロジックの再設計を行わない。既存 `mocap-vmc` Spec で確定した EVMC4U ベース Adapter の動作 (`IMoCapSource` 契約・`HumanoidMotionFrame` 発行・属性ベース自己登録・参照共有モデル等) は変更せず、配置するパッケージのみを移し替える「パッケージング・リファクタリング」が主眼である。

### 採用方針

- **対象アセンブリは据置**: `RealtimeAvatarController.MoCap.VMC` / `RealtimeAvatarController.MoCap.VMC.Editor` / `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` / `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` の 4 つの asmdef は名前・GUID・参照関係を変更せずに新パッケージ配下へ移動する。
- **namespace 据置**: `RealtimeAvatarController.MoCap.VMC` 名前空間およびその配下のクラス名 (`VMCMoCapSourceConfig` / `VMCMoCapSourceFactory` / `EVMC4UMoCapSource` / `EVMC4USharedReceiver` 等) は変更しない。typeId `"VMC"` も維持する。
- **コアパッケージのコンパイル独立**: コアパッケージ (`com.hidano.realtimeavatarcontroller`) のみを導入した状態 (新パッケージ未導入) で、Unity Editor / Player Build がエラーなくコンパイルを完了することを必須要件とする。EVMC4U / uOSC / UniRx の VMC 用参照解決をコアパッケージから完全に切り離す。
- **新パッケージ依存方向**: 新パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` は `package.json` の `dependencies` でコアパッケージを固定バージョンで参照する一方向依存とする。コア側から新パッケージへの逆依存は禁止する。
- **既存 GUID の保全**: 既存 `.cs` / `.asmdef` / `.asset` (特に `VMCMoCapSourceConfig_Shared.asset`) の `.meta` GUID は変更せず、移動後もインスペクタ参照が壊れない状態を維持する。
- **UI サンプル provider-agnostic 化**: 既存 `Samples~/UI` (UI Sample) は VMC 非依存に再構成する。UI 動作検証用の Stub MoCap Source / StubMoCapSourceConfig をサンプル内に新設し、既存共有 SlotSettings の Config 参照を Stub に差し替える。差し替えに伴って新規発行する `.meta` GUID は乱数 32 桁 hex とし、既存 GUID をコピーしない。
- **VMC サンプルの新設**: 新パッケージ内に `Samples~/VMC/` を新設し、VMC を使う利用者向けの最小デモを提供する。旧 UI サンプルが保持していた VMC 関連アセット (`VMCMoCapSourceConfig_Shared.asset` 等) を新パッケージ側 Samples へ移植する。
- **Reflection 化は対象外**: `EVMC4U` への asmdef 名前参照を Reflection 化する作業 (option ⑤) は本 Spec のスコープ外とし、別 Spec で扱う。本 Spec は EVMC4U asmdef を必要とする現状の参照構造のまま新パッケージへ移動する。

## スコープ境界

- **スコープ内**:
  - 新規 UPM パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` の `package.json` 作成と配布構造の整備
  - 既存 `Runtime/MoCap/VMC/` 配下のソース・asmdef・関連 `.meta` の新パッケージへの移動 (asmdef 名・GUID 据置)
  - 既存 `Editor/MoCap/VMC/` 配下のソース・asmdef・関連 `.meta` の新パッケージへの移動
  - 既存 `Tests/EditMode/mocap-vmc/` および `Tests/PlayMode/mocap-vmc/` 配下のテストおよび asmdef の新パッケージへの移動
  - コアパッケージ側の `Samples~/UI/` を VMC 非依存に再構成 (Stub MoCap Source / StubMoCapSourceConfig 新設、`SlotSettings_Shared_Slot1/2.asset` の Config 参照差し替え、`RealtimeAvatarController.Samples.UI.asmdef` から VMC 参照を削除)
  - 新パッケージ側 `Samples~/VMC/` の新設 (旧 UI サンプルから移植した VMC 関連アセットによる最小デモ)
  - 両パッケージの `package.json` / README / CHANGELOG の更新
  - `.kiro/steering/structure.md` の更新 (新パッケージ存在の反映)

- **スコープ外**:
  - EVMC4U asmdef 名前参照を Reflection 化する変更 (option ⑤、別 Spec)
  - 本家 EVMC4U リポジトリへの asmdef 追加 PR (option ④、プロジェクト外活動)
  - VMC 以外の MoCap source 実装の追加 (Mediapipe / Webカメラ / Sensor 等)
  - 既存 `mocap-vmc` Spec で確定済みの Adapter 内部仕様 (`HumanoidMotionFrame` 構造、属性ベース自己登録、共有 `ExternalReceiver` モデル、エラー通知方針) の変更
  - `_shared/contracts.md` で定義された `IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` 等の抽象 API 変更
  - VMC Sender (送信側) 実装

- **隣接 Spec / システムとの関係**:
  - `slot-core`: コアパッケージ側に維持。`IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` / `RegistryLocator` を提供する。新パッケージはこれらをコアパッケージ依存経由で参照する。
  - `motion-pipeline`: コアパッケージ側に維持。新パッケージは `MotionFrame` / `HumanoidMotionFrame` を発行先として利用する。
  - `mocap-vmc` (既存 Spec): 本 Spec の前提。本 Spec は当該 Spec で確定した実装をパッケージとして再配置するのみで、内部仕様を変更しない。
  - `ui-sample` (既存 Spec): 既存 UI サンプルは VMC 依存を排除した形に再構成される。Stub Source 経由で UI コア機能 (Slot 操作・Fallback 設定・エラー表示) の検証ができる状態を維持する。
  - `EVMC4U` / `uOSC`: 利用者プロジェクト側で `Assets/EVMC4U/` へのインポートと EVMC4U 用 asmdef の作成、uOSC の導入を行う前提とする。新パッケージはこれらが存在する場合にのみコンパイルが通る。

---

## Requirements

### Requirement 1: 新規 UPM パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` の新設

**Objective:** As a パッケージメンテナ, I want VMC 関連コードを独立した UPM パッケージとして配布できる構造, so that VMC を使わない利用者にコア機能のみを提供しつつ、VMC を使う利用者は単一の追加導入で従来機能を享受できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall リポジトリ内 `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/` ディレクトリに新規パッケージのルートを配置する。
2. The 新パッケージ shall ルートに `package.json` を持ち、`name` を `"com.hidano.realtimeavatarcontroller.mocap-vmc"`、`displayName` を VMC 用パッケージである旨が分かる名称、`unity` / `unityRelease` をコアパッケージと同一バージョン (`6000.3` / `10f1`) とする。
3. The 新パッケージの `package.json` shall `dependencies` フィールドにコアパッケージ `com.hidano.realtimeavatarcontroller` を固定バージョンで記述し、コアパッケージのバージョン更新時に追従できる管理方針を README に明記する。
4. The 新パッケージの `package.json` shall `samples` エントリに VMC サンプル (`Samples~/VMC`) を登録し、`displayName` / `description` / `path` を含める。
5. The 新パッケージ shall `Runtime/` / `Editor/` / `Tests/EditMode/` / `Tests/PlayMode/` / `Samples~/VMC/` の各ディレクトリ構造を持ち、各ディレクトリに対応する `.meta` ファイルが存在する。
6. While 本 Spec の実装期間中, the 新パッケージのバージョン管理 shall コアパッケージ側のバージョンと整合する初期バージョン (例: `0.1.0`) を採用し、初回コミット以降は両パッケージの CHANGELOG で対応関係を追跡できる状態を維持する。
7. If 新パッケージのみがプロジェクトに導入された場合 (コアパッケージ未導入), then Unity Package Manager shall 依存解決エラーとして `com.hidano.realtimeavatarcontroller` の不足を報告する (新パッケージ単独での利用は想定しない)。

---

### Requirement 2: VMC ランタイムソースの新パッケージへの移動 (asmdef・GUID 据置)

**Objective:** As a 既存実装の保守者, I want VMC ランタイム実装一式を新パッケージへ移動しつつ asmdef 名と GUID を据置きたい, so that 既存の参照構造・テスト・既存 SO アセットの参照 GUID を破壊せず、コア側のコンパイル独立だけを実現できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/` 配下の全ソースファイル (`VMCMoCapSourceConfig.cs` / `VMCMoCapSourceFactory.cs` / `EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `AssemblyInfo.cs` 等) および対応する `.meta` を新パッケージ `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/` 配下へ移動する。
2. The 新パッケージ Runtime asmdef shall 名前を `RealtimeAvatarController.MoCap.VMC` のまま維持し、`rootNamespace` を `RealtimeAvatarController.MoCap.VMC` のまま維持する。
3. The 新パッケージ Runtime asmdef shall `references` として `RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `uOSC.Runtime` / `UniRx` / `EVMC4U` を保持し、移動前と同一の参照構成を維持する。
4. The mocap-vmc-package-split implementation shall 移動対象の `.cs` / `.asmdef` / `AssemblyInfo.cs` などすべてのファイルについて、対応する `.meta` 内の `guid` を変更せずにそのまま移動する。
5. If 移動後のソースファイルの namespace を変更する変更が含まれていた場合, then the implementation shall その変更を取り消し、`RealtimeAvatarController.MoCap.VMC` 名前空間を維持する。
6. When コアパッケージのみを導入したプロジェクトをコンパイルした場合, the Unity Editor shall `RealtimeAvatarController.MoCap.VMC` を参照する型がコア内に残存しない状態を確認し、EVMC4U / uOSC 未導入でもコンパイルエラーを発生させない。
7. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/` ディレクトリを (`.meta` を含めて) 完全に削除し、空ディレクトリや旧 asmdef が残らないようにする。

---

### Requirement 3: VMC Editor 側コードと Editor asmdef の新パッケージへの移動

**Objective:** As a エディタ拡張の保守者, I want Editor 側 (Inspector 拡張・属性ベース自己登録の Editor 補助) も同様に新パッケージへ移動したい, so that Editor 起動時の VMC Factory 自己登録経路が新パッケージに閉じ込められ、コア側の Editor アセンブリが EVMC4U に依存しなくなる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Editor/MoCap/VMC/` 配下の全ソース (`VmcMoCapSourceFactoryEditorRegistrar.cs` 等) および `.meta` を新パッケージ `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Editor/` 配下へ移動する。
2. The 新パッケージ Editor asmdef shall 名前を `RealtimeAvatarController.MoCap.VMC.Editor` のまま維持し、`includePlatforms` を `["Editor"]` で維持する。
3. The 新パッケージ Editor asmdef shall 移動前と同一の `references` (`RealtimeAvatarController.MoCap.VMC` / `RealtimeAvatarController.Core` 等、Editor 自己登録に必要なもの) を維持する。
4. When コアパッケージのみを導入した状態で Unity Editor を起動した場合, the Editor shall コア Editor アセンブリのコンパイル時に `EVMC4U` 名前参照や `RealtimeAvatarController.MoCap.VMC` を要求しない (= Editor 側もコンパイル独立を満たす)。
5. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Editor/MoCap/VMC/` ディレクトリを (`.meta` を含めて) 完全に削除する。

---

### Requirement 4: VMC テストアセンブリの新パッケージへの移動

**Objective:** As a テスト保守者, I want VMC 関連の EditMode/PlayMode テスト一式を新パッケージへ移動したい, so that テストが対象アセンブリと同一パッケージに同梱される配置となり、利用者がテストを必要としない場合に新パッケージごと無効化できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Tests/EditMode/mocap-vmc/` 配下の全テストおよび asmdef を新パッケージ `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Tests/EditMode/` 配下へ移動する。
2. The mocap-vmc-package-split implementation shall コアパッケージ側 `Packages/com.hidano.realtimeavatarcontroller/Tests/PlayMode/mocap-vmc/` 配下の全テストおよび asmdef を新パッケージ `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Tests/PlayMode/` 配下へ移動する。
3. The 新パッケージ Test asmdef shall 名前を `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` / `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` のまま維持し、参照する `RealtimeAvatarController.MoCap.VMC` / 他の test framework / EVMC4U 等の参照構成を維持する。
4. When 新パッケージとコアパッケージの両方が導入され EVMC4U / uOSC が利用者プロジェクトに準備された状態で Unity Test Runner を実行した場合, the テストランナー shall 既存 `mocap-vmc` Spec で定義された全 EditMode / PlayMode テストを成功させる (既存テストの合否方針を変更しない)。
5. The mocap-vmc-package-split implementation shall コアパッケージ側 `Tests/EditMode/mocap-vmc/` および `Tests/PlayMode/mocap-vmc/` ディレクトリを (`.meta` を含めて) 完全に削除する。
6. The mocap-vmc-package-split implementation shall 移動対象テストおよび asmdef の `.meta` 内 `guid` を据置きとし、既存テスト結果ファイルや CI 設定が GUID 参照する場合に追加の修正を不要とする。

---

### Requirement 5: 既存 UI サンプル (Samples~/UI) の VMC 非依存化

**Objective:** As a UI サンプル利用者, I want UI サンプルが VMC を導入していないプロジェクトでも動作すること, so that UI サンプル単体で Slot 操作・Fallback 設定・エラー表示等の UI 機能を VMC を意識せずに評価できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall コアパッケージ側 `Samples~/UI/Runtime/RealtimeAvatarController.Samples.UI.asmdef` の `references` から `"RealtimeAvatarController.MoCap.VMC"` を削除する。
2. The mocap-vmc-package-split implementation shall コアパッケージ側 `Samples~/UI/Runtime/` 配下から `RealtimeAvatarController.MoCap.VMC` 名前空間 (`VMCMoCapSourceConfig` 等) を直接 `using` ・参照しているコードを削除または Stub Source への参照に置き換える。
3. The mocap-vmc-package-split implementation shall `Samples~/UI/Runtime/` 配下に Stub MoCap Source 実装 (例: `StubMoCapSource`) と Stub Config (例: `StubMoCapSourceConfig : MoCapSourceConfigBase`) を新設し、`IMoCapSource` 契約を満たすダミー実装として UI 検証用に動作させる。
4. The Stub MoCap Source shall `IMoCapSource.MotionStream` として空または常に同一ポーズを発行する `IObservable<MotionFrame>` を提供し、Slot Inspector / SlotErrorPanel / Fallback 設定 UI 等の UI 検証フローを VMC 不要で進行可能にする。
5. The Stub MoCap Source shall `MoCapSourceRegistry` への自己登録を `RuntimeInitializeOnLoadMethod` / `InitializeOnLoadMethod` で行い、UI サンプル導入時に `typeId="Stub"` (具体的な typeId は design フェーズで確定) として Inspector ドロップダウンに列挙される。
6. The mocap-vmc-package-split implementation shall 既存 `Samples~/UI/Data/SlotSettings_Shared_Slot1.asset` および `SlotSettings_Shared_Slot2.asset` の `MoCapSourceDescriptor.Config` 参照を Stub Config アセットへ差し替える。
7. The mocap-vmc-package-split implementation shall Stub Config の `.asset` および対応する `.meta` を新規発行し、`.meta` の GUID は Unity の `[guid]::NewGuid().ToString('N')` 等で生成した乱数 32 桁 hex を使用する (既存 GUID のコピー・連続パターン・1 文字シフトしたローテーション系列の使用を禁止する)。
8. The mocap-vmc-package-split implementation shall 既存 `Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset` および対応する `.meta` をコアパッケージ側 UI サンプルから削除する (新パッケージの VMC サンプル側に移動)。
9. When コアパッケージと UI サンプルのみが導入された状態 (新パッケージ未導入) で Unity Editor が起動した場合, the UI サンプル shall コンパイルエラー無く読み込まれ、`SlotManagementDemo.unity` が Stub Source 経由で動作する。
10. The UI サンプル shall Stub Source 経由でも既存 UI 検証シナリオ (Slot 追加・削除・Fallback 設定切替・エラー表示) が再現できることを README またはサンプル内ドキュメントに明記する。

---

### Requirement 6: 新パッケージ Samples~/VMC/ の新設

**Objective:** As a VMC 利用者, I want 新パッケージに VMC を使った最小デモが付属していること, so that VMC 関連アセットの入手手順と動作確認シーンが新パッケージ単体で完結する。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall 新パッケージに `Samples~/VMC/` ディレクトリを新設し、新パッケージの `package.json` `samples` エントリに登録する。
2. The mocap-vmc-package-split implementation shall コアパッケージ側 `Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset` および対応する `.meta` を新パッケージ `Samples~/VMC/Data/` 配下へ移動し、`.meta` の `guid` を据置きとする (既存 SO 参照が破壊されない移行を実現)。
3. The 新パッケージ VMC サンプル shall 旧 UI サンプルの VMC 受信デモ相当機能を再現する最小構成 (簡易シーンまたは UI サンプル併用前提のアセット) を提供する。簡易シーンの新規作成有無は design フェーズで最終確定する。
4. When 利用者が新パッケージとコアパッケージの両方を導入し、EVMC4U / uOSC を `Assets/` 配下に準備し、UI サンプルと VMC サンプルの両方をインポートした場合, the 統合デモ shall VMC ソースを使った Slot 駆動を再現できる。
5. The 新パッケージ VMC サンプル shall 利用者向け README またはサンプル内 README で、EVMC4U / uOSC の準備手順 (asmdef 追加手順含む) と既存 UI サンプルとの併用手順を明記する。
6. Where 新パッケージ VMC サンプル内で VMC 用のシーンを新規追加する場合, the シーン shall 新パッケージ専用の Scene asset として配置し、`.meta` の `guid` を乱数 32 桁 hex で新規発行する。

---

### Requirement 7: コアパッケージ側のコンパイル独立性

**Objective:** As a VMC を使わない利用者, I want コアパッケージのみを導入した状態で Unity プロジェクトがエラー無くコンパイルできること, so that EVMC4U / uOSC を準備しなくてもコア機能 (Slot 管理・Avatar Provider・Motion Pipeline・UI サンプル) を利用できる。

#### Acceptance Criteria

1. When コアパッケージ `com.hidano.realtimeavatarcontroller` のみを導入したプロジェクトを Unity Editor で開いた場合, the Unity Editor shall EVMC4U / uOSC / VMC 関連の名前参照不足によるコンパイルエラーを発生させない。
2. The コアパッケージ shall いずれの asmdef (`RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `RealtimeAvatarController.Avatar.Builtin` / `RealtimeAvatarController.Core.Editor` / 各 Tests asmdef / Samples~/UI asmdef) についても `references` に `"EVMC4U"` / `"uOSC.Runtime"` / `"RealtimeAvatarController.MoCap.VMC"` を含めない。
3. While コアパッケージ単独運用, the UI サンプル shall Stub Source 経由で起動・動作し、`SlotManagementDemo.unity` のシーン読み込み時にエラーや警告 (`MissingReferenceException` / `Could not load type` 等) を発生させない。
4. If コアパッケージ単独運用時にコアパッケージ asmdef のいずれかが VMC 関連参照を保持していたとき, then 本 Spec の検証手順 shall 該当 asmdef を不合格として扱い、参照削除を完了させる。
5. The コアパッケージ shall Player Build (例: Standalone / Mono) が VMC 未導入の状態でも成功することを CI またはローカル検証手順で確認できる状態を維持する。

---

### Requirement 8: 新パッケージのコンパイル要件と利用者プロジェクト前提

**Objective:** As a VMC 利用者, I want 新パッケージを導入したときの依存準備手順が明確であること, so that 利用者プロジェクト側で EVMC4U / uOSC を正しく準備すれば、新パッケージが従来 (コア同梱時) と同等の機能でコンパイル・動作する。

#### Acceptance Criteria

1. When 新パッケージとコアパッケージが導入され、利用者プロジェクトに `Assets/EVMC4U/` (asmdef 含む) と uOSC が正しく配置された場合, the Unity Editor shall 新パッケージ asmdef (`RealtimeAvatarController.MoCap.VMC` 他) をコンパイルエラー無くビルドする。
2. If 新パッケージが導入されているが EVMC4U asmdef または uOSC が利用者プロジェクトに存在しない場合, then the Unity Editor shall 新パッケージ asmdef のコンパイルエラーとして `EVMC4U` / `uOSC.Runtime` 名前参照不足を報告する (このエラーは利用者側の準備不足を示すもので、本 Spec のスコープ外)。
3. The 新パッケージ README shall 利用者向けに以下の準備手順を明記する: (a) `Assets/EVMC4U/` への EVMC4U インポート手順、(b) EVMC4U 用 asmdef (`EVMC4U.asmdef`) の作成手順 (本家 unitypackage に asmdef が含まれないため利用者側で作成が必要)、(c) uOSC の導入手順、(d) コアパッケージのバージョン整合性確認手順。
4. The 新パッケージ README shall 既存 `mocap-vmc` Spec で確定した動作 (typeId `"VMC"` / 属性ベース自己登録 / 共有 `ExternalReceiver` / `HumanoidMotionFrame` 発行) が新パッケージ移行後も変わらないことを明記する。
5. While 本 Spec のスコープ, the 新パッケージ shall EVMC4U asmdef への参照方法を `references: ["EVMC4U"]` のままに据置きとし、Reflection 化 (option ⑤) は別 Spec で対応する旨を README または CHANGELOG に明記する。

---

### Requirement 9: ドキュメンテーション (README / CHANGELOG / steering)

**Objective:** As a 利用者・メンテナ, I want 両パッケージの README / CHANGELOG とプロジェクト steering ドキュメントが新構成を正しく反映していること, so that 利用者は移行手順を理解でき、メンテナは新規構造をプロジェクトメモリとして保持できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall コアパッケージ側 README (リポジトリルートまたはコアパッケージ内 README) を更新し、VMC が別パッケージに分離された旨と、VMC 利用者向け移行手順 (新パッケージの導入方法へのリンクを含む) を記載する。
2. The mocap-vmc-package-split implementation shall コアパッケージ側 CHANGELOG に本 Spec による変更 (VMC 分離・UI サンプル Stub 化・asmdef references 削除) を版番号と共に記録する。
3. The mocap-vmc-package-split implementation shall 新パッケージ README を新設し、導入方法 / EVMC4U・uOSC 準備手順 / 既存 `mocap-vmc` Spec への参照 / 既知の制限 (Reflection 化未実施・利用者側 asmdef 作成必要) を記載する。
4. The mocap-vmc-package-split implementation shall 新パッケージ CHANGELOG を新設し、初回バージョンの内容 (旧コアパッケージから移動したファイル群の要約) を記録する。
5. The mocap-vmc-package-split implementation shall `.kiro/steering/structure.md` を更新し、新パッケージの存在・配置・依存方向 (コア → 一方向、新 → コア依存) をプロジェクト構造記述に追加する。
6. Where `.kiro/steering/` 配下にプロジェクト構造を参照する他のドキュメント (例: `tech.md`) が存在する場合, the 該当ドキュメント shall 新パッケージの追加に伴う矛盾が無いか確認され、必要に応じて軽微な追記が行われる。

---

### Requirement 10: 受け入れ検証手順 (合否判定)

**Objective:** As a メンテナ・レビュアー, I want 本 Spec の完了判定に使う検証手順が明確に定義されていること, so that 実装完了時に客観的な合否判定ができ、回帰検証時にも同一手順を再現できる。

#### Acceptance Criteria

1. The mocap-vmc-package-split implementation shall 検証シナリオ A として、コアパッケージのみを導入したプロジェクト (新パッケージ・EVMC4U・uOSC 未導入) で Unity Editor を起動し、コンパイルエラー / 重要警告無く `SlotManagementDemo.unity` が開ける状態を確認する。
2. The mocap-vmc-package-split implementation shall 検証シナリオ B として、コアパッケージと新パッケージの両方を導入し、利用者側で EVMC4U asmdef と uOSC を準備した状態で `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` および `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` の全テストが成功することを Unity Test Runner (または `Unity.exe -batchmode -runTests`) で確認する。
3. When 検証シナリオ A において UI サンプル (`SlotManagementDemo.unity`) を Play Mode で実行した場合, the UI サンプル shall Stub Source 経由で Slot Inspector / SlotErrorPanel / Fallback 設定 UI が機能し、UI 検証が VMC 不要で完了する。
4. When 検証シナリオ B において新パッケージの VMC サンプルをインポートした場合, the VMC サンプル shall 旧 UI サンプル相当の VMC 受信デモを再現でき、`HumanoidMotionFrame` の発行と Slot へのモーション適用が確認できる。
5. If 検証シナリオ A または B のいずれかが失敗した場合, then 本 Spec shall 未完了として扱い、原因の特定と修正後に検証を再実施する。
6. The mocap-vmc-package-split implementation shall 検証シナリオ A・B の手順とチェック項目を本 Spec の design / tasks フェーズで具体化し、CI または手動検証手順として再現可能な形で記録する。

---
