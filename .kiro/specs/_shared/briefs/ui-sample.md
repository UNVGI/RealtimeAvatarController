# Brief: ui-sample

## Spec 責務
機能部 API を実利用するサンプル UI を提供する。UPM の `Samples~` 機構で配布する想定。

## 依存
`project-foundation`, `slot-core`, `motion-pipeline`, `mocap-vmc`, `avatar-provider-builtin` (全 Spec に依存)

## スコープ

### 実装する
- Slot の追加・削除・設定を操作する UI
- 各 Slot のアバター選択・MoCap ソース設定・Weight 操作
- UI 経由での挙動確認用シーン
- Samples~ ディレクトリ配下への配置

### スコープ外
- 機能部 API の新規定義 (各機能 Spec で完結している前提)
- 機能部への UI 層依存の持ち込み (機能部は UI 非依存を厳守)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (全章)
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
- 運用の本命は別 VTuber システムへの組込みであり、本サンプルは検証デモの位置付け

## 言語
Markdown 出力は日本語
