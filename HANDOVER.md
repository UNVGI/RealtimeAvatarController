# Handover (2026-04-22)

## 今回やったこと

### 1. 前セッションの VRM モデルコミットを履歴から除去
- `bb55b94` (`uchinoko_dress.vrm` 追加) と `aad8061` (`KizunaAI_KAMATTE.vrm` 追加) を `git rebase --onto 31069c1 aad8061 main` で履歴から完全削除
- `backup/before-vrm-drop` ブランチを保険として作成 (動作確認済のため次セッション以降で削除可)
- rebase 中の conflict (manifest.json / packages-lock.json で `com.vrmc.vrm` / `com.unity.timeline` 依存が bb55b94 のみから来ていた) は backup 側の最終状態を採用して解決

### 2. VRM 0.x/1.x 問題の原因特定
- 前セッション §6 で「座標系ズレ」と推測した不具合の実体は **VRM 1.x モデルで自前 Transform 直接書込が正しく動作しない** だったと判明 (VRM 0.x モデルで自前実装も正常動作)
- 自前実装の座標系処理自体は正しかったが、VRM 1.x 対応を自前で解くコストと EVMC4U の検証実績を天秤にかけて **EVMC4U 全面置換方針を維持**

### 3. EVMC4U 全面置換を完遂 (mocap-vmc spec 再実行)
- **4 論点合意**: Adapter 方式維持 / 共有 Receiver + Adapter / Config asset 保持 / mocap-vmc+contracts.md 整備
- **Kiro 正攻法**: `/kiro:spec-requirements` → `/kiro:spec-design` → `/kiro:spec-tasks` を spec-requirements/design/tasks-agent で再生成、3 相承認
- **実装**: `/kiro:spec-run mocap-vmc` を **Phase 単位バッチ** で実行 (per-task だと 16 時間超試算 → Phase 単位で ~3 時間に圧縮)
- **Unity テスト**: MCP ではなく `Unity.exe -batchmode -runTests` を Bash で直叩き (子 claude-p の MCP が別プロジェクト Unity と混同する問題を回避)
- **結果**: 32 subtask + 2 fix commit、EditMode 347/347 + PlayMode 34/34 全 pass
- `/kiro:validate-impl mocap-vmc` で **GO 判定** (Finding は Warning 3 + 追加リスク 3、Critical なし)

### 4. リポジトリ整備
- 過去セッションの一時ファイル削除: `_impl_log_*.txt` 9, `remove_*_gitkeep.sh` 2, `t12-2.diff`, `cleanup_temp.sh`
- 空 asmdef フォルダ削除: `Editor/Avatar/Builtin/`, `Editor/Motion/` (skeleton だけで中身なし)

## 決定事項

- **mocap-vmc は EVMC4U wrapper で実装**: 自前 VMC 実装 (`VmcMoCapSource` / `VmcOscAdapter` / `VmcFrameBuilder` / `VmcMessageRouter` / `VmcBoneMapper` / `VmcTickDriver`) は完全削除
- **2 層構成**: `EVMC4UMoCapSource` (IMoCapSource Adapter) + `EVMC4USharedReceiver` (process-wide singleton + refcount + DontDestroyOnLoad)
- **EVMC4U 配布**: `.unitypackage` を `Assets/EVMC4U/` に展開 (UPM 非対応)、ローカルパッチ 4 種を適用 (header marker `// [RealtimeAvatarController mocap-vmc local patch]` コメント付き)
- **EVMC4U 改変 4 点**: Model=null ガード緩和 / bone table readonly アクセサ / LatestRoot\* キャッシュ / InjectBoneRotationForTest setter
- **contracts.md §2.2** `HumanoidMotionFrame.BoneLocalRotations` 保持、§13.1 MainThread 訂正済 (uOSC `onDataReceived` は `uOscServer.Update` から MainThread で発火)
- **typeId `"VMC"` は維持**: Config/Factory 名と UI/SlotSettings asset 互換性のため
- **Root 書込なし**: `RootPositionSynchronize` / `RootRotationSynchronize` を init 時に false 固定 (Hips と二重回転する問題回避)
- **Config 保持**: `VMCMoCapSourceConfig.asset` (port / bindAddress) を維持、Adapter が起動時に EVMC4U へ push
- **バッチ実装は Phase 単位で claude-p 起動**、Unity テストは batchmode CLI 直叩き (memory に feedback 保存)

## 捨てた選択肢と理由

- **選択肢 B (フレーム抽象捨てて EVMC4U 直結)**: 既存 slot-core / motion-pipeline の Fallback / Cache / Slot 管理を VMC 用に再実装する必要があり、スコープ過大
- **Slot ごと ExternalReceiver + daisy-chain**: `MoCapSourceRegistry` の「同一 Config は 1 source を共有」思想とズレる。1 つの共有 ExternalReceiver から Adapter が配る方式に
- **`VMCMoCapSourceConfig.asset` 廃止 → Inspector 直設定**: Runtime 追加 Slot の仕組みを作り直す必要、UI Sample 大改修
- **per-task バッチ実行**: 32 claude-p 起動で 16+ 時間見込み、Phase 単位で 3〜5 時間に圧縮
- **Unity テスト MCP 経由**: 子 claude-p の MCP が別プロジェクト Unity と混同し hang する事象が発生、batchmode CLI に変更
- **EVMC4U を Model=null なしで運用**: Model 指定すると EVMC4U 側の Transform 書込と `HumanoidMotionApplier` で二重書込になる
- **asmdef から uOSC.Runtime 参照撤去** (task 6.3 原指示): `EVMC4USharedReceiver` が `uOscServer.StopServer/StartServer` を直接制御する必要があるため保持 (validate-impl Finding 1、design §7.1 との微差として受容)
- **rebase 中に素直に conflict を削除**: manifest.json から `com.vrmc.vrm` 依存が失われる事態になるため、backup 側の最終状態を checkout で採用

## ハマりどころ

- **子 claude-p は `rm` / `git rm` / `mv` 実行不可**: Phase 6 で FAIL、親セッションで削除処理してから再度 delegate する必要あり (memory に feedback 保存)
- **Phase 6.1 で `AssemblyInfo.cs` を削除したら tests の internal constructor アクセス破綻**: `EVMC4UMoCapSource` の constructor が `internal` で Test assembly が `InternalsVisibleTo` 宣言に依存していた。復元 commit 必要
- **Task 1.1 (Research) に 1 時間 hang**: Unity MCP の応答待ちで詰まり、子 claude-p の UnityTestRunner MCP が別セッションの Unity と混線
- **rebase --onto での manifest 系 conflict**: 削除対象 commit に含まれる `com.vrmc.vrm` / `com.unity.timeline` 依存が後続 commit の前提になっていたため、素直な rebase resolution では依存が落ちる
- **EVMC4U は `.unitypackage` のみの配布**: UPM git URL / OpenUPM / npm いずれも非対応。asmdef は既存 uOSC/UniVRM/UniGLTF/MToon/VRM10 の GUID を参照するため追加インストール不要で解決
- **EVMC4U `ProcessMessage` の早期 return**: Model=null だと Bone Dictionary にも書き込まれないためパッチで緩和が必須
- **`uOscServer` に `bindAddress` フィールド未存在**: Config の `bindAddress` は情報保持のみで現状反映不可、将来の uOSC 拡張待ち

## 学び

- Kiro `/kiro:spec-run` は **per-task より Phase 単位でバッチ** する方が圧倒的に速い (claude-p startup + Unity batchmode テストのコストを amortize できる)
- **Unity CLI batchmode は MCP より信頼性が高い** (複数プロジェクト Unity 並行時の MCP 混同を避けられる)
- 子 claude-p (Agent spawn) は **sandbox 制約で削除系コマンド不可**、削除を伴う phase は親で先行処理する
- `rebase --onto` で中間 commit を削除する場合、その commit に含まれる **生きている依存の変更** (package.json / lock) が後続 commit に影響しうる
- **VRM 0.x / 1.x では Humanoid rig の normalization が異なる**: 自前 Transform 直接書込方式だと rig 差で姿勢がズレる可能性があり、EVMC4U のような実績ある実装に寄せる方が安全
- **EVMC4U のソース読み** で発覚した重要事項:
  - `HumanBodyBonesRotationTable` は private (要 readonly アクセサ追加)
  - `ProcessMessage` の Model=null early-return (要緩和)
  - Root は `RootPositionTransform.localPosition` に直書き (Synchronize=false では保存されない、要 cache プロパティ追加)
  - `onDataReceived` は MainThread 発火で確定

## 次にやること

### 優先度: 高
- **`backup/before-vrm-drop` ブランチ削除**: VRM 履歴除去時の保険、動作確認済で不要 (`git branch -D backup/before-vrm-drop`)
- **`origin/main` へ push**: 本セッションの成果 (60+ commit) を remote に反映

### 優先度: 中
- **EVMC4U ローカルパッチを `.patch` 化**: `Assets/EVMC4U/ExternalReceiver.cs` の 3 箇所パッチ (header L1 + accessors L391-424 + inline L848-878) を `.kiro/specs/mocap-vmc/evmc4u.patch` に artifact 化 (validate-impl R-6)

### 優先度: 低
- **`_dirty` 判定を write-counter 方式に変更**: 現行は `Count + Σ(x+y+z+w)` hash 近似で near-identity pose の衝突リスク (validate-impl R-1 / R-4)。`ExternalReceiver` に `uint WriteCounter` 追加で解決
- **PlayMode テストの動的 port 割当**: 現行は静的 port (`TestPort = 49502` 等) で socket 残留リスク (R-5)
- **`VMCMoCapSourceConfig.cs` の XML doc 要件 ID 表記整理**: 旧 `3-3` 記法を現行 `3.x` 記法に統一 (Finding 3)
- **Samples: `SlotManagementDemo.unity` が VRM 1.x でも動くことの検証** (別 spec 化の判断含む)

## 関連ファイル

### Spec (今日)
- `.kiro/specs/mocap-vmc/{requirements,design,tasks,spec.json}.md` — 全面再生成
- `.kiro/specs/_shared/contracts.md` — §13.1 MainThread 訂正

### 実装 (今日、新規)
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/EVMC4UMoCapSource.cs` — Adapter 本体
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/EVMC4USharedReceiver.cs` — process-wide singleton
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs` — Factory を `EVMC4UMoCapSource` 生成に差替
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/AssemblyInfo.cs` — `InternalsVisibleTo` (復元)
- `Assets/EVMC4U/ExternalReceiver.cs` — ローカルパッチ 4 種 + header marker (L1, L391-424, L848-878)

### 実装 (今日、維持)
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/VMCMoCapSourceConfig.cs`
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.asmdef`

### テスト (今日)
- `Tests/EditMode/mocap-vmc/{EVMC4UMoCapSourceTests,EVMC4USharedReceiverTests,ExternalReceiverPatchTests,VmcConfigCastTests,VmcFactoryRegistrationTests}.cs`
- `Tests/PlayMode/mocap-vmc/{EVMC4UMoCapSourceIntegrationTests,EVMC4UMoCapSourceSharingTests,SampleSceneSmokeTests}.cs`

### 削除 (今日、履歴に残る)
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/{VmcMoCapSource,Internal/Vmc*}.cs` — 旧自前実装
- `Tests/EditMode|PlayMode/mocap-vmc/Vmc*Tests.cs`, `UdpOscSenderTestDouble.cs` — 旧自前実装テスト
- `Packages/com.hidano.realtimeavatarcontroller/Editor/{Avatar/Builtin,Motion}/` — 空 skeleton asmdef フォルダ

### 削除 (今日、一時ファイル)
- `_impl_log_{7.3,9.1,12.2,15.4,15.5,mp_4-1,apb_T-1-3,apb_T-6-6,ui_T7-1}.txt`
- `remove_gitkeep.sh`, `remove_playmode_gitkeep.sh`, `cleanup_temp.sh`, `t12-2.diff`
