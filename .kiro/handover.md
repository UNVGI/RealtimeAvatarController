# セッション引き継ぎ: Realtime Avatar Controller 実装フェーズ着手前

## 現状

Phase 1 (Specification) 完了。全 6 Spec が requirements / design / tasks 生成・承認済み。`ready_for_implementation: true`。

### 全 Spec 状態
| Spec | req | design | tasks | ready | leaf タスク |
|------|:--:|:--:|:--:|:--:|:--:|
| project-foundation | ✅ | ✅ | ✅ | ✅ | 14 |
| slot-core | ✅ | ✅ | ✅ | ✅ | 48 |
| motion-pipeline | ✅ | ✅ | ✅ | ✅ | 25 |
| mocap-vmc | ✅ | ✅ | ✅ | ✅ | 32 |
| avatar-provider-builtin | ✅ | ✅ | ✅ | ✅ | 28 |
| ui-sample | ✅ | ✅ | ✅ | ✅ | 30 |
| **合計** | | | | | **177** |

### ドキュメント構成
- `.kiro/specs/_shared/spec-map.md` — 全 Spec 俯瞰
- `.kiro/specs/_shared/contracts.md` — 公開契約 (最終仕様確定済)
- `.kiro/specs/_shared/briefs/*.md` — 各 Spec ブリーフィング
- `.kiro/specs/<spec>/requirements.md` `design.md` `tasks.md` `validation-design.md`

## dig 4 ラウンドで確定した主要技術選定

- **Rx**: UniRx (`com.neuecc.unirx` 7.1.0, OpenUPM, R3 ではない)
- **Async**: UniTask (`com.cysharp.unitask` 2.5.10, OpenUPM)
- **OSC**: `com.hidano.uosc` 1.0.0 (npm scoped `com.hidano`, SO_REUSEADDR 有効版)
- **VMC パース実装**: EVMC4U (MIT) を参考実装として流用、帰属明記
- **Unity**: 6000.3.10f1
- **配置**: リポジトリ直下 `RealtimeAvatarController/` (Unity プロジェクト)

## 確定した設計要点

- Slot は Descriptor + Registry + Factory パターン
- `IMoCapSource` は Push 型 (UniRx `IObservable<MotionFrame>`) 、`OnError` 非発行、参照共有
- 各 Factory は `[RuntimeInitializeOnLoadMethod]` + `[UnityEditor.InitializeOnLoadMethod]` で自動登録、typeId 競合は `RegistryConflictException` throw
- `RegistryLocator` は `Interlocked.CompareExchange` 遅延初期化、`ResetForTest` 提供
- Weight は初期版で常に 1.0 (中間値は将来拡張フック)
- FallbackBehavior = `HoldLastPose` (default) / `TPose` / `Hide` (Renderer.enabled=false)
- ApplyFailure 発行主体は SlotManager (Applier は throw のみ)
- MotionFrame = sealed class (struct不採用)、timestamp = Stopwatch monotonic 秒
- MotionCache スレッドモデル = 方式 B (Interlocked.Exchange 直接書込)
- SlotSettings は SO アセット + ランタイム動的生成 (`ScriptableObject.CreateInstance`) の両対応
- Samples.UI asmdef は UniRx 直接参照の例外許容 (`.ObserveOnMainThread()` 使用のため)

## 次アクション: Phase 2 実装

### 推奨手順

1. 本セッションを `/clear` してコンテキストを解放
2. 新セッションで `/impl-run` を実行 (project-foundation から順次開始)
3. Wave 間の確認はデフォルトで有効。連続実行したい場合は `/impl-run --no-pause`
4. 特定 Spec のみ実行: `/impl-run --only <spec>`
5. 途中 Spec から再開: `/impl-run --from <spec>`

### Wave 順序 (依存グラフ厳守)

| Wave | Spec |
|:----:|------|
| 1 | project-foundation |
| 2 | slot-core |
| 3 | motion-pipeline |
| 4 | avatar-provider-builtin |
| 5 | mocap-vmc |
| 6 | ui-sample |

### 実行時注意

- 各タスクは 30 分タイムアウト (`/kiro:spec-run` 制約)
- 合計 177 タスクで実行時間は数時間〜数十時間
- Git コミットは各タスク完了時に `/kiro:spec-run` が自動実行
- Wave ごとにコンテキストが肥大化するため、可能なら Wave 単位で `/clear` + `/impl-run --from <next>` を推奨
- 実装失敗時は validation-design.md の Open Issue を参照して tasks.md 該当タスクを再検討

## Open Issue (tasks フェーズ持越し一覧)

各 Spec の `validation-design.md` 末尾に記載。主要なもの:

### slot-core
- `Inactive` API 未定義の解決
- `SlotRegistry` の `internal` 化

### project-foundation
- バージョン固定 (exact pin) のポリシー README 明文化

### motion-pipeline
- Muscles 配列イミュータビリティ方針の確定
- slot-core design.md §11.2 の Hide 記述の更新追従

### mocap-vmc
- `bindAddress` デフォルト値の requirements (`127.0.0.1`) と design (`0.0.0.0`) の差異調整

### avatar-provider-builtin
- Factory.Create の ErrorChannel null 安全性
- `BuiltinAvatarProvider._errorChannel` の null 安全性

### ui-sample
- FallbackBehavior デフォルト値 (HoldLastPose) の Inspector 表示明示
- Weight トグル閾値 (0.5) のコメント
