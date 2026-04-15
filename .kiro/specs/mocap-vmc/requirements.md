# 要件定義ドキュメント

## はじめに

本ドキュメントは `mocap-vmc` Spec の要件を定義する。`mocap-vmc` は VMC (バーチャルモーションキャプチャ) プロトコルに対応した MoCap ソース具象実装を提供する Spec であり、`slot-core` が定義した `IMoCapSource` 抽象インターフェースを実装する。OSC (Open Sound Control) プロトコルを通じてモーションキャプチャデータを受信し、`motion-pipeline` が定義するモーションデータ中立表現へ変換して Push 型ストリームとして供給することが主要責務である。

### ライブラリ採用方針 (dig ラウンド 2 確定)

- **Reactive 拡張ライブラリ: UniRx (`com.neuecc.unirx`) を採用する。R3 は採用しない。**
  - UniRx の `Subject<T>` は `System.IObservable<T>` を実装しているため、契約の型シグネチャ (`IObservable<MotionFrame>`) は変更不要である。
  - UniRx は NuGet 依存を持たないため、UPM 配布における scoped registry が OpenUPM 1 個で完結し、配布手続きが簡素化される。
  - `using UniRx;` を追加することで `ObserveOnMainThread()`、`Synchronize()` 等の拡張メソッドが利用可能になる。
- **Config 型階層**: `slot-core` が `contracts.md` 1.5 章で定義した `MoCapSourceConfigBase : ScriptableObject` を基底クラスとして採用する。本 Spec は VMC 固有の具象 Config `VMCMoCapSourceConfig : MoCapSourceConfigBase` を定義する責務を持つ。

## スコープ境界

- **スコープ内**:
  - `IMoCapSource` の VMC プロトコル (OSC 受信) 具象実装 (`VmcMoCapSource`)
  - 受信スレッドモデル (ワーカースレッドによる OSC 受信、UniRx `Subject` を通じた Push 型発行)
  - Slot 単位の通信パラメータ設定 (受信ポート番号等の構成データ)
  - `MoCapSourceRegistry` への Factory 登録 (`typeId="VMC"`)
  - Slot との紐付けおよび動的差替 (`MoCapSourceRegistry` 経由の参照共有モデル)
  - 受信 VMC データを `motion-pipeline` のモーション中立表現へ変換する責務
  - VMC 送信側 (Sender) の要件検討 (実装有無は design フェーズで確定)
- **スコープ外**:
  - VMC 以外の MoCap ツール・プロトコルへの対応
  - `IMoCapSource` 抽象インターフェース自体の定義 (`slot-core` Spec の責務)
  - モーションデータ中立表現の定義 (`motion-pipeline` Spec の責務)
  - `MoCapSourceRegistry` 自体の実装 (`slot-core` Spec の責務)
  - 表情制御・リップシンク処理
  - UI・サンプルシーン
- **隣接 Spec との関係**:
  - `slot-core`: `IMoCapSource`、`IMoCapSourceRegistry`、`SlotSettings` を参照する
  - `motion-pipeline`: モーションデータ中立表現 (`MotionFrame`) を変換先として利用する
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
5. `VmcMoCapSource` は **Push 型** の `IObservable<MotionFrame> MotionStream` プロパティを公開すること。内部実装は **UniRx (`com.neuecc.unirx`) の `Subject<MotionFrame>`** (またはスレッドセーフなラッパーである `Subject<MotionFrame>.Synchronize()`) を使用し、OSC 受信の都度 `Subject.OnNext(frame)` でフレームを発行すること。`MotionStream` の公開型は `System.IObservable<MotionFrame>` であり、UniRx `Subject<T>` はこれを実装するため型シグネチャの変更は不要である。
6. `MotionStream` は購読者が複数いる場合でも全購読者に同一フレームが配信されること (`Publish().RefCount()` 等によるマルチキャスト化を設計フェーズで確定する)。

---

### 要件 2: 受信スレッドモデル

**目的:** As a ランタイム統合者, I want OSC 受信処理がメインスレッドをブロックしないこと, so that Unity のフレームレートに悪影響を与えずにモーションデータを継続受信できる。

#### 受け入れ基準

1. OSC パケット受信はワーカースレッド (またはそれに準じる非同期処理) で行い、Unity メインスレッドをブロックしないこと。
2. ワーカースレッドが受信・パースした `MotionFrame` は、`Subject<MotionFrame>.OnNext()` を通じて即座にストリームへ発行すること。Pull 型バッファは使用しない。
3. 購読側 (Slot / Pipeline) は `.ObserveOnMainThread()` を用いて Unity メインスレッドでフレームを処理すること (contracts.md 2.1 章のスレッド安全性要求に準拠)。
4. `Initialize()` および `Shutdown()` はメインスレッドからの呼び出しを前提とすること。
5. `Subject<MotionFrame>` への `OnNext()` 呼び出しはワーカースレッドから行われるため、具象実装は UniRx の `Subject.Synchronize()` 等スレッドセーフなラッパーを使用すること (詳細は design フェーズで確定)。
6. ワーカースレッドの未ハンドル例外は、`MotionStream` の `OnError()` 通知またはログ記録を通じてメインスレッドで検知可能な形式でキャプチャし、受信ループを安全に停止すること。

---

### 要件 3: Slot 単位の通信パラメータ設定

**目的:** As a ユーザー, I want Slot ごとに VMC 受信ポートを個別に設定できること, so that 複数の VMC ソースを複数の Slot へ同時に割り当てられる。

#### 受け入れ基準

1. **本 Spec は `VMCMoCapSourceConfig : MoCapSourceConfigBase` を定義する責務を持つ**。`MoCapSourceConfigBase` は `contracts.md` 1.5 章で `slot-core` が定義した抽象基底クラス (`ScriptableObject` 派生) であり、`VMCMoCapSourceConfig` はこれを継承して VMC 固有の通信パラメータを保持する。(型名の最終決定は design フェーズ)
2. `VMCMoCapSourceConfig` は受信ポート番号 (int) を必須フィールドとして持つこと。
3. `VMCMoCapSourceConfig` は受信アドレス (IPv4 文字列、デフォルト `127.0.0.1`) をオプションフィールドとして持つこと。
4. ポート番号の有効範囲は 1025〜65535 とし、範囲外の値が設定された場合は `Initialize()` が例外をスローすること。
5. **同一ポートへの複数 `VmcMoCapSource` インスタンスのバインドは OS 側でエラーとなる**。この場合、後発の `Initialize()` 呼び出しで OSC ライブラリまたは OS からソケットバインドエラーが発生し、`Initialize()` は例外またはエラー結果を返すこと。既存インスタンスのバインドは破壊しないこと。
6. 複数 Slot が同一 VMC ポートのデータを利用したい場合は、**同一の `VmcMoCapSource` インスタンスを複数 Slot で参照共有**するシナリオを採用すること。参照共有の管理は `MoCapSourceRegistry` が担い、Slot 側は `IMoCapSourceRegistry.Resolve(descriptor)` で取得した同一インスタンスを購読すること。
7. `VMCMoCapSourceConfig` は Unity `ScriptableObject` を継承しているため、Inspector 上でドラッグ&ドロップによる型安全な参照が可能であり、エディタ上で設定・保存できること。

---

### 要件 4: Slot との紐付けと動的差替

**目的:** As a ランタイム統合者, I want 実行中に Slot の VMC ソースを別ポート設定に差し替えられること, so that 配信中の設定変更やトラブル対応が可能になる。

#### 受け入れ基準

1. `SlotManager` は `MoCapSourceDescriptor` (typeId="VMC" + 設定) を `IMoCapSourceRegistry.Resolve(descriptor)` へ渡して `VmcMoCapSource` インスタンスを取得すること。インスタンスの生成・再利用・参照カウントは `MoCapSourceRegistry` が管理する。
2. `SlotManager` は Slot を解放する際、`IMoCapSourceRegistry.Release(source)` を呼び出すこと。`VmcMoCapSource` の `Dispose()` / `Shutdown()` を直接呼び出してはならない。
3. When 既存の `VmcMoCapSource` を Slot から切り離して新しいソースへ差し替える場合, the `SlotManager` shall 旧ソースに対して `IMoCapSourceRegistry.Release()` を呼び出してから新ソースを `Resolve()` で取得すること。
4. 差替え操作中 (旧ソース解放〜新ソース取得の間) にフレーム更新が発生した場合、当該 Slot のパイプラインはフレームをスキップしても例外をスローしないこと。
5. `VmcMoCapSource` は初期化前・動作中・停止後の内部状態を管理し、状態外の操作 (例: 二重 `Initialize()`) が呼び出された場合は例外をスローすること。

---

### 要件 5: VMC データの中立表現への変換

**目的:** As a motion-pipeline 統合者, I want `VmcMoCapSource` が受信データをモーション中立表現に変換して提供すること, so that motion-pipeline 側が VMC プロトコルの詳細を意識せずにデータを利用できる。

#### 受け入れ基準

1. `VmcMoCapSource` は VMC プロトコルで受信した Humanoid ボーン情報 (OSC アドレス `/VMC/Ext/Bone/Pos` 等) を、`motion-pipeline` が定義するモーション中立表現 (Humanoid: `HumanoidMotionFrame`) へ変換し、`MotionStream` 経由で発行すること (OSC アドレスの最終リストは design フェーズで確定)。
2. `VmcMoCapSource` は VMC プロトコルで受信したルートトランスフォーム (`/VMC/Ext/Root/Pos`) をモーション中立表現のルートフィールドに反映すること。
3. VMC メッセージに含まれないボーンのモーション中立表現フィールドは、デフォルト値 (アイドルポーズまたはゼロ回転) として扱うこと。
4. 変換処理は OSC データ受信時点で完結し、変換済み `MotionFrame` を `Subject.OnNext()` で発行すること。
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

1. OSC ソケットのバインド失敗 (ポート競合等) は `Initialize()` 時に検知し、呼び出し元に例外またはエラー結果として通知すること。ソケットバインドエラーは `MotionStream` の `OnError()` を通じて購読者にも伝播させること。
2. OSC パケットのパース失敗は例外をスローせず、当該パケットをスキップしてログ記録すること。パースエラーは `MotionStream` の `OnError()` ではなくログのみで処理し、ストリームを継続すること。
3. ワーカースレッドの異常終了は Unity `Debug.LogError` またはそれに準じるログ機構で記録し、`MotionStream` の `OnError()` を通じて全購読者へ通知したうえで、`VmcMoCapSource` を `Inactive` 相当の状態に遷移させること。
4. **参照共有時のエラー伝播**: `MotionStream` に `OnError()` が発行された場合、当該ストリームを購読している全 Slot (全購読者) へエラーが伝播する。各 Slot は購読時に `OnError` ハンドラを登録し、自身のパイプラインを安全に停止する責務を持つこと。
5. `VmcMoCapSource` は受信パケット数・パースエラー数等の診断カウンタをプロパティとして公開できる拡張余地を持つこと (初期実装では省略可)。

---

### 要件 8: アセンブリ・名前空間と Registry 登録

**目的:** As a Spec 設計者, I want mocap-vmc の成果物が適切なアセンブリ・名前空間に配置され、`MoCapSourceRegistry` に自動または手動で登録されること, so that 他 Spec との依存関係が明確に管理でき、Slot から VMC ソースを型安全に取得できる。

#### 受け入れ基準

1. `VmcMoCapSource` およびその関連クラスは `RealtimeAvatarController.MoCap.VMC` アセンブリ (asmdef) に配置すること (contracts.md 6.1 章に準拠)。
2. 名前空間は `RealtimeAvatarController.MoCap.VMC` (またはサブ名前空間) を使用すること。
3. 本アセンブリは `RealtimeAvatarController.Core` (slot-core) および `RealtimeAvatarController.Motion` (motion-pipeline) への参照を持ち、逆方向の参照を持たないこと。
4. UniRx (`UniRx`) への依存は `RealtimeAvatarController.Core` を経由して間接的に解決すること。本アセンブリが直接 UniRx パッケージを参照する場合も、UniRx API の使用は `MotionStream` 公開に必要な最小限に留めること。
5. `VmcMoCapSource` の Factory (`IMoCapSourceFactory` 実装) は `MoCapSourceRegistry` へ `typeId="VMC"` で登録されること。登録方式 (属性スキャン / DI / 手動登録) は design フェーズで確定するが、少なくとも手動登録 (`IMoCapSourceRegistry.Register("VMC", factory)`) で動作すること。
6. OSC ライブラリの選定は design フェーズで確定するが、サードパーティライブラリはパッケージ参照として管理し、本アセンブリのソースツリーに直接含めないこと。

---

### 要件 9: `VMCMoCapSourceConfig` 型定義と `VMCMoCapSourceFactory` のキャスト責務

**目的:** As a Spec 設計者, I want VMC 専用の Config 型と Factory が適切に定義され、型安全なキャストによってインスタンス生成が行われること, so that 他の MoCap ソース実装と干渉せず、`MoCapSourceConfigBase` 基底型階層に正しく統合できる。

#### 受け入れ基準

1. **`VMCMoCapSourceConfig` の型定義**: 本 Spec は `VMCMoCapSourceConfig : MoCapSourceConfigBase` を `RealtimeAvatarController.MoCap.VMC` 名前空間内で定義すること。`MoCapSourceConfigBase` (`contracts.md` 1.5 章) を継承し、`ScriptableObject` 派生クラスとすること。
2. `VMCMoCapSourceConfig` は VMC 受信に必要な通信パラメータ (受信ポート番号・受信アドレス等、要件 3 参照) を保持するフィールドを持つこと。
3. **`VMCMoCapSourceFactory` のキャスト責務**: `VMCMoCapSourceFactory` は `IMoCapSourceFactory` を実装し、`Create(MoCapSourceConfigBase config)` 内で引数を `VMCMoCapSourceConfig` にキャスト (`config as VMCMoCapSourceConfig`) して使用すること。
4. **キャスト失敗時のエラーハンドリング**: キャスト結果が `null` の場合 (型不一致)、`VMCMoCapSourceFactory.Create()` は `ArgumentException` (またはそれに準じる例外) をスローし、受け取った型名を例外メッセージに含めること。例: `$"VMCMoCapSourceConfig が必要ですが {config?.GetType().Name} が渡されました"` 相当のメッセージ。
5. `VMCMoCapSourceFactory` は `MoCapSourceRegistry` に `typeId="VMC"` で登録される唯一の Factory エントリとなること (要件 8 の受け入れ基準 5 と整合)。
6. `VMCMoCapSourceConfig` は Unity Inspector 上で `MoCapSourceDescriptor.Config` フィールド (型: `MoCapSourceConfigBase`) への参照アセットとして設定・保存できること。これにより Descriptor パターン (`contracts.md` 1.1 章) との統合が実現する。
