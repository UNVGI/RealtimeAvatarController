# Brief: slot-core

## Spec 責務
Slot 概念を中核とするデータモデル・ライフサイクル管理 API を提供し、Spec 間で共有する公開 IF 群を定義する。

## 依存
`project-foundation`

## 位置付け (重要)
**Wave 1 の先行波 Spec**。本 Spec の requirements エージェントは、自身の requirements.md 生成に加え、`_shared/contracts.md` の 1〜5 章を埋める責務を持つ。これが完了するまで他 5 Spec は Wave 2 で待機する。

## スコープ

### 実装する
- Slot データモデル (設定項目: アバター参照・MoCap ソース参照・Facial / LipSync 参照・Weight 等)
- SlotRegistry / SlotManager 相当の動的追加・削除 API
- Slot 設定のシリアライズ可能な構造 (ScriptableObject 等)
- Slot ライフサイクル (生成・破棄・リソース所有)
- 以下の抽象インターフェースの定義 (シグネチャ確定は design フェーズ):
  - `IMoCapSource`
  - `IAvatarProvider`
  - `IFacialController` (受け口のみ)
  - `ILipSyncSource` (受け口のみ)

### スコープ外
- 抽象の具象実装 (それぞれ mocap-vmc / avatar-provider-builtin / 対象外 / 対象外)
- モーション中立表現 (motion-pipeline Spec)
- モーション適用処理 (motion-pipeline Spec)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md`

## 契約ドキュメントへの寄与 (Wave 1 責務)
本エージェントは `contracts.md` の以下を埋める:
- 1 章: Slot データモデル
- 2.1 章: `IMoCapSource` シグネチャ
- 3.1 章: `IAvatarProvider` シグネチャ
- 4.1 章: `IFacialController` シグネチャ
- 5.1 章: `ILipSyncSource` シグネチャ

注: 2.2 章 (モーションデータ中立表現) は motion-pipeline エージェントが Wave 2 で埋める。

## 出力物
- `.kiro/specs/slot-core/requirements.md`
- `.kiro/specs/slot-core/spec.json`
- `.kiro/specs/_shared/contracts.md` (編集)

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `slot-core` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定
4. `contracts.md` の 1〜5 章を編集 (2.2 章は除く)

## 言語
Markdown 出力は日本語
