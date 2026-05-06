# Realtime Avatar Controller MOVIN

`jp.co.unvgi.realtimeavatarcontroller.movin` は、MOVIN Studio から VMC 互換 OSC over UDP で送信されるモーションを Realtime Avatar Controller の MoCap source として扱う UPM パッケージです。

このパッケージは `typeId="MOVIN"` を `RegistryLocator.MoCapSourceRegistry` に自動登録し、MOVIN の Generic rig 向けデータを Transform 名の 1:1 対応で直接適用します。Humanoid retarget 経路や既存の `typeId="VMC"` 実装は変更しません。

## 前提

- Unity 6000.3.10f1
- `jp.co.unvgi.realtimeavatarcontroller` 0.1.0
- `com.hidano.uosc` 1.0.0
- MOVIN Studio の Unity 向けライブストリーミング設定

## インストール

`Packages/manifest.json` の `dependencies` にこのパッケージを追加します。依存する本体パッケージと uOSC は `package.json` の依存関係として解決されます。

```json
{
  "dependencies": {
    "jp.co.unvgi.realtimeavatarcontroller.movin": "0.1.0"
  }
}
```

git URL で導入する場合は、パッケージパスに `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.movin` を指定してください。

## 自己登録

このパッケージは利用側で明示的な initialization を必要としません。

- **Runtime**: `MovinMoCapSourceFactory` が `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で `RegistryLocator.MoCapSourceRegistry` に `typeId="MOVIN"` として自動登録されます。
- **Editor**: `MovinMoCapSourceFactoryEditorRegistrar` が `[InitializeOnLoadMethod]` で同等の登録を行います。これにより Editor の Inspector 上で SlotSettings を編集する際にも MOVIN factory が解決可能です。

登録時に同じ `typeId` が既に登録されている場合は `SlotErrorCategory.RegistryConflict` として `RegistryLocator.ErrorChannel` に publish されます（例外は throw されません）。

## クイックスタート

1. Package Manager から `Realtime Avatar Controller MOVIN` を導入します。
2. Package Manager の Samples から `MOVIN` sample を import します。
3. `Samples~/MOVIN/Scenes/MOVINSampleScene.unity` または import 後の sample scene を開きます。
4. SlotSettings の `MoCapSourceDescriptor.SourceTypeId` が `MOVIN` になっていることを確認します。
5. `MovinMoCapSourceConfig` の `port` が MOVIN Studio 側と一致していることを確認します。既定値は `11235` です。
6. MOVIN Studio 側で `Platform=Unity`、`Port=11235` を設定し、Start Streaming を実行します。
7. Unity の scene を Play すると、受信した Transform 名に一致する avatar の bone にモーションが適用されます。

## MOVIN Studio 側の設定

MOVIN Studio では Unity 向けストリーミングを選び、送信 port を Unity 側の `MovinMoCapSourceConfig.port` と合わせます。MOVIN 用の既定 port は `11235` です。標準 VMC / EVMC4U でよく使われる `39539` とは異なるため、VMC source と同時に使う場合は port を分けてください。

### Bone 名フォーマット

MOVIN Studio が送出する bone 名は `<class>:<boneName>` 形式の文字列です（例: `MOVIN:Hips`、`MOVIN:Spine`、`MOVIN:Head`）。Unity 側ではこの文字列を変換せず、同じ名前の Transform に対して `localPosition`、`localRotation`、`localScale` を直接書き込みます。自前 avatar に組み込む場合は、avatar 側 Transform 名を MOVIN Studio が出力する文字列と一致させ、`MovinMoCapSourceConfig.boneClass` で prefix 部分（例: `MOVIN`）を指定してください。

### OSC アドレスと payload

| アドレス | 引数構成 | 用途 |
| --- | --- | --- |
| `/VMC/Ext/Bone/Pos` | 8 引数: `string name, float px, py, pz, float qx, qy, qz, qw` | 各 bone の local pose |
| `/VMC/Ext/Root/Pos` | 8 / 11 / 14 引数: 上記 8 引数 (+ `float sx, sy, sz`) (+ `float ox, oy, oz`) | Root pose と任意 scale / offset |

Root pose の `localScale` は **avatar root ではなく**、payload の bone 名で解決された Transform にだけ書き込まれます（`MovinMotionApplier.Apply` の挙動）。`localOffset` は現在 frame には載せていますが applier では使用されません。

未知のアドレスや引数数が一致しない message は無視されます。

## SlotSettings

MOVIN source を使う SlotSettings では、MoCap source descriptor を次のように設定します。

| 項目 | 値 |
| --- | --- |
| `MoCapSourceDescriptor.SourceTypeId` | `MOVIN` |
| `MoCapSourceDescriptor.Config` | `MovinMoCapSourceConfig` asset |

`SourceTypeId` は大文字の `MOVIN` です。`VMC` とは別の source type として registry に登録されます。

## MovinMoCapSourceConfig

| フィールド | 役割 |
| --- | --- |
| `port` | uOSC が受信する UDP port。範囲 1〜65535、既定値 `11235`。 |
| `bindAddress` | 現時点では情報フィールドです。`com.hidano.uosc` 1.0.0 は bind address を公開していないため、実際の bind は全 interface（`0.0.0.0`）に対して行われます。 |
| `rootBoneName` | Avatar 内で bone table の探索起点にする Transform 名です。空の場合は次節「Bone table 探索」のフォールバックを使います。 |
| `boneClass` | 受信 bone 名を prefix で絞り込むための値です。例: `MOVIN` を指定すると **`MOVIN:` で始まる Transform 名だけ**を bone table に登録します。空の場合は起点以下の全 Transform を対象にします。 |

### Bone table 探索

`MovinMotionApplier.SetAvatar(avatarRoot, rootBoneName, boneClass)`（factory 経由では `CreateApplierBridge` が内部呼出し）は次の優先順で armature root を解決します。

1. 明示指定の `rootBoneName` が存在する場合、avatar root 配下を再帰探索して名前一致を採用。
2. avatar root 配下に humanoid `Avatar` を持つ `Animator` があれば、その `Animator` の Transform を採用。
3. Renderer 兄弟ヒューリスティック（`Renderer` を持たない Transform で、兄弟に `Renderer` が居るもの）。FBX 由来の `Body / Armature` レイアウトを想定。
4. 一般的な armature root 名 (`Hips`、`Pelvis`、`Armature`、`Root`、`RootBone`、`Skeleton`) との名前一致。
5. 上記すべて失敗した場合は avatar root 自身を起点として全階層を探索（warning ログを出力）。

armature root が決定したあとは、その配下の全 Transform を再帰的に走査し、`boneClass` 指定があれば `<boneClass>:` で始まる Transform 名のみを `Dictionary<string, Transform>` に登録します。重複名がある場合は後勝ちです。

存在しない bone 名が frame に含まれていても例外にはせず、その bone はスキップされます。

## 組み込みレシピ（Samples を使わない場合）

`Samples~/MOVIN/Runtime/MovinSlotDriver.cs` および `MovinSessionDriver.cs` は sample 専用 driver です。本番コードでは Slot 管理を自分のシステムに統合し、`MovinMoCapSourceFactory` を直接使うのが基本形です。

```csharp
using RealtimeAvatarController.Core;
using RealtimeAvatarController.MoCap.Movin;
using UnityEngine;

public sealed class MovinIntegration : System.IDisposable
{
    private readonly IMoCapSource _source;
    private readonly System.IDisposable _attachment;

    public MovinIntegration(GameObject avatar, MovinMoCapSourceConfig config)
    {
        // 1) typeId で factory を解決（自動登録済み）。
        var factory = RegistryLocator.MoCapSourceRegistry.Resolve(
            MovinMoCapSourceFactory.MovinSourceTypeId);

        // 2) Source を生成し UDP port を bind。
        _source = factory.Create(config);
        _source.Initialize(config);

        // 3) Avatar に bone table を構築し、source.MotionStream を applier に接続。
        //    返り値の IDisposable は bridge + applier を保持。Source は含まない。
        _attachment = factory.CreateApplierBridge(_source, avatar, config);
    }

    public void Dispose()
    {
        _attachment?.Dispose();   // Bridge subscription と applier を破棄
        _source?.Dispose();       // UDP port を release し MotionStream を OnCompleted
    }
}
```

`config` は `ScriptableObject.CreateInstance<MovinMoCapSourceConfig>()` で作るか、`Assets/...asset` として保存したものを使ってください。Slot system に乗せる場合は parent package の Slot 解決層が `MoCapSourceDescriptor` 経由で同等の処理を実行します。

## モーション適用と threading

`MovinMotionApplier.Apply(MovinMotionFrame)` は frame 内の全 bone pose と root pose を armature 配下の Transform に直接書き込みます。Humanoid 用の `HumanoidMotionApplier` や `HumanoidMotionFrame` は参照しません（`MovinMotionFrame.SkeletonType` は `SkeletonType.Generic`）。

### Threading

- uOSC は UDP 受信を内部で background thread で行いますが、`uOscServer.onDataReceived` への dispatch は main thread の `Update` で発火します。
- `MovinOscReceiverHost.LateUpdate` で `IMovinReceiverAdapter.Tick()` を呼び、`MovinMoCapSource` が `IObservable<MotionFrame>` に `OnNext` します。つまり **`MotionStream` の emission は main thread**です。
- 直接 `MotionStream` を subscribe する場合は通常 main thread で受け取れますが、`Subject.Synchronize()` で thread safe 化してあるため background から購読しても安全です。`MovinSlotBridge` は念のため `ObserveOnMainThread()` を挟んで applier に渡します。

### Lifecycle と state machine

`MovinMoCapSource` は `Uninitialized → Running → Disposed` の一方向 state machine です。

| 状態 | 入る契機 | 出る契機 |
| --- | --- | --- |
| `Uninitialized` | `new MovinMoCapSource()` | `Initialize(config)` 成功 |
| `Running` | `Initialize` 成功 | `Shutdown()` または `Dispose()` |
| `Disposed` | `Shutdown` / `Dispose` 呼出 | （戻れない） |

- `Initialize` を `Uninitialized` 以外で呼ぶと `InvalidOperationException`。
- `Initialize` 中に config 不正・port 範囲外・port 衝突などで失敗した場合は state は `Uninitialized` のまま、`MovinOscReceiverHost` も後始末されます。
- `Shutdown` / `Dispose` は冪等。`Dispose` 後の `MotionStream` は `OnCompleted` 済み。
- 1 つの source は 1 回しか `Initialize` できません。再起動したい場合は新しい instance を作成してください。

## エラー通知

受信処理や frame 発行中の復旧可能なエラーは `MotionStream.OnError()` では通知しません。代わりに `RegistryLocator.ErrorChannel` 経由で `SlotError` として publish します。

| 発生条件 | `Category` | `SlotId` |
| --- | --- | --- |
| factory 自己登録時に同 typeId が衝突 | `RegistryConflict` | `"MOVIN"` |
| Slot 初期化に紐づくエラーで slot id が解決可能 | （parent package 側で発行） | 実 slot id |
| OSC dispatch 中の例外（payload 解釈、`HandleBonePose`/`HandleRootPose`） | `VmcReceive` | `"MOVIN"` |
| `LateUpdate` 内 `Tick` 中の例外（snapshot 構築・OnNext） | `VmcReceive` | `"MOVIN"` |

`Initialize` の同期失敗（config 型不一致、port 範囲外、port 衝突 = `SocketException AddressAlreadyInUse`）は **例外として throw** され、`ErrorChannel` には流れません。Slot 解決層を経由しない直接利用では呼出側で catch してください。

## 同 typeId / 同 port の併用

- `MovinMoCapSource` は 1 instance ごとに自分専用の `MovinOscReceiverHost`（`DontDestroyOnLoad` の hidden GameObject）を生成します。
- 複数の MOVIN source を **異なる port** で同時起動できます。
- 同じ port を 2 つ以上の source が指定した場合、後発の `Initialize` が `SocketException(AddressAlreadyInUse)` で失敗します。これは host 内部で active port set + UDP listener 列挙の両方で検査されます。
- `VMC` source と同 port にした場合も同様にバインド失敗となります。

## テストでの利用

統合テストや EditMode test で MOVIN を使う場合：

- `RegistryLocator.ResetForTest()` (parent package) で MoCap source registry を初期状態に戻せます。
- VMC sample との混在テストで使う `EVMC4USharedReceiver.ResetForTest()` (parent package) も同様です。
- MOVIN source factory は `[RuntimeInitializeOnLoadMethod]` 経由で再登録されるので、reset 後はテスト側で必要な typeId を `Register` し直すか、Play mode を再起動してください。
- `MovinMoCapSource.Initialize` は同期的に UDP port を bind するため、テスト同士で port を分けるか、それぞれ `Dispose` を確実に呼ぶことで port 解放を担保してください（`MovinOscReceiverHost.Shutdown` で `s_activePorts` から外れます）。

## Samples

Package Manager の Samples には `MOVIN` sample が含まれます。

### Scenes
- `Samples~/MOVIN/Scenes/MOVINSampleScene.unity` — MOVIN 受信を確認するための demo scene。

### Configs
- `Samples~/MOVIN/Configs/MovinMoCapSourceConfig.asset` — `port=11235` などを設定済みの MOVIN source 用 config。
- `Samples~/MOVIN/Configs/MovinSampleAvatarProviderConfig.asset` — sample 用 AvatarProvider config。NeoMOVINMan prefab を参照します。

### SlotSettings
- `Samples~/MOVIN/Runtime/MovinSampleSlotSettings.asset` — `SourceTypeId=MOVIN` を持つ sample 用 SlotSettings。

### Prefabs / Avatar assets
- `Samples~/MOVIN/Prefabs/NeoMOVINMan_Unity.prefab` — sample avatar prefab。
- `Samples~/MOVIN/Prefabs/NeoMOVINManAssets/NeoMOVINMan_Unity.fbx` — prefab の元 FBX。
- `Samples~/MOVIN/Prefabs/NeoMOVINManAssets/Materials/` — sample 用の material・shader (`movinman.shader`、`movin common.hlsl`、`green.mat`、`white.mat`)。

### Sample driver scripts
- `Samples~/MOVIN/Runtime/RealtimeAvatarController.MoCap.Movin.Samples.asmdef` — sample script 用の asmdef。
- `Samples~/MOVIN/Runtime/MovinSlotDriver.cs` — Slot を組み立てて MOVIN source / applier を接続する driver。
- `Samples~/MOVIN/Runtime/MovinSessionDriver.cs` — Session 経由で sample を起動するための driver。

sample は MOVIN 専用 driver と `MovinSlotBridge` を使い、Slot が Active になった後に source と applier を接続します。本番組み込みでは driver を真似るのではなく、上記「組み込みレシピ」または parent package の Slot system 経由で接続してください。

## VMC source との併用

この package は既存の `typeId="VMC"` を上書きしません。`VMC` と `MOVIN` は同一 scene 内で併用できますが、UDP port は別々にしてください。同じ port を指定した場合は bind 失敗として初期化エラーになります。

## License

This package is released under the MIT License. See [LICENSE](./LICENSE) for the full text. Copyright (c) 2026 Hidano.

## Issues

不具合報告 / 機能要望は本リポジトリ (`RealtimeAvatarController`) の Issue tracker に投稿してください。
