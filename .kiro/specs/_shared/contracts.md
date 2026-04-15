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
| `weight` | `float` | 必須 | モーション合成ウェイト (0.0〜1.0、範囲外はクランプ) |
| `avatarProviderDescriptor` | `AvatarProviderDescriptor` | 必須 | アバター供給元の Descriptor (typeId + config) |
| `moCapSourceDescriptor` | `MoCapSourceDescriptor` | 必須 | MoCap ソースの Descriptor (typeId + config) |
| `facialControllerDescriptor` | `FacialControllerDescriptor?` | 省略可 (null 許容) | 表情制御の Descriptor |
| `lipSyncSourceDescriptor` | `LipSyncSourceDescriptor?` | 省略可 (null 許容) | リップシンクソースの Descriptor |

**Descriptor 骨格 (C# 疑似コード)**:

```csharp
// 各 Descriptor は typed POCO としてシリアライズ可能
[Serializable]
public class AvatarProviderDescriptor
{
    // Registry に登録された具象型を識別するキー (例: "Builtin", "Addressable")
    public string ProviderTypeId;

    // 具象型ごとのコンフィグ (ScriptableObject サブクラスまたは JSON 文字列)
    // design フェーズで具体化
    public ScriptableObject Config; // または public string ConfigJson;
}

[Serializable]
public class MoCapSourceDescriptor
{
    // Registry に登録された具象型を識別するキー (例: "VMC", "Custom")
    public string SourceTypeId;

    // 具象型ごとのコンフィグ
    public ScriptableObject Config; // または public string ConfigJson;
}

// FacialControllerDescriptor / LipSyncSourceDescriptor も同様の構造
```

> **設計の意図**: インターフェース型フィールド (`IAvatarProvider` 等) を Unity シリアライズに直接配置することはできない。Descriptor パターンにより、具象型の選択をランタイムの Registry/Factory 解決に委ねる。利用可能な具象型はランタイムのプロジェクト構成に応じて動的に決まるため、エディタ UI も Registry から候補を列挙して表示する。

### 1.2 シリアライズ形式

以下の保持形式をすべて許容する設計方針を採用する:

| 形式 | 用途 | 備考 |
|------|------|------|
| Unity `ScriptableObject` | エディタプロジェクト標準 | `SlotSettings` が SO を継承する実装を推奨するが必須ではない |
| POCO (純 C# オブジェクト) | ランタイム生成・テスト | `[Serializable]` 属性付与で Unity シリアライズ対象にできる |
| JSON | ファイル保存・外部連携 | `JsonUtility` または Newtonsoft.Json によるエクスポート / インポート |

- **Descriptor フィールドはシリアライズの中核**: `AvatarProviderDescriptor` / `MoCapSourceDescriptor` 等は `[Serializable]` POCO として定義し、インターフェース直参照を避ける
- **ScriptableObject は任意**: エディタとの統合性を重視する場合は SO を継承してよいが、ランタイム生成やユニットテストでは POCO のまま使用できる
- **具象型依存の分離**: `SlotSettings` 自体は具象型 (`VMCMoCapSource` 等) を知らない。型解決は Registry/Factory が担う

### 1.3 ライフサイクル

| 状態 | 説明 |
|------|------|
| `Created` | `SlotRegistry.AddSlot()` 呼び出し後、リソース未初期化 |
| `Active` | `SlotManager` が `IAvatarProvider` の初期化を完了し、動作中 |
| `Inactive` | リソースを保持したまま一時停止中 (再アクティブ化可能) |
| `Disposed` | `SlotRegistry.RemoveSlot()` 呼び出し後、全リソース解放済み |

- **IAvatarProvider のリソース所有**: `SlotManager` が各 Slot に紐付く `IAvatarProvider` のライフサイクルを管理し、初期化・解放を制御する
- **IMoCapSource のリソース所有 (重要)**: `IMoCapSource` のライフサイクル所有は `SlotManager` ではなく **`MoCapSourceRegistry`** が担う。複数 Slot が同一 `IMoCapSource` インスタンスを参照共有できるため、Slot の破棄は `IMoCapSource` の即時解放を意味しない。`MoCapSourceRegistry` が参照管理し、不要になった時点で解放する
- **破棄タイミング**: `SlotRegistry.RemoveSlot()` 呼び出し時、または `SlotManager` の `Dispose()` 時に全 Slot を一括破棄する (ただし `IMoCapSource` の解放は `MoCapSourceRegistry` 経由)
- **エラー処理**: 破棄中の例外はキャッチしてログ記録し、残余リソースの解放を継続する

---

## 1.4 ProviderRegistry / SourceRegistry 契約

> **記入者**: `slot-core` エージェント (Wave 1)

型 ID による Factory 解決と、エディタ UI 向け候補列挙を担う Registry 群を定義する。

### Registry 骨格 (C# 疑似コード)

```csharp
namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 具象型の登録・解決・候補列挙を担う Registry。
    /// 起動時に属性スキャン / DI / 手動登録によってエントリを追加する。
    /// 登録方式の詳細は design フェーズで確定。
    /// </summary>
    public interface IProviderRegistry
    {
        /// <summary>
        /// typeId を持つ Factory を登録する。
        /// </summary>
        void Register(string providerTypeId, IAvatarProviderFactory factory);

        /// <summary>
        /// Descriptor から IAvatarProvider インスタンスを生成する。
        /// 未登録 typeId の場合は例外またはエラー値を返す。
        /// </summary>
        IAvatarProvider Resolve(AvatarProviderDescriptor descriptor);

        /// <summary>
        /// 登録済みの providerTypeId 一覧を返す (エディタ UI 向け候補列挙)。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }

    /// <summary>
    /// IMoCapSource 具象型の登録・解決・候補列挙・参照共有を担う Registry。
    /// </summary>
    public interface IMoCapSourceRegistry
    {
        /// <summary>
        /// typeId を持つ Factory を登録する。
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
    public interface IAvatarProviderFactory
    {
        IAvatarProvider Create(ScriptableObject config); // 引数型は design フェーズで確定
    }

    public interface IMoCapSourceFactory
    {
        IMoCapSource Create(ScriptableObject config); // 引数型は design フェーズで確定
    }
}
```

### エントリ登録方式 (方針のみ / design フェーズで確定)

| 方式 | 概要 |
|------|------|
| 属性スキャン | `[RegisterAvatarProvider("Builtin")]` 等のカスタム属性を持つ型をアセンブリスキャンで自動登録 |
| DI コンテナ | `IProviderRegistry.Register()` を明示呼び出しで登録 |
| 手動登録 | 起動エントリポイントで直接 `Register()` を呼び出す |

> **注意**: エントリ登録方式は design フェーズで選択する。`slot-core` の実装フェーズでは、少なくとも手動登録方式が動作すれば十分である。

---

## 2. MoCap ソース抽象

### 2.1 `IMoCapSource` シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格・契約のみ定義する。

**設計方針の変更 (dig ラウンド 1 反映)**:
- Pull 型 (`FetchLatestMotion()`) を**廃止**し、Push 型 (`IObservable<MotionFrame>`) を採用する
- 受信全フレームを逃さず低レイテンシで処理するため UniRx `Subject` ベースのストリーミングを採用する
- `UniRx` は `RealtimeAvatarController.Core` アセンブリの依存として追加する
- 1 つの `IMoCapSource` インスタンスを複数 Slot で参照共有できる (ライフサイクルは `MoCapSourceRegistry` が管理する)

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
// UniRx (UniRx.IObservable<T>) を依存として使用
public interface IMoCapSource : IDisposable
{
    // ソース種別識別子 (例: "VMC", "Custom" 等)
    string SourceType { get; }

    // 初期化: 通信パラメータ (ポート番号等) を受け取る
    // 引数型は design フェーズで確定
    void Initialize(/* MoCapSourceConfig config */);

    // Push 型モーションストリーム (UniRx IObservable)
    // 受信スレッドが Subject.OnNext() を呼び出す; 購読側は ObserveOnMainThread() で Unity メインスレッドに同期する
    // 戻り値の MotionFrame は motion-pipeline が定義するモーションデータ中立表現 (2.2 章) に準拠
    IObservable<MotionFrame> MotionStream { get; }

    // 破棄 (IDisposable.Dispose() で代替可)
    void Shutdown();
}
```

**スレッド安全性の要求**:
- `MotionStream` への `OnNext()` 呼び出しは受信スレッドから行われる
- 購読側 (Slot / Pipeline) は `.ObserveOnMainThread()` を使用して Unity メインスレッドで処理すること
- `Initialize()` / `Shutdown()` はメインスレッドからの呼び出しを前提とする
- `Subject<MotionFrame>` への `OnNext()` は UniRx の既定ではスレッドセーフではないため、具象実装は `Subject` のスレッドセーフラッパー (`Subject.Synchronize()` 等) または `SerialDisposable` + `lock` を使用すること (詳細は design フェーズで確定)

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

| asmdef 名 | 担当 Spec | 配置パス (パッケージルート相対) | 備考 |
|-----------|----------|-------------------------------|------|
| `RealtimeAvatarController.Core` | slot-core | `Runtime/Core/` | Slot 抽象・各公開インターフェース群 |
| `RealtimeAvatarController.Motion` | motion-pipeline | `Runtime/Motion/` | モーションデータ中立表現・パイプライン |
| `RealtimeAvatarController.MoCap.VMC` | mocap-vmc | `Runtime/MoCap/VMC/` | VMC OSC 受信具象実装 |
| `RealtimeAvatarController.Avatar.Builtin` | avatar-provider-builtin | `Runtime/Avatar/Builtin/` | ビルトインアバター供給具象実装 |
| `RealtimeAvatarController.Samples.UI` | ui-sample | `Samples~/UI/` | UI サンプル (Samples~ 機構) |

**依存方向の制約**:
- `Samples.UI` → 機能部アセンブリ各種 (一方向のみ)
- 機能部アセンブリは `Samples.UI` を参照しない
- UI フレームワーク (UGUI / UIToolkit 等) への依存は `Samples.UI` にのみ許容する

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
