# Brief: project-foundation

## Spec 責務
Unity プロジェクト基盤と UPM パッケージ基盤を整備する。

## 依存
なし (Wave 2 並列波の先頭)

## スコープ

### 実装する
- Unity 6000.3.10f1 プロジェクトをリポジトリ直下 `RealtimeAvatarController/` に作成
- UPM パッケージ雛形 (`package.json` を含む構造)
  - `package.json` の `dependencies` に `com.neuecc.unirx` (最新安定版) を宣言
- アセンブリ定義ファイル (asmdef) の分割: 機能部 / UI サンプル
  - `RealtimeAvatarController.Core` asmdef に UniRx (`UniRx`) の参照を追加
  - 他の機能部 asmdef は Core 経由で UniRx を間接利用 (直接参照禁止)
- `Samples~` 機構による UI サンプルの同梱ルート
- 最小限の CI / ビルド検証の下地 (任意)
- 名前空間規約の確定
- 利用者向け README への OpenUPM scoped registry 追加手順の記載 (manifest.json スニペット含む)

### スコープ外
- 具体的なクラス実装 (各機能 Spec 側で行う)
- UI サンプルの中身 (ui-sample Spec)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (特に 6 章: アセンブリ / 名前空間境界)

## 契約ドキュメントへの寄与
- `contracts.md` の **6.1 asmdef 構成** と **6.2 名前空間規約** を埋める責務を持つ
- Wave 1 の slot-core エージェントが先行で contracts.md の 1〜5 章を埋めた後、本エージェントは 6 章を埋める

## 出力物
- `.kiro/specs/project-foundation/requirements.md`
- `.kiro/specs/project-foundation/spec.json` (kiro 標準)

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `project-foundation` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定
4. `contracts.md` の 6 章を編集

## 言語
Markdown 出力は日本語 (プロジェクトの `spec.json.language` に従う)
