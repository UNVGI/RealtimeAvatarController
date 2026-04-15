# Spec 間公開契約

本ドキュメントは、Spec 間で共有される公開 IF・データ契約・命名・アセンブリ境界を定義する。

## 位置付け

- **主執筆**: `slot-core` Spec の requirements エージェント (Wave 1)
- **参照**: 他 5 Spec の requirements / design エージェント (Wave 2 以降)
- **更新**: design フェーズでシグネチャを確定、以降は合意変更のみ

## 記入ガイド

各セクションには `<!-- TODO: slot-core agent -->` マーカーを置いている。Wave 1 のエージェントは、このマーカーを削除したうえでセクションを埋めること。

---

## 1. Slot データモデル

Slot は VTuber アバター制御の設定単位。`SlotSettings` は Descriptor ベースの POCO として定義し、ScriptableObject・JSON・直接生成のいずれの保持形式も許容する。インターフェース型フィールドを Unity シリアライズに直接配置する旧設計は廃止し、型 ID 文字列と型付き設定オブジェクトを持つ Descriptor パターンを採用する。

### 1.1 保持する設定項目

| フィールド | 型 | 必須/省略可 | 説明 |
|-----------|---|:---------:|------|
| `slotId` | `string` | 必須 | Slot を一意に識別する主キー |
| `displayName` | `string` | 必須 | エディタ・UI 向け表示名 |
| `weight` | `float` | 必須 | モーション合成ウェイト (0.0〜1.0、範囲外はクランプ)。**初期版では常に 1.0 を使用する。** |
| `avatarProviderDescriptor` | `AvatarProviderDescriptor` | 必須 | アバター供給元の Descriptor (typeId + config) |
| `moCapSourceDescriptor` | `MoCapSourceDescriptor` | 必須 | MoCap ソースの Descriptor (typeId + config) |
| `facialControllerDescriptor` | `FacialControllerDescriptor?` | 省略可 (null 許容) | 表情制御の Descriptor |
| `lipSyncSourceDescriptor` | `LipSyncSourceDescriptor?` | 省略可 (null 許容) | リップシンクソースの Descriptor |
| `fallbackBehavior` | `FallbackBehavior` | 必須 (省略時デフォルト: `HoldLastPose`) | Applier エラー発生時の Slot フォールバック挙動 (dig ラウンド 3 確定) |

> **weight フィールドの初期版方針 (dig ラウンド 2 確定)**: 初期版 (1 Slot 1 MoCap source 構成) では `weight` は常に `1.0` として扱う。`0.0` (skip) と `1.0` (full apply) の二値動作のみが初期版の有効値である。フィールド自体は将来の複数ソース混合シナリオのためのフックとして残す。`0.0 < weight < 1.0` の中間値セマンティクスは、複数ソース混合シナリオを導入する際に改めて定義する。

**Config 基底型による Descriptor 骨格 (C# 疑似コード)**:

```csharp
// 各 Descriptor は typed POCO としてシリアライズ可能
// Config フィールドは ScriptableObject 基底型を継承した ProviderConfigBase を参照する
[Serializable]
public class AvatarProviderDescriptor
{
    // Registry に登録された具象型を識別するキー (例: "Builtin", "Addressable")
    public string ProviderTypeId;

    // 具象型ごとのコンフィグ。ProviderConfigBase (ScriptableObject 派生) を参照。
    // Inspector でドラッグ&ドロップ可能。Factory 側はキャストで具象 Config を取得する。
    public ProviderConfigBase Config;
}

[Serializable]
public class MoCapSourceDescriptor
{
    // Registry に登録された具象型を識別するキー (例: "VMC", "Custom")
    public string SourceTypeId;

    // 具象型ごとのコンフィグ。MoCapSourceConfigBase (ScriptableObject 派生) を参照。
    public MoCapSourceConfigBase Config;
}

[Serializable]
public class FacialControllerDescriptor
{
    public string ControllerTypeId;
    public FacialControllerConfigBase Config;
}

[Serializable]
public class LipSyncSourceDescriptor
{
    public string SourceTypeId;
    public LipSyncSourceConfigBase Config;
}
```

> **設計の意図**: インターフェース型フィールド (`IAvatarProvider` 等) を Unity シリアライズに直接配置することはできない。Descriptor パターンにより、具象型の選択をランタイムの Registry/Factory 解決に委ねる。利用可能な具象型はランタイムのプロジェクト構成に応じて動的に決まるため、エディタ UI も Registry から候補を列挙して表示する。Config フィールドを `ScriptableObject` 直参照ではなく各基底クラス型にすることで、Inspector での型安全な参照とアセット管理を両立する。

### 1.2 シリアライズ形式

以下の保持形式をすべて許容する設計方針を採用する:

| 形式 | 用途 | 備考 |
|------|------|------|
| Unity `ScriptableObject` | エディタプロジェクト標準 | `SlotSettings` が SO を継承する実装を推奨するが必須ではない |
| POCO (純 C# オブジェクト) | ランタイム生成・テスト | `[Serializable]` 属性付与で Unity シリアライズ対象にできる |
| JSON | ファイル保存・外部連携 | `JsonUtility` または Newtonsoft.Json によるエクスポート / インポート |

- **Descriptor フィールドはシリアライズの中核**: `AvatarProviderDescriptor` / `MoCapSourceDescriptor` 等は `[Serializable]` POCO として定義し、インターフェース直参照を避ける
- **Config フィールドは ScriptableObject 基底派生型を参照**: 各 Descriptor の `Config` フィールドの型は `ProviderConfigBase` / `MoCapSourceConfigBase` 等の基底クラス (後述 1.5 章) を使用する。これにより Inspector でのドラッグ&ドロップ可能な型安全参照を実現する。`SlotSettings` 自体は POCO/SO のいずれでも可
- **ScriptableObject は任意**: エディタとの統合性を重視する場合は SO を継承してよいが、ランタイム生成やユニットテストでは POCO のまま使用できる
- **具象型依存の分離**: `SlotSettings` 自体は具象型 (`VMCMoCapSource` 等) を知らない。型解決は Registry/Factory が担う

### 1.3 ライフサイクル

| 状態 | 説明 |
|------|------|
| `Created` | `SlotRegistry.AddSlot()` 呼び出し後、リソース未初期化 |
| `Active` | `SlotManager` が `IAvatarProvider` の初期化を完了し、動作中 |
| `Inactive` | リソースを保持したまま一時停止中 (再アクティブ化可能) |
| `Disposed` | `SlotRegistry.RemoveSlot()` 呼び出し後、または初期化失敗後に全リソース解放済み |

- **IAvatarProvider のリソース所有**: `SlotManager` が各 Slot に紐付く `IAvatarProvider` のライフサイクルを管理し、初期化・解放を制御する
- **IMoCapSource のリソース所有 (重要)**: `IMoCapSource` のライフサイクル所有は `SlotManager` ではなく **`MoCapSourceRegistry`** が担う。複数 Slot が同一 `IMoCapSource` インスタンスを参照共有できるため、Slot の破棄は `IMoCapSource` の即時解放を意味しない。`MoCapSourceRegistry` が参照管理し、不要になった時点で解放する
- **破棄タイミング**: `SlotRegistry.RemoveSlot()` 呼び出し時、または `SlotManager` の `Dispose()` 時に全 Slot を一括破棄する (ただし `IMoCapSource` の解放は `MoCapSourceRegistry` 経由)
- **エラー処理**: 破棄中の例外はキャッチしてログ記録し、残余リソースの解放を継続する
- **Slot 初期化失敗時の遷移 (dig ラウンド 3 確定)**: Provider / Source の Resolve 失敗・Factory キャスト失敗等の初期化中例外は `SlotManager` が捕捉し、該当 Slot を `Created → Disposed` 状態に強制遷移させる。エラー詳細は `ISlotErrorChannel` (1.7 章) 経由で通知される。この Slot は以降 Active にならない

---

## 1.4 ProviderRegistry / SourceRegistry 契約

> **記入者**: `slot-core` エージェント (Wave 1 / dig ラウンド 3 更新)

型 ID による Factory 解決と、エディタ UI 向け候補列挙を担う Registry 群を定義する。Registry へのアクセスは静的 Locator (`RegistryLocator`) 経由で行う (1.6 章参照)。

### Registry 骨格 (C# 疑似コード)

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 具象型の登録・解決・候補列挙を担う Registry。
    /// 起動時に属性ベース自動登録によってエントリが追加される (1.4 章「エントリ登録方式」参照)。
    /// Registry インスタンスへのアクセスは RegistryLocator.ProviderRegistry 経由で行う。
    /// </summary>
    public interface IProviderRegistry
    {
        /// <summary>
        /// typeId を持つ Factory を登録する。
        /// 同一 typeId が既に登録されている場合は RegistryConflictException をスローする (上書き禁止)。
        /// </summary>
        void Register(string providerTypeId, IAvatarProviderFactory factory);

        /// <summary>
        /// Descriptor から IAvatarProvider インスタンスを生成する。
        /// 未登録 typeId の場合は例外をスローする。
        /// </summary>
        IAvatarProvider Resolve(AvatarProviderDescriptor descriptor);

        /// <summary>
        /// 登録済みの providerTypeId 一覧を返す (エディタ UI 向け候補列挙)。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }

    /// <summary>
    /// IMoCapSource 具象型の登録・解決・候補列挙・参照共有を担う Registry。
    /// Registry インスタンスへのアクセスは RegistryLocator.MoCapSourceRegistry 経由で行う。
    /// </summary>
    public interface IMoCapSourceRegistry
    {
        /// <summary>
        /// typeId を持つ Factory を登録する。
        /// 同一 typeId が既に登録されている場合は RegistryConflictException をスローする (上書き禁止)。
        /// </summary>
        void Register(string sourceTypeId, IMoCapSourceFactory factory);

        /// <summary>
        /// Descriptor から IMoCapSource インスタンスを取得する。
        /// 同一 Descriptor を持つインスタンスが既に存在する場合は参照を共有する。
        /// 所有権管理方式 (参照カウント等) は design フェーズで確定。
        /// </summary>
        IMoCapSource Resolve(MoCapSourceDescriptor descriptor);

        /// <summary>
        /// IMoCapSource の参照を解放する通知。
        /// 参照数が 0 になった場合は Registry が Dispose() を呼び出す。
        /// </summary>
        void Release(IMoCapSource source);

        /// <summary>
        /// 登録済みの sourceTypeId 一覧を返す (エディタ UI 向け候補列挙)。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }

    // Factory インターフェース骨格
    // config 引数は ProviderConfigBase / MoCapSourceConfigBase 派生型 (1.5 章参照)
    // Factory 実装側は具象型にキャストして使用する (例: config as BuiltinAvatarProviderConfig)
    public interface IAvatarProviderFactory
    {
        IAvatarProvider Create(ProviderConfigBase config);
    }

    public interface IMoCapSourceFactory
    {
        IMoCapSource Create(MoCapSourceConfigBase config);
    }
}
```

### エントリ登録方式 (dig ラウンド 3 確定)

**属性ベース自動登録**を採用する。具象 Factory 側が以下の 2 種の属性を用いて自己登録する。

| 属性 | 実行タイミング | 目的 |
|------|-------------|------|
| `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` | ランタイム起動時 (シーンロード前) | Player / Build での自動登録 |
| `[UnityEditor.InitializeOnLoadMethod]` | Editor 起動時 (コンパイル完了後) | Inspector / エディタ UI での候補列挙 |

```csharp
// 例: BuiltinAvatarProviderFactory の自己登録
public class BuiltinAvatarProviderFactory : IAvatarProviderFactory
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory());
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor()
    {
        RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory());
    }
#endif
}
```

> **同 typeId 競合時の挙動 (dig ラウンド 3 確定)**: 同一 `typeId` を持つ Factory が既に登録されている状態で `Register()` が呼ばれた場合、**例外をスローする** (上書き禁止)。型名は `RegistryConflictException` 相当とし、正式な型名は design フェーズで確定する。「最後登録勝ち」は採用しない。デバッグ容易性を優先する。

> **Domain Reload OFF 設定 (Enter Play Mode 最適化) 下での注意**: Unity の Domain Reload を無効化している場合、`[RuntimeInitializeOnLoadMethod]` が再実行されると同一 typeId が再登録され `RegistryConflictException` が発生しうる。`RegistryLocator` は Reset / Clear メカニズムを持つ設計余地を残す (詳細は 1.6 章参照)。

> **利用者の自前 Factory 登録**: ユーザーが独自の Factory を登録する場合も同じ属性ベース自動登録の仕組みを使うことを推奨する。

---

## 1.5 Config 基底型階層

> **記入者**: `slot-core` エージェント (dig ラウンド 2 確定)

各 Descriptor が参照する Config オブジェクトは ScriptableObject を基底とした型階層に従う。これにより、Inspector でのドラッグ&ドロップによる型安全な参照と、将来の具象 Config 追加を型システムで担保する。

### 基底クラス一覧 (C# 疑似コード)

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 用 Config の抽象基底クラス。
    /// 具象 Config (例: BuiltinAvatarProviderConfig) はこのクラスを継承して定義する。
    /// AvatarProviderDescriptor.Config フィールドの型として使用する。
    /// </summary>
    public abstract class ProviderConfigBase : ScriptableObject { }

    /// <summary>
    /// IMoCapSource 用 Config の抽象基底クラス。
    /// 具象 Config (例: VMCMoCapSourceConfig) はこのクラスを継承して定義する。
    /// MoCapSourceDescriptor.Config フィールドの型として使用する。
    /// </summary>
    public abstract class MoCapSourceConfigBase : ScriptableObject { }

    /// <summary>
    /// IFacialController 用 Config の抽象基底クラス。
    /// 将来の具象 Config はこのクラスを継承する。
    /// FacialControllerDescriptor.Config フィールドの型として使用する。
    /// </summary>
    public abstract class FacialControllerConfigBase : ScriptableObject { }

    /// <summary>
    /// ILipSyncSource 用 Config の抽象基底クラス。
    /// 将来の具象 Config はこのクラスを継承する。
    /// LipSyncSourceDescriptor.Config フィールドの型として使用する。
    /// </summary>
    public abstract class LipSyncSourceConfigBase : ScriptableObject { }
}
```

### 具象 Config の定義責務

| 基底クラス | 定義責務 Spec | 具象例 |
|-----------|-------------|--------|
| `ProviderConfigBase` | `slot-core` (基底定義) / `avatar-provider-builtin` (具象定義) | `BuiltinAvatarProviderConfig` |
| `MoCapSourceConfigBase` | `slot-core` (基底定義) / `mocap-vmc` (具象定義) | `VMCMoCapSourceConfig` |
| `FacialControllerConfigBase` | `slot-core` (基底定義) / 将来担当 Spec (具象定義) | (初期段階では具象なし) |
| `LipSyncSourceConfigBase` | `slot-core` (基底定義) / 将来担当 Spec (具象定義) | (初期段階では具象なし) |

### Factory でのキャスト方法

```csharp
// 例: BuiltinAvatarProviderFactory での使用
public class BuiltinAvatarProviderFactory : IAvatarProviderFactory
{
    public IAvatarProvider Create(ProviderConfigBase config)
    {
        var builtinConfig = config as BuiltinAvatarProviderConfig;
        if (builtinConfig == null)
            throw new ArgumentException($"Expected BuiltinAvatarProviderConfig, got {config?.GetType().Name}");
        return new BuiltinAvatarProvider(builtinConfig);
    }
}
```

---

## 1.6 Registry Locator 契約

> **記入者**: `slot-core` エージェント (dig ラウンド 3 確定)

`RegistryLocator` は `IProviderRegistry` および `IMoCapSourceRegistry` への静的アクセスポイントを提供する。Editor とランタイムで**同一のインスタンス**を共有する。

### RegistryLocator 骨格 (C# 疑似コード)

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IProviderRegistry / IMoCapSourceRegistry への静的アクセスポイント。
    /// Editor 起動時およびランタイム起動時に同一インスタンスを共有する。
    /// テスト時は ResetForTest() を呼び出してインスタンスをリセットできる。
    /// </summary>
    public static class RegistryLocator
    {
        // IProviderRegistry への静的アクセスポイント
        // 属性ベース自動登録 (1.4 章) により Factory が自己登録する
        public static IProviderRegistry ProviderRegistry => GetOrCreate(ref s_providerRegistry);

        // IMoCapSourceRegistry への静的アクセスポイント
        public static IMoCapSourceRegistry MoCapSourceRegistry => GetOrCreate(ref s_moCapSourceRegistry);

        // --- テスト・Domain Reload OFF 対応 ---

        /// <summary>
        /// テスト用: Registry インスタンスを破棄してリセットする。
        /// Domain Reload OFF (Enter Play Mode 最適化) 設定下でも使用可。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
        }

        /// <summary>
        /// テスト用: 任意の IProviderRegistry 実装を注入する (モック差し替え等)。
        /// </summary>
        public static void OverrideProviderRegistry(IProviderRegistry registry)
            => s_providerRegistry = registry;

        /// <summary>
        /// テスト用: 任意の IMoCapSourceRegistry 実装を注入する。
        /// </summary>
        public static void OverrideMoCapSourceRegistry(IMoCapSourceRegistry registry)
            => s_moCapSourceRegistry = registry;

        // --- 内部実装 (詳細は design フェーズで確定) ---
        private static IProviderRegistry s_providerRegistry;
        private static IMoCapSourceRegistry s_moCapSourceRegistry;

        private static T GetOrCreate<T>(ref T field) where T : class
        {
            // デフォルト実装インスタンスを遅延生成する (詳細は design フェーズで確定)
            // ...
            return field;
        }
    }
}
```

### 設計の意図

| 懸念事項 | 対応方針 |
|---------|---------|
| Editor / Runtime で同一インスタンス共有 | 静的フィールドで保持。Domain Reload ON なら Editor 再起動時に自動リセット |
| Domain Reload OFF (Enter Play Mode 最適化) での二重登録 | `SubsystemRegistration` タイミングの `ResetForTest()` で自動リセット |
| ユニットテストでの Registry 差し替え | `OverrideProviderRegistry()` / `OverrideMoCapSourceRegistry()` で任意実装を注入 |
| 正式なインスタンス生成責務 | design フェーズで `GetOrCreate` の実装を確定する |

---

## 1.7 エラーハンドリング契約

> **記入者**: `slot-core` エージェント (dig ラウンド 3 確定)

### ISlotErrorChannel インターフェース骨格

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot に関するエラー通知チャネル。
    /// UniRx Subject ベース。購読側は ObserveOnMainThread() で受信すること。
    /// </summary>
    public interface ISlotErrorChannel
    {
        /// <summary>
        /// Slot エラーの通知ストリーム。
        /// UniRx Subject<SlotError> で実装し、ObserveOnMainThread() で受信できる。
        /// </summary>
        IObservable<SlotError> Errors { get; }
    }
}
```

### SlotError クラス骨格

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot に関するエラー情報を格納するデータクラス。
    /// </summary>
    public class SlotError
    {
        /// <summary>エラーが発生した Slot の識別子。</summary>
        public string SlotId { get; }

        /// <summary>エラーのカテゴリ。</summary>
        public SlotErrorCategory Category { get; }

        /// <summary>エラーの原因となった例外 (存在しない場合は null)。</summary>
        public Exception Exception { get; }

        /// <summary>エラー発生タイムスタンプ (UTC)。</summary>
        public DateTime Timestamp { get; }

        public SlotError(string slotId, SlotErrorCategory category, Exception exception, DateTime timestamp)
        {
            SlotId    = slotId;
            Category  = category;
            Exception = exception;
            Timestamp = timestamp;
        }
    }
}
```

### SlotErrorCategory 列挙体

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot エラーのカテゴリ分類。
    /// </summary>
    public enum SlotErrorCategory
    {
        /// <summary>VMC / OSC 受信中のパースエラー・切断検知等。</summary>
        VmcReceive,

        /// <summary>Slot 初期化失敗 (Provider/Source Resolve 失敗、Factory キャスト失敗 等)。</summary>
        InitFailure,

        /// <summary>Applier (モーション適用処理) でのエラー。</summary>
        ApplyFailure,

        /// <summary>Registry への同一 typeId 二重登録。</summary>
        RegistryConflict,

        // 将来の拡張用 (design フェーズで追加候補を検討する)
    }
}
```

### Debug.LogError 抑制ポリシー

- `Debug.LogError` は同一 `(SlotId, Category)` 組合せにつき **初回 1 フレームのみ**出力する
- 以降の同一組合せについてはログ出力を抑制する (内部 `HashSet<(string, SlotErrorCategory)>` で追跡)
- `ISlotErrorChannel.Errors` への発行は抑制なく毎回行う (UI 側でフィルタリングを行う余地を残す)
- 抑制状態は `RegistryLocator.ResetForTest()` 等のリセット時に合わせてクリアする

### エラー通知の責務分担

| エラー発生箇所 | 通知方法 |
|-------------|---------|
| Slot 初期化失敗 | `SlotManager` が `ISlotErrorChannel` に発行 |
| Applier エラー | `SlotManager` が `ISlotErrorChannel` に発行 (フォールバック後) |
| VMC 受信エラー | `IMoCapSource` 具象実装が `ISlotErrorChannel` に発行 (または SlotManager 経由) |
| Registry 競合 | `IProviderRegistry` / `IMoCapSourceRegistry` が `ISlotErrorChannel` に発行 |

> **注意**: `ISlotErrorChannel` のインスタンス取得方法 (Locator 経由 / DI 等) は design フェーズで確定する。

---

## 1.8 Fallback 挙動契約

> **記入者**: `slot-core` エージェント (dig ラウンド 3 確定)

### FallbackBehavior 列挙体

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Applier (モーション適用処理) でエラーが発生した際の Slot フォールバック挙動。
    /// SlotSettings.fallbackBehavior フィールドで Slot ごとに設定する。
    /// </summary>
    public enum FallbackBehavior
    {
        /// <summary>
        /// エラー発生時、直前フレームのポーズを維持し続ける (デフォルト)。
        /// Applier がエラーを起こしても視覚的な崩れを最小化する。
        /// </summary>
        HoldLastPose,

        /// <summary>
        /// エラー発生時、アバターを T ポーズに戻す。
        /// 問題発生を視覚的に認識しやすくするデバッグ用途に適する。
        /// </summary>
        TPose,

        /// <summary>
        /// エラー発生時、アバターを非表示にする。
        /// アバターが破綻した状態で表示されることを防ぐ。
        /// </summary>
        Hide,
    }
}
```

### SlotSettings.fallbackBehavior の位置付け

- `SlotSettings` の `fallbackBehavior` フィールド (1.1 章参照) で Slot ごとに個別設定する
- **デフォルト値: `FallbackBehavior.HoldLastPose`**
- フォールバック実行後、`SlotManager` は `ISlotErrorChannel` にエラーを通知する (1.7 章参照)
- フォールバック状態からの回復方法 (エラーが解消した場合の挙動) は design フェーズで確定する

---

## 2. MoCap ソース抽象

### 2.1 `IMoCapSource` シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格・契約のみ定義する。

**設計方針 (dig ラウンド 1・2 反映)**:
- Pull 型 (`FetchLatestMotion()`) を**廃止**し、Push 型 (`IObservable<MotionFrame>`) を採用する
- 受信全フレームを逃さず低レイテンシで処理するため UniRx `Subject` ベースのストリーミングを採用する
- **採用ライブラリ: UniRx (`com.neuecc.unirx`) ― R3 は採用しない**
- `UniRx` は `RealtimeAvatarController.Core` アセンブリの依存として追加する (asmdef の references に `UniRx` を記載)
- 1 つの `IMoCapSource` インスタンスを複数 Slot で参照共有できる (ライフサイクルは `MoCapSourceRegistry` が管理する)

> **UniRx 採用理由 (dig ラウンド 2 確定)**: UniRx (`com.neuecc.unirx`) を採用する。R3 は採用しない。UniRx の `IObservable<T>` は `System.IObservable<T>` を実装しているため、契約の型シグネチャは `System.IObservable<MotionFrame>` のままで変更不要である。NuGet 依存を持たないため UPM 配布での scoped registry が OpenUPM 1 個のみで済み、配布手続きが簡素化される。

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
// using UniRx; を追加して ObserveOnMainThread() 等の拡張メソッドを利用する
// IObservable<MotionFrame> は System.IObservable<T> であり、UniRx の Subject<T> はこれを実装する
public interface IMoCapSource : IDisposable
{
    // ソース種別識別子 (例: "VMC", "Custom" 等)
    string SourceType { get; }

    // 初期化: 通信パラメータ (ポート番号等) を受け取る
    // 引数型は design フェーズで確定 (MoCapSourceConfigBase 派生型を想定)
    void Initialize(/* MoCapSourceConfigBase config */);

    // Push 型モーションストリーム
    // 型: System.IObservable<MotionFrame> (UniRx Subject<T> で実装)
    // 受信スレッドが Subject.OnNext() を呼び出す
    // 購読側は UniRx の ObserveOnMainThread() 拡張メソッドで Unity メインスレッドに同期する
    // 戻り値の MotionFrame は motion-pipeline が定義するモーションデータ中立表現 (2.2 章) に準拠
    IObservable<MotionFrame> MotionStream { get; }

    // 破棄 (IDisposable.Dispose() で代替可)
    void Shutdown();
}
```

**スレッド安全性の要求**:
- `MotionStream` への `OnNext()` 呼び出しは受信スレッドから行われる
- 購読側 (Slot / Pipeline) は UniRx の `.ObserveOnMainThread()` 拡張メソッドを使用して Unity メインスレッドで処理すること (`using UniRx;` が必要)
- `Initialize()` / `Shutdown()` はメインスレッドからの呼び出しを前提とする
- `Subject<MotionFrame>` への `OnNext()` は UniRx の既定ではスレッドセーフではないため、具象実装は `Subject` のスレッドセーフラッパー (`Subject.Synchronize()` 等) または `SerialDisposable` + `lock` を使用すること (詳細は design フェーズで確定)

**エラーハンドリング方針 (dig ラウンド 3 確定)**:
- `IMoCapSource.MotionStream` は **`OnError` を発行しない**。Observable ストリームとしてエラーで終端しない設計とする
- パースエラー・切断検知等の受信エラーが発生した場合は内部でログ出力 (`Debug.LogError`) し、ストリームの購読を継続する (受信再試行またはサイレント破棄)
- `Debug.LogError` の出力は `ISlotErrorChannel` (1.7 章) の抑制ポリシーに従い、同一 (SlotId, Category) 組合せにつき初回 1 フレームのみ出力し、以降は抑制する
- これにより購読側 (`Slot` / `motion-pipeline`) は `OnError` に対するエラー回復ロジックを持つ必要がない

**参照共有に関する注記**:
- `IMoCapSource` インスタンスは複数の Slot から共有参照される場合がある
- `MotionStream` は UniRx の `Publish().RefCount()` 等でマルチキャスト化することで、複数購読者が同一ストリームを購読できる
- インスタンスのライフサイクル (Dispose タイミング) は `MoCapSourceRegistry` (1.4 章) が管理する。Slot 側から直接 `Dispose()` を呼び出してはならない

### 2.2 モーションデータ中立表現

> **記入者**: `motion-pipeline` エージェント (Wave 2)。`slot-core` の 2.1 章と整合したうえで型骨格を確定。

#### 基底型: `MotionFrame`

全骨格形式 (Humanoid / Generic 等) の共通基底型。`IMoCapSource.MotionStream` (Push 型ストリーム) が流すフレーム型として採用する。

```csharp
// 骨格 (C# 疑似コード / 型名は仮。最終シグネチャは design フェーズで確定)
namespace RealtimeAvatarController.Motion
{
    // 骨格種別識別子
    public enum SkeletonType
    {
        Humanoid,
        Generic,
    }

    // 全骨格形式共通の基底型 (抽象クラスまたはインターフェース; design フェーズで確定)
    public abstract class MotionFrame
    {
        // 受信タイムスタンプ (Unix 時刻相当; 単位は design フェーズで確定)
        public double Timestamp { get; }

        // この フレームが表す骨格種別
        public abstract SkeletonType SkeletonType { get; }
    }
}
```

#### Humanoid 向け中立表現: `HumanoidMotionFrame`

Unity `HumanPose` 相当の構造を持つ具象型。Muscle 値配列と Root の位置・回転を保持する。

```csharp
namespace RealtimeAvatarController.Motion
{
    // Humanoid 骨格向けモーションフレーム (C# 疑似コード)
    public sealed class HumanoidMotionFrame : MotionFrame
    {
        public override SkeletonType SkeletonType => SkeletonType.Humanoid;

        // Unity HumanPose.muscles 相当 (要素数は HumanTrait.MuscleCount に準拠)
        public float[] Muscles { get; }

        // ルート位置 (ワールド空間またはローカル空間; design フェーズで確定)
        public Vector3 RootPosition { get; }

        // ルート回転
        public Quaternion RootRotation { get; }
    }
}
```

**補足**:
- `Muscles.Length == 0` は「データなし / 初期化前」を示す無効フレームとして扱う
- イミュータブル設計 (コンストラクタで全値を受け取り、以降は読み取り専用) を推奨する

#### Generic 向け中立表現 (初期段階: 抽象のみ)

初期段階では具象型は定義しない。将来の Generic 具象実装のためにプレースホルダーとして以下の方針のみを合意する。

```csharp
// 将来実装向けプレースホルダー (C# 疑似コード / 初期段階では実装しない)
namespace RealtimeAvatarController.Motion
{
    // Generic 骨格向けモーションフレーム (design フェーズ以降で具体化)
    // Transform 配列 (位置・回転・スケール) を保持することを想定
    public sealed class GenericMotionFrame : MotionFrame
    {
        public override SkeletonType SkeletonType => SkeletonType.Generic;

        // 各ボーンの Transform データ (型・構造は design フェーズで確定)
        // public TransformData[] Bones { get; }
    }
}
```

#### `IMoCapSource.MotionStream` のフレーム型方針

- **採用方針**: `IObservable<MotionFrame>` のフレーム型は `MotionFrame` 基底型を使用する
- 購読側は `MotionFrame.SkeletonType` を確認してキャストする、またはジェネリクス (`IMoCapSource<TFrame>`) を採用する。最終シグネチャは design フェーズで確定する
- 例示 (仮): `IObservable<MotionFrame> MotionStream { get; }` (2.1 章参照)

#### スレッド安全性の要求

- **書き込み**: 受信スレッド (`IMoCapSource` 具象実装の内部スレッド等) から `MotionFrame` を書き込む
- **読み込み**: Unity メインスレッド (`LateUpdate` 等) から最新の `MotionFrame` を読み取る
- **方針**: 具体的なスレッド安全実装 (ダブルバッファ / `Interlocked` / `lock` / ロックレスキュー等) は design フェーズで選択する。受信スレッド側の書き込みは Unity API を呼び出さない

---

## 3. アバター供給抽象

### 3.1 `IAvatarProvider` (仮) シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格・契約のみ定義する。

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
public interface IAvatarProvider : IDisposable
{
    // Provider 種別識別子 (例: "Builtin", "Addressable" 等)
    string ProviderType { get; }

    // アバター要求 (同期版)
    // 戻り値は供給された GameObject 参照 (Prefab インスタンス)
    GameObject RequestAvatar(/* AvatarRequest request */);

    // アバター要求 (非同期版): Addressable 等の将来実装に対応する拡張余地
    // UniTask / Task<GameObject> どちらを採用するかは design フェーズで確定
    /* Task<GameObject> */ object RequestAvatarAsync(/* AvatarRequest request */);

    // アバター解放: 供給した GameObject を受け取りリソースを解放する
    void ReleaseAvatar(GameObject avatar);
}
```

**同期 / 非同期の許容方針**:
- `IAvatarProvider` は同期・非同期のいずれの具象実装も許容する
- ビルトイン Provider (初期段階) は同期版を実装する
- Addressable Provider (将来) は非同期版を実装し、同期版は NotImplementedException でも可

### 3.2 Addressable 拡張余地

- 初期段階で Addressable Provider は実装しない
- `IAvatarProvider` は同期・非同期のいずれの具象実装も許容するシグネチャとする

---

## 4. 表情制御抽象 (受け口のみ)

### 4.1 `IFacialController` (仮) シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格のみ定義する。

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
public interface IFacialController : IDisposable
{
    // 初期化: 制御対象アバターの GameObject を受け取る
    void Initialize(/* GameObject avatarRoot */);

    // 表情データ適用: 表情データ型は design フェーズで確定
    void ApplyFacialData(/* FacialData data */);

    // 解放 (IDisposable.Dispose() で代替可)
    void Shutdown();
}
```

### 4.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。

---

## 5. リップシンク抽象 (受け口のみ)

### 5.1 `ILipSyncSource` (仮) シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格のみ定義する。

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
public interface ILipSyncSource : IDisposable
{
    // 初期化
    void Initialize(/* LipSyncSourceConfig config */);

    // リップシンクデータ取得 (pull 型を基本とする; push / イベント型は design フェーズで検討)
    // 戻り値型は design フェーズで確定 (母音ブレンドシェイプ値配列等を想定)
    /* LipSyncData */ object FetchLatestLipSync();

    // 解放 (IDisposable.Dispose() で代替可)
    void Shutdown();
}
```

### 5.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。

---

## 6. アセンブリ / 名前空間境界

### 6.1 アセンブリ定義 (asmdef) の構成

以下の asmdef を正式採用する。各アセンブリは担当 Spec の実装フェーズで実際のファイルとして作成される。

#### Runtime asmdef

| asmdef 名 | 担当 Spec | 配置パス (パッケージルート相対) | 備考 |
|-----------|----------|-------------------------------|------|
| `RealtimeAvatarController.Core` | slot-core | `Runtime/Core/` | Slot 抽象・各公開インターフェース群 |
| `RealtimeAvatarController.Motion` | motion-pipeline | `Runtime/Motion/` | モーションデータ中立表現・パイプライン |
| `RealtimeAvatarController.MoCap.VMC` | mocap-vmc | `Runtime/MoCap/VMC/` | VMC OSC 受信具象実装 |
| `RealtimeAvatarController.Avatar.Builtin` | avatar-provider-builtin | `Runtime/Avatar/Builtin/` | ビルトインアバター供給具象実装 |
| `RealtimeAvatarController.Samples.UI` | ui-sample | `Samples~/UI/` | UI サンプル (Samples~ 機構) |

#### Editor 専用 asmdef

各機能アセンブリに対応する Editor 専用 asmdef を定義する。`[UnityEditor.InitializeOnLoadMethod]` など `UnityEditor` 名前空間の API はこの Editor asmdef 内に配置する。

| asmdef 名 | 担当 Spec | 配置パス (パッケージルート相対) | 備考 |
|-----------|----------|-------------------------------|------|
| `RealtimeAvatarController.Core.Editor` | slot-core | `Editor/Core/` | Core アセンブリ向けエディタ拡張・Factory Editor 登録 |
| `RealtimeAvatarController.Motion.Editor` | motion-pipeline | `Editor/Motion/` | Motion アセンブリ向けエディタ拡張 |
| `RealtimeAvatarController.MoCap.VMC.Editor` | mocap-vmc | `Editor/MoCap/VMC/` | VMC アセンブリ向けエディタ拡張 |
| `RealtimeAvatarController.Avatar.Builtin.Editor` | avatar-provider-builtin | `Editor/Avatar/Builtin/` | Avatar.Builtin アセンブリ向けエディタ拡張 |
| `RealtimeAvatarController.Samples.UI.Editor` | ui-sample | `Samples~/UI/Editor/` | UI サンプル向けエディタ拡張 (必要な場合のみ) |

**Editor asmdef の依存ルール**:
- 各 Editor asmdef は `includePlatforms: ["Editor"]` を指定し、Unity Editor 環境でのみコンパイルされる
- 各 Editor asmdef は対応する Runtime asmdef を `references` に追加する片方向依存のみを持つ
- Runtime asmdef は Editor asmdef を参照しない (逆依存禁止)
- `#if UNITY_EDITOR` による Runtime asmdef 内への UnityEditor API 配置は代替手段として許容する (各 Spec の実装判断に委ねる)

**依存方向の制約 (Runtime)**:
- `Samples.UI` → 機能部アセンブリ各種 (一方向のみ)
- 機能部アセンブリは `Samples.UI` を参照しない
- UI フレームワーク (UGUI / UIToolkit 等) への依存は `Samples.UI` にのみ許容する

**外部ライブラリ依存 (UniRx)**:
- `RealtimeAvatarController.Core` は UniRx (`com.neuecc.unirx`) の asmdef (`UniRx`) を `references` に追加し、`IObservable<T>` 拡張メソッド・`Subject<T>` 等を直接利用する
- `RealtimeAvatarController.Motion`・`RealtimeAvatarController.MoCap.VMC`・`RealtimeAvatarController.Avatar.Builtin` は UniRx の asmdef を直接 `references` に持たず、`RealtimeAvatarController.Core` 経由で UniRx の型を間接利用する (二重依存禁止)
- ただし各アセンブリが UniRx の拡張メソッド (`ObserveOnMainThread()` 等) を直接呼び出す技術的必要が生じた場合は、design フェーズで要否を個別判断し本章に追記する
- `RealtimeAvatarController.Samples.UI` も同様に UniRx への直接依存は持たず、機能部 API 経由で利用する

### 6.2 名前空間規約

ルート名前空間を `RealtimeAvatarController` として確定する。

| 名前空間 | 対応 asmdef | 用途 |
|---------|------------|------|
| `RealtimeAvatarController.Core` | `RealtimeAvatarController.Core` | Slot・各公開インターフェース |
| `RealtimeAvatarController.Motion` | `RealtimeAvatarController.Motion` | モーションデータ・パイプライン |
| `RealtimeAvatarController.MoCap.VMC` | `RealtimeAvatarController.MoCap.VMC` | VMC 受信実装 |
| `RealtimeAvatarController.Avatar.Builtin` | `RealtimeAvatarController.Avatar.Builtin` | ビルトインアバター供給実装 |
| `RealtimeAvatarController.Samples.UI` | `RealtimeAvatarController.Samples.UI` | UI サンプル |
| `RealtimeAvatarController.*.Editor` | (各 asmdef の Editor 限定) | エディタ拡張コード |

**規約の補足**:
- 各クラス・インターフェースは上記マッピングに従い名前空間を選択する
- エディタ限定コードは対応する機能名前空間に `.Editor` サブ名前空間を付加する (例: `RealtimeAvatarController.Core.Editor`)
- テストコードは `.Tests` サブ名前空間を付加する (例: `RealtimeAvatarController.Core.Tests`)

---

## 7. 変更管理

- 本ドキュメントは Spec 間の**公式合意**として扱う
- requirements フェーズ以降の変更は、影響を受ける全 Spec の担当エージェント (または人間レビュー) で合意が必要
- シグネチャ確定は design フェーズで完了させる
