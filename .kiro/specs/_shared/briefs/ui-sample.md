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

## 設計上の重要事項 (dig ラウンド 1 反映)

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
- UniRx による `MotionStream` 購読プレビューはオプション機能として要件化済み
- 運用の本命は別 VTuber システムへの組込みであり、本サンプルは検証デモの位置付け

## 言語
Markdown 出力は日本語
