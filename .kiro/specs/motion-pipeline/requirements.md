# Requirements Document

## Introduction

本ドキュメントは `motion-pipeline` Spec の要件を定義する。`motion-pipeline` は Realtime Avatar Controller において MoCap ソースから Push 型ストリーム (`IObservable<MotionFrame>`) で受信したモーションデータを Slot 単位の内部キャッシュに保持し、Slot に設定された Weight 値に従ってアバターへ適用するパイプラインを提供する。依存 Spec は `slot-core` であり、`slot-core` が定義する `SlotSettings` / `IMoCapSource` / `IAvatarProvider` 等の抽象インターフェースを参照する。

**Wave A 反映事項 (dig ラウンド 1 確定済)**:
- `IMoCapSource` は Push 型 (`IObservable<MotionFrame>`) を採用。`FetchLatestMotion()` は廃止
- MoCap ソースは複数 Slot 間で参照共有。`MoCapSourceRegistry` が参照カウントでライフサイクルを管理
- `SlotSettings` は Descriptor ベース POCO (`MoCapSourceDescriptor` 等)

**dig ラウンド 2 確定事項**:
- **UniRx 採用**: リアクティブライブラリは UniRx (`com.neuecc.unirx`) を採用する。R3 は採用しない。UniRx の `Subject<T>` は `System.IObservable<T>` を実装するため、`IObservable<MotionFrame>` の型シグネチャは変更しない。`ObserveOnMainThread()` 等の Unity スレッド連携拡張メソッドは UniRx が提供する (`using UniRx;` が必要)
- **Weight 二値方針**: 初期版の有効な Weight 値は `{0.0 (skip), 1.0 (full apply)}` の二値のみ。`SlotSettings.weight` フィールド自体は将来の複数ソース混合シナリオのためのフックとして残す。`0.0 < weight < 1.0` の中間値セマンティクスは、複数ソース混合シナリオを導入する際に改めて定義する

**dig ラウンド 3 確定事項**:
- **Fallback 挙動**: Applier でエラーが発生した場合、`SlotSettings.fallbackBehavior` (`FallbackBehavior` enum) を参照して挙動を分岐する。`FallbackBehavior` enum の定義は `slot-core` Spec が担い (`RealtimeAvatarController.Core` 名前空間)、`motion-pipeline` は Core 経由で参照する
- **エラー通知**: Applier 内で発生した例外は `ISlotErrorChannel` に `SlotErrorCategory.ApplyFailure` カテゴリで発行する。`ISlotErrorChannel` は `slot-core` が提供し、motion-pipeline は Core 経由で間接利用する。`Debug.LogError` は `ISlotErrorChannel` 側で抑制管理されるため、motion-pipeline 側は明示的にストリームへ push するのみでよい
- **無効フレームとエラーの分離**: `MotionCache` から読み取った null/無効フレームは通常動作扱い (スキップ・前フレーム維持) であり、`ISlotErrorChannel` への発行は行わない。「Apply 例外」(Applier 呼び出し時の実行時例外) のみ `ApplyFailure` カテゴリで発行する

**dig ラウンド 4 確定事項**:
- **MotionFrame.Timestamp 仕様**: タイムスタンプは Stopwatch ベース monotonic (`double` 秒単位、App 起動基準)。`Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で取得し、受信ワーカースレッド上で打刻する。プロセス間比較は不可。将来ログ用途で `WallClock: DateTime?` フィールドの追加を検討するが、初期版では未実装
- **テスト asmdef 方針**: motion-pipeline は EditMode / PlayMode 両系統のテストアセンブリを持つ。命名は `RealtimeAvatarController.Motion.Tests.EditMode` / `RealtimeAvatarController.Motion.Tests.PlayMode`。カバレッジ目標は初期版では設定しない

## Boundary Context

- **In scope**:
  - モーションデータの中立表現の定義 (Humanoid 骨格ベース: HumanPose 相当の Muscle 配列 + Root 位置・回転)
  - Slot 単位の内部キャッシュ機構 (受信スレッドと Unity メインスレッドの分離)
  - Weight (0.0〜1.0) に従ったアバターへの適用処理 (クランプ付き)
  - Humanoid アバターへの適用層の実装
  - Generic 形式への拡張余地 (抽象インターフェースのみ; 具象実装はスコープ外)
  - ランタイム中のシームレスな MoCap 切替 / アバター切替の実現
  - 1 アクター多アバター / 多アクター 1 アバター等の応用シナリオの許容
  - `contracts.md` 2.2 章 (モーションデータ中立表現) の確定
- **Out of scope**:
  - Generic 形式の具象実装
  - MoCap ソース具象実装 (mocap-vmc Spec の責務)
  - アバター供給処理 (avatar-provider-builtin Spec の責務)
  - 表情制御 / リップシンク処理 (slot-core 抽象 + 将来 Spec の責務)
  - UI / サンプルシーン (ui-sample Spec の責務)
- **Adjacent expectations**:
  - `slot-core` が提供する `SlotSettings` の `weight` フィールドと `IMoCapSource` インターフェースを参照する
  - `mocap-vmc` が `IMoCapSource.MotionStream` (Push 型 `IObservable<MotionFrame>`) を通じて本 Spec が定義する中立表現を流す
  - 本 Spec のアセンブリは `RealtimeAvatarController.Motion` (asmdef: `RealtimeAvatarController.Motion`、配置パス: `Runtime/Motion/`)
  - `RealtimeAvatarController.Motion` アセンブリは `RealtimeAvatarController.Core` 経由で UniRx (`com.neuecc.unirx`) に間接依存する (contracts.md 6 章参照)。R3 は不採用
  - `IMoCapSource` インスタンスは複数 Slot 間で参照共有されるため、`MotionCache` は Slot ごとに独立して保持する。Slot が破棄された際の `IMoCapSource.Dispose()` 呼び出しは `MoCapSourceRegistry` (contracts.md 1.4 章) の責務であり、motion-pipeline 側は購読解除のみを行う

---

## Requirements

### Requirement 1: モーションデータ中立表現 (Humanoid)

**Objective:** As a パイプライン設計者, I want Humanoid 骨格向けのモーションデータを共通の中立表現として定義できること, so that MoCap ソース・アバター適用層・Slot キャッシュがすべて同一型を通じて疎結合に連携できる。

#### Acceptance Criteria

1. The `HumanoidMotionFrame` shall Unity `HumanPose` 相当の構造を持ち、Humanoid アバターの全 Muscle 値を `float[]` 配列で保持する。
2. The `HumanoidMotionFrame` shall Root の位置 (`Vector3`) および回転 (`Quaternion`) を保持するフィールドを持つ。
3. The `HumanoidMotionFrame` shall イミュータブル (値型 struct、または読み取り専用プロパティを持つ class) として設計し、誤った書き換えを防ぐ。
4. The `HumanoidMotionFrame` shall Muscle 値の総数が 0 の場合を「データなし / 初期化前」を示す無効フレームとして扱える設計とする。
5. The `HumanoidMotionFrame` は `RealtimeAvatarController.Motion` 名前空間に属する。

---

### Requirement 2: モーションデータ中立表現の基底型設計

**Objective:** As a パイプライン設計者, I want Humanoid / Generic など異なる骨格形式を統一的に扱える基底型を持てること, so that パイプライン上流・下流のコードが骨格形式に依存しない共通 API を利用できる。

> **Timestamp 仕様 (dig ラウンド 4 確定)**: タイムスタンプは Stopwatch ベース monotonic (`double` 秒単位、App 起動基準)。受信ワーカースレッドで打刻する。プロセス間比較は不可。将来ログ用途で `WallClock: DateTime?` フィールドの追加を検討するが、初期版では未実装とする。

#### Acceptance Criteria

1. The `MotionFrame` (抽象基底型) shall `HumanoidMotionFrame` および将来の Generic フレーム型が派生できる基底型として定義される。
2. The `MotionFrame` shall タイムスタンプフィールド `Timestamp` を `double` 型 (秒単位) で持つ。値は `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で算出される Stopwatch ベース monotonic 値であり、App 起動を基準とした相対値である。プロセス間比較は不可とする。
3. The `Timestamp` shall 受信ワーカースレッド上でフレーム構築時に打刻される。Unity メインスレッド API を使用しないため、スレッド安全に取得できる。
4. The `MotionFrame` shall 骨格種別を識別できるプロパティ (`SkeletonType` 列挙型等) の骨格を持つ。
5. `IMoCapSource.MotionStream` (`IObservable<MotionFrame>`) が流すフレーム型は `MotionFrame` 基底型 (または型パラメータ) として宣言し、design フェーズで最終シグネチャを確定する。Pull 型 (`FetchLatestMotion()`) は採用しない。
6. The `MotionFrame` は `RealtimeAvatarController.Motion` 名前空間に属する。
7. The `MotionFrame` shall `Timestamp` の用途として Slot 内フレーム順序整列・遅延計測・デバッグログを想定した設計とする。
8. 将来ログ用途で wall clock が必要な場合に備え、`WallClock: DateTime?` フィールドの追加を設計上の余地として残す。初期版では `WallClock` は**実装しない**。

---

### Requirement 3: Generic 形式への拡張余地

**Objective:** As a 拡張開発者, I want Humanoid 以外の骨格形式 (Generic) に対するインターフェースが抽象として確保されていること, so that 将来の Generic 具象実装を motion-pipeline の設計変更なしに追加できる。

#### Acceptance Criteria

1. The `IMotionApplier` (抽象インターフェース) shall アバター (GameObject またはコンポーネント参照) と `MotionFrame` を受け取ってモーションを適用するメソッドの骨格を持つ。
2. The `HumanoidMotionApplier` (Humanoid 具象実装) shall `IMotionApplier` を実装し、`HumanoidMotionFrame` を Unity `HumanPose` に変換して Humanoid アバターへ適用する。
3. When Generic 形式のモーション適用が将来実装される場合, the `IMotionApplier` shall 初期段階の設計変更なしに新しい具象クラスとして追加できる。
4. 初期段階において Generic 向け具象実装は存在しない; `IMotionApplier` の抽象定義のみが本 Spec の成果物となる。

---

### Requirement 4: Slot 単位の内部キャッシュと Push 型購読

**Objective:** As a ランタイム統合者, I want MoCap データの受信と Unity アバターへの適用を分離できること, so that 受信スレッドのレイテンシがメインスレッドの描画フレームレートに影響しない。

#### Acceptance Criteria

1. The `MotionCache` (またはそれに相当するバッファ型) shall Slot ごとに独立したインスタンスを持ち、最新の `MotionFrame` を保持する。複数 Slot が同一 `IMoCapSource` を共有参照する場合でも、各 Slot の `MotionCache` は互いに独立している。
2. The `MotionCache` shall `IMoCapSource.MotionStream` (`IObservable<MotionFrame>`) を購読することでフレームを受け取る (Push 型)。購読の開始は `MotionCache` の初期化時、または `IMoCapSource` の紐付け時に行う。
3. When `MotionCache` が同一 `MotionStream` を複数 Slot から並列購読する場合, the パイプライン shall UniRx のマルチキャスト (`Publish().RefCount()` 等) が前提として機能することを要件段階で許容する。最終的な購読構成は design フェーズで確定する。
4. `MotionStream` の受信スレッドから `MotionCache` へのフレーム書き込みについて、以下の 2 方式をどちらも要件段階では許容し、design フェーズで選択する:
   - **方式 A**: 購読時に `.ObserveOnMainThread()` を適用し、フレーム受信からキャッシュ書き込みまでをメインスレッドで完結させる
   - **方式 B**: 受信スレッドでスレッドセーフなプリミティブを使ってキャッシュへ書き込み、メインスレッドの `LateUpdate` タイミングで読み出す (ダブルバッファ / `Interlocked` 等)
5. When Slot が破棄される場合, the `MotionCache` shall 購読の解除 (`IDisposable.Dispose()`) のみを行う。`IMoCapSource` 自体の `Dispose()` は `MoCapSourceRegistry` が参照カウントをもとに制御するため、`MotionCache` や motion-pipeline 側から `IMoCapSource.Dispose()` を直接呼び出してはならない。
6. When `MotionCache` から読み取ったフレームが null または無効な場合, the パイプライン shall アバターへの適用をスキップし、前フレームのポーズを維持する。この動作は**通常動作扱い**であり、`ISlotErrorChannel` へのエラー発行は行わない。`ISlotErrorChannel` への発行対象は Applier 呼び出し時の実行時例外 (要件 13 参照) のみとする。
7. The `MotionCache` は `RealtimeAvatarController.Motion` 名前空間に属し、スレッドモデルの具体的な実装方式は design フェーズで確定する。

---

### Requirement 5: Weight に従ったモーション適用

**Objective:** As a ランタイム統合者, I want Slot に設定された Weight 値に従ってモーションをアバターへ適用できること, so that 将来の複数ソース混合シナリオに向けた拡張フックが確保される。

> **初期版 Weight 方針 (dig ラウンド 2 確定)**: 初期版の有効な Weight 値は `{0.0 (skip), 1.0 (full apply)}` の二値のみ。フィールド自体は将来の複数ソース混合シナリオのためのフックとして残す。`0.0 < weight < 1.0` の中間値セマンティクスは、複数ソース混合シナリオを導入する際に改めて定義する。

#### Acceptance Criteria

1. The `IMotionApplier` shall `MotionFrame` を適用する際に Weight 値 (float) を受け取るパラメータを持つ。Weight パラメータは将来の複数ソース混合シナリオのためのフックとして設計する。
2. When Weight が `1.0` の場合, the `IMotionApplier` shall `MotionFrame` を完全適用 (full apply) する。すなわち変換なしでアバターへ適用する。
3. When Weight が `0.0` の場合, the `IMotionApplier` shall アバターへのモーション適用をスキップし、前フレームのポーズを維持する (skip)。
4. When Weight に `0.0〜1.0` の範囲外の値が渡された場合, the `IMotionApplier` shall 値を `0.0〜1.0` にクランプして処理を継続する。

> **将来拡張 (中間値セマンティクス)**: `0.0 < weight < 1.0` の中間値 (複数 MoCap ソースの合成・フェードイン / アウト等) の挙動は、複数ソース混合シナリオ導入時に別途定義する。初期版では中間値が指定された場合の動作は未定義であり、実装に依存してよい。

---

### Requirement 6: Humanoid アバターへの適用層

**Objective:** As a ランタイム統合者, I want `HumanoidMotionFrame` を Humanoid アバターに適用できる具象クラスが提供されること, so that VMC 等から受信した MoCap データをそのままアバターの骨格制御に使用できる。

#### Acceptance Criteria

1. The `HumanoidMotionApplier` shall `HumanoidMotionFrame` を Unity の `HumanPoseHandler` を用いて Humanoid アバターに適用できる。
2. The `HumanoidMotionApplier` shall `Animator` コンポーネントを持つ GameObject を受け取り、Humanoid アバターであることを検証してから初期化する。
3. When 対象 GameObject が Humanoid アバターでない場合, the `HumanoidMotionApplier` shall 初期化時に例外またはエラーを返し、適用処理を開始しない。
4. The `HumanoidMotionApplier` shall `IDisposable` を実装し、`HumanPoseHandler` 等の内部リソースを確実に解放する。
5. The `HumanoidMotionApplier` は `RealtimeAvatarController.Motion` 名前空間に属する。
6. When `HumanoidMotionApplier` のモーション適用処理 (`Apply()` 相当) で実行時例外が発生した場合, the `HumanoidMotionApplier` shall `SlotSettings.fallbackBehavior` を参照してフォールバック処理を実行し、発生した例外を `ISlotErrorChannel` に `SlotErrorCategory.ApplyFailure` カテゴリで発行する (要件 12・13 参照)。

---

### Requirement 7: Unity メインスレッド制約

**Objective:** As a Unity 統合者, I want モーション適用処理が Unity メインスレッドの適切なタイミングで実行されること, so that Unity API の呼び出し制約に違反しない安定した動作が保証される。

#### Acceptance Criteria

1. The `IMotionApplier.Apply()` (仮名) shall Unity メインスレッドからのみ呼び出されることを設計上の前提とする。
2. The パイプライン全体のモーション適用フェーズは `LateUpdate` タイミングでの実行を推奨とし、設計ドキュメントに明記する。
3. When `MotionCache` への書き込みが受信スレッドから行われる場合, the `MotionCache` shall Unity API を一切呼び出さず、スレッドセーフなプリミティブのみを使用する。
4. When `HumanPoseHandler` 等の Unity API を呼び出す場合, the 実装 shall メインスレッドからの呼び出しであることを前提とし、マルチスレッド対応を要求しない。

---

### Requirement 8: ランタイム中の MoCap 切替

**Objective:** As a ランタイム統合者, I want 実行中に MoCap ソースをシームレスに切り替えられること, so that 配信中に MoCap デバイスや接続先ポートを変更してもアバターが不自然に停止しない。

#### Acceptance Criteria

1. When `IMoCapSource` の参照が Slot に設定されている状態で別の `IMoCapSource` に切り替えが行われた場合, the `MotionCache` shall 旧ソースへの購読を解除し、新しいソースの `MotionStream` を購読することで切替を完了する。切替直後のフレームで新しいソースのデータを優先的に使用する。
2. When 切替直後に新しいソースからデータが届いていない場合, the パイプライン shall 前ソースの最終フレームまたはデフォルトポーズを維持してアバターを保護する。
3. The パイプライン shall `IMoCapSource` の切替処理をメインスレッドから実行できる API を提供する。
4. When MoCap ソースが切り替えられた場合, the motion-pipeline 側は旧 `IMoCapSource` への購読解除のみを行い、`IMoCapSource` の `Shutdown()` / `Dispose()` は `MoCapSourceRegistry.Release()` を呼び出した `SlotManager` の責務とする。motion-pipeline は `IMoCapSource` のライフサイクル終端を直接制御しない。

---

### Requirement 9: ランタイム中のアバター切替

**Objective:** As a ランタイム統合者, I want 実行中にアバターをシームレスに切り替えられること, so that 配信中にアバターモデルを変更してもモーション適用が即座に再開される。

#### Acceptance Criteria

1. When 新しいアバター GameObject が `IMotionApplier` に設定された場合, the `IMotionApplier` shall 次のフレームから新しいアバターへのモーション適用を開始する。
2. When アバターが切り替えられた場合, the `HumanoidMotionApplier` shall 旧アバター向けの `HumanPoseHandler` を破棄し、新アバター向けに再初期化する。
3. The パイプライン shall アバター切替処理をメインスレッドから呼び出せる API を提供する。
4. When アバター切替中 (初期化処理中) にモーションデータが到達した場合, the パイプライン shall 切替完了まで適用をスキップし、完了後に最新フレームを適用する。

---

### Requirement 10: 1 対多 / 多対多 応用シナリオの許容

**Objective:** As a ツール統合者, I want 1 アクターの MoCap データを複数アバターに適用できること、また複数アクターを個別に管理できること, so that マルチアバター配信やコラボ配信などの応用シナリオを実現できる。

#### Acceptance Criteria

1. The パイプライン shall 複数の Slot が互いに独立した `MotionCache` と `IMotionApplier` を保持できる構造とする。
2. When 1 つの `IMoCapSource` が複数 Slot に関連付けられた場合, the パイプライン shall 各 Slot の `MotionCache` に同一のモーションデータを独立してコピー / 参照できる設計を許容する。
3. When 異なる Slot が異なる `IMoCapSource` を参照する場合, the パイプライン shall 各 Slot が独立して Weight 値とモーションデータを管理できる。
4. The パイプライン shall Slot 数に上限を設けず、Unity のパフォーマンス制約の範囲内で動的に拡張できる設計とする。

---

### Requirement 11: アセンブリ / 名前空間境界と UniRx 依存

**Objective:** As a アーキテクチャ担当者, I want motion-pipeline の成果物が規定のアセンブリ・名前空間に配置されること, so that 他 Spec との依存関係が明確に分離される。

> **UniRx 採用方針 (dig ラウンド 2 確定)**: リアクティブライブラリは **UniRx (`com.neuecc.unirx`) を採用する。R3 は採用しない**。UniRx の `Subject<T>` は `System.IObservable<T>` を実装するため、`IObservable<MotionFrame>` の型シグネチャに変更はない。`ObserveOnMainThread()` 等の Unity スレッド連携拡張メソッドは UniRx が提供する (`using UniRx;` を追加することで利用可能)。NuGet 依存を持たないため UPM 配布での scoped registry が OpenUPM 1 個のみで済む。

#### Acceptance Criteria

1. The motion-pipeline Spec の全クラス・インターフェースは `RealtimeAvatarController.Motion` 名前空間に属する。
2. The asmdef ファイルは `RealtimeAvatarController.Motion` として定義され、`Runtime/Motion/` に配置される。
3. The `RealtimeAvatarController.Motion` アセンブリは `RealtimeAvatarController.Core` アセンブリを参照し、`Samples.UI` 等の上位アセンブリを参照しない (contracts.md 6 章参照)。
4. The `RealtimeAvatarController.Motion` アセンブリは `RealtimeAvatarController.Core` が UniRx (`com.neuecc.unirx`) を依存として持つことを通じて、UniRx に間接依存する。`MotionCache` が `IObservable<MotionFrame>` を購読する実装において UniRx 型 (`IObservable<T>` / `IDisposable` 等) を直接使用してよい。UniRx の直接参照 (asmdef の references への `UniRx` 追加) が必要かどうかは design フェーズで確定する。
5. When テストコードを配置する場合, the テストアセンブリは `RealtimeAvatarController.Motion.Tests.EditMode` および `RealtimeAvatarController.Motion.Tests.PlayMode` の 2 系統を持つ (dig ラウンド 4 確定)。EditMode / PlayMode は別 asmdef として分割する。
6. エディタ限定コードが存在する場合, `RealtimeAvatarController.Motion.Editor` 名前空間を使用する。
7. `FallbackBehavior` enum および `ISlotErrorChannel` / `SlotError` / `SlotErrorCategory` は `RealtimeAvatarController.Core` 名前空間に属する型であり、本 Spec は定義しない。本 Spec はこれらを `RealtimeAvatarController.Core` アセンブリ参照経由で利用する。

---

### Requirement 12: Applier エラー時の Fallback 挙動 (dig ラウンド 3 確定)

**Objective:** As a ランタイム統合者, I want Applier でエラーが発生した際にアバターの表示状態を安全に保てること, so that 予期しない例外によってアバターが破綻した姿勢で固まることを防げる。

> **前提**: `FallbackBehavior` enum の定義は `slot-core` Spec (`RealtimeAvatarController.Core` 名前空間) が担う。motion-pipeline は参照のみ行う (定義責務なし)。

#### Acceptance Criteria

1. The `HumanoidMotionApplier` shall モーション適用処理 (`Apply()` 相当) で実行時例外が発生した場合、該当 Slot の `SlotSettings.fallbackBehavior` の値を参照して、以下のとおりフォールバック処理を実行する。
2. When `fallbackBehavior` が `FallbackBehavior.HoldLastPose` の場合, the `HumanoidMotionApplier` shall Applier 内部に保持している直前フレームのポーズ状態をそのまま維持し続ける。アバターの `HumanPoseHandler` への再書き込みは行わず、視覚的な変化が生じないようにする。これがデフォルト挙動である。
3. When `fallbackBehavior` が `FallbackBehavior.TPose` の場合, the `HumanoidMotionApplier` shall `HumanPoseHandler` を通じてアバターを T ポーズ (全 Muscle 値 0、Root 位置・回転を初期値) にリセットする。T ポーズはデバッグ用途向けであり、問題発生を視覚的に認識しやすくする。
4. When `fallbackBehavior` が `FallbackBehavior.Hide` の場合, the `HumanoidMotionApplier` shall アバター GameObject に付属するすべての `Renderer` コンポーネントを無効化 (`enabled = false`) して描画を停止する。このとき GameObject 自体は破棄せず生存させる。
5. When `FallbackBehavior.Hide` 適用後にモーション適用が正常に再開された場合 (次フレームの `Apply()` が例外なく完了した場合), the `HumanoidMotionApplier` shall 無効化した `Renderer` コンポーネントを再度有効化 (`enabled = true`) してアバターの描画を復帰させる。
6. The フォールバック処理はメインスレッドで実行されることを前提とし、Unity API (`HumanPoseHandler`、`Renderer.enabled` 等) を安全に呼び出せる。
7. The フォールバック処理の実行後、`HumanoidMotionApplier` shall エラー通知を `ISlotErrorChannel` に発行する (要件 13 参照)。

---

### Requirement 13: Applier エラー通知 (dig ラウンド 3 確定)

**Objective:** As a ランタイム統合者, I want Applier 内で発生した例外が一元的なエラーチャネルに通知されること, so that UI 層や監視システムがエラーを購読して適切な対応を行える。

> **前提**: `ISlotErrorChannel` / `SlotError` / `SlotErrorCategory` の定義は `slot-core` Spec (`RealtimeAvatarController.Core` 名前空間) が担う。motion-pipeline は参照のみ行う。`Debug.LogError` の抑制管理は `ISlotErrorChannel` 側が担うため、motion-pipeline は明示的に push するだけでよい。

#### Acceptance Criteria

1. When `HumanoidMotionApplier` のモーション適用処理で実行時例外が発生した場合, the `HumanoidMotionApplier` shall 該当 Slot の `ISlotErrorChannel` に対し、以下の内容を持つ `SlotError` を発行する。
   - `SlotId`: 該当 Slot の識別子
   - `Category`: `SlotErrorCategory.ApplyFailure`
   - `Exception`: 発生した例外オブジェクト
   - `Timestamp`: エラー発生時刻 (UTC)
2. The エラー発行は、フォールバック処理 (要件 12) の実行**後**に行う。フォールバックが先に完了することを保証する。
3. `ISlotErrorChannel` への push は同一フレームの連続例外も含めて毎回行う。`Debug.LogError` の重複抑制は `ISlotErrorChannel` 実装側が管理するため、motion-pipeline 側では抑制フィルタリングを行わない。
4. The `HumanoidMotionApplier` は `ISlotErrorChannel` をコンストラクタまたは初期化メソッドで受け取る。インスタンス取得の具体的な方法 (Locator 経由 / DI 等) は design フェーズで確定する。
5. When `MotionCache` から読み取ったフレームが null または無効な場合 (要件 4 AC6 参照), the パイプライン shall `ISlotErrorChannel` へのエラー発行を行わない。無効フレームのスキップは通常動作であり、エラーではない。`ApplyFailure` として発行するのは Applier 呼び出し中の実行時例外のみとする。

---

### Requirement 14: テスト戦略と asmdef 構成 (dig ラウンド 4 確定)

**Objective:** As a 品質担当者, I want motion-pipeline のテストが EditMode / PlayMode の 2 系統に分割された専用アセンブリで管理されること, so that 純粋なロジックテストと実 Unity ランタイムを必要とするテストを適切に分離できる。

> **テスト戦略 (dig ラウンド 4 確定)**: EditMode / PlayMode 両系統の asmdef を持つ。カバレッジ目標は初期版では設定しない。

#### Acceptance Criteria

1. The motion-pipeline Spec のテストアセンブリは以下の 2 つの asmdef として分割する。
   - `RealtimeAvatarController.Motion.Tests.EditMode` — EditMode テスト専用 (Unity Editor TestRunner の EditMode モード)
   - `RealtimeAvatarController.Motion.Tests.PlayMode` — PlayMode テスト専用 (Unity Editor TestRunner の PlayMode モード)
2. The **EditMode テスト** (`RealtimeAvatarController.Motion.Tests.EditMode`) shall 以下の対象を検証する。
   - `MotionCache` の購読ライフサイクル (購読開始・解除・旧ソースの切替)
   - `HumanoidMotionFrame` の単位変換 (Muscle 値 / Root 位置・回転の変換ロジック)
   - `FallbackBehavior` の分岐ロジック (各 enum 値に対応する挙動)
   - Weight 二値判定 (`0.0` → スキップ、`1.0` → フル適用、範囲外 → クランプ)
3. The **PlayMode テスト** (`RealtimeAvatarController.Motion.Tests.PlayMode`) shall 以下の対象を検証する。
   - 実 Humanoid アバター (テスト用 Prefab) への `HumanoidMotionApplier` によるモーション適用
   - `FallbackBehavior.Hide` の視覚的動作確認 (Renderer の無効化 / 復帰)
4. The 2 つのテストアセンブリは `Tests/EditMode/` および `Tests/PlayMode/` にそれぞれ配置する (具体的なパスは design フェーズで確定)。
5. カバレッジ目標は初期版では設定しない。テスト対象範囲の追加・変更は design / implementation フェーズで都度判断する。
