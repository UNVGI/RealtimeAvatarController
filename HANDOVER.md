# Handover (2026-04-22)

## 今回やったこと

### 1. UI Sample の Unity 実行時不具合を修復
- Package Manager → UI Sample Import 後、複数の実装欠陥を修復:
  - `RealtimeAvatarController.Samples.UI.asmdef` に UniTask 参照欠落 → 追加
  - `SlotManagementDemo.unity` で EventSystem と FallbackSwitchGroup の fileID (`&100240000` `&100240001`) が重複していたため EventSystem 側を `&100410xxx` 帯へリネーム
  - Scene 上で `SlotListScrollView` が `AddSlotButton` を覆っていたため RectTransform の `Pos Y` / `Height` を調整
  - `SlotListItemUI` に `IPointerClickHandler` が未実装で行クリック→Detail 反映の経路が欠落 → 実装追加 + `SlotManagementPanelUI` に `detailPanel` 参照追加
  - `DisplayNameField` / `WeightToggle` / `FallbackDropdown` が子要素 (Text/Placeholder/Background/Checkmark 等) を持たない欠陥状態だったため MCP の `Unity_RunCommand` 経由で `DefaultControls.CreateInputField / CreateToggle / CreateDropdown` で再生成 + 参照差し替え
  - `VmcOscAdapter.uOscServer` の hideFlags を `HideAndDontSave` → `DontSave` に変更しデバッグ時 Inspector で確認可能に

### 2. アバター Applier 結線の新規実装 (Spec 実装漏れの補完)
- `ui-sample` の `SlotManagerBehaviour` には VMC → アバターへの結線が完全に欠落していた
- `SlotManager` に `TryGetSlotResources(slotId, out source, out avatar)` public API 追加、`ApplyWithFallback` を public 化
- `SlotManagerBehaviour` に `LateUpdate` を追加、Slot ごとに `MotionCache` + `HumanoidMotionApplier` をビルドして `ApplyWithFallback` 経由で毎フレーム Apply

### 3. M-3 合意変更 (contracts.md §2.2)
- 既存 `VmcFrameBuilder.WriteBoneMuscles` は `Quaternion.eulerAngles / 180f` で muscle を作る破綻実装だったため、`HumanoidMotionFrame` に `IReadOnlyDictionary<HumanBodyBones, Quaternion> BoneLocalRotations` を追加
- 前半方針 (Applier が `Transform → GetHumanPose → SetHumanPose` で再構築): Humanoid rig constraint による近似誤差でボーン姿勢がズレ、GetHumanPose 内部キャッシュで数十秒に 1 回しか更新されない事象が出たため撤回
- 撤回後の方針: `Animator.GetBoneTransform(bone).localRotation` 直接書込 (EVMC4U と同方式)

### 4. 1 bundle ≠ 1 frame 問題の発見と修正 (受信 / 適用の分離モデル)
- `VmcOscAdapter.OnDataReceived` で per-message `TryFlush` を呼んでいたため、VMC bundle 内の個別 bone メッセージごとに 1 bone しか含まない `HumanoidMotionFrame` が発行されていた (MCP 診断ログで `boneRotations count=1` を確認)
- 調査結果: **VMC Protocol 仕様上、OSC message stream から frame 境界を検知する手段は定義されていない** (`/VMC/Ext/OK` は frame 終端ではなくモデル読込 status、1 frame = 1 bundle = 1 UDP packet の保証もなし。MTU 1500 byte 制約で分割されうる)
- **uOSC の `onDataReceived` は Unity MainThread で Invoke される** (`uOscServer.Update` が parser queue から dequeue する実装のため) → 過去 Spec が「ワーカースレッド」と書いていたのは事実誤認
- EVMC4U 準拠の「受信即キャッシュ、MainThread Tick で flush」の分離モデルに刷新
- `VmcTickDriver` (MonoBehaviour) を新設、LateUpdate で `VmcOscAdapter.Tick` を呼ぶ
- `VmcFrameBuilder.TryFlush` から `_bones.Clear()` 廃止 + `_dirty` flag 追加 (欠損 bone は前回値維持、無駄 OnNext 抑制)

### 5. Avatar root への RootRotation 書込を撤回
- 両手は合うが腰/脊椎/左足の軸がズレる症状が継続
- 原因: `avatar root.localRotation` に VMC RootRotation を書くと、Hips.localRotation と二重回転になり下半身姿勢が破綻
- EVMC4U もデフォルトで Root Transform 書込は無効 (Inspector option)
- 経路 A から avatar root への position/rotation 書込を削除

### 6. 上記 5 までやっても VMC 受信 rotation の座標系ズレが残る
- 「X 軸と Y 軸が入れ替わって見える」旨のユーザー観察
- 自前の `VmcFrameBuilder.SetBone` / `VmcMessageRouter.Route` / `VmcOscAdapter` の実装群は **VMC Protocol 仕様の座標系規定を正確に実装できている根拠が薄い**
- → 自前実装を諦め、準公式の **EVMC4U (`gpsnmeajp/EasyVirtualMotionCaptureForUnity`, MIT)** に置き換える方針で合意

## 決定事項

- `HumanoidMotionFrame.BoneLocalRotations` フィールドは contracts.md §2.2 で永続化 (M-3 合意変更)
- VMC → アバター適用経路は Transform 直接書込方式で確定 (Muscle pipeline 経由は撤回)
- VMC 受信と frame 発行は分離モデル (OnDataReceived で蓄積、LateUpdate Tick で flush)
- Avatar root (Animator.transform) への Root 書込は初期版では行わない
- **mocap-vmc の実装は次セッションで EVMC4U ベースに全面置換**

## 捨てた選択肢と理由

- **Muscle 変換 (Option B)**: `HumanPoseHandler.GetHumanPose` が Humanoid rig constraint で muscle を近似するため、VMC の parent-local rotation を正確に復元できない。副作用で GetHumanPose 内部キャッシュにより更新遅延
- **per-message TryFlush**: 1 bone ずつの HumanoidMotionFrame が発行され全身同期しない。VMC bundle と Unity LateUpdate を 1:1 対応させる前提自体が仕様的に誤り
- **uOSC Udp.cs の IPv6 dual-stack 維持**: Windows 一部環境で IPv4 配信が来ないため IPv4 bind に修正したが、`Library/PackageCache` 修正で永続性なし。EVMC4U が依存する uOSC の挙動に委ねる方針へ
- **自前で OSC パース + FrameBuilder + Applier を持つ**: 座標系変換の根拠が確立できず、VMagicMirror / VSeeFace との互換検証が不十分。EVMC4U は数年の運用実績と各送信アプリ対応の検証済み

## ハマりどころ

- **Unity Humanoid の Muscle system は精度限定**: GetHumanPose / SetHumanPose の往復で誤差が出るため VMC 再現には不向き
- **VMC Protocol 仕様に frame boundary signal は無い**: OSC bundle ≠ frame、`/VMC/Ext/OK` は frame 終端ではない
- **uOSC の `onDataReceived` は Main Thread で Invoke される**: 過去の mocap-vmc design §13.1 は誤り
- **VMC Root/Pos は多くの送信アプリで Hips と二重**: Root 書込デフォルト有効は破綻を生む
- **Windows Loopback + Wireshark (npcap) の相互作用**: WireShark 起動中は OS → Unity socket への loopback 配信が阻害されるケースあり。診断時は WireShark 閉じること
- **MCP の UNEXPECTED_ERROR**: `BindingFlags.NonPublic` を伴う Reflection で安定しない。診断スクリプトは public API 経由で書く
- **PackageCache への直接修正は永続性なし**: Unity が package を re-fetch すると消える。一時修正の位置づけと割り切る

## 学び

- VMC 関連の成熟した実装 (EVMC4U) が存在する領域では、自前実装は時間の浪費になる。要件 (blending / retargeting 等) が合えば積極的に採用する方針
- Protocol 仕様は第一次ソース (spec.md / 公式 wiki) で読む。仕様書が明示していない挙動を「暗黙ルール」で想像しない
- Unity Humanoid Avatar の Muscle system / Animator.GetBoneTransform / HumanPoseHandler の挙動を、実装前に必ず検証する (今回は理解不足のまま進めて 2 度の方針撤回に至った)
- 診断ログ (Debug.Log) 仕込みは仮説検証の近道。早いうちに実機ログを出すべきだった (boneRotations count=1 で 1 発で解決する問題を憶測で迷走した)

## 次にやること

### 最優先: EVMC4U への全面置換

**背景**: VMC Protocol 対応の準公式 Unity ライブラリ。VSeeFace / VMagicMirror / VirtualMotionCapture など主要送信アプリとの互換性が数年にわたり検証済み。

**置換スコープ**:
- `Runtime/MoCap/VMC/` 配下の自前実装を廃止:
  - `VmcMoCapSource.cs` / `VMCMoCapSourceFactory.cs` / `VMCMoCapSourceConfig.cs`
  - `Internal/VmcOscAdapter.cs` / `VmcMessageRouter.cs` / `VmcFrameBuilder.cs` / `VmcBoneMapper.cs` / `VmcTickDriver.cs`
  - `AssemblyInfo.cs` (InternalsVisibleTo)
- EVMC4U の `ExternalReceiver` を Avatar GameObject にアタッチし、VMC 受信とアバター適用を全て EVMC4U に委譲

**設計上の検討事項** (次セッション開始時に必ず議論):
1. **`IMoCapSource` 抽象との整合**
   - EVMC4U は Transform 直接書込で動くため、`IObservable<MotionFrame>` 契約を満たさない
   - 選択肢 A: EVMC4U の受信 Dictionary を定期的に読んで `HumanoidMotionFrame` に変換する薄い Adapter を書く (既存 slot-core / motion-pipeline 契約を維持)
   - 選択肢 B: Slot の Applier 層を EVMC4U 直結に変え、MotionFrame を介さない経路を追加 (契約変更あり)
2. **Slot 参照共有モデル (MoCapSourceRegistry) との相性**
   - EVMC4U は ExternalReceiver を 1 つの Avatar に 1 つ想定。複数 Slot で同一 port を共有するユースケースは EVMC4U 標準に無い
   - 複数 Slot が同一ポートで動く場合、`ExternalReceiver` を 1 個立てて複数 Avatar に適用するか、UDP packet 共有の仕組みを別途用意
3. **VMCMoCapSourceConfig 相当の設定を EVMC4U の Inspector プロパティで代替**
   - port / bindAddress 等を EVMC4U の `ExternalReceiver` 側の公開 field で管理
4. **契約 Spec の書き換え**
   - `mocap-vmc/design.md` と `requirements.md` を「EVMC4U パッケージに依存する wrapper 実装」として書き直す
   - contracts.md の `HumanoidMotionFrame.BoneLocalRotations` はそのまま残す (motion-pipeline の Generic な財産として)

**パッケージ導入手順 (概要)**:
- `Packages/manifest.json` の `dependencies` に EVMC4U を git URL か OpenUPM 経由で追加
- EVMC4U は `com.github.gpsnmeajp.easyvirtualmotioncaptureforunity` として npm / git から入手可能
- 依存する uOSC は EVMC4U 側が同梱 or 同一 `com.hidano.uosc` で共有できるか要確認

### 並行タスク (優先度: 中〜低)

- 現行の `slot-core` の `VmcMoCapSource` 参照共有バグ (Initialize 二重呼び出し) を EVMC4U 採用後にどう扱うか再検討
- `ui-sample` の PlayMode 統合テスト (T16/T17) は `Samples~` 配下配置の問題で欠落。EVMC4U 導入後に Sample 経路を再整理

## 関連ファイル

### Spec 変更 (今日)
- `.kiro/specs/_shared/contracts.md` — §2.2 `HumanoidMotionFrame` に `BoneLocalRotations` 追加 (M-3 合意変更 + 実装方針の撤回記録)
- `.kiro/specs/motion-pipeline/design.md` — §3.4 / §4.1 / §4.6 / §7.1 / §7.1.1 / §7.2 更新
- `.kiro/specs/mocap-vmc/design.md` — §6.1 / §6.3 / §6.4 / §7.3 / §13.1 更新 (次セッションで全面書き換え予定)

### 実装変更 (今日)
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Frame/HumanoidMotionFrame.cs` — `BoneLocalRotations` プロパティ + 新コンストラクタ追加
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Applier/HumanoidMotionApplier.cs` — 経路 A (Transform 直接書込) 追加、`_animator` 参照保持
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/Core/Slot/SlotManager.cs` — `TryGetSlotResources` 追加、`ApplyWithFallback` を public 化
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcFrameBuilder.cs` — `WriteBoneMuscles` 削除、`_dirty` flag 追加、`_bones.Clear()` 廃止
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcOscAdapter.cs` — `OnDataReceived` から `TryFlush` 削除、`Tick` 追加、`VmcTickDriver` AddComponent
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcTickDriver.cs` — 新規作成 (LateUpdate 駆動)

### UI Sample 修復 (今日)
- `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/SlotManagerBehaviour.cs` — `LateUpdate` + パイプライン結線
- `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/SlotManagementPanelUI.cs` — `detailPanel` [SerializeField] 追加、SpawnItem で callback 登録
- `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/SlotListItemUI.cs` — `IPointerClickHandler` 実装
- `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/RealtimeAvatarController.Samples.UI.asmdef` — UniTask 参照追加
- `Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Scenes/SlotManagementDemo.unity` — 重複 fileID 解消、Scroll View レイアウト調整
- `Assets/Samples/Realtime Avatar Controller/0.1.0/UI Sample/` 配下 — 対応する imported copy に同じ変更を適用
- (MCP 経由の Scene 内再生成) DisplayNameField / WeightToggle / FallbackDropdown を DefaultControls で正しい子構造付きで生成、`SlotDetailPanelUI` の参照を更新

### テスト (今日)
- `Tests/EditMode/motion-pipeline/Frame/HumanoidMotionFrameTests.cs` — `BoneLocalRotations` 関連テスト 6 件追加
- `Tests/PlayMode/motion-pipeline/Applier/HumanoidMotionApplierIntegrationTests.cs` — BoneLocalRotations 経路テスト 3 件追加
- `Tests/PlayMode/mocap-vmc/VmcMoCapSourceIntegrationTests.cs` — Muscles 期待値を `BoneLocalRotations` 検証に置換

### 補助
- `CLAUDE.md` — Unity Editor 起動コマンド (CLI から Unity 再起動可能に) を追記
- `Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/Core/DotNet/Udp.cs` — IPv6 dual-stack → IPv4 only bind に一時修正 (PackageCache なので永続性なし / EVMC4U 採用時に再評価)
- `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/Internal/VmcOscAdapter.cs` — `hideFlags` を `DontSave` (Hierarchy 表示可能化)

## EVMC4U 採用に向けた調査メモ

- リポジトリ: https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity (MIT License)
- Wiki: https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity/wiki
- 主要コンポーネント: `ExternalReceiver.cs` (メインの MonoBehaviour)
- 受信 → 適用モデル: 受信時は Dictionary にキャッシュ (コード内コメント `//受信と更新のタイミングは切り離した`)、Update / LateUpdate で Transform 直接書込
- Muscle / HumanPoseHandler は未使用
- Root 書込は Inspector で Transform 指定時のみ、デフォルト無効
- uOSC バージョン: EVMC4U は特定バージョンの uOSC に依存 (現行プロジェクト `com.hidano.uosc 1.0.0` との衝突可能性あり、次セッションで確認)
