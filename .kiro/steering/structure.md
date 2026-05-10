## Packages

- com.hidano.realtimeavatarcontroller (core): スロット管理、MoCap Source レジストリ、モーションパイプライン、アバタープロバイダー抽象を含む。プロバイダー非依存のデモ用に Stub MoCap Source を使う UI Sample を含む。
- com.hidano.realtimeavatarcontroller.mocap-vmc: 自前実装による VMC 受信 (uOSC のみ依存) を提供する。core パッケージに依存する。VMCReceiveDemo シーンを含む独自の VMC Sample を含む。

## Dependency Direction

- 一方向依存: mocap-vmc パッケージは core に依存し、core は mocap-vmc に依存しない。
- core は単独でインストール可能 (UI Sample は Stub source を使用)。mocap-vmc は core + uOSC の利用者側セットアップを必要とする。

## Sample Imports

- UI Sample (core): Stub MoCap Source を使うプロバイダー非依存の UI デモ。VMC 依存はない。
- VMC Sample (mocap-vmc): SlotSettings_VMC_Slot1 + Builtin avatar provider を使う VMCReceiveDemo シーン。利用者プロジェクトに uOSC が必要。
- 両サンプルは単一の利用者プロジェクトに同時インポートできる。
