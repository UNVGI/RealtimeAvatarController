# タスク 7.2 ハンドオーバー: Sample Scene 目視確認は DEFERRED TO USER

- **対象タスク**: tasks.md §7.2 [Research] Sample Scene の `SlotManagementDemo` が無改修で動作することの目視確認
- **ステータス**: DEFERRED TO USER (自動化不能)
- **要件**: 7.6, 11.3
- **タスク 7.1 との関係**: 自動化可能なプログラマティック経路 (SlotSettings → SlotManager.AddSlotAsync → EVMC4UMoCapSource Resolve → MotionStream 購読 / 参照共有 / Release→Resolve 差替) は
  `Tests/PlayMode/mocap-vmc/SampleSceneSmokeTests.cs` (3 ケース) でカバー済み。

## なぜユーザー側で実施する必要があるか

本タスクは Unity Editor 上で実 Scene (`SlotManagementDemo.unity`) を Play モードで開き、

- Slot 追加 UI 操作
- Slot 削除 UI 操作
- Slot 差替 UI 操作
- アバターへのポーズ反映の目視

といった**ランタイム画面を伴う視認**が必要であり、
サブエージェント (CLI / batchmode / headless 実行) では実行不能である。
`-runTests -batchmode` の自動テスト経路では Scene アセットを Play せず、UI 操作も再現できない。

## ユーザー実施手順の案

1. Unity Editor で
   `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Scenes/SlotManagementDemo.unity`
   または Package Manager からインポート済みの
   `Assets/Samples/Realtime Avatar Controller/<version>/UI Sample/Scenes/SlotManagementDemo.unity`
   を開く。
2. `VMCMoCapSourceConfig.asset` / `SlotSettings.asset` が参照するプロバイダ設定を確認 (無改修のまま)。
3. Play を開始。
4. 次を目視確認:
   - Slot を UI から追加すると EVMC4U 経由の受信で Slot が Active になり、アバターへのボーン反映 (またはプレビュー表示) が起きる。
   - Slot を削除すると他の Slot は影響を受けずに動作し続ける。
   - Slot 差替 (Remove → Add) 時にコンソールへ `NullReferenceException` や `SocketException` が出ない。
5. 期待と異なる挙動 (red) があれば、本 Spec スコープ外として別 Issue / 別 Spec で起票する (要件 7.6 / 11.3)。

## 自動化の限界ラインに関するメモ

- 本 Spec の自動化スコープはここまで (要件 12.1〜12.7 の範囲内) で完結する。
- 将来的に Unity Editor のヘッドレス Scene Play (e.g. `EditorSceneManager.OpenScene` + `EditorApplication.isPlaying = true`) を用いた PlayMode Scene 統合テストを追加する場合は、別 Spec で扱う。
