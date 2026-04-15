# Design Validation Report — project-foundation

**検証日時**: 2026-04-15  
**対象**: `.kiro/specs/project-foundation/design.md`  
**参照**: `.kiro/specs/project-foundation/requirements.md` / `.kiro/specs/_shared/contracts.md` (6章)  
**検証経路**: Skill `kiro:validate-design` は feature 名パース失敗のため手動検証 (ステップ B) に移行  

---

## 総合評価

| 評価 | 判定 |
|------|------|
| **GO / NO-GO** | **条件付き GO** |
| 重大欠陥 (Critical) | 0 件 |
| 軽微な欠陥 (Minor) | 3 件 |
| 推奨修正 (Recommendation) | 2 件 |

> design.md は全要件をカバーしており構造的に完成度が高い。指摘はいずれも minor/recommendation レベルであり、tasks 生成フェーズへの移行を阻害しない。

---

## 1. 要件トレーサビリティ

requirements.md の全要件について design.md 内での対応箇所を照合した結果を示す。

| Req# | 要件名 | design.md 対応章 | カバー状態 |
|------|--------|-----------------|-----------|
| Req 1 | Unity プロジェクトの作成と配置 | 第 1・2 章、第 9 章ツリー | ✅ 完全 |
| Req 2 | UPM パッケージ雛形 | 第 3 章 (3.1〜3.3) | ✅ 完全 |
| Req 3 | asmdef の構成 | 第 4 章 (4.1〜4.4) | ✅ 完全 |
| Req 4 | 名前空間規約の確定 | 第 5 章 (5.1〜5.4) | ✅ 完全 |
| Req 5 | Samples~ 機構 | 3.3 章・第 9 章ツリー | ✅ 完全 |
| Req 6 | 機能部と UI フレームワークの非依存性 | 4.4 章「禁止依存」 | ✅ 完全 |
| Req 7 | CI / ビルド検証の下地 (任意) | 第 8 章 | ✅ 完全 |
| Req 8 | UniRx UPM 依存の宣言 | 3.2 章 package.json、4.1.1 章 Core asmdef | ✅ 完全 |
| Req 9 | README への OpenUPM 手順記載 | 第 6 章 (6.1〜6.7) | ✅ 完全 |
| Req 10 | Domain Reload OFF 対応 | 第 7 章 (7.1〜7.4)、10.4 章 | ✅ 完全 |

**結論**: 全 10 要件が design.md でカバーされている。未対応の Req は存在しない。

---

## 2. contracts.md 6 章との整合性

contracts.md 6.1 章・6.2 章と design.md の記述を照合した。

### 2.1 Runtime asmdef 一覧 — ✅ 整合

contracts.md 6.1 章の Runtime asmdef 5 本と design.md 4.1 章の定義は名称・配置パス・担当 Spec が完全一致する。

### 2.2 Editor asmdef 一覧 — ✅ 整合

contracts.md 6.1 章の Editor asmdef 5 本と design.md 4.2 章の定義は名称・配置パス・依存ルールが一致する。

### 2.3 テスト asmdef 一覧 — ✅ 整合

contracts.md 6.1 章の Tests asmdef 10 本と design.md 4.3 章・10.2 章の定義は名称・配置パス・必須/任意区分が一致する。

### 2.4 名前空間規約 — ✅ 整合

contracts.md 6.2 章の名前空間マッピングと design.md 5.2〜5.4 章の記述は一致する。

### 2.5 外部ライブラリ依存確定値 — ✅ 整合

contracts.md 6.1 章末尾の確定値 (`com.neuecc.unirx: 7.1.0` / `com.cysharp.unitask: 2.5.10`) と design.md 3.2 章 package.json の dependencies 値が完全一致する。

---

## 3. UPM 配布仕様の完全性

### 3.1 package.json の dependencies 宣言 — ✅ 完全

design.md 3.2 章の `package.json` は以下を宣言している:

```json
"dependencies": {
  "com.neuecc.unirx": "7.1.0",
  "com.cysharp.unitask": "2.5.10"
}
```

requirements.md Req 2-6 / Req 8-1 の「dependencies フィールドに UniRx を宣言」を満たす。UniTask も contracts.md 確定値と一致する。

### 3.2 バージョン指定方式 — ⚠️ Minor 指摘 #1

design.md 3.2 章の注記で「バージョン固定 (exact version) を採用し再現性を優先する」と明記しているが、Unity の UPM で exact version 固定 (prefix なし) が確実に機能するかはランタイムの `package.json` 依存解決の挙動に依存する。

- **問題**: UPM は Semantic Versioning の互換バージョン範囲 (`1.x.x` など) をサポートするが、exact version 固定は upstream の patch アップデート (セキュリティ修正等) を自動取得できない。
- **推奨**: exact version の意図と patch アップデート適用方針をコメントとして tasks.md に引き継ぐ。design.md 自体の修正は不要だが、今後の維持コストを低減するため patch バージョン以上への更新ポリシーを明記することを推奨する。

### 3.3 OpenUPM scoped registry 手順 — ✅ 完全

design.md 6.2〜6.7 章で以下の手順がすべて網羅されている:

- `scopedRegistries` への `com.neuecc` および `com.cysharp` 両スコープの追加スニペット
- `dependencies` への追記形式スニペット (既存エントリ非破壊)
- `openupm-cli` コマンド例 (任意・参考)

Req 9-2 の要求するスニペット形式は完全に満たされている。

---

## 4. asmdef 構成の妥当性

### 4.1 Runtime asmdef の依存関係 — ✅ 妥当

片方向依存の遵守状況:

| asmdef | references | 循環 |
|--------|-----------|------|
| `Core` | UniRx, UniTask のみ | なし |
| `Motion` | Core のみ | なし |
| `MoCap.VMC` | Core, Motion | なし |
| `Avatar.Builtin` | Core のみ | なし |
| `Samples.UI` | Core, Motion, MoCap.VMC, Avatar.Builtin | なし |

循環依存なし。`Samples.UI` → 機能部の一方向依存も正しく設計されている。

### 4.2 UniRx / UniTask 参照の Core 集約 — ✅ 妥当

design.md 4.1.1 章の `Core` asmdef が `UniRx` と `UniTask` 両方を `references` に含む唯一の機能 asmdef として設計されている。`Motion`・`MoCap.VMC`・`Avatar.Builtin` は Core のみを参照し、UniRx/UniTask への直接依存を持たない。

contracts.md 6.1 章末尾の「二重依存禁止」ルールに準拠している。

### 4.3 Editor asmdef の片方向依存 — ✅ 妥当

各 Editor asmdef は `includePlatforms: ["Editor"]` を設定し、対応 Runtime asmdef のみを `references` に持つ。Runtime asmdef が Editor asmdef を参照する逆依存は発生しない。

design.md 4.4 章「禁止依存」にも明示されている。

### 4.4 テスト asmdef 設定 — ⚠️ Minor 指摘 #2

design.md 4.3 章の代表例 (Core.Tests.EditMode / Core.Tests.PlayMode) のみ具体的な JSON が示されており、残り 6 本 (Motion, MoCap.VMC, Avatar.Builtin の EditMode/PlayMode 計 6 本) の JSON 雛形は「他の機能 Spec のテスト asmdef も同一パターンで作成する」と委任されている。

- **問題**: tasks フェーズで各担当 Spec が個別に解釈する際に設定ミス (特に `optionalUnityReferences` の欠落) が生じるリスクがある。
- **推奨**: tasks.md に「各テスト asmdef は 4.3 章の代表例 JSON を雛形として使用し、`optionalUnityReferences: ["TestAssemblies"]` を省略しないこと」を明示するタスクを追加することを推奨する。

### 4.5 `RealtimeAvatarController.Samples.UI` の `autoReferenced` — ✅ 妥当

design.md 4.1.5 章で `autoReferenced: false` が設定されており、機能部ランタイムビルドへの UI サンプルコード混入が防止されている。

---

## 5. Domain Reload OFF 対応

### 5.1 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` の配置 — ✅ 適切

design.md 7.2 章で `RegistryLocator.ResetForTest()` に `SubsystemRegistration` タイミングが付与されており、初期化順序 (SubsystemRegistration → AfterAssembliesLoaded → Factory 自己登録) が正しく設計されている。

Req 10-2 の「同一 typeId の二重登録を防ぐ」要件を満たす。

### 5.2 テスト手順の明示 — ✅ 明示済み

design.md 7.4 章に 5 ステップの手動検証手順と、テストコード例 (`ResetForTest_ClearsAllRegistries`) が記載されている。

Req 10-3 の「テストを推奨する」要件に対応している。

### 5.3 テストセットアップでの呼び出し — ✅ 明示済み

design.md 10.4 章に `[SetUp]` / `[TearDown]` での `RegistryLocator.ResetForTest()` 呼び出しパターンが示されており、Req 10-4 の「テストコードからの直接呼び出しを許容」要件を満たす。

### 5.4 Domain Reload ON での副作用 — ✅ 言及済み

design.md 7.2 章の表「Domain Reload ON (通常設定)」行で「追加効果なし」と明記されており、Req 10-5 の「明示呼び出しによる副作用は生じない設計」要件を記述レベルで充足している。

---

## 6. ディレクトリ構造の妥当性

### 6.1 リポジトリ直下 `RealtimeAvatarController/` 配置 — ✅ 妥当

design.md 9 章の完全ツリーにおいて、Unity プロジェクトルートは `RealtimeAvatarController/` (リポジトリルート直下) に配置されている。Req 1-2 に準拠。

### 6.2 Embedded Package 方式の採用 — ⚠️ Recommendation #1

design.md 1 章の概要ノートで「Embedded Package を採用」と明記されているが、Req 2-5 の「git URL 経由での UPM インストール」要件と Embedded Package は矛盾する可能性がある。

- **問題**: Embedded Package は Unity プロジェクトの `Packages/` 内に直接埋め込むため、外部プロジェクトからの git URL インストールには別途 git のサブパス (`?path=`) 指定が必要になる。この手順が README (design.md 6 章) に記載されていない。
- **推奨**: README (第 6 章) に git URL インストール時の `?path=` パラメータ付き URL 例を追加するタスクを tasks.md に含めることを推奨する。例: `https://github.com/Cysharp/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.cysharp.realtimeavatarcontroller`

### 6.3 `Samples~` 機構の使用 — ✅ 正しい

design.md 3.3 章・9 章ツリーで `Samples~/UI/` 配下に asmdef が正しく配置されている。`Samples~` ディレクトリは Unity の自動インポート対象外であり、利用者は Package Manager UI の Import ボタンでコピーする仕組みが正しく設計されている。

Req 5-1〜5-5 をすべて満たす。

---

## 7. Open Issues

| # | カテゴリ | 内容 | 優先度 |
|---|---------|------|--------|
| OI-1 | UPM 配布 | Embedded Package + git URL インストール時の `?path=` URL 例が README に未記載 | 中 |
| OI-2 | package.json | exact version 固定ポリシーの長期維持方針が未文書化 | 低 |
| OI-3 | テスト asmdef | Core 以外の 6 本の JSON 雛形が設計書に未掲載 (委任形式) | 低 |

---

## 8. 推奨修正

> design.md の修正は行わない。以下はすべて **tasks.md への引き継ぎ項目**として推奨する。

| # | 種別 | 推奨内容 |
|---|------|---------|
| R-1 | tasks 追加 | README に git URL インストール時の `?path=` パラメータ付き URL 例を記載するタスクを追加する |
| R-2 | tasks 追加 | 各機能 Spec の テスト asmdef 作成タスクに「`optionalUnityReferences: ["TestAssemblies"]` を省略しないこと」をチェックリストとして明記する |
| R-3 | 将来 tasks | exact version 固定のアップグレードポリシーを `CHANGELOG.md` または依存関係管理ドキュメントに追記する |

---

## 9. Tasks への引き継ぎ

tasks.md 生成時に以下の観点を反映することを推奨する。

1. **README タスク**: `?path=` 付き git URL インストール手順の記載 (R-1)
2. **asmdef 雛形ファイル生成タスク**: 4 機能 Spec × 2 (EditMode/PlayMode) = 8 本の JSON 雛形をリポジトリに配置するタスク。`optionalUnityReferences` の必須チェックを含む (R-2)
3. **ProjectSettings.asset / EditorSettings.asset 設定タスク**: Domain Reload OFF の設定値 (design.md 7.3 章) を実際のファイルに反映する明示的タスク
4. **domain reload OFF 動作確認タスク**: design.md 7.4 章の 5 ステップ手順を検証タスクとして tasks.md に記載する
5. **CI タスク (任意)**: 第 8 章の GitHub Actions 設定例を `.github/workflows/unity-build.yml` として配置するタスク

---

*本レポートは手動検証 (ステップ B) により生成された。Skill `kiro:validate-design` は feature 名パース失敗により使用不可であった。*
