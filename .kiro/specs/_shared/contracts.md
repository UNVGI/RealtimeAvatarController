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

Slot は VTuber アバター制御の設定単位。`SlotSettings` を ScriptableObject として定義し、Unity 標準シリアライズで保存・復元を可能にする。

### 1.1 保持する設定項目

| フィールド | 型 | 必須/省略可 | 説明 |
|-----------|---|:---------:|------|
| `slotId` | `string` | 必須 | Slot を一意に識別する主キー |
| `displayName` | `string` | 必須 | エディタ・UI 向け表示名 |
| `avatarProvider` | `IAvatarProvider` | 必須 | 紐付けアバター供給元 (抽象参照) |
| `moCapSource` | `IMoCapSource` | 必須 | 紐付け MoCap ソース (抽象参照) |
| `facialController` | `IFacialController` | 省略可 (null 許容) | 紐付け表情制御 (抽象参照) |
| `lipSyncSource` | `ILipSyncSource` | 省略可 (null 許容) | 紐付けリップシンクソース (抽象参照) |
| `weight` | `float` | 必須 | モーション合成ウェイト (0.0〜1.0、範囲外はクランプ) |

### 1.2 シリアライズ形式

- **採用**: Unity `ScriptableObject`
  - `SlotSettings` は `ScriptableObject` を継承し、Unity エディタでアセット (.asset) として保存・管理する
  - インターフェース参照フィールドはシリアライズ可能な形式 (アセット参照または型名文字列) で保持する
  - JSON エクスポート / インポートの拡張余地を設計上確保する (design フェーズで具体化)
- **不採用の理由**:
  - MonoBehaviour は Scene に依存するためランタイム外での再利用性が低い
  - 純 JSON は Unity エディタとの統合性が低い (ただし将来の拡張として許容)

### 1.3 ライフサイクル

| 状態 | 説明 |
|------|------|
| `Created` | `SlotRegistry.AddSlot()` 呼び出し後、リソース未初期化 |
| `Active` | `SlotManager` が `IAvatarProvider`・`IMoCapSource` の初期化を完了し、動作中 |
| `Inactive` | リソースを保持したまま一時停止中 (再アクティブ化可能) |
| `Disposed` | `SlotRegistry.RemoveSlot()` 呼び出し後、全リソース解放済み |

- **リソース所有**: `SlotManager` が Slot に紐付くリソースのライフサイクルを管理し、`IAvatarProvider` / `IMoCapSource` の初期化・解放を制御する
- **破棄タイミング**: `SlotRegistry.RemoveSlot()` 呼び出し時、または `SlotManager` の `Dispose()` 時に全 Slot を一括破棄する
- **エラー処理**: 破棄中の例外はキャッチしてログ記録し、残余リソースの解放を継続する

---

## 2. MoCap ソース抽象

### 2.1 `IMoCapSource` (仮) シグネチャ

> **注意**: 具体的な引数型・戻り値型はすべて design フェーズで確定する。ここでは骨格・契約のみ定義する。

```csharp
// 骨格 (C# 疑似コード / 型名は仮)
public interface IMoCapSource : IDisposable
{
    // ソース種別識別子 (例: "VMC", "Custom" 等)
    string SourceType { get; }

    // 初期化: 通信パラメータ (ポート番号等) を Slot 単位で受け取る
    // 引数型は design フェーズで確定
    void Initialize(/* MoCapSourceConfig config */);

    // モーションデータ取得 (pull 型を基本とするが push / イベント型も design フェーズで検討)
    // 戻り値型は motion-pipeline が定義するモーションデータ中立表現 (2.2 章) に依存
    /* MotionData */ object FetchLatestMotion();

    // 破棄 (IDisposable.Dispose() で代替可)
    void Shutdown();
}
```

**スレッド安全性の要求**:
- `FetchLatestMotion()` はメインスレッド以外 (受信スレッド等) からも呼び出される可能性がある
- 具象実装はスレッドセーフなデータ交換バッファ (例: ロックレスキュー) を提供すること
- `Initialize()` / `Shutdown()` はメインスレッドからの呼び出しを前提とする

### 2.2 モーションデータ中立表現

> **記入者**: `motion-pipeline` エージェント (Wave 2)。`slot-core` の 2.1 章と整合したうえで型骨格を確定。

#### 基底型: `MotionFrame`

全骨格形式 (Humanoid / Generic 等) の共通基底型。`IMoCapSource.FetchLatestMotion()` の戻り値型として採用する。

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

#### `IMoCapSource.FetchLatestMotion()` 戻り値型の方針

- **採用方針**: 戻り値型は `MotionFrame` 基底型を使用する
- 呼び出し側は `MotionFrame.SkeletonType` を確認してキャストする、またはジェネリクス (`IMoCapSource<TFrame>`) を採用する。最終シグネチャは design フェーズで確定する
- 例示 (仮): `MotionFrame FetchLatestMotion();`

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
