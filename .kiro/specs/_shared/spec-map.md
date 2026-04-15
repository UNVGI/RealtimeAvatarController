# Spec 全体俯瞰

本ツール (Realtime Avatar Controller) は 6 つの Spec に分割して開発する。

## 分割方針

レイヤー分割。抽象インターフェースと具象実装、段階的拡張余地を Spec 境界として採用する。

## Spec 一覧

| # | Spec 名 | 責務 | 依存 |
|---|---------|------|------|
| 1 | `project-foundation` | Unity 6000.3.10f1 プロジェクト作成、`RealtimeAvatarController/` 配置、UPM パッケージ雛形、アセンブリ分割 (機能部 / UI サンプル)、CI 下地 | なし |
| 2 | `slot-core` | Slot 抽象、Slot 設定データモデル、動的追加・削除 API、SlotRegistry / Manager、設定シリアライズ、Facial / LipSync の抽象 IF 定義 | 1 |
| 3 | `motion-pipeline` | モーション中立表現、内部キャッシュ、Weight 適用、Humanoid 適用層、Generic 拡張余地 | 2 |
| 4 | `mocap-vmc` | `IMoCapSource` 抽象、VMC (OSC 受信) 具象実装、スレッドモデル、Slot 紐付け | 2, 3 |
| 5 | `avatar-provider-builtin` | `IAvatarProvider` 抽象、ビルトイン Provider 実装、Addressable Provider 拡張余地 | 2 |
| 6 | `ui-sample` | UI サンプル、機能部 API デモ | 1〜5 |

## 依存グラフ

```
project-foundation
       │
       ▼
   slot-core ──┬── avatar-provider-builtin
               │
               └── motion-pipeline
                        │
                        ▼
                    mocap-vmc
                        │
                        ▼
                   ui-sample
```

## 初期段階スコープ

| 項目 | 実装 | 抽象のみ | 対象外 |
|------|:----:|:--------:|:------:|
| Unity 6000.3.10f1 プロジェクト | ● | | |
| `RealtimeAvatarController/` 配置 | ● | | |
| UPM パッケージ化 | ● | | |
| 機能部 / UI サンプル分離 | ● | | |
| Slot 管理 (追加・削除・設定) | ● | | |
| プロジェクトビルトインアバター供給 | ● | | |
| Addressable アバター供給 | | ● | |
| MoCap ソース抽象 | ● | | |
| VMC MoCap ソース | ● | | |
| Humanoid モーション適用 | ● | | |
| Generic モーション適用 | | ● | |
| 内部キャッシュ + Weight 適用 | ● | | |
| 表情制御抽象 | | ● | |
| 表情制御具体実装 | | | ● |
| リップシンク抽象 | | ● | |
| リップシンク具体実装 | | | ● |
| UI サンプル | ● | | |

## 全 Spec 共通の前提

- Unity 6000.3.10f1
- リポジトリ直下 `RealtimeAvatarController/` に Unity プロジェクト配置
- UPM 配布可能 (`package.json` 同梱)
- 機能部とUI層のアセンブリ分離
- 機能部は UI フレームワークに非依存

## 並列作業運用

フェーズ並列 (案 X) + 先行波方式。フェーズごとに別エージェントチームを起動する。

### Wave 1: 先行波
`slot-core` の requirements エージェントが `_shared/contracts.md` の draft を確定させる。

### Wave 2: 並列波
残り 5 Spec の requirements エージェントを並列起動。`contracts.md` を参照して各 requirements.md を生成。

### Wave 3 以降: design → tasks → implementation
同様の波状並列。実装フェーズでのみ git worktree 分離を検討。

## 参照ドキュメント

- `_shared/spec-map.md` (本書)
- `_shared/contracts.md` (公開 IF・データ契約、slot-core が主執筆)
- `_shared/briefs/<spec>.md` (各 Spec エージェント向けブリーフィング)
- `_shared/dispatch-plan.md` (エージェント起動手順)
