# Requirements Document

## Project Description (Input)
VMC プロトコルを EVMC4U に依存せず自前で受信する MoCap source 実装。

`com.hidano.realtimeavatarcontroller.mocap-vmc` パッケージから EVMC4U 依存を撤廃し、uOSC のみを利用して VMC OSC メッセージ (`/VMC/Ext/Bone/Pos` および `/VMC/Ext/Root/Pos` を最低限) を直接パースする `IMoCapSource` 実装を新設する。

これにより利用者は以下の手作業から解放される:
- `Assets/EVMC4U/` への EVMC4U `.unitypackage` import
- `EVMC4U.asmdef` の自作
- `Assets/EVMC4U/ExternalReceiver.cs` に対する `evmc4u.patch` の `git apply` 適用

利用者が用意する依存は **uOSC のみ** となる。

### 背景

本 spec は当初 `mocap-vmc-reflection-loading` として「Reflection 化により EVMC4U asmdef 自作を不要化する」方針で着手したが、深掘り (`dig.md`) の結果以下が判明した:

1. EVMC4U 依存を残したまま `EVMC4U.asmdef` 自作だけを解消しても、`evmc4u.patch` の `git apply` 工程は残存する (Reflection では private method 内のロジック改変を逃せないため)。
2. EVMC4U 機能の実利用率は 5〜10% 以下であり、残り 90% の未使用コードが `Model==null` 早期 return 等で我々のデータ経路を妨害して patch を必要とさせている。
3. VMC プロトコル受信に必要な実装規模は ~210 行程度で自作可能と見積もれた。

これらを踏まえ、Reflection 化路線を破棄して「VMC 受信の自前実装」に spec の射程を再定義する。

## Requirements
<!-- Will be regenerated in /kiro:spec-requirements phase -->
