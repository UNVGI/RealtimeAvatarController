# Realtime Avatar Controller MOVIN

`jp.co.unvgi.realtimeavatarcontroller.movin` は、MOVIN Studio から VMC 互換 OSC over UDP で送信されるモーションを Realtime Avatar Controller の MoCap source として扱う UPM パッケージです。

このパッケージは `typeId="MOVIN"` を登録し、MOVIN の Generic rig 向けデータを Transform 名の 1:1 対応で直接適用します。Humanoid retarget 経路や既存の `typeId="VMC"` 実装は変更しません。

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

送信される bone 名は `prefix:boneName` 形式の文字列として扱われます。Unity 側ではこの文字列を変換せず、同じ名前の Transform に対して `localPosition`、`localRotation`、`localScale` を直接書き込みます。

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
| `port` | uOSC が受信する UDP port。既定値は `11235` です。 |
| `bindAddress` | 現時点では情報フィールドです。`com.hidano.uosc` 1.0.0 は bind address を公開していないため、実際の bind は全 interface に対して行われます。 |
| `rootBoneName` | Avatar 内で bone table の探索起点にする Transform 名です。空の場合は sample 由来の armature 探索を使います。 |
| `boneClass` | 受信 bone 名を prefix で絞り込むための値です。例: `MOVIN` を指定すると `MOVIN:` で始まる Transform 名だけを対象にします。空の場合は起点以下の全 Transform を対象にします。 |

`rootBoneName` と `boneClass` は、MOVIN から届く bone 名と avatar 側 Transform 名が一致するように調整してください。存在しない bone は例外にせず、その frame ではスキップされます。

## モーション適用

`MovinMotionApplier` は `MovinMotionFrame` の内容を Generic rig の Transform tree に直接適用します。Humanoid 用の `HumanoidMotionApplier` や `HumanoidMotionFrame` は参照しません。

`/VMC/Ext/Root/Pos` に `localScale` が含まれる場合、その scale は avatar root ではなく、OSC payload の bone 名で解決された Transform にのみ適用されます。

## エラー通知

受信処理や frame 発行中の復旧可能なエラーは `MotionStream.OnError()` では通知しません。`RegistryLocator.ErrorChannel` 経由で `SlotErrorCategory.VmcReceive` として publish されます。

MOVIN package から発行される `VmcReceive` エラーは、監視側で判別できるように `SlotError.SlotId="MOVIN"` を設定します。Slot 初期化に紐づくエラーは、Slot を解決できる場合は実際の slot id で通知されます。

Registry の二重登録は `SlotErrorCategory.RegistryConflict` として通知されます。

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

sample は MOVIN 専用 driver と `MovinSlotBridge` を使い、Slot が Active になった後に source と applier を接続します。

## VMC source との併用

この package は既存の `typeId="VMC"` を上書きしません。`VMC` と `MOVIN` は同一 scene 内で併用できますが、UDP port は別々にしてください。同じ port を指定した場合は bind 失敗として初期化エラーになります。
