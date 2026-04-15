# エージェント起動手順

6 Spec を波状並列でフェーズごとに処理する。本ドキュメントは requirements フェーズ起動手順を記す。design / tasks / implementation フェーズは本手順を準用し、別途更新する。

## フェーズ全体像

| フェーズ | 並列方式 | 担当 |
|----------|----------|------|
| Phase 1: requirements | 先行波 + 並列波 | サブエージェント 6 体 (本書が対象) |
| Phase 2: design | 並列波 | サブエージェント 6 体 (別途手順) |
| Phase 3: tasks | 並列波 | サブエージェント 6 体 (別途手順) |
| Phase 4: implementation | 波状並列 (worktree 検討) | 別途手順 |

## Phase 1 requirements 起動手順

### Wave 1 (先行波): slot-core のみ起動

#### 目的
`_shared/contracts.md` の 1〜5 章 (2.2 を除く) を確定させる。以後のエージェントはこの契約を参照する。

#### 起動方法
Agent ツール (subagent_type=general-purpose) を 1 体起動し、以下の責務を与える:

- `.kiro/specs/_shared/briefs/slot-core.md` を読む
- `.kiro/specs/_shared/spec-map.md` を読む
- `.kiro/specs/_shared/contracts.md` を読む
- Skill ツールで `kiro:spec-init slot-core` を実行
- Skill ツールで `kiro:spec-requirements slot-core` を実行
- 生成された requirements.md を Brief に沿って編集
- `contracts.md` の 1〜5 章 (2.2 除く) を編集

#### 完了条件
- `.kiro/specs/slot-core/requirements.md` が存在
- `.kiro/specs/_shared/contracts.md` の 1 章・2.1 章・3.1 章・4.1 章・5.1 章の `<!-- TODO: slot-core agent -->` マーカーが除去されている

### Wave 2 (並列波): 残り 5 Spec を一斉起動

#### 対象
- project-foundation
- motion-pipeline
- mocap-vmc
- avatar-provider-builtin
- ui-sample

#### 起動方法
Agent ツールを単一メッセージ内で 5 体並列起動 (subagent_type=general-purpose)。各エージェントに対応する Brief を読ませる。

各エージェントの責務:
- 対応する `.kiro/specs/_shared/briefs/<spec>.md` を読む
- `.kiro/specs/_shared/spec-map.md` を読む
- `.kiro/specs/_shared/contracts.md` を読む (Wave 1 で確定済みの内容)
- Skill ツールで `kiro:spec-init <spec>` を実行
- Skill ツールで `kiro:spec-requirements <spec>` を実行
- 生成された requirements.md を Brief に沿って編集
- (該当するエージェントのみ) contracts.md 担当章を編集

#### 完了条件
- 全 5 Spec の `requirements.md` が存在
- `project-foundation` が contracts.md 6 章を埋めている
- `motion-pipeline` が contracts.md 2.2 章を埋めている

### Wave 合流: 人間レビュー

requirements 全 6 件 + contracts.md の整合性を人間がレビューする。矛盾があれば該当エージェントに修正指示。合意後 design フェーズへ進む。

## サブエージェント起動時のプロンプト雛形

```
あなたは Realtime Avatar Controller プロジェクトの <SPEC> Spec 担当エージェントです。

以下のファイルを必ずこの順で読み、指示に従ってください:
1. .kiro/specs/_shared/spec-map.md
2. .kiro/specs/_shared/contracts.md
3. .kiro/specs/_shared/briefs/<SPEC>.md

あなたの責務は Brief に明記されています。実行手順セクションに従い、Skill ツール経由で kiro コマンドを実行し、requirements.md を生成・編集してください。

完了条件:
- .kiro/specs/<SPEC>/requirements.md が作成されている
- (該当 Spec のみ) _shared/contracts.md の担当章が埋められている
- 他 Spec の領域に手を加えていない

完了後、変更ファイル一覧と概要を短く報告してください。
```

## 注意事項

- サブエージェントが Skill ツール経由で `kiro:spec-*` を呼べることを前提としている。不可の場合は `claude -p` 子プロセス起動にフォールバックする (詳細は別途詰める)
- `contracts.md` は複数エージェントが書き込むため、Wave 2 内での同時編集は避ける。各担当章が分離しているため実質衝突はないが、Wave 2 のレビュー時に整合確認する
- 実装フェーズで初めて git worktree を検討する。requirements / design / tasks フェーズでは不要
