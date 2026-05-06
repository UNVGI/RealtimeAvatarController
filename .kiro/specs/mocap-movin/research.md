# Research & Design Decisions

## Summary

- **Feature**: `mocap-movin`
- **Discovery Scope**: Extension (本体パッケージ非改変、既存拡張点 `IMoCapSource` / `IMoCapSourceFactory` / `MoCapSourceConfigBase` / `RegistryLocator` を使用する新規外部 UPM パッケージ追加)
- **Key Findings**:
  - 本体パッケージは MoCap ソースの拡張点 (typeId による登録 / Factory / Config ScriptableObject) を完全な形で公開しており、改変なしで MOVIN を追加できる。
  - MOVIN プロトコルは VMC 互換 OSC over UDP だが、(a) デフォルトポート `11235`、(b) Generic Transform 直接書き込み、(c) `prefix:boneName` のフィルタ、(d) `/VMC/Ext/Root/Pos` の v2.1 拡張 (`localScale` / `localOffset`) の 4 点で標準 VMC / EVMC4U と異なる。
  - 既存 EVMC4U 実装は `EVMC4U.ExternalReceiver` (Humanoid 前提) に強く依存しており、共有 Receiver `EVMC4USharedReceiver` も Humanoid Receiver をホストする。MOVIN は OSC レイヤーから独自に組み立てるのが筋が通る (Receiver 共有ではなくパッケージ内に独立した OSC 受信ホストを持つ)。
  - `com.hidano.uosc` の `uOscServer` は MonoBehaviour ベースで、UDP 受信ワーカースレッドから queue され、メイン Update で `onDataReceived` が発火する。Adapter 側は受信ハンドラに購読し、Tick (LateUpdate) で snapshot して emit する 2 段階モデルが本体 EVMC4U 実装と整合する。
  - `MotionFrame` 階層は `Core.MotionFrame` 抽象 → `Motion.MotionFrame` (Timestamp / SkeletonType) → `Humanoid` / `Generic`。`Generic` はプレースホルダで具象フィールドを持たない。MOVIN は `Generic` 直系の派生としてではなく、`Motion.MotionFrame` 直系の本パッケージ独自具象 (`MovinMotionFrame`) として定義し、`SkeletonType.Generic` を返す方針が合理的 (Motion.GenericMotionFrame は契約上抽象であり実装を強制されないため、本 spec で具象を作れば本体の `motion-pipeline` 進化を妨げない)。

## Research Log

### Topic: 本体拡張点の確認 (slot-core / motion-pipeline)
- **Context**: 本体非改変の制約下で MOVIN を統合する経路を確定する必要がある。
- **Sources Consulted**:
  - `Runtime/Core/Interfaces/IMoCapSource.cs`
  - `Runtime/Core/Configs/MoCapSourceConfigBase.cs`
  - `Runtime/Core/Factory/IMoCapSourceFactory.cs`
  - `Runtime/Core/Descriptors/MoCapSourceDescriptor.cs`
  - `Runtime/Core/Locator/RegistryLocator.cs`
  - `Runtime/Core/Registry/DefaultMoCapSourceRegistry.cs`
  - `Runtime/Core/Slot/SlotManager.cs`
  - `Runtime/Core/Slot/SlotSettings.cs`
  - `Runtime/MoCap/VMC/VMCMoCapSourceFactory.cs`
  - `Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs`
- **Findings**:
  - `IMoCapSource` の `MotionStream` は `IObservable<RealtimeAvatarController.Core.MotionFrame>` を返す。
  - `MoCapSourceRegistry` は Descriptor (typeId + Config 参照) で Resolve / 参照カウントで Release。同一 Descriptor 共有可。
  - `RegistryLocator.ResetForTest()` は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で先行実行され、Domain Reload OFF 環境でも各 Factory の `BeforeSceneLoad` 自己登録より前にクリアされる。
  - `SlotManager.AddSlotAsync` 内で `IMoCapSource.Initialize(config)` が呼ばれ、`SocketException` 等が発生すると `InitFailure` カテゴリで `ISlotErrorChannel` に発行される (本体側の責務)。
  - `SlotManager.TryGetSlotResources(slotId, out source, out avatar)` で Sample 駆動コンポーネントが Source / Avatar を取り回す。
- **Implications**:
  - 本パッケージが提供すべきものは「Factory + Source + Config + MotionFrame + Applier + Sample 駆動 MonoBehaviour」のみ。本体は触らない。
  - Factory 自己登録は VMC と同形 (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + Editor 別ファイルで `[InitializeOnLoadMethod]`) を踏襲する。
  - `MovinMotionApplier` は `IMotionApplier` を実装する必要は無い (本体 motion-pipeline は Humanoid 想定のため)。本パッケージ独自 API として供給する。

### Topic: MOVIN プロトコルと標準 VMC の差分
- **Context**: 受信実装の責務範囲を確定する。
- **Sources Consulted**:
  - `Assets/MOVIN/Scripts/Core/VMCReceiver.cs` (Listen Port 既定 `11235`、`/VMC/Ext/Bone/Pos` `/VMC/Ext/Root/Pos` v2.0/2.1 引数解析を含む)
  - `Assets/MOVIN/Scripts/Core/MocapReceiver.cs` (`rootBoneName` / `boneClass` フィルタ + Transform 直接書込)
  - 公式: <https://help.movin3d.com/movin-studio-usage-guide/live-streaming/streaming-mocap-data-into-unity>
  - VMC 仕様: <https://protocol.vmc.info/english.html>
- **Findings**:
  - MOVIN は VMC over OSC 互換だが、`mixamorig:Hips` 等の prefix 付きボーン名で送出し、Humanoid bone enum へのリターゲットを行わない設計。
  - `/VMC/Ext/Root/Pos` v2.1 拡張は `localScale (xyz)` / `localOffset (xyz)` を末尾に追加。両者は optional。
  - Sample の `MocapReceiver` は OnBonePose / OnRootPose イベントから直接 `name2Transform` lookup で `SetLocalPositionAndRotation` を呼ぶ (Humanoid HumanPoseHandler 経路を通らない)。
  - 受信側 listen port は 1 接続 1 ポート前提だが、複数 Slot から同一 Config を共有する場合は本体 Registry の参照カウント機構で 1 つの Source インスタンスに集約される。
- **Implications**:
  - MOVIN MoCapSource は `/VMC/Ext/Bone/Pos` と `/VMC/Ext/Root/Pos` のみ取り扱う (Blend / Cam / HMD / Tracker / Time は無視)。
  - `MovinMotionFrame` は (a) ボーン名キーの bone Transform 辞書と (b) optional Root pose (boneName, position, rotation, scale, offset) を保持する 2 部構造とする。
  - Applier は name 一致 + boneClass prefix フィルタ + rootBoneName 起点探索を実装するが、これらは Sample `MocapReceiver` のロジックを踏襲。

### Topic: uOSC API と受信スレッドモデル
- **Context**: 受信ワーカースレッドと Unity メインスレッドの境界、Frame 発行タイミングを確定する。
- **Sources Consulted**:
  - 本体側 `EVMC4USharedReceiver.cs` (`uOscServer.autoStart=false`、`StopServer()`/`StartServer()` での明示的 bind / re-bind)
  - 本体側 `EVMC4UMoCapSource.cs` (LateUpdate Tick で snapshot → `Subject.OnNext`、Tick 内 try/catch + `ISlotErrorChannel` 通知)
  - 本体側 `EVMC4U/ExternalReceiver.cs` (`server.onDataReceived.AddListener(OnDataReceived)` パターンを確認)
  - `com.hidano.uosc` 1.0.0 (`Packages/manifest.json` の依存性)
- **Findings**:
  - `uOscServer` は `MonoBehaviour` 派生で、UDP 受信は内部ワーカースレッドだが `onDataReceived` の `Invoke` は MonoBehaviour `Update()` から `parser_.Dequeue()` 経由で**メインスレッド上で発火** (要件 10-1 に明記)。
  - 本体 EVMC4U 実装は `EVMC4USharedReceiver` 1 個に複数 Adapter を Subscribe して LateUpdate で Tick する共有モデル。MOVIN は別 port で別インスタンスを持つため、共有 Receiver パターンは流用できるが**インスタンスは MOVIN 専用**になる (HumanoidExternalReceiver を抱え込む必要がない)。
  - `uOscServer` は `bindAddress` を公開していないため (本体側コメント参照)、`MovinMoCapSourceConfig.bindAddress` は情報フィールド扱いで実 bind は全インターフェースとなる。
- **Implications**:
  - MOVIN は `MovinSharedOscReceiver` (内部) を持つ。複数 Slot から同一 port が要求された場合は共有し、参照カウント管理する。ただし本体 Registry が同一 Descriptor (typeId="MOVIN", 同一 Config) に対しては既に 1 インスタンスを共有するため、port 共有は概ね Config-level で解決される。port 衝突 (異なる Config が同 port を指す) は許容しないか、低レベルで bind 失敗を許容する設計。MVP では「Config 1 個 → uOSC Server 1 個」を単純に保ち、port 衝突時は SocketException を呼び出し元へ伝播 (要件 10-6 / 14-3)。
  - 受信スレッドモデルは方式 A (uOSC 内部 Update→onDataReceived → 内部キャッシュ書込 + LateUpdate Tick で snapshot → Subject.OnNext) を採用 (要件 10-1, 10-2, 10-3)。
  - Subject は `Subject<MotionFrame>.Synchronize().Publish().RefCount()` (本体 EVMC4U と同形) でマルチキャスト化。

### Topic: MotionFrame 継承元の選択 (Core.MotionFrame vs Motion.MotionFrame vs Generic)
- **Context**: 要件 2-1 / 7-1 で「Core.MotionFrame または Motion.MotionFrame のいずれかを継承する」と規定されており、design で確定する必要がある。
- **Sources Consulted**:
  - `Runtime/Core/Interfaces/MotionFrame.cs` (placeholder, Timestamp 無し)
  - `Runtime/Motion/Frame/MotionFrame.cs` (Timestamp / SkeletonType)
  - `Runtime/Motion/Frame/GenericMotionFrame.cs` (空のプレースホルダ抽象、SkeletonType=Generic を返す)
  - `Runtime/Motion/Frame/HumanoidMotionFrame.cs` (Muscles + Root + BoneLocalRotations)
- **Findings**:
  - `Core.MotionFrame` は型プレースホルダでプロパティを持たない。
  - `Motion.MotionFrame` は `Timestamp` / `SkeletonType` を提供し、`Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency` を契約として規定。
  - `Motion.GenericMotionFrame` は `SkeletonType.Generic` を返すだけの抽象で、フィールドは将来用プレースホルダ。本 spec で具象化することは可能だが、本体 `motion-pipeline` の将来仕様に縛られる懸念がある。
- **Decision**: 後述「Design Decision: MotionFrame 継承元」参照。
- **Implications**:
  - 採用: `Motion.MotionFrame` を直接継承し、`SkeletonType => SkeletonType.Generic` を override する独自具象 `MovinMotionFrame` を本パッケージ内に定義。`Motion.GenericMotionFrame` は将来 `motion-pipeline` 側で具象化されたら継承元差し替えを検討する (互換維持の責務は無し、別 spec)。

### Topic: Applier 設計 (HumanoidMotionApplier 非依存)
- **Context**: 要件 2-3 / 8-* で MOVIN 専用 Applier を本パッケージ内に閉じる必要がある。
- **Sources Consulted**:
  - `Assets/MOVIN/Scripts/Core/MocapReceiver.cs` (armature 探索 / boneClass フィルタ / `SetLocalPositionAndRotation` + 任意 `localScale`)
  - 本体 `Runtime/Motion/Applier/HumanoidMotionApplier.cs` (HumanPose ベース、本 spec では参照禁止)
- **Findings**:
  - サンプル `MocapReceiver` の armature 探索ロジックは: `rootBoneName` 指定があれば優先、無ければ「Renderer を持たないが兄弟に Renderer を持つ Transform」を armature と推定する経験則。
  - `boneClass` 指定があれば Construct 時に `${boneClass}:` プレフィックスフィルタを適用し、name2Transform に登録する Transform を絞り込む。
  - `OnBonePose` / `OnRootPose` 受信時に `name2Transform` lookup で 1:1 適用、未一致は黙ってスキップ。
- **Implications**:
  - `MovinMotionApplier` は既存 Sample のロジックを移植して以下を提供:
    - `SetAvatar(Transform avatarRoot, string rootBoneName, string boneClass)`
    - `Apply(MovinMotionFrame frame)`
    - `IDisposable.Dispose()` (内部辞書解放)
  - 本体 `IMotionApplier` は実装しない (Humanoid 前提シグネチャ依存を避ける)。Sample 駆動 MonoBehaviour で `MovinMoCapSource.MotionStream` を購読 → MainThread 同期 → `MovinMotionApplier.Apply` を直接呼ぶ。

### Topic: Slot との結線方式
- **Context**: 要件 9 で Slot ⇔ Source ⇔ Applier の結線を Sample / Runtime コンポーネントで提供することが規定。
- **Sources Consulted**:
  - 本体 Sample `Samples~/UI/Runtime/SlotManagerBehaviour.cs` (LateUpdate で `MotionCache.LatestFrame` → `HumanoidMotionApplier.Apply` を `SlotManager.ApplyWithFallback` 経由で呼ぶパターン)
  - `SlotManager.TryGetSlotResources` で Source / Avatar を取り回す。
- **Findings**:
  - 本体 Sample の Pipeline は `MotionCache + HumanoidMotionApplier` の 2 段。MOVIN ではこの組を `MotionCache + MovinMotionApplier` に置き換える経路が必要。`MotionCache` は `MotionFrame` (Core 抽象) を保持するので MOVIN フレームでも問題なく流れる (`MovinMotionFrame` も Core.MotionFrame 派生)。
  - ただし MOVIN の MotionFrame は型キャストが必要で、`MotionCache.LatestFrame` を `MovinMotionFrame` にキャストして `MovinMotionApplier.Apply` に渡す責務を Sample 駆動側に持たせる。
- **Decision**: Sample 専用ドライバ MonoBehaviour `MovinSlotDriver` (仮) を Samples~/MOVIN に配置し、本 Runtime asmdef にはヘッドレス Bridge `MovinSlotBridge` (Pure C# クラス) を提供する 2 層構造とする (Sample 側 MonoBehaviour は薄い MonoBehaviour で `MovinSlotBridge` を駆動)。

### Topic: 並行稼動・Domain Reload OFF
- **Context**: 要件 6-5 / 14-1〜14-5 で VMC との並行稼動と Domain Reload OFF 配慮が必要。
- **Sources Consulted**:
  - `RegistryLocator.ResetForTest()` は SubsystemRegistration で先行実行されるため、Factory 側で追加対応不要。
  - 本体 EVMC4USharedReceiver の `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で static フィールドをリセットしている。
- **Findings**:
  - MOVIN 側にも独自共有 Receiver を持たせる場合、同様に `SubsystemRegistration` で static リセットが必要。
  - typeId="MOVIN" は VMC と別 typeId なので Registry レベルで衝突しない。port が一致しない限り並行稼動可。
- **Implications**:
  - `MovinSharedOscReceiver` (内部) を作る場合は EVMC4USharedReceiver と同形の static リセット + RefCount + DontDestroyOnLoad パターンを踏襲。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| A: 本体 EVMC4USharedReceiver を流用 | EVMC4U Humanoid Receiver の共有インスタンスへ MOVIN Adapter を相乗り | 既存パターン再利用 | EVMC4U は Humanoid 前提 / Model フィールド等を保持。MOVIN にとって不要な Humanoid 経路を引き連れる。本体非改変制約の中で MOVIN 用の OSC アドレスを別ハンドラに分離するのが困難 | 採用しない |
| B: パッケージ内独自 Receiver (uOscServer 直接 wrap) | `MovinSharedOscReceiver` を本パッケージ内に定義し uOSC を直接 host する | EVMC4U に依存しない。Humanoid 経路を持ち込まない。要件 11-6 (VMC 型参照禁止) を自然に満たす | 受信スレッドモデルの実装を本パッケージ側で自前管理 (テスト範囲が増える) | **採用** |
| C: MoCapSource ごとに uOscServer 1 個 | 共有しない。Source 1 個 = MonoBehaviour 1 個 = uOscServer 1 個 | 最も単純 | 同一 port を要求する複数 Descriptor が来た場合に bind 衝突する。本体 Registry が同一 Descriptor 共有を保証する以上、ほぼ問題は無いが、`MoCapSourceRegistry` が共有しないテストパスでは衝突リスク | 部分採用 (Source 自体が MonoBehaviour ではなく Pure C# としつつ、内部の uOscServer ホスト用 MonoBehaviour 1 個を確保する形に簡略化) |

最終的に **B** を採用しつつ MVP では「Config 1 個 = port 1 個 = uOscServer 1 個」(C 寄り) を運用上のデフォルト方針とする。本体 Registry の参照共有機構が同一 Descriptor を 1 インスタンスに集約する以上、共有 Receiver の並行受信機能は当面不要。`MovinSharedOscReceiver` という名称ではなく `MovinOscReceiverHost` として位置づけ、参照カウント実装は MVP 範囲で必要最小限 (1 ホスト = 1 Source のため、参照共有の追加機構は不要)。

## Design Decisions

### Decision: MotionFrame 継承元
- **Context**: 要件 2-1 / 7-1 で `Core.MotionFrame` か `Motion.MotionFrame` のどちらを継承するか確定が必要。
- **Alternatives Considered**:
  1. `Core.MotionFrame` を直接継承 — 軽量だが Timestamp / SkeletonType を本パッケージで再実装する必要がある。
  2. `Motion.MotionFrame` を継承 — Timestamp / SkeletonType を再利用できる。
  3. `Motion.GenericMotionFrame` を継承 — 将来 motion-pipeline 側が GenericMotionFrame に具象フィールドを追加した場合に互換破壊の可能性がある。
- **Selected Approach**: `Motion.MotionFrame` を直接継承し、`SkeletonType` を `SkeletonType.Generic` で override する `MovinMotionFrame` を定義する。
- **Rationale**:
  - `Timestamp` の打刻ルール (Stopwatch ベース) を本体と統一できる。
  - `Motion.GenericMotionFrame` 継承は本体 motion-pipeline の進化を縛るため避ける (本体非改変制約の精神に反する)。
  - `Core.MotionFrame` 直系は再実装コストが嵩む。
- **Trade-offs**: `Motion.MotionFrame` 継承は本パッケージが `RealtimeAvatarController.Motion` asmdef に依存することを意味するが、これは要件 11-2 で許容済み。
- **Follow-up**: 将来 `Motion.GenericMotionFrame` が具象化された場合に継承元を差し替えるかは別 spec で判断。

### Decision: 受信ホストの粒度 (共有 vs Source 個別)
- **Context**: 要件 9-5 で同一 Config を共有する複数 Slot は 1 つの UDP バインドにする必要がある。
- **Alternatives Considered**:
  1. EVMC4USharedReceiver と同形の共有受信ホスト (refCount 管理)
  2. Source 1 個 = uOscServer 1 個 (シンプル、ただし共有不可)
  3. Source は Pure C# 化し、内部に uOscServer をホストする MonoBehaviour 1 個を抱える
- **Selected Approach**: 3 — `MovinMoCapSource` は Pure C# (IMoCapSource, IDisposable)、`Initialize` 内で `MovinOscReceiverHost` (内部 MonoBehaviour) を生成・保持する。Source 1 個 = ホスト 1 個。
- **Rationale**:
  - 本体 Registry が同一 Descriptor を 1 インスタンスに集約するため、Source レベル共有が達成されれば port 共有も自動的に達成される。
  - 共有 Receiver の参照カウント実装を持たないことで、MVP のコード量とテスト範囲を最小化できる。
  - 将来複数 typeId="MOVIN" Source が独立 port で動作する場合も自然に並行稼動する。
- **Trade-offs**: 異なる Config (port が同じ) を別 Slot で運用すると port 衝突して bind に失敗する。これは要件 14-3 通りで許容。
- **Follow-up**: MOVIN 用の共有 Receiver パターンが必要になったら別 spec で `MovinSharedOscReceiver` を導入。

### Decision: Applier のインタフェース
- **Context**: 要件 8-* で MOVIN 専用 Applier を提供する必要があるが、本体 `IMotionApplier` は Humanoid 想定。
- **Alternatives Considered**:
  1. 本体 `IMotionApplier` を実装する (Apply シグネチャを Humanoid 向けに調整)
  2. 本パッケージ独自 API として `MovinMotionApplier` を提供 (本体型を実装しない)
- **Selected Approach**: 2 — `MovinMotionApplier` は本体型を継承・実装しない、本パッケージ独自 API。Sample 駆動 MonoBehaviour が `MovinMotionFrame` をキャストして直接 `Apply(frame)` を呼ぶ。
- **Rationale**: 本体 `IMotionApplier` のシグネチャ (`Apply(MotionFrame frame, float weight, SlotSettings settings)` 等) は Humanoid 想定で、MOVIN の Generic Transform 直接書き込みには整合しない。本パッケージ独自 API として閉じることで本体非改変制約と完全自己完結方針を両立。
- **Trade-offs**: 本体 Sample `SlotManagerBehaviour` の Pipeline (HumanoidMotionApplier 前提) には乗らない。MOVIN 専用 Pipeline / Sample を本パッケージで提供する必要がある。
- **Follow-up**: Sample `Samples~/MOVIN/Runtime/MovinSlotDriver.cs` で MOVIN 専用 Pipeline を実装。

### Decision: 自己登録における Editor 経路
- **Context**: 要件 6 で Runtime / Editor 二経路の自己登録が必須。
- **Alternatives Considered**:
  1. Runtime 1 メソッド + `[InitializeOnLoadMethod]` を `#if UNITY_EDITOR` で同居
  2. Runtime ファイルと Editor 専用ファイル (別 asmdef) を分離
- **Selected Approach**: 2 — 本体 VMC 実装 (`VmcMoCapSourceFactoryEditorRegistrar.cs`) と同形に物理ファイル分離。Editor 専用 asmdef `RealtimeAvatarController.MoCap.Movin.Editor` を作成。
- **Rationale**: 要件 6-3 が明示。本体実装パターンと一致させ将来の保守性を確保。
- **Trade-offs**: ファイル数 + 1。
- **Follow-up**: なし。

## Risks & Mitigations

- **R-1: uOSC bindAddress 非公開**: `uOscServer` は `bindAddress` を公開していないため、`MovinMoCapSourceConfig.bindAddress` は情報フィールドのみとなり実 bind は全インターフェース。
  - **Mitigation**: README に明記。将来 `com.hidano.uosc` が `bindAddress` を公開した時点で実装を追加する Open Issue として登録。
- **R-2: Domain Reload OFF 環境での共有ホスト static 参照**: `MovinOscReceiverHost` の static フィールドが Play Mode 再開時に持ち越されると null reference / 二重バインドの懸念。
  - **Mitigation**: `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で static をクリアする (本体 EVMC4USharedReceiver と同形)。
- **R-3: name2Transform 構築コスト**: 大規模アバター (数千 Transform) で name 一致テーブル構築が初期化コストになる。
  - **Mitigation**: `Build` は `SetAvatar` 時に 1 回のみ実行。`boneClass` フィルタで対象 Transform を絞れる。MVP では再構築不要 (Avatar が動的に変わる用途は範囲外)。
- **R-4: localScale を毎フレーム書き込むことの副作用**: Avatar Transform ツリー上の特定ボーン localScale が毎フレーム書き換えられると Animator や他システムと衝突する可能性。
  - **Mitigation**: 要件 8-7 通り、書き込み対象は OSC で送られた boneName で resolve したボーン Transform のみ (Avatar GameObject ルートではない)。送信側がスケール書き換えを意図したボーンに限定して送る前提。Sample でドキュメント化。
- **R-5: 受信スレッドモデル誤実装**: 受信ワーカースレッドから直接 `Subject.OnNext` を呼ぶと UniRx 購読側の Unity API 呼び出しでクラッシュする可能性。
  - **Mitigation**: 要件 10-1 / 10-2 / 10-3 通り、uOSC `onDataReceived` (メインスレッド発火) → 内部キャッシュ書込 → LateUpdate Tick で snapshot → `Subject.OnNext` の 2 段モデルを厳守。

## References

- [Streaming Mocap Data into Unity (MOVIN Studio Docs)](https://help.movin3d.com/movin-studio-usage-guide/live-streaming/streaming-mocap-data-into-unity) — Unity 統合の公式手順 (Root Bone / Listen Port / バージョン要件)
- [VMC Protocol specification](https://protocol.vmc.info/english.html) — `/VMC/Ext/Bone/Pos` `/VMC/Ext/Root/Pos` v2.0 / v2.1 の OSC アドレス・引数仕様
- [VMC Protocol Reference Implementation](https://protocol.vmc.info/Reference.html) — リファレンス実装と利用例
- [MOVIN Inc. GitHub](https://github.com/MOVIN3D) — MOVIN 公式組織
- [SetLocalPositionAndRotation support (Unity Discussions)](https://discussions.unity.com/t/setlocalpositionandrotation-support/891499) — `Transform.SetLocalPositionAndRotation` API の現状
- 内部参照:
  - `Assets/MOVIN/Scripts/Core/VMCReceiver.cs` / `Assets/MOVIN/Scripts/Core/MocapReceiver.cs` (実装移植元)
  - `Packages/com.hidano.realtimeavatarcontroller/Runtime/MoCap/VMC/*` (パターン参照元)
  - `.kiro/specs/mocap-movin/requirements.md`
