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

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
