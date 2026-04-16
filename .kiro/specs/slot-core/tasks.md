# Implementation Tasks: slot-core

> **言語**: ja  
> **フェーズ**: tasks  
> **前提**: `project-foundation` Spec 完了済み (Unity 6000.3.10f1 プロジェクト・asmdef 雛形提供)  
> **参照**: `design.md` (全 11 章) / `requirements.md` (Req 1〜14) / `validation-design.md` (Open Issue 含む)

---

## タスク一覧

- [ ] 1. asmdef 配置とパッケージ依存の確立 (_Requirements: 4.8, 14.1, 14.2_)
  - [ ] 1.1 `Runtime/Core/` の asmdef ファイルを作成する
    - `RealtimeAvatarController/Runtime/Core/RealtimeAvatarController.Core.asmdef` を作成する
    - `references` に `UniRx` (`com.neuecc.unirx`) と `UniTask` (`com.cysharp.unitask`) を追加する
    - `allowUnsafeCode: false`、`autoReferenced: false` で設定する
    - _Requirements: 4.8_
  - [ ] 1.2 `Editor/Core/` の asmdef ファイルを作成する
    - `RealtimeAvatarController/Editor/Core/RealtimeAvatarController.Core.Editor.asmdef` を作成する
    - `includePlatforms: ["Editor"]` を設定し、`references` に `RealtimeAvatarController.Core` を追加する
    - `SlotSettingsEditor.cs` のプレースホルダーファイルを配置する (将来実装用スタブ)
    - _Requirements: 14.1_
  - [ ] 1.3 `Tests/EditMode/Core/` の asmdef ファイルを作成する
    - `RealtimeAvatarController/Tests/EditMode/Core/RealtimeAvatarController.Core.Tests.EditMode.asmdef` を作成する
    - `includePlatforms: ["Editor"]`、`references` に `RealtimeAvatarController.Core`・`UniRx`・`UniTask` を追加する
    - `optionalUnityReferences: ["TestAssemblies"]` で NUnit を有効化する
    - _Requirements: 14.1_
  - [ ] 1.4 `Tests/PlayMode/Core/` の asmdef ファイルを作成する
    - `RealtimeAvatarController/Tests/PlayMode/Core/RealtimeAvatarController.Core.Tests.PlayMode.asmdef` を作成する
    - `references` に `RealtimeAvatarController.Core`・`UniRx`・`UniTask` を追加する
    - `optionalUnityReferences: ["TestAssemblies"]` で NUnit を有効化する
    - _Requirements: 14.2_
  - [ ] 1.5 `package.json` に UniRx / UniTask の scoped registry 設定を追加する
    - `scopedRegistries` に `https://package.openupm.com` を追加し `com.neuecc.unirx` と `com.cysharp.unitask` を列挙する
    - `dependencies` に両パッケージのバージョンを記載する
    - _Requirements: 4.8_

- [ ] 2. Config 基底型階層の実装 (_Requirements: 8.5_)
  - [ ] 2.1 `ProviderConfigBase` を TDD で実装する
    - `Runtime/Core/Configs/ProviderConfigBase.cs` に `public abstract class ProviderConfigBase : ScriptableObject` を定義する
    - `[CreateAssetMenu]` は付けない (具象クラスが付与する設計)
    - EditMode テスト `ConfigBaseTests.cs` を先に書き、「SO 継承・インスタンス生成可能・CreateInstance 可能」を確認してからファイルを実装する
    - _Requirements: 8.5.1_
  - [ ] 2.2 `MoCapSourceConfigBase` を TDD で実装する
    - `Runtime/Core/Configs/MoCapSourceConfigBase.cs` に `public abstract class MoCapSourceConfigBase : ScriptableObject` を定義する
    - EditMode テストで SO 継承・CreateInstance を確認してから実装する
    - _Requirements: 8.5.1_
  - [ ] 2.3 `FacialControllerConfigBase` / `LipSyncSourceConfigBase` を TDD で実装する
    - `Runtime/Core/Configs/` 下に各ファイルを作成する
    - EditMode テストで `CreateInstance<FacialControllerConfigBase>()` / `CreateInstance<LipSyncSourceConfigBase>()` が呼べることを確認してから実装する (ConcreteSubclass 経由)
    - _Requirements: 8.5.1_
  - [ ] 2.4 Config 基底型の EditMode テストを完成させる
    - `Tests/EditMode/Core/ConfigBaseTests.cs` を作成する
    - テスト観点: 各基底クラスが ScriptableObject を継承していること、CreateInstance で生成した具象サブクラスが各基底型にキャストできること
    - _Requirements: 8.5.2, 8.5.5_

- [ ] 3. Descriptor 型群の実装 (_Requirements: 1.2, 8.4_)
  - [ ] 3.1 `AvatarProviderDescriptor` を TDD で実装する
    - `Runtime/Core/Descriptors/AvatarProviderDescriptor.cs` を作成する
    - `[Serializable]`・`sealed`・`IEquatable<AvatarProviderDescriptor>`・`operator ==` / `!=`・`GetHashCode()` (RuntimeHelpers 使用) を実装する
    - Config 等価判定は `ReferenceEquals` を使用すること
    - テストを先に `DescriptorTests.cs` に書いてから実装する
    - _Requirements: 1.2, 8.4_
  - [ ] 3.2 `MoCapSourceDescriptor` を TDD で実装する
    - `Runtime/Core/Descriptors/MoCapSourceDescriptor.cs` を作成する
    - 同一 Descriptor インスタンスが辞書キーとして機能することを EditMode テストで検証してから実装する
    - _Requirements: 1.2, 8.4_
  - [ ] 3.3 `FacialControllerDescriptor` / `LipSyncSourceDescriptor` を TDD で実装する
    - `Runtime/Core/Descriptors/` 下に各ファイルを作成する
    - 等価判定・GetHashCode・operator ==/!= を実装する
    - _Requirements: 1.2, 8.4_
  - [ ] 3.4 Descriptor の EditMode テストを完成させる
    - `Tests/EditMode/Core/DescriptorTests.cs` を作成する
    - テスト観点: 同一 typeId + 同一 Config 参照 → Equals 成立 / GetHashCode 一致、異なる Config 参照 → 非等価、typeId 違い → 非等価、null ガード (Equals(null) が false)、Dictionary キーとして正しく機能すること
    - _Requirements: 1.2, 14.3_

- [ ] 4. 抽象インターフェース定義 (_Requirements: 4, 5, 6, 7_)
  - [ ] 4.1 `IMoCapSource` を実装する
    - `Runtime/Core/Interfaces/IMoCapSource.cs` を作成する
    - `IDisposable` 継承・`string SourceType`・`void Initialize(MoCapSourceConfigBase config)`・`IObservable<MotionFrame> MotionStream`・`void Shutdown()` を定義する
    - `MotionFrame` は `motion-pipeline` で定義される型であるため、型プレースホルダーまたは `object` 仮定義を許容する (contracts.md §2.2 合意後に差し替え)
    - XML コメントに「OnError を発行しない」「購読側は ObserveOnMainThread() を使用すること」を明記する
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.9_
  - [ ] 4.2 `IAvatarProvider` を実装する
    - `Runtime/Core/Interfaces/IAvatarProvider.cs` を作成する
    - `IDisposable` 継承・`string ProviderType`・`GameObject RequestAvatar(ProviderConfigBase config)`・`UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)`・`void ReleaseAvatar(GameObject avatar)` を定義する
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  - [ ] 4.3 `IFacialController` を実装する
    - `Runtime/Core/Interfaces/IFacialController.cs` を作成する
    - `IDisposable` 継承・`void Initialize(GameObject avatarRoot)`・`void ApplyFacialData(object facialData)`・`void Shutdown()` を定義する
    - _Requirements: 6.1, 6.2_
  - [ ] 4.4 `ILipSyncSource` を実装する
    - `Runtime/Core/Interfaces/ILipSyncSource.cs` を作成する
    - `IDisposable` 継承・`void Initialize(LipSyncSourceConfigBase config)`・`object FetchLatestLipSync()`・`void Shutdown()` を定義する
    - _Requirements: 7.1, 7.2_
  - [ ] 4.5 Factory インターフェース群を実装する
    - `Runtime/Core/Factory/` 下に `IAvatarProviderFactory.cs`・`IMoCapSourceFactory.cs`・`IFacialControllerFactory.cs`・`ILipSyncSourceFactory.cs` を作成する
    - 各 `Create(XxxConfigBase config)` メソッドを定義する。XML コメントに「キャスト失敗時は ArgumentException をスローする」を明記する
    - _Requirements: 9.4, 9.8_

- [ ] 5. `FallbackBehavior` enum と `RegistryConflictException` の実装 (_Requirements: 13.1, 9.9_)
  - [ ] 5.1 `FallbackBehavior` enum を実装する
    - `Runtime/Core/Fallback/FallbackBehavior.cs` を作成する
    - `HoldLastPose` (デフォルト)・`TPose`・`Hide` の 3 値を定義する
    - `Hide` の XML コメントに「`Renderer.enabled = false` を使用。`GameObject.SetActive(false)` は使用しない」を明記する
    - _Requirements: 13.1_
  - [ ] 5.2 `RegistryConflictException` を実装する
    - `Runtime/Core/Error/RegistryConflictException.cs` を作成する
    - `sealed class RegistryConflictException : Exception`・`string TypeId`・`string RegistryName` を実装する
    - コンストラクタは `(string typeId, string registryName)` と `(string typeId, string registryName, Exception inner)` の 2 オーバーロードを提供する
    - _Requirements: 9.9_

- [ ] 6. エラーハンドリング基盤の実装 (_Requirements: 12_)
  - [ ] 6.1 `SlotErrorCategory` enum を実装する
    - `Runtime/Core/Error/SlotErrorCategory.cs` を作成する
    - `VmcReceive`・`InitFailure`・`ApplyFailure`・`RegistryConflict` の 4 値を定義する
    - _Requirements: 12.3_
  - [ ] 6.2 `SlotError` クラスを TDD で実装する
    - `Runtime/Core/Error/SlotError.cs` を作成する
    - `sealed` クラスとして `SlotId (string)`・`Category (SlotErrorCategory)`・`Exception (System.Exception, null 許容)`・`Timestamp (DateTime, UTC)` の各プロパティをコンストラクタ引数で初期化する (不変オブジェクト)
    - EditMode テストで各プロパティが正しく設定されることを確認してから実装する
    - _Requirements: 12.2_
  - [ ] 6.3 `ISlotErrorChannel` インターフェースを実装する
    - `Runtime/Core/Error/ISlotErrorChannel.cs` を作成する
    - `IObservable<SlotError> Errors { get; }` と `void Publish(SlotError error)` を定義する
    - _Requirements: 12.1_
  - [ ] 6.4 `DefaultSlotErrorChannel` を TDD で実装する
    - `Runtime/Core/Error/DefaultSlotErrorChannel.cs` を作成する
    - `internal sealed` クラスとして実装する
    - `private readonly ISubject<SlotError> _subject = new Subject<SlotError>().Synchronize();` で初期化する (Subject.Synchronize() は確定必須実装 — validation-design.md [N-1] 対応)
    - `Errors` は `_subject.AsObservable()` を返す
    - `Publish()` は常に `_subject.OnNext(error)` を呼び、さらに `RegistryLocator.s_suppressedErrors.Add(key)` が `true` の場合のみ `Debug.LogError()` を出力する
    - EditMode テストを先に書き、「Publish で Errors ストリームにイベントが流れること」「同一 (SlotId, Category) の 2 回目以降は Debug.LogError が抑制されること」「ResetForTest 後に抑制がリセットされること」を検証してから実装する
    - _Requirements: 12.1, 12.4, 12.5, 12.6, 12.7_
  - [ ] 6.5 エラーチャネルの EditMode テストを完成させる
    - `Tests/EditMode/Core/SlotErrorChannelTests.cs` を作成する
    - テスト観点: `Publish()` 後に `Errors` ストリームでイベント受信、同一キーの 2 回目発行では `Debug.LogError` カウントが増えないこと (LogAssert 等で検証)、`RegistryLocator.ResetForTest()` 後に抑制 HashSet がクリアされること
    - _Requirements: 12.5, 12.6, 12.7, 14.3_

- [ ] 7. Registry インターフェース群と具象実装の TDD 実装 (_Requirements: 9, 10_)
  - [ ] 7.1 Registry インターフェース群を実装する
    - `Runtime/Core/Registry/` 下に `IProviderRegistry.cs`・`IMoCapSourceRegistry.cs`・`IFacialControllerRegistry.cs`・`ILipSyncSourceRegistry.cs` を作成する
    - 各インターフェースのシグネチャを design.md §3.6 に従い定義する
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_
  - [ ] 7.2 `DefaultProviderRegistry` を TDD で実装する
    - `Runtime/Core/Registry/DefaultProviderRegistry.cs` を作成する (`internal sealed`)
    - `Dictionary<string, IAvatarProviderFactory>` で内部管理する
    - `Register()` は同一 typeId が存在する場合 `RegistryConflictException` をスローする
    - `Resolve()` は未登録 typeId の場合 `KeyNotFoundException` をスローし Factory の `Create()` を呼ぶ
    - `GetRegisteredTypeIds()` は `IReadOnlyList<string>` を返す
    - EditMode テストを先に書き、「Register/Resolve 成功」「同一 typeId 競合で RegistryConflictException」「未登録 typeId Resolve で例外」「GetRegisteredTypeIds 結果確認」を検証してから実装する
    - _Requirements: 9.1, 9.2, 9.3, 9.9_
  - [ ] 7.3 `DefaultMoCapSourceRegistry` を TDD で実装する
    - `Runtime/Core/Registry/DefaultMoCapSourceRegistry.cs` を作成する (`internal sealed`)
    - `Dictionary<MoCapSourceDescriptor, (IMoCapSource source, int refCount)>` で参照カウント管理する
    - `Resolve()` は同一 Descriptor に対して同一インスタンスを返し参照カウントをインクリメントする
    - `Release()` は参照カウントをデクリメントし 0 になった時点で `source.Dispose()` を呼ぶ
    - EditMode テストを先に書き、「同一 Descriptor → 同一インスタンス返却」「refCount の増減」「refCount=0 で Dispose 呼び出し確認」「RegistryConflictException 確認」を検証してから実装する
    - _Requirements: 9.4, 9.5, 9.6, 9.9, 10.1, 10.3_
  - [ ] 7.4 `DefaultFacialControllerRegistry` / `DefaultLipSyncSourceRegistry` を実装する
    - `Runtime/Core/Registry/` 下に各ファイルを作成する (`internal sealed`)
    - `DefaultProviderRegistry` と同構造 (Dictionary + RegistryConflictException) で実装する
    - EditMode テストで Register/Resolve 基本動作を確認してから実装する
    - _Requirements: 9.1, 9.9_
  - [ ] 7.5 Registry 群の EditMode テストを完成させる
    - `Tests/EditMode/Core/ProviderRegistryTests.cs` と `Tests/EditMode/Core/MoCapSourceRegistryTests.cs` を作成する
    - `ProviderRegistryTests`: Register 成功・競合例外・Resolve 成功・未登録例外・GetRegisteredTypeIds
    - `MoCapSourceRegistryTests`: 参照共有 (同一 Descriptor → 同一インスタンス)・Release で refCount デクリメント・refCount=0 で Dispose・RegistryConflictException
    - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を必ず呼ぶ
    - _Requirements: 9.9, 10.1, 10.2, 10.3, 14.3_

- [ ] 8. `RegistryLocator` の TDD 実装 (_Requirements: 11_)
  - [ ] 8.1 `RegistryLocator` 静的クラスを実装する
    - `Runtime/Core/Locator/RegistryLocator.cs` を作成する
    - 5 プロパティ (`ProviderRegistry`・`MoCapSourceRegistry`・`FacialControllerRegistry`・`LipSyncSourceRegistry`・`ErrorChannel`) を `Interlocked.CompareExchange` パターンで遅延初期化する
    - `ResetForTest()` に `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` を付与し、全フィールドを null に、`s_suppressedErrors` を `Clear()` する
    - `Override*()` メソッド 5 本を実装する
    - `internal static HashSet<(string SlotId, SlotErrorCategory Category)> s_suppressedErrors` を定義する
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 9.10_
  - [ ] 8.2 `RegistryLocator` の EditMode テストを TDD で実装する
    - `Tests/EditMode/Core/RegistryLocatorTests.cs` を作成する
    - テスト観点: 最初のアクセスでインスタンスが生成されること、`ResetForTest()` 後に新インスタンスが生成されること、`Override*()` でモック差し替えが反映されること、`[SetUp]` / `[TearDown]` で ResetForTest を呼ぶこと
    - _Requirements: 11.3, 11.4, 11.5, 14.3_
  - [x] 8.3 `InternalsVisibleTo` 設定を追加する (validation-design.md [N-3] 対応)
    - `Runtime/Core/RealtimeAvatarController.Core.asmdef` の `overrideReferences` または `AssemblyInfo.cs` に `[assembly: InternalsVisibleTo("RealtimeAvatarController.Core.Tests.EditMode")]` を追加する
    - `SlotRegistry`・`DefaultProviderRegistry`・`DefaultMoCapSourceRegistry`・`DefaultFacialControllerRegistry`・`DefaultLipSyncSourceRegistry`・`DefaultSlotErrorChannel` など `internal` 型がテストから参照できることを確認する
    - _Requirements: 14.1_

- [ ] 9. `SlotSettings` の TDD 実装 (_Requirements: 1, 8_)
  - [ ] 9.1 `SlotSettings` クラスを TDD で実装する
    - `Runtime/Core/Slot/SlotSettings.cs` を作成する
    - `[Serializable] public class SlotSettings : ScriptableObject` として定義する
    - フィールド: `slotId`・`displayName`・`weight ([Range(0f,1f)] = 1.0f)`・`avatarProviderDescriptor`・`moCapSourceDescriptor`・`facialControllerDescriptor (null 許容)`・`lipSyncSourceDescriptor (null 許容)`・`fallbackBehavior (= HoldLastPose)` を実装する
    - `Validate()` メソッドを実装する (slotId・displayName・avatarProviderDescriptor.ProviderTypeId・moCapSourceDescriptor.SourceTypeId の必須チェック)
    - weight クランプは `SlotManager` 側で行う設計であることを XML コメントに明記する
    - EditMode テストを先に書き、「Validate 成功」「各必須フィールド欠落で InvalidOperationException」「CreateInstance での動的生成とフィールドセット」を確認してから実装する
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.6, 1.7, 1.8, 1.9_
  - [ ] 9.2 `SlotSettings` の EditMode テストを完成させる
    - `Tests/EditMode/Core/SlotSettingsTests.cs` を作成する
    - テスト観点: `Validate()` 成功、`slotId` 欠落→例外、`displayName` 欠落→例外、`avatarProviderDescriptor` null→例外、`moCapSourceDescriptor` null→例外、`ScriptableObject.CreateInstance<SlotSettings>()` でインスタンス生成後フィールドに値を直接セットして `Validate()` 成功
    - _Requirements: 1.1, 1.6, 1.7, 1.8, 14.3_

- [ ] 10. `SlotState` / `SlotHandle` / `SlotStateChangedEvent` の実装 (_Requirements: 3.3, 3.4_)
  - [x] 10.1 `SlotState` enum を実装する
    - `Runtime/Core/Slot/SlotState.cs` を作成する
    - `Created`・`Active`・`Inactive`・`Disposed` の 4 値を定義する
    - `Inactive` の XML コメントに「将来機能。API 未定義 (Active ⇄ Inactive 遷移 API は設計予約済み)」と明記する (validation-design.md [N-2] 対応)
    - _Requirements: 3.3_
  - [ ] 10.2 `SlotHandle` クラスを実装する
    - `Runtime/Core/Slot/SlotHandle.cs` を作成する
    - `sealed class SlotHandle` として `SlotId (string)`・`DisplayName (string)`・`State (SlotState)`・`Settings (SlotSettings)` の読み取り専用プロパティを定義する
    - _Requirements: 3.3_
  - [x] 10.3 `SlotStateChangedEvent` クラスを実装する
    - `Runtime/Core/Slot/SlotStateChangedEvent.cs` を作成する
    - `sealed class SlotStateChangedEvent` として `SlotId (string)`・`PreviousState (SlotState)`・`NewState (SlotState)` の読み取り専用プロパティを定義する
    - _Requirements: 3.4_

- [ ] 11. `SlotRegistry` の TDD 実装 (_Requirements: 2, 3, validation-design.md [N-3]_)
  - [ ] 11.1 `SlotRegistry` を `internal sealed class` として TDD で実装する (validation-design.md [N-3] 対応)
    - `Runtime/Core/Slot/SlotRegistry.cs` を作成する
    - `internal sealed class SlotRegistry` として定義し、`public` では公開しない
    - `Dictionary<string, SlotHandle>` で Slot を管理する
    - `AddSlot(string slotId, SlotSettings settings)` は重複 slotId で `InvalidOperationException` をスローする
    - `RemoveSlot(string slotId)` は存在しない slotId で `InvalidOperationException` をスローする
    - `GetSlot(string slotId)`・`GetAllSlots()` を実装する
    - EditMode テストを先に書き、「追加」「重複追加エラー」「削除」「未登録削除エラー」「GetSlot 成功/失敗」「GetAllSlots」を確認してから実装する
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [ ] 12. `SlotManager` の TDD 実装 (_Requirements: 2, 3, 13_)
  - [ ] 12.1 Mock 類の準備
    - `Tests/EditMode/Core/Mocks/MockAvatarProvider.cs` を作成する (`IAvatarProvider` スタブ)
    - `Tests/EditMode/Core/Mocks/MockMoCapSource.cs` を作成する (`IMoCapSource` スタブ: `Subject<MotionFrame>` で MotionStream を実装)
    - `Tests/EditMode/Core/Mocks/MockAvatarProviderFactory.cs` を作成する (`IAvatarProviderFactory` スタブ)
    - `Tests/EditMode/Core/Mocks/MockMoCapSourceFactory.cs` を作成する (`IMoCapSourceFactory` スタブ)
    - 各 Mock の Dispose / Shutdown が呼ばれた回数をカウントするプロパティを設ける
    - _Requirements: 14.1, 14.3_
  - [ ] 12.2 `SlotManager` のコア実装を TDD で実装する
    - `Runtime/Core/Slot/SlotManager.cs` を作成する
    - `sealed class SlotManager : IDisposable` として実装する
    - コンストラクタは `IProviderRegistry`・`IMoCapSourceRegistry`・`ISlotErrorChannel` を引数に取る (DI / テスタビリティ確保)
    - `OnSlotStateChanged` を `Subject<SlotStateChangedEvent>` で実装する
    - EditMode テストを先に書き、「AddSlotAsync 成功→Active 遷移」「重複 slotId で InvalidOperationException」「RemoveSlotAsync 成功→Disposed 遷移」「GetSlots/GetSlot」「OnSlotStateChanged 通知」を確認してから実装する
    - _Requirements: 2.6, 2.7, 2.8, 3.1, 3.2, 3.4_
  - [ ] 12.3 Weight クランプを実装する
    - `AddSlotAsync` 内で `settings.weight = Mathf.Clamp01(settings.weight)` を実行する
    - EditMode テストで weight=1.5f がクランプされて 1.0f になることを確認する
    - _Requirements: 1.5_
  - [ ] 12.4 Slot 初期化失敗時の `Created → Disposed` 遷移を TDD で実装する
    - `AddSlotAsync` 内で `IProviderRegistry.Resolve()` / `IAvatarProvider.RequestAvatarAsync()` / `IMoCapSourceRegistry.Resolve()` の例外を catch する
    - 例外キャッチ時に Slot を `Disposed` 状態に遷移させ `ISlotErrorChannel.Publish(SlotError(slotId, InitFailure, ex, DateTime.UtcNow))` を呼ぶ
    - `UniTask` を正常完了させる (例外を飲み込む)
    - EditMode テストで MockProviderFactory が例外をスローする際に ErrorChannel にイベントが流れること・Slot が Disposed になることを確認する
    - _Requirements: 3.7, 3.8, 12.4_
  - [ ] 12.5 `RemoveSlotAsync` の リソース解放を TDD で実装する
    - `IAvatarProvider.ReleaseAvatar(avatar)` → `IAvatarProvider.Dispose()` → `IMoCapSourceRegistry.Release(moCapSource)` の順で実行する
    - Slot 側から `IMoCapSource.Dispose()` を直接呼び出してはならない
    - 破棄中に例外が発生した場合は catch してログに記録し、残余リソースの解放を継続する
    - EditMode テストで Mock の Dispose/Release が正しく呼ばれること、例外発生時でも後続リソースが解放されることを確認する
    - _Requirements: 3.2, 3.5, 3.6, 10.2_
  - [ ] 12.6 `ApplyFailure` とフォールバック処理のスケルトンを TDD で実装する
    - `SlotManager` に `ApplyWithFallback(string slotId, Action applyAction)` 内部メソッドを追加する
    - `applyAction` が例外をスローした場合、`settings.fallbackBehavior` に従いフォールバック処理を実行する
    - `HoldLastPose`: 何もしない (直前ポーズを維持)
    - `TPose`: `SlotHandle.AvatarGameObject` に対して Humanoid をリセット (具体 API は motion-pipeline 合意後に実装)
    - `Hide`: アバターに紐付く全 `Renderer.enabled = false` にする (`GameObject.SetActive(false)` は使用しない — validation-design.md 引き継ぎ事項 #3)
    - フォールバック後に `ISlotErrorChannel.Publish(SlotError(slotId, ApplyFailure, ex, UtcNow))` を呼ぶ
    - EditMode テストで ApplyFailure イベントが ErrorChannel に流れることを確認する
    - _Requirements: 13.3, 13.4, 12.4_
  - [ ] 12.7 `SlotManager.Dispose()` を TDD で実装する
    - 全 Slot に対して `RemoveSlotAsync` 相当の解放処理を実行する
    - `OnSlotStateChanged` Subject を Complete する
    - EditMode テストで Dispose 後に全 Mock の Dispose/Release が呼ばれることを確認する
    - _Requirements: 3.2, 3.5_
  - [ ] 12.8 `SlotManager` の EditMode テストを完成させる
    - `Tests/EditMode/Core/SlotManagerTests.cs` を作成する
    - テスト観点: AddSlotAsync 成功 (Created→Active 遷移)、同一 slotId 重複→InvalidOperationException、AddSlotAsync 初期化失敗→InitFailure ErrorChannel 発行 + Disposed 遷移、RemoveSlotAsync 成功 (Active→Disposed 遷移)、未登録 slotId RemoveSlotAsync→InvalidOperationException、weight クランプ、OnSlotStateChanged 通知内容確認、Dispose で全 Slot 解放
    - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼ぶ
    - `AddSlotAsync` / `RemoveSlotAsync` は `UniTask.ToTask()` + `async Task` テストメソッドで呼び出す (validation-design.md 引き継ぎ事項 #7)
    - _Requirements: 2.1, 2.2, 2.3, 2.6, 3.3, 3.4, 3.7, 3.8, 14.1, 14.3_

- [ ] 13. 属性ベース自動登録パターンの文書化と検証 (_Requirements: 9.8_)
  - [ ] 13.1 自動登録パターンのサンプルコードと XML コメントを整備する
    - `Runtime/Core/Locator/RegistryLocator.cs` の XML コメントに自動登録パターン (design.md §3.15 / §4.5) のコードスニペットを追記する
    - `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `[UnityEditor.InitializeOnLoadMethod]` パターンを示す
    - `RegistryConflictException` の try-catch による ErrorChannel 発行パターンも明記する (validation-design.md 引き継ぎ事項 #2)
    - _Requirements: 9.8_
  - [ ] 13.2 `RegistryLocator.ResetForTest()` の `SubsystemRegistration` 自動実行を EditMode テストで検証する
    - 起動タイミングのシミュレートは EditMode テスト上では `RegistryLocator.ResetForTest()` を直接呼び出すことで代替する
    - テスト観点: ResetForTest 後に ProviderRegistry が新インスタンスになること、s_suppressedErrors が Clear されること
    - _Requirements: 11.3, 9.8_

- [ ] 14. PlayMode 統合テストの実装 (_Requirements: 14.2_)
  - [ ] 14.1 `SlotLifecyclePlayModeTests` を実装する
    - `Tests/PlayMode/Core/SlotLifecyclePlayModeTests.cs` を作成する
    - Unity エンジン起動を伴う `[UnityTest]` として実装する
    - テスト観点: `SlotManager` を介した Slot 動的追加 (Created→Active 遷移)・削除 (Active→Disposed 遷移) の一連ライフサイクル
    - Mock Provider として `ScriptableObject.CreateInstance` で生成した SlotSettings を使用し、シナリオ Y (ランタイム動的生成) を PlayMode で検証する
    - _Requirements: 1.8, 2.8, 14.2_
  - [ ] 14.2 `MoCapSourceSharingPlayModeTests` を実装する
    - `Tests/PlayMode/Core/MoCapSourceSharingPlayModeTests.cs` を作成する
    - テスト観点: 同一 `MoCapSourceDescriptor` で複数 Slot に同一インスタンスが返ること、最後の Slot 削除で MockMoCapSource.Dispose が 1 回だけ呼ばれること
    - _Requirements: 10.1, 10.3, 14.2_

- [ ] 15. Open Issue 対応タスク (validation-design.md フォローアップ)
  - [ ] 15.1 `Inactive` 状態への遷移 API を将来予約コメントとして記録する (validation-design.md [N-2] 対応)
    - `SlotState.cs` の `Inactive` 値 XML コメントに「`InactivateSlotAsync` / `ReactivateSlotAsync` API を将来追加予定」と明記する
    - `SlotManager.cs` の `TODO` コメントとして「Inactive ⇄ Active 遷移 API は未実装 (設計予約)」を追記する
    - _Requirements: 3.3_
  - [ ] 15.2 `SlotRegistry` の `internal` スコープを徹底する (validation-design.md [N-3] 対応)
    - `SlotRegistry` クラスが `internal sealed` であることをコードレビューとテストで確認する
    - テストから `SlotRegistry` の内部メソッドを直接テストする場合は `InternalsVisibleTo` 経由で行う (タスク 8.3 と連動)
    - _Requirements: 2.1, 2.2_
  - [ ] 15.3 `VmcReceive` エラーはワーカースレッドから直接 `Publish()` 可能な旨を引き継ぎ文書に記録する (validation-design.md [N-4] 対応)
    - `ISlotErrorChannel.cs` の XML コメントに「`Subject.Synchronize()` によりワーカースレッドから直接 Publish() しても安全」と明記する
    - mocap-vmc Spec への引き継ぎ事項として本タスク完了時にコメントで残す
    - _Requirements: 12.1_
  - [ ] 15.4 `contracts.md` §1.7 (validation-design.md で言及された最終形反映) を確認する
    - `_shared/contracts.md` の §1〜§5 章の記述が design.md §11 の公開 API 一覧と整合していることを確認する
    - 差分がある場合は contracts.md を design.md §11 に合わせて更新する (requirements.md / design.md は修正しない)
    - _Requirements: 全般_
  - [ ] 15.5 `Hide` フォールバックの `Renderer.enabled` 実装方針を実装コードとコメントで確定する (validation-design.md §11.2 対応)
    - `SlotManager.cs` の `Hide` フォールバック処理コードに「`Renderer.enabled = false` を使用し `GameObject.SetActive(false)` は使用しない。次フレーム正常 Apply 時に `Renderer.enabled = true` に復元する」旨をコメントとして明記する
    - `FallbackBehavior.cs` の `Hide` enum 値の XML コメントも同内容で更新する
    - _Requirements: 13.3_

---

## 実装順序ガイド

依存関係に基づく推奨実装順序:

```
1 (asmdef) → 2 (Config 基底) → 3 (Descriptor) → 4 (抽象 IF) → 5 (FallbackBehavior / ConflictException)
→ 6 (ErrorChannel) → 7 (Registry) → 8 (RegistryLocator) → 9 (SlotSettings)
→ 10 (SlotState/Handle/Event) → 11 (SlotRegistry) → 12 (SlotManager) → 13 (自動登録パターン)
→ 14 (PlayMode テスト) → 15 (Open Issue 対応)
```

## テストファイル一覧

| テストファイル | 種別 | 対応タスク |
|-------------|------|---------|
| `ConfigBaseTests.cs` | EditMode | 2.4 |
| `DescriptorTests.cs` | EditMode | 3.4 |
| `SlotErrorChannelTests.cs` | EditMode | 6.5 |
| `ProviderRegistryTests.cs` | EditMode | 7.5 |
| `MoCapSourceRegistryTests.cs` | EditMode | 7.5 |
| `RegistryLocatorTests.cs` | EditMode | 8.2 |
| `SlotSettingsTests.cs` | EditMode | 9.2 |
| `SlotManagerTests.cs` | EditMode | 12.8 |
| `SlotLifecyclePlayModeTests.cs` | PlayMode | 14.1 |
| `MoCapSourceSharingPlayModeTests.cs` | PlayMode | 14.2 |

---

## 他 Spec への引き継ぎ事項

| 引き継ぎ先 | 内容 |
|----------|------|
| `mocap-vmc` | `ISlotErrorChannel.Publish()` は `Subject.Synchronize()` によりワーカースレッドから直接呼び出しても安全。`VmcReceive` エラーをメインスレッドに移行後に発行する必要はない (validation-design.md [N-4]) |
| `mocap-vmc` | `IMoCapSource.MotionStream` は `Publish().RefCount()` でマルチキャスト化すること (design.md §11.2) |
| `mocap-vmc` | Factory の `RegisterRuntime()` / `RegisterEditor()` で `RegistryConflictException` を try-catch して ErrorChannel に発行するパターンを必ず踏襲すること (design.md §4.6) |
| `avatar-provider-builtin` | `IAvatarProvider.RequestAvatar(config)` / `RequestAvatarAsync(config, ct)` の `config` は Descriptor から渡す方式を採用済み (変更要望は合意すること) |
| `motion-pipeline` | `MotionFrame` 型の具体フィールド定義 (contracts.md §2.2) を早期に確定し、`IMoCapSource` の `IObservable<MotionFrame>` 型プレースホルダーを差し替えること |
| `motion-pipeline` | `FallbackBehavior.TPose` の Humanoid リセット具体 API を motion-pipeline 側で確定し、SlotManager タスク 12.6 の実装を完成させること |
| `motion-pipeline` | `ApplyFailure` 解消時の自動回復挙動 (Req 13.5) は motion-pipeline との合意後に slot-core Tasks に追加タスクを挿入すること |
