# 要件定義ドキュメント

## はじめに

本ドキュメントは `mocap-vmc` Spec の要件を定義する。`mocap-vmc` は VMC (バーチャルモーションキャプチャ) プロトコルに対応した MoCap ソース具象実装を提供する Spec であり、`slot-core` が定義した `IMoCapSource` 抽象インターフェースを実装する。OSC (Open Sound Control) プロトコルを通じてモーションキャプチャデータを受信し、`motion-pipeline` が定義するモーションデータ中立表現へ変換して供給することが主要責務である。

## スコープ境界

- **スコープ内**:
  - `IMoCapSource` の VMC プロトコル (OSC 受信) 具象実装 (`VmcMoCapSource`)
  - 受信スレッドモデル (ワーカースレッドによる OSC 受信、メインスレッドへの同期)
  - Slot 単位の通信パラメータ設定 (受信ポート番号等の構成データ)
  - Slot との紐付けおよび動的差替
  - 受信 VMC データを `motion-pipeline` のモーション中立表現へ変換する責務
  - VMC 送信側 (Sender) の要件検討 (実装有無は design フェーズで確定)
- **スコープ外**:
  - VMC 以外の MoCap ツール・プロトコルへの対応
  - `IMoCapSource` 抽象インターフェース自体の定義 (`slot-core` Spec の責務)
  - モーションデータ中立表現の定義 (`motion-pipeline` Spec の責務)
  - 表情制御・リップシンク処理
  - UI・サンプルシーン
- **隣接 Spec との関係**:
  - `slot-core`: `IMoCapSource` および `SlotSettings` を参照する
  - `motion-pipeline`: モーションデータ中立表現を受け取り変換先として利用する
  - `project-foundation`: Unity プロジェクトおよびアセンブリ定義 (`RealtimeAvatarController.MoCap.VMC`) が提供済みであることを前提とする

---

## 要件

### 要件 1: VMC 受信ソース (`VmcMoCapSource`) の実装

**目的:** As a ランタイム統合者, I want `IMoCapSource` を実装した VMC 専用クラスが存在すること, so that Slot に VMC データソースを割り当ててモーション駆動ができる。

#### 受け入れ基準

1. `VmcMoCapSource` は `IMoCapSource` を実装し、`IDisposable` を継承すること。
2. `VmcMoCapSource` は `SourceType` プロパティとして定数文字列 `"VMC"` を返すこと。
3. `VmcMoCapSource` は `Initialize(MoCapSourceConfig config)` の呼び出しによって OSC 受信を開始できること (設定型の最終名称は design フェーズで確定)。
4. `VmcMoCapSource` は `Shutdown()` の呼び出しによって OSC 受信を停止し、関連リソースをすべて解放すること。
5. `VmcMoCapSource` は `FetchLatestMotion()` の呼び出しによって最新のモーション中立表現データを返すこと (戻り値型は `motion-pipeline` との合意後に確定)。
6. When `FetchLatestMotion()` が初回受信前またはデータ未取得の状態で呼び出された場合, `VmcMoCapSource` shall null またはデフォルト値を返すこと。

---

### 要件 2: 受信スレッドモデル

**目的:** As a ランタイム統合者, I want OSC 受信処理がメインスレッドをブロックしないこと, so that Unity のフレームレートに悪影響を与えずにモーションデータを継続受信できる。

#### 受け入れ基準

1. OSC パケット受信はワーカースレッド (またはそれに準じる非同期処理) で行い、Unity メインスレッドをブロックしないこと。
2. ワーカースレッドが受信したデータは、スレッドセーフなバッファ (例: `lock` によるミューテックス保護、またはロックレスキュー) を通じてメインスレッドから読み取り可能な状態に保つこと。
3. `FetchLatestMotion()` はメインスレッドから安全に呼び出せること。
4. `Initialize()` および `Shutdown()` はメインスレッドからの呼び出しを前提とすること (contracts.md 2.1 章のスレッド安全性要求に準拠)。
5. ワーカースレッドの未ハンドル例外は、メインスレッドで検知可能な形式でキャプチャし、ログ記録後に受信ループを安全に停止すること。

---

### 要件 3: Slot 単位の通信パラメータ設定

**目的:** As a ユーザー, I want Slot ごとに VMC 受信ポートを個別に設定できること, so that 複数の VMC ソースを複数の Slot へ同時に割り当てられる。

#### 受け入れ基準

1. `VmcMoCapSourceConfig` (仮称) は受信ポート番号 (int) を必須フィールドとして持つこと (型名・フィールド名の最終決定は design フェーズ)。
2. `VmcMoCapSourceConfig` は受信アドレス (IPv4 文字列、デフォルト `127.0.0.1`) をオプションフィールドとして持つこと。
3. 同一ポートに複数の `VmcMoCapSource` が同時にバインドを試みた場合、後発の `Initialize()` 呼び出しは例外またはエラー結果を返し、既存のバインドを破壊しないこと。
4. ポート番号の有効範囲は 1025〜65535 とし、範囲外の値が設定された場合は `Initialize()` が例外をスローすること。
5. `VmcMoCapSourceConfig` は Unity `ScriptableObject` またはシリアライズ可能な POCO として定義でき、エディタ上で設定・保存できること (最終形式は design フェーズで確定)。

---

### 要件 4: Slot との紐付けと動的差替

**目的:** As a ランタイム統合者, I want 実行中に Slot の VMC ソースを別ポート設定に差し替えられること, so that 配信中の設定変更やトラブル対応が可能になる。

#### 受け入れ基準

1. `SlotManager` が `VmcMoCapSource` を `SlotSettings.moCapSource` として登録し、`Initialize()` を呼び出すことで受信が開始されること。
2. When 既存の `VmcMoCapSource` を Slot から切り離す場合, the `SlotManager` shall 旧ソースの `Shutdown()` を呼び出してリソースを解放してから新ソースを割り当てること。
3. 差替え操作中 (旧ソース停止〜新ソース開始の間) にフレーム更新が発生した場合、`FetchLatestMotion()` は null またはデフォルト値を返し、例外をスローしないこと。
4. `VmcMoCapSource` は初期化前・動作中・停止後の内部状態を管理し、状態外の操作 (例: 二重 `Initialize()`) が呼び出された場合は例外をスローすること。

---

### 要件 5: VMC データの中立表現への変換

**目的:** As a motion-pipeline 統合者, I want `VmcMoCapSource` が受信データをモーション中立表現に変換して提供すること, so that motion-pipeline 側が VMC プロトコルの詳細を意識せずにデータを利用できる。

#### 受け入れ基準

1. `VmcMoCapSource` は VMC プロトコルで受信した Humanoid ボーン情報 (OSC アドレス `/VMC/Ext/Bone/Pos` 等) を、`motion-pipeline` が定義するモーション中立表現 (Humanoid: HumanPose 相当) へ変換すること (OSC アドレスの最終リストは design フェーズで確定)。
2. `VmcMoCapSource` は VMC プロトコルで受信したルートトランスフォーム (`/VMC/Ext/Root/Pos`) をモーション中立表現のルートフィールドに反映すること。
3. VMC メッセージに含まれないボーンのモーション中立表現フィールドは、デフォルト値 (アイドルポーズまたはゼロ回転) として扱うこと。
4. 変換処理は `FetchLatestMotion()` 呼び出し時点、またはデータ受信時点で完結すること (実行タイミングは design フェーズで確定)。
5. `VmcMoCapSource` は VMC の Blend Shape データ (`/VMC/Ext/Blend/Val`) を受信し、将来的に表情制御 (`IFacialController`) へ橋渡しできる拡張余地を構造上保持すること (初期段階では当該データを変換対象外とすることも可)。

---

### 要件 6: VMC 送信側 (Sender) の扱い

**目的:** As a Spec 設計者, I want VMC 送信機能の要件スコープを明示すること, so that design フェーズで実装有無を判断できる根拠を持てる。

#### 受け入れ基準

1. VMC 送信 (Sender) は本 Spec の初期段階スコープに含まれない。
2. 将来の拡張として `VmcMoCapSender` を追加できるよう、受信側実装はパケット送信ロジックと分離した構造をとること (design フェーズで確定)。
3. 送信機能が必要と判断された場合、design フェーズで別クラスとして設計し、本 requirements.md を更新すること。

---

### 要件 7: エラー処理と診断

**目的:** As a 開発者, I want VMC ソースの異常状態を検知・記録できること, so that トラブルシューティングと運用監視が可能になる。

#### 受け入れ基準

1. OSC ソケットのバインド失敗 (ポート競合等) は `Initialize()` 時に検知し、呼び出し元に例外またはエラー結果として通知すること。
2. OSC パケットのパース失敗は例外をスローせず、当該パケットをスキップしてログ記録すること。
3. ワーカースレッドの異常終了は Unity `Debug.LogError` またはそれに準じるログ機構で記録し、`VmcMoCapSource` を `Inactive` 相当の状態に遷移させること。
4. `VmcMoCapSource` は受信パケット数・パースエラー数等の診断カウンタをプロパティとして公開できる拡張余地を持つこと (初期実装では省略可)。

---

### 要件 8: アセンブリ・名前空間

**目的:** As a Spec 設計者, I want mocap-vmc の成果物が適切なアセンブリ・名前空間に配置されること, so that 他 Spec との依存関係が明確に管理できる。

#### 受け入れ基準

1. `VmcMoCapSource` およびその関連クラスは `RealtimeAvatarController.MoCap.VMC` アセンブリ (asmdef) に配置すること (contracts.md 6.1 章に準拠)。
2. 名前空間は `RealtimeAvatarController.MoCap.VMC` (またはサブ名前空間) を使用すること。
3. 本アセンブリは `RealtimeAvatarController.Core` (slot-core) および `RealtimeAvatarController.Motion` (motion-pipeline) への参照を持ち、逆方向の参照を持たないこと。
4. OSC ライブラリの選定は design フェーズで確定するが、サードパーティライブラリはパッケージ参照として管理し、本アセンブリのソースツリーに直接含めないこと。
