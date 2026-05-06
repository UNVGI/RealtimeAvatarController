# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-05-06

### Added

- MOVIN Studio 用 UPM package `jp.co.unvgi.realtimeavatarcontroller.movin` を追加しました。
- `typeId="MOVIN"` の runtime / editor self-registration と `MovinMoCapSourceFactory` を追加しました。
- uOSC を使う `MovinMoCapSource` と `MovinOscReceiverHost` を追加し、既定 port `11235` で `/VMC/Ext/Bone/Pos` と `/VMC/Ext/Root/Pos` を受信できるようにしました。
- Generic Transform 向けの `MovinMotionFrame`、`MovinBonePose`、`MovinRootPose` を追加しました。
- Transform 名一致で `localPosition`、`localRotation`、`localScale` を直接適用する `MovinMotionApplier` を追加しました。
- `IMoCapSource.MotionStream` と MOVIN applier を接続する `MovinSlotBridge` を追加しました。
- `Samples~/MOVIN` に MOVIN sample scene、config asset、SlotSettings asset、NeoMOVINMan prefab、sample driver を追加しました。
- EditMode / PlayMode test asmdef と MOVIN runtime の主要経路を検証するテストを追加しました。
- MOVIN Studio 側設定、SlotSettings 設定、config field、error handling を説明する README を追加しました。

### Notes

- `MovinMoCapSourceConfig.bindAddress` は `com.hidano.uosc` 1.0.0 の制約により情報フィールドです。実際の UDP bind は全 interface に対して行われます。
- MOVIN package から発行される `SlotErrorCategory.VmcReceive` は `SlotError.SlotId="MOVIN"` で識別できます。
- 既存の `typeId="VMC"` source は変更していません。MOVIN と VMC を併用する場合は異なる UDP port を指定してください。

### Future Work

- VMC + MOVIN を同一 scene で見せる並行動作 demo は MVP 範囲外として保留しています。
