# design.md 品質検証レポート

> **Spec**: `motion-pipeline`
> **検証対象**: `.kiro/specs/motion-pipeline/design.md`
> **参照資料**: `requirements.md`, `contracts.md`, `slot-core/design.md`
> **検証日**: 2026-04-15
> **検証者**: design validator (手動検証 / ステップ B 経路)

---

## 総合評価

| 項目 | 評価 |
|------|:----:|
| 要件トレーサビリティ | ✅ 合格 (軽微な指摘あり) |
| 契約整合性 | ✅ 合格 (1 件の差異あり) |
| MotionFrame 設計 | ✅ 合格 |
| MotionCache スレッドモデル | ✅ 合格 |
| Weight 適用仕様 | ⚠️ 条件付き合格 (設計上の曖昧さ) |
| FallbackBehavior 実装 | ⚠️ 条件付き合格 (1 件の不整合) |
| HumanoidMotionApplier | ✅ 合格 |
| ErrorChannel 連携 | ⚠️ 条件付き合格 (責務帰属の差異) |
| **総合判定** | **条件付き GO** |

重大な GO-STOP 案件は存在しない。以下に挙げる指摘事項を確認・解消したうえで tasks フェーズへ進むことを推奨する。

---

## 1. 要件トレーサビリティ

### 1.1 カバレッジマトリクス

| Req | タイトル | design.md の対応章 | 判定 |
|-----|---------|------------------|:----:|
| Req 1 | MotionFrame Humanoid 中立表現 | §3.3〜3.4, §4.1〜4.6 | ✅ |
| Req 2 | MotionFrame 基底型設計 | §3.3, §4.2〜4.5 | ✅ |
| Req 3 | Generic 形式への拡張余地 | §3.5, §3.6 | ✅ |
| Req 4 | Slot 単位の内部キャッシュと Push 型購読 | §3.8, §5 | ✅ |
| Req 5 | Weight に従ったモーション適用 | §6 | ⚠️ (指摘 #4) |
| Req 6 | Humanoid アバターへの適用層 | §3.7, §7 | ✅ |
| Req 7 | Unity メインスレッド制約 | §10 | ✅ |
| Req 8 | ランタイム中の MoCap 切替 | §5.4, §11.3 | ✅ |
| Req 9 | ランタイム中のアバター切替 | §7.3 | ✅ |
| Req 10 | 1 対多 / 多対多 応用シナリオ | §2.2〜2.3, §5.3 | ✅ |
| Req 11 | アセンブリ / 名前空間境界と UniRx 依存 | §3.1, §12 | ⚠️ (指摘 #5) |
| Req 12 | Applier エラー時の Fallback 挙動 | §8 | ⚠️ (指摘 #3) |
| Req 13 | Applier エラー通知 | §9 | ⚠️ (指摘 #6) |
| Req 14 | テスト戦略と asmdef 構成 | §12, §13 | ✅ |

### 1.2 未カバー / 不十分な箇所

- **Req 5 AC4 (範囲外クランプ)**: §6.1 テーブルでクランプ動作は記載されているが、§6.2 のコードサンプルが `Mathf.Clamp01` を呼び出す責務を「SlotManager (または上位コンポーネント)」に帰属させており、`IMotionApplier.Apply()` 内部でもクランプが行われるかどうかが曖昧 → 指摘 #4 参照。

---

## 2. 契約整合性 (contracts.md 2.2 章 / slot-core design.md)

### 2.1 contracts.md 2.2 章との整合

| 確認項目 | contracts.md 2.2 章 | design.md | 判定 |
|---------|---------------------|-----------|:----:|
| `MotionFrame` 型種別 | `abstract class` | `abstract class` | ✅ |
| `HumanoidMotionFrame` 型種別 | `sealed class` | `sealed class` | ✅ |
| `Muscles` 型 | `float[]` | `float[]` | ✅ |
| 無効フレーム表現 | `Muscles.Length == 0` | `Muscles.Length == 0` + `IsValid` | ✅ |
| `IsValid` プロパティ | 定義あり | 定義あり | ✅ |
| `CreateInvalid` ファクトリ | 定義あり | 定義あり | ✅ |
| `Timestamp` 型・算出式 | `double`, Stopwatch ベース | 一致 | ✅ |
| `WallClock` フィールド | 初期版未実装 (コメントアウト) | 同じ | ✅ |
| `SkeletonType` 列挙体 | `Humanoid`, `Generic` | 一致 | ✅ |
| `GenericMotionFrame` | 抽象プレースホルダー | 一致 | ✅ |
| スレッド安全実装方式 | 方式 B (Interlocked.Exchange) | 方式 B 選定 | ✅ |

**contracts.md 2.2 章との整合は完全。**

### 2.2 slot-core design.md の `IMoCapSource` シグネチャとの整合

| 確認項目 | slot-core design.md §3.1 | motion-pipeline design.md | 判定 |
|---------|--------------------------|---------------------------|:----:|
| `MotionStream` の型 | `IObservable<MotionFrame>` | 参照のみ (購読して使用) | ✅ |
| `ObserveOnMainThread()` 推奨 | slot-core §3.1 の doccomment「購読側は .ObserveOnMainThread() でメインスレッドに同期すること」 | design.md §5.1 で方式 B (受信スレッド直接書込) を選定。`ObserveOnMainThread()` を使わない設計 | ⚠️ (指摘 #1) |
| マルチキャスト化の責務 | `IMoCapSource` 実装または `MoCapSourceRegistry` のラッパーで行う | §2.2「slot-core / mocap-vmc 側で保証」と記載 | ✅ |
| `OnError` 非発行 | ストリームは `OnError` を発行しない | 明示的な言及なし | ⚠️ (指摘 #2) |
| `Dispose()` 禁止 | Slot 側から直接呼び出し禁止 | §5.5 で明示 | ✅ |

---

## 3. MotionFrame 設計の妥当性

### 3.1 class vs struct 選定

**判定: ✅ 十分**

§4.5 に選定理由が表形式で記載されている。

| 観点 | 評価 |
|------|------|
| 継承要件 (Humanoid / Generic 統一) | struct 不可 → class 採用 理由として明示 |
| IObservable ストリームでのボックス化回避 | 明示 |
| Muscles 配列保持 (参照型フィールド) | 明示 |
| null チェックによる未到着判定 | 明示 |

struct 案の否定理由が網羅的かつ具体的。設計意思決定として十分。

### 3.2 イミュータブル設計

**判定: ✅ 十分**

- `MotionFrame`: `public double Timestamp { get; }` — get only プロパティ、コンストラクタで完全初期化
- `HumanoidMotionFrame`: 全プロパティ get only。`Muscles` は `float[]` 参照型であり、配列要素への外部書き換えは技術的には可能だが、§4.5 に「全プロパティは readonly であり、コンストラクタで完全初期化する。外部からの書き換えは不可能。」と明記されている。

**軽微な懸念**: `Muscles` は `float[]` であり、プロパティ参照を取得した呼び出し元が `frame.Muscles[0] = 0f;` と書けば内容を変更できる。設計上これを「許容する」のか「防ぐ」のかの方針が明記されていない。コピーオンリード等を検討するかどうかを tasks フェーズで判断することを推奨する。ただしリアルタイム制御での毎フレームコピーはパフォーマンス上の懸念があるため、「許容する」方針でも問題ない。

### 3.3 Timestamp 打刻仕様

**判定: ✅ 十分**

§4.2〜4.3 に算出式・打刻タイミング・スレッド安全性・プロセス間非互換の制約が明記されている。コードサンプルも添付されており実装に迷いはない。

---

## 4. MotionCache スレッドモデル

### 4.1 方式選定理由

**判定: ✅ 十分**

§5.1 に方式 A / B の比較表と、方式 B 採用理由が記載されている。「キューの蓄積なし」「最新フレームのみ保持」「フレームドロップ許容」の設計方針が明確。

ただし要件 Req 4 AC4 では「方式 A / B / C」と記載されているが、requirements.md に方式 C の定義はない (requirements 段階の表記揺れ)。design.md は方式 A / B のみ扱っており、方式 C への言及がないが、これは要件側の記述ミスであり design.md の問題ではない。

### 4.2 スレッド安全性の実装詳細

**判定: ✅ 十分**

§5.2 に `Interlocked.Exchange` (書込) と `Volatile.Read` (読出) の組合せが明示され、コードサンプルが提供されている。§10.2 に操作×スレッド×使用プリミティブのマトリクスが整備されており、実装者が迷う余地はない。

### 4.3 購読解除フロー

**判定: ✅ 十分**

§5.4 に状態遷移表 (初回 SetSource / 切替 SetSource / null SetSource / Dispose) がすべて網羅されている。`IMoCapSource.Dispose()` 禁止も §5.5 で明示。

---

## 5. Weight 適用仕様

### 5.1 初期版 {0.0, 1.0} の二値動作

**判定: ⚠️ 設計上の曖昧さあり (指摘 #4)**

§6.1 テーブルには `0.0` → skip、`1.0` → 完全適用、範囲外 → クランプが記載されている。

**問題**: §6.2 のコードサンプルでクランプ処理 (`Mathf.Clamp01`) の責務を「SlotManager (または上位コンポーネント)」に帰属させている。一方、§3.6 の `IMotionApplier.Apply()` doccomment には「`weight` (0.0〜1.0、範囲外はクランプ)」と記載され、Applier 側でもクランプするかのように読める。

**どちらが正しいか (二重クランプなのか) が未確定**。

Req 5 AC4 は「`IMotionApplier` shall 値を `0.0〜1.0` にクランプして処理を継続する」と定めており、Applier 側でクランプするのが要件の意図である。設計ドキュメントで責務帰属を一本化すること。

### 5.2 将来の中間値拡張ポイント

**判定: ✅ 十分**

§6.3 に「インターフェース変更なしに実装変更で対応可能」と明記されており、拡張ポイントは確保されている。

---

## 6. FallbackBehavior 実装

### 6.1 各挙動の実装詳細

**判定: ✅ 十分**

§8.2〜8.4 に HoldLastPose / TPose / Hide の各実装コードが提供されている。

- **HoldLastPose**: `_lastGoodPose` を正常 Apply 時のみ更新し、エラー時は何もしない設計。明確。
- **TPose**: `HumanPoseHandler.SetHumanPose()` で全 Muscle 0 / Root 初期値にリセット。明確。
- **Hide**: `Renderer.enabled = false`。全 Renderer をイテレート。null チェックあり。明確。
- **Hide からの復帰**: `_isFallbackHiding` フラグを使用し、次フレームの正常 Apply 後に `RestoreRenderers()` を呼び出す設計。Req 12 AC5 に準拠。

### 6.2 slot-core design.md §11.2 との不整合

**判定: ⚠️ 不整合あり (指摘 #3)**

§8.4 の注記に以下の記述がある:

> slot-core design.md の 11.2 章では `Hide` の実装として `GameObject.SetActive(false)` が言及されているが、motion-pipeline 側の確定仕様は **`Renderer.enabled = false`** とする。

これは motion-pipeline design.md が意図的に異なる実装を選んでいることを示している。設計判断自体は Req 12 AC4「GameObject 自体は破棄せず生存させる」に沿っており妥当である。

ただし **slot-core design.md §11.2 が更新されていない**という状態は、将来の混乱を招くリスクがある。tasks フェーズで slot-core design.md への反映を検討すること (motion-pipeline の実装責務ではないが、跨がり管理が必要)。

### 6.3 SlotSettings.fallbackBehavior への参照経路

**判定: ✅ 十分**

§8.5 に `Apply(frame, weight, settings) → settings.fallbackBehavior → FallbackBehavior enum` の参照経路が図示されており、`FallbackBehavior` の定義責務が `slot-core` (`RealtimeAvatarController.Core`) にあることも明記されている。

---

## 7. HumanoidMotionApplier

### 7.1 HumanPoseHandler のライフサイクル

**判定: ✅ 十分**

§7.2 に `SetAvatar()` 呼び出し時の `InitializePoseHandler()` コードが提供されており、旧 `HumanPoseHandler` の `Dispose()` → 新 Handler 生成のフローが明確。`HumanoidMotionApplier.Dispose()` で `HumanPoseHandler` が解放される旨も §3.7 doccomment に記載されている。

### 7.2 アバター切替時の再初期化フロー

**判定: ✅ 十分**

§7.3 に状態遷移フロー (旧 PoseHandler 破棄 → Renderer キャッシュクリア → _isFallbackHiding リセット → null / non-null 分岐) が図示されている。切替中のフレームスキップ動作 (Req 9 AC4) も「切替中に `_poseHandler == null` の状態に到達したフレームは適用をスキップ」と文書化されている。

### 7.3 Humanoid 非対応時の例外仕様

**判定: ✅ 十分**

§7.4 に発生箇所×例外型×メッセージ例のマトリクスが整備されている。3 ケース (Animator なし / Humanoid 非対応 / PoseHandler 未初期化) の挙動がそれぞれ定義されており、Req 6 AC3 に対応している。

---

## 8. ErrorChannel 連携

### 8.1 ApplyFailure 発生経路

**判定: ✅ 十分**

§9.1 にフォールバック実行 → `Publish()` の順序が明示されている。シーケンス図 §11.2 にも流れが可視化されている。

### 8.2 無効フレームと ApplyFailure の区別

**判定: ✅ 十分**

§9.3 の「発行対象外のケース」テーブルに以下の区別が明記されている:

| ケース | 発行有無 |
|--------|:-------:|
| `LatestFrame == null` | しない |
| `IsValid == false` (Muscles.Length == 0) | しない |
| `HumanoidMotionFrame` 以外の型 | しない |
| `Apply()` 内実行時例外 | する |

Req 4 AC6 / Req 13 AC5 との整合は完全。

### 8.3 ErrorChannel の取得責務 (指摘 #6)

**判定: ⚠️ 要確認**

contracts.md 1.7 章の「エラー通知の責務分担」テーブルには:

> Applier エラー | `SlotManager` が `ISlotErrorChannel` に発行 (フォールバック後)

と記載されているが、motion-pipeline design.md §9.1〜9.2 では `HumanoidMotionApplier` 自身が `_errorChannel.Publish()` を直接呼び出す設計になっている。

contracts.md と motion-pipeline design.md の記述が矛盾している。どちらが最終確定仕様かを確認し、contracts.md または design.md のどちらかを更新すること。

motion-pipeline design.md の設計 (Applier が直接 Publish) は Req 13 AC1〜4 に合致しており、こちらが要件に近い。contracts.md 1.7 章の記述は Wave A 時点の暫定案である可能性が高い。contracts.md の更新を推奨する。

---

## 9. 個別指摘事項

### 指摘 #1: ObserveOnMainThread() 非使用の明示的な正当化不足

**優先度**: 中

**箇所**: §5.1 方式選定

**内容**: slot-core design.md §3.1 の `IMoCapSource` doccomment には「購読側は `.ObserveOnMainThread()` でメインスレッドに同期すること」と明記されている。motion-pipeline design.md は方式 B (受信スレッド直接書込 + `Interlocked.Exchange`) を選定しており、`ObserveOnMainThread()` を使用しない。

この選択自体は適切であるが、**なぜ slot-core の推奨に従わないか** (= なぜ方式 A を採用しないか) の説明が §5.1 の表に「UniRx キュー経由のため高頻度フレームでキューが積み重なる可能性がある」とのみ記載されており、slot-core の doccomment との関係が読み取れない。

**対応案**: §5.1 または §2.2 に「slot-core §3.1 の `.ObserveOnMainThread()` 推奨は、方式 A 相当の設計ガイダンスであるが、motion-pipeline は方式 B を採用するため適用しない」旨を補足すること。

---

### 指摘 #2: OnError 非発行方針の購読側での扱い

**優先度**: 低

**箇所**: §5 全体

**内容**: contracts.md 2.1 章および slot-core design.md §3.1 では「`MotionStream` は `OnError` を発行しない」と明記されているが、motion-pipeline design.md には `OnError` 非発行の前提とその取り扱い (= `OnError` ハンドラを登録しないことが許容される) への言及がない。

**対応案**: §5.4 購読ライフサイクルに「`MotionStream` は `OnError` を発行しない (contracts.md 2.1 章) ため、購読時に `onError` コールバックは省略してよい」旨を追記することを推奨する。

---

### 指摘 #3: slot-core design.md §11.2 との `Hide` 実装不整合

**優先度**: 中

**箇所**: §8.4 注記

**内容**: motion-pipeline は `Renderer.enabled = false` を採用し、slot-core design.md は `GameObject.SetActive(false)` に言及している。motion-pipeline の選択は Req 12 AC4 に準拠しており実装上正しい。

**対応案**: slot-core design.md §11.2 を「`motion-pipeline` の `Hide` 実装は `Renderer.enabled = false` を採用し、GameObject 自体は生存させる」と更新すること。ただしこれは slot-core の責務であるため、tasks フェーズでの作業項目として登録することを推奨する。

---

### 指摘 #4: Weight クランプ責務の二重定義

**優先度**: 中〜高

**箇所**: §3.6 `IMotionApplier.Apply()` doccomment / §6.2 コードサンプル

**内容**: §3.6 doccomment では「weight (0.0〜1.0、範囲外はクランプ)」とあり Applier 内クランプが示唆されている一方、§6.2 コードサンプルでは `SlotManager` (上位) が `Mathf.Clamp01` を呼ぶ設計になっている。Req 5 AC4 は `IMotionApplier` がクランプする旨を規定している。

**対応案**:
- Applier 側でクランプする (Req 5 AC4 通り) と確定し、§6.2 コードサンプルの `SlotManager` 側クランプを「参考情報」として位置づける注釈を加えるか、
- 上位コンポーネントのみがクランプすることを確定し、§3.6 doccomment を修正する

どちらかに一本化すること。

---

### 指摘 #5: asmdef の UniRx 直接参照方針

**優先度**: 低〜中

**箇所**: §12.1 asmdef 設定テーブル

**内容**: §12.1 テーブルでは `RealtimeAvatarController.Motion` asmdef の参照アセンブリに `UniRx` が含まれており、`UniRx` を直接参照していることが確定仕様として記載されている。一方 Req 11 AC4 では「UniRx の直接参照 (asmdef の references への `UniRx` 追加) が必要かどうかは design フェーズで確定する」とされており、確定を要求していた。

design.md は §12.1 末尾の注記「`MotionCache` が `IObservable<MotionFrame>.Subscribe()` を呼び出すため、`RealtimeAvatarController.Motion` の asmdef に `UniRx` を直接参照として追加する」と結論を述べており、要件の確定要求に応答できている。

**ただし**: `RealtimeAvatarController.Core` が UniRx を依存として持ち、`Motion` が `Core` を参照している場合でも、UniRx 型 (`Subject<T>` 等) を使用しないのであれば直接参照は不要なケースもある。`MotionCache` が `Subscribe()` のみで UniRx 型を型名直接参照していなければ `Core` 経由の間接依存で足りる。これは実装詳細に依存するため、tasks フェーズで最終確認を推奨する。

---

### 指摘 #6: ErrorChannel 発行責務の contracts.md との不整合

**優先度**: 高

**箇所**: §9.2 / contracts.md §1.7

**内容**: 上記「8.3 ErrorChannel の取得責務」で詳述済み。

**対応案**: contracts.md §1.7「エラー通知の責務分担」テーブルを「`HumanoidMotionApplier` が `ISlotErrorChannel` に発行 (フォールバック後)」に更新することを推奨する。これは slot-core の contracts.md への修正であるため、slot-core 担当または共同レビューでの合意が必要。

---

## 10. Open Issues

以下は現時点で未解決または意図的に tasks フェーズに委ねられている事項。

| # | 内容 | 影響 |
|---|------|------|
| OI-1 | `Muscles` 配列のディープイミュータビリティ方針 (コピーするか許容するか) | パフォーマンスと安全性のトレードオフ |
| OI-2 | `RegistryLocator.ErrorChannel` の静的プロパティが contracts.md / slot-core design.md に確定されているか | `HumanoidMotionApplier` がアクセスする静的経路の存在確認 |
| OI-3 | テスト用 Humanoid Prefab の配置パス (`Tests/PlayMode/Motion/Fixtures/`) | PlayMode テスト実行時の前提条件 |
| OI-4 | slot-core design.md §11.2 の `Hide` 実装記述更新 | Spec 間整合性 |
| OI-5 | contracts.md §1.7 の Applier エラー発行責務の更新 | Spec 間整合性 |

---

## 11. 最終評価サマリー

**判定: 条件付き GO**

design.md の品質は全体として高く、アーキテクチャの意思決定・スレッドモデル・Fallback 実装・ErrorChannel 連携はいずれも明確に設計されている。contracts.md 2.2 章との整合は完全であり、slot-core の `IMoCapSource` シグネチャとの接続も明示されている。

以下の 2 点は tasks フェーズ前に解消することを推奨する:

1. **指摘 #4 (Weight クランプ責務の一本化)**: 実装者の判断を分岐させる曖昧さであり、テストコードの期待値にも影響する。
2. **指摘 #6 (contracts.md との Applier 発行責務不整合)**: 他 Spec 実装者が contracts.md を参照した場合に設計を誤解するリスクがある。

指摘 #1〜#3 および #5 は実装への影響は軽微であり、tasks または実装フェーズでの対処で問題ない。
