# Brief: avatar-provider-builtin

## Spec 責務
プロジェクトビルトインで配置されたアバターを Slot に供給する `IAvatarProvider` の具象実装を提供する。Addressable Asset System 対応のための拡張余地を設計上担保する。

## 依存
`slot-core` (Wave 1 完了後に起動)

## スコープ

### 実装する
- `IAvatarProvider` のビルトインアバター供給実装
- Prefab 形式のアバター指定 → Scene 上へのインスタンス化
- アバターのライフサイクル管理 (生成・破棄)
- Slot との紐付け
- Addressable Provider 追加時に本実装を変更せずに済む抽象遵守

### スコープ外
- `IAvatarProvider` 抽象自体の定義 (`slot-core` Spec)
- Addressable Provider の具象実装 (初期段階では実装しない)
- アバターのモーション適用 (`motion-pipeline` Spec)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (特に 3 章: アバター供給抽象)

## 契約ドキュメントへの寄与
なし (3 章は slot-core が埋める)。本 Spec の具象要件が 3 章と矛盾しないことを確認する。

## 出力物
- `.kiro/specs/avatar-provider-builtin/requirements.md>`
- `.kiro/specs/avatar-provider-builtin/spec.json`

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `avatar-provider-builtin` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定

## 言語
Markdown 出力は日本語
