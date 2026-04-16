# design.md 検証レポート — ui-sample (再検証)

> 検証日: 2026-04-15 (再検証)
> 検証対象: `.kiro/specs/ui-sample/design.md`
> 参照ドキュメント: `requirements.md` (Req 1〜13) / `contracts.md` (§1.1〜1.8、§6.1)
> 前回判定: **条件付き GO** (2026-04-15 初回)
> 今回判定: **GO** (前回指摘 5 件すべて解消、構造的欠陥なし)

---

## 0. 前回指摘の解消状況 (最優先確認)

### [CC-3] Samples.UI asmdef — UniRx 直接参照の扱い

| 確認項目 | 結果 |
|---------|:----:|
| design.md §9.1 references に `UniRx` のみ記載 | ○ |
| design.md §9.1 references に `UniTask` なし | ○ |
| UniRx 直接参照の技術的根拠が §9.1 注記に明記されている | ○ |
| contracts.md §6.1「例外 (design フェーズ確定)」と整合している | ○ |
| 依存グラフ (§9.3) で UniRx が例外的直接参照と明記されている | ○ |

**解消状況: 完全解消。**
contracts.md §6.1 は「`Samples.UI` の asmdef `references` に `UniRx` を追加する。UniTask の直接参照は現時点で技術的必要がないため不要」と確定済みである。  
design.md §9.1 もこれと完全に整合しており、UniTask の直接参照が排除されていることも確認済み。

---

### [CC-4] テスト asmdef 配置パス不一致

| 確認項目 | 結果 |
|---------|:----:|
| §8.1 ディレクトリ構造でテストを `Tests/EditMode/ui-sample/` / `Tests/PlayMode/ui-sample/` に配置 | ○ |
| §11.3 テスト asmdef 命名テーブルが contracts.md §6.1 準拠パスに更新されている | ○ |
| `Samples~/UI/Tests/` 記載が削除されている | ○ |
| §13.1 ファイル構成にも同方針が明記されている | ○ |
| 設計意図 (サンプルコピー時のテスト混入防止) が説明されている | ○ |

**解消状況: 完全解消。**
旧記載 (`Samples~/UI/Tests/`) は削除済みであり、contracts.md §6.1 テスト asmdef テーブルの標準配置 (`Tests/EditMode/ui-sample/` / `Tests/PlayMode/ui-sample/`) と完全に整合した。

---

### [TRC-1] DrawMoCapSourceSection の省略解消

| 確認項目 | 結果 |
|---------|:----:|
| `DrawMoCapSourceSection()` の具体的コードが §4.2 に追加されている | ○ |
| 未割り当て選択肢 `"(未割り当て)"` の処理が実装されている | ○ |
| `typeId` を空文字列に設定する処理が明示されている | ○ |
| `IMoCapSourceRegistry.Release()` 呼び出しタイミングが注記として明記されている | ○ |
| Editor 側での `Release()` 不要の根拠が説明されている | ○ |
| 参照カウント > 1 時の `Dispose()` 非実行動作が説明されている | ○ |

**解消状況: 完全解消。**
`DrawMoCapSourceSection()` のフルコードスニペットが追加され、「未割り当て」選択肢処理 (AC 8 対応)・`Release()` 呼び出しタイミング (AC 7 対応) の両方が具体的に設計された。

---

### [SC-1] Descriptor 等価判定依存の注記

| 確認項目 | 結果 |
|---------|:----:|
| §10.4 注記が「設計フェーズで確定」の未解決表現から「slot-core design フェーズ確定済」に更新されている | ○ |
| SourceTypeId 文字列等価 + Config 参照等価 (`ReferenceEquals`) の確定内容が記述されている | ○ |
| `GetHashCode()` 実装も含め完全な等価判定ロジックが明記されている | ○ |
| デモシーン構築時の操作方法 (同一 Config SO アセットを両 SlotSettings で参照) が明示されている | ○ |

**解消状況: 完全解消。**
slot-core design.md §3.10 確定内容 (SourceTypeId 文字列等価 + Config 参照等価) が §10.4 に反映されており、open issue ではなく確定事項として扱われている。

---

### [TRC-2/3] Req 9 / Req 13 のスコープ決定

| 確認項目 | 結果 |
|---------|:----:|
| §12.1 に「Req 9: initial 版では実装しない」が明文化されている | ○ |
| §12.2 に「Req 13: シナリオ Y (動的生成) は対応しない」が明文化されている | ○ |
| シナリオ X (事前アセット設定) / シナリオ Y (動的生成) の対応/非対応が表で整理されている | ○ |
| 各スコープ外とする根拠が説明されている | ○ |
| 将来拡張の余地のみ残す旨が適切に記述されている | ○ |

**解消状況: 完全解消。**
§12「オプション要件のスコープ決定」章が新設され、Req 9・Req 13 ともに初期版での非実装が明文化された。

---

## 1. Requirements Traceability (要件トレーサビリティ)

### 対応マトリクス

| Req # | タイトル | design.md での対応箇所 | 充足度 |
|-------|---------|----------------------|:------:|
| Req 1 | Slot 操作 UI | §3.2 SlotManagementDemo / §6.1 SlotManagementPanel / §13.1 ファイル構成 | ○ |
| Req 2 | アバター選択 UI (Editor) | §4.1〜4.2 SlotSettingsEditor / DrawAvatarProviderSection | ○ |
| Req 3 | MoCap ソース設定 UI (Editor) | §4.2 DrawMoCapSourceSection (フルコード + Release() タイミング注記) | ○ |
| Req 4 | Weight 操作 UI | §4.4 DrawWeightToggle | ○ |
| Req 5 | 挙動確認用デモシーン | §3 サンプルシーン構成 / §10 参照共有シナリオ / §7.3 エラーシミュレーション | ○ |
| Req 6 | UPM Samples~ 配布機構 | §8 Samples~ 配置 / §8.2 package.json samples エントリ | ○ |
| Req 7 | UI 層と機能部の分離 | §9 依存アセンブリ / §9.3 依存グラフ | ○ |
| Req 8 | UI フレームワーク選択 | §2 UI フレームワーク選択 | ○ |
| Req 9 | UniRx MotionStream 購読 (オプション) | §12.1「initial 版では実装しない」と明文化 | ○ (スコープ外明示) |
| Req 10 | Fallback 設定 UI | §4.3 Fallback 設定 enum ドロップダウン | ○ |
| Req 11 | エラー表示 UI | §6.3 SlotErrorPanel / §7 エラー表示 UI 詳細 | ○ |
| Req 12 | テストアセンブリ定義 (任意) | §11 テスト設計 / §11.3 テスト asmdef 命名 (CC-4 解消済み) | ○ |
| Req 13 | ランタイム動的生成 SlotSettings への対応 (オプション) | §12.2「シナリオ Y は対応しない」と明文化 | ○ (スコープ外明示) |

**前回 × だった Req 13 が ○ (スコープ外明示) に変化。前回 △ だった Req 3・9 が ○ に変化。**

---

## 2. Contract Compliance (契約整合性)

### 2.1 contracts.md §6.1 asmdef 構成

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `Samples.UI` references に `UniRx` のみ (UniTask なし) | §9.1 で `UniRx` のみ明記 | ○ |
| UniRx 直接参照の例外的許容が contracts.md と整合 | §9.1 注記・§9.3 で明示。contracts.md §6.1 例外条項と完全整合 | ○ |
| テスト asmdef 配置 `Tests/EditMode/ui-sample/` / `Tests/PlayMode/ui-sample/` | §8.1 / §11.3 で contracts.md 準拠パスに統一 | ○ |
| `Samples.UI.Editor` が UniRx/UniTask 直接参照なし | §9.2 で references に `Samples.UI` / `Core` / `Core.Editor` のみ | ○ |

### 2.2 contracts.md §1.6 (RegistryLocator)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `RegistryLocator.ProviderRegistry` 経由でのアクセス | §4.2 `RefreshTypeIds()` で正しく参照 | ○ |
| `RegistryLocator.MoCapSourceRegistry` 経由でのアクセス | §4.2 で同様に参照 | ○ |
| `RegistryLocator.ErrorChannel` の使用 | §6.3 で `RegistryLocator.ErrorChannel.Errors` を参照 | ○ |
| `ResetForTest()` 前提の Domain Reload OFF 考慮 | §5.3 で詳述 | ○ |
| `OverrideProviderRegistry()` / `OverrideMoCapSourceRegistry()` の使用 | §11.4 で EditMode テストのセットアップコード付きで明示 | ○ |

**前回 △ だった「OverrideXxx() 使用の明示」が §11.4 追加により ○ に変化。**

### 2.3 contracts.md §1.7 (ErrorChannel)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `ISlotErrorChannel.Errors` の `ObserveOnMainThread().Subscribe()` | §6.3 コードスニペットに明示 | ○ |
| 購読の Dispose 管理 | `CompositeDisposable` + `OnDestroy` で正しく実装 | ○ |
| UI 側独自フィルタリング方針 | §7.1 カテゴリフィルタ (トグルグループ) として具体化 | ○ |
| Core 側 Debug.LogError 抑制前提の明記 | §7.2 で明示 | ○ |
| `SlotError` フィールド全利用 | §6.3 表示フォーマットで全フィールドを使用 | ○ |

### 2.4 contracts.md §1.8 (FallbackBehavior)

| 確認項目 | design.md の記載 | 判定 |
|---------|----------------|:----:|
| `FallbackBehavior` 列挙値の網羅 | §4.3 表示名テーブルで全列挙値を網羅 | ○ |
| デフォルト値 `HoldLastPose` の明示 | §4.3 に明示なし (前回 CC-2 指摘残存) | △ |
| `SlotSettings.fallbackBehavior` への反映方法 | `EditorGUILayout.PropertyField()` による自動 enum UI | ○ |

> **[CC-2] 残存軽微指摘**: `FallbackBehavior` デフォルト値 `HoldLastPose` の明示が §4.3 にない。ただし contracts.md §1.8 で定義されており、`SlotSettings` データモデル側の問題であるため ui-sample design の構造的欠陥には該当しない。tasks で対応可能。

---

## 3. SlotSettings CustomEditor 設計評価

| 評価観点 | 設計の充足度 | 指摘 |
|---------|:----------:|------|
| `[CustomEditor(typeof(SlotSettings))]` 実装方針の具体性 | ○ | コードスニペット付きで明確 |
| typeId ドロップダウンの Registry 動的列挙 | ○ | `RefreshTypeIds()` + `EditorGUILayout.Popup()` で具体化 |
| Config SO ドラッグ&ドロップ参照 | ○ | `EditorGUILayout.PropertyField(configProp, ...)` で実装 |
| Fallback enum ドロップダウン | ○ | `EditorGUILayout.PropertyField(_fallbackBehaviorProp)` で自動描画 |
| Weight 二値トグル | ○ | `DrawWeightToggle()` で `0.5f` 閾値による二値変換を実装 |
| MoCap ソース側の設計詳細 | ○ | フルコード追加。未割当選択・Release() タイミング注記あり (TRC-1 解消) |

**前回 △ だった MoCap ソース設計が ○ に変化。設計品質として十分。**

---

## 4. UI フレームワーク選定評価

| 評価観点 | 評価 |
|---------|:----:|
| UGUI vs UI Toolkit の比較評価が明示されているか | ○ |
| Editor Inspector 主眼方針との整合性 | ○ |
| `EditorGUILayout.Popup()` による動的候補列挙の実装容易性 | ○ |
| 将来の UI Toolkit 移行パスの言及 | ○ |
| ランタイム Canvas UI に UGUI を採用する合理性 | ○ |

**選定根拠は十分に記述されており問題なし。**

---

## 5. Registry 未初期化時の Graceful Degradation

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `GetRegisteredTypeIds()` が空配列の場合の HelpBox 表示 | ○ |
| Registry null 時の try-catch + HelpBox (Error) | ○ |
| 手入力フォールバックフィールドの提供 | ○ |
| 「候補を更新」ボタンによる再取得 | ○ |
| Domain Reload OFF 時の考慮 | ○ |

**Graceful Degradation の設計は十分。問題なし。**

---

## 6. ErrorChannel 購読 UI 評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `.ObserveOnMainThread().Subscribe()` の明示 | ○ |
| `CompositeDisposable` による購読管理 | ○ |
| フィルタリング戦略の定義 | ○ |
| 最大表示件数の設計 (最新 N 件) | ○ |
| Core 側 LogError 抑制ポリシーの理解 | ○ |

**ErrorChannel 購読 UI の設計は充実しており問題なし。**

---

## 7. 参照共有デモシナリオ評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| 1 VMC ソースを 2 Slot で共有するシナリオの具体性 | ○ |
| セットアップ手順の具体性 | ○ |
| 参照カウント挙動の視覚化 | ○ |
| `IMoCapSource.Dispose()` を直接呼ばない方針の明示 | ○ |
| Descriptor 等価判定方針 | ○ (slot-core design §3.10 確定内容が反映済み) |

**前回 △ だった Descriptor 等価判定が ○ に変化。参照共有シナリオの設計は完結している。**

---

## 8. Samples~ 配置評価

| 評価観点 | 設計の充足度 |
|---------|:----------:|
| `package.json` samples エントリとの整合 | ○ |
| ディレクトリ構造の網羅性 | ○ |
| asmdef 2 本 (Runtime + Editor) の配置 | ○ |
| テスト配置パスの contracts.md との整合 | ○ (CC-4 解消) |

**テスト配置パス問題が解消され、Samples~ 配置設計は完全。**

---

## 9. オプション要件スコープ評価

| Req # | スコープ決定内容 | 明文化場所 | 評価 |
|-------|--------------|---------|:----:|
| Req 9 | initial 版で実装しない | §12.1 | ○ |
| Req 13 (シナリオ Y) | 対応しない (スコープ外) | §12.2 | ○ |
| Req 12 | 任意。作成する場合の方針を §11 で定義 | §11 | ○ |

**両オプション要件のスコープ決定が明文化されており、tasks フェーズへの引き継ぎが明確。**

---

## 10. 新発見の問題

### [NEW-1] 軽微: Weight トグル閾値コメントなし (継続)

前回 CE-1 で指摘した内容は残存しているが、設計上の実害は初期版スコープでは限定的。  
tasks で「コメント追記」として取り込むことを推奨する。

### [NEW-2] 軽微: FallbackBehavior デフォルト値の明示 (CC-2 継続)

`SlotSettings` データモデルの初期値定義については slot-core Spec の責務であり、ui-sample design を blocking する問題ではない。  
tasks で SlotSettings 生成時の初期値確認テストケースとして取り込むことを推奨する。

### [NEW-3] 確認推奨: `SlotDetailPanelUI.cs` の Registry 候補取得タイミング

§6.2 で `SlotDetailPanel` がランタイム `GetRegisteredTypeIds()` を呼び出すと記載されているが、実際の呼び出しタイミング・エラーハンドリング方針の設計が §5.2 と同等の詳細度で記述されていない。  
Editor 側は §5.2 で詳細化されているが、ランタイム Canvas UI 側が「任意機能」と位置づけられているため設計省略は許容範囲。致命的指摘ではない。

---

## 11. Open Issues (残存・更新)

| ID | 優先度 | 内容 | 前回 ID | 状態 |
|----|:------:|------|---------|:----:|
| OI-1 | ~~高~~ | contracts.md §6.1 例外追記 — UniRx 直接参照の合意 | CC-3 | **解消** |
| OI-2 | ~~高~~ | テスト asmdef 配置パスの contracts.md 整合 | CC-4 | **解消** |
| OI-3 | ~~中~~ | DrawMoCapSourceSection() 詳細設計 (未割当・Release タイミング) | TRC-1 | **解消** |
| OI-4 | ~~中~~ | Descriptor 等価判定の slot-core 依存 | SC-1 | **解消** |
| OI-5 | ~~低~~ | Req 9 スコープ外明示 | TRC-2 | **解消** |
| OI-6 | ~~低~~ | Req 13 スコープ外明示 | TRC-3 | **解消** |
| OI-7 | **低** | FallbackBehavior デフォルト値 HoldLastPose の設定方法を明示 | CC-2 | 残存 (tasks 取込可) |
| OI-8 | **低** | Weight トグル閾値 0.5f にコメント追記 | CE-1 | 残存 (tasks 取込可) |

**重大 open issue: なし。残存は軽微 2 件のみ。**

---

## 総合評価サマリー

| 観点 | 評価 | コメント |
|-----|:----:|---------|
| Requirements Traceability | A | 全 Req が充足または明示的スコープ外。前回 B → A |
| Contract Compliance | A- | UniRx 直接参照・テスト配置パスともに解消。FallbackBehavior デフォルト値のみ軽微残存 |
| UI フレームワーク選定 | A | 変化なし |
| CustomEditor 設計 | A | MoCap ソース設計が完全化。前回 B+ → A |
| Graceful Degradation | A | 変化なし |
| ErrorChannel 購読 UI | A | 変化なし |
| 参照共有シナリオ | A | Descriptor 等価判定確定で完全解消。前回 A- → A |
| Samples~ 配置 | A | テスト配置パス解消。前回 B → A |
| Inspector 主眼方針 | A | 変化なし |
| オプション要件スコープ | A | 新設章 §12 により明文化完了 |

**総合判定: GO**  
前回条件付き GO の条件であった OI-1 (UniRx 直接依存の contracts.md 合意) と OI-2 (テスト配置パス整合) がともに解消された。前回指摘 5 件はすべて解消されており、構造的欠陥なし。  
残存 open issue は軽微 2 件 (OI-7 / OI-8) であり、設計ブロックには該当せず tasks フェーズで取り込み可能な粒度である。  
tasks 生成フェーズへの移行を承認する。
