# Dig Findings — mocap-vmc-native (post-pivot, requirements.md v2026-05-09)

> Phase 1〜2 (コンテキスト構築 + Assumption Mapping) 完了時点の深掘り結果。
> 本ドキュメントは pivot 後 (`mocap-vmc-reflection-loading` → `mocap-vmc-native`) の `requirements.md` (12 要件) を対象に、
> 「設計フェーズに進む前に明示的な意思決定が必要な未解決事項」を再評価する。
> 旧 `dig.md` は pivot 前の Reflection 化路線の検討記録として保存済み。
>
> Auto Mode 下では対話質問を AskUserQuestion で行わず、調査結果と推奨を本ドキュメントに集約する形式を踏襲する。
> design.md / research.md でこれらに対する決定を記録すること。

## Investigation Overview

- 入力: `mocap-vmc-native/requirements.md` (R1〜R12)、predecessor `mocap-vmc-package-split` の design.md / tasks.md / validation.md、現行 Runtime ソース (`EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `VMCMoCapSourceFactory.cs` / `VMCMoCapSourceConfig.cs`)、`Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/**` (uOSC 実装)、`Assets/EVMC4U/ExternalReceiver.cs` (bone マッピング既存実装)、`HumanoidMotionFrame.cs` (受信側契約)
- アプローチ: requirements.md の前提を「自前 OSC 受信 + uOSC 直接利用」という視点から逆向きに掘り、(1) uOSC 実装上のスレッド/アロケーション挙動、(2) bone 名 ↔ HumanBodyBones マッピングの実体、(3) `onDataReceived` UnityEvent の信頼性、(4) `HumanoidMotionFrame` の所有権契約と snapshot 戦略、(5) 移行クリーンアップの責任境界、を検証
- 結論: 設計フェーズで **明示的に意思決定が必要な論点が 9 件 (Critical 4 / High 3 / Med 2)**、**事実誤認または過剰宣言の可能性がある記述が 3 件**、**新たに発見されたリスク 4 件** 見つかった

---

## 🔴 Critical Findings (設計前に必ず解決)

### N-C1. R-10「0 byte/tick」目標は uOSC `Parser` 側の構造的アロケーションにより達成不可能

**事実 (コード調査済み)**:

`Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/Core/Parser.cs` の `ParseData` は受信メッセージごとに以下を **必ず** 確保する:

```csharp
object[] ParseData(byte[] buf, ref int pos) {
    var types = Reader.ParseString(buf, ref pos).Substring(1);  // string alloc (Substring)
    var n = types.Length;
    if (n == 0) return EmptyObjectArray;
    var data = new object[n];                                    // ← 配列 alloc
    for (int i = 0; i < n; ++i) {
        switch (types[i]) {
            case Identifier.Float : data[i] = Reader.ParseFloat(buf, ref pos); break;  // ← float boxing
            case Identifier.String: data[i] = Reader.ParseString(buf, ref pos); break; // ← string alloc
            ...
        }
    }
    return data;
}
```

さらに `Reader.ParseString` (要 `Reader.cs` 確認) は OSC string を都度新規 `string` として確保する。 Bone OSC メッセージあたりの **最低アロケーション**:

| 経路 | 確保サイズ目安 | 備考 |
|---|---|---|
| `new object[8]` | 8 ref slots = 32〜64 byte | 配列ヘッダ含む |
| `bone name string` (`"LeftLowerArm"` 等) | 24〜40 byte | OSC 受信ごとに new string |
| `float boxing × 7` | 各 24 byte = 168 byte | float (4byte 値型) → object 化 |
| `Substring` 結果 string | 16〜24 byte | type tag (`"fffffffs"` 等) |
| **計** | **~280 byte / Bone メッセージ** | |

VMC 標準送信レート (60Hz × 約 55 bone × 2 メッセージ root) ≒ 110 mes/frame。 `60 fps × 110 mes × 280 byte ≒ 1.85 MB/sec` の managed alloc が **uOSC layer 内で必ず発生する**。

**含意**:

- R-10.2「OSC ハンドラのホットパスにおいて `new` による Dictionary 生成 / List 生成 / string 連結 / Split / ToLower 等のアロケーションを行わない」は **我々の側のコードには一切該当しなくても、uOSC の Parser 経路で配列 + string + boxing 多数が必ず発生する**。
- R-10.5「Tick あたり target allocation を 0 byte として設計し、IL2CPP / Mono 双方で達成可能な範囲を design / validation で実測する」は、計測前提が「Tick = `HumanoidMotionFrame` 1 回発行」を指すのか「OSC 1 メッセージ受信処理 1 回」を指すのかが requirements 上で不明瞭。
- R-10.6「uOSC 側の API 利用方法を最適化する (生 byte buffer から直接読む等)」は **uOSC の現 API では達成不可能**: `onDataReceived` UnityEvent は `Message` 構造体 (中身 `object[] values`) を引数に取るのみで、 byte buffer は private (`Parser.Parse` 内 local) に閉じている。生 byte buffer 経路には uOSC fork / 再実装が必要。

**意思決定が必要な選択肢**:

- (a) **R-10 を「アプリケーション層 (我々のコード) が new するアロケーションを 0 とする」と明示的に再定義**: uOSC 由来の alloc は対象外と明記。`Profiler` で計測する区間を `EVMC4UMoCapSource.Tick()` 直前直後 (= MotionFrame 発行タイミング) に限定し、`onDataReceived` ハンドラ内の uOSC 由来 alloc は除外。
- (b) **uOSC を fork して生 byte buffer 経路を生やす**: スコープが本 Spec の「~210 行で済む」見積を超える。Out of Scope と表明されている動的アセンブリロード以上に重い拡張。明示的に却下するかが必要。
- (c) **uOSC を捨てて自前 UDP サーバを書く**: `System.Net.Sockets.UdpClient` を直接叩いて生 byte → bone 値の経路を組む。本 Spec の「外部依存は uOSC のみ」スタンスと真逆。
- (d) **R-10.2/10.5/10.6 を broadly 「best-effort target」に格下げ**: design で具体的な数値目標 (例: ホットパス内追加 alloc = 0) を再定義。

> 設計フェーズでは **(a) が現実解** と判断する。R-10.2 を「我々のハンドラ内で `new` を発行しない」、R-10.5/R-10.6 を「uOSC の structural alloc は accept、 application layer 追加 alloc を 0 とする」 とリフレーミングし、CI 計測スコープを明示する。`requirements.md` 修正を推奨。

---

### N-C2. R-1.5「OSC ハンドラが MainThread で起動される」は uOSC 実装で保証されているが、要件文の前提条件 (`While ... 前提下`) は誤り

**事実 (コード調査済み)**:

`Library/PackageCache/com.hidano.uosc@f7a52f0c524d/Runtime/uOscServer.cs`:

```csharp
void Update() {              // ← MainThread (Unity が自動呼出)
    UpdateReceive();
    UpdateChangePort();
}

void UpdateReceive() {
    while (parser_.messageCount > 0) {
        var message = parser_.Dequeue();
        onDataReceived.Invoke(message);  // ← 必ず MainThread から発火
    }
}
```

並行して、 `Runtime/Core/DotNet/Thread.cs` の `ThreadLoop()` が **ワーカースレッド** 上で `udp_.Receive()` + `parser_.Parse()` を実行する。Parser は内部で `lock (lockObject_)` + `Queue<Message>.Enqueue` を使ってスレッドセーフに enqueue する。 つまり **uOSC の設計上 `onDataReceived` は MainThread でのみ発火することが構造的に保証** されている (ユーザがコード破壊しない限り)。

**現 R-1.5 の問題**:

```
While VMC native receiver の OSC ハンドラが MainThread で起動されている前提下,
the VMC native receiver shall 受信処理を MainThread で完結させ、追加スレッドを生成しない
(`uOscServer.Update` 内 dequeue モデルを踏襲する)。
```

EARS 構文 `While X 前提下, Y shall Z` は「X が成り立つ間 Y は Z せよ」を意味する。 つまり「OSC ハンドラが MainThread で起動されている **間は** MainThread で完結させる」は、 X が成り立たない (=ワーカーから来た) 場合の挙動を **要件としては未規定** のまま残してしまう。

**含意**:

- 万一 uOSC 後継版で worker thread 通知に変更された場合、本要件は条件未充足として黙ってスキップされる読み方になる。
- R-B (research items) に「uOSC `onDataReceived` がメインスレッド以外で発火する余地が残っていないか実コードレベルで再検証」とあるが、 本 dig 段で **既に検証済み (uOSC 2.x 系列では構造的に保証)** であり、要件レベルで「MainThread 発火を前提とする」と肯定的に書けるはず。
- 逆に、もし将来 uOSC を別実装に差し替える可能性があるなら、本要件は「VMC native receiver は uOSC が MainThread で発火する前提に依存する。 他スレッド発火に対応する場合は本要件を改訂する」 と陽性形にすべき。

**意思決定**: requirements.md R-1.5 を `While ... 前提下` から `the VMC native receiver shall uOSC が MainThread で `onDataReceived` を発火することを前提として受信処理を MainThread で完結させ、追加スレッドを生成しない (uOSC 2.x 系列の `uOscServer.Update` 内 dequeue モデルに依存)` に書き換え、 R-B (uOSC スレッド再検証) を **本 dig で確定済み** として削除する。

---

### N-C3. R-3.1 の「VMC 標準 bone 名」は `HumanBodyBones` enum 名と完全一致するため、 EVMC4U の `Enum.TryParse` 実装より単純な静的辞書で十分

**事実 (コード調査済み)**:

EVMC4U の `ExternalReceiver.cs:1437-1463` `HumanBodyBonesTryParse` は受信した bone 名 string を **`Enum.TryParse<HumanBodyBones>(value, true)` (case-insensitive)** で初回解決し、 結果を `Dictionary<string, HumanBodyBones>` にキャッシュしている。 これは VMC プロトコルが送出する bone 名が **Unity の `HumanBodyBones` enum 名と一致する** という前提で動く。

R-3.1 で列挙されている bone 名 (`Hips` / `Spine` / ... / `RightLittleDistal`) は **すべて Unity の `HumanBodyBones` enum メンバ名と完全一致する**。これは VMC 公式仕様 (https://protocol.vmc.info/) の決定事項であり、 我々の実装側では `Dictionary<string, HumanBodyBones>` を起動時に静的初期化するだけで済む。

**現 R-3.1 / R-3.5 の暗黙の選択肢**:

- (1) **静的辞書 (`{ "Hips" => HumanBodyBones.Hips, ... }`) を全 enum メンバから自動生成**: `static class` の `cctor` で `Enum.GetValues(typeof(HumanBodyBones))` をループして辞書を構築。 起動時に1度だけ alloc、その後不変。 `LastBone` を除外する制御も自動。
- (2) **静的辞書を hand-coded で 55 エントリ列挙**: タイプミスのリスクあり。enum 名が将来追加されたら手動追従が必要。
- (3) **EVMC4U 同様 `Enum.TryParse` を初回 lazy 解決 + キャッシュ**: 不正 bone 名でメモリリークの恐れ (悪意ある送信元が無限の bone 名を送るとキャッシュが膨張)。

**含意**:

- 本要件 R-3.1 が単純な「全 enum 列挙でテーブル生成」 (option 1) で済むことを design で明示すべき。 EVMC4U の lazy キャッシュ実装を借用すると不要にメモリリーク懸念を持ち込む。
- R-D (bone 名マッピングの license / credit) は、 マッピングテーブル自体が「Unity の `HumanBodyBones` 全列挙」であれば EVMC4U 由来のオリジナリティは無く、 license / credit 議論は不要 (Unity 自体の API 名を機械的に転記しているに過ぎない)。 EVMC4U 由来の知見として残るのは「VMC 仕様が enum 名そのものを送出する」 という設計判断のみで、これは MIT credit には該当しない (アイデアは copyright で保護されない)。
- R-12.8 の research item「bone 名 mapping の参照元 license 互換性」は **明示的に「不要」と結論できる**。

**意思決定**: design.md で option (1) を採用。 R-3.5「マッピングテーブルを静的読み取り専用領域に保持」を `static readonly Dictionary<string, HumanBodyBones>` で `cctor` 内 `Enum.GetValues` ベースで自動初期化する経路に確定。 `LastBone` 除外は R-3.2 で既に明記済み。 R-D / R-12.8 第 2 項 を「不要 (Unity 公開 enum 名の機械的転記であり、EVMC4U 由来のオリジナリティを借用しない)」と確定。

---

### N-C4. R-5.5 と R-10.4 が要求する「snapshot コピー戦略」と `HumanoidMotionFrame` の所有権契約が不整合

**事実 (コード調査済み)**:

`HumanoidMotionFrame.cs` のコンストラクタコメント (line 100-103):
```
boneLocalRotations: 各ボーンの親ローカル回転辞書。null 可。
呼び出し元から所有権を移譲すること (Applier 側でコピーせず参照保持する)。
```

つまり `HumanoidMotionFrame.BoneLocalRotations` は **発行後に呼出元が変更してはならない** (受信側が参照保持してそのまま `HumanoidMotionApplier` が読む契約)。

一方:
- R-5.5: 「各 Tick で `BoneLocalRotations` を内部 Dictionary の **snapshot コピー** として渡し、参照を直接渡さない」 → snapshot コピーを毎 Tick で渡せ
- R-10.4: 「snapshot 用の Dictionary バッファを再利用 (フィールドに保持して `Clear()` + 再投入) する設計」 → 同じバッファを使い回して alloc を抑制せよ

両者を素朴に組合せると、**前 Tick で発行した `HumanoidMotionFrame.BoneLocalRotations` への参照が `Applier` 側に保持されたまま、新 Tick で `.Clear()` + 再投入される** ことになり、 受信側 (`Applier`) が読みかけの dict を破壊することになる。 これは `HumanoidMotionFrame` の immutability 契約違反であり、 `HumanoidMotionApplier` が期待する「所有権移譲済み = 不変」契約を壊す。

**現実装 (`EVMC4UMoCapSource.cs:254`) の挙動**:
```csharp
var snapshot = new Dictionary<HumanBodyBones, Quaternion>(view.Count);
foreach (var kv in view) { snapshot[kv.Key] = kv.Value; }
...
var frame = new HumanoidMotionFrame(timestamp, Array.Empty<float>(), rootPosition, rootRotation, snapshot);
```

**毎 Tick で `new Dictionary` している**。 これは Tick あたり「Dictionary オブジェクト + 内部 entries 配列」のアロケーションが入る。`Dictionary<HumanBodyBones, Quaternion>` の初期容量 55 エントリだと約 1.5KB のヒープ確保。 60Hz で 90 KB/sec、1分で 5.4 MB の Gen0 garbage を継続的に生む。

**意思決定が必要な選択肢**:

- (A) **現在の戦略を継続 (毎 Tick 新規 Dictionary)**: 安全だが alloc 0 不可能。R-10.5 の目標達成は不可能と確定。
- (B) **ダブルバッファリング**: 2 つの `Dictionary` をローテーションで交互に使う。Tick N で書き込んだ buffer を `HumanoidMotionFrame` に渡し、Tick N+1 で alternate buffer に書き込む。 Tick N+1 の発行までに Tick N の Applier 処理が完了している前提。 1 frame 内で完結する Pull モデルなら成立 (Unity の Update→LateUpdate→Animator は同 frame 内で順序保証)。 alloc は static fields で初期化時のみ。
- (C) **Immutable wrapper + 再利用 backing**: `IReadOnlyDictionary<HumanBodyBones, Quaternion>` を実装する custom struct で内部 Dictionary を共有 + 「frame 番号」で生死を管理。複雑。
- (D) **`ReadOnlyDictionary<TKey,TValue>` ラッパ + 再利用 backing**: 標準 BCL の `ReadOnlyDictionary` を `new` で wrap (これは alloc) しても、 backing dict は共有できる。 だが (B) と同じく backing 側を mutate する瞬間 wrap 経由の reader が壊れる。
- (E) **`HumanoidMotionFrame` の所有権契約を再解釈**: 「Applier は同フレーム内で読み終わる前提」を明示し、 frame 経過後の dict 再利用を許可する。 design でこの契約緩和を明記する必要あり。

> design では **(B) ダブルバッファ + (E) 契約緩和** の組合せを推奨する。 `HumanoidMotionFrame` 側の immutability 契約を「同 frame 内で消費完了する前提で参照渡しを許容」と再定義し、`HumanoidMotionApplier` の同 frame 完結性を確認することが前提条件となる。
>
> 確認すべき項目:
> 1. `HumanoidMotionApplier` は `LateUpdate` で動作するか (= VMC `Tick()` の同 frame 内完結性)。
> 2. `MotionStream` を別の購読者 (例: `MotionCache` 等) が **frame をまたいで** 保持している経路がないか。 もし frame をまたいで dict 参照保持される経路があるなら、 (B)+(E) は不可で、(A) フルコピーに retreat 必須。
>
> R-10.4 の文言は「再利用」を要求するが R-5.5 の「snapshot コピー」と素朴には両立しない。 design でこの両立可能性を **`HumanoidMotionApplier` の frame 完結性と `MotionCache` の保持パターンを実コードで検証した上で** 確定すること。

---

## 🟡 High-Severity Findings (確認推奨)

### N-H1. R-7.6 の README 改訂と R-12.1 のドキュメント改訂は重複しているが scope が違う

**事実**:

- R-7.6: 「README からも EVMC4U の `.unitypackage` インポート手順 / `EVMC4U.asmdef` 作成手順 / `evmc4u.patch` 適用手順を削除する」 (Requirement 7 内、依存撤廃の文脈で言及)
- R-12.1: 「パッケージ README を全面改訂し、利用者準備手順を「uOSC を導入する。これだけ。」に縮約する。EVMC4U / `evmc4u.patch` への参照は credit / 歴史的経緯セクション以外から削除する」 (Requirement 12 内、ドキュメント改訂の文脈で言及)

R-12.1 が「全面改訂」と書いているのに対し R-7.6 は単なる「該当手順削除」。 同じ README ファイルへの編集要求が 2 箇所に分かれていて、 tasks.md 分解時に重複タスクが生まれる/ task 抜けが起きる可能性。

**意思決定**: requirements.md で R-7.6 を「README 改訂は R-12.1 に集約する。 R-7 は依存撤廃事実の宣言のみ」 とリファクタする。

### N-H2. R-9.2 「`EVMC4USharedReceiverTests.cs` を新クラス名対応のテストにリネームし...新実装に対して検証する内容へ書き換える」は破壊変更

**事実**:

`EVMC4USharedReceiverTests.cs` は EVMC4U の `ExternalReceiver` に依存して以下を検証している:
- `EnsureInstance` が共有 GameObject を作る
- `Release` で refCount が減る
- `SubsystemRegistration` で static がリセットされる

新実装 (`VmcSharedReceiver` 等) では `ExternalReceiver` が消えて `uOscServer` 直接購読モデルになる。 つまり「リネーム + 書き換え」と書いてあるが、 **書き換え後のテストは別アサーションを持つ別物のテスト** であり、リネームでは表現しきれない。

CHANGELOG の意味 (`mocap-vmc-package-split` の test 履歴を継承するか) も不明瞭。

**意思決定**: requirements.md R-9.2 を「`EVMC4USharedReceiverTests.cs` を **削除し**、 新規 `VmcSharedReceiverTests.cs` (refCount / DontDestroyOnLoad / `SubsystemRegistration` リセット / 重複 `Acquire` の挙動を新実装に対して検証) を新規追加する」 と書き換える。 git rename の history-tracking を期待しないことを明示。

### N-H3. R-11.4 の SocketException 伝播経路と現実装の差異

**事実 (コード調査済み)**:

現 `EVMC4USharedReceiver.ApplyReceiverSettings(int port)` (line 164):
```csharp
public void ApplyReceiverSettings(int port) {
    if (_server == null) return;
    _server.StopServer();
    _server.port = port;
    _server.StartServer();   // ← SocketException が伝播
}
```

`uOscServer.StartServer` 内部で `udp_.StartServer(port)` を呼ぶが、 これは worker thread 起動前なので例外は **MainThread (= Initialize 呼出元 thread) に直接伝播する**。

R-11.4「`uOscServer.StartServer()` がポートバインドに失敗 (ポート使用中・権限不足等) したとき、 the VMC native MoCap source shall `Initialize()` から例外を呼び出し元へスローし、`SlotManager` が `SlotErrorCategory.InitFailure` として `ISlotErrorChannel` へ通知する経路に乗せる」

問題: 実は `udp_.StartServer` がワーカー thread 起動 **後** にバインドする実装の可能性も残る (uOSC のデザインによる)。 design で `Library/PackageCache/com.hidano.uosc@*/Runtime/Core/DotNet/Udp.cs` を確認して 「同期バインド = MainThread 例外伝播」 か 「非同期バインド = MainThread に伝播しない」 かを確定すべき。 後者なら R-11.4 の経路は破綻する (`Initialize` は無事に return し、 後で worker thread の `Debug.LogError` で初めて気づく)。

**意思決定**: design でうない の Udp.cs を読み、`udp_.StartServer` の同期/非同期挙動を確定。 非同期だった場合は R-11.4 を「`uOscServer.onServerStarted` / 別 callback を購読して bind 失敗を検出し ErrorChannel へ通知する」 経路に再設計する。

---

## 🟢 Medium-Severity Findings (要記録)

### N-M1. R-12.6「`Assets/EVMC4U/` を削除」の前提条件: ローカル `git` 操作と user authorization

**事実**:

- 親セッションでは `rm` 可能 (CLAUDE.md 子 Agent 削除制約は子に限定)。 git で tracked なら `git rm -rf RealtimeAvatarController/Assets/EVMC4U/` 経路。
- 但し 「(子 Agent は rm 不可) であることを tasks.md で明示する」とR-12.6 自体に書かれている → 親セッション task が必要 (kiro:spec-run の Phase 分解では親で扱う)。
- 「historical reference として残す必要があれば `.kiro/specs/mocap-vmc/handover-*.md` 等に移動する」 → 移動先の specifics が design.md / tasks.md で確定していない。
- `Assets/EVMC4U/3rdpartylicenses(ExternalReceiverPack).txt` は uOSC / UniVRM の credit が含まれており、削除する場合は credit を `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/THIRD_PARTY_NOTICES.md` 等に転記する必要 (MIT 義務)。

**意思決定**: design で「削除 vs 移動 vs 残置」を最終確定。 推奨は:
1. `Assets/EVMC4U/` 全体を削除 (主要動機: EVMC4U 完全撤廃)
2. `3rdpartylicenses(ExternalReceiverPack).txt` の uOSC credit のみ転記
3. EVMC4U 自体の MIT credit は (我々がコードを使わなくなるなら) 不要だが、「VMC プロトコル仕様の理解を借用」は credit 残置 (R-7.7 の inspiration credit)

### N-M2. R-12.5「`evmc4u.patch` の取り扱い」は本 Spec 内で完結すべきだが手段が曖昧

**事実**:

- `.kiro/specs/mocap-vmc/evmc4u.patch` は predecessor `mocap-vmc` Spec の deliverable。 本 Spec はその後継 (`mocap-vmc-package-split` → `mocap-vmc-native`) であり、 patch artifact は EVMC4U 撤廃と共に意義を喪失する。
- 「冒頭に **OBSOLETE** マーカと obsolete 化日時 を記載した上で履歴用に残す」 vs 「削除」 の判断軸が requirements に無い。

**意思決定**: design で **削除** を推奨 (本 Spec 完了時点で当該 patch は完全に死ぬため)。 履歴は git log で追える。 `.kiro/specs/mocap-vmc/handover-7.2.md` にも patch 関連の記述があり、 そちらの整理も同時に行う必要 (handover の OBSOLETE マーキング)。

---

## ⚠️ 事実誤認・過剰宣言の可能性 (要件文書の修正候補)

### N-F1. R-5.7「`Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で打刻し、VMC 送信側のタイムスタンプは使用しない」 — 「使用しない」が過剰宣言

**事実**:

VMC プロトコル基本部 (`/VMC/Ext/Bone/Pos`) **には timestamp 引数が存在しない** (8 引数すべて bone name + pos + rot)。 つまり「VMC 送信側のタイムスタンプ」という入力ソース自体が無く、 そもそも 「使用しない」 の対比対象が不在。

OSC bundle 階層には `Timestamp` が含まれるが、 VMC Sender 実装側は `0x1` (= immediate) を使うのが慣例。

**含意**: R-5.7 は「Stopwatch ベース打刻を採用する」 とのみ書けば充分で、 「VMC 送信側のタイムスタンプは使用しない」 は書かれている対象が誤認 (送信側 timestamp は OSC bundle 層であって VMC アプリ層ではない)。

**修正提案**: R-5.7 を 「The VMC native MoCap source shall `HumanoidMotionFrame.Timestamp` を `Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency` で受信時点で打刻する。 OSC bundle 層の timestamp は使用しない。」 に書き換え。

### N-F2. R-12.8 第 2 項「bone 名 mapping の参照元 license 互換性」は不要 (N-C3 と連動)

→ N-C3 で示した通り、 マッピングは `Enum.GetValues(typeof(HumanBodyBones))` の機械的列挙であり、 EVMC4U 由来の借用は無い。

**修正提案**: R-12.8 第 2 項を削除し、 `R-D` (research items) も削除。 代わりに「VMC 仕様準拠」「Unity HumanBodyBones 列挙の機械的転記」を design で明記。

### N-F3. R-3.4「文字列の `Split` / `ToLower` 等の追加文字列操作を行わない」は念のため記述だが、 EVMC4U 既存実装 (`Enum.TryParse(... ignoreCase: true)`) との対比

**事実**:

EVMC4U 実装は `Enum.TryParse<HumanBodyBones>(value, ignoreCase: true, out result)` を使っており、 これは **内部で ToUpper / ToLower 相当の case-folding を行う**。 すなわち EVMC4U は受信した bone 名の case 違い (`hips` / `Hips` / `HIPS`) をすべて `HumanBodyBones.Hips` に解決していた。

我々の実装が「VMC 仕様で送出される bone 名は `HumanBodyBones` enum 名と完全一致 (PascalCase)」 と前提するなら、 case-insensitive である必要は無い (送信側が PascalCase で送る前提)。 但し、これは **仕様準拠ベンダ間で互換性を持たせる前提** に依存する。

**含意**:

- R-3.4 で「ToLower 等を行わない」 = case-sensitive matching を選択している。 もし VMC 送信側の一部実装 (バグ持ち) が camelCase / lowercase で送出するなら未知 bone 扱いで黙って捨てられる。
- VMC 公式仕様サイト (https://protocol.vmc.info/) で「bone name は PascalCase」と明記されているか design で確認すべき。 されていなければ EVMC4U 互換性のため case-insensitive に倒す案も検討。

**修正提案**: R-3.4 のままでよいが、 design で「VMC 公式仕様による bone 名 PascalCase 規定の確認」を research item に追加する。 確認できなければ case-insensitive 経路を予備として残す。

---

## 🔵 New Risks (要件未カバー)

### N-R1. `Assets/EVMC4U/` 削除タイミングと `Library/ScriptAssemblies/EVMC4U.dll` キャッシュの不整合

`Assets/EVMC4U/` を削除しても Unity が `Library/ScriptAssemblies/EVMC4U.dll` をキャッシュとして残す可能性。 Unity 再起動または `Library/` 削除で解消するが、 削除タスクの DoD に「Unity Editor 再起動後の Console に `EVMC4U` 関連 warning なし」を含めないと、 開発者環境での動作確認が不十分なまま完了扱いになる。

→ tasks.md の検証項目に追加。

### N-R2. `[MovedFrom]` 属性の使用判断 (R-C / R-12.8 第 1 項)

`UnityEngine.Scripting.APIUpdating.MovedFromAttribute` は `[Serializable]` クラスや `MonoBehaviour` の SerializeReference 互換性のための機構。 VMC 関連の旧クラス (`EVMC4UMoCapSource` / `EVMC4USharedReceiver`) は **`MonoBehaviour` ではなく Plain C#** であり、 SerializeReference でも参照されていない (factory 経由で動的生成、 SO アセット参照は config だけ)。 **つまり `[MovedFrom]` は不要**。

但し `EVMC4USharedReceiver` のみ MonoBehaviour なので、 シーン上に Inspector からドラッグされている可能性がある場合は要検討。 → 現実装は `DontDestroyOnLoad` 経由で生成のみ、シーンに配置されない設計のため不要 と確定可能。

→ design で「`[MovedFrom]` 不要 (rationale: MonoBehaviour シーン参照無し / SerializeReference 無し)」 と明記。 R-12.8 第 1 項を「不要」と確定。

### N-R3. `ScriptAssemblies` の RealtimeAvatarController.MoCap.VMC.dll が `EVMC4U.dll` への参照を保持している期間

asmdef references から `EVMC4U` を外したタイミングで再 compile が走る。 失敗するパターン:
- `Assets/EVMC4U/` (= `Assembly-CSharp` または `EVMC4U.asmdef` 経由) のままでは、 旧 `EVMC4UMoCapSource.cs` が `using EVMC4U;` を残しているとビルドエラー。

→ tasks.md で「順序: (1) Runtime asmdef references から EVMC4U を外す と (2) Runtime ソース から `using EVMC4U;` 削除 を **同一コミットで実施**」 を明示。

### N-R4. VMC 送信側互換性テストの欠落

要件には「VSeeFace / VMagicMirror / VirtualMotionCapture 等の標準的な VMC 送信アプリ」 (R-2 Objective) と謳われているが、 実機で送られるパケットを使った互換性テストは新規 EditMode 単体テスト (R-9.5) ではカバーできない (実装本体の単体検証は OK だが「実 Sender との互換」は別)。

`SampleSceneSmokeTests.cs` (PlayMode) を更新する案が R-9.4 にあるが、 これは「実 OSC を流して動くか」 は確認されない (シーン読み込み smoke のみ)。

→ design or validation で「実 Sender (VirtualMotionCapture 等) からの実通信検証」 を validation シナリオ A/B に組み込むか、 「ローカルで `uOscClient` から既知 OSC packet を送出して検証」 のテストを `SampleSceneSmokeTests.cs` 拡張で追加する。

---

## Decisions Required Before Design Phase

| # | Topic | Options | Recommendation | Risk if deferred |
|---|-------|---------|----------------|------------------|
| N-C1 | R-10「0 byte/tick」目標の対象範囲 | (a)〜(d) | (a): アプリ層内追加 alloc を 0 と再定義、 uOSC structural alloc は accept | High: validation で要件未達確定 |
| N-C2 | R-1.5 EARS 構文 | While→ストレート shall に書換 | uOSC MainThread 発火を前提条件として陽性形に | Med: 仕様読解の混乱 |
| N-C3 | bone マッピング実装方式 | 静的辞書 / lazy / EVMC4U 借用 | `Enum.GetValues` ベース静的辞書 | Med: メモリリーク余地 |
| N-C4 | snapshot Dictionary 戦略 (R-A) | (A)〜(E) | (B) ダブルバッファ + (E) 契約緩和 | High: alloc 0 不可 / または契約破壊 |
| N-H1 | README 改訂の責務分割 | 統合 / 分割継続 | R-12.1 に統合、R-7.6 削除 | Low: タスク重複 |
| N-H2 | `EVMC4USharedReceiverTests.cs` の扱い | リネーム / 削除+新規 | 削除 + 新規追加 | Low: history 期待ズレ |
| N-H3 | R-11.4 SocketException 経路 | 同期 / 非同期 | uOSC `Udp.cs` 確認後確定 | Med: bind 失敗が黙殺される可能性 |
| N-M1 | `Assets/EVMC4U/` 削除と credit 転記 | 全削除 / 移動 / 部分削除 | 全削除 + uOSC credit を `THIRD_PARTY_NOTICES.md` 転記 | Med: license 義務違反 |
| N-M2 | `evmc4u.patch` 取扱い | 削除 / OBSOLETE マーキング | 削除 (history は git log で追える) | Low: artifact 陳腐化 |

---

## Recommended Next Steps

1. **requirements.md を是正する** (本 dig 直後に実施):
   - R-1.5 を `While ... 前提下` から「uOSC が MainThread 発火する前提に依存する」 に書き換え
   - R-5.7 から「VMC 送信側のタイムスタンプは使用しない」 を削除し「OSC bundle 層 timestamp は使用しない」 に修正
   - R-7.6 を削除 (R-12.1 に統合)
   - R-9.2 を「リネーム」 から「削除 + 新規追加」 に書換
   - R-10.2/10.5 を「アプリ層内追加 alloc を 0 と定義し uOSC structural alloc は対象外」 と明文化
   - 設計フェーズ research items の R-B (uOSC スレッド再検証) と R-D (bone mapping license) を「本 dig で確定済み」 として削除

2. **research.md を新規作成して以下を記録**:
   - N-C1: uOSC `Parser.ParseData` の structural alloc 数値見積 (本 dig 内記述を転記)
   - N-C2: uOSC `uOscServer.UpdateReceive` → `onDataReceived.Invoke` の MainThread 発火保証 (`uOscServer.cs:81-97` 参照)
   - N-C3: VMC 標準 bone 名 = `HumanBodyBones` enum 名 完全一致 (Unity Manual / VMC 公式仕様)
   - N-C4: `HumanoidMotionApplier` の frame 完結性確認 (実コード読解結果)
   - N-H3: uOSC `DotNet/Udp.StartServer` の同期/非同期挙動

3. **design.md で必ず明文化**:
   - OSC ハンドラ実装 (`/VMC/Ext/Bone/Pos` / `/VMC/Ext/Root/Pos` の dispatch)
   - bone 名 ↔ `HumanBodyBones` の `Enum.GetValues` ベース静的辞書実装
   - ダブルバッファリング戦略 (受信書込側 / Tick 読み出し側) と `HumanoidMotionFrame` 所有権契約の同 frame 完結性前提
   - `Assets/EVMC4U/` 削除手順と credit 転記先 (`THIRD_PARTY_NOTICES.md` 等)
   - 検証シナリオに「実 OSC packet 送出での互換性確認」 を追加
   - `[MovedFrom]` 不要の rationale (MonoBehaviour シーン参照無し / SerializeReference 無し)

4. **tasks.md に分解時の注意**:
   - **タスク 0** (順序最優先): asmdef references から `EVMC4U` 削除 と Runtime ソースから `using EVMC4U;` 削除を **同一コミット** で行う (N-R3)
   - 親セッション専用 task: `Assets/EVMC4U/` 削除 + credit 転記 (子 Agent では rm 不可)
   - 親セッション専用 task: `.kiro/specs/mocap-vmc/evmc4u.patch` 削除 + handover-7.2.md OBSOLETE マーキング
   - 検証 DoD に「Unity Editor 再起動後の Console warning なし」 を含む (N-R1)

---

## Investigation Summary

- Rounds completed: 1 (Phase 1〜2 ベース、 Auto Mode のため Phase 3 対話質問は本ドキュメントに集約)
- Assumptions challenged: 13 (Critical 4, High 3, Medium 2, New Risks 4)
- Decisions surfaced as required: 9 (N-C1〜N-M2)
- 事実誤認候補: 3 (N-F1, N-F2, N-F3)

### Key Discoveries (impact 順)

1. **uOSC `Parser.ParseData` の structural alloc により R-10「0 byte/tick」 は uOSC 改修なしには達成不可能** (N-C1): 受信メッセージあたり ~280 byte の managed alloc が確定。 R-10 を「アプリ層追加 alloc を 0」 にリフレーミング必要。
2. **`HumanoidMotionFrame.BoneLocalRotations` の所有権契約と R-10.4 buffer 再利用が衝突** (N-C4): 毎 Tick `new Dictionary` が現実装、 ダブルバッファ + 同 frame 完結性契約緩和が design で必要。
3. **uOSC は `onDataReceived` MainThread 発火を構造的に保証** (N-C2): R-1.5 EARS 構文の前提条件は不要。 R-B research item は本 dig で確定済み。
4. **bone 名マッピングは `Enum.GetValues` ベース静的辞書で十分** (N-C3): EVMC4U 借用不要、license / credit 議論も不要 (R-D / R-12.8 第 2 項 削除可)。
5. **`SocketException` 伝播経路 (R-11.4) は uOSC Udp 実装の同期/非同期次第** (N-H3): design で確認必要。

### Remaining Risks (acknowledged)

- N-R1: `Library/ScriptAssemblies/EVMC4U.dll` キャッシュの再起動依存。 検証 DoD に Unity 再起動を含めることで緩和。
- N-R4: 実 Sender (VirtualMotionCapture 等) との互換性は単体テストで担保困難。 design で `uOscClient` を使った in-process 実 OSC test を組込推奨。
- IL2CPP 環境での Boxing 起因 alloc は uOSC 既知問題として持ち越し (uOSC fork は本 Spec の Out of Scope)。
