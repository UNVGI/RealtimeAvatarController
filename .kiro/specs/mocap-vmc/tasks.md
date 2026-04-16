# mocap-vmc 実装タスク

> **フェーズ**: tasks  
> **言語**: ja  
> **対応 Spec バージョン**: requirements.md (Req 1–10) + design.md (§1–§15)  
> **実行方式**: `/kiro:spec-run mocap-vmc` で全タスクをバッチ実行

---

## タスク方針

- TDD を基本とし、`EditMode` テストは実装ファイル作成前にテストを先行作成する
- `PlayMode` テストは実装完了後に UDP テストダブルを用いて統合検証する
- 各 leaf タスクには対応する要件番号を `_Requirements:_ ` で明記する
- EVMC4U 参考実装 (`gpsnmeajp/EasyVirtualMotionCaptureForUnity`, MIT) の帰属は `VmcMessageRouter.cs` のヘッダーコメントで明記する

---

## 大項目 1: パッケージ依存確認と asmdef 配置

### タスク 1-1: `com.hidano.uosc` の Packages/manifest.json 導入確認

`Packages/manifest.json` に `com.hidano.uosc 1.0.0` の scoped registry エントリおよび依存が追加されているかを確認する。未追加の場合は以下を追記する。

```json
{
  "scopedRegistries": [
    {
      "name": "npm (com.hidano)",
      "url": "https://registry.npmjs.com",
      "scopes": ["com.hidano"]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.neuecc.unirx", "com.cysharp.unitask"]
    }
  ],
  "dependencies": {
    "com.hidano.uosc": "1.0.0",
    "com.neuecc.unirx": "7.1.0"
  }
}
```

- Unity 6000.3 での動作確認チェックリスト項目として「uOSC 受信コールバックが呼ばれること」を PlayMode テストに含める (design.md §4 の Unity 6 互換性注記対応)

_Requirements: 8-6_

---

### タスク 1-2: Runtime asmdef 配置

`RealtimeAvatarController/Packages/com.example.realtime-avatar-controller/Runtime/MoCap/VMC/` ディレクトリを作成し、`RealtimeAvatarController.MoCap.VMC.asmdef` を配置する。

**依存設定**:
- `RealtimeAvatarController.Core`
- `RealtimeAvatarController.Motion`
- `com.hidano.uosc` (uOSC)
- `UniRx`

_Requirements: 8-1, 8-2, 8-3, 8-4_

---

### タスク 1-3: Editor asmdef 配置

`Editor/MoCap/VMC/` ディレクトリを作成し、`RealtimeAvatarController.MoCap.VMC.Editor.asmdef` を配置する。

**依存設定**:
- `RealtimeAvatarController.MoCap.VMC`
- `RealtimeAvatarController.Core`
- `includePlatforms: ["Editor"]`

_Requirements: 8-1, 9-8_

---

### タスク 1-4: EditMode テスト asmdef 配置

`Tests/EditMode/MoCap/VMC/` ディレクトリを作成し、`RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef` を配置する。

**依存設定**:
- `RealtimeAvatarController.MoCap.VMC`
- `RealtimeAvatarController.Core`
- `UnityEngine.TestRunner`
- `UnityEditor.TestRunner`
- `includePlatforms: ["Editor"]`

_Requirements: 10-1_

---

### タスク 1-5: PlayMode テスト asmdef 配置

`Tests/PlayMode/MoCap/VMC/` ディレクトリを作成し、`RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef` を配置する。

**依存設定**:
- `RealtimeAvatarController.MoCap.VMC`
- `RealtimeAvatarController.Core`
- `RealtimeAvatarController.Motion`
- `UnityEngine.TestRunner`

_Requirements: 10-1_

---

## 大項目 2: `VMCMoCapSourceConfig` 実装

### タスク 2-1: [TDD] Config キャスト検証テスト先行作成

`Tests/EditMode/MoCap/VMC/VmcConfigCastTests.cs` を作成する。以下のテストケースを NUnit で記述する (この時点では実装クラスが存在しないため、コンパイルエラーになってよい)。

| テストケース | 期待動作 |
|------------|---------|
| `VMCMoCapSourceConfig` を `MoCapSourceConfigBase` として `VMCMoCapSourceFactory.Create()` に渡す | 正常に `VmcMoCapSource` が生成される |
| 別の `MoCapSourceConfigBase` 派生型を渡す | `ArgumentException` がスローされ、型名がメッセージに含まれる |
| `null` を渡す | `ArgumentException` がスローされる |
| `ScriptableObject.CreateInstance<VMCMoCapSourceConfig>()` で動的生成した Config を渡す | 正常に `VmcMoCapSource` が生成される (シナリオ Y) |

_Requirements: 9-3, 9-4, 9-11, 9-12, 10-2_

---

### タスク 2-2: `VMCMoCapSourceConfig` 実装

`Runtime/MoCap/VMC/VMCMoCapSourceConfig.cs` を作成する。

**実装仕様**:
- `VMCMoCapSourceConfig : MoCapSourceConfigBase` (`ScriptableObject` 派生)
- `[CreateAssetMenu(menuName = "RealtimeAvatarController/MoCap/VMC Config", fileName = "VMCMoCapSourceConfig")]`
- フィールド:
  - `[Range(1025, 65535)] public int port = 39539;` — VMC 標準ポート
  - `public string bindAddress = "0.0.0.0";` — 全インターフェース受信 (デフォルト; requirements との差異は設計合意済み)
- `ScriptableObject.CreateInstance<VMCMoCapSourceConfig>()` によるランタイム動的生成を許容 (public フィールドで直接セット可能)
- Unity Inspector 上で `MoCapSourceDescriptor.Config` フィールドへのドラッグ&ドロップ参照設定が可能

> **Open Issue L-2 対応**: `bindAddress` のデフォルト値は `requirements.md 要件 3-3` で `127.0.0.1` と規定されているが、design.md §5.1 で `0.0.0.0` (外部 VMC 送信ソース対応) に合意変更した。本クラスのコメントにその旨を明記すること。

_Requirements: 3-1, 3-2, 3-3, 3-7, 9-1, 9-2, 9-6, 9-11, 9-12_

---

## 大項目 3: VMC Bone マッピング

### タスク 3-1: [TDD] Bone マッピングテスト先行作成

`Tests/EditMode/MoCap/VMC/VmcBoneMapperTests.cs` を作成する。

| テストケース | 期待動作 |
|------------|---------|
| Unity `HumanBodyBones` の全列挙値名 (LastBone 除く) を `TryGetBone` に渡す | すべて `true` を返し、対応する `HumanBodyBones` 値が得られる |
| 未知のボーン名 (例: `"UnknownBone"`) を渡す | `TryGetBone` が `false` を返す |
| `null` または空文字を渡す | `TryGetBone` が `false` を返す (例外をスローしない) |

_Requirements: 5-1, 10-2_

---

### タスク 3-2: `VmcBoneMapper` 実装

`Runtime/MoCap/VMC/Internal/VmcBoneMapper.cs` を作成する。

**実装仕様**:
- `internal static class VmcBoneMapper`
- 静的コンストラクタで `Dictionary<string, HumanBodyBones> s_boneMap` を初期化
- `Enum.GetValues(typeof(HumanBodyBones))` で全ボーン列挙、`HumanBodyBones.LastBone` を除外
- `StringComparer.Ordinal` による O(1) 照合
- `public static bool TryGetBone(string vmcBoneName, out HumanBodyBones bone)`

_Requirements: 5-1, 10-2_

---

## 大項目 4: OSC アドレスディスパッチ (`VmcMessageRouter`)

### タスク 4-1: [TDD] OSC パーサ単体テスト先行作成

`Tests/EditMode/MoCap/VMC/VmcOscParserTests.cs` を作成する。

| テストケース | 期待動作 |
|------------|---------|
| 正常な `/VMC/Ext/Bone/Pos` アドレスのルーティング | `VmcFrameBuilder.SetBone` が呼ばれる |
| 正常な `/VMC/Ext/Root/Pos` アドレスのルーティング | `VmcFrameBuilder.SetRoot` が呼ばれる |
| `/VMC/Ext/Blend/Val` アドレス | 例外をスローせず、フレームビルダへの呼び出しなし |
| 未知アドレス (`/VMC/Ext/Unknown`) | 例外をスローせず無視される |
| uOSC の `OscDataHandle` に引数が不足している場合 | 例外をスローせず、`PublishError` 相当の通知が行われる |

_Requirements: 5-1, 5-2, 7-2, 10-2_

---

### タスク 4-2: `VmcMessageRouter` 実装

`Runtime/MoCap/VMC/Internal/VmcMessageRouter.cs` を作成する。

**実装仕様**:
- `internal sealed class VmcMessageRouter`
- ファイル先頭ヘッダーコメントに EVMC4U 帰属を明記:
  ```csharp
  // VMC プロトコルの OSC アドレスハンドリング構造は以下を参考に実装:
  // gpsnmeajp/EasyVirtualMotionCaptureForUnity (EVMC4U)
  // https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity
  // Copyright (c) 2019 gpsnmeajp, MIT License
  ```
- `void Route(string address, OscDataHandle data)` メソッド:
  - `switch (address)` で以下をハンドリング:
    - `"/VMC/Ext/Root/Pos"` → `_frameBuilder.SetRoot(data)`
    - `"/VMC/Ext/Bone/Pos"` → `_frameBuilder.SetBone(data)`
    - `"/VMC/Ext/Blend/Val"` → 初期版: 受信のみ・変換スキップ
    - `"/VMC/Ext/Blend/Apply"` → 初期版: 受信のみ・変換スキップ
    - `"/VMC/Ext/OK"` → スキップ (ログのみ)
    - `"/VMC/Ext/T"` → 使用しない (VMC v2.5 の不安定タイムスタンプ)
    - `default` → 無視
- 引数不足・型不一致時は例外をキャッチして呼び出し元へ伝播 (`VmcOscAdapter` が `PublishError` を担う)

_Requirements: 5-1, 5-2, 5-4, 7-2_

---

## 大項目 5: `VmcFrameBuilder` (HumanoidMotionFrame 構築)

### タスク 5-1: `VmcFrameBuilder` 実装

`Runtime/MoCap/VMC/Internal/VmcFrameBuilder.cs` を作成する。

**実装仕様**:
- `internal sealed class VmcFrameBuilder`
- 内部に `Dictionary<HumanBodyBones, (Vector3, Quaternion)>` でボーンデータを蓄積
- `void SetRoot(OscDataHandle data)`:
  - `/VMC/Ext/Root/Pos` 引数: `(string name, float px, float py, float pz, float qx, float qy, float qz, float qw)`
  - `rootPosition` / `rootRotation` を更新する
- `void SetBone(OscDataHandle data)`:
  - `/VMC/Ext/Bone/Pos` 引数: `(string boneName, float px, float py, float pz, float qx, float qy, float qz, float qw)`
  - `VmcBoneMapper.TryGetBone(boneName, out var bone)` で HumanBodyBones に変換
  - 辞書へ `(position, rotation)` を蓄積
- `bool TryFlush(out HumanoidMotionFrame frame)`:
  - フレームフラッシュ判定 (設計 OI-1: 初期版は `/VMC/Ext/Bone/Pos` 受信ごとにフラッシュを試みる簡易実装)
  - Muscles 配列組み立て (`HumanTrait.MuscleCount` = 95 固定長):
    - 受信済みボーンの回転クォータニオンから `HumanTrait.MuscleFromBone` を活用して `float[]` 変換
    - 未受信ボーンのインデックスは `0.0f` (アイドルポーズ = ゼロ回転) でゼロ埋め
  - `timestamp` 打刻: `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency`
  - `new HumanoidMotionFrame(timestamp, muscles, rootPosition, rootRotation)` を生成して返す
  - フラッシュ後はボーン蓄積辞書をリセット (次フレームに備える)
- 無効フレーム (bones 未受信) の場合は `TryFlush` が `false` を返す

_Requirements: 2-7, 5-1, 5-3, 5-4, 7-3_

---

## 大項目 6: `VmcOscAdapter` (uOSC コールバック受信)

### タスク 6-1: `VmcOscAdapter` 実装

`Runtime/MoCap/VMC/Internal/VmcOscAdapter.cs` を作成する。

**実装仕様**:
- `internal sealed class VmcOscAdapter`
- uOSC (`com.hidano.uosc`) が提供する受信コールバックに登録し、受信した `address` + `OscDataHandle` を `VmcMessageRouter.Route()` へ転送する薄いアダプタ層
- コールバック (`OnOscMessageReceived`) 内の処理フロー:
  1. `_router.Route(address, data)`
  2. `if (_frameBuilder.TryFlush(out var frame)) { _subject.OnNext(frame); }`
  3. 例外は `try-catch` で全捕捉 → `_errorHandler(SlotErrorCategory.VmcReceive, ex)` に委譲
- `Initialize(bindAddress, port)`: uOSC の受信オブジェクトを初期化し、コールバックを登録する
  - `SO_REUSEADDR` は `com.hidano.uosc` 側で有効化済みのため追加対応不要
  - ポートバインド失敗時は `SocketException` を伝播する
- `Shutdown()`: uOSC 受信を停止し、コールバックを解除する

_Requirements: 2-1, 2-2, 2-5, 2-6, 3-4, 3-5, 7-2_

---

## 大項目 7: `VmcMoCapSource` 実装

### タスク 7-1: `VmcMoCapSource` 骨格実装

`Runtime/MoCap/VMC/VmcMoCapSource.cs` を作成する。

**実装仕様**:
- `public sealed class VmcMoCapSource : IMoCapSource`
- `IDisposable` を実装 (`Shutdown()` と等価)
- `internal VmcMoCapSource(string slotId, ISlotErrorChannel errorChannel)` コンストラクタ
- `string SourceType => "VMC"`
- 内部状態列挙: `Uninitialized / Running / Disposed`

_Requirements: 1-1, 1-2, 4-5_

---

### タスク 7-2: UniRx Subject とマルチキャストストリーム実装

`VmcMoCapSource.cs` 内に以下を実装する。

**実装仕様**:
- `private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();`
- `private readonly ISubject<MotionFrame> _subject;` — `_rawSubject.Synchronize()` で初期化
- `private readonly IObservable<MotionFrame> _stream;` — `_subject.Publish().RefCount()` で初期化
- `public IObservable<MotionFrame> MotionStream => _stream;`
- コンストラクタ内で `_subject` / `_stream` を初期化する

_Requirements: 1-5, 1-6, 2-5_

---

### タスク 7-3: `Initialize(MoCapSourceConfigBase config)` 実装

`VmcMoCapSource.cs` に `Initialize` メソッドを実装する。

**処理フロー** (design.md §9.2 準拠):
1. 状態チェック: `Uninitialized` 以外であれば `InvalidOperationException` をスロー
2. `config as VMCMoCapSourceConfig` でキャスト: `null` であれば `ArgumentException` をスロー (型名をメッセージに含める)
3. ポート番号バリデーション (1025〜65535): 範囲外であれば `ArgumentOutOfRangeException` をスロー
4. `VmcOscAdapter.Initialize(bindAddress, port)` を呼び出し、uOSC 受信を開始
   - `SocketException` (ポート競合) は伝播する (呼び出し元 SlotManager が `InitFailure` カテゴリで通知)
5. 内部状態を `Running` に遷移

_Requirements: 1-3, 3-2, 3-3, 3-4, 3-5, 4-5, 7-4, 9-2, 9-3, 9-4_

---

### タスク 7-4: `Shutdown()` / `Dispose()` 実装

`VmcMoCapSource.cs` に `Shutdown` / `Dispose` メソッドを実装する。

**処理フロー** (design.md §9.3 準拠):
1. 状態チェック: `Disposed` であれば即時 return (冪等)
2. `VmcOscAdapter.Shutdown()` でソケット閉鎖・受信停止
3. `_rawSubject.OnCompleted()` でストリームを終端 (購読者への `OnCompleted` 通知)
4. `_rawSubject.Dispose()` でリソース解放
5. 内部状態を `Disposed` に遷移

_Requirements: 1-4, 4-5, 8-3_

---

### タスク 7-5: エラーハンドリング (`PublishError`) 実装

`VmcMoCapSource.cs` に `PublishError` ヘルパーを実装する。

**実装仕様**:
- `private void PublishError(SlotErrorCategory category, Exception ex)`
- `_errorChannel.Publish(new SlotError(_slotId, category, ex, DateTime.UtcNow))` を呼び出す
- `Debug.LogError` の抑制ロジックは `DefaultSlotErrorChannel` 側が担うため、`VmcMoCapSource` 側には抑制制御を持たない
- `MotionStream` の `OnError()` は一切発行しない

_Requirements: 7-1, 7-2, 7-3, 7-5_

---

## 大項目 8: `VMCMoCapSourceFactory` 実装

### タスク 8-1: [TDD] 属性ベース自己登録確認テスト先行作成

`Tests/EditMode/MoCap/VMC/VmcFactoryRegistrationTests.cs` を作成する。

| テストケース | 期待動作 |
|------------|---------|
| `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に `"VMC"` が含まれる | `[SetUp]` で `RegistryLocator.ResetForTest()` → 手動で `RegisterRuntime` 相当を呼び出し → `"VMC"` が含まれる |
| 同一 `typeId="VMC"` の二重登録 | `RegistryConflictException` がスローされる |
| `RegistryLocator.ResetForTest()` 後の再登録 | 正常に `"VMC"` で登録できる |
| `[TearDown]` で `RegistryLocator.ResetForTest()` を呼び出す | 他テストへの副作用がない |

_Requirements: 9-5, 9-7, 9-8, 9-9, 9-10, 10-2, 10-5_

---

### タスク 8-2: `VMCMoCapSourceFactory` 実装 (Runtime ランタイム自己登録)

`Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs` を作成する。

**実装仕様**:
- `public sealed class VMCMoCapSourceFactory : IMoCapSourceFactory`
- `public IMoCapSource Create(MoCapSourceConfigBase config)`:
  - `var vmcConfig = config as VMCMoCapSourceConfig`
  - `null` の場合: `throw new ArgumentException($"VMCMoCapSourceConfig が必要ですが {config?.GetType().Name ?? "null"} が渡されました", nameof(config))`
  - 正常: `new VmcMoCapSource(slotId: string.Empty, errorChannel: RegistryLocator.ErrorChannel)` を返す
- ランタイム自己登録:
  ```csharp
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void RegisterRuntime()
  {
      try { RegistryLocator.MoCapSourceRegistry.Register("VMC", new VMCMoCapSourceFactory()); }
      catch (RegistryConflictException ex)
      { RegistryLocator.ErrorChannel.Publish(SlotErrorCategory.RegistryConflict, ex, "..."); }
  }
  ```

_Requirements: 9-3, 9-4, 9-5, 9-7, 9-9, 9-10_

---

### タスク 8-3: `VmcMoCapSourceFactoryEditorRegistrar` 実装 (Editor 自己登録)

`Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs` を作成する。

**実装仕様**:
- `#if UNITY_EDITOR` ガード内 (`RealtimeAvatarController.MoCap.VMC.Editor` asmdef に配置)
- `internal static class VmcMoCapSourceFactoryEditorRegistrar`
- `[UnityEditor.InitializeOnLoadMethod]` 属性の静的メソッドで `RegistryLocator.MoCapSourceRegistry.Register("VMC", new VMCMoCapSourceFactory())` を呼び出す
- `RegistryConflictException` は try-catch で捕捉し、`RegistryLocator.ErrorChannel.Publish(SlotErrorCategory.RegistryConflict, ...)` でログ出力 (握り潰さない)

_Requirements: 9-5, 9-8, 9-9_

---

## 大項目 9: 参照共有モデルの使用側確認

### タスク 9-1: `IMoCapSourceRegistry.Resolve()` 参照共有挙動の理解確認

> **Note**: `MoCapSourceRegistry` 自体の実装は `slot-core` Spec の責務であり、本タスクでは `mocap-vmc` 側の使用方法が設計と整合していることを確認する。

確認事項:
- `VmcMoCapSource` の `Dispose()` は `public` で公開されているが、Slot 側が直接呼び出さず `IMoCapSourceRegistry.Release()` 経由で解放される
- `VMCMoCapSourceFactory.Create()` が `slotId: string.Empty` で `VmcMoCapSource` を生成し、Registry が後から `slotId` をセットする設計 (design.md §10.1 参照)
- `VmcMoCapSource` コンストラクタの `slotId` パラメータが Registry による後設定を許容する設計になっているか確認し、必要に応じて `internal` setter を追加する

_Requirements: 4-1, 4-2, 4-3, 4-4_

---

## 大項目 10: EditMode テスト完成

### タスク 10-1: `VmcOscParserTests.cs` テスト実装完成

タスク 4-1 で先行作成したテストファイルを、タスク 4-2 (`VmcMessageRouter`) 実装完了後に完成させる。モックまたはスタブを用いて `VmcFrameBuilder` との協調動作を検証する。

_Requirements: 5-1, 5-2, 7-2, 10-2_

---

### タスク 10-2: `VmcConfigCastTests.cs` テスト実装完成

タスク 2-1 で先行作成したテストファイルを、タスク 2-2 (`VMCMoCapSourceConfig`) および タスク 8-2 (`VMCMoCapSourceFactory`) 実装完了後に完成させる。`RegistryLocator.ResetForTest()` を `[SetUp]`/`[TearDown]` で使用してテスト独立性を確保する。

_Requirements: 9-3, 9-4, 9-11, 9-12, 10-2, 10-5_

---

### タスク 10-3: `VmcBoneMapperTests.cs` テスト実装完成

タスク 3-1 で先行作成したテストファイルを、タスク 3-2 (`VmcBoneMapper`) 実装完了後に完成させる。

_Requirements: 5-1, 10-2_

---

### タスク 10-4: `VmcFactoryRegistrationTests.cs` テスト実装完成

タスク 8-1 で先行作成したテストファイルを、タスク 8-2/8-3 実装完了後に完成させる。

_Requirements: 9-5, 9-7, 9-8, 9-9, 9-10, 10-2, 10-5_

---

## 大項目 11: PlayMode テスト

### タスク 11-1: `UdpOscSenderTestDouble` 実装

`Tests/PlayMode/MoCap/VMC/UdpOscSenderTestDouble.cs` を作成する。

**実装仕様**:
- `internal sealed class UdpOscSenderTestDouble : IDisposable`
- コンストラクタ: `UdpOscSenderTestDouble(int targetPort)` — `IPAddress.Loopback:targetPort` へ UDP 送信するクライアントを生成
- `void SendRootPos(Vector3 position, Quaternion rotation)` — `/VMC/Ext/Root/Pos` を OSC エンコードして送信
- `void SendBonePos(string boneName, Vector3 position, Quaternion rotation)` — `/VMC/Ext/Bone/Pos` を送信
- `void SendInvalidPacket()` — 不正バイト列を送信 (パースエラーテスト用)
- `void Dispose()` — `UdpClient.Dispose()`
- `[SetUp]` で生成、`[TearDown]` で `Dispose` する設計

_Requirements: 10-3_

---

### タスク 11-2: `VmcMoCapSourceIntegrationTests.cs` 実装

`Tests/PlayMode/MoCap/VMC/VmcMoCapSourceIntegrationTests.cs` を作成する。

| テストケース | 検証内容 |
|------------|---------|
| ローカル UDP 送信 → `MotionStream` 受信 | `UdpOscSenderTestDouble` が送信したパケットが `MotionStream` に届く |
| Root / Bone データの往復正確性 | 送信した位置・回転と受信 `HumanoidMotionFrame` の値が一致する |
| `timestamp` の単調増加 | 連続受信フレームの `Timestamp` が単調増加している |
| パースエラー時にストリームが継続する | 不正パケット後も次の正常パケットが `MotionStream` に届く (`OnError` が発行されないことも確認) |
| `Shutdown()` 後に `MotionStream` が完了する | `OnCompleted()` が発行される |
| Unity 6000.3 での uOSC 受信コールバック呼び出し確認 | `com.hidano.uosc` の UDP 受信コールバックが実際に呼ばれ、フレームが届くことを確認 (タスク 1-1 の互換性検証) |

**テスト実装方針**:
- `UniRx` の `ToList()` + `Timeout()` を用いてフレーム受信を非同期待機する
- ポート番号は `45678` (テスト専用固定ポート) を使用し、`[SetUp]`/`[TearDown]` で `VmcMoCapSource` を初期化・破棄する
- `RegistryLocator.ResetForTest()` をテスト間で呼び出してテスト独立性を確保する

_Requirements: 2-1, 2-2, 2-7, 5-1, 5-2, 7-1, 7-2, 10-3, 10-4, 10-5_

---

## 大項目 12: 最終統合確認

### タスク 12-1: asmdef 依存関係の最終確認

すべての asmdef が design.md §14 の依存関係テーブルと一致していることを確認する。循環依存がないことを Unity Editor のアセンブリ定義 Inspector で目視確認する。

_Requirements: 8-1, 8-2, 8-3_

---

### タスク 12-2: 名前空間・ファイル配置の最終確認

design.md §14 のファイル/ディレクトリ構成と実際の配置が一致していることを確認する。

| ファイル | 配置パス |
|---------|---------|
| `VmcMoCapSource.cs` | `Runtime/MoCap/VMC/` |
| `VMCMoCapSourceConfig.cs` | `Runtime/MoCap/VMC/` |
| `VMCMoCapSourceFactory.cs` | `Runtime/MoCap/VMC/` |
| `VmcFrameBuilder.cs` | `Runtime/MoCap/VMC/Internal/` |
| `VmcMessageRouter.cs` | `Runtime/MoCap/VMC/Internal/` |
| `VmcBoneMapper.cs` | `Runtime/MoCap/VMC/Internal/` |
| `VmcOscAdapter.cs` | `Runtime/MoCap/VMC/Internal/` |
| `VmcMoCapSourceFactoryEditorRegistrar.cs` | `Editor/MoCap/VMC/` |

名前空間は `RealtimeAvatarController.MoCap.VMC` (Internal クラスは同名前空間の `internal`) であることを確認する。

_Requirements: 8-1, 8-2_

---

### タスク 12-3: EVMC4U 帰属コメントの存在確認

`VmcMessageRouter.cs` ファイル先頭に以下の帰属コメントが存在することを確認する:

```csharp
// VMC プロトコルの OSC アドレスハンドリング構造は以下を参考に実装:
// gpsnmeajp/EasyVirtualMotionCaptureForUnity (EVMC4U)
// https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity
// Copyright (c) 2019 gpsnmeajp, MIT License
```

_Requirements: 8-6 (design.md §4 帰属義務)_

---

## タスク実行順序 (推奨)

```
Phase A (TDD 先行テスト作成):
  タスク 1-4 → 1-5 → 2-1 → 3-1 → 4-1 → 8-1

Phase B (asmdef・パッケージ基盤):
  タスク 1-1 → 1-2 → 1-3

Phase C (内部実装):
  タスク 2-2 → 3-2 → 4-2 → 5-1 → 6-1

Phase D (主クラス実装):
  タスク 7-1 → 7-2 → 7-3 → 7-4 → 7-5

Phase E (Factory 実装):
  タスク 8-2 → 8-3

Phase F (参照共有確認):
  タスク 9-1

Phase G (EditMode テスト完成):
  タスク 10-1 → 10-2 → 10-3 → 10-4

Phase H (PlayMode テスト):
  タスク 11-1 → 11-2

Phase I (最終確認):
  タスク 12-1 → 12-2 → 12-3
```
