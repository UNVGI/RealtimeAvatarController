# Requirements Document

## Introduction

本ドキュメントは `slot-core` Spec の要件を定義する。`slot-core` は Realtime Avatar Controller の中核 Spec であり、Slot 概念に基づくデータモデル・ライフサイクル管理 API を提供するとともに、他 5 Spec が参照する公開抽象インターフェース (`IMoCapSource` / `IAvatarProvider` / `IFacialController` / `ILipSyncSource`) を定義する Wave 1 先行 Spec である。

## Boundary Context

- **In scope**:
  - Slot データモデル (設定項目・シリアライズ構造)
  - SlotRegistry / SlotManager の動的追加・削除 API
  - Slot ライフサイクル (生成・破棄・リソース所有)
  - 抽象インターフェース 4 種の定義 (`IMoCapSource` / `IAvatarProvider` / `IFacialController` / `ILipSyncSource`)
  - `_shared/contracts.md` の 1 章・2.1 章・3.1 章・4.1 章・5.1 章の確定
  - `RegistryLocator` 静的アクセスポイントの定義
  - `ISlotErrorChannel` / `SlotError` / `SlotErrorCategory` の定義
  - `FallbackBehavior` 列挙体の定義
  - 属性ベース自動登録機構 (`[RuntimeInitializeOnLoadMethod]` + `[InitializeOnLoadMethod]`) の仕組み提供
- **Out of scope**:
  - 抽象インターフェースの具象実装 (各担当 Spec で実施)
  - モーション中立表現の定義 (motion-pipeline Spec)
  - モーション適用処理 (motion-pipeline Spec)
  - 表情制御・リップシンクの具象実装 (初期段階では対象外)
  - UI / サンプルシーン (ui-sample Spec)
- **Adjacent expectations**:
  - `project-foundation` が Unity プロジェクト・アセンブリ定義を提供する前提で設計する
  - `mocap-vmc` は `IMoCapSource` を実装する; `avatar-provider-builtin` は `IAvatarProvider` を実装する
  - `motion-pipeline` は本 Spec が定義する Slot データモデルを参照し、モーションデータ中立表現 (2.2 章) を確定させる

---

## Requirements

### Requirement 1: Slot データモデル (Descriptor ベース)

**Objective:** As a ツール統合者, I want Slot が必要な設定項目を一箇所で保持できること, so that アバター・モーション・表情制御の設定を統一的に管理できる。

#### 背景 (dig ラウンド 1・2 反映)

`IAvatarProvider` / `IMoCapSource` 等のインターフェース型フィールドを Unity の標準シリアライズに直接配置することは不可能である。また、利用可能な具象型はランタイムのプロジェクト構成に依存して動的に決まる。これらの問題を解決するため、`SlotSettings` は「型 ID 文字列 + 型付き設定オブジェクト」からなる **Descriptor パターン**を採用する。

Config フィールドの型設計として **ScriptableObject 基底派生方式** (dig ラウンド 2 確定) を採用する。`ProviderConfigBase : ScriptableObject` を抽象基底として定義し、具象 Config は継承して定義する。これにより Inspector でのドラッグ&ドロップによる型安全参照を実現しつつ、具象型依存を Factory 側のキャストに閉じ込める。

#### Acceptance Criteria

1. The SlotSettings shall 以下のフィールドを保持する: `slotId` (string、必須)、`displayName` (string、必須)、`weight` (float 0.0〜1.0、必須)、`avatarProviderDescriptor` (AvatarProviderDescriptor、必須)、`moCapSourceDescriptor` (MoCapSourceDescriptor、必須)、`facialControllerDescriptor` (FacialControllerDescriptor、null 許容)、`lipSyncSourceDescriptor` (LipSyncSourceDescriptor、null 許容)、`fallbackBehavior` (FallbackBehavior、必須、省略時デフォルト `HoldLastPose`)。
2. The AvatarProviderDescriptor および MoCapSourceDescriptor shall `providerTypeId` / `sourceTypeId` (string) と型付き Config オブジェクトを保持する。`typeId` は Registry に登録された具象型を識別するキーとして使用される。Config フィールドの型はそれぞれ `ProviderConfigBase` / `MoCapSourceConfigBase` (ScriptableObject 基底派生) を使用し、Inspector でのドラッグ&ドロップを可能にする。
3. The SlotSettings shall `[Serializable]` 属性を持つ POCO として定義され、ScriptableObject 継承なしでも Unity シリアライズ・ユニットテストで使用できる。
4. When SlotSettings を ScriptableObject として保持する場合, the SlotSettings shall Unity エディタでアセット (.asset) として保存・管理できる (ScriptableObject 継承は任意)。
5. When Weight 値に 0.0〜1.0 の範囲外の値が設定された場合, the SlotSettings shall 値を 0.0〜1.0 にクランプして保持する。**初期版では `weight` は常に `1.0` として使用する。** `weight` フィールドは将来の複数ソース混合シナリオのためのフックとして残すが、`0.0 < weight < 1.0` の中間値セマンティクスは初期版では未定義であり、複数ソース混合シナリオを導入する際に改めて定義する。`0.0` (skip) と `1.0` (full apply) の二値動作のみが初期版の有効値である。
6. The SlotSettings shall `slotId` フィールドを一意に識別するための主キーとして使用できる。
7. The SlotSettings shall インターフェース型フィールド (`IAvatarProvider` 等) を直接保持しない。具象型への参照解決は Registry/Factory が担い、`SlotSettings` 自体は具象型を知らない設計とする。

---

### Requirement 2: SlotRegistry / SlotManager の動的管理 API

**Objective:** As a ランタイム統合者, I want Slot を実行時に動的に追加・削除・取得できること, so that 複数アバター・複数ソースの組み合わせをユーザー操作に応じて切り替えられる。

#### Acceptance Criteria

1. When 新規 Slot の追加が要求された場合, the SlotRegistry shall SlotSettings を登録し、一意の Slot 識別子で参照可能にする。
2. When 既存 Slot の削除が要求された場合, the SlotRegistry shall 対象 Slot を登録から除去し、関連リソースの解放トリガーを発行する。
3. When 同一 Slot 識別子で重複登録が試みられた場合, the SlotRegistry shall 例外またはエラーを返し、既存 Slot を上書きしない。
4. The SlotRegistry shall 登録済み Slot の一覧を取得できる API を提供する。
5. When Slot 識別子を指定して検索した場合, the SlotRegistry shall 対象の SlotSettings を返す。If 指定した識別子が存在しない場合, the SlotRegistry shall null または Not Found を示す値を返す。
6. The SlotManager shall SlotRegistry を通じて Slot の追加・削除・設定変更のオーケストレーションを担い、ライフサイクルイベントを外部に通知できる。
7. While SlotManager が初期化済み状態である間, the SlotManager shall スレッドセーフな Slot 参照の読み取り操作をサポートする。

---

### Requirement 3: Slot ライフサイクルとリソース所有権

**Objective:** As a ランタイム統合者, I want Slot の生成・破棄が明確に管理されること, so that リソースリークを防ぎ、予測可能なリソース所有関係を維持できる。

#### 背景 (dig ラウンド 1 / 3 反映)

`IMoCapSource` のライフサイクル所有を `SlotManager` から `MoCapSourceRegistry` に移す。複数 Slot が同一 `IMoCapSource` インスタンスを参照共有するため、Slot の破棄は `IMoCapSource` の即時解放を意味しない。また dig ラウンド 3 で、Slot 初期化失敗時の遷移と `ISlotErrorChannel` 経由のエラー通知が確定した。

#### Acceptance Criteria

1. When Slot が生成された場合, the SlotManager shall 関連する `IAvatarProvider` の初期化をトリガーできる。`IMoCapSource` の初期化は `MoCapSourceRegistry.Resolve()` 経由で行われる。
2. When Slot が破棄された場合, the SlotManager shall 関連する `IAvatarProvider` の解放処理をトリガーし、リソースを確実に解放する。`IMoCapSource` については `MoCapSourceRegistry.Release()` を呼び出すに留め、直接 `Dispose()` を呼び出してはならない。
3. The Slot shall 生成 (Created)・アクティブ (Active)・非アクティブ (Inactive)・破棄済み (Disposed) の各ライフサイクル状態を持つ。
4. When Slot の状態が変化した場合, the SlotManager shall 状態変化イベントを購読者に通知する。
5. If Slot 破棄中に例外が発生した場合, the SlotManager shall 例外をキャッチしてログに記録し、残余リソースの解放を継続する。
6. The IMoCapSource のライフサイクル所有は MoCapSourceRegistry が担い、SlotManager は所有権を持たない。参照カウントが 0 になったときのみ MoCapSourceRegistry が `Dispose()` を呼び出す。
7. **[dig ラウンド 3 追加]** When Slot の初期化中に例外が発生した場合 (Provider / Source の Resolve 失敗、Factory キャスト失敗 等), the SlotManager shall その例外を捕捉し、該当 Slot を `Created → Disposed` 状態に強制遷移させる。Disposed 状態に遷移した Slot はそれ以降 `Active` にならない。
8. **[dig ラウンド 3 追加]** When Slot の初期化失敗が発生した場合, the SlotManager shall `ISlotErrorChannel` に `SlotErrorCategory.InitFailure` のエラーを発行し、エラー詳細を通知する。

---

### Requirement 4: `IMoCapSource` 抽象インターフェース定義 (Push 型 / UniRx / 参照共有)

**Objective:** As a Spec 設計者, I want `IMoCapSource` の骨格が定義されること, so that mocap-vmc Spec が具象実装を作成でき、受信全フレームを低レイテンシで処理できる。

#### 背景 (dig ラウンド 1・2 反映)

Pull 型 (`FetchLatestMotion()`) はポーリング間隔によるフレーム欠落が発生しうる。受信全フレームを逃さず低レイテンシで処理するため、**Push 型 (UniRx `IObservable<MotionFrame>`)** を採用する。また、同一 MoCap ソースを複数 Slot で共有する構成を許容するため、インスタンスのライフサイクル所有を `MoCapSourceRegistry` に移す。

**採用ライブラリ: UniRx (`com.neuecc.unirx`) ― R3 は採用しない** (dig ラウンド 2 確定)。UniRx の `IObservable<T>` は `System.IObservable<T>` を実装しているため、`IMoCapSource.MotionStream` の型は `IObservable<MotionFrame>` (System 名前空間の標準型) のままで変更不要である。NuGet 依存を持たないため UPM 配布での scoped registry が OpenUPM 1 個のみで済み、配布手続きが簡素化される。

#### Acceptance Criteria

1. The IMoCapSource shall `IObservable<MotionFrame> MotionStream { get; }` プロパティを持つ Push 型インターフェースとして定義される。`FetchLatestMotion()` は定義しない。
2. The IMoCapSource shall 初期化メソッド (`Initialize`) および破棄メソッド (`Shutdown` または `IDisposable.Dispose()`) の骨格を持つ。
3. The IMoCapSource shall ソース種別を識別するメタデータプロパティ (`SourceType: string`) の骨格を持つ。
4. The IMoCapSource shall 通信パラメータ (ポート番号等) を `Initialize()` の引数として受け取る構造の骨格を持つ (引数型は design フェーズで確定)。
5. The MotionStream shall 受信スレッドから `Subject.OnNext()` で配信され、購読側が `.ObserveOnMainThread()` を使用することで Unity メインスレッドで処理できる設計とする。
6. The IMoCapSource の MotionStream shall `Publish().RefCount()` 等のマルチキャスト演算子により、複数 Slot からの同時購読を許容する設計とする。
7. The IMoCapSource のインスタンスライフサイクルは SlotManager ではなく MoCapSourceRegistry が管理する。Slot 側から直接 `Dispose()` を呼び出してはならない。
8. The 依存ライブラリとして **UniRx (`com.neuecc.unirx`)** を `RealtimeAvatarController.Core` アセンブリの依存に追加する (asmdef の references に `UniRx` を記載する)。R3 は採用しない。パッケージ取得は OpenUPM の scoped registry 経由とし、NuGet 依存は持たない。
9. **[dig ラウンド 3 追加]** The IMoCapSource.MotionStream shall `OnError` を発行しない。パースエラー・切断検知等の受信エラーが発生した場合は内部でエラーを処理 (ログ出力および/またはサイレント破棄) し、Observable ストリームとして購読を継続する設計とする。購読側は `OnError` ハンドラを必須としない。

---

### Requirement 5: `IAvatarProvider` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `IAvatarProvider` の骨格が定義されること, so that avatar-provider-builtin Spec が具象実装を作成できる。

#### Acceptance Criteria

1. The IAvatarProvider shall アバターを要求するメソッドの骨格を持ち、同期・非同期のいずれの具象実装も許容するシグネチャとする。
2. The IAvatarProvider shall アバター (GameObject / Prefab インスタンス) を解放するメソッドの骨格を持つ。
3. The IAvatarProvider shall Provider 種別を識別するメタデータプロパティの骨格を持つ。
4. Where Addressable Provider を将来実装する場合, the IAvatarProvider shall 非同期要求 API を通じて拡張できる余地を持つ。

---

### Requirement 6: `IFacialController` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `IFacialController` の受け口骨格が定義されること, so that 将来の具象実装がこのインターフェースに準拠できる。

#### Acceptance Criteria

1. The IFacialController shall 表情データを適用するメソッドの骨格を持つ (引数型の最終決定は design フェーズ)。
2. The IFacialController shall 初期化・解放メソッドの骨格を持つ。
3. The SlotSettings shall `IFacialController` 参照フィールドに null を許容し、Slot に表情制御を割り当てない状態を有効とする。

---

### Requirement 7: `ILipSyncSource` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `ILipSyncSource` の受け口骨格が定義されること, so that 将来の具象実装がこのインターフェースに準拠できる。

#### Acceptance Criteria

1. The ILipSyncSource shall リップシンクデータを取得または適用するメソッドの骨格を持つ (引数型の最終決定は design フェーズ)。
2. The ILipSyncSource shall 初期化・解放メソッドの骨格を持つ。
3. The SlotSettings shall `ILipSyncSource` 参照フィールドに null を許容し、Slot にリップシンクを割り当てない状態を有効とする。

---

### Requirement 8: 設定シリアライズ可能性 (POCO / SO / JSON 許容)

**Objective:** As a ユーザー, I want Slot 設定を保存・復元できること, so that アバター構成をプロジェクト間で再利用できる。

#### 背景 (dig ラウンド 1・2 反映)

`SlotSettings` の保持形式として ScriptableObject のみを前提とする設計を撤回し、POCO / SO / JSON の 3 形式をすべて許容する設計方針を採用する。Descriptor パターンの採用により、インターフェース型参照を直接シリアライズする問題は解消される。

Config フィールドの型は `ProviderConfigBase` / `MoCapSourceConfigBase` 等の ScriptableObject 基底派生クラスを使用する (dig ラウンド 2 確定)。これにより Descriptor 自体は POCO のまま保持しつつ、Config アセットは Unity エディタで `.asset` として管理可能となる。

#### Acceptance Criteria

1. The SlotSettings shall `[Serializable]` 属性を持つ POCO として定義され、ScriptableObject を継承しない形でも Unity シリアライズおよびユニットテストで使用できる。
2. When SlotSettings を ScriptableObject として保持する場合, the SlotSettings shall Unity エディタでアセット (.asset) として保存・読み込みが可能である (ScriptableObject 継承は任意の選択肢)。
3. The SlotSettings shall JSON へのシリアライズおよびデシリアライズをサポートできる設計とする (`JsonUtility` または Newtonsoft.Json による実装余地を確保する)。
4. The Descriptor フィールド (`AvatarProviderDescriptor` / `MoCapSourceDescriptor` 等) は `[Serializable]` POCO として定義され、インターフェース型フィールドを直接保持しない。これにより Unity 標準シリアライズが正常動作する。Config フィールドの型は `ProviderConfigBase` / `MoCapSourceConfigBase` / `FacialControllerConfigBase` / `LipSyncSourceConfigBase` (各 ScriptableObject 派生基底クラス) を使用し、具象型への依存を Descriptor から排除する。
5. The SlotRegistry shall 登録済み Slot 一覧を外部から列挙できる API を提供し、シリアライズツールからアクセス可能にする。

---

### Requirement 8.5: Config 基底型階層の定義 (ScriptableObject 派生方式)

**Objective:** As a Spec 設計者, I want Config オブジェクトが ScriptableObject 基底派生型の階層として定義されること, so that 具象 Config の追加が型安全に行え、Inspector でのドラッグ&ドロップ参照が可能になる。

#### 背景 (dig ラウンド 2 確定)

Descriptor の Config フィールドを `ScriptableObject` 直参照ではなく用途別の抽象基底クラス参照にすることで、型安全性と拡張性を両立する。Factory 実装側は具象型にキャストして設定値を取得する。

#### Acceptance Criteria

1. The slot-core Spec shall `ProviderConfigBase : ScriptableObject`、`MoCapSourceConfigBase : ScriptableObject`、`FacialControllerConfigBase : ScriptableObject`、`LipSyncSourceConfigBase : ScriptableObject` の 4 つの抽象基底クラスを `RealtimeAvatarController.Core` 名前空間に定義する。
2. The `AvatarProviderDescriptor.Config` フィールドの型は `ProviderConfigBase` とし、`MoCapSourceDescriptor.Config` は `MoCapSourceConfigBase`、`FacialControllerDescriptor.Config` は `FacialControllerConfigBase`、`LipSyncSourceDescriptor.Config` は `LipSyncSourceConfigBase` を使用する。
3. The 具象 Config クラス (例: `BuiltinAvatarProviderConfig`、`VMCMoCapSourceConfig`) は各基底クラスを継承して定義する。基底クラスの定義は `slot-core` の責務とし、具象 Config の定義は各担当 Spec (`avatar-provider-builtin`、`mocap-vmc` 等) の責務とする。
4. The Factory インターフェース (`IAvatarProviderFactory`、`IMoCapSourceFactory`) の `Create` メソッド引数型は各 Config 基底クラス型 (`ProviderConfigBase`、`MoCapSourceConfigBase`) とし、具象 Factory 実装側でキャストして具象 Config を取得する。
5. The 各 Config 基底クラスは ScriptableObject を継承するため、Unity エディタで `.asset` として管理可能であり、Inspector でのドラッグ&ドロップ参照が可能である。

---

### Requirement 9: ProviderRegistry / SourceRegistry の動的登録と候補列挙

**Objective:** As a ランタイム統合者, I want 利用可能な IAvatarProvider / IMoCapSource の具象型を起動時に動的に登録し、エディタ UI から候補を列挙できること, so that プロジェクト構成に依存した具象型選択をランタイムで解決できる。

#### Acceptance Criteria

1. The IProviderRegistry shall `typeId` (string) をキーとして `IAvatarProviderFactory` を登録できる API (`Register`) を持つ。
2. The IProviderRegistry shall `AvatarProviderDescriptor` を受け取り、対応する `IAvatarProvider` インスタンスを生成して返す API (`Resolve`) を持つ。未登録 `typeId` の場合は明示的なエラー (例外または Result 型) を返す。
3. The IProviderRegistry shall 登録済みの `providerTypeId` 一覧を返す API (`GetRegisteredTypeIds`) を持ち、エディタ UI が利用可能な候補を列挙できる。
4. The IMoCapSourceRegistry shall `typeId` (string) をキーとして `IMoCapSourceFactory` を登録できる API (`Register`) を持つ。
5. The IMoCapSourceRegistry shall `MoCapSourceDescriptor` を受け取り、対応する `IMoCapSource` インスタンスを返す API (`Resolve`) を持つ。同一設定のインスタンスが既に存在する場合は参照を共有する。
6. The IMoCapSourceRegistry shall `IMoCapSource` の参照解放通知を受け取る API (`Release`) を持ち、参照数が 0 になった時点でインスタンスを `Dispose()` する (参照カウント方式またはその等価物)。
7. The IMoCapSourceRegistry shall 登録済みの `sourceTypeId` 一覧を返す API (`GetRegisteredTypeIds`) を持ち、エディタ UI が利用可能な候補を列挙できる。
8. **[dig ラウンド 3 確定]** The エントリ登録方式として**属性ベース自動登録**を採用する。具象 Factory 側に以下の 2 つの属性を付与した静的メソッドを定義し、Registry に自己登録する:
   - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`: ランタイム起動時 (シーンロード前) の自動登録
   - `[UnityEditor.InitializeOnLoadMethod]`: Editor 起動時の自動登録 (Inspector / エディタ UI 向け候補列挙)
   - `slot-core` は各具象 Factory が上記属性を利用するための仕組み (RegistryLocator) を提供する
   - 利用者の自前 Factory も同じ仕組みを用いて登録できる
9. **[dig ラウンド 3 確定]** When `Register()` が呼ばれた際に同一 `typeId` が既に登録されている場合, the Registry shall **例外をスローする**。「最後登録勝ち」による上書きは行わない。これによりデバッグ容易性を担保する。例外型の正式名称は design フェーズで確定する (`RegistryConflictException` 相当)。
10. **[dig ラウンド 3 確定]** The `RegistryLocator` shall `IProviderRegistry` および `IMoCapSourceRegistry` への静的アクセスポイントを提供し、Editor とランタイムで同一インスタンスを共有する。テスト用の `ResetForTest()` / `Override*()` API を持ち、Domain Reload OFF 設定下やユニットテストでの差し替えを可能にする。

---

### Requirement 10: MoCap ソース参照共有ライフサイクル

**Objective:** As a ランタイム統合者, I want 複数の Slot が同一 IMoCapSource インスタンスを共有参照できること, so that 同一の MoCap 入力を複数アバターに適用できる。

#### Acceptance Criteria

1. The IMoCapSourceRegistry shall 複数の Slot が同一 `IMoCapSource` インスタンスを参照共有できる設計をサポートする。同一 `MoCapSourceDescriptor` に対して複数の `Resolve()` 呼び出しが行われた場合、同一インスタンスを返す。
2. When Slot が破棄された場合, the SlotManager shall `IMoCapSourceRegistry.Release()` を呼び出し、参照カウントをデクリメントする。`IMoCapSource.Dispose()` を直接呼び出してはならない。
3. When MoCapSourceRegistry が持つ参照カウントが 0 になった場合, the MoCapSourceRegistry shall 対応する `IMoCapSource` インスタンスの `Dispose()` を呼び出してリソースを解放する。
4. The MotionStream のマルチキャスト (複数購読者サポート) は `IMoCapSource` 具象実装または `MoCapSourceRegistry` のラッパーで `Publish().RefCount()` 等を使用して実現する (詳細は design フェーズで確定)。
5. The 旧設計の「同一ポートへの複数バインド禁止」制約は撤回する。参照共有モデルにより、同一エンドポイントへの複数バインドは発生しない。

---

### Requirement 11: RegistryLocator の定義 (dig ラウンド 3 追加)

**Objective:** As a ツール統合者, I want `IProviderRegistry` / `IMoCapSourceRegistry` への静的アクセスポイントが提供されること, so that Editor とランタイムで同一の Registry インスタンスを共有し、属性ベース自動登録が正しく機能する。

#### Acceptance Criteria

1. The slot-core Spec shall `RegistryLocator` 静的クラスを `RealtimeAvatarController.Core` 名前空間に定義し、`IProviderRegistry` および `IMoCapSourceRegistry` への静的アクセスポイントを提供する。
2. The RegistryLocator shall Editor 起動時 (`[InitializeOnLoadMethod]`) とランタイム起動時 (`[RuntimeInitializeOnLoadMethod]`) の両方で同一の Registry インスタンスにアクセスできる設計とする。
3. The RegistryLocator shall テスト用に Registry インスタンスをリセットする API (`ResetForTest()`) を提供し、Domain Reload OFF 設定下での二重登録を防ぐ仕組みとして使用できる。`SubsystemRegistration` タイミングで自動的にリセットされることが望ましい。
4. The RegistryLocator shall テスト用に任意の `IProviderRegistry` / `IMoCapSourceRegistry` 実装を差し替える API (`Override*()`) を提供し、ユニットテストでのモック注入を可能にする。
5. The RegistryLocator のデフォルト実装インスタンス生成は遅延初期化 (最初のアクセス時に生成) とし、具体的な生成方式は design フェーズで確定する。

---

### Requirement 12: ISlotErrorChannel / SlotError / SlotErrorCategory の定義 (dig ラウンド 3 追加)

**Objective:** As a ツール統合者, I want Slot エラーの通知チャネルが UniRx ベースで提供されること, so that UI サンプルおよびユーザーコードがエラー情報を購読して適切に表示・対処できる。

#### Acceptance Criteria

1. The slot-core Spec shall `ISlotErrorChannel` インターフェースを `RealtimeAvatarController.Core` 名前空間に定義する。`ISlotErrorChannel.Errors` プロパティは `IObservable<SlotError>` を返し、UniRx `Subject<SlotError>` で実装される。購読側は `ObserveOnMainThread()` で Unity メインスレッドにて受信できる。
2. The slot-core Spec shall `SlotError` クラスを定義し、`SlotId` (string)、`Category` (SlotErrorCategory)、`Exception` (System.Exception、null 許容)、`Timestamp` (DateTime、UTC) の各フィールドを持つ。
3. The slot-core Spec shall `SlotErrorCategory` 列挙体を定義し、少なくとも `VmcReceive`、`InitFailure`、`ApplyFailure`、`RegistryConflict` の各値を含む。
4. The SlotManager shall Slot 初期化失敗 (`InitFailure`) および Applier エラー (`ApplyFailure`) の発生時に `ISlotErrorChannel` へエラーを発行する。
5. The Debug.LogError 出力は同一 `(SlotId, Category)` 組合せにつき **初回 1 フレームのみ**出力し、以降は抑制する。抑制の追跡には内部 `HashSet<(string, SlotErrorCategory)>` を使用する。
6. The `ISlotErrorChannel.Errors` への発行は抑制なく行う (Debug.LogError とは独立して動作する)。
7. The 抑制状態を管理する `HashSet` は `RegistryLocator.ResetForTest()` 等のリセット時に合わせてクリアする設計とする。

---

### Requirement 13: FallbackBehavior 列挙体と SlotSettings への組み込み (dig ラウンド 3 追加)

**Objective:** As a ランタイム統合者, I want Applier エラー発生時の Slot フォールバック挙動を Slot ごとに設定できること, so that 問題発生時の視覚表現をユースケースに応じて制御できる。

#### Acceptance Criteria

1. The slot-core Spec shall `FallbackBehavior` 列挙体を `RealtimeAvatarController.Core` 名前空間に定義し、`HoldLastPose`・`TPose`・`Hide` の 3 値を持つ。
2. The SlotSettings shall `fallbackBehavior: FallbackBehavior` フィールドを持ち、省略時のデフォルト値は `HoldLastPose` とする。
3. When Applier (モーション適用処理) でエラーが発生した場合, the SlotManager shall `SlotSettings.fallbackBehavior` に従いフォールバック処理を実行する:
   - `HoldLastPose`: 直前フレームのポーズを維持し続ける (視覚的崩れを最小化)
   - `TPose`: アバターを T ポーズに遷移させる (デバッグ用途)
   - `Hide`: アバターを非表示にする (破綻した表示を防ぐ)
4. When フォールバック処理が実行された場合, the SlotManager shall `ISlotErrorChannel` に `SlotErrorCategory.ApplyFailure` のエラーを発行する。
5. The フォールバック状態からの回復挙動 (エラーが解消した場合の処理) は design フェーズで確定する。
