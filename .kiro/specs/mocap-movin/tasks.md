# Implementation Plan

> 本タスク計画は `requirements.md` と `design.md` (Design Decisions D-1..D-4 確定済み) を入力に生成された。実装は `/kiro:spec-run` でバッチ実行する想定。本体パッケージ `jp.co.unvgi.realtimeavatarcontroller` は一切改変しない。
>
> **Parallel 凡例**: `(P)` は同一親配下で先行兄弟タスクへ依存しないことを示す。`_Boundary:_` で対象コンポーネントを明示。`_Depends:_` は major task をまたぐ非自明な依存にのみ付与。

---

## Phase 1: Foundation (パッケージ骨格と共有テスト基盤)

- [ ] 1. UPM パッケージ骨格と asmdef 構成を整備する
- [ ] 1.1 パッケージマニフェストとディレクトリ骨格を作成する
  - `Packages/com.hidano.realtimeavatarcontroller.movin/` 配下に Runtime / Editor / Tests/EditMode / Tests/PlayMode / `Samples~/MOVIN/{Runtime,Configs,Scenes,Prefabs}` の空ディレクトリを用意する
  - `package.json` に `name="jp.co.unvgi.realtimeavatarcontroller.movin"`, `unity="6000.3"`, `dependencies` (`jp.co.unvgi.realtimeavatarcontroller`, `com.hidano.uosc`) を宣言し、`samples` セクションで `Samples~/MOVIN` を Package Manager UI から import 可能にする
  - 観測可能完了: `Packages/manifest.json` に本パッケージが追加された状態で Unity が package を解決でき、Package Manager に "MOVIN" Sample が表示される
  - _Requirements: 1.1, 1.3, 12.3_

- [ ] 1.2 Runtime / Editor / Tests asmdef を name 参照で定義する
  - Runtime asmdef を `RealtimeAvatarController.MoCap.Movin` 名で作成し、`RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `uOSC.Runtime` (もしくは com.hidano.uosc 提供 asmdef name) / `UniRx` を name 参照で持たせる (GUID 参照禁止)
  - Editor asmdef を `RealtimeAvatarController.MoCap.Movin.Editor` 名で作成し、Editor プラットフォーム限定 + Runtime asmdef + `UnityEditor` を参照する
  - Tests/EditMode と Tests/PlayMode の asmdef (`...Tests.EditMode` / `...Tests.PlayMode`) を Runtime / Editor から逆参照されない一方向として定義し、`UNITY_INCLUDE_TESTS` 制約と nunit 参照を含める
  - 観測可能完了: Unity Editor が 4 つの asmdef を競合なくコンパイルし、本体パッケージ asmdef が本パッケージを逆参照していない
  - _Requirements: 1.4, 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [ ] 1.3 Runtime に AssemblyInfo.cs を追加し InternalsVisibleTo を宣言する
  - `Runtime/AssemblyInfo.cs` を新規追加し、EditMode / PlayMode テスト asmdef 名 (`RealtimeAvatarController.MoCap.Movin.Tests.EditMode` / `...PlayMode`) に対する `[assembly: InternalsVisibleTo(...)]` を 2 行宣言する
  - 観測可能完了: テスト assembly から `internal interface IMovinReceiverAdapter` および `internal sealed class MovinOscReceiverHost` が参照可能になる (D-2 帰結 / Major M-2)
  - _Requirements: 13.1_

---

## Phase 2: Core Domain (Config / Frame / Applier / Bone Table)

- [ ] 2. Config ScriptableObject (`MovinMoCapSourceConfig`) を実装する
  - `MoCapSourceConfigBase` を継承し、`port` (既定 11235, `[Range(1, 65535)]`) / `bindAddress` (既定 "") / `rootBoneName` (既定 "") / `boneClass` (既定 "") を public フィールドで保持する
  - `[CreateAssetMenu(menuName = "RealtimeAvatarController/MoCap/MOVIN Config")]` を付与してアセット生成および `ScriptableObject.CreateInstance` 動的生成の両方をサポートする
  - `bindAddress` フィールドに D-1 に従う `[Tooltip("uOSC 1.0.0 では参照のみで実 bind には反映されません。実バインドは全インターフェース (0.0.0.0) です。")]` を付与する
  - 将来のプロパティ追加余地のためクラスを `sealed` にしない
  - 観測可能完了: Project ビューから `.asset` が作成でき、Inspector で 4 フィールドが編集可能、`bindAddress` に Tooltip が表示される
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.6_
  - _Boundary: MovinMoCapSourceConfig_

- [ ] 3. MOVIN MotionFrame と値オブジェクト群を実装する
- [ ] 3.1 (P) `MovinBonePose` / `MovinRootPose` readonly struct を定義する
  - `MovinBonePose` に `LocalPosition` / `LocalRotation` / nullable `LocalScale` を保持
  - `MovinRootPose` に `BoneName` / `LocalPosition` / `LocalRotation` / nullable `LocalScale` / nullable `LocalOffset` を保持し v2.1 拡張対応
  - 観測可能完了: 両 struct がコンストラクタ引数を readonly フィールドへ書き込み、外部から再代入不可
  - _Requirements: 3.10, 3.11, 7.3_
  - _Boundary: MovinMotionFrame_

- [ ] 3.2 (P) `MovinMotionFrame` を `Motion.MotionFrame` 派生 immutable 具象として実装する
  - `RealtimeAvatarController.Motion.MotionFrame` を直接継承し、`SkeletonType` を `SkeletonType.Generic` で override
  - bone 名キーの `IReadOnlyDictionary<string, MovinBonePose> Bones` と nullable `MovinRootPose? RootPose` を保持
  - コンストラクタは外部から渡された Dictionary を再ラップせずそのまま `IReadOnlyDictionary` として保持し (snapshot コピーは Source 側責務)、構築後の mutate を許容しない
  - `Timestamp` は基底契約 (`Stopwatch.GetTimestamp() / Stopwatch.Frequency`) に従い、コンストラクタ引数で受け取る
  - Humanoid 関連型 (`HumanoidMotionFrame` 等) を一切参照しない
  - 観測可能完了: `MovinMotionFrame` インスタンスから `Bones` / `RootPose` / `Timestamp` / `SkeletonType` が想定通り読み取れ、`Bones` を外部からキャストして変更しても元 Dictionary が安全に共有されない (Source 側 snapshot 責務で担保)
  - _Requirements: 2.1, 2.2, 2.5, 2.6, 7.1, 7.2, 7.4, 7.5_
  - _Boundary: MovinMotionFrame_

- [ ] 4. Bone テーブル構築ユーティリティ (`MovinBoneTable`) を実装する
  - `internal static class MovinBoneTable` に `TryBuild(Transform avatarRoot, string rootBoneName, string boneClass, out Dictionary<string, Transform> table)` を実装し、成功時 `true`、armature 未検出時は `table = null` で `false` を返す
  - `rootBoneName` 非空: 該当名 Transform を armature ルートに採用
  - `rootBoneName` 空: 「Renderer を持たないが兄弟が Renderer を持つ」経験則 (Sample `MocapReceiver.SearchArmature` 移植) で armature を探索
  - `boneClass` 非空: `{boneClass}:` プレフィックスで始まる Transform 名のみ辞書登録 (D-4 含意の boneClass フィルタ)
  - `boneClass` 空: armature 配下の全 Transform を辞書登録
  - 観測可能完了: 単純 Transform ツリーに対して `TryBuild` が name→Transform 辞書を返し、フィルタや rootBoneName 起点採用が反映される
  - _Requirements: 8.2, 8.3, 8.4_
  - _Boundary: MovinBoneTable_

- [ ] 5. MOVIN Applier (`MovinMotionApplier`) を実装する
  - `public sealed class MovinMotionApplier : IDisposable` を定義し、`SetAvatar(GameObject avatarRoot, string rootBoneName, string boneClass)` で `MovinBoneTable.TryBuild` を呼び成功時のみ内部辞書を保持
  - `TryBuild` 失敗時は実 `rootBoneName` / `boneClass` を含むメッセージで `InvalidOperationException` をスローする
  - `Apply(MovinMotionFrame frame)` は `frame.Bones` 各エントリを name 一致 lookup → `Transform.SetLocalPositionAndRotation(localPos, localRotation)` で書き込み、`LocalScale` が非 null の場合 `localScale` も書き込む
  - `frame.RootPose` がある場合、`RootPose.BoneName` で resolve した Transform に対してのみ pos/rot/scale を書き込む (Avatar GameObject ルートは書き込み対象にしない、要件 8-7)
  - 未一致 bone は黙ってスキップし例外をスローしない、Avatar が破棄された (Transform null) bone もスキップ
  - `Dispose` で内部辞書を null クリアする
  - 本体 `IMotionApplier` を実装せず、`HumanoidMotionFrame` / `HumanoidMotionApplier` への型参照を一切持たない
  - 観測可能完了: テスト用 Transform ツリーに対して `Apply` 後に対象 Transform の localPosition/Rotation/Scale が想定値に変わる
  - _Requirements: 2.3, 2.4, 2.6, 8.1, 8.5, 8.6, 8.7, 8.8, 8.9_
  - _Boundary: MovinMotionApplier_

---

## Phase 3: Core Source (OSC 受信 / Frame 発行)

- [ ] 6. uOSC 受信ホスト (`MovinOscReceiverHost`) を実装する
  - `internal sealed class MovinOscReceiverHost : MonoBehaviour` を定義し、`Create(IMovinReceiverAdapter adapter)` で DontDestroyOnLoad GameObject に `uOscServer` (`autoStart=false`) と Host を AddComponent して 1 Source = 1 Host を保証する
  - `ApplyReceiverSettings(int port)` で `StopServer()` → `port=` → `StartServer()` の順に呼び明示的 bind を行い、`SocketException` は呼出元 (`MovinMoCapSource.Initialize`) へ伝播させる
  - `uOscServer.onDataReceived` を購読し、メインスレッド上で `/VMC/Ext/Bone/Pos` (8 引数固定) と `/VMC/Ext/Root/Pos` (8 / 11 / 14 引数許容、欠損は null) のみを解釈して `IMovinReceiverAdapter.HandleBonePose` / `HandleRootPose` にディスパッチする (それ以外のアドレスは無視)
  - `LateUpdate` で `adapter.Tick()` を呼び、try/catch して `adapter.HandleTickException(ex)` に委譲する
  - `Shutdown()` で onDataReceived 購読解除 + `StopServer()` + GameObject `Destroy`
  - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で static 参照を null クリアし Domain Reload OFF 環境で持ち越しを防ぐ
  - 観測可能完了: uOSC を mock せずとも Initialize 経路で GameObject が DontDestroyOnLoad に生成され、port 競合時は SocketException が呼出元へ伝播する
  - _Requirements: 3.4, 10.1, 10.3, 10.6, 14.3, 14.5_
  - _Boundary: MovinOscReceiverHost_

- [ ] 7. MOVIN MoCap Source (`MovinMoCapSource`) を実装する
  - `public sealed class MovinMoCapSource : IMoCapSource, IDisposable` を Pure C# クラスとして定義し、`SourceType => "MOVIN"` を返す
  - `MotionStream` は `Subject<MotionFrame>.Synchronize().Publish().RefCount()` のマルチキャスト Hot Observable として公開し、`OnError` を一切発行しない
  - `Initialize(MoCapSourceConfigBase config)` は (a) 状態が `Uninitialized` でなければ `InvalidOperationException`、(b) `config` を `MovinMoCapSourceConfig` にキャストし失敗時は実型名を含む `ArgumentException`、(c) `port` が 1..65535 範囲外なら `ArgumentOutOfRangeException`、(d) `bindAddress` が非空なら 1 回だけ `Debug.LogWarning` を出力 (D-1 帰結)、(e) `MovinOscReceiverHost.Create` + `ApplyReceiverSettings(port)` を呼び `Running` 状態へ遷移
  - 内部 `IMovinReceiverAdapter` を実装し、`HandleBonePose` / `HandleRootPose` で受信値を内部 Dictionary キャッシュに最新値で上書き、`Tick` で bone Dictionary を新規 Dictionary にコピーした上で `MovinMotionFrame` を組み立て `Subject.OnNext` する
  - bone Dictionary が空の Tick は emit を抑制 (要件 7-6)
  - `HandleTickException` および受信例外は try/catch 後 `RegistryLocator.ErrorChannel.Publish(new SlotError(slotId: MovinMoCapSourceFactory.MovinSourceTypeId, category: SlotErrorCategory.VmcReceive, exception: ex, occurredAt: DateTime.UtcNow))` で集約 (D-2 帰結 / 要件 10-5)
  - `Shutdown()` / `Dispose()` は冪等、内部 Subject の `OnCompleted` + `Dispose`、`MovinOscReceiverHost.Shutdown`、状態を `Disposed` に固定し、Disposed 後の `Initialize` は `InvalidOperationException` (terminal-Disposed 契約、Critical Issue 1 帰結)
  - 観測可能完了: テスト用に `IMovinReceiverAdapter` 経由で擬似 OSC イベントを注入すると Subscribe 側に `MovinMotionFrame` が届き、`Shutdown → Initialize` が `InvalidOperationException` をスローする
  - _Requirements: 2.5, 2.6, 3.1, 3.2, 3.3, 3.5, 3.6, 3.7, 3.8, 3.9, 4.5, 7.6, 10.2, 10.4, 10.5, 10.7, 10.8_
  - _Boundary: MovinMoCapSource_
  - _Depends: 6_

---

## Phase 4: Factory / 自己登録 / Bridge

- [ ] 8. Factory と Runtime/Editor 自己登録を実装する
- [ ] 8.1 `MovinMoCapSourceFactory` と Runtime 自己登録を実装する
  - `public sealed class MovinMoCapSourceFactory : IMoCapSourceFactory` を定義し、`public const string MovinSourceTypeId = "MOVIN"` を保持する
  - `Create(MoCapSourceConfigBase config)` は `MovinMoCapSourceConfig` にキャストし、失敗時は実型名を含む `ArgumentException`、成功時は `new MovinMoCapSource(...)` を返す (参照キャッシュなし)
  - `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` の `RegisterRuntime()` 静的メソッドで `RegistryLocator.MoCapSourceRegistry.Register(MovinSourceTypeId, new MovinMoCapSourceFactory())` を呼び、`RegistryConflictException` 発生時は握り潰さず `RegistryLocator.ErrorChannel.Publish(new SlotError("MOVIN", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を発行 (D-2 帰結)
  - 観測可能完了: ランタイム起動後 `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に `"MOVIN"` が含まれ、Factory.Create が `MovinMoCapSource` を返す
  - _Requirements: 1.6, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.4, 6.6, 9.1, 14.1_
  - _Boundary: MovinMoCapSourceFactory_

- [ ] 8.2 Editor 自己登録 (`MovinMoCapSourceFactoryEditorRegistrar`) を実装する
  - Editor asmdef 配下に Runtime と物理的に別ファイルで配置し、`[InitializeOnLoadMethod]` の `RegisterEditor()` で同一の `Register` を呼ぶ (本体 `VmcMoCapSourceFactoryEditorRegistrar` と同形)
  - `RegistryConflictException` 発生時は同様に `RegistryLocator.ErrorChannel.Publish(new SlotError("MOVIN", SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow))` を発行
  - 観測可能完了: Editor 起動時に MOVIN typeId が Registry に登録され、Inspector の typeId 候補列挙 (`GetRegisteredTypeIds`) に `"MOVIN"` が現れる
  - _Requirements: 6.2, 6.3, 6.4, 6.5_
  - _Boundary: MovinMoCapSourceFactoryEditorRegistrar_
  - _Depends: 8.1_

- [ ] 9. `MovinSlotBridge` Pure C# Pipeline を Runtime API として実装する
  - D-4 に従い `public sealed class MovinSlotBridge : IDisposable` を Runtime asmdef に配置する
  - コンストラクタ `MovinSlotBridge(IMoCapSource source, MovinMotionApplier applier)` で `source.MotionStream` を `.ObserveOnMainThread()` 経由で購読し、`MovinMotionFrame` キャストに成功したフレームのみ `applier.Apply(frame)` を呼ぶ
  - `Dispose()` は購読解除のみ実施し、`source.Dispose` / `applier.Dispose` は呼ばない (Source 解放は本体 `MoCapSourceRegistry.Release` に委ね、Applier は呼出元責務、要件 9-4)
  - 観測可能完了: テストハーネスで `IMoCapSource` をモック注入したとき、`MovinMotionFrame` 受信ごとに Applier の Apply が呼ばれ、`Dispose` 後は呼ばれなくなる
  - _Requirements: 9.2, 9.3, 9.4_
  - _Boundary: MovinSlotBridge_
  - _Depends: 5, 7_

---

## Phase 5: Sample / Documentation Integration

- [ ] 10. Sample (`Samples~/MOVIN`) と README を整備する
- [ ] 10.1 Sample driver MonoBehaviour (`MovinSlotDriver`) を実装する
  - `Samples~/MOVIN/Runtime/MovinSlotDriver.cs` を Sample 専用 MonoBehaviour として実装し、Inspector で `SlotSettings[]` (typeId="MOVIN") とアバター参照 (`Transform`) を受け取る
  - Awake で `SlotManager` を生成し、`OnSlotStateChanged` を購読して `SlotState.Active` 到達時に `TryGetSlotResources(slotId, out source, out avatar)` で取得 → `MovinMotionApplier.SetAvatar(avatar, config.rootBoneName, config.boneClass)` → `MovinSlotBridge` を生成して保持
  - `SlotState.Disposed` 遷移で Bridge / Applier を Dispose (Source は本体 Registry に委ねる)
  - 初期化失敗時 (Avatar 解決失敗 / armature 未検出等) に `RegistryLocator.ErrorChannel.Publish(new SlotError(slotId, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow))` を発行する
  - 本体 `internal` 型に依存しない (要件 12-4)
  - 観測可能完了: シーン再生時に Driver が SlotManager / Bridge / Applier を結線し、Slot Active 時に Apply が走る (目視 / シーン状態で確認)
  - _Requirements: 9.2, 9.4, 9.7, 12.4_
  - _Boundary: MovinSlotDriver (Sample)_
  - _Depends: 5, 7, 8.1, 9_

- [ ] 10.2 Sample アセット群とデモシーンを作成する
  - `Samples~/MOVIN/Configs/MovinMoCapSourceConfig.asset` を `port=11235` 既定、`rootBoneName` / `boneClass` をプリセット済みで作成
  - `Samples~/MOVIN/Runtime/MovinSampleSlotSettings.asset` を `MoCapSourceDescriptor.SourceTypeId="MOVIN"` で作成し、上記 Config asset を参照させる
  - `Samples~/MOVIN/Prefabs/NeoMOVINMan_Unity.prefab` を既存 `Assets/MOVIN/` から Samples 配下にコピー (本体 Asset 領域は無改変)
  - `Samples~/MOVIN/Scenes/MOVINSampleScene.unity` を NeoMOVINMan + `MovinSlotDriver` 配置済みで作成し、1 シーン再生で MOVIN Studio 受信 → 適用が完結する状態にする
  - 観測可能完了: Package Manager の Samples タブから "MOVIN" Sample をインポートしてシーンを再生でき、`SlotSettings` に typeId="MOVIN" が紐付いている
  - _Requirements: 12.1, 12.2_
  - _Boundary: Samples~/MOVIN assets & scene_
  - _Depends: 10.1_

- [ ] 10.3 README.md / CHANGELOG.md を整備する
  - パッケージルートに `README.md` を作成し、(a) MOVIN Studio 側 Platform=Unity / Port=11235 の設定手順、(b) `SlotSettings.MoCapSourceDescriptor.SourceTypeId="MOVIN"` 設定手順、(c) `rootBoneName` / `boneClass` の役割、(d) D-1 帰結の `bindAddress` 情報フィールド注意事項、(e) D-2 帰結の `SlotErrorCategory.VmcReceive` が MOVIN typeId からも発行されること (`SlotError.SlotId="MOVIN"` で識別) を記載する
  - `CHANGELOG.md` に初版エントリと、D-3 帰結 "VMC + MOVIN 並行稼働 Demo" を Future Work として記録する
  - 観測可能完了: README からエンドユーザーが MOVIN 接続〜適用の手順を辿れ、bindAddress / VmcReceive 拡張意味の注意事項が明示される
  - _Requirements: 12.5_
  - _Boundary: README / CHANGELOG_

---

## Phase 6: Validation (EditMode / PlayMode テスト)

- [ ] 11. EditMode テストを整備する
- [ ] 11.1 (P) Config / Factory キャストと自己登録結果を検証する EditMode テストを実装する
  - `MovinMoCapSourceConfigTests`: `MovinMoCapSourceConfig` を `MoCapSourceConfigBase` 経由で `MovinMoCapSource.Initialize` に渡し、port 範囲内なら例外なく Initialize 完了 / 範囲外で `ArgumentOutOfRangeException` を確認
  - `MovinMoCapSourceFactoryTests`: `Create` が `MovinMoCapSourceConfig` で `MovinMoCapSource` を返し、誤った Config 型 (例: `VMCMoCapSourceConfig` 相当のダミー) で実型名を含む `ArgumentException` をスローすることを確認
  - `MovinSelfRegistrationTests`: `RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds()` に `"MOVIN"` が含まれ、二重 `Register` で `RegistryConflictException` 発生時に `OverrideErrorChannel` に差し込んだモックへ `RegistryConflict` カテゴリで通知されることを確認
  - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼びテスト独立性を保つ
  - 観測可能完了: EditMode Test Runner で 3 テストクラスがすべて緑になる
  - _Requirements: 4.5, 5.3, 5.4, 6.4, 6.6, 13.1, 13.2, 13.5_
  - _Boundary: Tests/EditMode_
  - _Depends: 2, 7, 8.1, 8.2_

- [ ] 11.2 (P) Source ライフサイクル契約 (terminal-Disposed) を検証する EditMode テストを実装する
  - `MovinMoCapSource.Initialize` の二度呼び出しで `InvalidOperationException` がスローされること、`Shutdown` / `Dispose` が冪等で二重呼び出しでも例外を出さないことを確認
  - **`Shutdown → Initialize` 経路でも `InvalidOperationException` がスローされる** ことを明示テストし、terminal-Disposed 契約をロックする (Critical Issue 1 帰結)
  - 観測可能完了: 上記 3 経路に対するテストが緑になり、再 Initialize がコンパイル時ではなく実行時例外として確実に防がれる
  - _Requirements: 3.7, 3.8_
  - _Boundary: MovinMoCapSource lifecycle tests_
  - _Depends: 7_

- [ ] 12. PlayMode テストを整備する
- [ ] 12.1 (P) Applier / BoneTable PlayMode テストを実装する
  - 3 階層 Transform (`mixamorig:` プレフィックスを含む複数 bone) のテスト用 GameObject を生成し、`MovinMotionApplier.SetAvatar` で name 一致テーブルが構築されることを確認
  - 任意の `MovinMotionFrame` (テスト用に直接生成) を `Apply` に渡し、対象 Transform の `localPosition` / `localRotation` / `localScale` (RootPose 含む) が想定値に変化することを確認
  - 受信 bone 名がテーブルに存在しないとき例外をスローせずスキップされること、Avatar が破棄された (Transform null) ボーンへの書き込みもスキップすることを確認
  - 観測可能完了: PlayMode Test Runner で Transform 値の変化と例外非発生が確認される
  - _Requirements: 8.1, 8.5, 8.6, 8.7, 8.9, 13.3_
  - _Boundary: Tests/PlayMode (Applier)_
  - _Depends: 4, 5_

- [ ] 12.2 (P) `boneClass` フィルタ PlayMode テストを実装する
  - `boneClass="MOVIN"` 等を指定して `MovinBoneTable.TryBuild` を呼び、対象外プレフィックスの Transform が辞書から除外されること、`Apply` 後に対象外 Transform の値が書き換わらないことを確認
  - 観測可能完了: フィルタ非対象 Transform の localPosition/Rotation がテスト前後で不変であることがアサートされる
  - _Requirements: 8.4, 13.3_
  - _Boundary: Tests/PlayMode (BoneClassFilter)_
  - _Depends: 4, 5_

- [ ] 12.3 (P) Source Observable PlayMode テストを実装する
  - InternalsVisibleTo を介して `IMovinReceiverAdapter` 経由で `MovinMoCapSource` に擬似 `HandleBonePose` / `HandleRootPose` / `Tick` を注入し、Subscribe 側が `MovinMotionFrame` を受信できることを確認
  - 内部 bones Dictionary が空の Tick では emit が抑制されること、`OnError` が発行されないこと、Tick 内例外が `RegistryLocator.ErrorChannel` に `VmcReceive` カテゴリで `SlotError.SlotId="MOVIN"` として届くことを確認 (D-2 帰結)
  - `[SetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼びテスト独立性を保つ
  - 観測可能完了: Observable 受信、空フレーム抑制、ErrorChannel 通知の 3 つが PlayMode で緑になる
  - _Requirements: 7.6, 10.4, 10.5, 13.3, 13.4, 13.5_
  - _Boundary: Tests/PlayMode (SourceObservable)_
  - _Depends: 7_

- [ ]* 12.4 (P) VMC 並行稼動 / VMC v2.0–v2.7 互換 PlayMode テストを実装する (任意・MVP 後回し可)
  - 異なる port で typeId="VMC" と typeId="MOVIN" が共存できること (要件 14-1, 14-2, 14-4) と、`/VMC/Ext/Root/Pos` の 8/11/14 引数いずれも `MovinMotionFrame.RootPose` に正しく反映されること (要件 14-5) を擬似 OSC 注入で確認する
  - 同一 port を要求した場合に `MovinMoCapSource.Initialize` が `SocketException` を伝播することを確認 (要件 14-3, 10-6)
  - 観測可能完了: 並行稼動テスト・引数長許容テスト・port 衝突伝播テストが PlayMode で緑になる (本 MVP では Acceptance Criteria 14-* の補強的検証として deferrable)
  - _Requirements: 10.6, 14.2, 14.3, 14.4, 14.5_
  - _Boundary: Tests/PlayMode (Compatibility)_
  - _Depends: 7_

---

## Cross-Cutting Coverage Notes

- **要件 1.2 / 1.5**: 本タスク群は `Packages/com.hidano.realtimeavatarcontroller/**` および `Assets/MOVIN/**` を改変対象としない (タスク 1.1 / 10.2 の Samples コピーで本体 Asset を読み取り専用扱いとする) ことで満たす。本体改変が必要な変更は本 spec 外として扱う。
- **要件 9.5 / 9.6**: 本体 `DefaultMoCapSourceRegistry` の参照共有契約に依存し、本パッケージ側で追加の参照管理を持たない (Source 1 個 = Host 1 個の単純化、research.md Decision 参照)。タスク 7 の Source 実装で参照キャッシュなし設計を担保。
- **要件 10.1**: uOSC のメインスレッド発火モデル前提はタスク 6 の `MovinOscReceiverHost` で uOSC 内部スレッドへ直接アクセスしない実装により担保。
- **要件 11.5**: 本体 asmdef へ破壊的変更を加えないことはタスク 1.2 で本体 asmdef を参照のみ (name reference) として担保。
- **要件 13.6**: コードカバレッジ数値目標は初期版で未設定 (将来リリースで再評価)。
