# Implementation Plan

> **実行モード**: Hybrid Option C による段階的 Move + 新設 + In-place 編集。Phase 構造は design.md の Migration Strategy に対応する (Phase 1 → 2 → 3 → 4 → 5 → 6 → Validate)。
>
> **重要制約**:
> - Phase 5 (旧 VMC パスの `git rm` による削除) は **親セッション専用タスク**。Subagent はファイル削除を実行できないため (`feedback_subagent_file_deletion.md`)、対応タスクは親セッションで処理する。
> - 新規 `.meta` GUID はすべて PowerShell `[guid]::NewGuid().ToString('N')` で乱数 32 桁 hex 生成する (CLAUDE.md グローバル規則)。連続パターン・1 文字シフト系列は禁止。
> - 既存 GUID (特に `5c4569b4a17944fba4667acebe26c25f` および 4 つの asmdef GUID) は移動操作中も据置きする。
> - Unity Editor を閉じた状態で `git mv` 操作を実行し、Library 再生成は移動完了後に Editor を開き直して許容する。

---

## Phase 1 — 新パッケージ scaffold + testables 登録

- [ ] 1. 新パッケージ scaffold とテスト解決基盤を確立する
- [ ] 1.1 新パッケージのルートディレクトリと `package.json` を作成する
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/` ディレクトリを新設し、`Runtime/` / `Editor/` / `Tests/EditMode/` / `Tests/PlayMode/` / `Samples~/VMC/Data/` / `Samples~/VMC/Scenes/` の空ディレクトリ階層を準備する (各ディレクトリに対応する `.meta` を含める。`.meta` GUID は乱数生成)。
  - `package.json` を新規作成し、`name = "com.hidano.realtimeavatarcontroller.mocap-vmc"`、`displayName` (VMC 用パッケージである旨)、`unity = "6000.3"`、`unityRelease = "10f1"`、`version = "0.1.0"`、`keywords` を設定する。
  - `dependencies` フィールドにコアパッケージ `com.hidano.realtimeavatarcontroller` を**固定バージョン**で記述する (range 演算子 `^` `~` 不使用)。EVMC4U / uOSC / UniRx は `dependencies` に含めず、利用者プロジェクト側および core 経由で解決させる。
  - `samples` 配列に VMC サンプル 1 件 (`displayName: "VMC Sample"` / `description` / `path: "Samples~/VMC"`) を登録する。
  - 観測可能な完了状態: 新パッケージのルート構造が Git に追加され、`package.json` が JSON として valid であることを `python -m json.tool` 等で確認できる。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_
  - _Boundary: NewPackageManifest_

- [ ] 1.2 リポジトリ `Packages/manifest.json` の `testables` に新パッケージを登録する
  - `RealtimeAvatarController/Packages/manifest.json` の `testables` 配列に `"com.hidano.realtimeavatarcontroller.mocap-vmc"` を追加する。現状 `testables: ["com.hidano.realtimeavatarcontroller"]` のみのため、追加しないと Phase 2 完了後に Test Runner ウィンドウへ新パッケージのテストが列挙されない。
  - 同 `dependencies` セクションに新パッケージ参照を追加 (ローカルパッケージ参照、リポジトリ内 `Packages/` 配下を Unity Package Manager に認識させる) する。
  - 観測可能な完了状態: Unity Editor を起動した際にコンパイルが通り、Package Manager ウィンドウで両パッケージがリスト表示される。
  - _Requirements: 4.1, 4.2, 4.4_
  - _Depends: 1.1_
  - _Boundary: VmcTestsEditAsmdef, VmcTestsPlayAsmdef_

---

## Phase 2 — VMC ランタイム / Editor / テストの新パッケージへの移動 (GUID 据置)

> **重要**: Phase 2 のすべての移動は Unity Editor を閉じた状態で `git mv` を用いて実施し、`.meta` GUID を保全する。移動完了後に Editor を再起動して Library 再生成を許容する。

- [ ] 2. VMC コードと asmdef を新パッケージへ GUID 据置で物理移動する
- [ ] 2.1 VMC Runtime 一式を新パッケージ Runtime/ へ移動する
  - 旧 `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/` 配下の全ソース (`VMCMoCapSourceConfig.cs` / `VMCMoCapSourceFactory.cs` / `EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `AssemblyInfo.cs`) と `RealtimeAvatarController.MoCap.VMC.asmdef` および対応 `.meta` を新パッケージ `Runtime/` 直下へ `git mv` で移動する。
  - asmdef 名 (`RealtimeAvatarController.MoCap.VMC`)、`rootNamespace`、`references` (`["RealtimeAvatarController.Core","RealtimeAvatarController.Motion","uOSC.Runtime","UniRx","EVMC4U"]`)、`includePlatforms` / `excludePlatforms` (空) を据置する。
  - `.meta` 内 `guid` 値および C# `namespace` (`RealtimeAvatarController.MoCap.VMC`) は変更禁止。
  - 観測可能な完了状態: 新パッケージ Runtime/ 配下に 5 つの `.cs` + 1 つの `.asmdef` + 各 `.meta` が配置され、`git log --follow` で旧パスからの move として追跡可能。
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  - _Boundary: VmcRuntimeAsmdef_

- [ ] 2.2 (P) VMC Editor 一式を新パッケージ Editor/ へ移動する
  - 旧 `Packages/com.hidano.realtimeavatarcontroller/Editor/MoCap/VMC/` 配下の `VmcMoCapSourceFactoryEditorRegistrar.cs` および `RealtimeAvatarController.MoCap.VMC.Editor.asmdef` と各 `.meta` を新パッケージ `Editor/` 直下へ `git mv` で移動する。
  - asmdef 名 (`RealtimeAvatarController.MoCap.VMC.Editor`)、`includePlatforms: ["Editor"]`、`references` (`["RealtimeAvatarController.MoCap.VMC","RealtimeAvatarController.Core"]` 等の据置構成) を変更しない。
  - 観測可能な完了状態: 新パッケージ Editor/ 配下に 1 つの `.cs` + 1 つの `.asmdef` + 各 `.meta` が配置され、Editor asmdef GUID が旧位置と一致する。
  - _Requirements: 3.1, 3.2, 3.3_
  - _Boundary: VmcEditorAsmdef_

- [ ] 2.3 (P) VMC EditMode テストを新パッケージ Tests/EditMode/ へ平置きで移動する
  - 旧 `Packages/com.hidano.realtimeavatarcontroller/Tests/EditMode/mocap-vmc/` 配下の全テストファイル (`EVMC4USharedReceiverTests.cs` / `EVMC4UMoCapSourceTests.cs` / `ExternalReceiverPatchTests.cs` / `VmcConfigCastTests.cs` / `VmcFactoryRegistrationTests.cs`) と `RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef` および各 `.meta` を新パッケージ `Tests/EditMode/` 直下へ平置きで `git mv` する (中間 `mocap-vmc/` ディレクトリは再現しない)。
  - asmdef 名と GUID、`references` 構成 (`RealtimeAvatarController.MoCap.VMC` / Test Framework / EVMC4U 等) を据置する。
  - 観測可能な完了状態: 新パッケージ Tests/EditMode/ 直下に 5 つのテスト `.cs` + 1 つの `.asmdef` + 各 `.meta` が配置される。
  - _Requirements: 4.1, 4.3, 4.6_
  - _Boundary: VmcTestsEditAsmdef_

- [ ] 2.4 (P) VMC PlayMode テストを新パッケージ Tests/PlayMode/ へ平置きで移動する
  - 旧 `Packages/com.hidano.realtimeavatarcontroller/Tests/PlayMode/mocap-vmc/` 配下の全テストファイル (`EVMC4UMoCapSourceIntegrationTests.cs` / `EVMC4UMoCapSourceSharingTests.cs` / `SampleSceneSmokeTests.cs`) と `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef` および各 `.meta` を新パッケージ `Tests/PlayMode/` 直下へ平置きで `git mv` する。
  - asmdef 名と GUID、`references` 構成を据置する。
  - 観測可能な完了状態: 新パッケージ Tests/PlayMode/ 直下に 3 つのテスト `.cs` + 1 つの `.asmdef` + 各 `.meta` が配置される。
  - _Requirements: 4.2, 4.3, 4.6_
  - _Boundary: VmcTestsPlayAsmdef_

- [ ] 2.5 Phase 2 完了チェックポイント — Test Runner ウィンドウでの目視検証
  - Unity Editor を起動し Library 再生成を待機後、`Window → General → Test Runner` を開く。
  - `EditMode` タブと `PlayMode` タブで `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` / `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` の両 asmdef がリスト表示されることを目視確認する。
  - Console の Errors == 0 を確認する。表示されない場合は Phase 1.2 の `manifest.json.testables` 設定または Phase 2.3/2.4 の asmdef GUID 据置に問題があるため、Phase 5 に進まずに `git revert` で rollback する。
  - 観測可能な完了状態: Test Runner ウィンドウに新パッケージの両テスト asmdef がリストされ、Console Errors == 0。
  - _Requirements: 4.4, 10.2_
  - _Depends: 1.2, 2.1, 2.3, 2.4_

---

## Phase 3 — Stub MoCap Source 新設 + UI Sample provider-agnostic 化

> **方針**: Stub Source は UI Sample の VMC 非依存化のためのダミー実装。Editor 自己登録は既存 `RealtimeAvatarController.Samples.UI.Editor` asmdef へ別ファイルとして追加し、Runtime asmdef との `#if UNITY_EDITOR` 同居は禁止する (VMC 既存パターン整合)。

- [ ] 3. UI Sample を Stub MoCap Source 経由で動作する provider-agnostic 構成へ再構成する
- [ ] 3.1 Stub MoCap Source / Stub Config / Stub Factory の Runtime 実装を新設する
  - `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/` に `StubMoCapSourceConfig.cs` を新設する (`MoCapSourceConfigBase` 継承の空 SO、`[CreateAssetMenu]` を付与)。
  - 同階層に `StubMoCapSource.cs` を新設する (`IMoCapSource` 実装、`SourceType => "Stub"`、`MotionStream` は `Subject<MotionFrame>().Synchronize().Publish().RefCount()` で空ストリーム emit、Initialize/Shutdown/Dispose の状態機械は `EVMC4UMoCapSource` と同等)。
  - 同階層に `StubMoCapSourceFactory.cs` を新設する (`IMoCapSourceFactory` 実装、`Create` で Config キャストと `StubMoCapSource` 生成、`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で `RegistryLocator.MoCapSourceRegistry` へ typeId="Stub" 自己登録、登録衝突時は `RegistryLocator.ErrorChannel` 通知)。
  - 各 `.cs` の `.meta` GUID は新規ランダム生成する。Sample Runtime asmdef 内に Editor 専用 API を `#if UNITY_EDITOR` で同居させない。
  - 観測可能な完了状態: コアパッケージのコンパイルがエラーなく完了し、Player Build にも Stub クラス群が含まれる。
  - _Requirements: 5.3, 5.4, 5.5_
  - _Boundary: StubMoCapSource, StubMoCapSourceConfig, StubMoCapSourceFactory_

- [ ] 3.2 Stub Editor 自己登録を `Samples.UI.Editor` asmdef に分離追加する
  - `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Editor/StubMoCapSourceFactoryEditorRegistrar.cs` を新規作成し、`[UnityEditor.InitializeOnLoadMethod]` で `RegistryLocator.MoCapSourceRegistry` へ typeId="Stub" 自己登録する (VMC 既存実装 `VmcMoCapSourceFactoryEditorRegistrar.cs` と同構造)。
  - 既存の `RealtimeAvatarController.Samples.UI.Editor.asmdef` (`includePlatforms: ["Editor"]`) はそのまま使用し、`asmdef` の編集は不要。
  - `.cs` の `.meta` GUID は新規ランダム生成する。
  - 観測可能な完了状態: Unity Editor 起動直後に `MoCapSourceRegistry` で typeId="Stub" が解決可能になり、Inspector ドロップダウンに "Stub" が列挙される。
  - _Requirements: 5.5_
  - _Depends: 3.1_
  - _Boundary: StubMoCapSourceFactoryEditorRegistrar_

- [ ] 3.3 UI Sample asmdef references から VMC 参照を削除する
  - `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/RealtimeAvatarController.Samples.UI.asmdef` の `references` 配列から `"RealtimeAvatarController.MoCap.VMC"` を削除する。
  - 残存 references が `["RealtimeAvatarController.Core","RealtimeAvatarController.Motion","RealtimeAvatarController.Avatar.Builtin","UniRx","UniTask"]` 構成になることを確認する。
  - Samples~/UI/Runtime 配下の C# ソース内に `using RealtimeAvatarController.MoCap.VMC;` または同 namespace の型直接参照が残っていないことを Grep で確認する。
  - 観測可能な完了状態: コアパッケージの UI Sample asmdef が VMC 非依存となり、`Samples~/UI/Runtime` の任意ファイルに対する `Grep "RealtimeAvatarController.MoCap.VMC"` がヒットゼロ件。
  - _Requirements: 5.1, 5.2, 7.2_
  - _Depends: 3.1_
  - _Boundary: UISampleAsmdef_

- [ ] 3.4 Stub Config SO アセットを生成する
  - `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Data/StubMoCapSourceConfig_Shared.asset` を Unity Editor の CreateAssetMenu 経由で新規作成する。
  - 対応 `.meta` の GUID は PowerShell `[guid]::NewGuid().ToString('N')` で乱数 32 桁 hex として生成し、既存 21 GUID と一致しないこと、相互シフトパターンを取らないことを Grep / 目視で確認する。
  - 観測可能な完了状態: `.asset` と `.meta` がリポジトリに追加され、Inspector で Stub Config が表示される。
  - _Requirements: 5.3, 5.6, 5.7_
  - _Depends: 3.1_
  - _Boundary: StubMoCapSourceConfig_

- [ ] 3.5 SlotSettings_Shared_Slot1/2 の Config 参照を Stub Config に差し替える
  - Unity Editor で `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Data/SlotSettings_Shared_Slot1.asset` を Inspector で開き、`moCapSourceDescriptor.SourceTypeId` を `"VMC"` → `"Stub"` に変更する。
  - 同 `moCapSourceDescriptor.Config` 欄に Stub Config (`StubMoCapSourceConfig_Shared.asset`) をドラッグ&ドロップで割り当て、`Config.guid` を Stub Config の新規 GUID へ更新する。`fileID: 11400000` / `type: 2` は据置。
  - `SlotSettings_Shared_Slot2.asset` も同様に編集する。
  - 観測可能な完了状態: 両 SlotSettings の YAML 内 `moCapSourceDescriptor` ブロックが Stub Config 参照に書き換わり、Inspector に "Stub" typeId が表示される。
  - _Requirements: 5.6_
  - _Depends: 3.4_
  - _Boundary: SlotSettingsSharedAssetEdit_

---

## Phase 4 — 新パッケージ Samples~/VMC の新設

- [ ] 4. 新パッケージ側に VMC サンプル一式を新設する
- [ ] 4.1 VMCMoCapSourceConfig_Shared.asset を新パッケージ Samples~/VMC/Data/ へ GUID 据置で移動する
  - 旧 `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset` および対応 `.meta` を Unity Editor を閉じた状態で `git mv` で新パッケージ `Samples~/VMC/Data/VMCMoCapSourceConfig_Shared.asset` へ移動する。
  - `.meta` の `guid: 5c4569b4a17944fba4667acebe26c25f` を**変更禁止**で据置する (既存 SO 参照を壊さない要)。
  - 観測可能な完了状態: 移動先 `.meta` の GUID 値が `5c4569b4a17944fba4667acebe26c25f` であることを確認できる。
  - _Requirements: 6.2_
  - _Boundary: VmcSamples_

- [ ] 4.2 (P) 新パッケージ単独完結用 BuiltinAvatarProviderConfig_VmcDemo.asset を新規作成する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Samples~/VMC/Data/BuiltinAvatarProviderConfig_VmcDemo.asset` を Unity Editor の CreateAssetMenu 経由で新規作成する (script ref はコアパッケージの `BuiltinAvatarProviderConfig`、`avatarPrefab` は `null` で利用者が VMC 受信デモで Inspector から設定する前提)。
  - **UI Sample 側の `BuiltinAvatarProviderConfig_AvatarA.asset` 等とは Sample 間 GUID 参照を持たない独立コピー**として作成する (Unity Sample import 時のバージョンサフィックス付きパス展開への耐性確保)。
  - `.meta` GUID は乱数 32 桁 hex で新規生成する。
  - 観測可能な完了状態: 新パッケージ Samples~/VMC/Data/ に `.asset` と `.meta` が追加され、新規 GUID が CLAUDE.md ルールに沿って生成されている。
  - _Requirements: 6.3_
  - _Boundary: VmcSamples_

- [ ] 4.3 SlotSettings_VMC_Slot1.asset を新規作成する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Samples~/VMC/Data/SlotSettings_VMC_Slot1.asset` を新規作成する。
  - `slotId: "vmc-slot-01"` / `displayName: "VMC Slot 1"`、`avatarProviderDescriptor.ProviderTypeId: "Builtin"` + `Config` 参照は同パッケージ `BuiltinAvatarProviderConfig_VmcDemo.asset`、`moCapSourceDescriptor.SourceTypeId: "VMC"` + `Config` 参照は移動済み `VMCMoCapSourceConfig_Shared.asset` (GUID `5c4569b4a17944fba4667acebe26c25f`) を割当てる。
  - `.meta` GUID は乱数生成する。
  - 観測可能な完了状態: SlotSettings の Inspector で VMC Config と Builtin Avatar Provider Config が共に解決済み表示になる。
  - _Requirements: 6.3, 6.4_
  - _Depends: 4.1, 4.2_
  - _Boundary: VmcSamples_

- [ ] 4.4 VMCReceiveDemo.unity シーンを新規作成する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Samples~/VMC/Scenes/VMCReceiveDemo.unity` を新規作成する。
  - 最小構成: Camera + DirectionalLight + `SlotManagerBehaviour` (initialSlots に `SlotSettings_VMC_Slot1` を割当)。
  - `.meta` GUID は乱数生成する。
  - 観測可能な完了状態: Unity Editor でシーンを開けて Console Errors == 0、`SlotManagerBehaviour` の inspector に `SlotSettings_VMC_Slot1` が割り当てられている。
  - _Requirements: 6.1, 6.3, 6.4, 6.6_
  - _Depends: 4.3_
  - _Boundary: VmcSamples_

---

## Phase 5 — 旧 VMC パスの削除 (親セッション専用)

> **重要**: Phase 5 のすべての削除タスクは Subagent 実行不可 (`feedback_subagent_file_deletion.md` ルールにより `rm` / `git rm` が Subagent から呼べない)。**必ず親セッションで実施**する。

- [ ] 5. コアパッケージから旧 VMC パスを完全削除する **(親セッション専用 / Subagent 不可)**
- [ ] 5.1 旧 VMC Runtime ディレクトリをコアパッケージから削除する **(親セッション専用)**
  - Unity Editor を閉じた状態で `git rm -r Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/` を親セッションで実行する。配下の `.cs` / `.asmdef` / `.meta` および `MoCap/VMC/` ディレクトリ自体の `.meta` も含めて完全削除する。
  - 観測可能な完了状態: コアパッケージ Runtime/MoCap/ 配下に VMC ディレクトリと残存 `.meta` が一切存在しない。
  - _Requirements: 2.7_

- [ ] 5.2 旧 VMC Editor ディレクトリをコアパッケージから削除する **(親セッション専用)**
  - 親セッションで `git rm -r Packages/com.hidano.realtimeavatarcontroller/Editor/MoCap/VMC/` を実行する。
  - 観測可能な完了状態: コアパッケージ Editor/MoCap/ 配下に VMC ディレクトリと `.meta` が残存しない。
  - _Requirements: 3.5_

- [ ] 5.3 旧 VMC EditMode/PlayMode テストディレクトリをコアパッケージから削除する **(親セッション専用)**
  - 親セッションで `git rm -r Packages/com.hidano.realtimeavatarcontroller/Tests/EditMode/mocap-vmc/` および `git rm -r Packages/com.hidano.realtimeavatarcontroller/Tests/PlayMode/mocap-vmc/` を実行する。
  - 観測可能な完了状態: コアパッケージ Tests/EditMode および Tests/PlayMode 配下に `mocap-vmc/` ディレクトリが存在しない。
  - _Requirements: 4.5_

- [ ] 5.4 旧 UI Sample 内 VMCMoCapSourceConfig_Shared.asset の削除確認 **(親セッション専用)**
  - Phase 4.1 の `git mv` により旧 `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Data/VMCMoCapSourceConfig_Shared.asset(.meta)` は新パッケージ側へ既に移動済み。改めて Git status で旧位置に該当ファイルが**残存していない**ことを親セッションで確認する。
  - 万一旧位置に残存ファイルがあれば `git rm` で削除する。
  - 観測可能な完了状態: コアパッケージ `Samples~/UI/Data/` 配下に `VMCMoCapSourceConfig_Shared.asset` が存在しない。
  - _Requirements: 5.8_
  - _Depends: 4.1_

- [ ] 5.5 コアパッケージ内 VMC 名前参照ゼロ件を Grep で検証する
  - `Packages/com.hidano.realtimeavatarcontroller/` 配下の全 `.cs` および `.asmdef` に対し `Grep "RealtimeAvatarController.MoCap.VMC"` および `Grep "EVMC4U"` および `Grep "uOSC.Runtime"` を実行し、ヒットゼロ件を確認する。
  - ヒットがあれば該当 asmdef を不合格として扱い、Phase 3 / 5 の修正完了まで Phase 6 に進まない。
  - 観測可能な完了状態: コアパッケージ全 `.cs` / `.asmdef` で VMC / EVMC4U / uOSC.Runtime 関連名前参照が全件ヒットゼロ。
  - _Requirements: 2.6, 2.7, 3.4, 3.5, 7.1, 7.2, 7.4_
  - _Depends: 5.1, 5.2, 5.3, 5.4_

---

## Phase 6 — ドキュメンテーション (README / CHANGELOG / steering)

- [ ] 6. 両パッケージのドキュメントとプロジェクト steering を新構成に整合させる
- [ ] 6.1 (P) コアパッケージ README に VMC 分離記述を追記する
  - リポジトリルートまたは `Packages/com.hidano.realtimeavatarcontroller/README.md` に「VMC 受信機能は別パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` に分離されました」記述を追加する。
  - VMC 利用者向け移行手順 (新パッケージ追加導入の手順) と新パッケージ README へのリンクを記載する。
  - 「UI Sample は Stub MoCap Source 経由で動作し、Slot UI 検証が VMC 不要で完結する」旨を明記する。
  - 観測可能な完了状態: README に VMC 分離セクションが追加される。
  - _Requirements: 5.10, 9.1_
  - _Boundary: CoreReadmeUpdate_

- [ ] 6.2 (P) コアパッケージ CHANGELOG に変更記録を追加する
  - `Packages/com.hidano.realtimeavatarcontroller/CHANGELOG.md` に新バージョン (例: `[0.2.0] - YYYY-MM-DD`) を追記する。
  - `### Removed` (VMC Runtime / Editor / Tests / Sample Data 移動)、`### Changed` (UI Sample asmdef references / SlotSettings 差替)、`### Added` (Stub MoCap Source / Stub Config 新設)、`### Migration` (利用者向け案内) のセクションを記述する。
  - 観測可能な完了状態: CHANGELOG に新バージョンエントリが追加される。
  - _Requirements: 9.2_
  - _Boundary: CoreChangelogUpdate_

- [ ] 6.3 (P) 新パッケージ README を新設する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/README.md` を新規作成する。
  - 導入手順 (`manifest.json` への両パッケージ追加方法)、利用者準備手順 (a. `Assets/EVMC4U/` への EVMC4U インポート、b. EVMC4U 用 asmdef 自作手順 — 本家 unitypackage に asmdef が含まれない理由含む、c. uOSC 導入手順、d. コアパッケージとのバージョン整合確認) を記載する。
  - 既存 `mocap-vmc` Spec で確定した動作 (typeId="VMC" / 属性ベース自己登録 / 共有 ExternalReceiver / `HumanoidMotionFrame` 発行) が新パッケージ移行後も継承される旨を明記する。
  - 既知の制限 (Reflection 化未実施 (option ⑤、別 spec で対応予定)、利用者側 asmdef 作成必要) を記載する。
  - 観測可能な完了状態: 新パッケージ README が作成され、利用者が手順に沿って導入できる構成となる。
  - _Requirements: 6.5, 8.3, 8.4, 8.5, 9.3_
  - _Boundary: NewPackageReadme_

- [ ] 6.4 (P) 新パッケージ CHANGELOG を新設する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/CHANGELOG.md` を新規作成する。
  - `[0.1.0] - YYYY-MM-DD` 初回バージョンエントリ。`### Added` で旧コアパッケージから移動したファイル群 (Runtime / Editor / Tests / Sample Data) の要約と動作確認済みバージョン表 (UniRx / uOSC / EVMC4U / Unity) を記述する。
  - 観測可能な完了状態: CHANGELOG が作成される。
  - _Requirements: 9.4_
  - _Boundary: NewPackageChangelog_

- [ ] 6.5 (P) `.kiro/steering/structure.md` を新規作成する
  - `.kiro/steering/structure.md` を新規作成し、`## Packages` (両パッケージ概要)、`## Dependency Direction` (新 → コア一方向)、`## Sample Imports` (UI Sample / VMC Sample 併用パターン) の最小スコープで記述する。
  - 既存 `tech.md` / `product.md` 等が `.kiro/steering/` に存在しないため新規作成のみで完結する。
  - 観測可能な完了状態: steering ファイルが作成され、後続 spec が依存マップを参照できる。
  - _Requirements: 9.5, 9.6_
  - _Boundary: SteeringStructureDoc_

---

## Phase 7 — 受け入れ検証 (シナリオ A / B)

> 検証手順は `.kiro/specs/mocap-vmc-package-split/validation.md` または README に記録し、再現可能な runbook 形式で残す (要件 10.6)。

- [ ] 7. 検証シナリオ A / B を実施し合否を判定する
- [ ] 7.1 検証シナリオ A — コアパッケージ単独運用での合否判定
  - 新規 Unity プロジェクト (Unity 6000.3.10f1) に対し `Packages/manifest.json.dependencies` にコアパッケージのみ追加する (新パッケージ・EVMC4U・uOSC は追加しない)。
  - Unity Editor を起動し Console をクリア後、コンパイルを実行して **Errors == 0** を確認する。
  - Package Manager で UI Sample をインポートし、`Assets/Samples/Realtime Avatar Controller/<version>/UI/Scenes/SlotManagementDemo.unity` を開く。Console Errors == 0 を確認する。
  - Play Mode を起動し、Slot Add / Remove / Fallback 設定切替 / Error Simulation の 4 操作を実行して Console Errors == 0 が維持されることを確認する。
  - (Optional) Player Build (Standalone Mono) を実行し Build Errors == 0 / `MissingReferenceException` 系警告ゼロを確認する。
  - 観測可能な完了状態: Console Errors == 0 を保ったまま UI 操作 4 件が完了する。
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 10.1, 10.3, 10.5, 10.6_
  - _Depends: 5.5, 6.5_

- [ ] 7.2 検証シナリオ B — 両パッケージ + EVMC4U + uOSC 環境での合否判定
  - 検証シナリオ A の環境に対し、新パッケージを `manifest.json.dependencies` に追加。`Assets/EVMC4U/` に EVMC4U unitypackage を取り込み `EVMC4U.asmdef` (`name: "EVMC4U"`, `references: ["uOSC.Runtime"]`) を新規作成する。uOSC を `manifest.json.dependencies` に追加する。
  - Unity Editor を再起動し Console Errors == 0 を確認する。
  - `Window → General → Test Runner` で `EditMode` / `PlayMode` の `RealtimeAvatarController.MoCap.VMC.Tests.*` を全件実行し、**全 pass** を合否基準とする。または CLI: `Unity.exe -batchmode -runTests -projectPath <path> -testPlatform EditMode|PlayMode -testResults <path>` で検証する。
  - VMC Sample をインポートし `VMCReceiveDemo.unity` を開く。外部 VMC 送信ソースを起動した状態で Play Mode を実行し、`HumanoidMotionFrame` が `MotionStream` 経由で発行され Avatar Pose が変化することを確認する (外部 VMC 送信ソースが用意できない場合は手順 6/7 のテスト全 pass のみで合否判定する代替路を許容する)。
  - 観測可能な完了状態: Test Runner で全 VMC テストが pass し、VMCReceiveDemo シーンが Play Mode で起動可能になる。
  - _Requirements: 1.7, 4.4, 6.4, 8.1, 8.2, 10.2, 10.4, 10.5, 10.6_
  - _Depends: 7.1_

- [ ] 7.3 spec 完了判定と未完了時 rollback 手順の確認
  - シナリオ A / B のいずれかが失敗した場合は本 spec を未完了として扱い、原因特定後に再実施する (要件 10.5)。
  - rollback triggers (design.md Migration Strategy 参照): Phase 2 後にテスト未列挙 → Phase 1 まで `git revert`、Phase 3 後にコア単独コンパイル失敗 → Phase 2 まで `git revert`、Phase 5 後にシナリオ A 失敗 → Phase 4 まで `git revert`。
  - 観測可能な完了状態: シナリオ A / B 双方が pass し、`spec.json.phase` を `tasks-approved` または後続実装フェーズへ遷移可能な状態となる。
  - _Requirements: 10.5, 10.6_
  - _Depends: 7.2_

---

## Requirements Coverage Map (要件 ↔ タスク対応)

| Req | Tasks |
|-----|-------|
| 1.1, 1.2, 1.3, 1.4, 1.5, 1.6 | 1.1 |
| 1.7 | 7.2 |
| 2.1, 2.2, 2.3, 2.4, 2.5 | 2.1 |
| 2.6, 2.7 | 5.1, 5.5 |
| 3.1, 3.2, 3.3 | 2.2 |
| 3.4, 3.5 | 5.2, 5.5 |
| 4.1 | 1.2, 2.3 |
| 4.2 | 1.2, 2.4 |
| 4.3, 4.6 | 2.3, 2.4 |
| 4.4 | 1.2, 2.5, 7.2 |
| 4.5 | 5.3 |
| 5.1, 5.2 | 3.3 |
| 5.3 | 3.1, 3.4 |
| 5.4 | 3.1 |
| 5.5 | 3.1, 3.2 |
| 5.6 | 3.4, 3.5 |
| 5.7 | 3.4 |
| 5.8 | 5.4 |
| 5.9 | 7.1 |
| 5.10 | 6.1 |
| 6.1 | 4.4 |
| 6.2 | 4.1 |
| 6.3 | 4.2, 4.3, 4.4 |
| 6.4 | 4.3, 4.4, 7.2 |
| 6.5 | 6.3 |
| 6.6 | 4.4 |
| 7.1, 7.2, 7.3, 7.4, 7.5 | 5.5, 7.1 |
| 8.1, 8.2 | 7.2 |
| 8.3, 8.4, 8.5 | 6.3 |
| 9.1 | 6.1 |
| 9.2 | 6.2 |
| 9.3 | 6.3 |
| 9.4 | 6.4 |
| 9.5, 9.6 | 6.5 |
| 10.1 | 7.1 |
| 10.2 | 2.5, 7.2 |
| 10.3 | 7.1 |
| 10.4 | 7.2 |
| 10.5, 10.6 | 7.1, 7.2, 7.3 |
