# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-05-09

### Removed

- VMC Runtime / Editor / Tests / Sample Data moved to new package "com.hidano.realtimeavatarcontroller.mocap-vmc".

### Changed

- UI Sample asmdef references no longer reference RealtimeAvatarController.MoCap.VMC.
- SlotSettings_Shared_Slot1/2.asset now reference Stub MoCap Source instead of VMC.

### Added

- Stub MoCap Source / StubMoCapSourceConfig / StubMoCapSourceFactory in Samples~/UI/Runtime/.

### Migration

- Install the new mocap-vmc package alongside core if VMC reception is needed; see the new package README.

## [0.1.0] - 2026-05-08

### Added

- 初回 UPM パッケージリリース
  - Runtime アセンブリ: `Core` / `Avatar.Builtin` / `Motion` / `MoCap.VMC`
  - Editor アセンブリ: `Core.Editor` / `MoCap.VMC.Editor`
- Slot ベースの MoCap ソース管理機構
- アバタープロバイダ抽象化レイヤ (Builtin 実装同梱)
- リアルタイムモーションパイプライン
- EVMC4U 経由の VMC (Virtual Motion Capture) 受信機能
- UI Sample: Slot 管理 Inspector 拡張と `SlotManagementDemo.unity` デモシーン

### 動作確認済みバージョン

| パッケージ | バージョン | 取得元 | 備考 |
|------------|-----------|--------|------|
| com.neuecc.unirx | 7.1.0 | OpenUPM | NuGet 依存なし |
| com.cysharp.unitask | 2.5.10 | OpenUPM | — |
| com.hecomi.uosc | 2.2.0 | npmjs (scope: com.hecomi) | VMC OSC 受信用。MIT ライセンス |
