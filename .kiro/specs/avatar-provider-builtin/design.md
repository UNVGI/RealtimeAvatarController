# avatar-provider-builtin 設計ドキュメント

> **フェーズ**: design
> **言語**: ja
> **Wave**: Wave B (並列波) — `slot-core` design.md 第 11 章を起点として設計

---

## 1. 概要

### 責務範囲

`avatar-provider-builtin` は Realtime Avatar Controller において、Unity プロジェクト内に Prefab として配置されたアバターを Slot へ供給する責務を担う。`slot-core` が定義した `IAvatarProvider` 抽象インターフェースの具象実装を提供し、以下のコンポーネントを定義する。

- **`BuiltinAvatarProviderConfig`**: `ProviderConfigBase` を継承した具象 Config 型。`avatarPrefab` フィールドを保持する
- **`BuiltinAvatarProvider`**: `IAvatarProvider` を実装し、Prefab の Instantiate・Destroy・ライフサイクル管理を行う
- **`BuiltinAvatarProviderFactory`**: `IAvatarProviderFactory` を実装し、`IProviderRegistry` に `typeId="Builtin"` で属性ベース自己登録を行う

### slot-core との境界

| 責務 | 担当 |
|------|------|
| `IAvatarProvider` インターフェース定義 | `slot-core` |
| `IAvatarProviderFactory` インターフェース定義 | `slot-core` |
| `IProviderRegistry` インターフェース定義・実装 | `slot-core` |
| `ProviderConfigBase` 抽象基底クラス定義 | `slot-core` |
| `RegistryLocator` 静的アクセスポイント | `slot-core` |
| `ISlotErrorChannel` / `SlotError` / `SlotErrorCategory` | `slot-core` |
| `BuiltinAvatarProviderConfig` 具象 Config 定義 | **本 Spec** |
| `BuiltinAvatarProvider` 具象実装 | **本 Spec** |
| `BuiltinAvatarProviderFactory` 具象実装 + 自己登録 | **本 Spec** |
| Slot の状態遷移管理・例外捕捉 | `slot-core` (`SlotManager`) |

---

## 2. アーキテクチャ

### レイヤー位置付け

```
┌──────────────────────────────────────────────────────────┐
│  slot-core (RealtimeAvatarController.Core)                │
│  IAvatarProvider / IAvatarProviderFactory / IProviderRegistry │
│  ProviderConfigBase / AvatarProviderDescriptor             │
│  RegistryLocator / ISlotErrorChannel                       │
└─────────────────────┬────────────────────────────────────┘
                      │ 依存 (参照)
┌─────────────────────▼────────────────────────────────────┐
│  avatar-provider-builtin                                   │
│  (RealtimeAvatarController.Avatar.Builtin)                 │
│  BuiltinAvatarProviderConfig : ProviderConfigBase          │
│  BuiltinAvatarProvider : IAvatarProvider                   │
│  BuiltinAvatarProviderFactory : IAvatarProviderFactory     │
└──────────────────────────────────────────────────────────┘
```

### 解決フロー

```
SlotManager
  │
  ▼ IProviderRegistry.Resolve(avatarProviderDescriptor)
ProviderRegistry
  │ typeId="Builtin" → BuiltinAvatarProviderFactory
  ▼ IAvatarProviderFactory.Create(config)
BuiltinAvatarProviderFactory
  │ config を BuiltinAvatarProviderConfig にキャスト
  ▼ new BuiltinAvatarProvider(config)
BuiltinAvatarProvider
  │
  ▼ RequestAvatar(config) → Object.Instantiate(prefab)
GameObject (Sceneに配置されたアバターインスタンス)
```

### 1 Slot 1 インスタンス原則

`IAvatarProvider` のライフサイクルは `IMoCapSource` と異なり参照共有を採用しない。各 Slot は独立した `BuiltinAvatarProvider` インスタンスを保有し、Slot の破棄と同時に `SlotManager` がアバター GameObject を破棄する。

---

## 3. 公開 API 仕様 (最終 C# シグネチャ)

```csharp
namespace RealtimeAvatarController.Avatar.Builtin
{
    /// <summary>
    /// ビルトイン Provider 用 Config。avatarPrefab フィールドを保持する。
    /// ScriptableObject アセット編集 (シナリオ X) および
    /// ScriptableObject.CreateInstance によるランタイム動的生成 (シナリオ Y) の両方をサポートする。
    /// </summary>
    [CreateAssetMenu(
        menuName = "RealtimeAvatarController/BuiltinAvatarProviderConfig",
        fileName = "BuiltinAvatarProviderConfig")]
    public sealed class BuiltinAvatarProviderConfig : ProviderConfigBase
    {
        /// <summary>アバターとしてインスタンス化する Prefab 参照。</summary>
        public GameObject avatarPrefab;
    }

    /// <summary>
    /// IAvatarProvider のビルトイン具象実装。
    /// Object.Instantiate / Object.Destroy によるアバターのライフサイクルを管理する。
    /// </summary>
    public sealed class BuiltinAvatarProvider : IAvatarProvider
    {
        public string ProviderType { get; }

        public BuiltinAvatarProvider(BuiltinAvatarProviderConfig config, ISlotErrorChannel errorChannel);

        public GameObject RequestAvatar(ProviderConfigBase config);

        public UniTask<GameObject> RequestAvatarAsync(
            ProviderConfigBase config,
            CancellationToken cancellationToken = default);

        public void ReleaseAvatar(GameObject avatar);

        public void Dispose();
    }

    /// <summary>
    /// IAvatarProviderFactory のビルトイン具象実装。
    /// [RuntimeInitializeOnLoadMethod] および [InitializeOnLoadMethod] で自己登録する。
    /// </summary>
    public sealed class BuiltinAvatarProviderFactory : IAvatarProviderFactory
    {
        public BuiltinAvatarProviderFactory(ISlotErrorChannel errorChannel = null);

        public IAvatarProvider Create(ProviderConfigBase config);
    }
}
```

---

## 4. BuiltinAvatarProviderConfig 詳細

### クラス定義

`BuiltinAvatarProviderConfig` は `slot-core` が定義した `ProviderConfigBase : ScriptableObject` を継承する具象 Config 型である。

```csharp
[CreateAssetMenu(
    menuName = "RealtimeAvatarController/BuiltinAvatarProviderConfig",
    fileName = "BuiltinAvatarProviderConfig")]
public sealed class BuiltinAvatarProviderConfig : ProviderConfigBase
{
    /// <summary>
    /// アバターとしてインスタンス化する Prefab。
    /// Inspector からのドラッグ&ドロップ、またはランタイムコードからの直接代入に対応する。
    /// </summary>
    public GameObject avatarPrefab;
}
```

### フィールド仕様

| フィールド | 型 | アクセス修飾子 | 説明 |
|-----------|---|:---:|------|
| `avatarPrefab` | `GameObject` | `public` | Scene にインスタンス化するアバター Prefab |

`avatarPrefab` を `public` フィールドとして公開することで、以下の両シナリオを同一 API で処理できる。

### シナリオ X — Inspector (SO アセット経由)

```csharp
// Unity エディタでアセットを作成 (CreateAssetMenu から)
// Inspector にて avatarPrefab フィールドへ Prefab をドラッグ&ドロップする
```

### シナリオ Y — ランタイム動的生成

```csharp
var config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
config.avatarPrefab = someAvatarPrefab;  // public フィールドへ直接代入
```

`BuiltinAvatarProviderFactory.Create()` はどちらのシナリオで生成した Config を受け取っても同一のキャスト・インスタンス化ロジックで処理する。

---

## 5. BuiltinAvatarProvider 内部設計

### コンストラクタ

```csharp
public BuiltinAvatarProvider(BuiltinAvatarProviderConfig config, ISlotErrorChannel errorChannel)
```

- `config`: 使用する Prefab 設定。Factory から渡される (`null` 禁止)
- `errorChannel`: エラー発行チャネル。`null` の場合は `RegistryLocator.ErrorChannel` にフォールバック

### 内部状態

| フィールド | 型 | 説明 |
|-----------|---|------|
| `_config` | `BuiltinAvatarProviderConfig` | Prefab 参照を保持する Config |
| `_errorChannel` | `ISlotErrorChannel` | エラー発行チャネル |
| `_managedAvatars` | `HashSet<GameObject>` | 供給済み Avatar の追跡セット |
| `_disposed` | `bool` | Dispose 済みフラグ |

### RequestAvatar (同期 API)

```csharp
public GameObject RequestAvatar(ProviderConfigBase config)
{
    ThrowIfDisposed();

    // --- config 解決方針 ---
    // 引数 config を優先してキャストする。
    // 引数が null の場合はコンストラクタで格納済みのフィールド _config を使用する。
    // どちらも null か BuiltinAvatarProviderConfig にキャストできない場合は
    // InitFailure を発行して InvalidOperationException をスローする。
    var builtinConfig = (config as BuiltinAvatarProviderConfig) ?? _config;
    if (builtinConfig == null)
    {
        var ex = new InvalidOperationException(
            $"config は BuiltinAvatarProviderConfig でなければなりません。実際の型: {config?.GetType().Name ?? "null"}");
        _errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
        throw ex;
    }

    try
    {
        // null Prefab ガード — try ブロック内に配置し、catch で InitFailure を発行できるようにする
        if (builtinConfig.avatarPrefab == null)
        {
            throw new InvalidOperationException(
                "BuiltinAvatarProviderConfig.avatarPrefab が null です。Prefab を設定してください。");
        }

        var instance = Object.Instantiate(builtinConfig.avatarPrefab);
        _managedAvatars.Add(instance);
        return instance;
    }
    catch (Exception ex)
    {
        // null Prefab 例外・Object.Instantiate 例外いずれも InitFailure として発行する。
        // contracts.md §1.7 / slot-core/design.md §3.8 の Publish(SlotError) に従い
        // RegistryLocator.ErrorChannel 経由の _errorChannel に発行する。
        _errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
        throw;  // SlotManager に再スロー
    }
}
```

> **config 引数の設計意図**: `IAvatarProvider` インターフェース契約 (contracts.md §3、slot-core/design.md §3.7) では `RequestAvatar(ProviderConfigBase config)` に config 引数が存在する。`BuiltinAvatarProvider` はコンストラクタ時点で `_config` を保持しているため、引数 config は**省略可能な上書きパス**として機能する。呼び出し元が `null` を渡した場合はコンストラクタ引数の `_config` を使用する。これにより将来的に同一 Provider インスタンスを異なる Config で再利用する拡張余地を確保しつつ、通常の Factory 経由フローでは `_config` を透過的に使用できる。

- `Object.Instantiate(prefab)` でアバターを Scene に配置する
- 生成した `GameObject` を `_managedAvatars` に登録し、`ReleaseAvatar()` / `Dispose()` での追跡に使用する
- 同期 API のため即座に `GameObject` を返す

### RequestAvatarAsync (非同期 API — 即時完了ラップ)

```csharp
public UniTask<GameObject> RequestAvatarAsync(
    ProviderConfigBase config,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    var avatar = RequestAvatar(config);   // 同期版を呼び出す
    return UniTask.FromResult(avatar);    // 即時完了 UniTask にラップして返す
}
```

ビルトイン Provider は同期 Prefab Instantiate のみを行うため、非同期 API は `UniTask.FromResult` で同期版の結果を包んで即時完了する。これにより:

1. `IAvatarProvider` インターフェースの非同期 API 契約を満たす
2. 将来の Addressable Provider が真に非同期な実装を提供する際も、呼び出し側 (`SlotManager`) のコードを変更しない

### ReleaseAvatar

```csharp
public void ReleaseAvatar(GameObject avatar)
{
    if (!_managedAvatars.Contains(avatar))
    {
        Debug.LogError($"[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
        return;
    }
    _managedAvatars.Remove(avatar);
    Object.Destroy(avatar);
}
```

- 本 Provider が供給した `GameObject` のみを破棄する
- 管理外の `GameObject` が渡された場合はエラーをログに記録し、破棄しない

### Dispose

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    foreach (var avatar in _managedAvatars.ToArray())
    {
        if (avatar != null)
            Object.Destroy(avatar);
    }
    _managedAvatars.Clear();
}
```

- 追跡中の全 `GameObject` を破棄し、内部状態をクリアする
- `Dispose()` 後に `RequestAvatar()` が呼ばれた場合は `ObjectDisposedException` をスローする

### 1 Slot 1 インスタンス原則

`IMoCapSource` で採用している参照共有モデル (複数 Slot が同一インスタンスを共有し、`MoCapSourceRegistry` が参照カウントを管理) は採用しない。各 Slot は独立した `BuiltinAvatarProvider` インスタンスと独立したアバター `GameObject` インスタンスを保有する。Slot の破棄は直ちにアバターの `Destroy` につながる。

---

## 6. Factory 実装

### BuiltinAvatarProviderFactory クラス

```csharp
public sealed class BuiltinAvatarProviderFactory : IAvatarProviderFactory
{
    private readonly ISlotErrorChannel _errorChannel;

    /// <summary>
    /// コンストラクタ。
    /// errorChannel が null の場合は RegistryLocator.ErrorChannel を使用する。
    /// </summary>
    public BuiltinAvatarProviderFactory(ISlotErrorChannel errorChannel = null)
    {
        _errorChannel = errorChannel;
    }

    public IAvatarProvider Create(ProviderConfigBase config)
    {
        var channel = _errorChannel ?? RegistryLocator.ErrorChannel;
        var builtinConfig = config as BuiltinAvatarProviderConfig;
        if (builtinConfig == null)
        {
            var ex = new ArgumentException(
                $"Expected BuiltinAvatarProviderConfig, got {config?.GetType().Name ?? "null"}",
                nameof(config));
            channel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
            throw ex;
        }
        return new BuiltinAvatarProvider(builtinConfig, channel);
    }
}
```

### キャストロジック

1. `config as BuiltinAvatarProviderConfig` でキャストを試みる
2. キャスト結果が `null` の場合 → `ArgumentException` を生成し `ISlotErrorChannel` に `InitFailure` で発行後、例外をスローする
3. キャスト成功の場合 → `new BuiltinAvatarProvider(builtinConfig, channel)` を返す

### エラーチャネルの解決順序

| 優先度 | 取得元 |
|:---:|------|
| 1 | コンストラクタ引数 `errorChannel` (非 null の場合) |
| 2 | `RegistryLocator.ErrorChannel` (静的アクセスポイント) |
| 3 | `null` ならエラーログ省略 (channel が null のとき) |

### ステートレス設計

`BuiltinAvatarProviderFactory` はインスタンス固有の可変状態を持たない。`_errorChannel` は読み取り専用の参照であり、複数回の `Create()` 呼び出しは互いに干渉しない。

---

## 7. Factory 自動登録

### エントリコード

```csharp
namespace RealtimeAvatarController.Avatar.Builtin
{
    public sealed class BuiltinAvatarProviderFactory : IAvatarProviderFactory
    {
        // --- 自己登録エントリポイント ---

        /// <summary>
        /// ランタイム起動時 (シーンロード前) に ProviderRegistry へ自己登録する。
        /// Player / Build 両環境で実行される。
        /// </summary>
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
                // 二重登録 (Domain Reload OFF 環境等) を ErrorChannel に通知する。
                // contracts.md §1.7 / slot-core/design.md §8.1 の確定パターンに従い
                // slotId は空文字列、カテゴリは RegistryConflict を使用する。
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Unity Editor 起動時 (コンパイル完了後) に ProviderRegistry へ自己登録する。
        /// Inspector の typeId 候補列挙のために必要。
        /// </summary>
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
                // Editor リロード時の二重登録を ErrorChannel に通知する。
                // contracts.md §1.7 / slot-core/design.md §8.1 の確定パターンに従う。
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }
#endif

        // --- 定数 ---
        /// <summary>本 Factory が登録する typeId。</summary>
        public const string BuiltinProviderTypeId = "Builtin";

        // ... Create() 等 ...
    }
}
```

### 登録タイミング一覧

| 属性 | 実行タイミング | 環境 |
|------|-------------|------|
| `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` | シーンロード前 | Player ビルド・Editor Play Mode |
| `[UnityEditor.InitializeOnLoadMethod]` | コンパイル完了直後 | Editor のみ |

### typeId

`"Builtin"` で固定。`RegistryLocator.ProviderRegistry.GetRegisteredTypeIds()` の返却リストに含まれることで Inspector の候補 UI に表示される。

### Editor コードの配置

`#if UNITY_EDITOR` ガードにより Player ビルドへの混入を防ぐ。Editor asmdef (`RealtimeAvatarController.Avatar.Builtin.Editor`) を別途作成する場合は `RegisterEditor()` メソッドをそちらへ移動してもよい (初期版では `#if UNITY_EDITOR` ガードで対応)。

---

## 8. エラーハンドリング

### InitFailure / RegistryConflict 発生パターン

> **ISlotErrorChannel.Publish() 参照**: 本節のエラー発行はすべて `contracts.md §1.7`（`ISlotErrorChannel` インターフェース骨格・`Publish(SlotError error)` メソッド確定）および `slot-core/design.md §3.8`（`DefaultSlotErrorChannel` 実装）に基づく。`_errorChannel.Publish(new SlotError(...))` の呼び出し形式はこれらの最終確定シグネチャと完全に一致する。

| パターン | 発生箇所 | 処理 |
|---------|---------|------|
| `Create()` に `BuiltinAvatarProviderConfig` 以外の Config が渡された | `BuiltinAvatarProviderFactory.Create()` | `_errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出し後、`ArgumentException` をスロー |
| `RequestAvatar()` の config 引数と `_config` フィールドがともに null またはキャスト不可 | `BuiltinAvatarProvider.RequestAvatar()` | `_errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出し後、`InvalidOperationException` をスロー |
| `avatarPrefab` が null の状態で `RequestAvatar()` が呼ばれた | `BuiltinAvatarProvider.RequestAvatar()` (try ブロック内) | try ブロック内で `InvalidOperationException` をスローし、catch ブロックで `_errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出してから再スロー |
| `Object.Instantiate()` が例外を発生させた | `BuiltinAvatarProvider.RequestAvatar()` (try ブロック内) | catch ブロックで `_errorChannel.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を呼び出し後、例外を再スロー |
| `Dispose()` 後に `RequestAvatar()` が呼ばれた | `BuiltinAvatarProvider.RequestAvatar()` | `ObjectDisposedException` をスロー (ErrorChannel 発行なし) |
| `Register()` 呼び出し時の typeId 重複 (`RegistryConflictException`) | `RegisterRuntime()` / `RegisterEditor()` (catch ブロック) | `RegistryLocator.ErrorChannel.Publish(new SlotError("", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を呼び出す (再スローなし) |

### SlotManager との責務分担

```
BuiltinAvatarProviderFactory / BuiltinAvatarProvider
  ├─ ISlotErrorChannel に InitFailure を発行 (本 Spec の責務)
  └─ 例外をスロー (本 Spec の責務)

SlotManager (slot-core)
  ├─ 例外を捕捉 (slot-core の責務)
  └─ 該当 Slot を Disposed 状態に強制遷移 (slot-core の責務)
```

本 Spec は **発行とスローのみ** を担う。例外の抑制・Slot 状態管理は `slot-core` の責務であり、本 Spec は関与しない。

### ISlotErrorChannel へのアクセス

`BuiltinAvatarProvider` および `BuiltinAvatarProviderFactory` は `ISlotErrorChannel` を DI (コンストラクタ引数) または `RegistryLocator.ErrorChannel` の静的アクセスで取得する。テスト時は `RegistryLocator.OverrideErrorChannel()` でモック実装を注入できる。

**ISlotErrorChannel.Publish() の最終シグネチャ確認先**:

| 文書 | 章 | 内容 |
|------|---|------|
| `contracts.md` | §1.7 | `void Publish(SlotError error)` — Wave A で正式追記済み |
| `slot-core/design.md` | §3.8 | `DefaultSlotErrorChannel` 実装・`Subject<SlotError>.Synchronize()` によるスレッドセーフ保証 |

本 Spec のすべてのエラー発行は `_errorChannel.Publish(new SlotError(slotId, category, ex, DateTime.UtcNow))` の形式で統一する。`_errorChannel` は `BuiltinAvatarProviderFactory.Create()` でコンストラクタに渡されるインスタンス (DI 優先) であり、null の場合は `RegistryLocator.ErrorChannel` にフォールバックする (§6 エラーチャネル解決順序を参照)。

---

## 9. Addressable Provider 拡張余地

### 抽象遵守による変更不要の保証

将来の `avatar-provider-addressable` Spec が `IAvatarProvider` を実装する場合、本 Spec (`avatar-provider-builtin`) への変更は不要である。これは以下の設計により保証される。

1. **`IAvatarProvider` 抽象への完全準拠**: `BuiltinAvatarProvider` は `IAvatarProvider` のすべてのメンバー (`ProviderType` / `RequestAvatar` / `RequestAvatarAsync` / `ReleaseAvatar` / `Dispose`) を実装する。呼び出し側 (`SlotManager`) は `IAvatarProvider` 型で操作するため、具象型を知らない

2. **非同期 API (`RequestAvatarAsync`) が将来用のフックとして機能**: ビルトイン Provider では `UniTask.FromResult` による即時完了を返すが、Addressable Provider は `Addressables.LoadAssetAsync` 等の真の非同期処理をこの API に実装できる。`SlotManager` は `RequestAvatarAsync` を呼び出す際に `await` するだけでよく、実装の差異を意識しない

3. **Registry/Factory の独立性**: 各 Provider は独立した Factory を `IProviderRegistry` に登録する。Addressable Provider は `typeId="Addressable"` で独自の Factory を登録するのみで本 Spec に変更は生じない

4. **Config 型の独立性**: `BuiltinAvatarProviderConfig` と将来の `AddressableAvatarProviderConfig` は共に `ProviderConfigBase` を継承するが、互いに依存しない。`AvatarProviderDescriptor.ProviderTypeId` で利用する Provider を切り替えるため、Config 型の切り替えも Descriptor レベルで完結する

### 同期 / 非同期 API の使い分け方針

| Provider 種別 | `RequestAvatar` | `RequestAvatarAsync` |
|-------------|----------------|---------------------|
| BuiltinAvatarProvider (本 Spec) | `Object.Instantiate` で同期実装 | `UniTask.FromResult(RequestAvatar(config))` で即時完了 |
| 将来の AddressableAvatarProvider | `NotSupportedException` をスローしてよい | `Addressables.LoadAssetAsync` の await で真の非同期実装 |

`SlotManager` は `RequestAvatarAsync` を統一的に使用することで、将来 Addressable Provider が追加されても呼び出し側の変更を最小化できる。

---

## 10. ファイル / ディレクトリ構成

```
RealtimeAvatarController/                       # Unity プロジェクトルート
└── Packages/
    └── com.yourcompany.realtimeavatarcontroller/
        ├── Runtime/
        │   └── Avatar/
        │       └── Builtin/
        │           ├── BuiltinAvatarProviderConfig.cs
        │           ├── BuiltinAvatarProvider.cs
        │           └── BuiltinAvatarProviderFactory.cs
        ├── Editor/
        │   └── Avatar/
        │       └── Builtin/
        │           └── (将来: BuiltinAvatarProviderEditor.cs 等)
        └── Tests/
            ├── EditMode/
            │   └── Avatar/
            │       └── Builtin/
            │           ├── BuiltinAvatarProviderFactoryTests.cs
            │           └── BuiltinAvatarProviderRegistrationTests.cs
            └── PlayMode/
                └── Avatar/
                    └── Builtin/
                        ├── BuiltinAvatarProviderInstantiateTests.cs
                        └── BuiltinAvatarProviderLifecycleTests.cs
```

### アセンブリ定義 (asmdef) 一覧

| asmdef 名 | 配置先 | 参照 |
|----------|-------|------|
| `RealtimeAvatarController.Avatar.Builtin` | `Runtime/Avatar/Builtin/` | `RealtimeAvatarController.Core` |
| `RealtimeAvatarController.Avatar.Builtin.Editor` | `Editor/Avatar/Builtin/` | `RealtimeAvatarController.Core`, `RealtimeAvatarController.Avatar.Builtin` (Editor only フラグ: true) |
| `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` | `Tests/EditMode/Avatar/Builtin/` | `RealtimeAvatarController.Core`, `RealtimeAvatarController.Avatar.Builtin`, NUnit, UnityEngine.TestRunner |
| `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` | `Tests/PlayMode/Avatar/Builtin/` | `RealtimeAvatarController.Core`, `RealtimeAvatarController.Avatar.Builtin`, NUnit, UnityEngine.TestRunner |

**依存制約**: `RealtimeAvatarController.Avatar.Builtin` は `RealtimeAvatarController.Core` のみを参照し、`RealtimeAvatarController.Motion` 等の他の機能アセンブリには依存しない。

---

## 11. テスト設計

### 11.1 EditMode テスト (`RealtimeAvatarController.Avatar.Builtin.Tests.EditMode`)

Unity ランタイム (Instantiate / Destroy) に依存しないロジックを検証する。

#### テストケース一覧

| テストクラス | テストケース | 検証内容 |
|------------|------------|---------|
| `BuiltinAvatarProviderFactoryTests` | `Create_WithValidConfig_ReturnsBuiltinAvatarProvider` | 正しい `BuiltinAvatarProviderConfig` を渡した場合に `BuiltinAvatarProvider` インスタンスが返る |
| `BuiltinAvatarProviderFactoryTests` | `Create_WithInvalidConfig_ThrowsArgumentException` | `BuiltinAvatarProviderConfig` 以外の Config を渡した場合に `ArgumentException` がスローされる |
| `BuiltinAvatarProviderFactoryTests` | `Create_WithInvalidConfig_PublishesInitFailureToErrorChannel` | キャスト失敗時に `ISlotErrorChannel` に `InitFailure` が発行される |
| `BuiltinAvatarProviderFactoryTests` | `Create_IsStateless_MultipleCalls_DoNotInterfere` | 複数回の `Create()` 呼び出しが互いに干渉しない |
| `BuiltinAvatarProviderRegistrationTests` | `RegisterRuntime_RegistersBuiltinTypeId` | `[RuntimeInitializeOnLoadMethod]` 相当の登録メソッドを直接呼び出すと `typeId="Builtin"` が `IProviderRegistry` に登録される |
| `BuiltinAvatarProviderRegistrationTests` | `Register_DuplicateTypeId_ThrowsRegistryConflictException` | 同一 `typeId` の二重登録で `RegistryConflictException` がスローされる |
| `BuiltinAvatarProviderRegistrationTests` | `Resolve_AfterRegistration_ReturnsBuiltinAvatarProvider` | 登録後に `IProviderRegistry.Resolve()` を呼ぶと `BuiltinAvatarProvider` が返る |
| `BuiltinAvatarProviderLifecycleEditTests` | `ReleaseAvatar_UnmanagedObject_LogsErrorAndDoesNotDestroy` | 管理外 GameObject が渡された場合にエラーログが出力され破棄されない (モック環境) |
| `BuiltinAvatarProviderLifecycleEditTests` | `Dispose_MarksProviderAsDisposed` | `Dispose()` 後に `RequestAvatar()` を呼ぶと `ObjectDisposedException` がスローされる |

#### テストセットアップ

- `RegistryLocator.ResetForTest()` を `[SetUp]` で呼び出してテスト間の Registry 汚染を防ぐ
- `ISlotErrorChannel` はモック実装 (`FakeSlotErrorChannel`) を `RegistryLocator.OverrideErrorChannel()` で注入する

### 11.2 PlayMode テスト (`RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode`)

Unity ランタイムを必要とする `Object.Instantiate` / `Object.Destroy` を含む動作を検証する。

#### テストケース一覧

| テストクラス | テストケース | 検証内容 |
|------------|------------|---------|
| `BuiltinAvatarProviderInstantiateTests` | `RequestAvatar_ScenarioX_InstantiatesPrefab` | SO アセット経由の Config (シナリオ X) で Prefab が Scene 上にインスタンス化される |
| `BuiltinAvatarProviderInstantiateTests` | `RequestAvatar_ScenarioY_InstantiatesPrefab` | `ScriptableObject.CreateInstance` 動的生成の Config (シナリオ Y) で Prefab が Scene 上にインスタンス化される |
| `BuiltinAvatarProviderInstantiateTests` | `RequestAvatar_NullPrefab_ThrowsAndPublishesError` | `avatarPrefab` が null の場合に例外がスローされ `InitFailure` が発行される |
| `BuiltinAvatarProviderLifecycleTests` | `ReleaseAvatar_DestroysGameObject` | `ReleaseAvatar()` 呼び出し後にインスタンスが破棄される |
| `BuiltinAvatarProviderLifecycleTests` | `Dispose_DestroysAllManagedAvatars` | `Dispose()` 呼び出し後に追跡中の全 GameObject が破棄される |
| `BuiltinAvatarProviderLifecycleTests` | `RequestAvatarAsync_ReturnsInstantiatedPrefab` | `RequestAvatarAsync()` が即時完了し、インスタンス化された GameObject を返す |
| `BuiltinAvatarProviderLifecycleTests` | `MultipleSlots_ReceiveIndependentInstances` | 複数 Slot が異なる `BuiltinAvatarProvider` インスタンスを保有し、各 Slot に独立したアバターが供給される |

#### テストセットアップ

- Prefab 生成には `GameObject.CreatePrimitive(PrimitiveType.Cube)` 等で動的生成した軽量 GameObject を Prefab として使用する
- 各テスト後に `[TearDown]` で生成した GameObject の後片付けを行う

---

*本ドキュメントは `.kiro/specs/avatar-provider-builtin/design.md` として Wave B で生成された。*
