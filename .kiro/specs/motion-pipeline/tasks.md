# Tasks: motion-pipeline

> **フェーズ**: tasks
> **言語**: ja
> **実行方法**: `/kiro:spec-run motion-pipeline`

---

## 大項目一覧

1. [asmdef・ディレクトリ構成の整備](#1-asmdefディレクトリ構成の整備)
2. [MotionFrame 型階層の実装](#2-motionframe-型階層の実装)
3. [IMotionApplier インターフェース定義](#3-imotionapplier-インターフェース定義)
4. [MotionCache の実装](#4-motioncache-の実装)
5. [HumanoidMotionApplier の実装](#5-humanoidmotionapplier-の実装)
6. [EditMode テストの実装](#6-editmode-テストの実装)
7. [PlayMode テストの実装](#7-playmode-テストの実装)
8. [Open Issue 対応](#8-open-issue-対応)

---

## 1. asmdef・ディレクトリ構成の整備

_Requirements: Req 11, Req 14_

### 1-1. Runtime asmdef の作成

- **ファイル**: `Packages/com.hidano.realtime-avatar-controller/Runtime/Motion/RealtimeAvatarController.Motion.asmdef`
- **内容**:
  - `name`: `RealtimeAvatarController.Motion`
  - `references`: `["RealtimeAvatarController.Core", "UniRx"]`
  - `allowUnsafeCode`: `false`
  - `autoReferenced`: `false`
- **前提条件**: `project-foundation` Spec が `RealtimeAvatarController.Core` asmdef を出力済みであること、および UPM パッケージ配置 (`com.hidano.realtime-avatar-controller`) が存在すること
- **サブディレクトリ作成**: `Frame/`、`Cache/`、`Applier/` の 3 サブディレクトリを作成する

### 1-2. EditMode テスト asmdef の作成

- **ファイル**: `Packages/com.hidano.realtime-avatar-controller/Tests/EditMode/Motion/RealtimeAvatarController.Motion.Tests.EditMode.asmdef`
- **内容**:
  - `name`: `RealtimeAvatarController.Motion.Tests.EditMode`
  - `references`: `["RealtimeAvatarController.Motion", "RealtimeAvatarController.Core", "UniRx", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`
  - `includePlatforms`: `["Editor"]`
  - `optionalUnityReferences`: `["TestAssemblies"]`
- **サブディレクトリ作成**: `Frame/`、`Cache/`、`Applier/` の 3 サブディレクトリを作成する

### 1-3. PlayMode テスト asmdef の作成

- **ファイル**: `Packages/com.hidano.realtime-avatar-controller/Tests/PlayMode/Motion/RealtimeAvatarController.Motion.Tests.PlayMode.asmdef`
- **内容**:
  - `name`: `RealtimeAvatarController.Motion.Tests.PlayMode`
  - `references`: `["RealtimeAvatarController.Motion", "RealtimeAvatarController.Core", "UniRx", "UnityEngine.TestRunner"]`
  - `optionalUnityReferences`: `["TestAssemblies"]`
- **サブディレクトリ作成**: `Applier/`、`Applier/Fixtures/` の 2 サブディレクトリを作成する

---

## 2. MotionFrame 型階層の実装

_Requirements: Req 1, Req 2, Req 3_

### 2-1. SkeletonType 列挙体の実装

**[TDD: 先にテスト 6-1 を記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Frame/SkeletonType.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **内容**:
  ```csharp
  public enum SkeletonType
  {
      Humanoid,
      Generic,
  }
  ```

### 2-2. MotionFrame 抽象基底クラスの実装

**[TDD: 先にテスト 6-1 を記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Frame/MotionFrame.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.3):
  - `public abstract class MotionFrame`
  - `public double Timestamp { get; }` — Stopwatch ベース秒単位 monotonic 値、読み取り専用
  - `public abstract SkeletonType SkeletonType { get; }`
  - `protected MotionFrame(double timestamp)` — コンストラクタ
  - `WallClock: DateTime?` フィールドは初期版では**実装しない** (設計余地として doccomment にコメントアウト記述のみ)
- **スレッド要件**: `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻。受信ワーカースレッドから安全に呼び出せること

### 2-3. HumanoidMotionFrame の実装

**[TDD: 先にテスト 6-1 を記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Frame/HumanoidMotionFrame.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.4, §4.1〜4.6):
  - `public sealed class HumanoidMotionFrame : MotionFrame`
  - `public override SkeletonType SkeletonType => SkeletonType.Humanoid;`
  - `public float[] Muscles { get; }` — 要素数 95 (`HumanTrait.MuscleCount`) または 0 (無効フレーム)
  - `public Vector3 RootPosition { get; }` — Root 位置 (読み取り専用)
  - `public Quaternion RootRotation { get; }` — Root 回転 (読み取り専用)
  - `public bool IsValid => Muscles.Length > 0;`
  - コンストラクタ: `HumanoidMotionFrame(double timestamp, float[] muscles, Vector3 rootPosition, Quaternion rootRotation)` — `muscles` が null の場合は `Array.Empty<float>()` を代入
  - ファクトリメソッド: `public static HumanoidMotionFrame CreateInvalid(double timestamp)`
- **イミュータブル要件**: 全プロパティは readonly。外部からの書き換え不可

> **OI-1 対応 (Muscles 配列のディープイミュータビリティ)**: 初期版はリアルタイム制約を優先し、`float[]` をコンストラクタに渡した時に内部コピー (ディープコピー) を行わず、参照渡しのまま保持する「許容」方針を採用する。外部から配列要素を書き換えた場合の挙動は未定義とし、呼び出し元 (MoCap ソース) が所有権を移譲することを doccomment で明記する。将来パフォーマンス要件やセキュリティ要件が変化した場合は `ReadOnlyMemory<float>` への移行を検討する。

### 2-4. GenericMotionFrame 抽象プレースホルダーの実装

- **ファイル**: `Runtime/Motion/Frame/GenericMotionFrame.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.5):
  - `public abstract class GenericMotionFrame : MotionFrame`
  - `public override SkeletonType SkeletonType => SkeletonType.Generic;`
  - `protected GenericMotionFrame(double timestamp) : base(timestamp) { }`
  - 具象フィールドは定義しない (将来の Generic Spec 向けプレースホルダーのみ)
  - 将来実装フィールド (`Bones` 等) を doccomment にコメントアウトで記載する

---

## 3. IMotionApplier インターフェース定義

_Requirements: Req 3, Req 5, Req 6, Req 7_

### 3-1. IMotionApplier インターフェースの実装

**[TDD: 先にテスト 6-4 の Weight テストを記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Applier/IMotionApplier.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.6):
  - `public interface IMotionApplier : IDisposable`
  - `void Apply(MotionFrame frame, float weight, SlotSettings settings);`
    - `frame`: 適用するフレーム。null または無効フレームの場合はスキップ
    - `weight`: 呼び出し元 (SlotManager) が事前に `Mathf.Clamp01` でクランプした値を渡す。Applier 内部ではクランプしない。初期版有効値: `0.0` / `1.0`
    - `settings`: 対象 Slot の設定 (`fallbackBehavior` 等の参照に使用)
  - `void SetAvatar(GameObject avatarRoot);`
    - null を渡すとアバターを切り離し、次の Apply 呼び出しをスキップ
    - Unity メインスレッドからのみ呼び出すこと
- **スレッド要件**: `Apply()` および `SetAvatar()` は Unity メインスレッドからのみ呼び出すことを doccomment に明記する

---

## 4. MotionCache の実装

_Requirements: Req 4, Req 7, Req 8, Req 10_

### 4-1. MotionCache の実装

**[TDD: 先にテスト 6-3 を記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Cache/MotionCache.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.8, §5):
  - `public sealed class MotionCache : IDisposable`
  - **内部フィールド**:
    - `private volatile MotionFrame _latestFrame;` — volatile + Interlocked で保護
    - `private IDisposable _subscription;` — 購読の IDisposable
  - **コンストラクタ**: `public MotionCache()` — 生成直後は購読を開始しない
  - **SetSource メソッド**: `public void SetSource(IMoCapSource source)` — メインスレッドからのみ呼び出す
    - 旧購読の `IDisposable.Dispose()` を呼んでから新ソースを購読
    - null を渡すと購読のみ解除 (`_latestFrame` は保持)
    - `source.MotionStream.Subscribe(OnReceive)` で購読開始
    - `IMoCapSource` 本体の `Dispose()` は**絶対に呼び出さない**
  - **LatestFrame プロパティ**: `public MotionFrame LatestFrame => Volatile.Read(ref _latestFrame);`
  - **OnReceive プライベートメソッド**: `private void OnReceive(MotionFrame frame)` — 受信スレッドから呼ばれる
    - `Interlocked.Exchange(ref _latestFrame, frame)` でアトミックに書き込む
  - **Dispose メソッド**: `public void Dispose()` — 購読の `IDisposable.Dispose()` のみ実行
    - `IMoCapSource` 本体の `Dispose()` は**絶対に呼び出さない**
  - **OnError コールバック省略**: `MotionStream` は `OnError` を発行しない (contracts.md §2.1 保証) ため、`Subscribe(onNext, onError)` の `onError` コールバックは省略してよい

- **スレッドモデル** (design.md §5.1 — 方式 B 採用):
  - 受信スレッドでは `Interlocked.Exchange` によりアトミックに参照を更新する
  - メインスレッドでは `Volatile.Read` で読み出す
  - `ObserveOnMainThread()` は使用しない (design.md §5.1 逸脱理由ブロック参照)

---

## 5. HumanoidMotionApplier の実装

_Requirements: Req 6, Req 8, Req 9, Req 12, Req 13_

### 5-1. HumanoidMotionApplier の実装

**[TDD: 先にテスト 6-5 および 7-1 を記述してから本実装を行うこと]**

- **ファイル**: `Runtime/Motion/Applier/HumanoidMotionApplier.cs`
- **名前空間**: `RealtimeAvatarController.Motion`
- **設計仕様** (design.md §3.7, §7, §8):
  - `public sealed class HumanoidMotionApplier : IMotionApplier`
  - **内部フィールド**:
    - `private HumanPoseHandler _poseHandler;`
    - `private HumanPose _lastGoodPose;`
    - `private Renderer[] _renderers;`
    - `private bool _isFallbackHiding;`
    - `private readonly string _slotId;`
  - **コンストラクタ**: `public HumanoidMotionApplier(string slotId)` — `ISlotErrorChannel` は受け取らない (発行責務は SlotManager)

- **SetAvatar メソッド実装**:
  - null 渡し時: `_poseHandler?.Dispose()`、`_renderers = null`、`_isFallbackHiding = false` でリセット
  - non-null 渡し時:
    - `avatarRoot.GetComponent<Animator>()` が null の場合: `InvalidOperationException` をスロー
    - `animator.isHuman == false` の場合: `InvalidOperationException` をスロー
    - 正常時: `new HumanPoseHandler(animator.avatar, avatarRoot.transform)` で初期化
    - `avatarRoot.GetComponentsInChildren<Renderer>(includeInactive: true)` で `_renderers` をキャッシュ
    - `_poseHandler.GetHumanPose(ref _lastGoodPose)` で初期ポーズを保持

- **Apply メソッド実装**:
  - `frame` が null または `_poseHandler == null` の場合: スキップ (例外なし)
  - `frame` が `HumanoidMotionFrame` 以外の型の場合: スキップ (例外なし)
  - `HumanoidMotionFrame.IsValid == false` の場合: スキップ (例外なし)
  - weight == 0.0 の場合: スキップ (SlotManager 側でクランプ済みのため、このチェックは保険)
  - weight == 1.0 の場合: 適用処理を実行
  - **ApplyInternal (内部ロジック)**:
    - `HumanPose pose` を構築 (Muscles 配列の値を `pose.muscles` にコピー、RootPosition/RootRotation を設定)
    - `_poseHandler.SetHumanPose(ref pose)` を呼び出す (例外が発生すれば呼び出し元に伝搬)
    - **例外を catch しない** — SlotManager が catch して Fallback / Publish を行う (§8.1 参照)
  - 正常完了時:
    - `_lastGoodPose` を更新
    - `_isFallbackHiding == true` の場合: `RestoreRenderers()` を呼び出す

- **FallbackBehavior ヘルパメソッドの実装** (SlotManager から呼び出す公開メソッドまたは SlotManager が直接制御する — design.md §11.2 注記より tasks フェーズで確定):
  - `ExecuteFallback(FallbackBehavior behavior)` を `public` または `internal` メソッドとして提供する
  - **HoldLastPose**: 何もしない (`_lastGoodPose` は更新済みの値を保持)
  - **TPose**: `new HumanPose()` (全 Muscle 値 0、`bodyPosition = Vector3.up`、`bodyRotation = Quaternion.identity`) を `_poseHandler.SetHumanPose()` で適用
  - **Hide**: `_renderers` の各 `Renderer.enabled = false`、`_isFallbackHiding = true`

- **RestoreRenderers プライベートメソッド**:
  - `_renderers` の各 `Renderer.enabled = true`、`_isFallbackHiding = false`

- **Dispose メソッド**:
  - `_poseHandler?.Dispose()`
  - `_renderers = null`

> **設計注記 — Applier は throw のみ**: `Apply()` 内の例外は catch せず呼び出し元 (SlotManager) に伝搬する。`ISlotErrorChannel` の参照は保持しない。FallbackBehavior の実行は SlotManager が `ExecuteFallback()` を呼び出すことで行う (design.md §8.1 / §9.1)。

---

## 6. EditMode テストの実装

_Requirements: Req 14_

### 6-1. HumanoidMotionFrame の単体テスト

- **ファイル**: `Tests/EditMode/Motion/Frame/HumanoidMotionFrameTests.cs`
- **テストクラス**: `HumanoidMotionFrameTests`
- **テストケース**:
  - `IsValid_WhenMusclesLength95_ReturnsTrue` — 要素数 95 の Muscles 配列を渡した場合 `IsValid == true`
  - `IsValid_WhenMusclesLengthZero_ReturnsFalse` — 要素数 0 (CreateInvalid) の場合 `IsValid == false`
  - `Constructor_WhenMusclesIsNull_DefaultsToEmptyArray` — null を渡した場合 `Muscles == Array.Empty<float>()`
  - `RootPosition_IsReadOnly` — コンストラクタで設定した `RootPosition` が読み取り専用プロパティとして保持されること
  - `RootRotation_IsReadOnly` — コンストラクタで設定した `RootRotation` が読み取り専用プロパティとして保持されること
  - `SkeletonType_IsHumanoid` — `SkeletonType == SkeletonType.Humanoid`
  - `CreateInvalid_ReturnsInvalidFrame` — `CreateInvalid(timestamp)` が `IsValid == false` かつ `Timestamp == timestamp` のフレームを返す

### 6-2. MotionFrame Timestamp の単体テスト

- **ファイル**: `Tests/EditMode/Motion/Frame/MotionFrameTimestampTests.cs`
- **テストクラス**: `MotionFrameTimestampTests`
- **テストケース**:
  - `Timestamp_IsPositive` — Stopwatch ベース値を渡した場合 `Timestamp > 0`
  - `Timestamp_IsPreserved` — コンストラクタで渡した timestamp 値がプロパティから正しく読み取れること
  - `Timestamp_IsMonotonicallyIncreasing` — 連続生成した 2 フレームの Timestamp が単調増加していること (テスト環境での近似確認)
  - `Timestamp_IsCalculatedCorrectly` — `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` の算出式で正値が得られること

### 6-3. MotionCache の単体テスト

- **ファイル**: `Tests/EditMode/Motion/Cache/MotionCacheTests.cs`
- **テストクラス**: `MotionCacheTests`
- **スタブ戦略**: UniRx の `Subject<MotionFrame>` を使ったスタブ `IMoCapSource` を実装する。実 `IMoCapSource` は使用しない
- **テストケース**:
  - `LatestFrame_BeforeSetSource_IsNull` — `SetSource()` 前は `LatestFrame == null`
  - `LatestFrame_AfterOnNext_IsUpdated` — Subject に `OnNext(frame)` を送信後、`LatestFrame` が更新されること
  - `SetSource_SwitchesSource_UnsubscribesOld` — 新ソース設定後、旧 Subject への `OnNext` が `LatestFrame` を更新しないこと
  - `SetSource_WithNull_UnsubscribesButKeepsLatestFrame` — `SetSource(null)` 後、旧フレームが `LatestFrame` に保持されること
  - `Dispose_UnsubscribesStream` — `Dispose()` 後、Subject への `OnNext` が `LatestFrame` を更新しないこと
  - `Dispose_DoesNotCallIMoCapSourceDispose` — `MotionCache.Dispose()` 時に `IMoCapSource.Dispose()` が呼ばれないこと (モックで検証)
  - `SetSource_WithNull_DoesNotCallIMoCapSourceDispose` — `SetSource(null)` 時に `IMoCapSource.Dispose()` が呼ばれないこと

### 6-4. Weight 二値判定のテスト

- **ファイル**: `Tests/EditMode/Motion/Applier/WeightTests.cs`
- **テストクラス**: `WeightTests`
- **テスト対象**: SlotManager の LateUpdate での Weight クランプ・Apply 呼び出しロジック (design.md §6.2 のサンプルコード相当)
- **テストケース**:
  - `Weight_Zero_SkipsApply` — weight == 0.0 のとき `IMotionApplier.Apply()` が呼ばれないこと (モック Applier で検証)
  - `Weight_One_CallsApply` — weight == 1.0 のとき `Apply()` が呼ばれること
  - `Weight_OutOfRange_Positive_ClampsToOne` — weight == 1.5 を SlotManager 側で `Mathf.Clamp01` した後、clamp 済み値 1.0 で `Apply()` が呼ばれること
  - `Weight_OutOfRange_Negative_ClampsToZero` — weight == -0.5 を SlotManager 側で `Mathf.Clamp01` した後 skip されること
  - `Apply_DoesNotClampInternally` — Applier 内部ではクランプ処理を行わないことを確認 (doccomment 遵守確認)

### 6-5. HumanoidMotionApplier Fallback の単体テスト

- **ファイル**: `Tests/EditMode/Motion/Applier/HumanoidMotionApplierFallbackTests.cs`
- **テストクラス**: `HumanoidMotionApplierFallbackTests`
- **スタブ・モック戦略**:
  - `ISlotErrorChannel` モックを `RegistryLocator.OverrideErrorChannel(mock)` で差し込む
  - テスト終了時に `RegistryLocator.ResetForTest()` でリセットする (`[TearDown]` に登録)
  - HumanoidMotionApplier コンストラクタへの `ISlotErrorChannel` 注入は不要
- **テストケース**:
  - `Apply_WhenExceptionThrown_PropagatesException` — `_poseHandler` への SetHumanPose が例外をスローした場合、`Apply()` が例外を再スローすること
  - `Fallback_HoldLastPose_DoesNothing` — SlotManager の catch ブロックで `ExecuteFallback(HoldLastPose)` を実行してもアバターのポーズが変化しないこと
  - `Fallback_TPose_ResetsMuscles` — `ExecuteFallback(TPose)` 後にアバターが T ポーズ (全 Muscle 0) になること
  - `Fallback_Hide_DisablesAllRenderers` — `ExecuteFallback(Hide)` 後にアバターの全 `Renderer.enabled == false` になること
  - `Fallback_Hide_SetsIsFallbackHidingTrue` — `_isFallbackHiding` フラグが `true` になること (Hide 後の正常 Apply で RestoreRenderers が呼ばれる前提)
  - `ApplySuccess_AfterHide_RestoresRenderers` — Hide 状態から正常 Apply が完了した場合、`Renderer.enabled == true` に復帰すること
  - `SlotManager_PublishesApplyFailure_OnException` — SlotManager の catch ブロックで `RegistryLocator.ErrorChannel.Publish()` が `SlotErrorCategory.ApplyFailure` で呼ばれること
  - `NullFrame_DoesNotPublishError` — `LatestFrame == null` の場合は `ErrorChannel.Publish()` が呼ばれないこと
  - `InvalidFrame_DoesNotPublishError` — `IsValid == false` のフレームでは `ErrorChannel.Publish()` が呼ばれないこと

---

## 7. PlayMode テストの実装

_Requirements: Req 14_

### 7-1. テスト用 Humanoid Prefab の準備

> **OI-3 対応**: テスト用 Humanoid Prefab の配置パスを確定する。

- **配置パス**: `Tests/PlayMode/Motion/Fixtures/TestHumanoidAvatar.prefab`
- **前提条件**: Humanoid リグが設定された `Animator` コンポーネントを持つ小型テスト用 FBX / Prefab を用意する。`project-foundation` または本 Spec の tasks フェーズ内で作成する
- **内容**: `Humanoid` アバター設定済みの `Animator` コンポーネントを持つ最小 Prefab。少なくとも 1 つの `SkinnedMeshRenderer` を含み、`Renderer.enabled` のトグルが確認できること

### 7-2. HumanoidMotionApplier 統合テスト

- **ファイル**: `Tests/PlayMode/Motion/Applier/HumanoidMotionApplierIntegrationTests.cs`
- **テストクラス**: `HumanoidMotionApplierIntegrationTests`
- **テストケース**:
  - `Apply_WithValidHumanoidFrame_ChangesAvatarPose` — テスト用 Humanoid Prefab を `Instantiate` し、`HumanoidMotionApplier.SetAvatar()` を設定後、`HumanoidMotionFrame` を `Apply()` してアバターのボーン回転が変化していることを確認
  - `SetAvatar_WithNonHumanoidObject_ThrowsInvalidOperationException` — Humanoid ではない GameObject を `SetAvatar()` に渡した場合 `InvalidOperationException` がスローされること
  - `SetAvatar_WithNull_SkipsNextApply` — `SetAvatar(null)` 後の `Apply()` が例外なくスキップされること
  - `SetAvatar_Switch_ReInitializesPoseHandler` — アバター切替時に旧 `HumanPoseHandler` が破棄され、新アバターへの適用が次フレームから開始されること

### 7-3. FallbackBehavior.Hide の視覚動作確認テスト

- **ファイル**: `Tests/PlayMode/Motion/Applier/FallbackHideTests.cs`
- **テストクラス**: `FallbackHideTests`
- **テストケース**:
  - `Hide_DisablesAllRenderers_OnApplyFailure` — `FallbackBehavior.Hide` が設定された Slot で Apply 例外が発生した場合、アバターの全 `Renderer.enabled == false` になること (実 Renderer コンポーネントで確認)
  - `Hide_KeepsGameObjectAlive` — Hide 後も GameObject が `Destroy()` されておらず生存していること
  - `Hide_Recovery_EnablesRenderers_OnNextSuccessfulApply` — Hide 状態から次フレームの正常 `Apply()` 完了後に全 `Renderer.enabled == true` に復帰していること

---

## 8. Open Issue 対応

### 8-1. OI-1: Muscles 配列イミュータビリティ方針の確定

_Requirements: Req 1 AC3_

- **方針**: 初期版はリアルタイム制約を優先し、配列のディープコピーを行わず参照渡しのまま保持する「許容」方針を採用する (タスク 2-3 の設計注記参照)
- **作業内容**: `HumanoidMotionFrame` の doccomment に「呼び出し元から所有権を移譲すること」を明記する

### 8-2. OI-3: テスト用 Humanoid Prefab の配置パス確定

_Requirements: Req 14 AC4_

- **確定パス**: `Tests/PlayMode/Motion/Fixtures/TestHumanoidAvatar.prefab` (タスク 7-1 参照)
- **作業内容**: design.md §13.3 に記載の `Tests/PlayMode/Motion/Fixtures/` をそのまま採用し、Prefab を配置する

### 8-3. OI-4: slot-core design.md §11.2 の Hide 実装記述更新

- **作業内容**: `slot-core` 担当に対し、design.md §11.2「Hide の実装は `Renderer.enabled = false` とする (GameObject.SetActive(false) ではない)」への更新を依頼する issue / コメントを作成する
- **本 Spec の作業**: motion-pipeline 側の確定仕様は `Renderer.enabled = false` (design.md §8.4 参照)。本タスクは slot-core 側への通知記録のみ

### 8-4. OI-5: contracts.md §1.7 の Applier エラー発行責務記述の更新確認

- **作業内容**: contracts.md §1.7「エラー通知の責務分担」テーブルが「Applier エラー | SlotManager が catch して FallbackBehavior 実行後に `ISlotErrorChannel.Publish()` を呼ぶ」と記述されているかを確認する。不整合がある場合は `slot-core` 担当と合意のうえ更新する
- **本 Spec の作業**: design.md §9.1 に「この責務分担は contracts.md §1.7 に準拠する」と記載済み。tasks フェーズでの確認のみ

---

## 実装順序ガイド

以下の順序で実装すると依存関係の問題が発生しない:

```
1. asmdef 整備 (タスク 1-1〜1-3)
   ↓
2. SkeletonType + MotionFrame + HumanoidMotionFrame + GenericMotionFrame (タスク 2-1〜2-4)
   ↓
3. IMotionApplier (タスク 3-1)
   ↓
4. MotionCache (タスク 4-1)
   ↓
5. HumanoidMotionApplier (タスク 5-1)
   ↓
6. EditMode テスト (タスク 6-1〜6-5) ← TDD: 各コンポーネント実装と並走
   ↓
7. PlayMode テスト Fixtures 準備 (タスク 7-1)
   ↓
8. PlayMode テスト (タスク 7-2〜7-3)
   ↓
9. Open Issue 対応 (タスク 8-1〜8-4)
```

> **TDD 方針**: 各コンポーネントの実装前に、対応する EditMode テストケースのシェルを先に記述する。テストが失敗することを確認してから実装を進める。PlayMode テストは Fixtures が揃った後に記述する。
