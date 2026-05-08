# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-04-15

### Added

- Initial package structure with UPM support
- Slot-based MoCap source management architecture
- Avatar provider abstraction layer
- Motion pipeline for Unity

### 依存パッケージ

| パッケージ | バージョン | 取得元 | 備考 |
|------------|-----------|--------|------|
| com.neuecc.unirx | 7.1.0 | OpenUPM | 2024 年時点の最新安定版。NuGet 依存なし |
| com.cysharp.unitask | 2.5.10 | OpenUPM | 2024 年時点の最新安定版 |
| com.hecomi.uosc | 2.2.0 | npmjs (scope: com.hecomi) | VMC OSC 受信用。MIT ライセンス |
