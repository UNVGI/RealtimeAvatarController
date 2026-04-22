# 要件定義ドキュメント

## はじめに

本ドキュメントは `mocap-vmc` Spec の要件を定義する。本 Spec は、VMC (バーチャルモーションキャプチャ) プロトコルに対応した MoCap ソース具象実装を提供し、Slot パイプラインに VMC 由来のモーションデータを供給することを目的とする。

実装方針として、VMC プロトコル受信・OSC パース・座標系変換・ボーンマッピング等の詳細は、VSeeFace / VMagicMirror / VirtualMotionCapture など主要な VMC 送信アプリケーションとの互換性が数年にわたり検証されている準公式 Unity ライブラリ **EVMC4U** (`gpsnmeajp/EasyVirtualMotionCaptureForUnity`, MIT) に委譲する。本 Spec 自体は EVMC4U をラップする薄い Adapter を定義し、EVMC4U が内部に保持するボーン状態を `HumanoidMotionFrame` (motion-pipeline の中立表現) に変換して `IObservable<MotionFrame>` として発行する責務を負う。

### 採用方針 (前セッションで確定済み)

- **EVMC4U を採用** (`Assets/EVMC4U/` に `.unitypackage` 取り込み済み、MIT): OSC 受信・VMC プロトコルパース・座標系変換・ボーンマッピングは EVMC4U が担う。本 Spec ではこれらを再実装しない。
- **EVMC4U の配布形態は `.unitypackage` のみ**: UPM / OpenUPM / npm 配布は存在しないため、`Assets/EVMC4U/` 配下にソースをインポートする方式で運用する。これを前提とする。
- **Adapter パターンで `IMoCapSource` と統合**: 既存の `IObservable<HumanoidMotionFrame>` 契約 (`_shared/contracts.md` §2.1 / §2.2) を維持し、Adapter (`EVMC4UMoCapSource` 相当) が EVMC4U の内部状態を読み取って `HumanoidMotionFrame` を組み立てて発行する。下流 (`HumanoidMotionApplier` / `MotionCache` / `SlotManager` / FallbackPolicy) は変更しない。
- **共有 `ExternalReceiver` + Slot 毎 Adapter モデル**: 1 つの `ExternalReceiver` GameObject をシーンに独立配置し、複数の Slot レベル Adapter がその状態を読み取って各 Slot 用の Frame を生成する。`MoCapSourceRegistry` による単一ソース共有モデル (contracts.md §1.4) と整合する。
- **`VMCMoCapSourceConfig.asset` を継続使用**: 既存の ScriptableObject Config (port, bindAddress 等) は UX 継続性のため保持する。Adapter は初期化時に Config の値を読み、`ExternalReceiver` の該当 public フィールドへ反映する。
- **型名 `VMCMoCapSourceConfig` / typeId `"VMC"` を維持**: VMC はプロトコル名であり、内部実装が EVMC4U に変わっても上位コードからの名称は変更しない。
- **スレッドモデル**: uOSC の `onDataReceived` は Unity MainThread で発火する (`uOscServer.Update` 内で dequeue する実装のため)。`ExternalReceiver` は受信時に内部 Dictionary へキャッシュし、Adapter は Slot の LateUpdate Tick で Dictionary を snapshot して `HumanoidMotionFrame` を発行する。これは EVMC4U 自体の設計 (受信と更新のタイミングを分離) と整合する。
- **Root Transform 書込は既定で無効**: VMC `RootRotation` / `RootPosition` を avatar root Transform へ書き込むと Hips との二重回転になり破綻する。EVMC4U のデフォルト設定と同様、本 Adapter 経路からの Root Transform 書込は初期版で無効化する (EVMC4U 側の `RootPositionSynchronize` / `RootRotationSynchronize` 設定に準拠する方針)。

## スコープ境界

- **スコープ内**:
  - EVMC4U をラップする `IMoCapSource` 具象実装 (以下 Adapter と呼ぶ) の定義
  - シーン上に単一の `ExternalReceiver` GameObject を確保するライフサイクル管理
  - `VMCMoCapSourceConfig` の継続 (受信ポート・受信アドレスの保持)
  - `VMCMoCapSourceConfig` の値を `ExternalReceiver` の受信設定へ反映するロジック
  - EVMC4U 内部のボーン状態 Dictionary を読み取り、`HumanoidMotionFrame` (`BoneLocalRotations` 付き) に変換して `IObservable<MotionFrame>` で発行する Adapter 実装
  - Slot の LateUpdate Tick によるフレーム発行 (受信と発行の分離モデル)
  - `MoCapSourceRegistry` への Factory 登録 (`typeId="VMC"`, 属性ベース自己登録)
  - 複数 Slot による同一 VMC ポートの共有 (1 個の `ExternalReceiver` + N 個の Adapter)
  - 既存 UI Sample の実行時シナリオ (Slot 動的追加 / 削除 / ソース差替) の継続動作
  - エラー通知 (`ISlotErrorChannel` への `VmcReceive` / `InitFailure` カテゴリ発行)
  - EVMC4U ソースに対する最小限のローカル改変の許容 (例: 必要な state を public 化 / event 化する変更)。`Assets/EVMC4U/` 配下はプロジェクトの管理下にあるため改変は可能だが、本 Spec は改変を最小化する方針とする

- **スコープ外**:
  - VMC プロトコル (OSC) の再実装 (EVMC4U が担うため本 Spec では実装しない)
  - 座標系変換ロジックの再実装 (EVMC4U が担う)
  - IK / Spring Bone / BlendShape / 表情制御 / リップシンクのうち EVMC4U が提供しない拡張 (本 Spec では実装しない)
  - `_shared/contracts.md` §2.2 `HumanoidMotionFrame` 形状の変更 (`BoneLocalRotations` を含む既存フィールドは保持)
  - `IMoCapSource` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` 抽象インターフェース自体の定義 (`slot-core` Spec の責務)
  - VMC Sender (送信側) 実装
  - VRM 1.x の完全サポート検証 (VRM 0.x を主対象とし、VRM 1.x は EVMC4U 側の対応状況に準ずる)

- **隣接 Spec との関係**:
  - `slot-core`: `IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` / `ISlotErrorChannel` / `RegistryLocator` を参照する
  - `motion-pipeline`: `MotionFrame` / `HumanoidMotionFrame` (`BoneLocalRotations` 付き) を発行先として利用する
  - `project-foundation`: Unity プロジェクトおよびアセンブリ定義 (`RealtimeAvatarController.MoCap.VMC`) が提供済みであることを前提とする
  - `EVMC4U` (`Assets/EVMC4U/`): 受信・パース・座標変換・ボーンマッピングの実装主体として依存する。`com.hidano.uosc` (UniVRM / VRM10 / UniGLTF / MToon) は EVMC4U が GUID 参照で利用する

---

## 要件

### 要件 1: EVMC4U ベースの VMC 受信ソース Adapter

**目的:** As a ランタイム統合者, I want EVMC4U をラップした `IMoCapSource` 具象実装が存在すること, so that Slot に VMC データソースを割り当てて、EVMC4U の検証済み実装経由でアバターをモーション駆動できる。

#### 受け入れ基準

1. The `mocap-vmc` Spec shall `IMoCapSource` インターフェースを実装する Adapter クラスを 1 つ定義する (クラス名は design フェーズで確定するが、EVMC4U ベースであることを示す命名とする)。
2. The Adapter shall `IDisposable` を継承し、`Shutdown()` と `Dispose()` が冪等な破棄操作として動作する。
3. The Adapter shall `SourceType` プロパティとして文字列 `"VMC"` を返す (既存の typeId を維持する)。
4. The Adapter shall `MotionStream` プロパティを `IObservable<MotionFrame>` として公開し、発行する具象型は `HumanoidMotionFrame` とする。
5. When `Initialize(MoCapSourceConfigBase config)` が呼び出されたとき, the Adapter shall `config` を `VMCMoCapSourceConfig` へキャストし、キャスト失敗時は `ArgumentException` をスローする (例外メッセージに受け取った型名を含めること)。
6. When `Initialize()` が成功したとき, the Adapter shall シーン上に EVMC4U の `ExternalReceiver` GameObject が存在することを確保し、存在しない場合は新規生成する (複数 Adapter インスタンスが同一の `ExternalReceiver` を共有する)。
7. When `Initialize()` が成功したとき, the Adapter shall `VMCMoCapSourceConfig` の受信ポート番号および受信アドレスを共有 `ExternalReceiver` の uOSC 受信設定へ反映する。
8. The Adapter shall VMC プロトコル (OSC) のパース・座標系変換・VMC Bone 名から Unity `HumanBodyBones` へのマッピングを自前で再実装してはならず、これらの責務は EVMC4U に委譲する。
9. If `Initialize()` が完了せずに `MotionStream` が購読された場合, then the Adapter shall 空のストリームとしてふるまい、購読後に `Initialize()` が完了した時点からフレームを発行する (または `Initialize()` 完了前の購読を禁止する設計とする; 最終挙動は design フェーズで確定する)。

---

### 要件 2: 共有 `ExternalReceiver` と Slot 毎 Adapter の構成

**目的:** As a ランタイム統合者, I want 1 個の `ExternalReceiver` で受信した VMC データを複数 Slot の Adapter が共有できること, so that `MoCapSourceRegistry` の単一ソース共有モデルを維持しつつ、複数 Slot で同一の VMC ポート設定を扱える。

#### 受け入れ基準

1. The Adapter shall シーン全体で 1 個の `ExternalReceiver` GameObject のみを生存させる (`MoCapSourceRegistry` の参照共有機能と協調し、典型的には同一 `VMCMoCapSourceConfig` インスタンスを参照する複数 Slot が同一 Adapter インスタンスを共有する)。
2. When 既存の `ExternalReceiver` GameObject がシーン上に存在する状態で新しい Adapter インスタンスが `Initialize()` されたとき, the Adapter shall 既存の `ExternalReceiver` を再利用する。
3. When `MoCapSourceRegistry.Release()` によって Adapter の参照カウントが 0 になったとき, the Registry shall Adapter の `Dispose()` を呼び出し、そのタイミングで共有 `ExternalReceiver` が他の Adapter から参照されていない場合は `ExternalReceiver` GameObject も破棄する。
4. While 共有 `ExternalReceiver` GameObject が生存中, the Adapter shall `ExternalReceiver` を直接操作してモデルを差し替えたり、ランタイムで `Model` フィールドを書き換えたりしてはならない (EVMC4U に対して本 Adapter は受信状態を読み取るのみとする)。ただし Adapter が `ExternalReceiver` の受信設定フィールド (ポート・バインドアドレス等) を書き換えることは許容する。
5. Where EVMC4U の `ExternalReceiver` が `Model` フィールドに割り当てを要求する場合, the `mocap-vmc` implementation shall `Assets/EVMC4U/ExternalReceiver.cs` に対し受信のみで動作可能とする最小のソース改変を行ってよい (`Model` が null でも例外を発生させずに受信処理を続行する等)。

---

### 要件 3: VMC データから `HumanoidMotionFrame` への変換

**目的:** As a motion-pipeline 統合者, I want Adapter が EVMC4U の受信結果を `HumanoidMotionFrame` (`BoneLocalRotations` 付き) に変換して発行すること, so that 下流の `HumanoidMotionApplier` が既存の `BoneLocalRotations` 経路 (Transform 直接書込) でアバターへ適用できる。

#### 受け入れ基準

1. The Adapter shall EVMC4U が内部に保持するボーン回転 Dictionary (例: `HumanBodyBones → Quaternion`) の内容を読み取り、それを `HumanoidMotionFrame.BoneLocalRotations` (`IReadOnlyDictionary<HumanBodyBones, Quaternion>`) として渡した `HumanoidMotionFrame` を生成する。
2. The Adapter shall `HumanoidMotionFrame.Muscles` には `Array.Empty<float>()` (長さ 0 配列) を渡し、適用経路は `BoneLocalRotations` のみに限定する (既存の M-3 合意変更方針に準拠)。
3. The Adapter shall `HumanoidMotionFrame.Timestamp` を `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻し、VMC 送信側のタイムスタンプは使用しない。
4. While 初期版, the Adapter shall VMC `RootPosition` / `RootRotation` を `HumanoidMotionFrame.RootPosition` / `RootRotation` に格納してよいが、avatar root Transform への書込は下流 Applier 側で既定無効であることを前提とする (本 Spec 側で Root Transform 書込を強制しない)。
5. If EVMC4U の内部 Dictionary にまだ 1 件もボーン情報が到着していない状態で Adapter が Tick されたとき, then the Adapter shall `HumanoidMotionFrame` を発行しない (空 frame の連続発行による無駄な通知を避ける)。
6. The Adapter shall 各 Tick で生成する `BoneLocalRotations` 辞書を、EVMC4U 側 Dictionary の参照ではなく Tick 時点の snapshot コピーとする (`HumanoidMotionFrame` はイミュータブルであることが `_shared/contracts.md` §2.2 で規定されているため)。
7. The Adapter shall 受信状況に関わらず VMC Blend Shape / 表情データを `HumanoidMotionFrame` に含めない (初期版では `IFacialController` への橋渡しを行わず、EVMC4U が独自に Blend Shape 適用する挙動に干渉しない)。

---

### 要件 4: スレッドモデル — 受信と Tick の分離

**目的:** As a ランタイム統合者, I want 受信処理と Frame 発行処理を分離する EVMC4U 準拠のモデルを採用すること, so that 1 bundle ≠ 1 frame 問題を回避し、Unity の LateUpdate サイクルに同期した安定したフレーム発行ができる。

#### 受け入れ基準

1. The Adapter shall uOSC の `onDataReceived` が Unity MainThread で発火することを前提とし、受信スレッドモデルを EVMC4U の既定 (MainThread 受信) に委ねる。
2. The Adapter shall 受信コールバックで直接 `Subject.OnNext()` を呼ばず、受信データは EVMC4U が内部 Dictionary にキャッシュする構造を利用する。
3. The Adapter shall Unity の `LateUpdate` タイミングで実行される Tick 処理において、EVMC4U の内部 Dictionary を snapshot し、その snapshot を `HumanoidMotionFrame` として `Subject.OnNext()` で発行する。
4. The Adapter shall Tick 処理を Unity の `MonoBehaviour.LateUpdate` または同等の PlayerLoop フェーズで駆動する (具体的な実装クラスは design フェーズで確定)。
5. When `Shutdown()` / `Dispose()` が呼ばれたとき, the Adapter shall Tick 駆動を停止し、購読者への `OnCompleted()` 発行 (または購読解除) を行ったうえでリソースを解放する。
6. The Adapter shall `Subject` への `OnNext()` 呼び出しがすべて Unity MainThread 上で行われることを保証する (`Subject.Synchronize()` 等のスレッドセーフラッパーは過剰同期として任意採用可、但し必須ではない)。
7. The Adapter shall `MotionStream` を複数 Slot / 複数購読者が共有できるマルチキャスト Observable として公開する (`Publish().RefCount()` 等の具体実装は design フェーズで確定)。

---

### 要件 5: `VMCMoCapSourceConfig` の継続と Factory キャスト責務

**目的:** As a エディタユーザー・ランタイム統合者, I want 既存の `VMCMoCapSourceConfig` アセットと Descriptor 設定がそのまま使えること, so that UI Sample やランタイム Slot 追加経路を改修せずに EVMC4U 版 Adapter へ移行できる。

#### 受け入れ基準

1. The `mocap-vmc` Spec shall `VMCMoCapSourceConfig : MoCapSourceConfigBase` を `RealtimeAvatarController.MoCap.VMC` 名前空間で維持し、既存の public フィールド (受信ポート番号 `port`、受信アドレス `bindAddress`) を保持する。
2. The `VMCMoCapSourceConfig` shall `ScriptableObject` 派生クラスであり、`.asset` 編集 (シナリオ X) と `ScriptableObject.CreateInstance<VMCMoCapSourceConfig>()` によるランタイム動的生成 (シナリオ Y) の両方を引き続き許容する (`contracts.md` §1.5 に準拠)。
3. The `VMCMoCapSourceConfig` shall ポート番号の有効範囲として 1025〜65535 を維持し、範囲外の値が設定された場合は Adapter の `Initialize()` で例外をスローする。
4. The `mocap-vmc` Spec shall `VMCMoCapSourceFactory : IMoCapSourceFactory` を維持し、`Create(MoCapSourceConfigBase config)` 内で `config as VMCMoCapSourceConfig` のキャストを行い、キャスト結果が `null` の場合は `ArgumentException` を受け取った型名付きでスローする。
5. The `VMCMoCapSourceFactory` shall Adapter インスタンスを生成して返すこと。Adapter が生成された時点で Factory は EVMC4U `ExternalReceiver` の存在を確認し、必要に応じて生成指示を出してよい (最終的な責務分担は design フェーズで確定する)。
6. When Factory が `Create()` で生成する Adapter が同一の `VMCMoCapSourceConfig` インスタンスに対して既に存在するとき, the `MoCapSourceRegistry` shall 既存 Adapter を参照共有し、新規 Adapter を生成しない (`contracts.md` §1.4 の参照共有セマンティクスに準拠)。

---

### 要件 6: Factory の属性ベース自己登録

**目的:** As a 他 Spec のユーザー, I want `VMCMoCapSourceFactory` が `RegistryLocator.MoCapSourceRegistry` に `typeId="VMC"` で自動登録されること, so that Editor / Player いずれの実行経路でも Slot から VMC ソースを解決できる。

#### 受け入れ基準

1. The `VMCMoCapSourceFactory` shall `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 属性を持つ静的メソッドを経由して `RegistryLocator.MoCapSourceRegistry.Register("VMC", new VMCMoCapSourceFactory())` を呼び、Player ビルドおよびランタイム起動時に自己登録する。
2. The `VMCMoCapSourceFactory` shall `[UnityEditor.InitializeOnLoadMethod]` 属性を持つ静的メソッドを経由して、Editor 起動時 (Inspector UI での候補列挙向け) にも同一の登録処理を実行する。このメソッドは `#if UNITY_EDITOR` ガード内または `RealtimeAvatarController.MoCap.VMC.Editor` asmdef に配置する。
3. If 同一 `typeId="VMC"` が既に登録されている状態で `Register()` が呼ばれたとき, then the `IMoCapSourceRegistry` shall `RegistryConflictException` をスローし、Factory 側の自己登録メソッドは例外を握り潰さずに `RegistryLocator.ErrorChannel` 経由で `SlotErrorCategory.RegistryConflict` として通知する (`contracts.md` §1.7 に準拠)。
4. Where Unity の Domain Reload が無効化されている場合, the `RegistryLocator.ResetForTest()` shall `SubsystemRegistration` タイミングで呼ばれて Registry が自動リセットされ、Factory 側で追加の対応を要しない (`contracts.md` §1.6 に準拠)。

---

### 要件 7: Slot との紐付けとライフサイクル

**目的:** As a ランタイム統合者, I want 実行中に Slot の VMC ソースを追加・削除・差替できること, so that 配信中の設定変更や UI からの操作に応じて VMC ソースを柔軟に切り替えられる。

#### 受け入れ基準

1. The `SlotManager` shall `MoCapSourceDescriptor { SourceTypeId="VMC", Config=VMCMoCapSourceConfig }` を `IMoCapSourceRegistry.Resolve(descriptor)` へ渡すことで Adapter インスタンスを取得する。Adapter の生成・再利用・参照カウントは `MoCapSourceRegistry` が担う。
2. When Slot を解放するとき, the `SlotManager` shall `IMoCapSourceRegistry.Release(source)` を呼び、Adapter の `Dispose()` / `Shutdown()` を直接呼ばない。
3. When 既存の Adapter を Slot から切り離して新しいソースへ差し替えるとき, the `SlotManager` shall 旧ソースに対して `Release()` を呼び出してから新ソースを `Resolve()` で取得する。
4. If 差替操作中 (旧ソース解放〜新ソース取得の間) にフレーム更新が発生したとき, then the Slot のパイプライン shall フレームをスキップしても例外をスローしない。
5. The Adapter shall 初期化前 / 動作中 / 停止後の内部状態を管理し、状態外の操作 (例: 二重 `Initialize()`) が呼ばれた場合は `InvalidOperationException` をスローする。
6. While UI Sample のランタイム Slot 追加・削除フロー, the `VMCMoCapSourceConfig.asset` および `SlotSettings.asset` を更新せずに (本 Spec の内部実装を EVMC4U 版に置き換えただけで) 既存シーンが動作し続ける。

---

### 要件 8: エラー処理と診断

**目的:** As a 開発者, I want VMC ソースの異常状態が `ISlotErrorChannel` 経由で通知されること, so that 購読者 (`MotionStream`) 側にエラー回復ロジックを持たせずに運用監視とトラブルシューティングができる。

#### 受け入れ基準

1. The Adapter shall `MotionStream` の `OnError()` を一切発行しない (`contracts.md` §2.1 / 旧要件 7-1 の方針を継続する)。
2. If EVMC4U が受信エラーを検知して内部的に処理したとき, then the Adapter shall `MotionStream` をエラー終端させず、次の有効フレームを待ち続ける。EVMC4U 側がログ出力 (`Debug.LogError` 等) を行う場合はそれに任せ、本 Adapter 側で二重ログを出さない。
3. When Adapter が検知可能な例外 (Adapter 自身の変換処理での例外、`ExternalReceiver` が null 参照で動作停止した場合等) が発生したとき, the Adapter shall `RegistryLocator.ErrorChannel.Publish(SlotError(slotId, SlotErrorCategory.VmcReceive, ex, UtcNow))` を呼ぶ。
4. If OSC ソケットのバインドに失敗したとき (ポート競合、権限不足等), then the Adapter shall `Initialize()` から例外をスローして呼び出し元に伝播する。`SlotManager` が捕捉して `SlotErrorCategory.InitFailure` として `ISlotErrorChannel` に通知する (`contracts.md` §1.7)。
5. The Adapter shall `Debug.LogError` の抑制制御 (同一 `(SlotId, Category)` の 1F 制限) を自身で持たず、`DefaultSlotErrorChannel` が担う抑制ロジックに委ねる。
6. While 受信タイムアウト処理の実装, the `mocap-vmc` Spec shall 初期版では実装しない (design フェーズで必要性を再検討する)。
7. The Adapter shall 診断カウンタ (受信パケット数・パース失敗数・Tick 発行数等) を公開する拡張余地を構造上持つが、初期版で実装することは必須としない。

---

### 要件 9: 互換性・VRM 対応

**目的:** As a エンドユーザー, I want VSeeFace / VMagicMirror / VirtualMotionCapture 等の主要 VMC 送信アプリケーションと本システムが互換動作すること, so that 既存の配信セットアップを大きく変更せずに本システムを利用できる。

#### 受け入れ基準

1. The `mocap-vmc` implementation shall EVMC4U に委譲することで、EVMC4U が公式に互換性検証を行っている VMC 送信アプリ群 (VSeeFace, VMagicMirror, VirtualMotionCapture 等) に対して追加の実装変更なく受信動作する。
2. While VRM 0.x アバターを用いる場合, the Adapter shall EVMC4U と併せて正しくボーンローテーションを `HumanoidMotionFrame.BoneLocalRotations` として発行し、`HumanoidMotionApplier` 経由でアバターに適用できる。
3. Where VRM 1.x アバターを用いる場合, the Adapter shall EVMC4U 側の VRM 1.x サポート状況に準拠し、VRM 1.x 固有の追加検証は別 Spec または後続タスクで扱う (初期版では VRM 0.x を主対象とする)。
4. The `mocap-vmc` implementation shall VMC プロトコルの OSC アドレス体系 (例: `/VMC/Ext/Root/Pos`, `/VMC/Ext/Bone/Pos`, `/VMC/Ext/Blend/Val` 等) の対応範囲を EVMC4U の対応範囲に従わせ、本 Spec 側でアドレスごとの個別要件を追加しない。

---

### 要件 10: アセンブリ・名前空間・依存関係

**目的:** As a Spec 設計者, I want 成果物が適切なアセンブリ・名前空間に配置され、依存関係が明確に管理されること, so that 他 Spec との境界を保ちつつ EVMC4U への依存を適切な場所に閉じ込められる。

#### 受け入れ基準

1. The Adapter および関連クラス (`VMCMoCapSourceConfig` / `VMCMoCapSourceFactory`) shall `RealtimeAvatarController.MoCap.VMC` アセンブリ (asmdef) に配置する。
2. The `RealtimeAvatarController.MoCap.VMC` asmdef shall `RealtimeAvatarController.Core` (slot-core) / `RealtimeAvatarController.Motion` (motion-pipeline) / EVMC4U (`EVMC4U.asmdef`) / `com.hidano.uosc` への参照を持ち、逆方向の参照を持たない。
3. The `RealtimeAvatarController.MoCap.VMC` asmdef shall UniRx への参照を `RealtimeAvatarController.Core` 経由、または `MotionStream` 公開に必要な最小限の直接参照として持つ。
4. The Editor 自己登録用 asmdef (`RealtimeAvatarController.MoCap.VMC.Editor`) shall `RealtimeAvatarController.MoCap.VMC` / `RealtimeAvatarController.Core` / UnityEditor への参照を持つ。
5. The `mocap-vmc` implementation shall `Assets/EVMC4U/` 配下を改変する場合でも `Assets/EVMC4U/EVMC4U.asmdef` への破壊的変更 (namespace 変更や public API 名称変更) を行わず、追加フィールド / 追加メソッドによる拡張にとどめる。

---

### 要件 11: 旧自前実装の廃止

**目的:** As a メンテナ, I want 旧来の自前 VMC 実装 (`VmcMoCapSource` / `VmcOscAdapter` / `VmcFrameBuilder` / `VmcMessageRouter` / `VmcBoneMapper` / `VmcTickDriver` 等) が整理されていること, so that 新旧実装の二重稼働や参照の混乱を避けられる。

#### 受け入れ基準

1. The `mocap-vmc` implementation shall `Runtime/MoCap/VMC/` 配下の旧自前実装 (`VmcMoCapSource.cs`, `VmcMoCapSourceFactory.cs`, `VmcOscAdapter.cs`, `VmcFrameBuilder.cs`, `VmcMessageRouter.cs`, `VmcBoneMapper.cs`, `VmcTickDriver.cs`, 関連 `AssemblyInfo.cs`) を削除または EVMC4U ベース Adapter の内部実装へ統合する。
2. The `mocap-vmc` implementation shall 旧自前実装へのテスト参照 (`Tests/PlayMode/mocap-vmc/VmcMoCapSourceIntegrationTests.cs` 等) を EVMC4U ベース Adapter に対するテストへ置き換える。
3. The `mocap-vmc` implementation shall 既存の `VMCMoCapSourceConfig.asset` および Slot 参照を破壊せず、上位コード (UI Sample, SlotManagerBehaviour 等) が変更なしで新 Adapter を使用できる状態を維持する (Factory typeId `"VMC"` を維持する要件 5.4 と整合する)。
4. The `_shared/contracts.md` shall §2.2 `HumanoidMotionFrame` (含 `BoneLocalRotations` フィールド) の形状を変更しない。`mocap-vmc` 実装の置き換えは contracts 形状に影響を及ぼさない。
5. Where `_shared/contracts.md` §13.1 の旧記述で「`VmcMessageRouter` / `VmcFrameBuilder` / `Subject.OnNext` はワーカースレッド」と書かれた箇所がある場合, the contracts.md shall 「uOSC の `onDataReceived` は Unity MainThread で発火するため受信後の処理はすべて MainThread」と修正される (本 Spec の変更作業の一環として実施する)。

---

### 要件 12: テスト戦略

**目的:** As a 開発者, I want Adapter の品質をテストで担保できること, so that EVMC4U 依存の変換ロジックに回帰が生じた際に早期検知できる。

#### 受け入れ基準

1. The `mocap-vmc` testing shall EditMode および PlayMode の 2 系統の asmdef を維持する (`RealtimeAvatarController.MoCap.VMC.Tests.EditMode` / `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode`)。
2. The EditMode テスト shall 次をカバーする:
   - `VMCMoCapSourceConfig` の `MoCapSourceConfigBase` からのキャスト成功 / 型不一致時の `ArgumentException` スロー
   - `VMCMoCapSourceFactory` の属性ベース自己登録 (`typeId="VMC"` が `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に含まれる)
   - 同一 typeId 二重登録時の `RegistryConflictException` スロー
3. The PlayMode テスト shall EVMC4U の `ExternalReceiver` 内部 Dictionary (またはそれに準ずる state 書込ポイント) をテストハーネスから直接操作し、Adapter が次 Tick で `HumanoidMotionFrame` を発行すること、および `BoneLocalRotations` の内容が注入値と一致することを検証できる。実 OSC over UDP を用いた統合テストは任意とし、必須ではない。
4. When 複数 Slot (複数 Adapter) が同一 `VMCMoCapSourceConfig` を参照する状態をセットアップしたとき, the test shall 同一 Adapter インスタンスが参照共有されることを `MoCapSourceRegistry` 経由で確認できる。
5. While PlayMode テスト実行中, the test shall `RegistryLocator.ResetForTest()` を `[SetUp]` / `[TearDown]` で呼び出し、テスト間の Registry 状態を独立させる。
6. The `mocap-vmc` testing shall コードカバレッジの数値目標を初期版では設定しない (将来のリリースサイクルで必要に応じて追加)。
7. Where EVMC4U の内部 state を public 化した改変を行う場合, the test shall その public state を使用して注入を行い、改変箇所がテストカバレッジ上も意味を持つことを保証する。
