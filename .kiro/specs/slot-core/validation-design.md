# Design Validation Report: slot-core (再検証)

## 検証日時
2026-04-16

## 総合評価
✅ Pass

前回検証時 (2026-04-15) の ⚠️ Conditional Pass から ✅ Pass に格上げする。主要 5 指摘項目のうち 5/5 が解消され、Wave A 追加確認 4 項目も全て解消確認済み。新たな問題は軽微・周知済み事項のみであり、Tasks フェーズへの移行を妨げない。

---

## 前回指摘の解消状況

| # | 指摘 | 解消状況 | 確認箇所 | 備考 |
|---|------|:--------:|---------|------|
| A-1 | Descriptor の `GetHashCode()` / `IEquatable<T>` 実装 | ✅ 解消 | §3.10 | 4 種 Descriptor 全て `IEquatable<T>` 実装・`GetHashCode()` (`RuntimeHelpers.GetHashCode` 使用)・`operator ==` / `!=` を完備。設計方針注記も追記済み |
| C-1 | contracts.md §1.6 と design.md §3.7 の RegistryLocator 同期 | ✅ 解消 | §3.7, §3.6, §3.7 Override* | `IFacialControllerRegistry` / `ILipSyncSourceRegistry` / `ISlotErrorChannel (ErrorChannel)` プロパティが §3.7 に追加。`OverrideFacialControllerRegistry()` / `OverrideLipSyncSourceRegistry()` / `OverrideErrorChannel()` も全て定義済み。※contracts.md は独立ファイルとして存在せず、design.md §11 が公開契約サマリを兼ねる。§3.7 と §11.1 の記述は整合している |
| E-1 | `RegistryConflict` カテゴリの ErrorChannel 発行経路 | ✅ 解消 | §4.6, §8.1 | 「Registry 自身は ErrorChannel に発行しない。呼び出し元 (Factory 自己登録コード) が try-catch で捕捉し発行する」方針が §4.6 に明文化。§8.1 の表にも同内容が明記されており完全に一本化 |
| T-1 | RegistryLocator の遅延初期化スレッド安全性 | ✅ 解消 | §3.7, §4.4 | `Interlocked.CompareExchange` パターンを全プロパティに適用。§4.4 に「`volatile` キーワードも不要 (`Interlocked.CompareExchange` が適切なメモリバリアを発行するため)」と明記 |
| T-2 | DefaultSlotErrorChannel の `Subject.Synchronize()` 適用 | ✅ 解消 | §4.3, §7.4 | §4.3 実装コードで `new Subject<SlotError>().Synchronize()` が明記。§7.4 でも「推奨」ではなく「**確定実装**」として記載済み |

---

## Wave A 追加確認項目の解消状況

| 項目 | 解消状況 | 確認箇所 | 備考 |
|------|:--------:|---------|------|
| Hide 実装の `Renderer.enabled = false` 統一 | ✅ 解消 | §11.2 FallbackBehavior | 「全 `Renderer` コンポーネントの `enabled = false` にする。`GameObject.SetActive(false)` は使用しない (motion-pipeline の確定実装と統一)」と明記。エラー解消後の `Renderer.enabled = true` 復元も記載 |
| ApplyFailure 発行主体の SlotManager 一本化 | ✅ 解消 | §4.6, §8.1, §11.2 | 「slot-core の SlotManager が ApplyFailure の唯一の発行主体となる」と §4.6 に明文化。Applier は throw するだけで ErrorChannel 参照を持たないことも確定 |
| Samples.UI UniRx 直接参照の例外ルール | ⚠️ 対象外 | — | `contracts.md` は独立ファイルとして存在しない (design.md §11 が契約文書を兼ねる)。ui-sample の asmdef 依存ルール・UniRx 直接参照の例外明記は design.md 内に記載なし。ただし ui-sample は別 Spec であり、slot-core design の範囲外である。Tasks フェーズへの影響なし |
| `ISlotErrorChannel.Publish()` の contracts 追記 | ✅ 解消 | §3.8, §11.1 | `ISlotErrorChannel.Publish(SlotError error)` が §3.8 インターフェース定義に明記。§11.1 公開契約サマリにも `Publish(SlotError)` が記載済み |

---

## 新たな問題

### [N-1] §7.2 スレッド安全性表の「推奨」表記残存 (軽微)

§7.2 テーブルの `ISlotErrorChannel.Publish()` 行に「Subject.Synchronize() でスレッドセーフ化**推奨**」と記載されている。§4.3 および §7.4 では「**確定実装**」として明記されており、§7.2 の表記のみが古い「推奨」表現のまま残存している。矛盾とまでは言えないが、Tasks フェーズ担当者が §7.2 のみを参照した場合に実装必須か任意かを誤解する可能性がある。

**対処**: Tasks フェーズでの実装タスク作成時に「Subject.Synchronize() は確定実装」と明記すること。design.md の修正は任意。

### [N-2] `Inactive` 状態への遷移 API が未定義のまま (継続課題)

§4.1 状態遷移図に `Active ⇄ Inactive` 遷移が「将来機能」として存在するが、これを引き起こす API (`InactivateSlotAsync` / `ReactivateSlotAsync` 等) の仮置きが design.md に記載されていない。前回指摘 [A-2] と同件。初期版スコープ外であることは認識されているが、API 名の仮置きが未実施。

**対処**: Tasks フェーズで「将来 API 予約 (stubなし)」として一行コメント追記を検討。ブロッカーではない。

### [N-3] `SlotRegistry` の公開スコープ記述 (継続課題)

§9.1 のファイル一覧に `SlotRegistry.cs` が記載されているが、§3.x に公開 API シグネチャがなく internal か public かが不明。`SlotManager` XML コメントに「SlotRegistry を内包し」とあることから internal 想定と読めるが、明示的な記載がない。前回指摘 [A-3] と同件。

**対処**: Tasks フェーズで `SlotRegistry` を `internal sealed class` として実装タスク化し、`InternalsVisibleTo` によるテスト公開を考慮すること。

### [N-4] `VmcReceive` エラーの `RegistryLocator.ErrorChannel.Publish()` 呼び出しタイミング記述の微細な不整合

§8.1 テーブルで `VmcReceive` の処理方針に「受信エラーはメインスレッドに移行後 `RegistryLocator.ErrorChannel.Publish(...)` を呼ぶ」と記載されているが、§4.3 の `DefaultSlotErrorChannel` は `Subject.Synchronize()` によりワーカースレッドからの直接 `Publish()` も安全になっている。「メインスレッドに移行後」という制約は技術的に不要であり、mocap-vmc Spec の実装担当者が混乱する可能性がある。

**対処**: Tasks フェーズの引き継ぎ事項として「VmcReceive は受信スレッドから直接 Publish() しても安全である」旨を mocap-vmc Spec に伝達すること。

---

## 要件トレーサビリティ (変更なし確認)

前回レポートのトレーサビリティマトリクスに変更なし。改訂は design.md 内の実装詳細・方針確定の追記であり、要件カバー状況に影響していない。Req 13 (FallbackBehavior) の `HoldLastPose` / `TPose` / `Hide` 挙動は §11.2 で確定。フォールバック回復挙動 (Req 13.5) は motion-pipeline Wave B 合意待ちのまま (変化なし)。

---

## 契約整合性 (design.md 内部)

contracts.md は独立ファイルとして存在せず、design.md §11 が公開契約サマリを兼ねる体制に変更なし。§3.x の各インターフェース定義と §11.1 の公開 API 一覧の整合を確認した。

| 確認観点 | 整合状態 | 備考 |
|---------|---------|------|
| §3.7 RegistryLocator プロパティ ↔ §11.1 | ✅ 整合 | ProviderRegistry / MoCapSourceRegistry / FacialControllerRegistry / LipSyncSourceRegistry / ErrorChannel 全て一致 |
| §3.8 ISlotErrorChannel.Publish ↔ §11.1 | ✅ 整合 | Publish(SlotError) 記載あり |
| §4.6 RegistryConflict 発行経路 ↔ §8.1 | ✅ 整合 | 「Registry はスローのみ、呼び出し元が try-catch 発行」で一致 |
| §4.3 Subject.Synchronize() ↔ §7.4 | ✅ 整合 | 両節で「確定実装」として一致。§7.2 表のみ「推奨」残存 ([N-1] 参照) |
| §11.2 Hide = Renderer.enabled=false ↔ §3.12 FallbackBehavior | ⚠️ 軽微 | §3.12 の enum 定義コメントには「非表示にする」のみで `Renderer.enabled` の具体実装は §11.2 にのみ記載。読者が §3.12 のみを参照した場合に `SetActive` を使う可能性がある。実装タスクに明記すること |

---

## Tasks フェーズ引き継ぎ事項

1. **`Subject.Synchronize()` は確定必須実装**: `DefaultSlotErrorChannel._subject` を `new Subject<SlotError>().Synchronize()` で生成すること。§7.2 の「推奨」表記に惑わされないこと ([N-1])。

2. **`RegistryConflict` ErrorChannel 発行パターンの実装**: Factory の `RegisterRuntime()` / `RegisterEditor()` は try-catch で `RegistryConflictException` を捕捉し、`RegistryLocator.ErrorChannel.Publish(new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を呼ぶ実装パターンを必ず踏襲すること (§4.6 のサンプルコード参照)。

3. **`Hide` フォールバックは `Renderer.enabled` で実装**: `GameObject.SetActive(false)` は使用しない。エラー解消後の次フレーム正常 Apply 時に `Renderer.enabled = true` に復元する処理もペアで実装すること (§11.2)。

4. **`SlotRegistry` を `internal sealed class` として実装**: 公開スコープを `internal` に限定し、テストから参照が必要な場合は `InternalsVisibleTo` を使用すること ([N-3])。

5. **`Interlocked.CompareExchange` パターンの全プロパティ適用**: §3.7 のサンプルコードを全 5 プロパティ (`ProviderRegistry` / `MoCapSourceRegistry` / `FacialControllerRegistry` / `LipSyncSourceRegistry` / `ErrorChannel`) に適用すること。

6. **VmcReceive エラーはワーカースレッドから直接 Publish() 可能**: Subject.Synchronize() により安全。mocap-vmc Spec 担当者へ周知すること ([N-4])。

7. **UniTask EditMode テスト戦略**: `SlotManager` の非同期 API (`AddSlotAsync` / `RemoveSlotAsync`) を NUnit EditMode テストから呼ぶ際は `UniTask.ToTask()` + `async Task` テストメソッド、または `UniTask.ToCoroutine()` + `[UnityTest]` のいずれかを選択し、テストテンプレートを `Mocks/` と並べて用意すること (前回 [TE-1] 継続)。

8. **フォールバック回復挙動は motion-pipeline Wave B 合意後**: Req 13.5 (ApplyFailure 解消時の自動回復) は未定義のまま。motion-pipeline との合意が取れた時点で slot-core Tasks に追加タスクとして挿入すること。
