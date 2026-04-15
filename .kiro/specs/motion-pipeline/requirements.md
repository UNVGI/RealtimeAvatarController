# Requirements Document

## Introduction

本ドキュメントは `motion-pipeline` Spec の要件を定義する。`motion-pipeline` は Realtime Avatar Controller において MoCap ソースから受信したモーションデータを内部キャッシュに保持し、Slot に設定された Weight 値に従ってアバターへ適用するパイプラインを提供する。依存 Spec は `slot-core` であり、`slot-core` が定義する `SlotSettings` / `IMoCapSource` / `IAvatarProvider` 等の抽象インターフェースを参照する。

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
  - `mocap-vmc` が `IMoCapSource.FetchLatestMotion()` を通じて本 Spec が定義する中立表現を返す
  - 本 Spec のアセンブリは `RealtimeAvatarController.Motion` (asmdef: `RealtimeAvatarController.Motion`、配置パス: `Runtime/Motion/`)

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

#### Acceptance Criteria

1. The `MotionFrame` (抽象基底型) shall `HumanoidMotionFrame` および将来の Generic フレーム型が派生できる基底型として定義される。
2. The `MotionFrame` shall タイムスタンプ (受信時刻相当の `double` または `long`) フィールドを持つ。
3. The `MotionFrame` shall 骨格種別を識別できるプロパティ (`SkeletonType` 列挙型等) の骨格を持つ。
4. `IMoCapSource.FetchLatestMotion()` の戻り値は `MotionFrame` 基底型 (または型パラメータ) として宣言し、design フェーズで最終シグネチャを確定する。
5. The `MotionFrame` は `RealtimeAvatarController.Motion` 名前空間に属する。

---

### Requirement 3: Generic 形式への拡張余地

**Objective:** As a 拡張開発者, I want Humanoid 以外の骨格形式 (Generic) に対するインターフェースが抽象として確保されていること, so that 将来の Generic 具象実装を motion-pipeline の設計変更なしに追加できる。

#### Acceptance Criteria

1. The `IMotionApplier` (抽象インターフェース) shall アバター (GameObject またはコンポーネント参照) と `MotionFrame` を受け取ってモーションを適用するメソッドの骨格を持つ。
2. The `HumanoidMotionApplier` (Humanoid 具象実装) shall `IMotionApplier` を実装し、`HumanoidMotionFrame` を Unity `HumanPose` に変換して Humanoid アバターへ適用する。
3. When Generic 形式のモーション適用が将来実装される場合, the `IMotionApplier` shall 初期段階の設計変更なしに新しい具象クラスとして追加できる。
4. 初期段階において Generic 向け具象実装は存在しない; `IMotionApplier` の抽象定義のみが本 Spec の成果物となる。

---

### Requirement 4: Slot 単位の内部キャッシュ

**Objective:** As a ランタイム統合者, I want MoCap データの受信と Unity アバターへの適用を分離できること, so that 受信スレッドのレイテンシがメインスレッドの描画フレームレートに影響しない。

#### Acceptance Criteria

1. The `MotionCache` (またはそれに相当するバッファ型) shall Slot ごとに最新の `MotionFrame` を保持する。
2. When 受信スレッドが新しい `MotionFrame` を書き込む場合, the `MotionCache` shall メインスレッドの読み取りをブロックしないスレッドセーフな更新を保証する。
3. The `MotionCache` shall メインスレッドから最新の `MotionFrame` を読み取る API を提供する。
4. When `MotionCache` から読み取ったフレームが null または無効な場合, the パイプライン shall アバターへの適用をスキップし、前フレームのポーズを維持する。
5. The `MotionCache` は `RealtimeAvatarController.Motion` 名前空間に属し、スレッドモデルの具体的な実装方式 (ダブルバッファ / lock / ロックレスキュー等) は design フェーズで確定する。

---

### Requirement 5: Weight に従ったモーション適用

**Objective:** As a ランタイム統合者, I want Slot に設定された Weight (0.0〜1.0) に従ってモーションをアバターへ適用できること, so that 複数 MoCap ソースの合成や段階的フェードイン / アウトを実現できる。

#### Acceptance Criteria

1. The `IMotionApplier` shall `MotionFrame` を適用する際に Weight 値 (float) を受け取るパラメータを持つ。
2. When Weight が 1.0 の場合, the `IMotionApplier` shall `MotionFrame` を変換なしでアバターへ適用する。
3. When Weight が 0.0 の場合, the `IMotionApplier` shall アバターへのモーション適用を行わない (または前フレームのポーズを維持する)。
4. When Weight が 0.0 より大きく 1.0 より小さい場合, the `IMotionApplier` shall `MotionFrame` のポーズをデフォルトポーズ (または現在のポーズ) と線形補間 (Lerp) して適用する。
5. When Weight に 0.0〜1.0 の範囲外の値が渡された場合, the `IMotionApplier` shall 値を 0.0〜1.0 にクランプして処理を継続する。

---

### Requirement 6: Humanoid アバターへの適用層

**Objective:** As a ランタイム統合者, I want `HumanoidMotionFrame` を Humanoid アバターに適用できる具象クラスが提供されること, so that VMC 等から受信した MoCap データをそのままアバターの骨格制御に使用できる。

#### Acceptance Criteria

1. The `HumanoidMotionApplier` shall `HumanoidMotionFrame` を Unity の `HumanPoseHandler` を用いて Humanoid アバターに適用できる。
2. The `HumanoidMotionApplier` shall `Animator` コンポーネントを持つ GameObject を受け取り、Humanoid アバターであることを検証してから初期化する。
3. When 対象 GameObject が Humanoid アバターでない場合, the `HumanoidMotionApplier` shall 初期化時に例外またはエラーを返し、適用処理を開始しない。
4. The `HumanoidMotionApplier` shall `IDisposable` を実装し、`HumanPoseHandler` 等の内部リソースを確実に解放する。
5. The `HumanoidMotionApplier` は `RealtimeAvatarController.Motion` 名前空間に属する。

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

1. When `IMoCapSource` の参照が Slot に設定されている状態で別の `IMoCapSource` に切り替えが行われた場合, the `MotionCache` shall 切替直後のフレームで新しいソースのデータを優先的に使用する。
2. When 切替直後に新しいソースからデータが届いていない場合, the パイプライン shall 前ソースの最終フレームまたはデフォルトポーズを維持してアバターを保護する。
3. The パイプライン shall `IMoCapSource` の切替処理をメインスレッドから実行できる API を提供する。
4. When MoCap ソースが切り替えられた場合, the 古い `IMoCapSource` shall `Shutdown()` および `Dispose()` が呼び出されてリソースが解放される。

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

### Requirement 11: アセンブリ / 名前空間境界

**Objective:** As a アーキテクチャ担当者, I want motion-pipeline の成果物が規定のアセンブリ・名前空間に配置されること, so that 他 Spec との依存関係が明確に分離される。

#### Acceptance Criteria

1. The motion-pipeline Spec の全クラス・インターフェースは `RealtimeAvatarController.Motion` 名前空間に属する。
2. The asmdef ファイルは `RealtimeAvatarController.Motion` として定義され、`Runtime/Motion/` に配置される。
3. The `RealtimeAvatarController.Motion` アセンブリは `RealtimeAvatarController.Core` アセンブリを参照し、`Samples.UI` 等の上位アセンブリを参照しない。
4. When テストコードを配置する場合, the テストアセンブリは `RealtimeAvatarController.Motion.Tests` 名前空間を使用する。
5. エディタ限定コードが存在する場合, `RealtimeAvatarController.Motion.Editor` 名前空間を使用する。
