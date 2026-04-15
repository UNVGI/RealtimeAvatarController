# Brief: mocap-vmc

## Spec 責務
VMC (バーチャルモーションキャプチャ) プロトコルに対応した MoCap ソース具象実装を提供する。

## 依存
`slot-core`, `motion-pipeline` (Wave 1 完了後に起動)

## スコープ

### 実装する
- `IMoCapSource` の VMC プロトコル実装 (OSC 受信、Push 型 `IObservable<MotionFrame>` ストリーム)
- 受信スレッドモデル (ワーカースレッドで OSC 受信、**UniRx (`com.neuecc.unirx`) `Subject<MotionFrame>.OnNext()`** で発行、購読側は `.ObserveOnMainThread()` で同期)
  - **R3 は採用しない。UniRx を使用する。**
  - `MotionStream` の公開型は `System.IObservable<MotionFrame>` (UniRx `Subject<T>` はこれを実装するため型シグネチャ変更不要)
- **`VMCMoCapSourceConfig : MoCapSourceConfigBase` の型定義** (本 Spec の責務)
  - `MoCapSourceConfigBase` は `contracts.md` 1.5 章で `slot-core` が定義した抽象基底クラス
  - `VMCMoCapSourceConfig` は受信ポート・アドレス等の通信パラメータを保持
- **`VMCMoCapSourceFactory` のキャスト責務**: `Create(MoCapSourceConfigBase config)` 内で `config as VMCMoCapSourceConfig` にキャストし、型不一致の場合は `ArgumentException` をスロー
- Slot 単位の通信パラメータ設定 (受信ポート等)
- `MoCapSourceRegistry` への Factory 登録 (typeId="VMC")
- Slot との紐付け / 動的差替 (`IMoCapSourceRegistry.Resolve()` / `Release()` 経由の参照共有モデル)
- 受信したデータを `motion-pipeline` の中立表現へ変換する責務

### スコープ外
- 他 MoCap ツールへの対応 (本 Spec の抽象遵守により将来別 Spec で追加可能とする)
- MoCap ソース抽象 `IMoCapSource` 自体の定義 (`slot-core` Spec)
- モーション中立表現の定義 (`motion-pipeline` Spec)

## dig ラウンド 3 確定事項の反映 (requirements.md 改訂内容)

### MotionStream.OnError 不発行方針
`IMoCapSource.MotionStream` は `OnError()` を一切発行しない。受信エラー (OSC パースエラー・ネットワーク切断検知) が発生してもストリームはエラーで終端せず継続する。購読側は `OnError` ハンドラを実装しなくてよい。

### ISlotErrorChannel 連携
受信エラー (OSC パースエラー・ネットワーク切断検知) は次の 2 段階で通知する:
1. `Debug.LogError` によるログ出力 (抑制ロジックは `ISlotErrorChannel` 実装側が担う)
2. `ISlotErrorChannel` に `SlotErrorCategory.VmcReceive` カテゴリで `SlotError` を発行

ポート競合 (ソケットバインド失敗) は `Initialize()` 時に例外をスローし、`SlotManager` が `InitFailure` カテゴリで発行する。

### 属性ベース自己登録
`VMCMoCapSourceFactory` は以下の 2 属性で自己登録する:
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`: ランタイム起動時 (シーンロード前) に `RegistryLocator.MoCapSourceRegistry.Register("VMC", ...)` を実行
- `[UnityEditor.InitializeOnLoadMethod]` (`#if UNITY_EDITOR` ガードまたは `RealtimeAvatarController.MoCap.VMC.Editor` asmdef 内): エディタ起動時に同一登録を実行

同 typeId 競合時は `RegistryConflictException` 相当の例外がスローされる (上書き禁止)。

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md` (特に 2 章: MoCap ソース抽象)

## 契約ドキュメントへの寄与
なし (2.1 は slot-core、2.2 は motion-pipeline が埋める)。ただし本 Spec の具象実装要件が 2 章と矛盾しないことを確認する。

## 出力物
- `.kiro/specs/mocap-vmc/requirements.md`
- `.kiro/specs/mocap-vmc/spec.json`

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `mocap-vmc` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定

## 備考
- VMC の受信側 (Receiver) を必須対応とする。送信側 (Sender) は要件段階で検討可とする
- OSC ライブラリの選定は design フェーズで確定

## 言語
Markdown 出力は日本語
