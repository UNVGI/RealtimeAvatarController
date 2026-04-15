# design.md 検証レポート — ui-sample

> 検証日: 2026-04-15  
> 検証対象: `.kiro/specs/ui-sample/design.md`  
> 参照ドキュメント: `requirements.md` (Req 1〜13) / `contracts.md` (1.1〜1.8、6章)  
> 総合判定: **条件付き GO** (軽微な不整合・未記述事項あり、重大な構造的欠陥なし)

---

## 1. Requirements Traceability (要件トレーサビリティ)

### 対応マトリクス

| Req # | タイトル | design.md での対応箇所 | 充足度 |
|-------|---------|----------------------|:------:|
| Req 1 | Slot 操作 UI | §3.2 SlotManagementDemo / §6.1 SlotManagementPanel / §12.1 ファイル構成 | ○ |
| Req 2 | アバター選択 UI (Editor) | §4.1〜4.2 SlotSettingsEditor / DrawAvatarProviderSection | ○ |
| Req 3 | MoCap ソース設定 UI (Editor) | §4.1〜4.2 DrawMoCapSourceSection (記載省略「同じパターン」注記あり) | △ |
| Req 4 | Weight 操作 UI | §4.4 DrawWeightToggle | ○ |
| Req 5 | 挙動確認用デモシーン | §3 サンプルシーン構成 / §10 参照共有シナリオ / §7.3 エラーシミュレーション | ○ |
| Req 6 | UPM Samples~ 配布機構 | §8 Samples~ 配置 / §8.2 package.json samples エントリ | ○ |
| Req 7 | UI 層と機能部の分離 | §9 依存アセンブリ / §9.3 依存グラフ | ○ |
| Req 8 | UI フレームワーク選択 | §2 UI フレームワーク選択 | ○ |
| Req 9 | UniRx MotionStream 購読 (オプション) | §6.3 SlotErrorPanel で `.ObserveOnMainThread().Subscribe()` は記載あり。MotionStream 購読の個別設計は未詳述 | △ |
| Req 10 | Fallback 設定 UI | §4.3 Fallback 設定 enum ドロップダウン | ○ |
| Req 11 | エラー表示 UI | §6.3 SlotErrorPanel / §7 エラー表示 UI 詳細 | ○ |
| Req 12 | テストアセンブリ定義 (任意) | §11 テスト設計 / §11.3 テスト asmdef 命名 | ○ |
| Req 13 | ランタイム動的生成 SlotSettings への対応 (オプション) | 未記載 (設計判断として言及なし) | × |

### トレーサビリティ指摘事項

**[TRC-1] Req 3 (MoCap ソース設定 UI) の設計省略**  
`DrawMoCapSourceSection()` の実装コードが「同じパターンで実装する」という一行注記のみで省略されている。  
Req 3 は `Release()` 経由の旧ソース解放 (AC 7)・未割り当て状態選択 (AC 8) など Avatar Provider とは異なるロジックを含む。  
→ **推奨**: 少なくとも「未割り当て」選択肢 (空文字列または None エントリ) の処理方針と、`IMoCapSourceRegistry.Release()` 呼び出しタイミングを設計に追記すること。

**[TRC-2] Req 9 (MotionStream 購読) の詳細設計なし**  
`SlotErrorPanel` での `ObserveOnMainThread().Subscribe()` は記載があるが、`MotionStream` のデバッグプレビュー UI (フレーム数・タイムスタンプ表示) の設計が存在しない。  
→ オプション要件のため致命的ではないが、「実装しない」という明示的な設計判断の記載を推奨する。

**[TRC-3] Req 13 (ランタイム動的生成対応) の言及なし**  
オプション要件だが、contracts.md 1.1/1.2 で「公式サポート」と明記されているシナリオ Y について design.md で何も触れていない。  
→ `SlotManagerBehaviour.cs` の `initialSlots` フィールドはシナリオ X 固定になっており、シナリオ Y への対応可否が不明。「スコープ外とする」旨を明記することを推奨する。

---

## 2. Contract Compliance (契約整合性)

### 2.1 contracts.md 1.6 章 (RegistryLocator)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `RegistryLocator.ProviderRegistry` 経由でのアクセス | §4.2 `RefreshTypeIds()` で正しく参照 | ○ |
| `RegistryLocator.MoCapSourceRegistry` 経由でのアクセス | §4.2 で同様に参照 | ○ |
| `RegistryLocator.ErrorChannel` の使用 | §6.3 `SlotErrorPanel` で `RegistryLocator.ErrorChannel.Errors` を参照 | ○ |
| `ResetForTest()` 前提の Domain Reload OFF 考慮 | §5.3 で詳述 | ○ |
| `OverrideProviderRegistry()` / `OverrideMoCapSourceRegistry()` | テスト asmdef (§11) で使用を想定しているが明示なし | △ |

**[CC-1] テストでの Registry モック注入方針未記載**  
§11 EditMode テストに「Registry に Factory を登録した状態で…」とあるが、`RegistryLocator.OverrideProviderRegistry()` を使うのか実際の Factory を登録するのかが未定義。  
→ **推奨**: テストセットアップで `RegistryLocator.OverrideXxx()` を用いたモック注入方針を明記する。

### 2.2 contracts.md 1.7 章 (ErrorChannel)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `ISlotErrorChannel.Errors` の `ObserveOnMainThread().Subscribe()` 使用 | §6.3 コードスニペットに明示 | ○ |
| 購読の Dispose 管理 | `CompositeDisposable` + `OnDestroy` で正しく実装 | ○ |
| UI 側独自フィルタリング方針 | §7.1 カテゴリフィルタ (トグルグループ) として具体化 | ○ |
| Core 側 Debug.LogError 抑制前提の明記 | §7.2 で明示 | ○ |
| `SlotError` フィールド (`SlotId` / `Category` / `Exception` / `Timestamp`) の利用 | §6.3 表示フォーマットで全フィールドを使用 | ○ |

### 2.3 contracts.md 1.8 章 (FallbackBehavior)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `FallbackBehavior` 列挙値 (`HoldLastPose` / `TPose` / `Hide`) の網羅 | §4.3 表示名テーブルで全列挙値を網羅 | ○ |
| デフォルト値 `HoldLastPose` の明示 | Req 10 AC 3 準拠として実装。design.md には「デフォルト選択値」明記なし | △ |
| `SlotSettings.fallbackBehavior` への反映方法 | `EditorGUILayout.PropertyField()` による自動 enum UI | ○ |

**[CC-2] FallbackBehavior デフォルト値の design への明記不足**  
contracts.md 1.8 章で「デフォルト値: HoldLastPose」と明記されているが、design.md §4.3 にデフォルト設定の記述がない。  
→ **推奨**: `SlotSettings` 初期化時に `fallbackBehavior = FallbackBehavior.HoldLastPose` を設定する旨を `SlotSettingsEditor` の `OnEnable()` または `SlotSettings` のデフォルトフィールド定義として言及する。

### 2.4 contracts.md 6章 (Samples~ asmdef)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `RealtimeAvatarController.Samples.UI` が `Samples~/UI/Runtime/` に配置 | §8.1 ディレクトリ構造、§9.1 で定義 | ○ |
| `RealtimeAvatarController.Samples.UI.Editor` が `Samples~/UI/Editor/` に配置 | §8.1、§9.2 で定義 | ○ |
| Tests asmdef の配置パス | §8.1、§11.3 で `Samples~/UI/Tests/` 内に配置 | △ (後述) |
| `Samples.UI` の UniRx / UniTask 直接依存 | **contracts.md 6.1 章「Samples.UI も同様に UniRx/UniTask への直接依存は持たず」に違反** | **×** |

**[CC-3] 重要: `Samples.UI` の UniRx / UniTask 直接依存が contracts.md 6.1 章に違反**  
contracts.md 6.1 章には「`RealtimeAvatarController.Samples.UI` も同様に UniRx / UniTask への直接依存は持たず、機能部 API 経由で利用する」と明記されている。  
しかし design.md §9.1 の `references` には `UniRx` と `UniTask` が直接列挙されている。

```
// design.md §9.1 の記載 (問題箇所)
references: [Core, Motion, MoCap.VMC, Avatar.Builtin, UniRx, UniTask]
```

一方で §6.3 `SlotErrorPanel` は `using UniRx;` を直接使用しており、`ObserveOnMainThread()` を呼び出している。  
`Core` アセンブリが UniRx を依存として持つため、`Samples.UI` からは `Core` 経由で型が見えるが、拡張メソッド (`ObserveOnMainThread()` 等) を直接呼び出すには UniRx への直接参照が必要という技術的実態がある。  
contracts.md 6.1 章には「技術的必要が生じた場合は、design フェーズで要否を個別判断し本章に追記する」という逃げ道が用意されている。  
→ **必須対応**: `Samples.UI` が UniRx 拡張メソッドを直接使用することの技術的必要性を design.md に明記し、contracts.md 6.1 章への追記合意として記録すること。

**[CC-4] テスト asmdef の配置パスが contracts.md と不整合**  
contracts.md 6.1 章のテスト asmdef テーブルでは、ui-sample テストを `Tests/EditMode/ui-sample/` / `Tests/PlayMode/ui-sample/` (パッケージルート直下の `Tests/` ディレクトリ) に配置するよう定義している。  
しかし design.md §8.1、§11.3 では `Samples~/UI/Tests/` 内への配置としている。

| | contracts.md 6.1 章 | design.md §8.1/§11.3 |
|--|----|----|
| EditMode | `Tests/EditMode/ui-sample/` | `Samples~/UI/Tests/EditMode/` |
| PlayMode | `Tests/PlayMode/ui-sample/` | `Samples~/UI/Tests/PlayMode/` |

UPM の `Samples~` 機構ではサンプルディレクトリがユーザープロジェクトにコピーされるため、`Samples~/UI/Tests/` に配置するとコピー先でもテストが含まれる。これは意図的か否かを設計判断として明記する必要がある。  
→ **推奨**: どちらの配置が正とするか contracts.md 担当者と合意し、design.md を修正または contracts.md への例外追記を行うこと。

---

## 3. UI フレームワーク選定評価

### 選定結果: UGUI + EditorGUILayout (§2)

| 評価観点 | 評価 |
|---------|:----:|
| UGUI vs UI Toolkit の比較評価が明示されているか | ○ (比較テーブルあり) |
| Editor Inspector 主眼方針との整合性 | ○ (選定理由 1 で明示) |
| `EditorGUILayout.Popup()` による動的候補列挙の実装容易性 | ○ (選定理由 2 で明示) |
| 将来の UI Toolkit 移行パスの言及 | ○ (注記あり) |
| ランタイム Canvas UI に UGUI を採用する合理性 | ○ (選定理由 4 で説明) |

**選定根拠は十分に記述されており問題なし。**

---

## 4. SlotSettings CustomEditor 設計評価

| 評価観点 | 設計の充足度 | 指摘 |
|---------|:----------:|------|
| `[CustomEditor(typeof(SlotSettings))]` 実装方針の具体性 | ○ | コードスニペット付きで明確 |
| typeId ドロップダウンの Registry 動的列挙 | ○ | `RefreshTypeIds()` + `EditorGUILayout.Popup()` で具体化 |
| Config SO ドラッグ&ドロップ参照 | ○ | `EditorGUILayout.PropertyField(configProp, ...)` で実装 |
| Fallback enum ドロップダウン | ○ | `EditorGUILayout.PropertyField(_fallbackBehaviorProp)` で自動描画 |
| Weight 二値トグル | ○ | `DrawWeightToggle()` で `0.5f` 閾値による二値変換を実装 |
| MoCap ソース側の設計詳細 | △ | 「同じパターン」として省略。Release 処理・未割当選択の設計なし (TRC-1 参照) |

**[CE-1] Weight トグル閾値ロジックの潜在的問題**  
`_weightProp.floatValue >= 0.5f` でトグル状態を判定しているが、contracts.md 1.1 章では「初期版では 0.0 または 1.0 の二値のみ」と定義されている。  
中間値が誤って入力された場合の挙動が曖昧。ただし初期版では中間値入力の手段が UI に存在しないため、実害は限定的。  
→ コメントとして「初期版では 0.0/1.0 のみ。閾値 0.5f は安全側への丸め用」と明記を推奨。

---

## 5. Registry 未初期化時の Graceful Degradation

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `GetRegisteredTypeIds()` が空配列の場合の HelpBox 表示 | ○ (§5.2 / §4.2 で実装) |
| Registry null 時の try-catch + HelpBox (Error) | ○ (§5.2 テーブル、§4.2 コードで実装) |
| 手入力フォールバックフィールドの提供 | ○ (§4.2 `PropertyField(typeIdProp, ...)`) |
| 「候補を更新」ボタンによる再取得 | ○ (§4.2 `GUILayout.Button("候補を更新")`) |
| Domain Reload OFF 時の考慮 | ○ (§5.3 で詳述) |

**Graceful Degradation の設計は十分。問題なし。**

---

## 6. ErrorChannel 購読 UI 評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `.ObserveOnMainThread().Subscribe()` の明示 | ○ (§6.3 コードスニペットに明示) |
| `CompositeDisposable` による購読管理 | ○ (§6.3 `_disposables.AddTo()`) |
| フィルタリング戦略の定義 | ○ (§7.1 カテゴリフィルタ: トグルグループで `VmcReceive` / `InitFailure` / `ApplyFailure` / `RegistryConflict`) |
| 最大表示件数の設計 (最新 N 件) | ○ (§6.3 `_maxDisplayCount = 20`、§7.1 テーブルで 20 件と明記) |
| Core 側 LogError 抑制ポリシーの理解 | ○ (§7.2 で前提として明記) |

**ErrorChannel 購読 UI の設計は充実しており問題なし。**

---

## 7. 参照共有デモシナリオ評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| 1 VMC ソースを 2 Slot で共有するシナリオの具体性 | ○ (§10.1〜10.3 で詳述) |
| セットアップ手順の具体性 | ○ (§10.2 手順 1〜4 でアセット設定・API 呼び出し順を明示) |
| 参照カウント挙動の視覚化 | ○ (§10.3 テーブルで操作→カウント→視覚確認を対応付け) |
| `IMoCapSource.Dispose()` を直接呼ばない方針の明示 | ○ (§10.4 注意点に明記) |
| Descriptor 等価判定方針 | △ (§10.4 で「設計フェーズで確定」として未解決のまま残存) |

**[SC-1] Descriptor 等価判定の未解決問題**  
§10.4 に「同一 Descriptor の等価判定は `SourceTypeId` + `Config` の内容比較で行う (詳細は `IMoCapSourceRegistry` 設計フェーズで確定)」と記されており、未解決のまま残っている。  
参照共有デモシナリオが正しく動作するかどうかはこの等価判定ロジックに依存する。  
→ **推奨**: `IMoCapSourceRegistry` の設計フェーズ (slot-core Spec) での確定が必要な依存事項として open issue に明記する。

---

## 8. Samples~ 配置評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `package.json` samples エントリとの整合 | ○ (§8.2 で JSON 全体を例示、`path: Samples~/UI` と整合) |
| ディレクトリ構造の網羅性 | ○ (§8.1 で全ファイルを列挙) |
| asmdef 2 本 (Runtime + Editor) の配置 | ○ (§8.1 と §9.1/9.2 で整合) |
| テスト配置パスの contracts.md との整合 | × (CC-4 参照) |

---

## 9. Editor Inspector 主眼方針の明確性

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| スタンドアロン GUI がスコープ外であることの明示 | ○ (§1 概要・§6 ランタイム UI 冒頭で「Editor PlayMode でのデモ確認用」と明示) |
| ランタイム Canvas UI が「任意提供範囲」であることの明示 | ○ (§1 UI の役割分担テーブルで「任意提供範囲」と分類) |
| 主要提供範囲が Inspector 拡張であることの一貫性 | ○ (§1 / §4 で一貫して主眼と位置付け) |

**スコープの明確化は十分。問題なし。**

---

## 10. Open Issues

現時点で design.md に明記されていない未解決事項・追加対応が必要な項目を整理する。

| ID | 優先度 | 内容 | 参照 |
|----|:------:|------|------|
| OI-1 | **高** | `Samples.UI` の UniRx / UniTask 直接依存を contracts.md 6.1 章への例外追記として合意すること | CC-3 |
| OI-2 | **高** | テスト asmdef 配置パス (`Samples~/UI/Tests/` vs `Tests/EditMode/ui-sample/`) の contracts.md との整合解決 | CC-4 |
| OI-3 | **中** | `DrawMoCapSourceSection()` の詳細設計 (未割当選択・`Release()` 呼び出しタイミング) を追記 | TRC-1 |
| OI-4 | **中** | `IMoCapSourceRegistry` の Descriptor 等価判定確定待ち (slot-core Spec 依存) | SC-1 |
| OI-5 | **低** | Req 9 (MotionStream デバッグプレビュー) について「初期版では実装しない」旨を明示 | TRC-2 |
| OI-6 | **低** | Req 13 (ランタイム動的生成対応) について「スコープ外」または「読み取り専用」とする設計判断を明示 | TRC-3 |
| OI-7 | **低** | `FallbackBehavior` デフォルト値 `HoldLastPose` の設定方法を `SlotSettings` 定義側またはエディタ側で明示 | CC-2 |

---

## 総合評価サマリー

| 観点 | 評価 | コメント |
|-----|:----:|---------|
| Requirements Traceability | B | Req 3・9・13 に未詳述あり。核心部 (Req 1〜2、4〜8、10〜11) は充足 |
| Contract Compliance | C+ | UniRx 直接依存 (CC-3) とテスト配置パス不整合 (CC-4) が重要指摘。他は概ね整合 |
| UI フレームワーク選定 | A | 比較・選定根拠ともに明確 |
| CustomEditor 設計 | B+ | 主要部は具体的。MoCap ソース側の省略が残課題 |
| Graceful Degradation | A | HelpBox / 手入力 / 更新ボタンの三段構えで充実 |
| ErrorChannel 購読 UI | A | ObserveOnMainThread / CompositeDisposable / フィルタ戦略が揃っている |
| 参照共有シナリオ | A- | 手順・視覚化ともに具体的。等価判定は外部依存 |
| Samples~ 配置 | B | ディレクトリ構造は整合。テスト配置パスは要修正 |
| Inspector 主眼方針 | A | スコープ外明示が一貫している |

**総合判定: 条件付き GO**  
重大な構造的欠陥はなく、主要な設計判断 (CustomEditor、Graceful Degradation、ErrorChannel、参照共有シナリオ、UI フレームワーク選定) はいずれも十分な品質を持つ。  
tasks フェーズ移行前に OI-1 (UniRx 直接依存の contracts.md 合意) と OI-2 (テスト配置パス整合) の 2 点を解消することを条件とする。  
その他 open issues は tasks に取り込みうる粒度のため、設計ブロックには該当しない。
