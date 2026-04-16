# タスクリスト — avatar-provider-builtin

> **フェーズ**: tasks
> **言語**: ja
> **対応 Spec**: `avatar-provider-builtin`
> **参照**: `requirements.md`、`design.md`、`contracts.md`、`slot-core/design.md`

---

## タスク概要

| 大項目 | 内容 |
|--------|------|
| T-1 asmdef 配置 | ランタイム / テスト用アセンブリ定義ファイルを作成する |
| T-2 BuiltinAvatarProviderConfig | `ProviderConfigBase` 継承の具象 Config ScriptableObject を実装する |
| T-3 BuiltinAvatarProvider | `IAvatarProvider` の具象実装を実装する (同期・非同期 API、ライフサイクル管理) |
| T-4 BuiltinAvatarProviderFactory | `IAvatarProviderFactory` の具象実装・キャストロジック・エラーチャネル連携を実装する |
| T-5 Factory 自己登録 | `[RuntimeInitializeOnLoadMethod]` / `[InitializeOnLoadMethod]` による属性ベース自己登録を実装する |
| T-6 EditMode テスト | Factory キャスト、Resolve、ライフサイクル、自己登録、null Prefab の EditMode テストを実装する |
| T-7 PlayMode テスト | Prefab インスタンス化 (シナリオ X/Y)、ライフサイクル、Disposed 遷移の PlayMode テストを実装する |

---

## T-1: asmdef 配置

### T-1-1: ランタイム asmdef を作成する

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Runtime/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.asmdef`

- `name` を `RealtimeAvatarController.Avatar.Builtin` に設定する
- `references` に `RealtimeAvatarController.Core` のみを設定する (`RealtimeAvatarController.Motion` 等の他の機能アセンブリは含めない)
- `autoReferenced` を `false` に設定する

_Requirements: Req 7 AC 1, Req 7 AC 2_

---

### T-1-2: Editor asmdef を作成する

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Editor/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.Editor.asmdef`

- `name` を `RealtimeAvatarController.Avatar.Builtin.Editor` に設定する
- `references` に `RealtimeAvatarController.Core` と `RealtimeAvatarController.Avatar.Builtin` を設定する
- `includePlatforms` を `["Editor"]` に設定する (Editor only フラグ: true)
- `autoReferenced` を `false` に設定する

_Requirements: Req 7 AC 5_

---

### T-1-3: EditMode テスト asmdef を作成する

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Tests/EditMode/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.Tests.EditMode.asmdef`

- `name` を `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` に設定する
- `references` に `RealtimeAvatarController.Core`、`RealtimeAvatarController.Avatar.Builtin`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner` を設定する
- `includePlatforms` を `["Editor"]` に設定する
- `optionalUnityReferences` に `"TestAssemblies"` を設定する

_Requirements: Req 9 AC 1, Req 9 AC 5_

---

### T-1-4: PlayMode テスト asmdef を作成する

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Tests/PlayMode/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode.asmdef`

- `name` を `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` に設定する
- `references` に `RealtimeAvatarController.Core`、`RealtimeAvatarController.Avatar.Builtin`、`UnityEngine.TestRunner` を設定する
- `optionalUnityReferences` に `"TestAssemblies"` を設定する

_Requirements: Req 9 AC 1, Req 9 AC 5_

---

## T-2: BuiltinAvatarProviderConfig

### T-2-1: BuiltinAvatarProviderConfig クラスを実装する

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Runtime/Avatar/Builtin/BuiltinAvatarProviderConfig.cs`

以下の仕様を満たす `BuiltinAvatarProviderConfig` クラスを作成する。

- 名前空間: `RealtimeAvatarController.Avatar.Builtin`
- `ProviderConfigBase` (ScriptableObject 派生) を継承した `sealed class` として定義する
- `[CreateAssetMenu(menuName = "RealtimeAvatarController/BuiltinAvatarProviderConfig", fileName = "BuiltinAvatarProviderConfig")]` 属性を付与する
- `public GameObject avatarPrefab;` フィールドを定義する
  - Inspector からのドラッグ&ドロップによる参照設定を可能にする
  - ランタイムコード (`config.avatarPrefab = prefab;`) からの直接代入も可能にする

**シナリオ X / Y 対応確認**:
- `[CreateAssetMenu]` 属性によりエディタ上でアセット作成 (シナリオ X) できること
- `ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` でランタイム動的生成 (シナリオ Y) できること
- どちらの生成経路も `BuiltinAvatarProviderFactory.Create()` に渡せること

_Requirements: Req 2 AC 1, Req 2 AC 2, Req 2 AC 3, Req 2 AC 4_

---

## T-3: BuiltinAvatarProvider

### T-3-1: BuiltinAvatarProvider の基本構造を実装する (TDD: EditMode テスト先行)

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Runtime/Avatar/Builtin/BuiltinAvatarProvider.cs`

以下の構造を実装する。

- 名前空間: `RealtimeAvatarController.Avatar.Builtin`
- `IAvatarProvider` インターフェース (contracts.md §3 / slot-core/design.md §3.2) を実装した `sealed class` として定義する
- `string ProviderType { get; }` プロパティで `"Builtin"` を返す
- コンストラクタ: `BuiltinAvatarProvider(BuiltinAvatarProviderConfig config, ISlotErrorChannel errorChannel)`
  - `config` (null 禁止): コンストラクタ引数で受け取った Config を `_config` フィールドに格納する
  - `errorChannel`: null の場合は `RegistryLocator.ErrorChannel` にフォールバックする (`_errorChannel = errorChannel ?? RegistryLocator.ErrorChannel` の形式で実装する)
- 内部フィールド:
  - `_config`: `BuiltinAvatarProviderConfig` — Prefab 参照を保持する Config
  - `_errorChannel`: `ISlotErrorChannel` — エラー発行チャネル (null 条件演算子で null 安全に呼び出す)
  - `_managedAvatars`: `HashSet<GameObject>` — 供給済みアバターの追跡セット
  - `_disposed`: `bool` — Dispose 済みフラグ
- `ThrowIfDisposed()` プライベートヘルパーを実装する (`_disposed == true` の場合に `ObjectDisposedException` をスローする)

_Requirements: Req 1 AC 1, Req 1 AC 2, Req 1 AC 3_

---

### T-3-2: RequestAvatar (同期 API) を実装する (TDD: EditMode テスト先行)

**ファイル**: `BuiltinAvatarProvider.cs` に追記

`GameObject RequestAvatar(ProviderConfigBase config)` を以下の仕様で実装する。

1. `ThrowIfDisposed()` を呼び出して Dispose 済みの場合に `ObjectDisposedException` をスローする
2. Config 解決 (インラインロジック):
   - `var builtinConfig = (config as BuiltinAvatarProviderConfig) ?? _config;` でキャストと _config フォールバックをインラインで処理する
   - `builtinConfig == null` の場合: `InvalidOperationException` を生成し、`_errorChannel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出してから例外をスローする
3. try ブロック内処理:
   - `if (builtinConfig.avatarPrefab == null)` の場合に `InvalidOperationException("BuiltinAvatarProviderConfig.avatarPrefab が null です。...")` をスローする (null Prefab ガードは try ブロック内に配置する)
   - `var instance = Object.Instantiate(builtinConfig.avatarPrefab);` でインスタンス化する
   - `_managedAvatars.Add(instance);` で追跡セットに登録する
   - `instance` を返す
4. catch ブロック処理:
   - `_errorChannel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出す
   - `throw;` で例外を再スローする (SlotManager に伝播させる)

_Requirements: Req 2 AC 5, Req 3 AC 1, Req 3 AC 2, Req 3 AC 3, Req 3 AC 4_

---

### T-3-3: RequestAvatarAsync (非同期 API) を実装する

**ファイル**: `BuiltinAvatarProvider.cs` に追記

`UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)` を以下の仕様で実装する。

- `cancellationToken.ThrowIfCancellationRequested();` でキャンセル確認を行う
- `var avatar = RequestAvatar(config);` で同期版を呼び出す
- `return UniTask.FromResult(avatar);` で即時完了 UniTask にラップして返す

ビルトイン Provider は同期 Prefab Instantiate のみを行うため、非同期 API は UniTask.FromResult でラップする。これにより将来の Addressable Provider と共通の `IAvatarProvider` インターフェース契約を満たす。

_Requirements: Req 6 AC 1, Req 6 AC 2_

---

### T-3-4: ReleaseAvatar を実装する

**ファイル**: `BuiltinAvatarProvider.cs` に追記

`void ReleaseAvatar(GameObject avatar)` を以下の仕様で実装する。

- `!_managedAvatars.Contains(avatar)` の場合: `Debug.LogError("[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。")` を出力し、早期リターンする
- `_managedAvatars.Remove(avatar)` で追跡セットから除去する
- `Object.Destroy(avatar)` で GameObject を破棄する

_Requirements: Req 4 AC 1, Req 4 AC 2, Req 4 AC 4_

---

### T-3-5: Dispose を実装する

**ファイル**: `BuiltinAvatarProvider.cs` に追記

`void Dispose()` を以下の仕様で実装する。

- `if (_disposed) return;` でべき等性を保証する
- `_disposed = true;` にセットする
- `foreach (var avatar in _managedAvatars.ToArray())` でコレクションの変更中の安全性を確保しながらイテレートする
- 各アバターに対して `if (avatar != null) Object.Destroy(avatar);` を呼び出す
- `_managedAvatars.Clear();` で追跡セットをクリアする

_Requirements: Req 4 AC 3, Req 4 AC 5_

---

## T-4: BuiltinAvatarProviderFactory

### T-4-1: BuiltinAvatarProviderFactory の基本構造を実装する (TDD: EditMode テスト先行)

**ファイル**: `Packages/com.yourcompany.realtimeavatarcontroller/Runtime/Avatar/Builtin/BuiltinAvatarProviderFactory.cs`

以下の構造を実装する。

- 名前空間: `RealtimeAvatarController.Avatar.Builtin`
- `IAvatarProviderFactory` インターフェース (contracts.md §1.4 / slot-core/design.md §3.5) を実装した `sealed class` として定義する
- `public const string BuiltinProviderTypeId = "Builtin";` 定数を定義する
- `private readonly ISlotErrorChannel _errorChannel;` フィールドを定義する
- コンストラクタ: `public BuiltinAvatarProviderFactory(ISlotErrorChannel errorChannel = null)` — `errorChannel` を `_errorChannel` に格納する
- ステートレス設計: `_errorChannel` は読み取り専用参照であり、複数回の `Create()` 呼び出しが互いに干渉しない

_Requirements: Req 8 AC 4, Req 8 AC 5_

---

### T-4-2: Factory.Create() キャストロジック・エラーチャネル連携を実装する (TDD: EditMode テスト先行)

**ファイル**: `BuiltinAvatarProviderFactory.cs` に追記

`IAvatarProvider Create(ProviderConfigBase config)` を以下の仕様で実装する。

1. エラーチャネル解決: `var channel = _errorChannel ?? RegistryLocator.ErrorChannel;`
   - 優先度: コンストラクタ引数 `_errorChannel` → `RegistryLocator.ErrorChannel` → null (エラーログ省略)
   - **null 安全性 (validation-design.md Minor #1 対応)**: `channel?.Publish(...)` の形式で null 条件演算子を使用する
2. キャストロジック: `var builtinConfig = config as BuiltinAvatarProviderConfig;`
3. キャスト失敗時 (`builtinConfig == null`):
   - `var ex = new ArgumentException($"Expected BuiltinAvatarProviderConfig, got {config?.GetType().Name ?? "null"}", nameof(config));` を生成する
   - `channel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));` を呼び出す
   - `throw ex;` で例外をスローする
4. キャスト成功時: `return new BuiltinAvatarProvider(builtinConfig, channel);` を返す

_Requirements: Req 8 AC 1, Req 8 AC 2, Req 8 AC 3, Req 8 AC 6_

---

## T-5: Factory 自己登録

### T-5-1: ランタイム自己登録メソッドを実装する

**ファイル**: `BuiltinAvatarProviderFactory.cs` に追記

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 属性付きの `private static void RegisterRuntime()` メソッドを実装する。

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void RegisterRuntime()
{
    try
    {
        RegistryLocator.ProviderRegistry.Register(
            BuiltinProviderTypeId,
            new BuiltinAvatarProviderFactory());
    }
    catch (RegistryConflictException ex)
    {
        RegistryLocator.ErrorChannel.Publish(
            new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
    }
}
```

- Player ビルド・Editor Play Mode の両環境でシーンロード前に実行される
- `RegistryConflictException` を catch し、`RegistryLocator.ErrorChannel.Publish()` で `SlotErrorCategory.RegistryConflict` として通知する (再スローしない)

_Requirements: Req 1 AC 5, Req 1 AC 7_

---

### T-5-2: Editor 自己登録メソッドを実装する

**ファイル**: `BuiltinAvatarProviderFactory.cs` に追記 (`#if UNITY_EDITOR` ガード内)

`#if UNITY_EDITOR` ガード内に `[UnityEditor.InitializeOnLoadMethod]` 属性付きの `private static void RegisterEditor()` メソッドを実装する。

```csharp
#if UNITY_EDITOR
[UnityEditor.InitializeOnLoadMethod]
private static void RegisterEditor()
{
    try
    {
        RegistryLocator.ProviderRegistry.Register(
            BuiltinProviderTypeId,
            new BuiltinAvatarProviderFactory());
    }
    catch (RegistryConflictException ex)
    {
        RegistryLocator.ErrorChannel.Publish(
            new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
    }
}
#endif
```

- Unity Editor 起動時・コンパイル完了後に実行され、Inspector の typeId 候補列挙を可能にする
- Player ビルドに含まれないよう `#if UNITY_EDITOR` ガードで保護する
- `RegistryConflictException` は `RegistryLocator.ErrorChannel.Publish()` で通知し、再スローしない

_Requirements: Req 1 AC 6, Req 1 AC 7, Req 7 AC 5_

---

## T-6: EditMode テスト

> **前提**: 各テストクラスの `[SetUp]` で `RegistryLocator.ResetForTest()` を呼び出してテスト間の Registry 汚染を防ぐ。`ISlotErrorChannel` はモック実装 (`FakeSlotErrorChannel`) を `RegistryLocator.OverrideErrorChannel()` で注入する。

### T-6-1: BuiltinAvatarProviderFactoryTests — キャスト成功テストを実装する

**ファイル**: `Tests/EditMode/Avatar/Builtin/BuiltinAvatarProviderFactoryTests.cs`

テストクラス `BuiltinAvatarProviderFactoryTests` に以下のテストケースを実装する。

- `Create_WithValidConfig_ReturnsBuiltinAvatarProvider`:
  - `BuiltinAvatarProviderConfig` のインスタンス (CreateInstance) を渡した場合に `BuiltinAvatarProvider` インスタンスが返ること (`Assert.IsInstanceOf<BuiltinAvatarProvider>`) を検証する

_Requirements: Req 8 AC 2, Req 9 AC 2_

---

### T-6-2: BuiltinAvatarProviderFactoryTests — キャスト失敗テストを実装する

**ファイル**: `BuiltinAvatarProviderFactoryTests.cs` に追記

- `Create_WithInvalidConfig_ThrowsArgumentException`:
  - `BuiltinAvatarProviderConfig` 以外の Config (モック `ProviderConfigBase` 派生型等) を渡した場合に `ArgumentException` がスローされること (`Assert.Throws<ArgumentException>`) を検証する
- `Create_WithInvalidConfig_PublishesInitFailureToErrorChannel`:
  - キャスト失敗時に `FakeSlotErrorChannel` に `SlotErrorCategory.InitFailure` が発行されること (`Assert.IsTrue(fakeChannel.HasReceived(SlotErrorCategory.InitFailure))` 相当) を検証する
  - **null channel 安全性 (validation-design.md Minor #1 対応)**: `channel == null` のケースでも `ArgumentException` はスローされること (エラー発行はスキップされても例外は必ずスローされる) を検証する

_Requirements: Req 8 AC 3, Req 9 AC 2_

---

### T-6-3: BuiltinAvatarProviderFactoryTests — ステートレス設計テストを実装する

**ファイル**: `BuiltinAvatarProviderFactoryTests.cs` に追記

- `Create_IsStateless_MultipleCalls_DoNotInterfere`:
  - 同一 Factory インスタンスに対して複数回 `Create()` を呼び出した場合に、それぞれ独立した `BuiltinAvatarProvider` インスタンスが返ること (参照が異なること) を検証する

_Requirements: Req 8 AC 5, Req 9 AC 2_

---

### T-6-4: BuiltinAvatarProviderRegistrationTests — 自己登録テストを実装する

**ファイル**: `Tests/EditMode/Avatar/Builtin/BuiltinAvatarProviderRegistrationTests.cs`

テストクラス `BuiltinAvatarProviderRegistrationTests` に以下のテストケースを実装する。

- `RegisterRuntime_RegistersBuiltinTypeId`:
  - `BuiltinAvatarProviderFactory` の `RegisterRuntime()` 相当の登録処理 (Reflection またはテスト用の公開ヘルパー経由) を呼び出した後に `RegistryLocator.ProviderRegistry.GetRegisteredTypeIds()` に `"Builtin"` が含まれることを検証する
- `Register_DuplicateTypeId_ThrowsRegistryConflictException`:
  - 同一 `typeId="Builtin"` の二重登録で `RegistryConflictException` がスローされること、かつ `FakeSlotErrorChannel` に `SlotErrorCategory.RegistryConflict` が発行されることを検証する
- `Resolve_AfterRegistration_ReturnsBuiltinAvatarProvider`:
  - 登録後に `IProviderRegistry.Resolve()` (有効な `AvatarProviderDescriptor` を使用) を呼び出すと `IAvatarProvider` (実態は `BuiltinAvatarProvider`) が返ることを検証する

_Requirements: Req 1 AC 5, Req 1 AC 7, Req 5 AC 2, Req 9 AC 2_

---

### T-6-5: BuiltinAvatarProviderLifecycleEditTests — ライフサイクル EditMode テストを実装する

**ファイル**: `Tests/EditMode/Avatar/Builtin/BuiltinAvatarProviderLifecycleEditTests.cs`

テストクラス `BuiltinAvatarProviderLifecycleEditTests` に以下のテストケースを実装する。

- `ReleaseAvatar_UnmanagedObject_LogsErrorAndDoesNotDestroy`:
  - Provider が供給していない (管理外の) GameObject を `ReleaseAvatar()` に渡した場合にエラーがログ出力されることを検証する (モック環境で Unity ランタイムに依存しない形で確認する)
- `Dispose_MarksProviderAsDisposed`:
  - `Dispose()` 後に `RequestAvatar()` を呼び出した場合に `ObjectDisposedException` がスローされることを検証する (`Assert.Throws<ObjectDisposedException>`)
- `RequestAvatar_AfterDispose_ThrowsObjectDisposedException`:
  - `Dispose()` 後の `RequestAvatar()` 呼び出しが `ObjectDisposedException` をスローし、ErrorChannel への発行は行わないことを検証する (design.md §8 のエラーパターン表に準拠)

_Requirements: Req 4 AC 4, Req 4 AC 5, Req 9 AC 2_

---

### T-6-6: BuiltinAvatarProviderNullPrefabEditTests — null Prefab 時のエラー発行 EditMode テストを実装する

**ファイル**: `Tests/EditMode/Avatar/Builtin/BuiltinAvatarProviderNullPrefabEditTests.cs`

テストクラス `BuiltinAvatarProviderNullPrefabEditTests` に以下のテストケースを実装する。

- `RequestAvatar_NullPrefab_PublishesInitFailureAndThrows`:
  - `avatarPrefab = null` の `BuiltinAvatarProviderConfig` を使用した `BuiltinAvatarProvider` で `RequestAvatar()` を呼び出した場合に:
    1. `InvalidOperationException` がスローされること
    2. `FakeSlotErrorChannel` に `SlotErrorCategory.InitFailure` が発行されること
  - を検証する (EditMode での検証は try ブロック内での null チェックロジックを確認するためのもの; PlayMode での実際のインスタンス化は T-7-3 で検証する)
- **`_errorChannel` null 安全性 (validation-design.md Minor #2 対応)**: `_errorChannel` に null を設定した状態でも `NullReferenceException` が発生しないこと (`_errorChannel?.Publish(...)` の null 条件演算子による保護を確認する) を検証する

_Requirements: Req 2 AC 5, Req 3 AC 4, Req 9 AC 2_

---

## T-7: PlayMode テスト

> **前提**: Prefab 生成には `GameObject.CreatePrimitive(PrimitiveType.Cube)` 等で動的生成した軽量 GameObject を使用する。各テスト後に `[TearDown]` で生成した GameObject を後片付けする。

### T-7-1: BuiltinAvatarProviderInstantiateTests — シナリオ X インスタンス化テストを実装する

**ファイル**: `Tests/PlayMode/Avatar/Builtin/BuiltinAvatarProviderInstantiateTests.cs`

テストクラス `BuiltinAvatarProviderInstantiateTests` に以下のテストケースを実装する。

- `RequestAvatar_ScenarioX_InstantiatesPrefab`:
  - エディタ上で作成した SO アセット経由の `BuiltinAvatarProviderConfig` (シナリオ X) を使用して `RequestAvatar()` を呼び出し、有効な (null でない) `GameObject` が Scene 上に生成されることを検証する
  - 返却された `GameObject` が `Resources.FindObjectsOfTypeAll` 等で Scene に存在することを確認する

_Requirements: Req 3 AC 1, Req 3 AC 2, Req 9 AC 3_

---

### T-7-2: BuiltinAvatarProviderInstantiateTests — シナリオ Y インスタンス化テストを実装する

**ファイル**: `BuiltinAvatarProviderInstantiateTests.cs` に追記

- `RequestAvatar_ScenarioY_InstantiatesPrefab`:
  - `ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` でランタイム動的生成し、`config.avatarPrefab` に `GameObject.CreatePrimitive(PrimitiveType.Cube)` のプレハブ相当のオブジェクトをセットした Config (シナリオ Y) を使用して `RequestAvatar()` を呼び出し、有効な `GameObject` が返ることを検証する
  - シナリオ X と同一の結果が得られることを確認する

_Requirements: Req 2 AC 3, Req 3 AC 1, Req 9 AC 3_

---

### T-7-3: BuiltinAvatarProviderInstantiateTests — null Prefab 時の PlayMode テストを実装する

**ファイル**: `BuiltinAvatarProviderInstantiateTests.cs` に追記

- `RequestAvatar_NullPrefab_ThrowsAndPublishesError`:
  - `avatarPrefab = null` の `BuiltinAvatarProviderConfig` で `RequestAvatar()` を呼び出した場合に例外がスローされ、`InitFailure` が `ISlotErrorChannel` に発行されることを PlayMode 環境で検証する (try ブロック内の null Prefab ガードの統合動作確認)

_Requirements: Req 2 AC 5, Req 3 AC 4, Req 9 AC 3_

---

### T-7-4: BuiltinAvatarProviderLifecycleTests — ReleaseAvatar テストを実装する

**ファイル**: `Tests/PlayMode/Avatar/Builtin/BuiltinAvatarProviderLifecycleTests.cs`

テストクラス `BuiltinAvatarProviderLifecycleTests` に以下のテストケースを実装する。

- `ReleaseAvatar_DestroysGameObject`:
  - `RequestAvatar()` で取得した `GameObject` に対して `ReleaseAvatar()` を呼び出した後に、その GameObject が破棄されていること (`gameObject == null` または `!gameObject` で確認) を検証する

_Requirements: Req 4 AC 1, Req 4 AC 2, Req 9 AC 3_

---

### T-7-5: BuiltinAvatarProviderLifecycleTests — Dispose テストを実装する

**ファイル**: `BuiltinAvatarProviderLifecycleTests.cs` に追記

- `Dispose_DestroysAllManagedAvatars`:
  - 複数の `RequestAvatar()` で取得した `GameObject` が存在する状態で `Dispose()` を呼び出した後に、追跡中の全 `GameObject` が破棄されていることを検証する

_Requirements: Req 4 AC 3, Req 9 AC 3_

---

### T-7-6: BuiltinAvatarProviderLifecycleTests — RequestAvatarAsync テストを実装する

**ファイル**: `BuiltinAvatarProviderLifecycleTests.cs` に追記

- `RequestAvatarAsync_ReturnsInstantiatedPrefab`:
  - `RequestAvatarAsync()` を `await` して即時完了し、インスタンス化された有効な `GameObject` が返ることを検証する
  - `UniTask.FromResult` ラップによる即時完了動作を確認する

_Requirements: Req 6 AC 1, Req 6 AC 2, Req 9 AC 3_

---

### T-7-7: BuiltinAvatarProviderLifecycleTests — 複数 Slot 独立性テストを実装する

**ファイル**: `BuiltinAvatarProviderLifecycleTests.cs` に追記

- `MultipleSlots_ReceiveIndependentInstances`:
  - 2 つの独立した `BuiltinAvatarProvider` インスタンスが各々 `RequestAvatar()` で異なる `GameObject` インスタンスを返すこと (参照が異なること) を検証する
  - 1 Slot 1 インスタンス原則に従い、各 Slot が独立したアバター GameObject を保有することを確認する

_Requirements: Req 4 AC 6, Req 5 AC 4, Req 9 AC 3_

---

### T-7-8: BuiltinAvatarProviderLifecycleTests — Disposed 状態遷移テストを実装する

**ファイル**: `BuiltinAvatarProviderLifecycleTests.cs` に追記

- `AfterDispose_RequestAvatar_ThrowsObjectDisposedException`:
  - `Dispose()` 後に `RequestAvatar()` を呼び出した場合に `ObjectDisposedException` がスローされることを PlayMode 環境で検証する

_Requirements: Req 4 AC 5, Req 9 AC 3_

---

## 完了条件チェックリスト

| # | 項目 | 完了 |
|---|------|:---:|
| T-1-1 | ランタイム asmdef 作成 | □ |
| T-1-2 | Editor asmdef 作成 | □ |
| T-1-3 | EditMode テスト asmdef 作成 | □ |
| T-1-4 | PlayMode テスト asmdef 作成 | □ |
| T-2-1 | BuiltinAvatarProviderConfig 実装 | □ |
| T-3-1 | BuiltinAvatarProvider 基本構造 | □ |
| T-3-2 | RequestAvatar (同期) 実装 | □ |
| T-3-3 | RequestAvatarAsync 実装 | □ |
| T-3-4 | ReleaseAvatar 実装 | □ |
| T-3-5 | Dispose 実装 | □ |
| T-4-1 | BuiltinAvatarProviderFactory 基本構造 | □ |
| T-4-2 | Factory.Create() キャストロジック実装 | □ |
| T-5-1 | ランタイム自己登録メソッド実装 | □ |
| T-5-2 | Editor 自己登録メソッド実装 | □ |
| T-6-1 | EditMode: キャスト成功テスト | □ |
| T-6-2 | EditMode: キャスト失敗・ErrorChannel 発行テスト | □ |
| T-6-3 | EditMode: ステートレス設計テスト | □ |
| T-6-4 | EditMode: 自己登録・Resolve テスト | □ |
| T-6-5 | EditMode: ライフサイクルテスト | □ |
| T-6-6 | EditMode: null Prefab + null channel テスト | □ |
| T-7-1 | PlayMode: シナリオ X インスタンス化テスト | □ |
| T-7-2 | PlayMode: シナリオ Y インスタンス化テスト | □ |
| T-7-3 | PlayMode: null Prefab テスト | □ |
| T-7-4 | PlayMode: ReleaseAvatar テスト | □ |
| T-7-5 | PlayMode: Dispose テスト | □ |
| T-7-6 | PlayMode: RequestAvatarAsync テスト | □ |
| T-7-7 | PlayMode: 複数 Slot 独立性テスト | □ |
| T-7-8 | PlayMode: Disposed 遷移テスト | □ |
