# Implementation Plan — mocap-vmc-native

> 本タスク計画は `.kiro/specs/mocap-vmc-native/design.md` および `requirements.md` (R1〜R12) に基づく。
> Phase 0 は親セッション専用 (子 Agent の `rm` / `git rm` 不可制約)、 Phase 1〜7 は通常実装 phase。
> 命名規約: 公開クラスは `VMC` プレフィックス (`VMCMoCapSource` / `VMCSharedReceiver`)、 internal helper は `Vmc` プレフィックス (`VmcOscMessageRouter` / `VmcBoneNameMap`)。
> N-R3 制約: Phase 4 (`using EVMC4U;` 削除 + asmdef references 削除) は同一論理ステップで実施し中間状態でビルド破綻させない。
> TDD ordering: 新規コンポーネントは RED (失敗テスト) → GREEN (最小実装) → REFACTOR の順で進める。

## Phase 0: 親セッション専用クリーンアップ (子 Agent 不可)

> このタスク群は CLAUDE.md グローバルルール「子 Agent は rm/git rm 不可」 に従い、 必ず親セッションで先行実施する。
> 削除対象には対応する `.meta` ファイルも含む。 削除後 Unity Editor を再起動して `Library/ScriptAssemblies/` キャッシュを再生成すること。

- [x] 0. 親セッション削除 + OBSOLETE マーカ追記
- [x] 0.1 旧 EVMC4U 同梱ディレクトリ全削除
  - `RealtimeAvatarController/Assets/EVMC4U/` ディレクトリ全体 (.cs × 7 + LICENSE + 3rdpartylicenses(ExternalReceiverPack).txt + 同梱 .meta 群) を `git rm -rf` で削除する
  - 削除前に `Assets/EVMC4U/3rdpartylicenses(ExternalReceiverPack).txt` の uOSC / UniVRM credit 内容を Phase 6 で `THIRD_PARTY_NOTICES.md` へ転記する目的で控えておく
  - 削除後 Unity Editor を起動した際に `Assets/EVMC4U/` が消えていること、 および Console に `EVMC4U` 関連の missing script 警告が出ないことを目視確認する (compile はこの時点では破綻している前提でよい / Phase 4 で修復する)
  - _Requirements: 7.5, 12.6_
- [x] 0.2 廃止対象テストの削除
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Tests/EditMode/ExternalReceiverPatchTests.cs` および `.cs.meta` を `git rm` で削除する
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Tests/EditMode/EVMC4USharedReceiverTests.cs` および `.cs.meta` を `git rm` で削除する (GUID 移植せず、 Phase 5 で新規作成する `VMCSharedReceiverTests.cs` はランダム新規 GUID を持つ)
  - 削除後 `Tests/EditMode/` 配下に上記 2 ファイルが存在しないことを確認する
  - _Requirements: 9.1, 9.2_
- [x] 0.3 旧 evmc4u.patch / handover に OBSOLETE マーカ追記
  - `.kiro/specs/mocap-vmc/evmc4u.patch` の冒頭に `# OBSOLETE (2026-05-09) — EVMC4U 依存撤廃により当 patch は適用不要となった。 mocap-vmc-native Spec 参照。` を追記し、 patch 本体は履歴用に残す
  - `.kiro/specs/mocap-vmc/handover-7.2.md` の patch 関連記述箇所に同等の OBSOLETE マーカを追記する
  - 両ファイル冒頭で OBSOLETE 宣言が読み取れる状態になっていることを `head` または diff で確認する
  - _Requirements: 12.5_

---

## Phase 1: 新規コンポーネント TDD (VmcBoneNameMap / VmcOscMessageRouter)

> 設計上独立した `internal static` ヘルパー 2 個。 互いに参照を持たないため並列実装可能 (`(P)`)。
> 既存 `EVMC4U*.cs` と asmdef 参照は Phase 4 まで残存するためコンパイル可能な状態を維持できる。

- [x] 1. VmcBoneNameMap: bone 名 → HumanBodyBones 静的辞書
- [x] 1.1 (P) RED: VmcBoneNameMapTests を先行作成して失敗状態を確認
  - `Tests/EditMode/VmcBoneNameMapTests.cs` を新規作成 (ランダム GUID `.meta` 付き)
  - `Enum.GetValues(typeof(HumanBodyBones))` の `LastBone` を除く全 enum メンバが `TryGetValue` で解決できることを検証するテストを追加する
  - 大文字小文字の区別 (例: `"hips"` で false) を検証するテストを追加する
  - 未知 bone 名 (例: `"Foo"`) で false が返ることを検証するテストを追加する
  - null 入力で例外を投げず false が返ることを検証するテストを追加する
  - 観測完了条件: テストが「型が存在しない」 ビルドエラーで失敗する状態に到達 (RED 確認)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 9.5_
  - _Boundary: VmcBoneNameMap (新規 internal static)_
- [x] 1.2 GREEN: VmcBoneNameMap を実装してテストを通す
  - `Runtime/VmcBoneNameMap.cs` を新規作成 (ランダム GUID `.meta` 付き)
  - `static readonly Dictionary<string, HumanBodyBones>` を cctor で `Enum.GetValues(typeof(HumanBodyBones))` から構築し、 `LastBone` を除外する (`StringComparer.Ordinal`)
  - `TryGetValue(string boneName, out HumanBodyBones bone)` を public 静的 API として提供する
  - 観測完了条件: 1.1 で追加した全テストが GREEN になる
  - _Requirements: 3.1, 3.2, 3.5, 10.3_
  - _Boundary: VmcBoneNameMap_
- [x] 1.3 REFACTOR: 起動時 1 回確保の確認とテスト seam 整備
  - cctor が起動時に 1 回のみ実行され、 マッピング辞書が 55 entries (Unity 6000.3.10f1 想定) を持つことを assert するテストを追加する
  - `internal static IEnumerable<KeyValuePair<string, HumanBodyBones>> EnumerateForTest()` を test seam として公開する
  - 観測完了条件: 全 EditMode テストが GREEN、 マッピング辞書数の expected 値がテストに記録される
  - _Requirements: 3.1, 3.5_
  - _Boundary: VmcBoneNameMap_

- [x] 2. VmcOscMessageRouter: OSC address dispatch + 8 引数構造解釈
- [x] 2.1 (P) RED: VmcOscMessageRouterTests を先行作成して失敗状態を確認
  - `Tests/EditMode/VmcOscMessageRouterTests.cs` を新規作成 (ランダム GUID `.meta` 付き)
  - `IVmcBoneRotationWriter` の test double (mock / fake) を作成する
  - `/VMC/Ext/Bone/Pos` 8 引数で `WriteBoneRotation` が 1 回呼ばれることを検証
  - `/VMC/Ext/Root/Pos` 8 引数で `WriteRoot` が 1 回呼ばれることを検証
  - `/VMC/Ext/Root/Pos` 14 引数で先頭 8 引数だけ解釈し例外なしを検証
  - 引数長 0 / 7 / 9 で writer 呼出なし、 例外なしを検証
  - 引数型不一致 (`values[0]` が float、 `values[1]` が string 等) で writer 呼出なし、 例外なしを検証
  - 未知 bone 名 (`"Foo"`) で writer 呼出なしを検証
  - 未知 OSC アドレス (`/VMC/Ext/Blend/Val`、 `/VMC/Ext/Cam`、 `/VMC/Ext/OK` 等) で writer 呼出なしを検証
  - 観測完了条件: テストが「型が存在しない」 ビルドエラーで失敗する (RED 確認)
  - _Requirements: 1.3, 1.4, 2.1, 2.2, 2.6, 3.3, 9.5, 9.6_
  - _Boundary: VmcOscMessageRouter (新規 internal static)_
- [x] 2.2 GREEN: IVmcBoneRotationWriter + VmcOscMessageRouter を実装してテストを通す
  - `Runtime/VmcOscMessageRouter.cs` を新規作成 (ランダム GUID `.meta` 付き)
  - 同ファイル内または同じ Runtime 配下に `internal interface IVmcBoneRotationWriter { WriteBoneRotation; WriteRoot; }` を定義
  - address 定数 `AddressBonePos = "/VMC/Ext/Bone/Pos"` / `AddressRootPos = "/VMC/Ext/Root/Pos"` を定義
  - `RouteMessage(in uOSC.Message message, IVmcBoneRotationWriter writer)` を実装し、 if/else または switch で address dispatch する (delegate alloc を避けるため `Dictionary<string,Action>` は不採用)
  - 引数長判定 + 型判定 (`values[0] is string`、 `values[i] is float`) で early return し例外を発生させない
  - bone 名は `VmcBoneNameMap.TryGetValue` 1 回のみで解決し、 失敗時 no-op
  - `Quaternion(rotX, rotY, rotZ, rotW)` / `Vector3(posX, posY, posZ)` を構築して writer に渡す (Bone の pos は破棄)
  - 観測完了条件: 2.1 で追加した全テストが GREEN になる
  - _Requirements: 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.3, 3.4, 10.2, 10.3, 11.2_
  - _Boundary: VmcOscMessageRouter, IVmcBoneRotationWriter_
- [x] 2.3 REFACTOR: test seam の追加と alloc 0 確認
  - `internal static bool TryParseBoneMessage(in uOSC.Message, out HumanBodyBones, out Quaternion)` および `TryParseRootMessage(...)` を test seam として公開する
  - 上記 test seam を直接呼ぶ単体テストを追加する
  - dispatch table に列挙された全 silently-ignored アドレス (`/VMC/Ext/Blend/*` / `/VMC/Ext/Cam` / `/VMC/Ext/Light` / `/VMC/Ext/Hmd/*` / `/VMC/Ext/Con/*` / `/VMC/Ext/Tra/*` / `/VMC/Ext/Setting/*` / `/VMC/Ext/OK` / `/VMC/Ext/T` / `/VMC/Ext/VRM` / `/VMC/Ext/Root/T`) について writer 呼出なしを検証する
  - 観測完了条件: 全 EditMode テストが GREEN、 dispatch table coverage が確保される
  - _Requirements: 1.4, 2.7, 10.2_
  - _Boundary: VmcOscMessageRouter_

---

## Phase 2: VMCSharedReceiver (リネーム + GUID 移植 + 内部実装書換)

> Phase 1 で作成した `VmcOscMessageRouter` / `IVmcBoneRotationWriter` / `VmcBoneNameMap` に依存する。
> 旧 `EVMC4USharedReceiver.cs` GUID `58052dfd9ff9ad04cae524187979f918` を新ファイルへ移植し、 `using EVMC4U;` を削除して uOSC 直接購読構造へ書換える。

- [x] 3. VMCSharedReceiver: uOSC 直接購読 + refCount lifecycle
- [x] 3.1 RED: VMCSharedReceiverTests を新規作成して失敗状態を確認
  - `Tests/EditMode/VMCSharedReceiverTests.cs` を新規作成 (ランダム GUID `.meta` 付き、 削除された旧 `EVMC4USharedReceiverTests.cs` の history は継承しない)
  - `EnsureInstance` 初回で GameObject + uOscServer + receiver 自身が AddComponent されることを検証
  - 重複 `EnsureInstance` で同一 instance が返り、 内部 refCount が 2 になることを検証
  - `Release` で refCount が減り、 0 到達で GameObject が破棄され `s_instance == null` に戻ることを検証
  - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 経路の static reset を test seam (`ResetForTest`) 経由で検証
  - `ApplyReceiverSettings(port)` が `_server.isRunning == false` 時に `InvalidOperationException` を投げることを検証 (port 占有テスト or test double で再現)
  - 観測完了条件: テストが「型が存在しない」 ビルドエラーで失敗する (RED 確認)
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.6, 8.6, 9.2, 9.7, 11.4_
  - _Boundary: VMCSharedReceiver (新規 / 旧 EVMC4USharedReceiver の後継)_
- [x] 3.2 GREEN: 旧 EVMC4USharedReceiver.cs を VMCSharedReceiver.cs にリネーム + GUID 移植 + 内部書換
  - `Runtime/EVMC4USharedReceiver.cs` を `Runtime/VMCSharedReceiver.cs` にリネームし、 旧 `.cs.meta` GUID `58052dfd9ff9ad04cae524187979f918` を新 `.cs.meta` に移植する
  - `using EVMC4U;` ステートメントを削除する
  - `MonoBehaviour` 派生で `uOscServer` を `AddComponent` し、 `autoStart=false` を設定して `port` 設定後 `StartServer()` を呼ぶ構造へ書換える (旧 `ExternalReceiver` 抱え込みを撤廃)
  - `ApplyReceiverSettings(int port)` 内で `_server.StartServer()` 直後に `if (!_server.isRunning) throw new InvalidOperationException(...)` で bind 失敗を検出する (Decision C / R-G)
  - `onDataReceived` UnityEvent に `OnOscMessage(uOSC.Message)` ハンドラを購読し、 `VmcOscMessageRouter.RouteMessage(in message, this)` に委譲する
  - `IVmcBoneRotationWriter` を実装し、 `_writeRotations: Dictionary<HumanBodyBones, Quaternion>` (capacity 64) / `_writeRootPosition` / `_writeRootRotation` フィールドへ MainThread 書込を行う
  - `Subscribe(IVmcMoCapAdapter)` / `Unsubscribe` / `internal Dictionary<...> ReadAndClearWriteBuffer(out Vector3, out Quaternion)` を提供する
  - `Update()` 末尾で `_subscribers` の各 `Tick()` を呼ぶ (uOscServer.Update が同 GameObject 上で先に走る前提)
  - `EnsureInstance()` / `Release()` / refCount 静的辞書で旧 `EVMC4USharedReceiver` と同型の lifecycle を維持する
  - Play Mode では `DontDestroyOnLoad`、 Edit Mode では適用しない (現状維持)
  - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で `s_instance = null; s_refCount = 0;` を実行する
  - 旧 `Receiver` プロパティ (EVMC4U `ExternalReceiver` を expose) を削除する
  - test seam として `public static void ResetForTest()` / `InstanceForTest` / `RefCountStaticForTest` を提供する
  - 観測完了条件: 3.1 のテストが GREEN、 `Runtime/` 配下に `EVMC4USharedReceiver.cs` が存在せず `VMCSharedReceiver.cs` が GUID `58052dfd9ff9ad04cae524187979f918` を保持する
  - _Requirements: 1.1, 1.2, 1.5, 1.6, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 6.3, 7.4, 8.2, 8.3, 8.6, 8.7, 11.3, 11.4_
  - _Boundary: VMCSharedReceiver_
- [x] 3.3 REFACTOR: writeBuffer 設計と adapter Tick 駆動の整理
  - `IVmcMoCapAdapter` interface を Runtime 配下に internal 定義 (`Tick()` / `HandleTickException(Exception)`)
  - `_subscribers: HashSet<IVmcMoCapAdapter>` の add / remove が thread-safe である必要がない (MainThread 構造保証) ことをコードコメントで明示する
  - bind 失敗テスト (3.1 のうち port 占有 / 同 port 二重 bind ケース) を実 `uOscServer` で再現できるよう PlayMode 側に補助テストを追加 or EditMode で test double 経路を整備する
  - 観測完了条件: refCount lifecycle / SubsystemRegistration reset / bind 失敗の全テストが GREEN
  - _Requirements: 4.4, 11.4_
  - _Boundary: VMCSharedReceiver_

---

## Phase 3: VMCMoCapSource (リネーム + GUID 移植 + 内部実装書換)

> Phase 2 完成後に着手。 旧 `EVMC4UMoCapSource.cs` GUID `42ced34567d8f9012ab345678901cdef` を新ファイルへ移植する。
> ダブルバッファリング戦略 (Decision A / E) と `HumanoidMotionFrame` 同 frame 完結性前提を実装に反映する。

- [x] 4. VMCMoCapSource: IMoCapSource Adapter + ダブルバッファ swap
- [x] 4.1 RED: VMCMoCapSourceTests を旧テストからリネーム + GUID 移植 + 失敗状態を確認
  - `Tests/EditMode/EVMC4UMoCapSourceTests.cs` を `Tests/EditMode/VMCMoCapSourceTests.cs` にリネームし、 旧 `.cs.meta` GUID `964bca5178c164b4e8d31bde1a9235b2` を新 `.cs.meta` に移植する
  - `using EVMC4U;` を削除する
  - `SourceType == "VMC"` を検証
  - `Initialize` 二重呼出で `InvalidOperationException` を検証
  - 異 config 型で `ArgumentException` (型名含む) を検証
  - port 範囲外で `ArgumentOutOfRangeException` を検証
  - `Dispose` 後の `Shutdown` 二重呼出で例外なし (idempotent) を検証
  - `MotionStream` が `IObservable<MotionFrame>` 型で公開されることを検証
  - 観測完了条件: テストが「型 `VMCMoCapSource` が存在しない」 ビルドエラーで失敗する (RED 確認)
  - _Requirements: 5.1, 5.2, 8.1, 8.5, 9.4, 11.5, 11.7_
  - _Boundary: VMCMoCapSource_
- [x] 4.2 GREEN: 旧 EVMC4UMoCapSource.cs を VMCMoCapSource.cs にリネーム + GUID 移植 + 内部書換
  - `Runtime/EVMC4UMoCapSource.cs` を `Runtime/VMCMoCapSource.cs` にリネームし、 旧 `.cs.meta` GUID `42ced34567d8f9012ab345678901cdef` を新 `.cs.meta` に移植する
  - `using EVMC4U;` を削除する
  - `IMoCapSource` / `IDisposable` / `IVmcMoCapAdapter` を実装する sealed class へ書換える
  - `SourceType => "VMC"` を返す
  - `MotionStream` を UniRx `Subject<MotionFrame>.Synchronize().Publish().RefCount()` 経路で公開する
  - ダブルバッファ `_bufferA` / `_bufferB` (各 `new Dictionary<HumanBodyBones, Quaternion>(64)`) をコンストラクタで事前確保する
  - `_writeBufferRef` / `_readBufferRef` ポインタで管理する
  - 状態機械 `_state ∈ { Uninitialized, Running, Disposed }` を実装、 `Initialize` 二重呼出で `InvalidOperationException`
  - `Initialize(VMCMoCapSourceConfig)` で `VMCSharedReceiver.EnsureInstance` → `ApplyReceiverSettings(port)` → `Subscribe(this)` を順次呼ぶ
  - `IVmcMoCapAdapter.Tick()` 内で: (1) `_writeBufferRef.Clear()` → (2) `VMCSharedReceiver.ReadAndClearWriteBuffer` の戻り値を foreach で `_writeBufferRef` に copy → (3) swap pointer → (4) `_readBufferRef` が空 (entries 0) なら OnNext 抑制、 そうでなければ `new HumanoidMotionFrame(timestamp, Array.Empty<float>(), rootPos, rootRot, _readBufferRef)` を OnNext
  - `Timestamp` は `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻する
  - `Shutdown()` / `Dispose()` で `Unsubscribe` + `Release` + `Subject.OnCompleted` を冪等に実行する
  - test seam として `internal State CurrentState` / `InjectBoneRotationForTest` / `InjectRootForTest` / `ForceTickForTest` を提供する
  - 観測完了条件: 4.1 のテストが GREEN、 `Runtime/` 配下に `EVMC4UMoCapSource.cs` が存在せず `VMCMoCapSource.cs` が GUID `42ced34567d8f9012ab345678901cdef` を保持する
  - _Requirements: 1.5, 5.1, 5.2, 5.5, 5.6, 5.7, 5.8, 5.9, 6.3, 7.4, 8.1, 8.3, 8.5, 8.7, 10.1, 10.4, 10.5, 10.6, 11.1, 11.5, 11.7_
  - _Boundary: VMCMoCapSource_
- [x] 4.3 REFACTOR: ダブルバッファ swap 順序検証 + 例外伝播経路
  - `Tick` 内で例外発生時に `IVmcMoCapAdapter.HandleTickException` 経路で `ISlotErrorChannel.Publish(SlotError(slotId, SlotErrorCategory.VmcReceive, ex, UtcNow))` を発行し `MotionStream.OnError` を呼ばないことを検証するテストを追加
  - swap 順序検証: ForceTickForTest を呼んで `_readBufferRef` が前 Tick 値、 `_writeBufferRef` が次 Tick 用に Clear 済みであることを assert (PlayMode で実施)
  - 空 dict の Tick で OnNext が抑制されることを検証
  - `Shutdown` 後 `Subject.OnCompleted` が発行されることを検証
  - 観測完了条件: ダブルバッファ swap 順序 / 例外伝播 / OnCompleted 発行 / OnNext 抑制の全テストが GREEN
  - _Requirements: 5.5, 5.8, 10.4, 11.1, 11.3, 11.5_
  - _Boundary: VMCMoCapSource_

---

## Phase 4: Factory wiring + asmdef references の EVMC4U 削除 (atomic)

> N-R3 制約: ソース側 `using EVMC4U;` 全削除 と asmdef references から `"EVMC4U"` 削除 を **同一論理ステップ** で完了させる。
> Phase 2/3 でソース書換は完了している前提。 Phase 4 は asmdef 編集 + Factory 内 `new EVMC4UMoCapSource(...)` → `new VMCMoCapSource(...)` 置換を一括で行う。
> 完了後の Compile A / B シナリオ (design.md §13) で EVMC4U 不在状態の compile 成功を確認する。

- [x] 5. Factory + asmdef 一括更新 (atomic)
- [x] 5.1 VMCMoCapSourceFactory の Create 戻り値型更新
  - `Runtime/VMCMoCapSourceFactory.cs` 内 `new EVMC4UMoCapSource(...)` を `new VMCMoCapSource(...)` に置換する
  - `using EVMC4U;` が残っている場合は削除する (`.meta` GUID は不変)
  - 観測完了条件: Factory.Create が VMCMoCapSource インスタンスを返す
  - _Requirements: 5.3, 7.4, 8.1_
  - _Boundary: VMCMoCapSourceFactory_
- [x] 5.2 4 つの asmdef references から "EVMC4U" を一括削除
  - `Runtime/RealtimeAvatarController.MoCap.VMC.asmdef` の `references` 配列から `"EVMC4U"` を削除し `"uOSC.Runtime"` を維持する
  - `Editor/RealtimeAvatarController.MoCap.VMC.Editor.asmdef` を確認し `"EVMC4U"` 参照があれば削除する (元から無い想定だが verify)
  - `Tests/EditMode/RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef` の `references` から `"EVMC4U"` 削除、 `"uOSC.Runtime"` 維持
  - `Tests/PlayMode/RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef` の `references` から `"EVMC4U"` 削除、 `"uOSC.Runtime"` を必要に応じて追加
  - 観測完了条件: 全 4 asmdef の references から `"EVMC4U"` 文字列が消える、 各 `.meta` GUID は不変
  - _Requirements: 7.1, 7.2, 7.3, 9.8_
  - _Boundary: asmdef ファイル群_
- [x] 5.3 EVMC4U 全消去後の compile A 確認
  - `Assets/EVMC4U/` が削除済み (Phase 0.1) かつ `using EVMC4U;` / asmdef references から `"EVMC4U"` が全て消えた状態で Unity Editor を再起動し、 `Library/ScriptAssemblies/` を再生成する
  - Console に EVMC4U 関連の missing reference / compile error が出ないことを確認する
  - リポジトリ全体に対し `using EVMC4U;` および asmdef references `"EVMC4U"` が grep で 0 件であることを確認する
  - 観測完了条件: VMC パッケージ単体 + uOSC 依存のみで compile success、 Console clean
  - _Requirements: 1.7, 7.1, 7.2, 7.3, 7.4, 7.5_
  - _Boundary: パッケージ全体_

---

## Phase 5: テスト書換 (Integration / Sharing / 既存テスト)

> Phase 4 まで完了で compile が通る状態。 Phase 5 では既存 PlayMode / EditMode テストを新クラス名 + internal 注入 API へ書換える。
> リネーム + GUID 移植は 3 ファイル、 不変維持は 3 ファイル、 新規追加は 0 (Phase 1〜3 で作成済み)。

- [x] 6. PlayMode 統合テスト書換
- [x] 6.1 VMCMoCapSourceIntegrationTests へリネーム + uOscClient ループバック化
  - `Tests/PlayMode/EVMC4UMoCapSourceIntegrationTests.cs` を `Tests/PlayMode/VMCMoCapSourceIntegrationTests.cs` にリネームし、 旧 `.cs.meta` GUID `9184bfd5f018a534393e68abc0c0dc3b` を移植する
  - `using EVMC4U;` を削除し `Receiver` プロパティ経由のテスト経路を internal 注入 API (`InjectBoneRotationForTest` / `InjectRootForTest` / `ForceTickForTest`) に書換える (旧 `InjectBoneRotationForTest` 経路は廃止 / 新たな internal 注入 API へ統合)
  - in-process uOSC `uOscClient` を作成して `127.0.0.1:port` に既知 bone OSC packet を送出するループバックテストを追加する
  - 受信側 `VMCMoCapSource.MotionStream` を購読して `HumanoidMotionFrame.BoneLocalRotations` に反映されることを assert する
  - 55 bone 全送出フレームで全 bone が `BoneLocalRotations` に含まれることを検証する
  - `MotionStream.OnError` が一度も呼ばれないこと、 `Shutdown` 後 `OnCompleted` が発行されることを検証する
  - 観測完了条件: PlayMode テスト runner で integration tests が GREEN
  - _Requirements: 5.2, 5.5, 9.3, 9.7, 11.1_
  - _Boundary: VMCMoCapSourceIntegrationTests, VMCMoCapSource, VMCSharedReceiver_
- [x] 6.2 VMCMoCapSourceSharingTests へリネーム + 共有 lifecycle 検証
  - `Tests/PlayMode/EVMC4UMoCapSourceSharingTests.cs` を `Tests/PlayMode/VMCMoCapSourceSharingTests.cs` にリネームし、 旧 `.cs.meta` GUID `53c1e6a7f8b94e2cb6d5a89e0c1f2345` を移植する
  - `using EVMC4U;` を削除し `EVMC4USharedReceiver` 参照を `VMCSharedReceiver` に置換する
  - 同一 `VMCMoCapSourceConfig` で複数 Slot が refCount 経由で同一 `VMCSharedReceiver` を共有することを検証する
  - 別 Config (別 port) で別 `VMCSharedReceiver` インスタンスが立ち、 ポート競合しないことを検証する
  - 全 Slot Release 後に GameObject が破棄され `s_instance == null` に戻ることを検証する
  - 観測完了条件: PlayMode テスト runner で sharing tests が GREEN
  - _Requirements: 4.1, 4.3, 4.6, 9.4_
  - _Boundary: VMCMoCapSourceSharingTests, VMCSharedReceiver_

- [x] 7. 不変維持系テストの調整
- [x] 7.1 (P) VmcConfigCastTests / VmcFactoryRegistrationTests の維持確認
  - `Tests/EditMode/VmcConfigCastTests.cs` および `Tests/EditMode/VmcFactoryRegistrationTests.cs` の `using EVMC4U;` を削除する (`.meta` GUID は不変)
  - typeId `"VMC"` / Factory 自己登録 / Config キャスト経路が新実装でも維持されていることを検証するテストを追加または既存検証を実行する
  - 観測完了条件: EditMode runner で 2 ファイルとも GREEN を維持
  - _Requirements: 5.3, 5.9, 9.4_
  - _Boundary: VmcConfigCastTests, VmcFactoryRegistrationTests_
- [x] 7.2 (P) SampleSceneSmokeTests の維持確認
  - `Tests/PlayMode/SampleSceneSmokeTests.cs` を `using EVMC4U;` 等が無ければ無改修で実行し GREEN を確認する (`.meta` GUID 不変)
  - sample asset (`VMCMoCapSourceConfig_Shared.asset` GUID `5c4569b4a17944fba4667acebe26c25f`) の参照解決が破壊されていないことを検証する
  - sample scene `VMCReceiveDemo.unity` を開いた際に `MissingReferenceException` / `The associated script can not be loaded` 警告が出ないことを目視 + assert で検証する
  - 観測完了条件: PlayMode runner で smoke test が GREEN、 sample asset GUID 不変
  - _Requirements: 6.1, 6.2, 6.4, 12.7_
  - _Boundary: SampleSceneSmokeTests, Samples~/VMC/_

---

## Phase 6: ドキュメント / Steering / Third-Party Notices

> 利用者向け準備手順を「uOSC を導入する。これだけ。」 に縮約し、 EVMC4U 撤廃の breaking change を記録する。
> EVMC4U inspiration credit と uOSC / UniVRM credit を `THIRD_PARTY_NOTICES.md` にまとめる。

- [x] 8. パッケージドキュメント全面改訂
- [x] 8.1 (P) パッケージ README 全面改訂
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/README.md` の利用者準備手順を「uOSC を導入する。これだけ。」 に縮約する
  - EVMC4U `.unitypackage` インポート / `EVMC4U.asmdef` 自作 / `evmc4u.patch git apply` の旧手順を全削除する
  - 「Credits」セクションに EVMC4U inspiration credit (共有 receiver パターン / refCount lifecycle / SubsystemRegistration reset) を記載する
  - VMC v2.1 拡張 (BlendShape / Camera 等) は対象外である旨を明記する
  - 観測完了条件: README から `evmc4u.patch` / `EVMC4U.asmdef` / `Assets/EVMC4U/` の手順記述が消え、 セットアップ手順は uOSC 導入のみで完結する
  - _Requirements: 7.6, 7.7, 12.1_
  - _Boundary: README.md_
- [x] 8.2 (P) パッケージ CHANGELOG breaking change エントリ追加
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/CHANGELOG.md` に EVMC4U 依存撤廃の breaking change エントリを追加する
  - 「Internal: EVMC4U dependency removed in favor of native uOSC subscription. Public API (`IMoCapSource`, `MoCapSourceConfigBase`, `HumanoidMotionFrame`, typeId `VMC`) is unchanged.」 を記載する
  - 観測完了条件: CHANGELOG に当該エントリが日付付きで追加される
  - _Requirements: 12.2_
  - _Boundary: CHANGELOG.md_
- [x] 8.3 (P) THIRD_PARTY_NOTICES.md 新規作成
  - `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/THIRD_PARTY_NOTICES.md` を新規作成する (ランダム GUID `.meta` 付き)
  - uOSC `com.hidano.uosc` (MIT) credit を `Library/PackageCache` 内の license から転記する
  - VMC プロトコル仕様 (`https://protocol.vmc.info/specification`) への準拠表明を記載する
  - EVMC4U (MIT) inspiration credit を記載する (共有 receiver / refCount / SubsystemRegistration の設計借用元として)
  - Phase 0.1 で削除した `Assets/EVMC4U/3rdpartylicenses(ExternalReceiverPack).txt` 内の uOSC / UniVRM credit を転記する
  - 観測完了条件: THIRD_PARTY_NOTICES.md が新規追加され、 削除した EVMC4U 同梱 license の内容が新ファイルに保全される
  - _Requirements: 3.6, 7.7_
  - _Boundary: THIRD_PARTY_NOTICES.md_
- [x] 8.4 リポジトリルート / core README の利用者準備変更を反映
  - リポジトリルート README または core パッケージ README で VMC パッケージの利用者準備が EVMC4U 不要に変わった旨を反映する
  - 観測完了条件: ルート / core README の VMC 関連記述が「uOSC のみ依存」 表現に統一される
  - _Requirements: 12.3_
  - _Boundary: README.md (root / core)_
- [x] 8.5 .kiro/steering/structure.md の VMC パッケージ説明更新
  - `.kiro/steering/structure.md` の VMC パッケージ説明を「EVMC4U 連携による VMC 受信」 から「自前実装による VMC 受信 (uOSC のみ依存)」 へ更新する
  - 利用者側セットアップ要件記述を「core + uOSC」に変更する (EVMC4U を除外)
  - 観測完了条件: structure.md を読んだ AI / 開発者が「VMC パッケージは EVMC4U 不要」 と理解できる状態になる
  - _Requirements: 12.4_
  - _Boundary: .kiro/steering/structure.md_

---

## Phase 7: Validation (Compile A / B / Runtime C)

> design.md §13 に対応する 3 段階の検証を実施し、 EVMC4U 撤廃が破壊的影響を持たないことを確認する。

- [x] 9. 統合検証
- [x] 9.1 Compile A: VMC パッケージ単体 + uOSC のみで compile 成功
  - 利用者プロジェクトに `com.hidano.realtimeavatarcontroller.mocap-vmc` + `com.hidano.uosc` のみインストールした状態を再現する
  - `Assets/EVMC4U/` が存在しなくても compile error が出ないことを確認する
  - `Library/ScriptAssemblies/` 再生成後の Console に `EVMC4U` 関連 warning / error が一切出ないこと (N-R1 mitigation) を確認する
  - 観測完了条件: VMC パッケージ単体 compile 成功 + Console clean
  - _Requirements: 1.7, 7.5, 7.1, 7.2, 7.3_
  - _Boundary: パッケージ統合_
- [x] 9.2 Compile B: VMC パッケージ + Sample import で compile 成功 + sample scene 開閉
  - VMC sample (`Samples~/VMC/`) を import し、 sample scene `VMCReceiveDemo.unity` を Editor で開く
  - `MissingReferenceException` / `The associated script can not be loaded` 系警告が出ないことを目視確認する
  - `VMCMoCapSourceConfig_Shared.asset` の GUID `5c4569b4a17944fba4667acebe26c25f` が不変であることを `.meta` 確認する
  - 観測完了条件: sample import + scene open で警告ゼロ、 既存 GUID 不変
  - _Requirements: 6.1, 6.4, 12.7_
  - _Boundary: Samples~/VMC/_
- [x] 9.3 Runtime C: 実 OSC packet ループバックでアバター追従検証
  - sample scene `VMCReceiveDemo.unity` を Play、 同一 Unity プロセス内 `uOscClient` または別プロセス VMC 送信側 (VirtualMotionCapture / VSeeFace 等) から実 OSC packet を `127.0.0.1:port` に流す
  - アバターが正常に追従することを目視で確認する
  - PlayMode 自動テスト (Phase 5.1 で書換た integration test) が同一シナリオで GREEN になることを確認する
  - 観測完了条件: Runtime ループバックでアバター追従、 自動 integration test GREEN
  - _Requirements: 1.1, 1.2, 1.3, 1.5, 2.1, 2.2, 5.2, 5.5, 11.1_
  - _Boundary: VMC sample 統合_
- [x] 9.4* アプリ層 Tick あたり alloc 0 byte 計測
  - Unity Profiler `GC.Allocated` を `_writeBufferRef` への copy + swap + `new HumanoidMotionFrame` 区間に限定して計測する
  - `HumanoidMotionFrame` 1 個分 (= 既知の不可避 alloc) 以外が 0 byte であることを確認する
  - IL2CPP / Mono 双方で計測する (uOSC 由来 boxing は計測区間外)
  - 観測完了条件: アプリ層 Tick あたり alloc が `HumanoidMotionFrame` 1 個分のみ
  - _Requirements: 10.1, 10.2, 10.3, 10.5, 10.6_
  - _Boundary: VMCMoCapSource (perf 計測)_
- [x] 9.5 spec.json メタデータ更新と最終確認
  - `.kiro/specs/mocap-vmc-native/spec.json` の `phase` を `"tasks-generated"` に更新する
  - `approvals.tasks.generated = true`、 `approvals.tasks.approved = true` (auto-approve) に設定する
  - `updated_at` をタスク完了タイムスタンプに更新する
  - `ready_for_implementation = true` に設定する
  - リポジトリ全体に対し `using EVMC4U;` 残存ゼロ、 asmdef references `"EVMC4U"` 残存ゼロ、 `Assets/EVMC4U/` 不在を最終確認する
  - 観測完了条件: spec.json 更新完了、 EVMC4U 関連残存ゼロ、 全テスト GREEN
  - _Requirements: 12.8_
  - _Boundary: spec.json, リポジトリ全体_

---

## Requirements Coverage Map

| Requirement | 担当タスク |
|---|---|
| 1.1 | 3.2, 9.3 |
| 1.2 | 3.2, 9.3 |
| 1.3 | 2.2, 9.3 |
| 1.4 | 2.1, 2.2, 2.3 |
| 1.5 | 3.2, 4.2, 9.3 |
| 1.6 | 3.2 |
| 1.7 | 5.3, 9.1 |
| 2.1 | 2.1, 2.2, 9.3 |
| 2.2 | 2.1, 2.2, 9.3 |
| 2.3 | 2.2 |
| 2.4 | 2.2 |
| 2.5 | 2.2 |
| 2.6 | 2.1, 2.2 |
| 2.7 | 2.2, 2.3 |
| 3.1 | 1.1, 1.2, 1.3 |
| 3.2 | 1.1, 1.2 |
| 3.3 | 1.1, 2.2 |
| 3.4 | 1.1, 2.2 |
| 3.5 | 1.2, 1.3 |
| 3.6 | 8.3 |
| 4.1 | 3.1, 6.2 |
| 4.2 | 3.1 |
| 4.3 | 3.1, 6.2 |
| 4.4 | 3.1, 3.3 |
| 4.5 | 3.2 |
| 4.6 | 3.1, 6.2 |
| 5.1 | 4.1, 4.2 |
| 5.2 | 4.1, 4.2, 6.1, 9.3 |
| 5.3 | 5.1, 7.1 |
| 5.4 | 7.1 |
| 5.5 | 4.2, 4.3, 6.1, 9.3 |
| 5.6 | 4.2 |
| 5.7 | 4.2 |
| 5.8 | 4.2, 4.3 |
| 5.9 | 4.2, 7.1 |
| 6.1 | 7.2, 9.2 |
| 6.2 | 7.2 |
| 6.3 | 3.2, 4.2 |
| 6.4 | 7.2, 9.2 |
| 6.5 | 1.1, 1.2, 2.1, 2.2, 3.1, 8.3 |
| 7.1 | 5.2, 9.1 |
| 7.2 | 5.2, 9.1 |
| 7.3 | 5.2, 9.1 |
| 7.4 | 3.2, 4.2, 5.1 |
| 7.5 | 0.1, 9.1 |
| 7.6 | 8.1 |
| 7.7 | 8.1, 8.3 |
| 8.1 | 4.2, 5.1 |
| 8.2 | 3.2 |
| 8.3 | 3.2, 4.2 |
| 8.4 | (Decision E / R-C で「不要」 確定済み — 設計フェーズ完了済) |
| 8.5 | 4.1, 4.2 |
| 8.6 | 3.1, 3.2 |
| 8.7 | 3.2, 4.2 |
| 9.1 | 0.2 |
| 9.2 | 0.2, 3.1 |
| 9.3 | 6.1 |
| 9.4 | 4.1, 6.2, 7.1 |
| 9.5 | 1.1, 2.1 |
| 9.6 | 2.1 |
| 9.7 | 3.1, 6.1 |
| 9.8 | 5.2 |
| 10.1 | 4.2, 9.4 |
| 10.2 | 2.2, 2.3, 9.4 |
| 10.3 | 1.2, 2.2, 9.4 |
| 10.4 | 4.2, 4.3 |
| 10.5 | 4.2, 9.4 |
| 10.6 | 4.2, 9.4 |
| 11.1 | 4.3, 6.1, 9.3 |
| 11.2 | 2.2 |
| 11.3 | 3.2, 4.3 |
| 11.4 | 3.1, 3.2, 3.3 |
| 11.5 | 4.1, 4.2, 4.3 |
| 11.6 | (R-11.6 で「初期版で実装しない」 と要件化済 — タスク不要) |
| 11.7 | 4.1, 4.2 |
| 12.1 | 8.1 |
| 12.2 | 8.2 |
| 12.3 | 8.4 |
| 12.4 | 8.5 |
| 12.5 | 0.3 |
| 12.6 | 0.1 |
| 12.7 | 7.2, 9.2 |
| 12.8 | 9.5 |

> **Coverage Note**: R-8.4 (`[MovedFrom]` の必要性判断) および R-11.6 (受信タイムアウト初期版未実装) は要件本文で結論済 (Decision E / R-C / R-11.6) のためタスク化不要。 design.md / research.md で意思決定済みであることを spec.json metadata で示す。
