# Realtime Avatar Controller MOVIN

`jp.co.unvgi.realtimeavatarcontroller.movin` は、MOVIN Studio から VMC 互換 OSC over UDP で送信されるモーションを Realtime Avatar Controller の MoCap source として扱う UPM パッケージです。

このパッケージは `typeId="MOVIN"` を `RegistryLocator.MoCapSourceRegistry` に自動登録し、MOVIN の Generic rig 向けデータを Transform 名の 1:1 対応で直接適用します。Humanoid retarget 経路や既存の `typeId="VMC"` 実装は変更しません。

> 本 README は MOVIN 固有の事項のみを扱います。Slot / Registry / Factory / `IMoCapSource` 契約 / エラーチャネル / コード経由の統合パターン / テスト用 reset API などの **本体パッケージ全般のアーキテクチャ**は、本体 README の「アーキテクチャ」節を参照してください。
> リポジトリ内パス: `Packages/com.hidano.realtimeavatarcontroller/README.md`

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

scopedRegistries の構成（本体パッケージ用 / OpenUPM / uOSC 用）は本体 README の「インストール」節を参照してください。git URL で導入する場合は、パッケージパスに `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.movin` を指定します。

## クイックスタート

1. Package Manager から `Realtime Avatar Controller MOVIN` を導入します。
2. Package Manager の Samples から `MOVIN` sample を import します。
3. `Samples~/MOVIN/Scenes/MOVINSampleScene.unity` または import 後の sample scene を開きます。
4. SlotSettings の `MoCapSourceDescriptor.SourceTypeId` が `MOVIN` になっていることを確認します。
5. `MovinMoCapSourceConfig` の `port` が MOVIN Studio 側と一致していることを確認します。既定値は `11235` です。
6. MOVIN Studio 側で `Platform=Unity`、`Port=11235` を設定し、Start Streaming を実行します。
7. Unity の scene を Play すると、受信した Transform 名に一致する avatar の bone にモーションが適用されます。

## 自動登録

`MovinMoCapSourceFactory` は本体パッケージで規定された属性ベース自動登録パターンに従い、`typeId="MOVIN"` を `RegistryLocator.MoCapSourceRegistry` に自己登録します。利用側で明示的な initialization は不要です。

- Runtime: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
- Editor: `[InitializeOnLoadMethod]`（Inspector からの SlotSettings 編集時に factory が見えるようにするため）

二重登録時は `SlotErrorCategory.RegistryConflict` として `ErrorChannel` に publish されます（例外は throw されません）。エラー購読の方法は本体 README を参照。

## MOVIN Studio 側の設定

MOVIN Studio では Unity 向けストリーミングを選び、送信 port を Unity 側の `MovinMoCapSourceConfig.port` と合わせます。MOVIN 用の既定 port は `11235` です。標準 VMC / EVMC4U でよく使われる `39539` とは異なるため、VMC source と同時に使う場合は port を分けてください。

### Bone 名フォーマット

MOVIN Studio が送出する bone 名は `<class>:<boneName>` 形式の文字列です（例: `MOVIN:Hips`、`MOVIN:Spine`、`MOVIN:Head`）。Unity 側ではこの文字列を変換せず、同じ名前の Transform に対して `localPosition`、`localRotation`、`localScale` を直接書き込みます。

自前 avatar に組み込む場合：

- avatar 側 Transform 名を MOVIN Studio が出力する文字列と一致させる。
- `MovinMoCapSourceConfig.boneClass` で prefix 部分（例: `MOVIN`）を指定する。

### OSC アドレスと payload

| アドレス | 引数構成 | 用途 |
| --- | --- | --- |
| `/VMC/Ext/Bone/Pos` | 8 引数: `string name, float px, py, pz, float qx, qy, qz, qw` | 各 bone の local pose |
| `/VMC/Ext/Root/Pos` | 8 / 11 / 14 引数: 上記 8 引数 (+ `float sx, sy, sz`) (+ `float ox, oy, oz`) | Root pose と任意 scale / offset |

Root pose の `localScale` は **avatar root ではなく**、payload の bone 名で解決された Transform にだけ書き込まれます（`MovinMotionApplier.Apply` の挙動）。`localOffset` は frame には保持されますが applier では未使用です。

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

`MovinMotionApplier.SetAvatar(avatarRoot, rootBoneName, boneClass)` は次の優先順で armature root を解決します（factory 経由では `CreateApplierBridge` が内部で呼び出します）。

1. 明示指定の `rootBoneName` が存在する場合、avatar root 配下を再帰探索して名前一致を採用。
2. avatar root 配下に humanoid `Avatar` を持つ `Animator` があれば、その `Animator` の Transform を採用。
3. Renderer 兄弟ヒューリスティック（`Renderer` を持たない Transform で、兄弟に `Renderer` が居るもの）。FBX 由来の `Body / Armature` レイアウトを想定。
4. 一般的な armature root 名 (`Hips`、`Pelvis`、`Armature`、`Root`、`RootBone`、`Skeleton`) との名前一致。
5. 上記すべて失敗した場合は avatar root 自身を起点として全階層を探索（warning ログを出力）。

armature root が決定したあとは、その配下の全 Transform を再帰的に走査し、`boneClass` 指定があれば `<boneClass>:` で始まる Transform 名のみを `Dictionary<string, Transform>` に登録します。重複名がある場合は後勝ちです。

存在しない bone 名が frame に含まれていても例外にはせず、その bone はスキップされます。

## コードからの統合

本体 README の「コードからの統合」節（経路 A: Registry 管理 / 経路 B: Factory 直接利用）の手順を MOVIN に当てはめると次のようになります。経路 A は Slot system に組み込む場合の標準、経路 B は Slot を介さず source を直接使いたい場合に利用します。

```csharp
// 経路 A: Registry 管理
var descriptor = new MoCapSourceDescriptor
{
    SourceTypeId = MovinMoCapSourceFactory.MovinSourceTypeId, // "MOVIN"
    Config = movinConfigAsset,                                 // MovinMoCapSourceConfig
};
var source = RegistryLocator.MoCapSourceRegistry.Resolve(descriptor);
source.Initialize(descriptor.Config);
// ... source.MotionStream を購読する場合は ObserveOnMainThread() を挟む ...
RegistryLocator.MoCapSourceRegistry.Release(source);
```

```csharp
// 経路 B: Factory 直接利用
RegistryLocator.MoCapSourceRegistry.TryGetFactory(
    MovinMoCapSourceFactory.MovinSourceTypeId, out var factory);
var config = factory.CreateDefaultConfig() as MovinMoCapSourceConfig;
config.port = 11235;
config.boneClass = "MOVIN";
var source = factory.Create(config);
source.Initialize(config);
var attachment = factory.CreateApplierBridge(source, avatarGameObject, config);
// ...
attachment.Dispose();
source.Dispose();
```

`Samples~/MOVIN/Runtime/MovinSlotDriver.cs` および `MovinSessionDriver.cs` は sample 専用 driver です。本番コードでは driver を真似ず、上記いずれかの経路で組み込んでください。

## MOVIN 固有の挙動

本体 README の `IMoCapSource` 契約（lifecycle、threading、Dispose ルール）を踏まえた上で、MOVIN 実装の固有事項を以下にまとめます。

### Threading の実態

`IMoCapSource` の契約では `MotionStream` の OnNext は受信スレッドで発行される可能性があり、購読側は `ObserveOnMainThread()` を必須とします。MOVIN 実装では uOSC が main thread の `Update` で dispatch する仕組みのため、結果として **MOVIN の `MotionStream` emission は実際には main thread 上**で行われます。とはいえ契約に従い、購読側は引き続き `ObserveOnMainThread()` を挟むことを推奨します（`MovinSlotBridge` は内部で挟んでいます）。

### `MovinMoCapSource` の state machine

`Uninitialized → Running → Disposed` の一方向です。

- `Initialize` は `Uninitialized` 状態でのみ呼べます。それ以外は `InvalidOperationException`。
- `Initialize` 中に config 不正・port 範囲外・port 衝突などで失敗した場合、state は `Uninitialized` のまま、内部の `MovinOscReceiverHost` も後始末されます。
- `Shutdown` / `Dispose` は冪等。`Dispose` 後の `MotionStream` は `OnCompleted` 済み。
- 1 つの instance は 1 回しか `Initialize` できません。再起動したい場合は新しい instance を作成してください。
- 経路 A（Registry 管理）の場合、`Dispose` は Registry が呼びます。利用側は `Release` を呼んでください。

### `Initialize` の失敗ケース

`Initialize` の同期失敗は **例外として throw** され、`ErrorChannel` には流れません。Registry 経由でも Factory 直接利用でも同じです。

| 例外 | 発生条件 |
| --- | --- |
| `ArgumentException` | `config` が `MovinMoCapSourceConfig` 以外 |
| `ArgumentOutOfRangeException` | `config.port` が 1〜65535 の範囲外 |
| `SocketException(AddressAlreadyInUse)` | 同一 port が既に他の MOVIN/VMC source や OS 上の他プロセスで bind 済み |

実行時の受信エラーは `ErrorChannel` に `SlotErrorCategory.VmcReceive` として publish されます（`SlotId="MOVIN"`）。本体 README の「エラー通知」節も参照してください。

### 同 typeId / 同 port の併用

- `MovinMoCapSource` は instance ごとに自分専用の `MovinOscReceiverHost`（`DontDestroyOnLoad` の hidden GameObject）を生成します。
- 経路 A では同一 Descriptor は同一 source を共有するため、同じ port の重複起動は発生しません。異なる Descriptor で異なる port を指定すれば複数 MOVIN source を併用できます。
- 経路 B では呼出側が複数 source を作れるため、port の重複は呼出側責任で避けてください。
- VMC source と同 port にした場合も同様にバインド失敗となります。

### Root pose の `localScale`

MOVIN は `/VMC/Ext/Root/Pos` で送る `localScale` を、avatar root ではなく **payload の bone 名で解決した Transform** に書き込みます。VMC 互換実装の挙動と異なる点なので、混在運用するときは注意してください。

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

sample は MOVIN 専用 driver と `MovinSlotBridge` を使い、Slot が Active になった後に source と applier を接続します。本番組み込みでは driver を真似るのではなく、上の「コードからの統合」または parent package の Slot system 経由で接続してください。

## VMC source との併用

この package は既存の `typeId="VMC"` を上書きしません。`VMC` と `MOVIN` は同一 scene 内で併用できますが、UDP port は別々にしてください。同じ port を指定した場合は bind 失敗として初期化エラーになります。

## License

This package is released under the MIT License. See [LICENSE](./LICENSE) for the full text. Copyright (c) 2026 Hidano.

## Issues

不具合報告 / 機能要望は本リポジトリ (`RealtimeAvatarController`) の Issue tracker に投稿してください。
