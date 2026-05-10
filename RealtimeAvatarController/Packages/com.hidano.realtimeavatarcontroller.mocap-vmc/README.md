# Realtime Avatar Controller MoCap VMC

`com.hidano.realtimeavatarcontroller.mocap-vmc` は、Realtime Avatar Controller 向けの VMC MoCap Source パッケージです。
uOSC を通じて VMC OSC の `/VMC/Ext/Bone/Pos` と `/VMC/Ext/Root/Pos` を受信し、コアパッケージの MoCap Source / Motion Pipeline へ接続するための実装、Editor 連携、VMC サンプルを提供します。

## セットアップ

uOSC を導入する。これだけ。

## 仕様

既存の `mocap-vmc` spec で定義済みの VMC 受信仕様は、このパッケージへ移動した後も変更しません。

- `typeId="VMC"` による MoCap Source 識別
- 属性ベースの自己登録
- 共有 receiver による受信モデル
- `HumanoidMotionFrame` の発行

## 対応範囲

- 対応する VMC OSC メッセージは `/VMC/Ext/Bone/Pos` と `/VMC/Ext/Root/Pos` です。
- VMC v2.1 拡張の BlendShape、Camera、Light、Tracker Status などは、このパッケージの対象外です。

## Credits

EVMC4U は、shared receiver pattern、refCount lifecycle、SubsystemRegistration reset の設計上の着想元です。
