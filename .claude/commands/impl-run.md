---
description: Execute implementation waves for all RealtimeAvatarController specs in dependency order via /kiro:spec-run
allowed-tools: Read, Bash, Glob, Grep, Skill
argument-hint: [--from <spec>] [--only <spec>] [--no-pause]
---

# Implementation Wave Runner (全 Spec 順次実行)

RealtimeAvatarController プロジェクトの 6 Spec を依存グラフ順に `/kiro:spec-run` で順次実行する。

## 引数
- `--from <spec>`: 指定 Spec から開始 (前の Wave はスキップ)
- `--only <spec>`: 指定 Spec のみ実行
- `--no-pause`: Wave 間の確認を省略し連続実行 (デフォルトは各 Wave 完了後に確認)

## 事前チェック

次を実行する前に以下を検証:

1. 全 6 Spec の `spec.json.ready_for_implementation` が `true` であることを確認:
   - `.kiro/specs/slot-core/spec.json`
   - `.kiro/specs/project-foundation/spec.json`
   - `.kiro/specs/motion-pipeline/spec.json`
   - `.kiro/specs/mocap-vmc/spec.json`
   - `.kiro/specs/avatar-provider-builtin/spec.json`
   - `.kiro/specs/ui-sample/spec.json`

2. いずれかが `ready_for_implementation: false` の場合は実行を中止し、理由と該当 Spec を報告。

3. `~/.claude/commands/kiro/spec-run.md` (または `.claude/commands/kiro/spec-run.md`) が存在することを確認。無ければ実行を中止。

## Wave 定義

依存グラフ:
```
project-foundation (基盤)
    ↓
slot-core (公開 API)
    ↓        ↓
motion-pipeline   avatar-provider-builtin
    ↓
mocap-vmc
    ↓
ui-sample
```

実行順序:

| Wave | Spec | 推定タスク数 | 備考 |
|:----:|------|:---:|------|
| 1 | `project-foundation` | 14 | Unity プロジェクト・UPM・asmdef 計 20 本 |
| 2 | `slot-core` | 48 | 公開 API 本体 (Config/Descriptor/Registry/Locator/ErrorChannel/SlotManager) |
| 3 | `motion-pipeline` | 25 | MotionFrame / MotionCache / HumanoidMotionApplier |
| 4 | `avatar-provider-builtin` | 28 | Config / Provider / Factory |
| 5 | `mocap-vmc` | 32 | VmcMoCapSource / OSC / Factory |
| 6 | `ui-sample` | 30 | CustomEditor / デモシーン (テストは任意) |

※ Wave 3 と 4 は依存上並列可だが、`/kiro:spec-run` の逐次実行モデルに合わせて連続実行する。

## 実行手順

`--only` 指定時は当該 Spec のみ実行。`--from` 指定時は当該 Spec から順次実行。それ以外は Wave 1 から実行。

各 Wave について:

1. Wave 開始を報告: `"=== Wave N: <spec> 開始 ==="`
2. Skill ツールで `kiro:spec-run <spec>` を呼び出す
3. 完了を待機
4. Wave 完了サマリ (kiro:spec-run の結果テーブル) を受け取る
5. 任意の失敗があれば:
   - `--no-pause` 指定時: FAIL リストを記録して次の Wave へ
   - それ以外: ユーザに継続確認 (FAIL タスクを列挙し、続行/停止を問う)
6. `--no-pause` 未指定時は Wave 間で `"続行してよいか?"` を確認

## 最終サマリ

全 Wave 完了後、以下のテーブルを表示:

| Wave | Spec | 実行タスク数 | OK | FAIL | TIMEOUT | 所要時間 |
|:----:|------|:---:|:---:|:---:|:---:|:---:|

失敗がある場合の次ステップ候補:
- 該当 Spec の `.kiro/specs/<spec>/tasks.md` を再点検
- 個別に `/kiro:spec-run <spec>` を再実行
- `/kiro:validate-impl <spec>` で実装検証

## 注意事項

- 各タスクは 30 分タイムアウト (`/kiro:spec-run` の制約)
- 全タスク合計 177。最大実行時間の目安は数時間〜数十時間
- コンテキストが長時間の運用で圧迫される可能性あり。Wave 間で手動 `/clear` を挟む運用も妥当
- Git コミットは各タスク内部で `/kiro:spec-run` が自動実行する
- `--no-pause` は CI / 夜間実行向け。通常運用は Wave 間の確認を推奨

## 依存スキル

- `/kiro:spec-run <feature>` — 各 Spec の tasks をバッチ実行する
