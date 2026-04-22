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

> **SlotSettings のランタイム動的生成 (dig ラウンド 4 確定)**: `SlotSettings` は `ScriptableObject.CreateInstance<SlotSettings>()` によるランタイム動的生成を公式に許容する。SO アセットをエディタで編集する**シナリオ X** と、ランタイムコードで `CreateInstance` して各フィールドを直接セットする**シナリオ Y** の**両方**を公式サポートする。どちらの形式で生成したインスタンスも `SlotManager` に渡して Slot を生成できる。長期保存 (JSON ファイル等への永続化) は**将来要件**とし、本ラウンドでは実装・契約しない。受け口のみを確保する (1.2 章参照)。

**Config 基底型による Descriptor 骨格 (C# 疑似コード)**:

```csharp
// 各 Descriptor は typed POCO としてシリアライズ可能
// Config フィールドは ScriptableObject 基底型を継承した ProviderConfigBase を参照する
// IEquatable<T> を実装し、IMoCapSourceRegistry の参照共有辞書キーとして利用可能にする
[Serializable]
public class AvatarProviderDescriptor : IEquatable<AvatarProviderDescriptor>
{
    // Registry に登録された具象型を識別するキー (例: "Builtin", "Addressable")
    public string ProviderTypeId;

    // 具象型ごとのコンフィグ。ProviderConfigBase (ScriptableObject 派生) を参照。
    // Inspector でドラッグ&ドロップ可能。Factory 側はキャストで具象 Config を取得する。
    // 等価判定は参照等価 (ReferenceEquals) を使用する。
    public ProviderConfigBase Config;

    public bool Equals(AvatarProviderDescriptor other)
        => other != null
           && ProviderTypeId == other.ProviderTypeId
           && ReferenceEquals(Config, other.Config);

    public override bool Equals(object obj) => Equals(obj as AvatarProviderDescriptor);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (ProviderTypeId != null ? ProviderTypeId.GetHashCode() : 0);
            hash = hash * 31 + (Config != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Config) : 0);
            return hash;
        }
    }

    public static bool operator ==(AvatarProviderDescriptor a, AvatarProviderDescriptor b)
        => a is null ? b is null : a.Equals(b);
    public static bool operator !=(AvatarProviderDescriptor a, AvatarProviderDescriptor b)
        => !(a == b);
}

[Serializable]
public class MoCapSourceDescriptor : IEquatable<MoCapSourceDescriptor>
{
    // Registry に登録された具象型を識別するキー (例: "VMC", "Custom")
    public string SourceTypeId;

    // 具象型ごとのコンフィグ。MoCapSourceConfigBase (ScriptableObject 派生) を参照。
    // 等価判定は参照等価 (ReferenceEquals) を使用する。
    public MoCapSourceConfigBase Config;

    public bool Equals(MoCapSourceDescriptor other)
        => other != null
           && SourceTypeId == other.SourceTypeId
           && ReferenceEquals(Config, other.Config);

    public override bool Equals(object obj) => Equals(obj as MoCapSourceDescriptor);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (SourceTypeId != null ? SourceTypeId.GetHashCode() : 0);
            hash = hash * 31 + (Config != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Config) : 0);
            return hash;
        }
    }

    public static bool operator ==(MoCapSourceDescriptor a, MoCapSourceDescriptor b)
        => a is null ? b is null : a.Equals(b);
    public static bool operator !=(MoCapSourceDescriptor a, MoCapSourceDescriptor b)
        => !(a == b);
}

[Serializable]
public class FacialControllerDescriptor : IEquatable<FacialControllerDescriptor>
{
    public string ControllerTypeId;
    public FacialControllerConfigBase Config;

    public bool Equals(FacialControllerDescriptor other)
        => other != null
           && ControllerTypeId == other.ControllerTypeId
           && ReferenceEquals(Config, other.Config);

    public override bool Equals(object obj) => Equals(obj as FacialControllerDescriptor);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (ControllerTypeId != null ? ControllerTypeId.GetHashCode() : 0);
            hash = hash * 31 + (Config != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Config) : 0);
            return hash;
        }
    }

    public static bool operator ==(FacialControllerDescriptor a, FacialControllerDescriptor b)
        => a is null ? b is null : a.Equals(b);
    public static bool operator !=(FacialControllerDescriptor a, FacialControllerDescriptor b)
        => !(a == b);
}

[Serializable]
public class LipSyncSourceDescriptor : IEquatable<LipSyncSourceDescriptor>
{
    public string SourceTypeId;
    public LipSyncSourceConfigBase Config;

    public bool Equals(LipSyncSourceDescriptor other)
        => other != null
           && SourceTypeId == other.SourceTypeId
           && ReferenceEquals(Config, other.Config);

    public override bool Equals(object obj) => Equals(obj as LipSyncSourceDescriptor);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (SourceTypeId != null ? SourceTypeId.GetHashCode() : 0);
            hash = hash * 31 + (Config != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Config) : 0);
            return hash;
        }
    }

    public static bool operator ==(LipSyncSourceDescriptor a, LipSyncSourceDescriptor b)
        => a is null ? b is null : a.Equals(b);
    public static bool operator !=(LipSyncSourceDescriptor a, LipSyncSourceDescriptor b)
        => !(a == b);
}
```

> **Descriptor 等価判定方針 (design フェーズ確定)**: Config フィールドの等価判定は ScriptableObject の**参照等価** (`ReferenceEquals`) を使用する。これは `MoCapSourceRegistry` が `Dictionary<MoCapSourceDescriptor, IMoCapSource>` を辞書キーとして使用する際の前提条件であり、`GetHashCode()` は `typeId` の文字列ハッシュと Config の参照ハッシュ (`RuntimeHelpers.GetHashCode`) を組み合わせる。同一 SO アセットを参照している場合は参照が等価となるため、参照共有が正しく機能する。

> **設計の意図**: インターフェース型フィールド (`IAvatarProvider` 等) を Unity シリアライズに直接配置することはできない。Descriptor パターンにより、具象型の選択をランタイムの Registry/Factory 解決に委ねる。利用可能な具象型はランタイムのプロジェクト構成に応じて動的に決まるため、エディタ UI も Registry から候補を列挙して表示する。Config フィールドを `ScriptableObject` 直参照ではなく各基底クラス型にすることで、Inspector での型安全な参照とアセット管理を両立する。

### 1.2 シリアライズ形式

以下の保持形式をすべて**公式に許容**する設計方針を採用する:

| 形式 | 用途 | 備考 |
|------|------|------|
| Unity `ScriptableObject` アセット編集 (シナリオ X) | エディタプロジェクト標準。アセットとして `.asset` 保存 | `SlotSettings` が SO を継承する実装を推奨するが必須ではない |
| ランタイム動的生成 (シナリオ Y) | ランタイムコードから `ScriptableObject.CreateInstance<SlotSettings>()` で生成し、各フィールドを直接セット | Unity ライフサイクル上は SO インスタンスだがアセットファイルは作成しない |
| POCO (純 C# オブジェクト) | ユニットテスト・軽量生成 | `[Serializable]` 属性付与で Unity シリアライズ対象にできる |
| JSON 永続化 | ファイル保存・外部連携 | **将来要件**。初期段階では実装しない。受け口のみ確保 (下記注記参照) |

> **JSON 永続化は将来要件 (dig ラウンド 4 確定)**: JSON エクスポート/インポートは本ラウンドでは**実装・契約しない**。`SlotSettings` の設計は JSON 永続化を将来追加できる構造 (`[Serializable]` POCO / `JsonUtility` / Newtonsoft.Json への拡張余地) を確保するに留める。Config アセット参照の GUID 依存問題 (ScriptableObject 参照の JSON 表現) は、JSON 永続化を導入する際の design フェーズで詳細を詰める。

- **Descriptor フィールドはシリアライズの中核**: `AvatarProviderDescriptor` / `MoCapSourceDescriptor` 等は `[Serializable]` POCO として定義し、インターフェース直参照を避ける
- **Config フィールドは ScriptableObject 基底派生型を参照**: 各 Descriptor の `Config` フィールドの型は `ProviderConfigBase` / `MoCapSourceConfigBase` 等の基底クラス (後述 1.5 章) を使用する。これにより Inspector でのドラッグ&ドロップ可能な型安全参照を実現する。`SlotSettings` 自体は POCO/SO のいずれでも可
- **ScriptableObject は任意 (シナリオ X/Y 両対応)**: エディタとの統合性を重視する場合は SO を継承してよく、ランタイム動的生成 (`CreateInstance`) でも同一の SO 継承クラスを使用できる。ユニットテストでは POCO のまま使用することも可能
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

> **記入者**: `slot-core` エージェント (dig ラウンド 2 確定 / dig ラウンド 4 更新)

各 Descriptor が参照する Config オブジェクトは ScriptableObject を基底とした型階層に従う。これにより、Inspector でのドラッグ&ドロップによる型安全な参照と、将来の具象 Config 追加を型システムで担保する。

> **ランタイム動的生成の許容 (dig ラウンド 4 確定)**: `ProviderConfigBase` / `MoCapSourceConfigBase` / `FacialControllerConfigBase` / `LipSyncSourceConfigBase` の各基底型派生クラスは、Unity エディタでアセットとして編集するだけでなく、**`ScriptableObject.CreateInstance<T>()` によるランタイム動的生成も公式に許容する**。ランタイムで生成した Config インスタンスには、具象 Config の公開フィールドを直接セットして設定値を与える。この設計により、SO アセット編集 (シナリオ X) とランタイム動的構築 (シナリオ Y) の両方で同一の Factory / Registry API を透過的に使用できる。

### 基底クラス一覧 (C# 疑似コード)

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 用 Config の抽象基底クラス。
    /// 具象 Config (例: BuiltinAvatarProviderConfig) はこのクラスを継承して定義する。
    /// AvatarProviderDescriptor.Config フィールドの型として使用する。
    /// SO アセット編集 (シナリオ X) / ScriptableObject.CreateInstance ランタイム動的生成 (シナリオ Y) の両方を許容する。
    /// </summary>
    public abstract class ProviderConfigBase : ScriptableObject { }

    /// <summary>
    /// IMoCapSource 用 Config の抽象基底クラス。
    /// 具象 Config (例: VMCMoCapSourceConfig) はこのクラスを継承して定義する。
    /// MoCapSourceDescriptor.Config フィールドの型として使用する。
    /// SO アセット編集 (シナリオ X) / ScriptableObject.CreateInstance ランタイム動的生成 (シナリオ Y) の両方を許容する。
    /// </summary>
    public abstract class MoCapSourceConfigBase : ScriptableObject { }

    /// <summary>
    /// IFacialController 用 Config の抽象基底クラス。
    /// 将来の具象 Config はこのクラスを継承する。
    /// FacialControllerDescriptor.Config フィールドの型として使用する。
    /// SO アセット編集 (シナリオ X) / ScriptableObject.CreateInstance ランタイム動的生成 (シナリオ Y) の両方を許容する。
    /// </summary>
    public abstract class FacialControllerConfigBase : ScriptableObject { }

    /// <summary>
    /// ILipSyncSource 用 Config の抽象基底クラス。
    /// 将来の具象 Config はこのクラスを継承する。
    /// LipSyncSourceDescriptor.Config フィールドの型として使用する。
    /// SO アセット編集 (シナリオ X) / ScriptableObject.CreateInstance ランタイム動的生成 (シナリオ Y) の両方を許容する。
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

### ランタイム動的生成パターン (シナリオ Y)

```csharp
// 例: ランタイムで VMCMoCapSourceConfig を動的生成して SlotSettings を構築する
// SlotSettings も CreateInstance で生成し、フィールドを直接セットする
var moCapConfig = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
moCapConfig.port = 39539;
moCapConfig.bindAddress = "0.0.0.0";

var providerConfig = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
providerConfig.avatarPrefab = someAvatarPrefab;

var slotSettings = ScriptableObject.CreateInstance<SlotSettings>();
slotSettings.slotId = "slot-01";
slotSettings.displayName = "Player 1";
slotSettings.weight = 1.0f;
slotSettings.moCapSourceDescriptor = new MoCapSourceDescriptor
{
    SourceTypeId = "VMC",
    Config = moCapConfig,
};
slotSettings.avatarProviderDescriptor = new AvatarProviderDescriptor
{
    ProviderTypeId = "Builtin",
    Config = providerConfig,
};
slotSettings.fallbackBehavior = FallbackBehavior.HoldLastPose;

// SlotManager の AddSlot API でそのまま渡せる
await slotManager.AddSlotAsync(slotSettings);
```

### Factory でのキャスト方法

```csharp
// 例: BuiltinAvatarProviderFactory での使用
// SO アセット経由でも CreateInstance 動的生成経由でも同一 Factory コードが動作する
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

> **最終確定 (design フェーズ)**: contracts.md は design.md §3.7 に同期済み。以下が正式仕様。

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IProviderRegistry / IMoCapSourceRegistry / IFacialControllerRegistry /
    /// ILipSyncSourceRegistry / ISlotErrorChannel への静的アクセスポイント。
    /// Editor 起動時・ランタイム起動時に同一インスタンスを共有する。
    /// Domain Reload OFF 対応: SubsystemRegistration タイミングで自動リセットする。
    /// テスト時は ResetForTest() / Override*() を使用してインスタンスを差し替える。
    /// </summary>
    public static class RegistryLocator
    {
        // --- 公開プロパティ ---

        /// <summary>IProviderRegistry への静的アクセスポイント。遅延初期化 (スレッドセーフ)。</summary>
        public static IProviderRegistry ProviderRegistry
            => s_providerRegistry
               ?? Interlocked.CompareExchange(ref s_providerRegistry, new DefaultProviderRegistry(), null)
               ?? s_providerRegistry;

        /// <summary>IMoCapSourceRegistry への静的アクセスポイント。遅延初期化 (スレッドセーフ)。</summary>
        public static IMoCapSourceRegistry MoCapSourceRegistry
            => s_moCapSourceRegistry
               ?? Interlocked.CompareExchange(ref s_moCapSourceRegistry, new DefaultMoCapSourceRegistry(), null)
               ?? s_moCapSourceRegistry;

        /// <summary>IFacialControllerRegistry への静的アクセスポイント。遅延初期化 (将来用)。</summary>
        public static IFacialControllerRegistry FacialControllerRegistry
            => s_facialControllerRegistry
               ?? Interlocked.CompareExchange(ref s_facialControllerRegistry, new DefaultFacialControllerRegistry(), null)
               ?? s_facialControllerRegistry;

        /// <summary>ILipSyncSourceRegistry への静的アクセスポイント。遅延初期化 (将来用)。</summary>
        public static ILipSyncSourceRegistry LipSyncSourceRegistry
            => s_lipSyncSourceRegistry
               ?? Interlocked.CompareExchange(ref s_lipSyncSourceRegistry, new DefaultLipSyncSourceRegistry(), null)
               ?? s_lipSyncSourceRegistry;

        /// <summary>ISlotErrorChannel への静的アクセスポイント。遅延初期化。</summary>
        public static ISlotErrorChannel ErrorChannel
            => s_errorChannel
               ?? Interlocked.CompareExchange(ref s_errorChannel, new DefaultSlotErrorChannel(), null)
               ?? s_errorChannel;

        // --- テスト・Domain Reload OFF 対応 API ---

        /// <summary>
        /// 全 Registry インスタンスをリセットする。
        /// Domain Reload OFF 設定下での二重登録防止。SubsystemRegistration タイミングで自動実行される。
        /// ユニットテストの [TearDown] でも明示的に呼び出すこと。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
            s_facialControllerRegistry = null;
            s_lipSyncSourceRegistry = null;
            s_errorChannel = null;
        }

        /// <summary>テスト用: 任意の IProviderRegistry 実装を注入する (モック差し替え等)。</summary>
        public static void OverrideProviderRegistry(IProviderRegistry registry)
            => s_providerRegistry = registry;

        /// <summary>テスト用: 任意の IMoCapSourceRegistry 実装を注入する。</summary>
        public static void OverrideMoCapSourceRegistry(IMoCapSourceRegistry registry)
            => s_moCapSourceRegistry = registry;

        /// <summary>テスト用: 任意の IFacialControllerRegistry 実装を注入する (将来用)。</summary>
        public static void OverrideFacialControllerRegistry(IFacialControllerRegistry registry)
            => s_facialControllerRegistry = registry;

        /// <summary>テスト用: 任意の ILipSyncSourceRegistry 実装を注入する (将来用)。</summary>
        public static void OverrideLipSyncSourceRegistry(ILipSyncSourceRegistry registry)
            => s_lipSyncSourceRegistry = registry;

        /// <summary>テスト用: 任意の ISlotErrorChannel 実装を注入する。</summary>
        public static void OverrideErrorChannel(ISlotErrorChannel channel)
            => s_errorChannel = channel;

        // --- 内部フィールド ---
        private static IProviderRegistry s_providerRegistry;
        private static IMoCapSourceRegistry s_moCapSourceRegistry;
        private static IFacialControllerRegistry s_facialControllerRegistry;
        private static ILipSyncSourceRegistry s_lipSyncSourceRegistry;
        private static ISlotErrorChannel s_errorChannel;
    }
}
```

### 設計の意図

| 懸念事項 | 対応方針 |
|---------|---------|
| Editor / Runtime で同一インスタンス共有 | 静的フィールドで保持。Domain Reload ON なら Editor 再起動時に自動リセット |
| Domain Reload OFF (Enter Play Mode 最適化) での二重登録 | `SubsystemRegistration` タイミングの `ResetForTest()` で自動リセット |
| ユニットテストでの Registry 差し替え | `Override*()` メソッド群で任意実装を注入 |
| 遅延初期化のスレッド安全性 | `Interlocked.CompareExchange` によりアトミック初期化を保証 (volatile キーワード不要) |
| IFacialControllerRegistry / ILipSyncSourceRegistry | design フェーズで追加確定。`Override*()` および `ErrorChannel` も提供 |

---

## 1.7 エラーハンドリング契約

> **記入者**: `slot-core` エージェント (dig ラウンド 3 確定)

### ISlotErrorChannel インターフェース骨格

> **最終確定 (design フェーズ)**: `Publish(SlotError)` メソッドを正式追加。slot-core/design.md §3.8 と同期済み。

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
        /// 発行は抑制なく毎回行う。
        /// </summary>
        IObservable<SlotError> Errors { get; }

        /// <summary>
        /// エラーを発行する。SlotManager・Factory 自己登録コードから呼び出す。
        /// Debug.LogError の出力は同一 (SlotId, Category) 組合せにつき初回 1F のみ行い、以降は抑制する。
        /// </summary>
        void Publish(SlotError error);
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

> **最終確定 (design フェーズ)**: 各発行主体と経路を以下に確定する。

| エラー発生箇所 | 発行主体 | 通知方法 |
|-------------|---------|---------|
| Slot 初期化失敗 | `SlotManager` | `AddSlotAsync` 内で例外をキャッチし、`ISlotErrorChannel.Publish(SlotError(slotId, InitFailure, ex, UtcNow))` を呼ぶ |
| Applier エラー | `SlotManager` | Applier は例外を throw するだけ。`SlotManager` が catch して FallbackBehavior 実行後に `ISlotErrorChannel.Publish(SlotError(slotId, ApplyFailure, ex, UtcNow))` を呼ぶ |
| VMC 受信エラー | `IMoCapSource` 具象実装 | 受信スレッドで発生した場合はメインスレッドに移行後に `RegistryLocator.ErrorChannel.Publish(...)` を呼ぶ |
| Registry 競合 | **呼び出し元 (Factory 自己登録コード)** | Registry の `Register()` は `RegistryConflictException` を throw するだけ。**Registry 自身は ErrorChannel に発行しない**。Factory の `RegisterRuntime()` / `RegisterEditor()` が try-catch で捕捉し、必要に応じて `RegistryLocator.ErrorChannel.Publish(SlotError("", RegistryConflict, ex, UtcNow))` を呼ぶ |

> **確定 (design フェーズ)**: `ISlotErrorChannel` のインスタンスは `RegistryLocator.ErrorChannel` 静的プロパティ経由で取得する。テスト時は `RegistryLocator.OverrideErrorChannel()` でモックを注入できる。

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
        /// 実装: アバター GameObject に紐付く全 Renderer コンポーネントの enabled を false にする。
        /// GameObject 自体は生存させる (SetActive(false) は使用しない)。
        /// エラー解消後の次フレーム正常 Apply 時に Renderer.enabled = true へ復元する。
        /// </summary>
        Hide,
    }
}
```

### SlotSettings.fallbackBehavior の位置付け

- `SlotSettings` の `fallbackBehavior` フィールド (1.1 章参照) で Slot ごとに個別設定する
- **デフォルト値: `FallbackBehavior.HoldLastPose`**
- フォールバック実行後、`SlotManager` は `ISlotErrorChannel` にエラーを通知する (1.7 章参照)
- フォールバック状態からの回復方法 (エラーが解消した場合の挙動) は tasks フェーズで詳細を確定する (Wave B との合意事項)

---

## 2. MoCap ソース抽象

### 2.1 `IMoCapSource` シグネチャ

> **最終仕様確定** (slot-core design フェーズ / Wave A 先行波)。以降は合意変更のみ。

**設計方針 (design フェーズ最終確定)**:
- Pull 型 (`FetchLatestMotion()`) を**廃止**し、Push 型 (`IObservable<MotionFrame>`) を採用する
- 受信全フレームを逃さず低レイテンシで処理するため UniRx `Subject` ベースのストリーミングを採用する
- **採用ライブラリ: UniRx (`com.neuecc.unirx`) ― R3 は採用しない**
- **非同期: UniTask (`com.cysharp.unitask`) を採用する。Task は採用しない**
- `UniRx` / `UniTask` は `RealtimeAvatarController.Core` アセンブリの依存として追加する
- 1 つの `IMoCapSource` インスタンスを複数 Slot で参照共有できる (ライフサイクルは `MoCapSourceRegistry` が管理する)

> **UniRx 採用理由 (dig ラウンド 2 / design フェーズ最終確定)**: UniRx (`com.neuecc.unirx`) を採用する。R3 は採用しない。UniRx の `IObservable<T>` は `System.IObservable<T>` を実装しているため、契約の型シグネチャは `System.IObservable<MotionFrame>` のままで変更不要である。NuGet 依存を持たないため UPM 配布での scoped registry が OpenUPM 1 個のみで済み、配布手続きが簡素化される。

> **UniTask 採用理由 (design フェーズ確定)**: `SlotManager.AddSlotAsync` は将来の Addressable Provider が非同期 Asset ロードを行うため非同期 API が必要。Unity `PlayerLoop` と統合された UniTask を採用することでメインスレッド復帰・キャンセル処理が簡潔に書ける。UniRx 導入時点で既に OpenUPM scoped registry は追加済みのため、新たなトレードオフは最小。

```csharp
// 最終シグネチャ (design フェーズ確定)
// using UniRx; が必要 (ObserveOnMainThread() は UniRx 拡張メソッド)
// IObservable<MotionFrame> は System.IObservable<T> であり、UniRx の Subject<T> はこれを実装する
public interface IMoCapSource : IDisposable
{
    /// <summary>ソース種別識別子 (例: "VMC", "Custom")</summary>
    string SourceType { get; }

    /// <summary>
    /// 初期化。通信パラメータを格納した Config を渡す。
    /// メインスレッドからの呼び出しを前提とする。
    /// </summary>
    void Initialize(MoCapSourceConfigBase config);

    /// <summary>
    /// Push 型モーションストリーム。受信スレッドから Subject.OnNext() で配信される。
    /// 購読側は .ObserveOnMainThread() でメインスレッドに同期すること。
    /// OnError は発行しない。エラーは内部処理しストリームを継続する。
    /// </summary>
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

> **最終仕様確定** (motion-pipeline design フェーズ / Wave B)。以降は合意変更のみ。
> 記入者: `motion-pipeline` エージェント (Wave B)。`slot-core` の 2.1 章と整合したうえで型骨格を確定。

#### 基底型: `MotionFrame` (最終確定)

全骨格形式 (Humanoid / Generic 等) の共通基底型。`IMoCapSource.MotionStream` (Push 型ストリーム) が流すフレーム型として採用する。**抽象クラス** を選定 (struct 案は不採用: 継承不可・ボックス化コスト等の理由)。

```csharp
// 最終シグネチャ (motion-pipeline design フェーズ確定)
namespace RealtimeAvatarController.Motion
{
    // 骨格種別識別子
    public enum SkeletonType
    {
        Humanoid,
        Generic,
    }

    // 全骨格形式共通の基底型 (抽象クラス)
    public abstract class MotionFrame
    {
        // 受信タイムスタンプ (Stopwatch ベース monotonic、double 秒単位)
        // 値: Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency で算出
        // 基準: App 起動時 (Stopwatch 起動基準の相対値)
        // 打刻タイミング: 受信ワーカースレッド上でフレーム構築時
        // 注意: プロセス間比較不可 (相対値のため)
        public double Timestamp { get; }

        // 将来の拡張フィールド (初期版では未実装)
        // ログ用途で wall clock が必要な場合に追加を検討する
        // public DateTime? WallClock { get; }

        // このフレームが表す骨格種別
        public abstract SkeletonType SkeletonType { get; }

        protected MotionFrame(double timestamp) { Timestamp = timestamp; }
    }
}
```

#### Humanoid 向け中立表現: `HumanoidMotionFrame` (最終確定 + M-3 合意変更 2026-04-22)

Unity `HumanPose` 相当の構造を持つ具象型。Muscle 値配列と Root の位置・回転を保持するイミュータブル sealed class。
M-3 合意変更で、VMC 等「ボーン回転クォータニオンを native 形式として出す」MoCap ソースの正確な変換経路を提供するため、`BoneLocalRotations` (親ローカル回転の辞書、任意) を追加した。

```csharp
// 最終シグネチャ (motion-pipeline design フェーズ確定 + M-3 合意変更)
namespace RealtimeAvatarController.Motion
{
    public sealed class HumanoidMotionFrame : MotionFrame
    {
        public override SkeletonType SkeletonType => SkeletonType.Humanoid;

        // Unity HumanPose.muscles 相当
        // 要素数: HumanTrait.MuscleCount = 95 (正常フレーム) / 0 (Muscles 未供給)
        // BoneLocalRotations 経路を使う MoCap ソースは空配列 (長さ 95 の 0 埋めでも可) を渡す
        public float[] Muscles { get; }

        // Root 位置 (HumanPose.bodyPosition 相当)
        public Vector3 RootPosition { get; }

        // Root 回転 (HumanPose.bodyRotation 相当)
        public Quaternion RootRotation { get; }

        // M-3 追加: 各ボーンの親ローカル座標系での回転 (optional)
        // VMC など「ボーン回転クォータニオンを native 形式として emit する」MoCap ソースが使用する。
        // null または Count == 0 の場合は従来の Muscles 経路でのみ適用する。
        // 非 null かつ Count > 0 の場合、Applier は MainThread で
        //   Animator.GetBoneTransform(bone).localRotation = rotation の直接書込
        // を行う (HumanPoseHandler は使用しない)。Muscles は無視される。
        // 【M-3 途中の実装方針撤回】 初期方針では "Transform 書込 → GetHumanPose で逆算 → SetHumanPose で
        // 再構築" の経路を想定したが、Humanoid Muscle の近似誤差でボーン角度がずれる問題が発生したため、
        // Transform.localRotation を直接書き込む方針 (EVMC4U など業界標準) に変更した (2026-04-22)。
        public IReadOnlyDictionary<HumanBodyBones, Quaternion> BoneLocalRotations { get; }

        // true: 有効フレーム (Muscles が有効 or BoneLocalRotations が有効)
        // false: 両方とも空 (データなし / 初期化前)
        public bool IsValid
            => Muscles.Length > 0
               || (BoneLocalRotations != null && BoneLocalRotations.Count > 0);

        // 既存コンストラクタ (互換維持: BoneLocalRotations は null)
        public HumanoidMotionFrame(double timestamp, float[] muscles,
            Vector3 rootPosition, Quaternion rootRotation)
            : this(timestamp, muscles, rootPosition, rootRotation, null) { }

        // M-3 追加: BoneLocalRotations 対応コンストラクタ
        public HumanoidMotionFrame(double timestamp, float[] muscles,
            Vector3 rootPosition, Quaternion rootRotation,
            IReadOnlyDictionary<HumanBodyBones, Quaternion> boneLocalRotations) : base(timestamp)
        {
            Muscles = muscles ?? Array.Empty<float>();
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            BoneLocalRotations = boneLocalRotations;
        }

        public static HumanoidMotionFrame CreateInvalid(double timestamp)
            => new HumanoidMotionFrame(timestamp, Array.Empty<float>(), Vector3.zero, Quaternion.identity);
    }
}
```

**補足**:
- `Muscles.Length == 0` かつ `BoneLocalRotations` が null / Count == 0 の場合のみ「データなし / 初期化前」を示す無効フレームとなる
- 無効フレームは通常動作扱いであり、`ISlotErrorChannel` へのエラー発行は行わない
- 全プロパティは readonly。コンストラクタで完全初期化するイミュータブル設計
- `BoneLocalRotations` は読み取り専用辞書 (`IReadOnlyDictionary<HumanBodyBones, Quaternion>`) 型で渡す。呼び出し元は Frame 生成後に辞書を変更してはならない

**M-3 合意変更の経緯** (2026-04-22):
- VMC プロトコルは `/VMC/Ext/Bone/Pos` で「各ボーンの親ローカル回転クォータニオン」を送信する
- Unity の Muscle 値は「ボーンごとに固有の軸で正規化された 3 DoF 値」であり、単純な Euler 角からの変換は意味的に不正確 (各ボーンの muscle axis / rest pose を考慮しないため)
- 既存実装 (`VmcFrameBuilder.WriteBoneMuscles`) は `Quaternion.eulerAngles / 180f` で muscle を作成しており、Hips (Root 経路) 以外のボーンが実質ほぼゼロ値となって動かない実害が発生
- 解決策として `BoneLocalRotations` フィールドを追加し、変換責務をワーカースレッド (`VmcFrameBuilder`) → MainThread (`HumanoidMotionApplier`) に移動することで合意

**M-3 実装方針の途中修正** (2026-04-22 同日):
- 初期実装: MainThread で `Transform.localRotation` へ VMC rotation を書込 → `HumanPoseHandler.GetHumanPose` で Muscle 逆算 → `SetHumanPose` で再構築
- 問題: GetHumanPose は Humanoid rig の constraint を通して「近似 muscle」を返すため、書き込んだ rotation と SetHumanPose 後のボーン姿勢が一致しない。さらに GetHumanPose の内部キャッシュで更新頻度が落ち、数十秒に 1 回しか反映されない現象が発生
- 撤回後の方針: `Animator.GetBoneTransform(bone).localRotation = rotation` の直接書込。HumanPoseHandler / Muscle パスは BoneLocalRotations 経路では完全にバイパスする。EVMC4U などの業界標準実装と同じ方式
- 将来の blending / retargeting 要求は、BoneLocalRotations 辞書のレベルで `Quaternion.Slerp` を適用する別経路で対応する (Muscle pipeline への統合にはこだわらない)

#### Generic 向け中立表現 (初期段階: 抽象のみ)

初期段階では具象型は定義しない。将来の Generic 具象実装のためにプレースホルダーとして以下の方針のみを合意する。

```csharp
// 将来実装向けプレースホルダー (初期段階では実装しない)
namespace RealtimeAvatarController.Motion
{
    // Generic 骨格向けモーションフレーム抽象クラス
    // 具象実装は将来の Generic Spec が担う
    public abstract class GenericMotionFrame : MotionFrame
    {
        public override SkeletonType SkeletonType => SkeletonType.Generic;
        protected GenericMotionFrame(double timestamp) : base(timestamp) { }

        // 将来実装予定:
        // public TransformData[] Bones { get; }  // 各ボーンの位置・回転・スケール
    }
}
```

#### `IMoCapSource.MotionStream` のフレーム型方針 (最終確定)

- **採用方針**: `IObservable<MotionFrame>` のフレーム型は `MotionFrame` 基底型を使用する (`IObservable<MotionFrame> MotionStream { get; }`)
- 購読側は `MotionFrame.SkeletonType` を確認してキャストする (`HumanoidMotionFrame` への `as` キャスト)
- ジェネリクス型パラメータ (`IMoCapSource<TFrame>`) は採用しない

#### タイムスタンプ仕様 (最終確定)

| 項目 | 内容 |
|------|------|
| 型 | `double` (秒単位) |
| 基準 | App 起動時 (Stopwatch 起動基準の相対値) |
| 取得式 | `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` |
| 打刻タイミング | 受信ワーカースレッド上でフレーム構築時 (Unity メインスレッド API 不使用のため安全) |
| 用途 | Slot 内フレーム順序整列 / 遅延計測 / デバッグログ |
| プロセス間比較 | **不可** (相対値のため異なるプロセスとの比較は意味を持たない) |
| 将来拡張 | ログ用途で wall clock が必要な場合は `WallClock: DateTime?` フィールドの追加を検討する。初期版では **未実装** とする |

#### スレッド安全性の要求 (最終確定 / M-3 追補 2026-04-22)

- **書き込み**: MoCap ソース実装が使用するスレッドから `Interlocked.Exchange` によりアトミックに `MotionCache._latestFrame` を更新する。`Timestamp` の打刻は書込スレッド上で行い、Unity API は使用しない
- **読み込み**: Unity メインスレッド (`LateUpdate` 等) から `Volatile.Read` で最新フレームを読み取る
- **選定方式**: 方式 B (ソーススレッド直接書込 / `Interlocked.Exchange`) を採用。方式 A (`ObserveOnMainThread()` 経由) は高頻度フレームでキュー蓄積が生じるため不採用
- **M-3 追補**: MoCap ソース実装が `MotionStream.OnNext` をどのスレッドから呼ぶかは実装依存である。VMC (`mocap-vmc`) は uOSC の `onDataReceived` が Unity MainThread で Invoke されるため MainThread で OnNext する。その場合でも `Interlocked.Exchange` / `Volatile.Read` の要件は過剰同期として残すだけでよく、コントラクトは変わらない

---

## 3. アバター供給抽象

### 3.1 `IAvatarProvider` シグネチャ

> **最終仕様確定** (slot-core design フェーズ / Wave A 先行波)。以降は合意変更のみ。
> 非同期 API の戻り値型は **UniTask** に確定 (Task は採用しない)。

```csharp
// 最終シグネチャ (design フェーズ確定)
// using Cysharp.Threading.Tasks; が必要 (UniTask は UniTask パッケージが提供)
public interface IAvatarProvider : IDisposable
{
    /// <summary>Provider 種別識別子 (例: "Builtin", "Addressable")</summary>
    string ProviderType { get; }

    /// <summary>
    /// アバターを同期的に要求する。
    /// 非同期 Provider では NotSupportedException をスローしてよい。
    /// </summary>
    GameObject RequestAvatar(ProviderConfigBase config);

    /// <summary>
    /// アバターを非同期に要求する。UniTask を採用 (Task ではない)。
    /// 同期 Provider は同期完了の UniTask を返してよい。
    /// </summary>
    UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default);

    /// <summary>供給したアバターを解放する。</summary>
    void ReleaseAvatar(GameObject avatar);
}
```

**同期 / 非同期の許容方針**:
- `IAvatarProvider` は同期・非同期のいずれの具象実装も許容する
- ビルトイン Provider (初期段階) は同期版を実装し、`RequestAvatarAsync` は `UniTask.FromResult` で同期完了を返す
- Addressable Provider (将来) は非同期版を実装し、`RequestAvatar` は `NotSupportedException` でも可
- **UniTask を確定** (design フェーズ決定。Task / ValueTask は採用しない)

### 3.2 Addressable 拡張余地

- 初期段階で Addressable Provider は実装しない
- `IAvatarProvider` は同期・非同期のいずれの具象実装も許容するシグネチャとする

---

## 4. 表情制御抽象 (受け口のみ)

### 4.1 `IFacialController` シグネチャ

> **最終仕様確定** (slot-core design フェーズ / Wave A)。初期段階では具象実装なし。受け口のみ。
> 引数 `facialData` の型は将来の具象実装フェーズで `object` から具象型に更新する。

```csharp
// 最終シグネチャ (design フェーズ確定 / 受け口のみ)
public interface IFacialController : IDisposable
{
    /// <summary>初期化。制御対象アバターの GameObject を受け取る。</summary>
    void Initialize(GameObject avatarRoot);

    /// <summary>
    /// 表情データを適用する。
    /// 引数型 FacialData は将来の具象実装フェーズで確定する。初期段階では object 型を使用する。
    /// </summary>
    void ApplyFacialData(object facialData);

    /// <summary>シャットダウン。IDisposable.Dispose() と等価。</summary>
    void Shutdown();
}
```

### 4.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。`facialData` の具象型は将来担当 Spec が確定する。

---

## 5. リップシンク抽象 (受け口のみ)

### 5.1 `ILipSyncSource` シグネチャ

> **最終仕様確定** (slot-core design フェーズ / Wave A)。初期段階では具象実装なし。受け口のみ。
> `FetchLatestLipSync()` の戻り値型は将来の具象実装フェーズで `object` から具象型に更新する。

```csharp
// 最終シグネチャ (design フェーズ確定 / 受け口のみ)
public interface ILipSyncSource : IDisposable
{
    /// <summary>初期化。</summary>
    void Initialize(LipSyncSourceConfigBase config);

    /// <summary>
    /// 最新のリップシンクデータを取得する (Pull 型)。
    /// 戻り値型は将来の具象実装フェーズで確定する (母音ブレンドシェイプ値配列等を想定)。
    /// 初期段階では object 型を使用する。
    /// </summary>
    object FetchLatestLipSync();

    /// <summary>シャットダウン。IDisposable.Dispose() と等価。</summary>
    void Shutdown();
}
```

### 5.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。`FetchLatestLipSync()` の戻り値具象型は将来担当 Spec が確定する。

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

#### テスト専用 asmdef (Tests.EditMode / Tests.PlayMode)

> **dig ラウンド 4 確定 (案 C 採用)**

各機能 Spec に EditMode / PlayMode の 2 系統のテスト asmdef を用意する。**slot-core / motion-pipeline / mocap-vmc / avatar-provider-builtin の 4 Spec は必須**。ui-sample は運用判断により任意とする。

| asmdef 名 | 担当 Spec | 配置パス (パッケージルート相対) | 必須 / 任意 |
|-----------|----------|-------------------------------|:-----------:|
| `RealtimeAvatarController.Core.Tests.EditMode` | slot-core | `Tests/EditMode/slot-core/` | 必須 |
| `RealtimeAvatarController.Core.Tests.PlayMode` | slot-core | `Tests/PlayMode/slot-core/` | 必須 |
| `RealtimeAvatarController.Motion.Tests.EditMode` | motion-pipeline | `Tests/EditMode/motion-pipeline/` | 必須 |
| `RealtimeAvatarController.Motion.Tests.PlayMode` | motion-pipeline | `Tests/PlayMode/motion-pipeline/` | 必須 |
| `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` | mocap-vmc | `Tests/EditMode/mocap-vmc/` | 必須 |
| `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` | mocap-vmc | `Tests/PlayMode/mocap-vmc/` | 必須 |
| `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` | avatar-provider-builtin | `Tests/EditMode/avatar-provider-builtin/` | 必須 |
| `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` | avatar-provider-builtin | `Tests/PlayMode/avatar-provider-builtin/` | 必須 |
| `RealtimeAvatarController.Samples.UI.Tests.EditMode` | ui-sample | `Tests/EditMode/ui-sample/` | 任意 |
| `RealtimeAvatarController.Samples.UI.Tests.PlayMode` | ui-sample | `Tests/PlayMode/ui-sample/` | 任意 |

**テスト asmdef の命名規約**:
- 形式: `<RuntimeAsmdefName>.Tests.EditMode` / `<RuntimeAsmdefName>.Tests.PlayMode`
- 例: `RealtimeAvatarController.Core` の EditMode テスト → `RealtimeAvatarController.Core.Tests.EditMode`

**テスト asmdef の設定ルール**:
- `optionalUnityReferences: ["TestAssemblies"]` を必ず設定し、Unity Test Runner (NUnit) への参照を有効化する
- `includePlatforms: []` (空配列) とする。全プラットフォームを対象とし、EditMode / PlayMode の区別は Unity Test Runner が制御する
- 対応する Runtime asmdef **のみ**を `references` に追加する片方向依存とする
- テスト asmdef 間の相互参照は禁止する (Runtime → Tests の逆参照も禁止)
- `RegistryLocator.ResetForTest()` を各テストのセットアップ / ティアダウンフェーズで呼び出し、Domain Reload OFF 環境下での二重登録を防止すること

> **カバレッジ目標**: 初期版では定量カバレッジ目標を設定しない。design / tasks フェーズで改めて検討する。

**依存方向の制約 (Runtime)**:
- `Samples.UI` → 機能部アセンブリ各種 (一方向のみ)
- 機能部アセンブリは `Samples.UI` を参照しない
- UI フレームワーク (UGUI / UIToolkit 等) への依存は `Samples.UI` にのみ許容する

**外部ライブラリ依存 (UniRx / UniTask)**:

> **project-foundation design フェーズ最終確定 (Wave B)**: UniTask (`com.cysharp.unitask`) を `package.json` の `dependencies` に追加し、`RealtimeAvatarController.Core` の asmdef `references` に `UniRx` と `UniTask` の両方を追加することを確定した。バージョンは `com.neuecc.unirx: 7.1.0` / `com.cysharp.unitask: 2.5.10`。

- `RealtimeAvatarController.Core` は UniRx (`com.neuecc.unirx`) の asmdef (`UniRx`) および UniTask (`com.cysharp.unitask`) の asmdef (`UniTask`) を `references` に追加し、`IObservable<T>` 拡張メソッド・`Subject<T>`・`UniTask<T>` 等を直接利用する
- `RealtimeAvatarController.Motion`・`RealtimeAvatarController.MoCap.VMC`・`RealtimeAvatarController.Avatar.Builtin` は UniRx / UniTask の asmdef を直接 `references` に持たず、`RealtimeAvatarController.Core` 経由で型を間接利用する (二重依存禁止)
- ただし各アセンブリが UniRx の拡張メソッド (`ObserveOnMainThread()` 等) を直接呼び出す技術的必要が生じた場合は、design フェーズで要否を個別判断し本章に追記する
- **原則**: `RealtimeAvatarController.Samples.UI` は UniRx / UniTask への直接依存を持たず、機能部 API 経由で利用する
- **例外 (design フェーズ確定)**: `RealtimeAvatarController.Samples.UI` は `ISlotErrorChannel.Errors` の `.ObserveOnMainThread()` 拡張メソッドを直接呼び出すため、**UniRx の直接参照を例外的に許容する**。具体的には `Samples.UI` の asmdef `references` に `UniRx` を追加する。UniTask の `Samples.UI` 直接参照は現時点で技術的必要がないため不要 (必要になった場合は改めて本章に追記する)。
- **package.json の dependencies (project-foundation design フェーズ確定 / com.hidano.uosc 追加)**:
  ```json
  "dependencies": {
    "com.neuecc.unirx": "7.1.0",
    "com.cysharp.unitask": "2.5.10",
    "com.hidano.uosc": "1.0.0"
  }
  ```
- **scoped registry (利用者の manifest.json へ追加必須)**:
  - **OpenUPM**: `https://package.openupm.com` — scopes: `com.neuecc`, `com.cysharp`
  - **npm (hidano)**: `https://registry.npmjs.com` — scopes: `com.hidano`
  - 両レジストリの追加が必須。`com.hidano.uosc` (OSC ライブラリ、MIT、`SO_REUSEADDR` 有効版) は npm scoped registry から配布される。

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
