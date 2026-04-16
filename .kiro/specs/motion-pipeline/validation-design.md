# design.md 品質検証レポート (再検証)

> **Spec**: `motion-pipeline`
> **検証対象**: `.kiro/specs/motion-pipeline/design.md`
> **参照資料**: `requirements.md`, `contracts.md`, `slot-core/design.md`
> **初回検証日**: 2026-04-15
> **再検証日**: 2026-04-15
> **検証者**: design validator (手動検証 / ステップ B 経路)

---

## 総合評価

| 項目 | 前回評価 | 今回評価 |
|------|:-------:|:-------:|
| 要件トレーサビリティ | ✅ 合格 (軽微な指摘あり) | ✅ 合格 |
| 契約整合性 | ✅ 合格 (1 件の差異あり) | ✅ 合格 |
| MotionFrame 設計 | ✅ 合格 | ✅ 合格 |
| MotionCache スレッドモデル | ✅ 合格 | ✅ 合格 |
| Weight 適用仕様 | ⚠️ 条件付き合格 | ✅ 合格 |
| FallbackBehavior 実装 | ⚠️ 条件付き合格 | ✅ 合格 (跨ぎ注記継続) |
| HumanoidMotionApplier | ✅ 合格 | ✅ 合格 |
| ErrorChannel 連携 | ⚠️ 条件付き合格 | ✅ 合格 |
| **総合判定** | **条件付き GO** | **GO** |

前回の「条件付き GO」から **GO** に格上げ。主要 5 指摘のうち 5 件すべてが解消または適切な根拠付きで処理されている。残存オープンイシューはすべて tasks フェーズ以降での対処で問題ない。

---

## 前回指摘 解消状況テーブル

| # | 指摘内容 | 優先度 | 解消状況 | 解消根拠 (design.md 該当箇所) |
|---|---------|:------:|:-------:|-------------------------------|
| #1 | ObserveOnMainThread() 非使用の正当化不足 | 中 | ✅ **解消** | §5.1 に専用ブロックを追加。slot-core §3.1 推奨は「方式 A 相当のガイダンス」と明言し、方式 B 採用の 3 理由 (キュー蓄積リスク・最新フレーム優先・フレームドロップ許容) を明記 |
| #2 | OnError 非発行方針の購読側扱い (低優先) | 低 | ✅ **解消** | §5.4 末尾注記「`MotionStream` は `OnError` を発行しない (contracts.md §2.1) ため `onError` コールバックは省略してよい」を追記 |
| #3 | slot-core §11.2 との Hide 実装不整合 | 中 | ✅ **適切処理** | §8.4 注記で `Renderer.enabled = false` を確定仕様として明記。tasks フェーズで slot-core 側更新を対応項目として登録する旨を明記。本 Spec 範囲での対処は完了 |
| #4 | Weight クランプ責務の二重定義 | 中〜高 | ✅ **解消** | §3.6 doccomment を「**呼び出し元 (SlotManager) が事前に Mathf.Clamp01 でクランプした値を渡すこと。Applier 内部ではクランプ処理を行わない。**」に修正。§6.2 も「SlotManager が担う / 二重クランプ禁止」と明記。Req 5 AC4 との整合根拠も §6.2 末尾注記に記載 |
| #5 | asmdef UniRx 直接参照方針の確定 (低〜中) | 低〜中 | ✅ **解消** (前回同様) | §12.1 末尾注記「MotionCache が Subscribe() を呼び出すため UniRx を直接参照として追加する」で確定済み |
| #6 | ErrorChannel 発行責務の contracts.md 不整合 | 高 | ✅ **解消** | §9.1 / §9.2 で「発行主体は SlotManager」と明記。§8.1 サンプルコードが「Applier throw → SlotManager catch → Publish」パターンに統一。§9.1 末尾に「この責務分担は contracts.md §1.7 に準拠する」と明記 |

**OI-2 (RegistryLocator.ErrorChannel 経路)**: §9.2 に専用セクションを新設。`RegistryLocator.ErrorChannel` 静的プロパティ経由での `Publish()` 呼び出し、遅延初期化のスレッドセーフ性、テスト時のモック差し替え手順 (`OverrideErrorChannel` / `ResetForTest`) を明記。経路が完全に可視化されている。

---

## 追加確認項目

### HumanoidMotionApplier コンストラクタから ISlotErrorChannel が削除されたか

**確認結果: ✅ 確認済み**

§3.7 コンストラクタシグネチャ:
```csharp
public HumanoidMotionApplier(string slotId);
```
`ISlotErrorChannel` パラメータは存在しない。§7.1 内部フィールド一覧 (`_poseHandler`, `_lastGoodPose`, `_renderers`, `_isFallbackHiding`, `_slotId`) に `_errorChannel` は含まれていない。§7.1 注記「**_errorChannel を持たない設計**: ApplyFailure の ErrorChannel 発行責務は SlotManager が担う」と明記されており、責務分離が徹底されている。

### §8.1 サンプルコードが「Applier throw → SlotManager catch」になっているか

**確認結果: ✅ 確認済み**

§8.1 に 2 つのコードサンプルが明確に区別されて掲載されている:

1. **HumanoidMotionApplier 側** — `ApplyInternal()` 呼び出し後に正常完了時のみ `_lastGoodPose` を更新。Applier は catch しない設計であることが明示されている。

2. **SlotManager 側** — `try { applier.Apply(...) } catch (Exception ex) { ExecuteFallback(...); RegistryLocator.ErrorChannel.Publish(...); }` の構造が明記されている。

§11.2 シーケンス図にも「Applier → SlotManager: 例外を再スロー (Applier は catch しない)」「SM → EC: RegistryLocator.ErrorChannel.Publish(SlotError{ApplyFailure, ...})」が図示されており、両者が一貫している。

### 新たな記述矛盾の確認

以下の項目について追加チェックを実施した。矛盾は発見されなかった。

| 確認項目 | 結果 |
|---------|:----:|
| §3.6 doccomment と §6.2 コードサンプルのクランプ責務が一致するか | ✅ 一致 (両者ともに「SlotManager がクランプ / Applier はクランプしない」) |
| §8.1 と §11.2 シーケンス図の Fallback / Publish 順序が一致するか | ✅ 一致 (Fallback が先、Publish が後) |
| §9.1 と §9.2 の発行主体が一致するか | ✅ 一致 (ともに SlotManager) |
| §7.1 フィールド一覧と §3.7 コンストラクタシグネチャが一致するか | ✅ 一致 |
| §5.4 OnError 注記と §13.3 テスト方針が整合するか | ✅ 整合 (テスト方針に `onError` コールバック省略の旨が反映) |
| §10.1 スレッド境界図と §10.2 規約テーブルが整合するか | ✅ 整合 |

---

## 1. 要件トレーサビリティ (再確認)

### 1.1 カバレッジマトリクス

| Req | タイトル | design.md の対応章 | 判定 |
|-----|---------|------------------|:----:|
| Req 1 | MotionFrame Humanoid 中立表現 | §3.3〜3.4, §4.1〜4.6 | ✅ |
| Req 2 | MotionFrame 基底型設計 | §3.3, §4.2〜4.5 | ✅ |
| Req 3 | Generic 形式への拡張余地 | §3.5, §3.6 | ✅ |
| Req 4 | Slot 単位の内部キャッシュと Push 型購読 | §3.8, §5 | ✅ |
| Req 5 | Weight に従ったモーション適用 | §6 | ✅ (前回指摘 #4 解消) |
| Req 6 | Humanoid アバターへの適用層 | §3.7, §7 | ✅ |
| Req 7 | Unity メインスレッド制約 | §10 | ✅ |
| Req 8 | ランタイム中の MoCap 切替 | §5.4, §11.3 | ✅ |
| Req 9 | ランタイム中のアバター切替 | §7.3 | ✅ |
| Req 10 | 1 対多 / 多対多 応用シナリオ | §2.2〜2.3, §5.3 | ✅ |
| Req 11 | アセンブリ / 名前空間境界と UniRx 依存 | §3.1, §12 | ✅ (前回指摘 #5 解消) |
| Req 12 | Applier エラー時の Fallback 挙動 | §8 | ✅ (前回指摘 #3 適切処理済) |
| Req 13 | Applier エラー通知 | §9 | ✅ (前回指摘 #6 解消) |
| Req 14 | テスト戦略と asmdef 構成 | §12, §13 | ✅ |

すべての要件がカバーされている。

---

## 2. 契約整合性 (再確認)

### 2.1 contracts.md 2.2 章との整合

前回と変化なし。完全整合を確認済み。

### 2.2 slot-core design.md との整合

| 確認項目 | 前回判定 | 今回判定 | 変化 |
|---------|:-------:|:-------:|------|
| ObserveOnMainThread() 非使用の正当化 | ⚠️ | ✅ | §5.1 に逸脱理由ブロック追記で解消 |
| OnError 非発行方針の購読側扱い | ⚠️ | ✅ | §5.4 注記追記で解消 |
| Hide 実装の不整合 | ⚠️ | ✅ (適切処理) | §8.4 注記で motion-pipeline 側確定仕様を明記。slot-core 側更新は tasks 登録事項 |
| ApplyFailure 発行主体 | ⚠️ | ✅ | §9.1/§9.2 で SlotManager 発行に一本化 |

---

## 3. 残存オープンイシュー

前回の OI テーブルを再評価した。

| # | 内容 | ステータス | 対処方針 |
|---|------|:--------:|---------|
| OI-1 | `Muscles` 配列のディープイミュータビリティ方針 | 継続 | tasks フェーズで実装判断。リアルタイム制約上「許容」方針が有力 |
| OI-2 | `RegistryLocator.ErrorChannel` 静的プロパティの確定 | ✅ 解消 | §9.2 で経路・スレッド安全性・テスト手順を明記 |
| OI-3 | テスト用 Humanoid Prefab の配置パス | 継続 | tasks フェーズ前提条件として記録済み |
| OI-4 | slot-core design.md §11.2 の Hide 実装記述更新 | 継続 | tasks フェーズ作業項目として登録 |
| OI-5 | contracts.md §1.7 の Applier エラー発行責務の更新 | 継続 | §9.1 で「contracts.md §1.7 に準拠」と記述変更。contracts.md 本体の更新は slot-core 担当との合意が必要。tasks フェーズ作業項目として登録 |

OI-2 が解消し、残存 OI は 4 件 (OI-1、OI-3、OI-4、OI-5)。すべて tasks フェーズ以降での対処で問題ない。

---

## 4. 最終評価サマリー

**判定: GO**

design.md の品質は前回から顕著に向上した。前回の重大課題であった Weight クランプ責務の二重定義 (指摘 #4) と ErrorChannel 発行責務の不整合 (指摘 #6) がともに解消されており、実装者が設計意図を誤解するリスクは排除されている。

MotionCache スレッドモデル (§5.1) には slot-core 推奨からの意図的逸脱理由が専用ブロックで明記され、チームメンバーが「なぜ ObserveOnMainThread を使わないのか」を即座に理解できる。HumanoidMotionApplier のコンストラクタから `ISlotErrorChannel` が削除されており、責務分離が実装レベルで担保されている。シーケンス図 (§11.2) が「Applier throw → SlotManager catch → Fallback → Publish」の流れを可視化しており、実装フェーズで迷う余地がない。

tasks フェーズへの移行を承認する。
