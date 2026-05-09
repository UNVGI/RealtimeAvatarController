# 検証レポート

## 2026-05-09: 7.1 検証シナリオ A — コアパッケージ単独運用での合否判定

- Date: 2026-05-09
- Scenario A status: FAIL
- 判定理由: 構造検証ではコアパッケージの VMC/EVMC4U/uOSC 型・アセンブリ参照は検出されなかったが、Unity EditMode batchmode で `RealtimeAvatarController.Core` のコンパイルエラーが発生したため。

### 構造検証結果

- `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller` 配下の `*.cs` / `*.asmdef` / `*.asmref` に対し、`EVMC4U|uOSC|RealtimeAvatarController\.MoCap\.VMC` を grep した結果、該当なし。
- コアパッケージ配下の `*.asmdef` に対し、`EVMC4U|uOSC|RealtimeAvatarController\.MoCap\.VMC|RealtimeAvatarController\.MoCap\.VMC\.Editor|RealtimeAvatarController\.MoCap\.VMC\.Tests` を grep した結果、該当なし。
- コア asmdef の参照は `RealtimeAvatarController.Core`, `RealtimeAvatarController.Motion`, `RealtimeAvatarController.Avatar.Builtin`, `UniRx`, `UniTask`, Unity Test Runner 系に閉じており、`EVMC4U` / `uOSC.Runtime` / `RealtimeAvatarController.MoCap.VMC` 参照はなし。
- 補足: テストデータ、コメント、UI 表示文には `"VMC"` 文字列が残っているが、型・namespace・asmdef 参照ではないため、今回の「コア compile graph の VMC/EVMC4U/uOSC 参照禁止」判定の失敗要因には含めない。
- `RealtimeAvatarController/Packages/manifest.json` の `testables` は以下 2 件を含むことを確認。
  - `com.hidano.realtimeavatarcontroller`
  - `com.hidano.realtimeavatarcontroller.mocap-vmc`

### Unity batchmode 結果

- 実行対象: Unity 6000.3.10f1 / EditMode
- 実行コマンド: `Unity.exe -batchmode -nographics -projectPath "D:\Personal\Repositries\RealtimeAvatarController\RealtimeAvatarController" -runTests -testPlatform EditMode -testCategory "" -testFilter "RealtimeAvatarController.Core.Tests.EditMode|RealtimeAvatarController.Avatar.Builtin.Tests.EditMode|RealtimeAvatarController.Motion.Tests.EditMode" -testResults "D:\Personal\Repositries\RealtimeAvatarController\test-results-7-1.xml" -logFile "D:\Personal\Repositries\RealtimeAvatarController\unity-7-1.log"`
- 終了コード: `1`
- `test-results-7-1.xml`: 未生成のため failure count は取得不可。
- `unity-7-1.log`: evidence としてコミット対象にする。ログはコンパイル段階で停止しており、Test Runner の結果 XML 生成まで到達していない。
- 主なエラー:
  - `Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Interfaces\IAvatarProvider.cs(3,7): error CS0246: The type or namespace name 'Cysharp' could not be found`
  - `Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Slot\SlotManager.cs(4,7): error CS0246: The type or namespace name 'Cysharp' could not be found`
  - `Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Interfaces\IAvatarProvider.cs(28,9): error CS0246: The type or namespace name 'UniTask<>' could not be found`
  - `Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Slot\SlotManager.cs(74,22): error CS0246: The type or namespace name 'UniTask' could not be found`
  - `Packages\com.hidano.realtimeavatarcontroller\Runtime\Core\Slot\SlotManager.cs(132,16): error CS0246: The type or namespace name 'UniTask' could not be found`
- 参考観察: `RealtimeAvatarController.Core.asmdef` は `UniTask` を参照しているが、現行 `Packages/manifest.json` / `packages-lock.json` には `com.cysharp.unitask` が確認できなかった。

### 手動確認メモ

- spec 要件 7.1 の完全な受け入れには、Unity Editor の Test Runner / Console で UI Sample import、`SlotManagementDemo.unity` オープン、Play Mode で Slot Add / Remove / Fallback 設定切替 / Error Simulation の手動確認が必要。
- 現時点では batchmode のコアコンパイルエラーにより Scenario A は未受け入れ。原因修正後に手動 Test Runner 検証を再実施する。
