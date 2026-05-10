# Research & Design Decisions — mocap-vmc-native

---
**Purpose**: 設計フェーズで確定すべき技術的論点 (R-A 〜 R-H) に対して、 実コード・実ファイル参照に基づく根拠と最終決定を記録する。 design.md の各章で参照される一次資料として位置付ける。
---

## Summary

- **Feature**: `mocap-vmc-native`
- **Discovery Scope**: Complex Integration (既存 Tick / Slot / MotionCache / HumanoidMotionApplier 経路への自前 OSC 受信実装の挿し込み)
- **Key Findings**:
  1. `HumanoidMotionApplier.Apply` は `BoneLocalRotations` を `foreach` で読み終え、 同期的に `Transform.localRotation` へ書込んだ後に return する。 frame をまたいで dict 参照を保持しない。
  2. `MotionCache._latestFrame` は `Interlocked.Exchange` で OnNext された frame を保持するため、 直前 frame を最大 1 Tick 期間 retain する。 ダブルバッファ戦略では 「OnNext 後に書込側 buffer を Clear」 という順序を守れば衝突しない。
  3. uOSC `DotNet/Udp.StartServer` は bind 失敗を **try/catch + Debug.LogError + `state_ = State.Stop`** で握り潰す。 `SocketException` を呼出元に伝播させない。 → R-11.4 の bind 失敗検出は `uOscServer.isRunning` を `StartServer()` 後に確認する経路へ再設計する必要あり。
  4. uOSC `Parser.ParseData` は受信メッセージあたり `new object[n]` + `float` boxing + `string` (bone name + Substring 結果) を必ず確保する。 アプリ層の R-10 計測スコープは「 `onDataReceived` 受領 (uOSC `Message` 受領後) から OnNext 直前まで」に厳密化する。
  5. VMC bone 名は Unity `HumanBodyBones` enum 名と完全一致し、 `Enum.GetValues(typeof(HumanBodyBones))` ベース静的辞書で網羅できる。 EVMC4U の `HumanBodyBonesTryParse` 経路の借用は不要。

---

## Research Log

### R-A. snapshot Dictionary の再利用戦略 (ダブルバッファリング)

- **Context**: R-5.5 (snapshot コピー) と R-10.4 (再利用バッファ) の同居可否。 `HumanoidMotionFrame.BoneLocalRotations` の所有権契約と再利用が衝突しないことを実コード読解で検証する。
- **Sources Consulted**:
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Applier/HumanoidMotionApplier.cs` L113-146, L217-257
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Cache/MotionCache.cs` L84, L99-113
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Frame/HumanoidMotionFrame.cs` L100-103 (所有権コメント)
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Samples~/UI/Runtime/SlotManagerBehaviour.cs` L96-115 (LateUpdate consumer)
- **Findings**:
  - `HumanoidMotionApplier.Apply` (L113) → `ApplyInternal` (L217) は同期的に `foreach (var kv in boneRotations)` で読み終え、 即座に `boneTf.localRotation = kv.Value` を書込んで return する (L230-237)。 dict 参照を `_lastGoodPose` 等のフィールドに **保存しない**。
  - `MotionCache._latestFrame` (L34) は `volatile MotionFrame` で、 `Interlocked.Exchange(ref _latestFrame, frame)` (L112) によって OnNext のたびに置換される。 直前 frame は次 OnNext まで retain される (= 最大 1 Tick 周期分)。
  - `SlotManagerBehaviour.LateUpdate` (L96-115) は `var frame = pipeline.Cache.LatestFrame;` でローカル変数にキャプチャしてから `capturedApplier.Apply(frame, ...)` を呼ぶ。 LateUpdate 内同期完結。
- **Implications**:
  - **同 frame 完結性は実コードで保証**: Update 段で uOSC が dispatch → ハンドラが write buffer に書込み → Tick 境界で swap → OnNext (MotionCache が `_latestFrame` を atomic に置換) → LateUpdate で Apply が `LatestFrame` を読み始める。 LateUpdate 終了時点で読みは完結。
  - **直前 frame の dict 参照は MotionCache が次 OnNext まで保持**。 ダブルバッファ戦略では、 「**新 frame の OnNext を発行してから旧 buffer の Clear を実行**」 という順序を守れば、 MotionCache が次 OnNext まで保持していた frame の dict 参照が解放されたタイミングで Clear が走る (時間的に Clear は次 Tick の Update 段、 Apply は LateUpdate 段で完了済み)。
- **Decision**: **ダブルバッファリング採用** (詳細は Design Decision §A 参照)。 buffer 型は `Dictionary<HumanBodyBones, Quaternion>` × 2、 swap タイミングは Update 末尾 (LateUpdate より前)、 swap 順序は (1) capture write → (2) swap pointer → (3) OnNext (新 readBuffer) → (4) 旧 readBuffer = 次 writeBuffer は次 Tick で Clear。

### R-B. uOSC `onDataReceived` の MainThread 発火保証

- **Context**: dig-native.md N-C2 で uOSC 構造的に保証されると確認済み。 design.md で再確認。
- **Sources Consulted**:
  - `RealtimeAvatarController/Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/uOscServer.cs` L81-97
  - `RealtimeAvatarController/Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/Core/DotNet/Udp.cs` L60-71 (worker thread = enqueue のみ)
  - `RealtimeAvatarController/Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/Core/Parser.cs` L31-60 (lock-protected enqueue)
- **Findings**:
  - uOSC worker thread は受信 byte buffer を `messageQueue_` に enqueue するのみで `onDataReceived.Invoke` は呼ばない (Udp.cs L60-71)。
  - `uOscServer.Update` (Unity MainThread) → `UpdateReceive` (L87) → `parser_.Dequeue()` → `onDataReceived.Invoke(message)` (L92) という直列パスで MainThread 発火が構造的に保証される。
- **Decision**: 確定 (構造的保証)。 design.md の Threading 章で uOSC 経路を MainThread 受信前提として記述する。

### R-C. `[MovedFrom]` 属性の必要性

- **Context**: `EVMC4UMoCapSource` / `EVMC4USharedReceiver` を新クラス名にリネームする際、 既存 SerializeReference / scene 配置経路で `[UnityEngine.Scripting.APIUpdating.MovedFrom]` が必要かを判断する。
- **Sources Consulted**:
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/EVMC4UMoCapSource.cs` (plain C# class, MonoBehaviour ではない)
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/EVMC4USharedReceiver.cs` L49 (`MonoBehaviour` だが factory 経由で動的生成のみ、 scene への配置なし)
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Samples~/VMC/Scenes/VMCReceiveDemo.unity` 全体 grep `EVMC4U` → 0 件
  - `RealtimeAvatarController/ProjectSettings/ProjectSettings.asset` `EVMC4U` → 1 件 (`Standalone: EVMC4U_JA` プロダクト名のみ。 scripting 参照ではない)
  - 他の `.unity` / `.prefab` / `.asset` ファイルへの `EVMC4U` 文字列 grep → 0 件
- **Findings**:
  - `EVMC4UMoCapSource` は plain C# class、 SerializeReference で参照されない。
  - `EVMC4USharedReceiver` は MonoBehaviour だが `EnsureInstance` 経由で動的生成され、 scene asset / prefab にドラッグ配置されていない。 `DontDestroyOnLoad` 後の hierarchy にのみ存在する。
  - 既存 sample scene `VMCReceiveDemo.unity` にも EVMC4U 系参照は無く、 ProjectSettings の `Standalone: EVMC4U_JA` は単なるプロダクト名 (= プロダクト識別子) であり scripting 参照ではない。
- **Decision**: **`[MovedFrom]` 属性は不要**。 リネーム時は旧 `.cs` の `.meta` GUID を新 `.cs` へ移植する (R-6.3) ことで Inspector / asset 参照不変性を維持する。 ただし `EVMC4USharedReceiverTests.cs` は GUID 移植せず (R-9.2 / N-H2)、 削除 + 新規作成。

### R-D. bone マッピング license / credit (解消済み)

- **Context**: R-3.1 で列挙した VMC bone 名 → `HumanBodyBones` マッピングが EVMC4U 由来のオリジナリティを借用するか。
- **Sources Consulted**:
  - `Assets/EVMC4U/ExternalReceiver.cs` L1437-1463 (`HumanBodyBonesTryParse`)
  - `UnityEngine.HumanBodyBones` enum (Unity Manual)
- **Findings**:
  - VMC プロトコル送出 bone 名 (`Hips` / `Spine` / ... / `RightLittleDistal`) は Unity `HumanBodyBones` enum 名と完全一致。
  - EVMC4U の `HumanBodyBonesTryParse` は `Enum.TryParse<HumanBodyBones>(value, true)` + キャッシュという実装で、 オリジナリティは「VMC が enum 名を送る前提で `Enum.TryParse` を使う」というアイデアに過ぎない。 アイデアは copyright 保護対象外。
- **Decision**: **マッピングテーブル生成は `Enum.GetValues(typeof(HumanBodyBones))` ベースの機械的列挙**。 EVMC4U MIT credit は不要。 ただし「共有 receiver パターン」「refCount lifecycle」「`SubsystemRegistration` リセット」 といった設計借用については R-7.7 に従い README / CHANGELOG / コードコメントに inspiration credit を残す。

### R-E. VMC v2.1 拡張のサポート範囲

- **Context**: VMC v2.1 仕様で増える `/VMC/Ext/Blend/Val` (BlendShape)、 `/VMC/Ext/Cam/Pos` (Camera)、 `/VMC/Ext/Hmd/Pos` (Tracker)、 Root の 14 引数版等への対応可否。
- **Sources Consulted**:
  - requirements.md 「Out of scope」 (BlendShape / Camera / Light / Tracker Status を明示除外)
  - requirements.md R-2.6 (14 引数 Root を 8 引数までで打切る非例外動作)
- **Findings**:
  - BlendShape / 表情 / Camera / Light / Tracker Status はすべて「現 Spec の射程外」 と明示済み。
  - Root の 14 引数版 (v2.1) は本 Spec で先頭 8 引数のみ解釈し、 残余を例外なく無視する経路を要件化済み。
- **Decision**: **基本部 (`/VMC/Ext/Bone/Pos` + `/VMC/Ext/Root/Pos` の各 8 引数) のみ対応**。 拡張対応は後続 Spec へ。 design.md 「OSC dispatch table」で sliently-ignored アドレスを明文化する (`/VMC/Ext/Blend/*`、 `/VMC/Ext/Cam/*`、 `/VMC/Ext/Hmd/*`、 `/VMC/Ext/Con/*`、 `/VMC/Ext/Tra/*`、 `/VMC/Ext/Light/*`、 `/VMC/Ext/Setting/*`、 `/VMC/Ext/OK`、 `/VMC/Ext/T` など)。

### R-F. `Assets/EVMC4U/` および `evmc4u.patch` の disposition

- **Context**: 旧 EVMC4U 同梱ディレクトリと patch artifact の最終処遇。
- **Sources Consulted**:
  - dig-native.md N-M1 / N-M2
  - `Assets/EVMC4U/3rdpartylicenses(ExternalReceiverPack).txt` (uOSC / UniVRM credit を含む)
  - `.kiro/specs/mocap-vmc/evmc4u.patch` (predecessor spec deliverable)
- **Findings**:
  - `Assets/EVMC4U/` 全体を削除する場合、 含まれる uOSC / UniVRM credit を別文書へ転記する義務 (MIT 等の license 義務) がある。
  - `evmc4u.patch` は EVMC4U 撤廃後に意義を失うが、 履歴として `git log` で追跡可能。
  - 子 Agent では `rm` / `git rm` 不可 (CLAUDE.md feedback メモ)。 親セッション専用タスクとして tasks.md に明示する必要。
- **Decision**:
  - **`Assets/EVMC4U/` 全削除** (親セッションタスク)。 含まれる uOSC / UniVRM 等 third-party credit は新規 `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/THIRD_PARTY_NOTICES.md` へ転記。
  - **`evmc4u.patch` は削除せず冒頭に OBSOLETE マーカ + obsolete 化日時 (`2026-05-09` 以降) を記載した上で履歴用に残す** (history preservation)。 git log でも追跡可能だが、 spec 文書クロスリファレンスからの参照経路を断たない目的で文書として保持する。
  - `.kiro/specs/mocap-vmc/handover-7.2.md` 等 patch 関連記述には OBSOLETE マーカを追加する (handover 整合性維持)。

### R-G. uOSC `DotNet/Udp.StartServer` の bind 失敗伝播挙動

- **Context**: R-11.4 「`uOscServer.StartServer()` がポートバインドに失敗したとき `Initialize()` から例外を呼出元へスローする」 経路の妥当性確認。
- **Sources Consulted**:
  - `RealtimeAvatarController/Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/Core/DotNet/Udp.cs` L37-71 (`StartServer`)
  - `RealtimeAvatarController/Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/uOscServer.cs` L57-67 (`StartServer`)
- **Findings (重要)**:
  ```csharp
  // Udp.cs L37-58
  public override void StartServer(int port)
  {
      Stop();
      state_ = State.Server;

      try
      {
          endPoint_ = new IPEndPoint(IPAddress.Any, port);
          udpClient_ = new UdpClient(AddressFamily.InterNetwork);
          udpClient_.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
          udpClient_.Client.Bind(endPoint_);   // ← 同期的に bind を試行
      }
      catch (System.Exception e)
      {
          UnityEngine.Debug.LogError(e.ToString());   // ← 例外を log に出すだけ
          state_ = State.Stop;                          // ← state を Stop に戻す
          return;                                        // ← 例外を再スローしない !!!
      }
      // 以降 thread_.Start(...) で worker 起動
  }
  ```
  - **`StartServer` は bind 失敗時に例外を握り潰し `state_ = State.Stop` に戻して return する**。 `SocketException` は呼出元に伝播しない。
  - bind は **同期的** に MainThread で実行される (worker thread 起動の `thread_.Start(...)` は L60 で bind 成功後にのみ走る)。
  - 失敗時、 `udp_.isRunning` (L32: `state_ != State.Stop`) は **false** を返す。
- **Implications**:
  - R-11.4 の前提 (`StartServer()` から `SocketException` が伝播する) は **実装上成立しない**。
  - bind 失敗を検出するには `_server.StartServer()` 直後に `_server.isRunning` を確認する経路に再設計する必要がある。
  - `Debug.LogError` への出力は uOSC が握っているため、 `SlotErrorCategory.InitFailure` への誘導は受信側 (本 Spec) が能動的に行う必要がある。
- **Decision**: **`Initialize()` 内で `_server.StartServer()` 呼出後に `_server.isRunning == false` を判定し、 `SocketException` ではなく自前の `InvalidOperationException` (詳細メッセージで「ポート N へのバインドに失敗しました。 既に使用中の可能性があります。 詳細は uOSC の Debug.LogError を参照してください」 を含む) をスローして呼出元 (`SlotManager`) へ伝播させる**。 `SlotManager` 側の `try/catch` で `SlotErrorCategory.InitFailure` として `ISlotErrorChannel` 通知に乗る経路は維持される。 design.md §エラー処理章に明記。

### R-H. VMC bone 名の PascalCase 規定確認

- **Context**: R-3.4 「文字列の `Split` / `ToLower` 等を行わない」 = case-sensitive matching の妥当性確認。
- **Sources Consulted**:
  - VMC 公式仕様 https://protocol.vmc.info/ (オンライン参照)
  - `Assets/EVMC4U/ExternalReceiver.cs` L1437-1463 (`HumanBodyBonesTryParse` の case-insensitive 解決の動機)
  - Unity Manual `HumanBodyBones` enum (PascalCase)
- **Findings**:
  - VMC 公式仕様サイト (`https://protocol.vmc.info/specification`) は bone 名を Unity `HumanBodyBones` enum 名 = PascalCase で送出する旨を明記している (`Hips`、 `LeftUpperArm` 等)。
  - 主要 VMC 送信実装 (VirtualMotionCapture / VSeeFace / VMagicMirror) は PascalCase で送出する。
  - EVMC4U の case-insensitive 経路 (`Enum.TryParse(... ignoreCase: true)`) は「念のため」 の防御的実装に近く、 仕様遵守送信元では発動しない。
- **Decision**: **case-sensitive matching を採用** (PascalCase 厳格)。 仕様準拠送信元との互換性は確保され、 仕様逸脱送信元 (バグ持ち) は未知 bone として黙って無視される (R-3.3)。 R-H は確定。
- **Follow-up**: 互換性問題が運用上発生した場合のみ後続 Spec で case-insensitive 経路 (静的辞書を ordinal-ignore-case 比較で構築) への切替を検討。

---

## Architecture Pattern Evaluation (snapshot dictionary 戦略)

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| (A) 毎 Tick 新規 Dictionary | 現行 `EVMC4UMoCapSource.Tick` 同様、 Tick 毎に `new Dictionary<HumanBodyBones, Quaternion>(view.Count)` | 安全・単純・所有権契約と完全整合 | Tick あたり ~1.5 KB の Gen0 allocation。 60Hz で ~90 KB/sec。 R-10.5 の 0 byte target 不可 | 現行版の挙動。 retreat 候補 |
| (B) ダブルバッファリング | フィールドに `Dictionary` × 2、 Tick 毎に swap | alloc 0 (起動時 2 個確保のみ)。 同 frame 完結性前提で安全 | Apply / Cache 側が frame をまたいで dict 参照保持しない前提が崩れた瞬間に破壊。 検証が design 必須 | **採用 (本 Spec)** |
| (C) Custom IReadOnlyDictionary wrapper + 共有 backing | frame 番号で生死を管理する struct wrapper | alloc 0 + 契約再解釈不要 | 実装複雑度大、 BCL の `IReadOnlyDictionary` を満たす全 method 実装が必要 | 過剰設計 |
| (D) `ReadOnlyDictionary` ラッパ + 共有 backing | BCL `ReadOnlyDictionary` で wrap、 backing は共有 | 標準型を活用できる | wrap 自体が `new` で alloc を発生させる。 backing mutate 時の reader 破壊問題は (B) と同質 | 利点なし |
| (E) `HumanoidMotionFrame` の所有権契約緩和 | 「同 frame 内消費前提で参照渡し許容」と明文化 | (B) を採用するための前提条件 | motion-pipeline contracts への影響範囲を確認する必要 | (B) と組合せて採用 |

---

## Design Decisions

### Decision A: snapshot Dictionary 戦略 — ダブルバッファリング採用

- **Context**: R-10.5 (Tick あたり 0 byte target) と `HumanoidMotionFrame` 所有権契約 (frame 内 dict 参照保持禁止) を両立する戦略。
- **Alternatives Considered**:
  1. (A) 毎 Tick 新規 `new Dictionary` — alloc 0 不可
  2. (B) ダブルバッファリング — 同 frame 完結性前提で alloc 0 達成
  3. (C) Custom wrapper struct — 過剰設計
  4. (D) `ReadOnlyDictionary` wrap — 利点なし
- **Selected Approach**:
  - `VmcMoCapSource` 内に `Dictionary<HumanBodyBones, Quaternion> _bufferA`、 `_bufferB` をフィールドで保持。
  - `_writeBuffer` ポインタ (どちらが書込側か) と `_readBuffer` ポインタ (直前 OnNext で emit した側) を持つ。
  - OSC ハンドラ (`/VMC/Ext/Bone/Pos`) は `_writeBuffer[bone] = quaternion` を実行 (新規エントリは Dictionary が再ハッシュなく追加できる初期容量 64 で確保済み)。
  - Tick 境界 (詳細は §B) で:
    1. `var snapshot = _writeBuffer;` (alias capture)
    2. swap: `_writeBuffer = _readBuffer; _readBuffer = snapshot;`
    3. `OnNext(new HumanoidMotionFrame(... boneLocalRotations: snapshot))` (`HumanoidMotionFrame` は `IReadOnlyDictionary` を保持)
    4. `_writeBuffer.Clear()` (旧 readBuffer = 次 writeBuffer を消去。 旧 readBuffer は MotionCache が直前 frame として retain していたが、 step 3 の OnNext で MotionCache が atomic に置換したため retainer は無い)
  - Root Position / Rotation はフィールド保持で十分 (struct 値型)。
- **Rationale**: `HumanoidMotionApplier.Apply` は同期的に foreach で読み終え、 `MotionCache._latestFrame` は OnNext で atomic 置換される。 swap 順序を上記通り守れば、 Clear 実行時点で旧 readBuffer の retainer は存在しない。
- **Trade-offs**:
  - メリット: アプリ層 alloc 0 (起動時 2 個 Dictionary 確保のみ)。 60Hz × 55 bone 連続稼働でも GC 負荷ゼロ。
  - デメリット: `HumanoidMotionFrame` の所有権契約を 「同 frame 内消費前提」 と明示する必要 (Decision E)。 contracts.md 改訂は本 Spec の射程外のため、 design.md にて 「VMC native 実装は同 frame 完結性に依存する」 と明文化する補完アプローチを採る。
- **Follow-up**: validation で `HumanoidMotionApplier` 改修 / `MotionCache` 拡張が発生した場合に再検証。 PlayMode 統合テストで「次 Tick でも前 frame の dict が読めなくなる (= 上書き)」 ことを assert する。

### Decision B: Tick 境界の配置

- **Context**: ダブルバッファ swap タイミングを Update / LateUpdate / OSC handler 内のいずれに置くか。
- **Alternatives Considered**:
  1. OSC handler 内で逐次 emit (1 メッセージごと) — 1 frame に 50+ 回 OnNext で過剰
  2. Update 末尾で 1 回 emit — uOscServer.Update 完了後の即時境界
  3. LateUpdate で 1 回 emit — Apply 直前すぎて MotionCache の `_latestFrame` 更新が遅い
- **Selected Approach**: **`VmcSharedReceiver` の `Update()` 末尾で Tick 境界を起動**。 uOscServer.Update が同 GameObject 上で先に走る (script execution order: AddComponent 順) ため、 receiver の `Update()` が呼ばれた時点で当該フレームに到着した OSC メッセージは全て `onDataReceived` で処理済み。 この時点で swap + OnNext を発行する。
- **Rationale**:
  - Unity script execution: Update phase は LateUpdate phase より先 → MotionCache.LatestFrame は LateUpdate 段で確実に最新。
  - uOscServer.Update + VmcSharedReceiver.Update は同 GameObject 上で連続実行され、 同期完結。
  - 1 frame に 1 回の OnNext で BoneLocalRotations 1 セットを emit する rate 制御が成立する (60Hz frame rate に同期)。
- **Trade-offs**:
  - メリット: シンプル、 タイミング決定的、 frame rate と同期。
  - デメリット: VMC 送信側が 90Hz 等の異 rate で送ってきた場合、 同 frame 内の最新値で上書きされる。 これは LateUpdate Apply の Tick 仕様 (frame rate 駆動) と整合するため許容。
- **Follow-up**: VSeeFace / VMagicMirror の送信 rate (実機計測) と Unity frame rate のミスマッチが起きた場合の品質を validation で観測。

### Decision C: bind 失敗検出経路 (R-G)

- **Context**: R-11.4 が前提とした `StartServer()` 例外伝播は uOSC `DotNet/Udp` で成立しない (R-G 確認)。
- **Alternatives Considered**:
  1. uOSC を fork して bind 例外を伝播するよう改修 — Out of Scope
  2. `_server.StartServer()` 呼出後に `_server.isRunning` を判定し、 false なら自前例外スロー — uOSC API 内で完結
  3. `onServerStarted` event subscribe 経由 — 失敗時 event は発火しないので timeout が必要、 同期判定が困難
- **Selected Approach**: **`VmcSharedReceiver.ApplyReceiverSettings` 内で `_server.StartServer()` 呼出後に `_server.isRunning == false` を判定し、 `InvalidOperationException` をスロー**。 例外メッセージに失敗ポート番号と uOSC `Debug.LogError` を参照させる注釈を含める。
- **Rationale**: uOSC 改修なしで bind 失敗を検出できる唯一の経路。 worker thread 起動も bind 成功後なので、 isRunning 判定は同期的に正しい結果を返す。
- **Trade-offs**:
  - メリット: uOSC 不変、 SlotManager の `SlotErrorCategory.InitFailure` 経路に乗せられる。
  - デメリット: uOSC 側 `Debug.LogError` のメッセージが Console に出るのは抑制できない (二重表示)。 `DefaultSlotErrorChannel` 側の重複制御では捕えきれないため、 ユーザ視点では bind 失敗時に Console に 2 行出る。 受容。
- **Follow-up**: 将来 uOSC が bind 失敗 event を提供したら切替検討。

### Decision D: クラス命名 — `VMCMoCapSource` / `VMCSharedReceiver` (大文字 'VMC')

- **Context**: 既存 `VMCMoCapSourceConfig` / `VMCMoCapSourceFactory` は全て大文字 `VMC` を採用。 新規クラス命名で modern C# convention の `VmcMoCapSource` (lowercase 'mc') と既存準拠の `VMCMoCapSource` のいずれを採るか。
- **Alternatives Considered**:
  1. (a) `VmcMoCapSource` / `VmcSharedReceiver` — modern convention (acronym ≥3 chars をキャメルケース化)
  2. (b) `VMCMoCapSource` / `VMCSharedReceiver` — 既存 `VMCMoCapSourceConfig` / `VMCMoCapSourceFactory` / `VMCMoCapSourceConfig_Shared.asset` 命名と一貫
- **Selected Approach**: **(b) `VMCMoCapSource` / `VMCSharedReceiver`** を採用。
- **Rationale**:
  - 既存 `VMCMoCapSourceConfig` / `VMCMoCapSourceFactory` はリネーム対象外であり (typeId 不変・ GUID 不変が要件)、 新規クラスのみ命名規約変更すると同パッケージ内で `Vmc*` と `VMC*` が混在する。
  - sample asset 名 (`VMCMoCapSourceConfig_Shared.asset`) との一貫性。
  - VMC 公式仕様ドキュメントも全て大文字 `VMC` 表記。
- **Trade-offs**:
  - メリット: パッケージ内命名一貫性、 sample 名一致。
  - デメリット: modern C# convention から外れる (Microsoft の 2017 以降の guideline は `Vmc` 推奨)。 受容。
- **Follow-up**: 後続 Spec で全 `VMC*` クラスを `Vmc*` 系へ統一リネームする際は `[MovedFrom]` を活用すべし (本 Spec では retreat しない)。

### Decision E: `HumanoidMotionFrame` 所有権契約の補完 (Spec 内自己宣言)

- **Context**: ダブルバッファ採用は `HumanoidMotionApplier` が同 frame 内で dict を消費完了する前提に依存する。 `HumanoidMotionFrame.cs` のコメントは「呼出元から所有権を移譲、 Applier 側でコピーせず参照保持する」 と明示するのみで「frame をまたいで保持しない」 は明示されていない。
- **Alternatives Considered**:
  1. `HumanoidMotionFrame.cs` の docstring を改訂して 「同 frame 内消費前提」 を追記 — motion-pipeline 側の改修となり本 Spec 射程外
  2. 本 Spec design.md 内で 「VMC native MoCap source は同 frame 完結性前提に依存する」 と自己宣言 — Spec 自閉的
  3. 毎 Tick 新規 Dictionary に retreat — alloc 0 不可
- **Selected Approach**: (2) **本 Spec design.md 内で同 frame 完結性前提を明文化**。 該当節は「データフロー」「ライフサイクル」 章。 motion-pipeline contracts.md の改訂は本 Spec の Out of Scope (依存方向逆転を避ける)。
- **Rationale**: 受信側が消費パターンを規定するのは依存方向違反だが、 「VMC native 実装が依存する前提」 として記述する分には逆転にならない。 `HumanoidMotionApplier` の実装上、 同 frame 完結性は (R-A 調査結果) 確認済み。 将来 Applier 改修で dict 参照を frame またぎ保持するように変更されたら、 本 Spec の VmcMoCapSource を毎 Tick 新規 Dictionary 戦略に retreat させる必要がある (revalidation trigger)。
- **Follow-up**: design.md「Revalidation Triggers」 章に「HumanoidMotionApplier の dict 消費パターン変更」 を明示登録。

---

## Risks & Mitigations

- **Risk 1 (R-A)**: ダブルバッファ swap 順序の取違いで dict 破壊 → 単体テストで 「次 Tick で旧 buffer が Clear されること」 を assert (PlayMode test)。
- **Risk 2 (R-G)**: uOSC 将来 version で `StartServer` 動作変更 → uOSC version pin (`com.hidano.uosc@f7a52f0c524d`) を `package.json` `dependencies` で明示し、 アップデート時に R-G 再検証を必須にする。
- **Risk 3 (R-H)**: VMC 送信元実装が PascalCase 規約から逸脱 → R-3.3 で「未知 bone は黙って無視」 を要件化済み。 運用上 case 違いが頻発したら後続 Spec で case-insensitive 経路を検討。
- **Risk 4 (R-10 measurement)**: IL2CPP 環境での boxing 起因 alloc は uOSC layer 内で発生し制御不可 → R-10 計測スコープを 「uOSC `Message` 受領後 〜 OnNext 直前まで」 に厳密化、 uOSC 由来 alloc は別計測として記録する旨を design.md / validation で明記。
- **Risk 5 (Domain Reload OFF)**: `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` の static reset と factory 自己登録の順序 → 既存 `VMCMoCapSourceFactory` 同一パターンで踏襲 (現状動作実績あり)。

---

## References

- VMC プロトコル仕様: https://protocol.vmc.info/specification
- Unity Manual `HumanBodyBones`: https://docs.unity3d.com/ScriptReference/HumanBodyBones.html
- Unity `HumanPoseHandler`: https://docs.unity3d.com/ScriptReference/HumanPoseHandler.html
- Unity `[RuntimeInitializeOnLoadMethod]`: https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html
- Unity Domain Reload OFF: https://docs.unity3d.com/Manual/DomainReloading.html
- uOSC repository (本リポジトリ内 `Library/PackageCache/com.hidano.uosc@f7a52f0c524d/`)
- 既存実装参照:
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/EVMC4UMoCapSource.cs`
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/EVMC4USharedReceiver.cs`
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/VMCMoCapSourceFactory.cs`
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Applier/HumanoidMotionApplier.cs`
  - `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/Runtime/Motion/Cache/MotionCache.cs`
- 上位 Spec 不変条件:
  - `.kiro/specs/mocap-vmc/design.md`
  - `.kiro/specs/mocap-vmc-package-split/design.md`
- pivot 経緯:
  - `.kiro/specs/mocap-vmc-native/dig.md` (pre-pivot 検討記録)
  - `.kiro/specs/mocap-vmc-native/dig-native.md` (post-pivot 自律意思決定 9 件)
