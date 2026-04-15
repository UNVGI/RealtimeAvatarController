# Brief: motion-pipeline

## Spec 責務
MoCap ソースから受信したモーションデータを内部キャッシュに保持し、Weight 値に従ってアバターへ適用するパイプラインを提供する。

## 依存
`slot-core` (Wave 1 完了後に起動)

## スコープ

### 実装する
- モーションデータの中立表現 (Humanoid 骨格ベース / HumanPose 相当)
- Slot 単位の内部キャッシュ機構 (`MotionCache`): `IMoCapSource.MotionStream` を Push 型で購読し、最新フレームを保持する
  - 各 Slot の `MotionCache` は独立したインスタンスを持つ (同一 `IMoCapSource` を複数 Slot が参照共有しても、キャッシュは Slot ごとに独立)
  - 購読解除 (`IDisposable.Dispose()`) のみが motion-pipeline の責務; `IMoCapSource.Dispose()` は `MoCapSourceRegistry` が制御する
- Weight に従ったアバターへの適用処理 (初期版有効値: `{0.0 (skip), 1.0 (full apply)}` の二値のみ; 中間値セマンティクスは将来の複数ソース混合シナリオ導入時に定義)
- Humanoid アバターへの適用層 (`LateUpdate` タイミングでアバターに適用)
- ランタイム中のシームレスな MoCap 切替 / アバター切替の実現
- Generic 形式への拡張余地 (抽象のみ)
- 1 アクター多アバター / 多アクター 1 アバター等の応用シナリオの許容

### dig ラウンド 2 確定事項
- **UniRx 採用**: リアクティブライブラリは UniRx (`com.neuecc.unirx`) を採用。R3 は不採用。`IObservable<MotionFrame>` 型シグネチャは変更なし (UniRx `Subject<T>` は `System.IObservable<T>` を実装)。`ObserveOnMainThread()` 等は UniRx 拡張メソッド経由で利用
- **Weight 二値方針**: 初期版有効値は `{0.0, 1.0}` の二値のみ。`SlotSettings.weight` フィールドは将来フックとして残す。中間値 (`0.0 < w < 1.0`) の挙動は複数ソース混合シナリオ導入時に定義

### dig ラウンド 3 確定事項
- **Fallback 挙動対応**: Applier (HumanoidMotionApplier) はエラー発生時に `SlotSettings.fallbackBehavior` を参照して挙動を分岐する。`FallbackBehavior` enum は slot-core 定義 (`RealtimeAvatarController.Core`)、motion-pipeline は参照のみ。`HoldLastPose` (デフォルト) / `TPose` (HumanPoseHandler で初期 Pose 書き戻し) / `Hide` (Renderer 無効化、GameObject 生存維持) の 3 値を処理する。`Hide` からの復帰は次フレームの正常 Apply 完了時に Renderer を再有効化する
- **ISlotErrorChannel 連携**: Applier の実行時例外は `ISlotErrorChannel` に `SlotErrorCategory.ApplyFailure` カテゴリで発行する。`ISlotErrorChannel` は `RealtimeAvatarController.Core` 提供、motion-pipeline は Core 経由で間接利用。`Debug.LogError` 抑制は ISlotErrorChannel 側が管理するため motion-pipeline は push のみ行う
- **無効フレームとエラーの分離**: MotionCache からの null/無効フレームは通常動作 (スキップ・前フレーム維持) であり ISlotErrorChannel に発行しない。ISlotErrorChannel への ApplyFailure 発行対象は Applier 呼び出し中の実行時例外のみ

### スコープ外
- Generic 形式の具象実装
- MoCap ソース具象実装 (mocap-vmc Spec)
- アバター供給処理 (avatar-provider-builtin Spec)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (特に 2.2 章: モーションデータ中立表現 — 本エージェントが埋める)

## 契約ドキュメントへの寄与
- `contracts.md` の **2.2 章 (モーションデータ中立表現)** を埋める責務を持つ
- slot-core と整合する表現を選定すること

## 出力物
- `.kiro/specs/motion-pipeline/requirements.md`
- `.kiro/specs/motion-pipeline/spec.json`
- `.kiro/specs/_shared/contracts.md` (2.2 章を編集)

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `motion-pipeline` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定
4. `contracts.md` の 2.2 章を編集

## 言語
Markdown 出力は日本語
