# Design Validation Report — avatar-provider-builtin (再検証)

> **検証日時**: 2026-04-15 (再検証)
> **前回検証日時**: 2026-04-15 (初回)
> **検証対象**: `.kiro/specs/avatar-provider-builtin/design.md`
> **参照文書**: `contracts.md`、`slot-core/design.md`、`requirements.md`
> **総合評価**: **承認 (GO)**

---

## 総合サマリ

初回検証で指摘した **Critical 1 件・Major 2 件** はすべて解消済みである。design.md は前回から大幅に改訂されており、各指摘箇所に対して明確な擬似コードと参照注記が追加された。新たに発見した問題は軽微 (Minor) 2 件であり、タスク生成フェーズの進行を妨げるものではない。

---

## 前回指摘の解消確認

### [Critical #1] ISlotErrorChannel.Publish() 参照の明示化

| 確認項目 | 結果 |
|--------|:---:|
| `contracts.md §1.7` への明示的参照が design.md に記載されたか | ✅ 解消 |
| `slot-core/design.md §3.8` への参照が記載されたか | ✅ 解消 |
| Publish API コード例が擬似コードに反映されたか | ✅ 解消 |

**解消内容**: §8 冒頭のブロック引用ノート・§8 末尾の参照表 (`contracts.md §1.7` / `slot-core/design.md §3.8`)・§5 擬似コード内コメントの 3 箇所で明記された。`contracts.md §1.7` に `void Publish(SlotError error)` が Wave A で正式追記済みという状況も明示されており、前回の「骨格に Publish() が存在しない」という不整合が完全に解消された。

**判定: RESOLVED**

---

### [Major #1] ResolveConfig() 未定義

| 確認項目 | 結果 |
|--------|:---:|
| `ResolveConfig()` ヘルパーが削除またはインライン展開されたか | ✅ 解消 |
| config 引数の設計意図 (引数優先 / `_config` フォールバック) が明記されたか | ✅ 解消 |

**解消内容**: §5 `RequestAvatar` 擬似コードで `ResolveConfig()` が削除され、`var builtinConfig = (config as BuiltinAvatarProviderConfig) ?? _config;` としてインライン展開された。さらに擬似コード直下の「config 引数の設計意図」ブロックに「引数 config を優先→null 時は `_config` にフォールバック」という方針が 3 行で明文化されており、曖昧さが完全に解消された。

**判定: RESOLVED**

---

### [Major #2] ThrowIfPrefabNull 配置とエラー発行の整合

| 確認項目 | 結果 |
|--------|:---:|
| null Prefab ガードが try ブロック内に移動されたか | ✅ 解消 |
| catch ブロックで `_errorChannel.Publish(InitFailure)` が呼ばれるようになったか | ✅ 解消 |
| §8 エラーパターン表と擬似コードの記述が整合しているか | ✅ 解消 |

**解消内容**: `ThrowIfPrefabNull()` 呼び出しが廃止され、try ブロック内で `if (builtinConfig.avatarPrefab == null) { throw new InvalidOperationException(...); }` として再実装された。catch ブロックで `_errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` が呼ばれる設計に統一された。§8 の表でも「try ブロック内でスロー → catch ブロックで Publish」と明記されており、擬似コードとエラーパターン表が完全に整合している。

**判定: RESOLVED**

---

## 追加確認事項

### Factory 自己登録 try-catch + RegistryConflict ErrorChannel Publish パターン

**判定: PASS**

§7 の `RegisterRuntime()` および `RegisterEditor()` 擬似コードで、`RegistryConflictException` を catch して `RegistryLocator.ErrorChannel.Publish(new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を呼ぶパターンが両メソッドに実装された。コメントにも `contracts.md §1.7` / `slot-core/design.md §8.1` への参照が記載されており整合している。

### contracts.md §1.7 の Publish API と design.md の参照整合

**判定: PASS**

§8 末尾の参照表に `contracts.md §1.7` — `void Publish(SlotError error)` — Wave A で正式追記済みと明記された。`slot-core/design.md §3.8` の `DefaultSlotErrorChannel` 実装への参照も追加されており、設計根拠の追跡が可能になった。

---

## Requirements Traceability (再確認)

初回検証での PASS 判定を維持。前回指摘の「Dispose 後の ReleaseAvatar 挙動が未記述」は依然として記述がないが、Req 4 AC 1〜5 の主要パスは網羅されており軽微な補足欠落の範疇である。

---

## 新発見の問題

### [Minor #1] Factory.Create() の null channel 時のエラー発行スキップ

§6 の `Create()` 擬似コードで `channel?.Publish(...)` (null 条件演算子) を使用している。`channel` が null の場合、`ArgumentException` はスローされるがエラーチャネルへの発行がサイレントにスキップされる。

- §6「エラーチャネル解決順序」で「null ならエラーログ省略」と明記されており設計上の矛盾はない
- ただし Req 8 AC 3「例外スロー前に `ISlotErrorChannel` へ発行」という文言との乖離が軽微に残る
- `RegistryLocator.ErrorChannel` が常に非 null であることを前提とすれば実運用上は問題ない

**対応推奨**: タスク生成フェーズを妨げないが、実装時に `channel` が null のケースの挙動を `Debug.LogError` フォールバックとして明記することを推奨。

### [Minor #2] `_errorChannel` の null 安全性が一部不明確

§5 `RequestAvatar()` の try ブロック外 (config 解決部) で `_errorChannel.Publish(...)` を直接呼び出している。コンストラクタが `errorChannel = null` かつ `RegistryLocator.ErrorChannel` も未設定の場合、`NullReferenceException` が発生する可能性がある。

- §6 の Factory `Create()` では `channel = _errorChannel ?? RegistryLocator.ErrorChannel` としており null を意識しているが、`BuiltinAvatarProvider` 自体の `_errorChannel` フィールドは null 代入が禁止されていない
- テスト時は `RegistryLocator.OverrideErrorChannel()` で注入するとあり (§8)、実際の問題は限定的
- 実装フェーズでコンストラクタの null ガードまたは null 条件演算子による保護を推奨

---

## 検証結果サマリ

| 観点 | 判定 | 重大度 | 変化 |
|------|:---:|:---:|:---:|
| Requirements Traceability | PASS | — | 維持 |
| Contract Compliance | PASS | — | 向上 |
| BuiltinAvatarProviderConfig 設計 | PASS | — | 維持 |
| 同期/非同期 API 実装 | PASS | — | 維持 |
| Factory キャストロジック・ResolveConfig | **PASS** | — | ✅ 解消 |
| 1 Slot 1 インスタンス原則 | PASS | — | 維持 |
| Factory 自動登録 + RegistryConflict 対応 | **PASS** | — | ✅ 向上 |
| Addressable 拡張余地 | PASS | — | 維持 |
| ErrorChannel 連携 (ThrowIfPrefabNull 整合) | **PASS** | — | ✅ 解消 |
| ISlotErrorChannel.Publish() 参照整合 | **PASS** | — | ✅ 解消 |
| null channel 時のエラー発行スキップ | Minor | Low | 新発見 |
| `_errorChannel` null 安全性 | Minor | Low | 新発見 |

### 前回指摘の解消状況

| # | 重大度 | 内容 | 状態 |
|:---:|:---:|------|:---:|
| 1 | Critical | `ISlotErrorChannel.Publish()` 参照の明示化 | ✅ RESOLVED |
| 2 | Major | `ResolveConfig()` 未定義 → インライン展開 | ✅ RESOLVED |
| 3 | Major | `ThrowIfPrefabNull` try ブロック外配置 | ✅ RESOLVED |

**すべての前回指摘が解消済み。新発見は Minor 2 件のみ。タスク生成フェーズへの進行を承認する。**

---

*本レポートは `.kiro/specs/avatar-provider-builtin/validation-design.md` として手動再検証により上書き生成された。*
