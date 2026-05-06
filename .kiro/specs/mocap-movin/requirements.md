# Requirements Document

## Project Description (Input)
mocap-movin: MOVIN モーションキャプチャシステムを新規 UPM パッケージとして追加する。本体パッケージ jp.co.unvgi.realtimeavatarcontroller には一切手を入れず、`jp.co.unvgi.realtimeavatarcontroller.movin` という別 UPM パッケージで自己完結させる。

## 背景
本リポジトリは VTuber 向けのリアルタイムモーション受信・アバター切替ツール (jp.co.unvgi.realtimeavatarcontroller) の社内フォーク。本体は IMoCapSource / IMoCapSourceFactory / MoCapSourceConfigBase / MoCapSourceDescriptor / RegistryLocator (registry-based dispatch by string typeId) という拡張点を備えており、外部 UPM パッケージから MoCap ソースを追加できる設計になっている。本 spec はその拡張点を使って MOVIN サポートを追加する初の事例となる。

## 対象 MoCap デバイス: MOVIN
内部プロトコルは VMC (OSC over UDP) だが、以下が標準 VMC / EVMC4U と異なる:
- デフォルトポート 11235 (標準 VMC の 39539 ではない)
- Humanoid リターゲットではなく **Generic リグの Transform 直接書き込み**で適用 (NeoMOVINMan のような非 Humanoid キャラクタ前提)
- 骨は `prefix:boneName` 形式 (例 `mixamorig:Hips`, `MOVIN:Spine`) で送られ、Unity 側で同名 Transform に 1:1 適用
- `boneClass` prefix によるフィルタ機能あり
- `/VMC/Ext/Root/Pos` の v2.1 拡張で localScale / localOffset を毎フレーム送出

参考資料:
- 公式 Unity ガイド: https://help.movin3d.com/movin-studio-usage-guide/live-streaming/streaming-mocap-data-into-unity
- 同梱サンプル: `RealtimeAvatarController/Assets/MOVIN/` (MocapReceiver.cs / VMCReceiver.cs / NeoMOVINMan_Unity.prefab / Sample_Ch14・Ch29・MOVINman シーン)

## スコープ
### 本 spec で実装するもの
- 新 UPM パッケージ `jp.co.unvgi.realtimeavatarcontroller.movin` の雛形 (package.json / Runtime / Editor / Tests / Samples~)
- `MovinMoCapSource : IMoCapSource` — uOSC で 11235 に bind、VMC OSC 受信、メインスレッドで emit
- `MovinMoCapSourceFactory : IMoCapSourceFactory` — typeId="MOVIN" で属性ベース自己登録 (Runtime / Editor の二経路)
- `MovinMoCapSourceConfig : MoCapSourceConfigBase` — port / bindAddress / rootBoneName / boneClass (将来追加プロパティ余地)
- MOVIN 専用 MotionFrame 型 (Generic Transform ベース、name キーで localPosition/Rotation/Scale を保持)
- MOVIN 専用 Applier — Avatar Transform ツリーを走査して name 一致で直接書き込み (本体側 HumanoidMotionApplier には乗せない、自己完結)
- 自己登録パターンは VMC 実装 (`Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs` / `Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs`) を踏襲
- Samples~/MOVIN: NeoMOVINMan を使ったデモシーン
- EditMode / PlayMode テスト

### 本 spec で実装しないもの (out of scope)
- Humanoid リターゲット経路のサポート (将来拡張、別 spec)
- 本体パッケージ (jp.co.unvgi.realtimeavatarcontroller) の改変
- 既存 HumanoidMotionApplier / GenericMotionFrame の流用
- MOVIN Studio 側の設定自動化
- 表情 (Blend) / カメラ / HMD / Controller / Tracker 系 OSC アドレスの処理 (将来拡張)

## 制約
- 本体パッケージは一切改変しない (拡張点経由のみ)
- Unity 6000.3.10f1
- 依存: RealtimeAvatarController.Core / RealtimeAvatarController.Motion (asmdef name 参照) / com.hidano.uosc / UniRx / UniTask
- Player Build / Editor の両環境で動作 (Domain Reload OFF も考慮)
- 既存 VMC (typeId="VMC") との並行稼動可 (異なるポートで同居)

## アーキ方針
1. **完全自己完結**: 本パッケージ内で Source → MotionFrame → Applier まで一貫して提供。本体側の MotionFrame / Applier には依存しない (継承元として `RealtimeAvatarController.Core.MotionFrame` のみ参照)。
2. **typeId = "MOVIN"**: SlotSettings の MoCapSourceDescriptor.SourceTypeId から一意に dispatch される。
3. **Slot との結線**: Sample 側で SlotManager.TryGetSlotResources を介して Source と Avatar を取得し、本パッケージ提供の MovinMotionApplier を駆動する (or 上位層で同等の連携を提供)。具体的な結線方式は requirements / design で詰める。

## 利用シナリオ
1. 利用者は Unity プロジェクトの manifest.json に本パッケージと本体パッケージを追加
2. SlotSettings アセットを作成し、MoCapSourceDescriptor.SourceTypeId に "MOVIN" を選択 (Sample SlotSettingsEditor が GetRegisteredTypeIds で動的列挙)
3. MovinMoCapSourceConfig アセットを作成 (port=11235, rootBoneName, boneClass を設定)
4. MOVIN Studio で Platform=Unity, Port=11235 を設定して Start Streaming
5. アバターに対してリアルタイムにモーションが適用される

## Requirements
<!-- Will be generated in /kiro:spec-requirements phase -->
