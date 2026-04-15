# Brief: avatar-provider-builtin

## Spec 責務

プロジェクトビルトインで配置されたアバターを Slot に供給する `IAvatarProvider` の具象実装を提供する。Addressable Asset System 対応のための拡張余地を設計上担保する。

Wave A 確定方針として、Descriptor / Registry / Factory モデルを採用する。`AvatarProviderDescriptor` に基づいて `IProviderRegistry` が `BuiltinAvatarProviderFactory` を解決し、`BuiltinAvatarProvider` を生成する。

## 依存

`slot-core` (Wave 1 完了後に起動)

## スコープ

### 実装する

- `IAvatarProvider` のビルトインアバター供給実装 (`BuiltinAvatarProvider`)
- `IAvatarProviderFactory` のビルトイン実装 (`BuiltinAvatarProviderFactory`)
- `IProviderRegistry` への `typeId="Builtin"` の Factory 登録 (起動時)
- `BuiltinAvatarProviderConfig` の型定義 (`AvatarProviderDescriptor.Config` の具象設定型; Prefab 参照を保持)
- Prefab 形式のアバター指定 → Scene 上へのインスタンス化
- アバターのライフサイクル管理 (生成・破棄)。1 Slot 1 インスタンス原則、参照共有なし
- Slot との紐付け (`IProviderRegistry.Resolve()` 経由)
- Addressable Provider 追加時に本実装を変更せずに済む抽象遵守

### スコープ外

- `IAvatarProvider` / `IProviderRegistry` / `IAvatarProviderFactory` / `AvatarProviderDescriptor` の抽象定義 (`slot-core` Spec)
- Addressable Provider の具象実装 (初期段階では実装しない)
- アバターのモーション適用 (`motion-pipeline` Spec)
- `IMoCapSource` の参照共有モデルの適用 (Avatar Provider では採用しない)

## 参照必須ドキュメント

- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (特に 1.4 章: ProviderRegistry / SourceRegistry 契約、3 章: アバター供給抽象)

## 契約ドキュメントへの寄与

なし (1.4 章・3 章は slot-core が埋める)。本 Spec の具象要件が 1.4 章・3 章と矛盾しないことを確認する。

## 出力物

- `.kiro/specs/avatar-provider-builtin/requirements.md`
- `.kiro/specs/avatar-provider-builtin/spec.json`

## 言語

Markdown 出力は日本語
