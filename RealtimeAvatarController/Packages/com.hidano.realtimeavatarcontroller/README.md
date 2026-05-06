# Realtime Avatar Controller

VTuber ユースケース向けのランタイムアバターコントローラーです。Slot ベースの MoCap ソース管理、アバタープロバイダ抽象化、モーションパイプラインを Unity 向けに提供します。

> 本パッケージは UNVGI 社内利用向けに UPM 配布されています (パッケージ名: `jp.co.unvgi.realtimeavatarcontroller`)。配信レジストリは社内 npm (`https://npm.unvgi.com/`) です。

## 前提条件

- **Unity 6000.3.10f1** 以降
- 社内 npm レジストリ (`https://npm.unvgi.com/`) へのアクセス
- **OpenUPM CLI** (任意 — 手動 `manifest.json` 編集でも可)

## インストール

### Step 1: scoped registry の追加

Unity プロジェクトの `Packages/manifest.json` を開き、`scopedRegistries` セクションに以下の **3 つのレジストリ**を追加してください。すべての追加が必須です。

```json
{
  "scopedRegistries": [
    {
      "name": "UNVGI",
      "url": "https://npm.unvgi.com/",
      "scopes": [
        "jp.co.unvgi"
      ]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.neuecc",
        "com.cysharp"
      ]
    },
    {
      "name": "npm (hidano)",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "com.hidano"
      ]
    }
  ]
}
```

> **既存の `scopedRegistries` がある場合**: 上記 3 つのオブジェクトを配列に**追記**してください。既存エントリは削除しないでください。
>
> - **UNVGI** (`https://npm.unvgi.com/`): 本パッケージ (`jp.co.unvgi.realtimeavatarcontroller`) の取得に使用します。
> - **OpenUPM** (`https://package.openupm.com`): UniRx (`com.neuecc`) および UniTask (`com.cysharp`) の取得に使用します。
> - **npm (hidano)** (`https://registry.npmjs.com`): OSC ライブラリ `com.hidano.uosc` の取得に使用します。

### Step 2: dependencies への追加

同じ `manifest.json` の `dependencies` セクションに以下を追加してください。

```json
{
  "dependencies": {
    "jp.co.unvgi.realtimeavatarcontroller": "0.1.0"
  }
}
```

### Step 3: manifest.json 完全スニペット例 (新規プロジェクト向け)

新規プロジェクトで最初から導入する場合は、以下の完全な `manifest.json` を参考にしてください。

```json
{
  "scopedRegistries": [
    {
      "name": "UNVGI",
      "url": "https://npm.unvgi.com/",
      "scopes": [
        "jp.co.unvgi"
      ]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.neuecc",
        "com.cysharp"
      ]
    },
    {
      "name": "npm (hidano)",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "com.hidano"
      ]
    }
  ],
  "dependencies": {
    "jp.co.unvgi.realtimeavatarcontroller": "0.1.0",
    "com.unity.ugui": "2.0.0"
  }
}
```

> `com.unity.ugui` は例示用です。実際のプロジェクト依存は適宜追加してください。`jp.co.unvgi` / `com.neuecc` / `com.cysharp` / `com.hidano` の各スコープと対応レジストリは依存解決のためいずれも必須です。

### Step 4: Package Manager UI での確認

1. Unity エディタの **Window > Package Manager** を開く
2. 左上のドロップダウンから **My Registries** を選択
3. `Realtime Avatar Controller` が表示されることを確認
4. **Install** ボタンをクリック (Step 2 で追記済みの場合はすでにインストール済みと表示されます)

### Step 5: UI サンプルのインポート

1. Package Manager の `Realtime Avatar Controller` エントリを選択
2. 右側の **Samples** セクションを展開
3. **UI Sample** の横にある **Import** ボタンをクリック
4. `Assets/Samples/Realtime Avatar Controller/<version>/UI/` にサンプルがコピーされます
5. `SampleScene.unity` を開いてデモを確認してください

## 補足: openupm-cli を使う場合 (任意)

```bash
# プロジェクトルートで実行
openupm add jp.co.unvgi.realtimeavatarcontroller
```

`openupm-cli` は scoped registry の追加と `dependencies` への追記を自動で行います。UniRx・UniTask も依存として自動解決されます。

> **注意**: `openupm-cli` は OpenUPM レジストリの scoped registry のみを自動追加します。本パッケージ取得用の **UNVGI registry** および `com.hidano.uosc` 取得用の **npm (hidano) registry** は **手動で追加**する必要があります。Step 1 のスニペットを参照してください。

## 補足: git URL によるインストール (任意)

scoped registry を使わずに git URL で直接インストールする場合は、`?path=` パラメータでパッケージパスを指定してください。

`Packages/manifest.json` の `dependencies` に以下のように記述します。

```json
{
  "dependencies": {
    "jp.co.unvgi.realtimeavatarcontroller": "https://github.com/Hidano-Dev/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller"
  }
}
```

> **注意 1**: パッケージ ID は `jp.co.unvgi.realtimeavatarcontroller` ですが、リポジトリ内のディレクトリ名は移行前の名残により `com.hidano.realtimeavatarcontroller` のままです。`?path=` には実ディレクトリ名を指定してください。
>
> **注意 2**: git URL インストールでは Unity が依存パッケージ (`com.neuecc.unirx`・`com.cysharp.unitask`・`com.hidano.uosc`) を自動解決しません。Step 1 の scoped registry 追加と各依存パッケージの手動追加が別途必要になります。通常のインストールには **scoped registry 方式 (Step 1〜3) を推奨**します。

## バージョン固定ポリシー

本パッケージは全依存パッケージを **exact version** で固定し、再現性を優先しています。

| パッケージ | バージョン | 取得元 |
|------------|-----------|--------|
| `com.neuecc.unirx` | `7.1.0` | OpenUPM |
| `com.cysharp.unitask` | `2.5.10` | OpenUPM |
| `com.hidano.uosc` | `1.0.0` | npm (hidano) |

### アップグレード方針

- **パッケージ管理者**: セキュリティ修正等が必要な場合は `package.json` の `dependencies` を更新してリリースします。
- **利用者が個別にバージョンを変更する場合**: 自プロジェクトの `Packages/manifest.json` の `dependencies` に対象パッケージとバージョンを直接記述することで、`package.json` の宣言を上書きできます。

```json
{
  "dependencies": {
    "com.neuecc.unirx": "7.2.0"
  }
}
```

## アーキテクチャ

本パッケージは VTuber システムを構築するエンジニアが MoCap source / Avatar provider / Facial controller / LipSync source などを差し替え可能な形で組み立てられるよう、Slot ベースの抽象化を提供します。`MOVIN` や `VMC` といった source 実装はそれぞれ別 package で提供され、共通の契約に従って本体パッケージに自己登録されます。

### Slot と Descriptor

Slot は 1 つのアバターと、それに紐付く各種 source / provider をひとまとめにした実行単位です。SlotSettings (ScriptableObject) には source 種別ごとの **Descriptor** が並びます。MoCap source 用の Descriptor は次の構造です：

| フィールド | 型 | 役割 |
| --- | --- | --- |
| `SourceTypeId` | `string` | Registry に登録された具象型の識別子 (例: `"VMC"`、`"MOVIN"`)。 |
| `Config` | `MoCapSourceConfigBase` | 具象 Config への ScriptableObject 参照。Factory 側でキャストして使う。 |

Descriptor の等価判定は `SourceTypeId` の文字列等価 + `Config` の参照等価です（`Dictionary` のキーに使える）。

### `RegistryLocator` と各 Registry

`RegistryLocator` は次の Registry / Channel への静的アクセスポイントです（遅延初期化）。

| プロパティ | 型 |
| --- | --- |
| `RegistryLocator.MoCapSourceRegistry` | `IMoCapSourceRegistry` |
| `RegistryLocator.ProviderRegistry` | `IProviderRegistry` |
| `RegistryLocator.FacialControllerRegistry` | `IFacialControllerRegistry` |
| `RegistryLocator.LipSyncSourceRegistry` | `ILipSyncSourceRegistry` |
| `RegistryLocator.ErrorChannel` | `ISlotErrorChannel` |

`RuntimeInitializeOnLoadMethod(SubsystemRegistration)` で全 Registry が自動 reset されるため、Domain Reload OFF (Enter Play Mode 最適化) 環境でも二重登録は発生しません。

### `IMoCapSource` の契約

```csharp
public interface IMoCapSource : IDisposable
{
    string SourceType { get; }
    void Initialize(MoCapSourceConfigBase config);
    IObservable<MotionFrame> MotionStream { get; }
    void Shutdown();
}
```

- **Lifecycle**: `Initialize` で起動、`Shutdown`/`Dispose` で停止。`Initialize` および `Shutdown` は **main thread からの呼び出しを前提**とします。
- **`MotionStream` の threading 契約**: 実装は受信スレッド（必ずしも main thread とは限らない）から `OnNext` を発行します。**購読側は必ず `.ObserveOnMainThread()` を挟んで Unity main thread で処理してください**。
- **`OnError` は発行しない**: 復旧可能な受信エラー等は `RegistryLocator.ErrorChannel` 経由で通知され、stream は継続します。
- **Dispose の責務**: Slot 経由（`Resolve`）で取得した source は **Slot 側から直接 `Dispose` を呼び出してはいけません**。Registry が参照カウントで管理するため、`Release(source)` を呼んでください。`TryGetFactory` から直接 `Create` した場合は呼出側が `Dispose` 責任を持ちます。

### `IMoCapSourceFactory` の契約

```csharp
public interface IMoCapSourceFactory
{
    IMoCapSource Create(MoCapSourceConfigBase config);
    MoCapSourceConfigBase CreateDefaultConfig();
    IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config);
}
```

- `Create`: 与えられた Config から具象 source を生成（まだ `Initialize` は呼ばれていない状態）。Config が想定型でなければ `ArgumentException` を throw。
- `CreateDefaultConfig`: 既定値で初期化された Config の ScriptableObject インスタンスを返す。高レベル API が override 未指定時に使う。
- `CreateApplierBridge`: source の `MotionStream` を avatar に適用するための applier 一式を構築し、`IDisposable` として返す。具象 source ごとに必要な applier（直接 Transform 書き込み / Humanoid retarget 等）の差をここに閉じ込める。標準パイプラインで賄える実装は `NotSupportedException` を投げてもよい。

### `IMoCapSourceRegistry` の契約

```csharp
public interface IMoCapSourceRegistry
{
    void Register(string sourceTypeId, IMoCapSourceFactory factory);
    IMoCapSource Resolve(MoCapSourceDescriptor descriptor);
    void Release(IMoCapSource source);
    IReadOnlyList<string> GetRegisteredTypeIds();
    bool TryGetFactory(string sourceTypeId, out IMoCapSourceFactory factory);
}
```

- `Register`: `sourceTypeId` をキーに factory を登録。同一キーの二重登録は `RegistryConflictException`。
- `Resolve(descriptor)`: Descriptor に対応する `IMoCapSource` を返す。**同一 Descriptor に対して同一インスタンスを返し、参照カウントを内部で管理します**。未登録 typeId の場合は `KeyNotFoundException`。
- `Release(source)`: 参照カウントをデクリメントし、0 になった時点で内部で `Dispose` します。
- `GetRegisteredTypeIds`: Editor 等の候補列挙用。
- `TryGetFactory`: Registry の参照カウント管理を経由せずに raw factory を直接使いたい場合の入口。`CreateDefaultConfig` / `CreateApplierBridge` を呼びたいケースで利用。

### 属性ベース自動登録パターン

各 source package の具象 Factory は次のテンプレートで自己登録します。Runtime entry と Editor entry の両方を持つことで、Player ビルドと Editor Inspector の双方で typeId を解決できます。

```csharp
public sealed class ConcreteFactory : IMoCapSourceFactory
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        try
        {
            RegistryLocator.MoCapSourceRegistry.Register("TypeId", new ConcreteFactory());
        }
        catch (RegistryConflictException ex)
        {
            RegistryLocator.ErrorChannel.Publish(
                new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor()
    {
        try
        {
            RegistryLocator.MoCapSourceRegistry.Register("TypeId", new ConcreteFactory());
        }
        catch (RegistryConflictException ex)
        {
            RegistryLocator.ErrorChannel.Publish(
                new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
        }
    }
#endif

    // Create / CreateDefaultConfig / CreateApplierBridge ...
}
```

実行順は Unity が `SubsystemRegistration → BeforeSceneLoad` の順序を保証するため、`ResetForTest` (`SubsystemRegistration`) → 各 Factory の `RegisterRuntime` (`BeforeSceneLoad`) の順で確実に発火します。

### エラー通知

復旧可能なエラーや registry 競合は `RegistryLocator.ErrorChannel.Publish(SlotError)` で通知されます。`SlotError` は不変オブジェクトです：

```csharp
public sealed class SlotError
{
    public string SlotId { get; }
    public SlotErrorCategory Category { get; }
    public Exception Exception { get; }   // 原因例外（無い場合は null）
    public DateTime Timestamp { get; }    // UTC
}
```

`SlotErrorCategory` の 4 値：

| 値 | 用途 |
| --- | --- |
| `VmcReceive` | OSC 受信処理中のパース失敗・切断検知。MoCap source 系から発行。 |
| `InitFailure` | Slot 初期化失敗（Provider / Source の resolve 失敗、factory のキャスト失敗等）。 |
| `ApplyFailure` | Applier（モーション適用）でのエラー。 |
| `RegistryConflict` | Registry への同一 typeId 二重登録。`RegistryConflictException` を伴う。 |

購読は次の通り：

```csharp
RegistryLocator.ErrorChannel.Errors
    .ObserveOnMainThread()
    .Subscribe(err => HandleSlotError(err));
```

`Publish` 自体はワーカースレッドから直接呼び出しても安全です（実装は `Subject.Synchronize()` 適用済み）。スレッド切り替えは購読側の責任です。

### コードからの統合

source package を組み込む場合、原則として次の 2 経路があります。

**経路 A — Registry 管理 (Slot system に乗せる場合の標準)**

```csharp
var descriptor = new MoCapSourceDescriptor
{
    SourceTypeId = "MOVIN",
    Config = configAsset, // ScriptableObject
};

var source = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
source.Initialize(descriptor.Config);

source.MotionStream
    .ObserveOnMainThread()
    .Subscribe(frame => /* applier に渡す */);

// 終了時:
RegistryLocator.MoCapSourceRegistry.Release(source); // Dispose は Registry が行う
```

同一 Descriptor は同一 source インスタンスを共有し、`Release` を呼ぶ回数が `Resolve` と等しくなった時点で内部で `Dispose` されます。**`source.Dispose()` を直接呼ばないでください**。

**経路 B — Factory 直接利用 (Slot system を介さない場合)**

```csharp
if (!RegistryLocator.MoCapSourceRegistry.TryGetFactory("MOVIN", out var factory))
{
    throw new InvalidOperationException("MOVIN factory が未登録");
}

var config = factory.CreateDefaultConfig(); // または既存 asset を渡す
var source = factory.Create(config);
source.Initialize(config);

var applierAttachment = factory.CreateApplierBridge(source, avatar, config);

// 終了時 (順序が重要):
applierAttachment.Dispose(); // bridge / applier の解放
source.Dispose();            // source 自体の解放（呼出側責任）
```

経路 B では参照カウント管理が無いため source の lifecycle は呼出側が完全に管理します。同じ Descriptor / Config に対して何度も `Create` を呼べばその数だけ source が作られます。

### テストと Domain Reload OFF

```csharp
[SetUp]
public void SetUp()
{
    RegistryLocator.ResetForTest();
}
```

- `ResetForTest()` は全 Registry / ErrorChannel / suppression set を null に戻し、次回アクセス時に default 実装が再生成されます。
- Override 系 (`OverrideMoCapSourceRegistry` 等) は test double の差し込みに使えます。
- Domain Reload OFF 環境では `SubsystemRegistration` で `ResetForTest` が自動実行されるため、Play Mode 開始ごとに Registry が初期化されます。
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` の Factory 自己登録は reset 後に再走行するため、Play Mode 中の状態は通常通りです。EditMode test 内で reset した場合は必要な typeId を test 側で `Register` し直してください。

### 参考: 既存の source 実装

| typeId | package | 概要 |
| --- | --- | --- |
| `VMC` | 本体パッケージ内 (`MoCap/VMC/`) | VMC / EVMC4U 互換の OSC を Humanoid retarget で適用 |
| `MOVIN` | `jp.co.unvgi.realtimeavatarcontroller.movin` | MOVIN Studio の Generic Transform を直接書き込み |

各 source の典型的な Config フィールドや bone 名規則、port 既定値などは各 source package の README を参照してください。

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。
