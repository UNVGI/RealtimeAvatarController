# Design Validation Report — project-foundation (再検証)

**検証日時**: 2026-04-15 (再検証)
**対象**: `.kiro/specs/project-foundation/design.md`
**参照**: `.kiro/specs/project-foundation/requirements.md` / `.kiro/specs/_shared/contracts.md` (6章)
**検証経路**: Skill `kiro:validate-design` は feature 名パース失敗のため手動検証 (ステップ B) に移行

---

## 総合評価

| 評価 | 判定 |
|------|------|
| **GO / NO-GO** | **条件付き GO** |
| 重大欠陥 (Critical) | 0 件 |
| 軽微な欠陥 (Minor) | 1 件 (新発見) |
| 推奨修正 (Recommendation) | 0 件 |

> 前回指摘 3 件 (Minor #1・#2・Recommendation #1) はすべて解消済み。`com.hidano.uosc` 採用も design.md・contracts.md・README に一貫して反映されている。ただし contracts.md §6.1 の「Samples.UI は UniRx を直接参照する例外」と design.md 4.1.5 の asmdef JSON との不整合が新たに発見された (tasks.md での対処を推奨)。

---

## 1. 前回指摘の解消状況

| # | 分類 | 内容 | 解消状況 |
|---|------|------|---------|
| Minor #1 | バージョン固定ポリシー | tasks.md 引き継ぎ方針が design.md に追記されたか | ✅ 解消済み |
| Minor #2 | テスト asmdef JSON 雛形の不完全性 | Motion / MoCap.VMC / Avatar.Builtin の 6 本が追加されたか | ✅ 解消済み |
| Recommendation #1 | git URL `?path=` 手順 | README 章に追加されたか | ✅ 解消済み |

### 1.1 Minor #1 — バージョン固定ポリシー ✅ 解消済み

design.md 3.2 章 `dependencies バージョン根拠` 表の直下に以下の注記が追加されている。

> **バージョン固定ポリシー (Minor #1 対応)**: 全依存パッケージは exact version 固定を採用する。patch バージョンのアップグレード適用方針 (セキュリティ修正対応等) は tasks.md に引き継ぎ、依存管理ドキュメントに明記する予定。

tasks.md への引き継ぎ意図が明確化されており、指摘は解消された。

### 1.2 Minor #2 — テスト asmdef JSON 雛形の不完全性 ✅ 解消済み

design.md 4.3 章に以下 6 本の完全な JSON 雛形が追加された。

| asmdef 名 | 配置パス |
|-----------|---------|
| `RealtimeAvatarController.Motion.Tests.EditMode` | `Tests/EditMode/motion-pipeline/` |
| `RealtimeAvatarController.Motion.Tests.PlayMode` | `Tests/PlayMode/motion-pipeline/` |
| `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` | `Tests/EditMode/mocap-vmc/` |
| `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` | `Tests/PlayMode/mocap-vmc/` |
| `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` | `Tests/EditMode/avatar-provider-builtin/` |
| `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` | `Tests/PlayMode/avatar-provider-builtin/` |

全 JSON で `optionalUnityReferences: ["TestAssemblies"]` が設定されており、欠落リスクは排除された。4.3 章末尾に「全テスト asmdef で `optionalUnityReferences: ["TestAssemblies"]` を省略しないこと」の注記も追加されている。

### 1.3 Recommendation #1 — git URL `?path=` 手順 ✅ 解消済み

design.md 6.8 章として新設された。`?path=` パラメータ付きの完全な URL 例と `manifest.json` スニペット、および依存パッケージの手動追加が必要である旨の注意書きが記載されている。

```
https://github.com/Hidano-Dev/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller
```

---

## 2. com.hidano.uosc 採用反映の完全性確認

### 2.1 package.json dependencies — ✅ 反映済み

design.md 3.2 章の package.json に `"com.hidano.uosc": "1.0.0"` が追加されている。バージョン根拠表にも MIT ライセンス・`SO_REUSEADDR` 有効版・npm scoped registry 配布の説明が記載されている。

### 2.2 README の scoped registry 例 (2 レジストリ) — ✅ 反映済み

design.md 6.2 章に以下 2 つの scoped registry が明記されている。

| レジストリ | URL | scopes |
|-----------|-----|--------|
| OpenUPM | `https://package.openupm.com` | `com.neuecc`, `com.cysharp` |
| npm (hidano) | `https://registry.npmjs.com` | `com.hidano` |

6.4 章の完全スニペット例にも両レジストリが含まれている。6.7 章の `openupm-cli` 利用時の注意書きにも「`com.hidano.uosc` の取得に必要な npm (hidano) registry は手動で追加する必要がある」と明記されている。

### 2.3 contracts.md §6.1 との整合 — ✅ 整合

contracts.md §6.1 末尾に以下が確定値として記載されており、design.md の記述と完全一致する。

- `com.hidano.uosc: 1.0.0` が `dependencies` に追加
- OpenUPM (`com.neuecc`, `com.cysharp`) と npm (hidano) (`com.hidano`) の 2 レジストリが必須と明記

### 2.4 MoCap.VMC asmdef への反映 — ✅ 記載済み

design.md 4.1.3 章の `RealtimeAvatarController.MoCap.VMC` の役割説明に「OSC 受信には `com.hidano.uosc` ライブラリを使用する」が記載されている。

---

## 3. 要件トレーサビリティ (再確認)

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
| Req 9 | README への OpenUPM 手順記載 | 第 6 章 (6.1〜6.8) | ✅ 完全 |
| Req 10 | Domain Reload OFF 対応 | 第 7 章 (7.1〜7.4)、10.4 章 | ✅ 完全 |

**結論**: 全 10 要件が design.md でカバーされている。未対応の Req は存在しない。

---

## 4. 新発見の問題

### 4.1 Samples.UI asmdef の UniRx 直接参照が未反映 — ⚠️ Minor 指摘 #3 (新発見)

**状況**: contracts.md §6.1 に以下の例外規則が明記されている。

> **例外 (design フェーズ確定)**: `RealtimeAvatarController.Samples.UI` は `ISlotErrorChannel.Errors` の `.ObserveOnMainThread()` 拡張メソッドを直接呼び出すため、**UniRx の直接参照を例外的に許容する**。具体的には `Samples.UI` の asmdef `references` に `UniRx` を追加する。

しかし design.md 4.1.5 章の `RealtimeAvatarController.Samples.UI` asmdef JSON の `references` には `UniRx` が含まれていない。

```json
"references": [
  "RealtimeAvatarController.Core",
  "RealtimeAvatarController.Motion",
  "RealtimeAvatarController.MoCap.VMC",
  "RealtimeAvatarController.Avatar.Builtin"
]
```

**問題**: contracts.md §6.1 (上位確定値) と design.md 4.1.5 章 (asmdef 定義) が不整合のため、実装フェーズで担当者が誤ったまま実装するリスクがある。

**推奨**: design.md の修正は制約上禁止されているため、tasks.md に「`RealtimeAvatarController.Samples.UI.asmdef` の `references` に `UniRx` を追加すること (contracts.md §6.1 例外規則に準拠)」を明示タスクとして追加することを推奨する。

---

## 5. contracts.md §6.1 との整合性 (全項目再確認)

| 確認項目 | 状態 |
|---------|------|
| Runtime asmdef 5 本の名称・配置パス・担当 Spec | ✅ 完全一致 |
| Editor asmdef 5 本の名称・配置パス・依存ルール | ✅ 完全一致 |
| Tests asmdef 10 本の名称・配置パス・必須/任意区分 | ✅ 完全一致 |
| 名前空間マッピング (6.2 章) | ✅ 完全一致 |
| package.json dependencies 確定値 (3 本) | ✅ 完全一致 |
| scoped registry 2 本の URL・scopes | ✅ 完全一致 |
| Samples.UI asmdef の UniRx 直接参照 | ⚠️ contracts.md に例外規則あり / design.md JSON に未反映 |

---

## 6. Open Issues

| # | カテゴリ | 内容 | 優先度 |
|---|---------|------|--------|
| OI-1 | asmdef 定義 | contracts.md §6.1 例外規則による `Samples.UI` asmdef への `UniRx` 直接参照が design.md JSON に未反映 | 中 |

---

## 7. Tasks への引き継ぎ (推奨)

tasks.md 生成時に以下の観点を反映することを推奨する。

1. **Samples.UI asmdef 修正タスク**: `RealtimeAvatarController.Samples.UI.asmdef` の `references` に `"UniRx"` を追加する (contracts.md §6.1 例外規則に準拠、Minor #3 対応)
2. **バージョン固定ポリシー文書化タスク**: exact version 固定のアップグレードポリシーを依存管理ドキュメントに追記する (Minor #1 引き継ぎ)
3. **README タスク**: git URL インストール時の `?path=` 手順は 6.8 章で解消済み (Recommendation #1 引き継ぎ完了)
4. **asmdef 雛形ファイル生成タスク**: 全 asmdef JSON の配置タスク。`optionalUnityReferences: ["TestAssemblies"]` の必須チェックを含む
5. **ProjectSettings.asset / EditorSettings.asset 設定タスク**: Domain Reload OFF の設定値 (design.md 7.3 章) を実際のファイルに反映する明示的タスク
6. **domain reload OFF 動作確認タスク**: design.md 7.4 章の 5 ステップ手順を検証タスクとして tasks.md に記載する
7. **CI タスク (任意)**: 第 8 章の GitHub Actions 設定例を `.github/workflows/unity-build.yml` として配置するタスク

---

*本レポートは手動検証 (ステップ B) により生成された。Skill `kiro:validate-design` は feature 名パース失敗により使用不可であった。*
