# Brief: avatar-provider-builtin

## Spec 責務

プロジェクトビルトインで配置されたアバターを Slot に供給する `IAvatarProvider` の具象実装を提供する。Addressable Asset System 対応のための拡張余地を設計上担保する。

Wave A 確定方針として、Descriptor / Registry / Factory モデルを採用する。`AvatarProviderDescriptor` に基づいて `IProviderRegistry` が `BuiltinAvatarProviderFactory` を解決し、`BuiltinAvatarProvider` を生成する。

dig ラウンド 3 追加確定事項:
- **属性ベース自己登録**: `BuiltinAvatarProviderFactory` は `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` でランタイム、`[UnityEditor.InitializeOnLoadMethod]` でエディタの各起動タイミングに `RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory())` を自己実行する。Editor 用コードは `RealtimeAvatarController.Avatar.Builtin.Editor` asmdef または `#if UNITY_EDITOR` ガードに配置する。同 typeId 競合時は例外スロー (上書き禁止)。
- **ErrorChannel 連携**: Factory キャスト失敗・Instantiate 失敗等は `ISlotErrorChannel` に `SlotErrorCategory.InitFailure` で発行後、例外を上位 `SlotManager` に伝播する。例外捕捉と Slot 状態遷移 (`Disposed`) は `slot-core` の責務。

dig ラウンド 4 追加確定事項:
- **BuiltinAvatarProviderConfig のランタイム動的生成**: `BuiltinAvatarProviderConfig` は `ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` によるランタイム動的生成を公式サポートする。`avatarPrefab` 等のフィールドは `public` で直接セット可能。Factory は SO アセット経由・ランタイム動的生成のいずれの Config でも同一経路で処理できる (contracts.md 1.5 章のシナリオ X / Y 両方に対応)。
- **テスト asmdef 2 系統**: EditMode / PlayMode 両系統のテスト asmdef を用意する。命名は `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` (キャスト / Resolve / Release / 自己登録確認) および `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` (Prefab インスタンス化・破棄・Disposed 遷移)。初期版のカバレッジ数値目標は設定しない。

## 依存

`slot-core` (Wave 1 完了後に起動)

## スコープ

### 実装する

- `IAvatarProvider` のビルトインアバター供給実装 (`BuiltinAvatarProvider`)
- `IAvatarProviderFactory` のビルトイン実装 (`BuiltinAvatarProviderFactory`)
- `IProviderRegistry` への `typeId="Builtin"` の Factory 登録 (起動時)
- **`BuiltinAvatarProviderConfig : ProviderConfigBase` の型定義** (contracts.md 1.5 章の `ProviderConfigBase` を継承する具象 Config 型; `avatarPrefab` フィールド (GameObject) を保持)
- **`BuiltinAvatarProviderFactory` のキャスト責務**: `IAvatarProviderFactory.Create(ProviderConfigBase config)` 引数を `BuiltinAvatarProviderConfig` にキャストし、失敗時は `ArgumentException` をスロー
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
- `.kiro/specs/_shared/contracts.md` (特に 1.4 章: ProviderRegistry / SourceRegistry 契約、1.5 章: Config 基底型階層、3 章: アバター供給抽象)

## 契約ドキュメントへの寄与

なし (1.4 章・3 章は slot-core が埋める)。本 Spec の具象要件が 1.4 章・3 章と矛盾しないことを確認する。

## 出力物

- `.kiro/specs/avatar-provider-builtin/requirements.md`
- `.kiro/specs/avatar-provider-builtin/spec.json`

## 言語

Markdown 出力は日本語
