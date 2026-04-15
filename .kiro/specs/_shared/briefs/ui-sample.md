# Brief: ui-sample

## Spec 責務
機能部 API を実利用するサンプル UI を提供する。UPM の `Samples~` 機構で配布する想定。

## 依存
`project-foundation`, `slot-core`, `motion-pipeline`, `mocap-vmc`, `avatar-provider-builtin` (全 Spec に依存)

## スコープ

### 実装する
- Slot の追加・削除・設定を操作する UI
- 各 Slot のアバター選択・MoCap ソース設定・Weight 操作
- UI 経由での挙動確認用シーン (参照共有シナリオを含む)
- Samples~ ディレクトリ配下への配置

### スコープ外
- 機能部 API の新規定義 (各機能 Spec で完結している前提)
- 機能部への UI 層依存の持ち込み (機能部は UI 非依存を厳守)

## 設計上の重要事項 (dig ラウンド 4 反映)

### テストアセンブリ定義 (dig ラウンド 4 確定: 任意)

- ui-sample のテスト asmdef は**任意**。作成有無はプロジェクト運用判断とし、design/tasks フェーズで決定する。
- **作成する場合の命名**:
  - EditMode: `RealtimeAvatarController.Samples.UI.Tests.EditMode`
  - PlayMode: `RealtimeAvatarController.Samples.UI.Tests.PlayMode`
- EditMode 対象 (任意): Inspector ドロップダウン表示確認、Fallback UI 設定の反映確認 (EditorAssembly 参照可)
- PlayMode 対象 (任意): デモシーン起動確認、参照共有デモシナリオの再現確認
- カバレッジ目標は初期版では設定しない。

### ランタイム動的生成 SlotSettings への対応 (dig ラウンド 4 確定: オプション)

- ui-sample の主眼は Editor Inspector 上での編集体験 (シナリオ X) であり、`ScriptableObject.CreateInstance<SlotSettings>()` 経由のランタイム動的生成 (シナリオ Y) への UI 対応は**オプション**扱い。
- contracts.md 1.1 章の確定事項として `SlotSettings` のランタイム動的生成は公式に許容済み。UI がこれを妨げないことを最低条件とする。
- 動的生成 Slot の編集 UI 等の具体的な対応は design フェーズで実装有無を判断する。

## 設計上の重要事項 (dig ラウンド 1・2・3 反映)

### Editor Inspector 主眼方針 (dig ラウンド 3 確定)

- **本 UI サンプルが主要に提供するのは Unity Editor Inspector 上での編集体験である。**
- スタンドアロンビルド (Player) 向けの実行時 GUI 提供は本サンプルのスコープ外。VTuber システム側が独自に実装する。
- Editor 起動時に `[InitializeOnLoadMethod]` で Factory が `RegistryLocator` に自動登録されることを前提とし、Inspector の `providerTypeId` / `sourceTypeId` ドロップダウンは `RegistryLocator.ProviderRegistry.GetRegisteredTypeIds()` / `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` から動的列挙する。
- Editor / Runtime で同一 Registry を参照共有する。

### Fallback 設定 UI (dig ラウンド 3 確定)

- `SlotSettings.fallbackBehavior` (型: `FallbackBehavior` enum) を Inspector UI の enum ドロップダウンとして提供する。
- 選択肢: `HoldLastPose`（最後のポーズを保持）/ `TPose`（T ポーズに戻す）/ `Hide`（アバターを非表示にする）
- デフォルト値は `HoldLastPose`。

### エラー表示 UI (dig ラウンド 3 確定)

- `ISlotErrorChannel.Errors` (UniRx `IObservable<SlotError>`) を `.ObserveOnMainThread().Subscribe()` で購読し、エラーを UI に表示する。
- Core 側 (`SlotManager`) では同一 `(SlotId, Category)` につき初回 1 フレームのみ `Debug.LogError` を抑制済み。
- UI 側での独自フィルタリング (カテゴリ別・最新 N 件リング・トースト通知等) を要件として許容する。具体的な表示方式は design フェーズで確定。

### デモシーン拡張 (dig ラウンド 3 確定)

- エラー発生シミュレーションシナリオを追加: VMC 切断シミュレーション・初期化失敗シミュレーション。
- Fallback 挙動の視覚確認シナリオを追加: `HoldLastPose` / `TPose` / `Hide` の各選択肢をシーン内で切り替え、エラー発生時の動作を視覚確認できる。

## 設計上の重要事項 (dig ラウンド 1・2 反映)

### ライブラリ採用 (dig ラウンド 2 確定)
- **UniRx (`com.neuecc.unirx`) を採用。R3 は採用しない。**
- `IMoCapSource.MotionStream` は UniRx `Subject<MotionFrame>` で実装された `IObservable<MotionFrame>`
- UI 層での購読は `.ObserveOnMainThread()` (UniRx 拡張メソッド) を使用する

### Weight の初期版方針 (dig ラウンド 2 確定)
- 初期版 Weight は `1.0` (full apply) / `0.0` (skip) の**二値のみ**
- UI はスライダーではなく**チェックボックスまたはトグルスイッチ**として実装する
- 中間値スライダーは初期版では提供しない。将来の複数ソース混合シナリオで再検討

### Config アセット参照 UI (dig ラウンド 2 確定)
- `AvatarProviderDescriptor.Config` は `ProviderConfigBase` 派生 SO アセットをドラッグ&ドロップで参照設定
- `MoCapSourceDescriptor.Config` は `MoCapSourceConfigBase` 派生 SO アセットをドラッグ&ドロップで参照設定
- Inspector 表示型は基底型 (`ProviderConfigBase` / `MoCapSourceConfigBase`) を使用する

### Registry 経由の動的候補列挙
- アバター Provider 候補は `IProviderRegistry.GetRegisteredTypeIds()` から動的に取得する
- MoCap ソース候補は `IMoCapSourceRegistry.GetRegisteredTypeIds()` から動的に取得する
- 選択肢をハードコードすることは禁止

### Descriptor ベースの設定反映
- アバター選択結果は `AvatarProviderDescriptor` (typeId + Config) として `SlotSettings.avatarProviderDescriptor` に反映する
- MoCap ソース設定結果は `MoCapSourceDescriptor` (typeId + Config) として `SlotSettings.moCapSourceDescriptor` に反映する

### IMoCapSource 参照共有
- `IMoCapSource` は Push 型 (UniRx `IObservable<MotionFrame>`)
- 複数 Slot が同一 MoCap ソースを参照共有可能。UI 側では同一 `sourceTypeId` + 同一 Config を複数 Slot に設定することで参照共有を許容する
- ライフサイクル管理は `IMoCapSourceRegistry` が担う。Slot 側 / UI 側から直接 `Dispose()` を呼ばない

### 参照共有デモシナリオ
- デモシーンに「1 VMC ソースを複数 Slot で共有」するシナリオを含める

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (全章、特に 1 章・1.4 章・2.1 章)
- `.kiro/specs/project-foundation/requirements.md` (asmdef 分割 / Samples~ 配置方針)

## 契約ドキュメントへの寄与
なし。本 Spec は契約の消費側。

## 出力物
- `.kiro/specs/ui-sample/requirements.md`
- `.kiro/specs/ui-sample/spec.json`

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `ui-sample` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定

## 備考
- UI フレームワーク (UGUI / UI Toolkit) は要件段階で提案し、design フェーズで確定
- 動的候補列挙 UI (Registry 列挙結果のドロップダウン等) は UGUI・UI Toolkit いずれでも実現可能
- UniRx (`com.neuecc.unirx`) による `MotionStream` 購読プレビューはオプション機能として要件化済み。R3 は採用しない
- 運用の本命は別 VTuber システムへの組込みであり、本サンプルは検証デモの位置付け

## 言語
Markdown 出力は日本語
