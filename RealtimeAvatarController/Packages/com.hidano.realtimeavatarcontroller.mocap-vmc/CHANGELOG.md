# Changelog

このパッケージのすべての注目すべき変更はこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) に基づき、バージョン番号は [Semantic Versioning](https://semver.org/spec/v2.0.0.html) に従います。

## [0.1.0] - 2026-05-09

### Added

- 旧コアパッケージ `com.hidano.realtimeavatarcontroller` から VMC Runtime 一式を移動:
  `AssemblyInfo.cs`, `EVMC4UMoCapSource.cs`, `EVMC4USharedReceiver.cs`, `VMCMoCapSourceConfig.cs`, `VMCMoCapSourceFactory.cs`, `RealtimeAvatarController.MoCap.VMC.asmdef`
- 旧コアパッケージから VMC Editor 一式を移動:
  `VmcMoCapSourceFactoryEditorRegistrar.cs`, `RealtimeAvatarController.MoCap.VMC.Editor.asmdef`
- 旧コアパッケージから VMC EditMode tests 一式を移動:
  `EVMC4UMoCapSourceTests.cs`, `EVMC4USharedReceiverTests.cs`, `ExternalReceiverPatchTests.cs`, `VmcConfigCastTests.cs`, `VmcFactoryRegistrationTests.cs`, `RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef`
- 旧コアパッケージから VMC PlayMode tests 一式を移動:
  `EVMC4UMoCapSourceIntegrationTests.cs`, `EVMC4UMoCapSourceSharingTests.cs`, `SampleSceneSmokeTests.cs`, `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef`
- VMC Sample Data を追加:
  `VMCMoCapSourceConfig_Shared.asset`, `BuiltinAvatarProviderConfig_VmcDemo.asset`, `SlotSettings_VMC_Slot1.asset`
- VMC Sample Scene を追加:
  `VMCReceiveDemo.unity`
- 動作確認済みバージョン表を追加:

  | Dependency | Verified version |
  |------------|------------------|
  | Unity | tested with Unity 6000.3.10f1 |
  | UniRx | tested with com.neuecc.unirx 7.1.0 |
  | uOSC | tested with com.hidano.uosc 1.0.0 (`uOSC.Runtime`) |
  | EVMC4U | tested with EVMC4U 3.x.x latest known compatible |
