# Dig Findings — mocap-vmc-reflection-loading (superseded → mocap-vmc-native)

> **⚠️ STATUS: 本 dig は spec rename 前の `mocap-vmc-reflection-loading` を対象に行われた検討記録。**
> dig 結果を踏まえ、Reflection 化路線では patch 工程を消せないこと・EVMC4U 機能の実利用率が低いことが判明し、
> spec は **`mocap-vmc-native` (EVMC4U 依存撤廃 + 自前 VMC 受信実装)** へ pivot した (2026-05-09)。
> 本ドキュメントは pivot に至った経緯の判断記録として保存する。要件は requirements.md (新規) を参照。
>
> Phase 1〜2 (コンテキスト構築 + Assumption Mapping) 完了時点の深掘り結果。
> 本ドキュメントは旧 requirements.md に対し「設計フェーズに進む前に明示的な意思決定が必要な未解決事項」を列挙していた。

## Pivot Decision (2026-05-09 追記)

| 当初 dig が提示した選択肢 | 議論経過 | 最終決定 |
|---|---|---|
| C-1 EVMC4U local patch との関係 | (e) → (b) → (b-α) → 案 X 採用 | **本 spec を `mocap-vmc-native` へ rename し、EVMC4U 依存を完全撤廃。VMC OSC 受信を自前実装する** |
| C-2 アセンブリ解決戦略 | — | **不要化** (EVMC4U Reflection 解決自体が消滅) |
| C-3 Tests asmdef 戦略 | (D) Tests asmdef のみ EVMC4U/uOSC 参照保持 | **再考必要** (新 spec で全テスト書換予定。Tests asmdef は uOSC のみ参照) |
| C-4 性能目標 (0 byte/tick) | ホットパスのみ CreateDelegate / 初期化は Invoke | **不要化** (Reflection そのものが不要)。性能要件は新 spec で「直接型参照」を前提に再設定 |
| C-5 `Receiver` プロパティ | internal + InternalsVisibleTo | **不要化** (`EVMC4USharedReceiver` 自体が消える可能性) |

### Pivot の根拠 (要約)

1. **dig C-1 の発見**: 現コードは `Assets/EVMC4U/ExternalReceiver.cs` への 4 つの local patch (`evmc4u.patch` artifact) に依存しており、Reflection では patch #2 (`LatestRootLocal*` 新設) と patch #3 (`Model==null` 早期 return 解除) を逃せない。「asmdef 自作不要化」だけ実現しても利用者の手作業は半減止まり。
2. **EVMC4U 実利用率分析**: `EVMC4UMoCapSource.Tick` が EVMC4U に求めているのは Bone Rotation 辞書と Root 値の 2 つのみ。EVMC4U が提供する VRM blendshape / Camera / DirectionalLight / Communication Validator / ExternalReceiverManager 等の機能は一切使っていない。**実利用率は 5〜10% 以下**。
3. **自作見積**: VMC OSC 受信 (`/VMC/Ext/Bone/Pos` + `/VMC/Ext/Root/Pos` パース + bone name → HumanBodyBones マッピング) は ~210 行で自作可能。
4. **保守性**: 自作すれば EVMC4U upgrade による patch コンフリクト / private 名変更による Reflection 破綻のリスクがゼロ化。外部依存は uOSC のみとなる。

### 引き継ぐべき設計上の知見 (新 spec で再利用)

- VMC プロトコルの Bone OSC 形式: `/VMC/Ext/Bone/Pos` メッセージは `[string boneName, float pos.x, pos.y, pos.z, float rot.x, rot.y, rot.z, rot.w]` の 8 引数 (EVMC4U `ExternalReceiver.cs` 参考)
- Root OSC 形式: `/VMC/Ext/Root/Pos` メッセージも同 8 引数 (拡張プロトコル v2.1 では 14 引数だが本 spec では基本部のみ対応で十分)
- bone name → `UnityEngine.HumanBodyBones` マッピング: VMC 仕様では Unity の `HumanBodyBones` 名 (`Hips` / `Spine` / `Chest` / ... 計 ~55 値) を string で送出する想定 (要 EVMC4U の bone マッピング実装の確認)
- 既存 `EVMC4USharedReceiver.cs` の refCount / DontDestroyOnLoad / SubsystemRegistration リセット / `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 自己登録パターンは新実装でも踏襲する
- 既存 SO アセット `VMCMoCapSourceConfig_Shared.asset` (GUID `5c4569b4a17944fba4667acebe26c25f`) は `port` フィールドを保持。新実装でもこの GUID を維持する判断はあり得る

### 旧 requirements.md は破棄

旧 `requirements.md` の Reflection 化要件 (R-1〜R-11) は新 spec では使用しない。新 `requirements.md` は project description のみのスタブに置換済み。`/kiro:spec-requirements mocap-vmc-native` で再生成する。

---

## (以下は pivot 前 (rename 前) の dig 検討内容。判断記録として保存)

> Phase 1〜2 (コンテキスト構築 + Assumption Mapping) 完了時点の深掘り結果。
> 本ドキュメントは requirements.md に対し「設計フェーズに進む前に明示的な意思決定が必要な未解決事項」を列挙する。
> design.md / research.md でこれらに対する決定を記録すること。

## Investigation Overview

- 入力: requirements.md (10 要件)、predecessor `mocap-vmc-package-split` の design.md / research.md、現行 Runtime / Tests / asmdef / README ソース
- アプローチ: requirements.md に書かれた前提を「Reflection 化」という視点から逆向きに掘り、`Type.GetType` 解決経路・既存ローカルパッチ・テスト書き換え方針・性能目標の整合性を検証
- 結論: 設計フェーズで **明示的に意思決定が必要な論点が 11 件**、**事実誤認の可能性がある記述が 2 件** 見つかった

---

## 🔴 Critical Findings (設計前に必ず解決)

### C-1. EVMC4U upstream には `GetBoneRotationsView()` / `LatestRootLocalPosition` / `LatestRootLocalRotation` / `InjectBoneRotationForTest` / `IsShutdown` が存在しない

**事実 (コード調査済み)**:

- 現行 `EVMC4UMoCapSource.Tick` は `receiver.GetBoneRotationsView()`、`receiver.LatestRootLocalPosition`、`receiver.LatestRootLocalRotation` を呼ぶ。
- `Tests/EditMode/ExternalReceiverPatchTests.cs` のヘッダコメントに「ExternalReceiver (Assets/EVMC4U/ExternalReceiver.cs) への **local patch 検証用** EditMode テスト」と明記されている。
- すなわち **これらのメンバは upstream EVMC4U の素のコードには無く、利用者が `Assets/EVMC4U/ExternalReceiver.cs` を手で改変している前提** で本パッケージは動いている。
- predecessor spec の README にも「EVMC4U の公式 unitypackage には asmdef が含まれていません」とは書いてあるが、**「ExternalReceiver.cs を本プロジェクト用に patch する手順」が記述されていない**。これは実装者だけが暗黙に共有している作業で、利用者目線では完全な未開示工程である。

**Reflection 化への含意**:

- R-2.2 で Reflection 解決対象に挙げられた `GetBoneRotationsView()` / `LatestRootLocalPosition` / `LatestRootLocalRotation` は **利用者の手元の patch がなければ存在しない**。
- すなわち、いくら R-7.1/7.2 で「`EVMC4U.asmdef` を作る作業が不要になる」と謳っても、**利用者は ExternalReceiver.cs を patch する作業からは逃れられない**。「セットアップ手順簡素化」という Spec 全体の動機がほぼ成立しない可能性がある。
- もし「patch 不要にする」なら、`GetBoneRotationsView` 等を呼ばず、upstream にも存在する経路 (`ExternalReceiver` 内部の `private` Dictionary) へ Reflection でアクセスする必要があり、これは **R-2.2 のメンバ表自体を見直す** スコープになる。

**意思決定が必要な選択肢**:

- (a) **patch 同梱方式**: `Assets/EVMC4U/ExternalReceiver.patch` 等の patch ファイルを Samples~ に同梱し、利用者にあてさせる。R-7 のセットアップ手順簡素化目的とは相容れない。
- (b) **upstream 互換 Reflection 経路**: 内部 `private Dictionary<HumanBodyBones, Quaternion>` を `BindingFlags.NonPublic | Instance` でリフレクション読込し、自前で Snapshot する。性能要件 R-9 と合わせて設計負荷大、かつ upstream が field 名を変えると壊れる。
- (c) **EVMC4U fork を本リポジトリ内に同梱**: predecessor の boundary 外活動 (option ④) を引き戻す案。スコープ外宣言を覆す必要がある。
- (d) **patched ExternalReceiver.cs を `RealtimeAvatarController.MoCap.VMC` パッケージ Runtime 内に同梱**: license 確認が前提 (EVMC4U は GPLv3 のコンポーネントを含むため再配布制約あり)。
- (e) **本 Spec のスコープを「asmdef references 削除」のみに縮小**: ExternalReceiver の patch 手順は引き続き利用者責務として README に記載する。R-7 の主張トーンを「asmdef 作成不要」に限定し、「patch も含めた完全セットアップ免除」とは謳わない。
- (f) **upstream EVMC4U の API 変更要請 (option ④ 復活)**: 上流 PR を出す活動を本 Spec の前提に組み込む。

> design フェーズで上記から **明示選択** すること。`(e)` が現状最も整合的だが、その場合 README / requirements.md の文言の修正が必要 (現 R-7.1/7.2 は patch 工程を完全に隠蔽している)。

---

### C-2. `Packages/` 配下のソースから `Assembly-CSharp` の型を `Type.GetType` で解決できるか

**事実 (Unity 仕様)**:

- 利用者が `Assets/EVMC4U/EVMC4U.asmdef` を **作らない** 場合、EVMC4U 関連 .cs はすべて `Assembly-CSharp.dll` にコンパイルされる。
- `Type.GetType("EVMC4U.ExternalReceiver, Assembly-CSharp")` は AppDomain 内に `Assembly-CSharp` がロード済みなら Runtime に解決可能。
- ただし **Editor 起動直後の `[InitializeOnLoadMethod]` 段では `Assembly-CSharp` が未ロードの場合がある** (Editor が user assemblies をロードする順序の保証が薄い)。
- IL2CPP build では `Assembly-CSharp` という名前の DLL は `[Preserve]` 等で残されないと strip される可能性がある。

**含意**:

- R-2.1 / R-3.1 は `Type.GetType` 単発、または「全 AppDomain アセンブリ走査」を許容しているが、**走査タイミングが Initialize 呼出時ではなく Factory 自己登録時 (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`) だったら、`Assembly-CSharp` ロード前に走査しても見つからない可能性がある**。
- R-2.5 で「Domain Reload 時のみキャッシュクリア」と書いてあるが、**初回解決失敗のキャッシュ (R-4.3) を持ち越すと、Initialize 時には実際にはロード済みなのに「未導入」と誤判定する** ことがある。
- 解決タイミング (Factory 登録時 vs Initialize 呼出時) と、解決失敗キャッシュの寿命は **明示的に切り分け** が必要。

**意思決定が必要な点**:

- 解決タイミング: 「Initialize 呼出時にだけ Reflection 解決を行う (lazy)」で固定するか、Factory 登録段で eager に解決するか。
- アセンブリ走査範囲: `Type.GetType("FQN")` (現在の AppDomain 全走査) にするか、`AppDomain.CurrentDomain.GetAssemblies()` を全走査するか、それとも候補アセンブリ名の whitelist (`EVMC4U`, `Assembly-CSharp`, `Assembly-CSharp-firstpass`) を順に試すか。
- 失敗キャッシュ寿命: 「同一 Domain 内では諦める」か、Initialize 呼出毎に再試行するか (per-Slot 起動毎なので頻度は低い)。

---

### C-3. 既存テスト全件 (`using EVMC4U;` 直依存) の書換コストが想定されていない

**事実 (コード調査済み)**:

- EditMode テスト: `EVMC4USharedReceiverTests.cs` / `EVMC4UMoCapSourceTests.cs` / `ExternalReceiverPatchTests.cs` / `VmcConfigCastTests.cs` / `VmcFactoryRegistrationTests.cs` (5 件)
- PlayMode テスト: `EVMC4UMoCapSourceIntegrationTests.cs` / `EVMC4UMoCapSourceSharingTests.cs` / `SampleSceneSmokeTests.cs` (3 件)
- このうち `ExternalReceiverPatchTests.cs` は **完全に EVMC4U 型に張り付いた検証**。`InjectBoneRotationForTest`、`GetBoneRotationsView`、`LatestRootLocalPosition` 等のローカルパッチ専用 API を直接アサートしている。
- PlayMode 統合テストも `shared.Receiver.InjectBoneRotationForTest(...)` で受信 emulation を行っている。
- Tests asmdef の `references` 配列に `"EVMC4U"` / `"uOSC.Runtime"` が含まれている。

**現 Spec の扱い**:

- R-1.5: 「Tests asmdef についても整理し、参照削除または Reflection 経路への置き換えを行う (テスト書換の最終方針は **design フェーズで確定する**)」
- 「design フェーズで確定する」と書かれているが、これは **要件レベルで方針が無い** ことを意味する。実装フェーズに入ってから書換コストが噴出するリスクが高い。

**意思決定が必要な選択肢 (A〜D は排他、E は補助)**:

- (A) **Tests asmdef は EVMC4U/uOSC を保持**: 検証シナリオ B' (両ライブラリ導入時) でしかテストを走らせないと割り切る。Tests と Runtime の Reflection 化を非対称にする。R-1.5 の「整理」を「Tests は対象外」と再定義する。
- (B) **テストを完全 Reflection 化**: 全 8 ファイルを `dynamic` または `MethodInfo.Invoke` ベースに書き換える。既存赤テスト (現状 `ExternalReceiverPatchTests` が成立しないと検証 B' が落ちる) の書換量が膨大。
- (C) **テストヘルパ層を新設**: 内部 `EVMC4UTestSupport` 等の Reflection ラッパ (テスト用) を Runtime asmdef とは別に作り、テストはそれ経由で書く。書換量は中、可読性は中。
- (D) **テストのみ EVMC4U asmdef 任意ロード**: Tests asmdef は引き続き `references: ["EVMC4U", "uOSC.Runtime"]` を持つが、Runtime asmdef からは外す。Tests を走らせる利用者は asmdef 自作が必要 (Runtime 利用者は不要)。**実用的には妥当だが、R-7.1 の「利用者は asmdef 自作不要」を「Runtime 利用者には不要、テスト走らせる利用者には必要」に注記する必要がある**。
- (E) **`ExternalReceiverPatchTests.cs` の所属を別 asmdef へ分離**: 「EVMC4U 直依存テスト」と「Reflection 経路テスト」を別 asmdef にして、前者は本 Spec のスコープ外として温存。

> R-1.5 の現「design フェーズで確定する」は決定回避になっているため、design.md で **A〜E のうちどれを採るか** を明文化すること。本 dig としては **(D) を推奨** (利用者の主要 persona は Runtime ユーザで、テストを走らせるのは開発者であるため、asmdef 作業を「開発者だけ」に押し付けるのは合理的)。

---

### C-4. R-9.3「0 byte/tick」目標は素朴な `MethodInfo.Invoke` では達成不可能

**事実 (CLR 仕様)**:

- `MethodInfo.Invoke(receiver, args)` は引数配列を毎回確保する (空配列でも実装によっては alloc あり)。戻り値は `object` で boxing 経路を通る。
- 戻り値の `IReadOnlyDictionary<HumanBodyBones, Quaternion>` を取り出す際、Reflection の戻り値が `IDictionary<HumanBodyBones, Quaternion>` 等 generic 完全一致でない場合、cast 経路で boxing が必要。
- 値型 (`Quaternion`、`Vector3`) を `FieldInfo.GetValue` で取り出すと毎回 boxing が発生する。
- `Delegate.CreateDelegate` でオープンデリゲート化すれば argument array allocation と boxing を回避できるが、**条件付き** で、特に「receiver が値型」「メソッド第一引数を `this` として渡す instance method」「fields は通常 method 化されないため Expression Tree が必要」 など制約がある。
- Field setter/getter を allocation free にするには `Expression.Lambda<Action<TReceiver, TValue>>` を JIT する必要があり、IL2CPP では **AOT 制約により Expression.Compile が動かない** (System.Linq.Expressions が IL2CPP では非対応または部分対応)。

**含意**:

- R-9.2 / R-9.3 の「Tick あたり 0 byte alloc を目標、Expression Tree ベースのオープンデリゲート化を検討」は **IL2CPP 環境で破綻する**。
- もし IL2CPP で Expression Tree が動かないなら、IL2CPP 環境では `MethodInfo.Invoke` フォールバックに落とすしかなく、**性能要件 R-9.3 は IL2CPP では達成できないことを認める** か、**field を直接読む経路を諦めて patch を要請する** か、二択になる。

**意思決定が必要な点**:

- IL2CPP での性能目標を「0 byte/tick」から「N byte/tick (N は採寸結果次第)」に緩和するか、Mono Editor / Mono Standalone では 0 byte、IL2CPP では best-effort に分けるか。
- `Delegate.CreateDelegate` 単独 (Expression Tree 不要) で済む範囲をメンバ毎に表で確定する: `GetBoneRotationsView()` (instance method, 戻り値 dict) は CreateDelegate 可能、`Model = null` (Field 書込) は Expression Tree 必須、`port = N` (Field 書込) は Expression Tree 必須、`StartServer()` / `StopServer()` (instance method, 引数なし) は CreateDelegate 可能。
- Tick ホットパスは `GetBoneRotationsView()` のみ (initialize 時の field 書込はホットパス外) なので、**ホットパスだけ CreateDelegate、初期化は Invoke** という分割設計を採れば IL2CPP でも 0 byte 維持可能。design でこれを明示すべき。

---

### C-5. `EVMC4USharedReceiver.Receiver` プロパティは public な型署名を破壊する

**事実 (コード調査済み)**:

```csharp
public ExternalReceiver Receiver => _receiver;
```

`EVMC4USharedReceiver` クラスは **public** で、その `Receiver` プロパティも public。型は `EVMC4U.ExternalReceiver`。

`using EVMC4U;` を消すなら、このプロパティの宣言型を変える必要がある:
- (i) `public object Receiver => _receiver;` (情報量喪失、利用者は dynamic 推奨)
- (ii) `internal ExternalReceiver Receiver` 化 (asmdef references で EVMC4U が無いとそもそもコンパイル不能なので意味をなさない)
- (iii) プロパティ削除 (テストが使っているので破壊変更)

**現 Spec の扱い**:

- R-8.3 「`EVMC4USharedReceiver` の以下の振る舞いを変更しない」と書かれているが、**プロパティの宣言型変更は「振る舞い」ではなく「API シグネチャ」の変更**。R-8 の不変性保証範囲が曖昧。
- PlayMode tests は `shared.Receiver.InjectBoneRotationForTest(...)` を直接呼んでいる。これは C-3 の「テスト書換」と連動する。

**意思決定が必要な点**:

- `Receiver` プロパティを残すか削除するか、残すなら型を何にするか (`object` / `dynamic` / 自前 wrapper interface)。
- テスト経路と Runtime 経路で必要な公開度合いを切り分け、`internal Receiver` + テスト用 `[InternalsVisibleTo]` で済むかを確認する。

---

## 🟡 High-Severity Findings (確認推奨)

### H-1. uOSC の `port` / `autoStart` は public field か public property か上流側で確定していない

`EVMC4USharedReceiver.cs` の現コードは `server.autoStart = false;` / `server.port = port;` と直接代入している。これらが Reflection 経路に置き換わるとき、書込先が `FieldInfo` か `PropertyInfo` かを Reflection 解決層で **両方試す** か、片方に決め打つかを決める必要がある。

uOSC `2.2.0` の実装を README で固定するなら、design.md 段で実装を読んで「port は public field、autoStart は public field」と確定し、**Field のみで解決** にするのが allocation 観点で有利 (Field の方が Property より分岐が浅い)。両対応にすると分岐が増えコードの認知負荷も上がる。

### H-2. `gameObject.AddComponent(Type)` の解決可否

R-2.3 / R-3.3 で `gameObject.AddComponent` の **Type 引数版** を Reflection 経由で呼ぶとあるが、`UnityEngine.GameObject.AddComponent(System.Type)` は **public method** であり、これは Unity 側 (`UnityEngine.dll`) に静的に依存して呼ぶだけで足りる。Reflection 不要。**ただし戻り値の `Component` 型から先に進むには、`MonoBehaviour` への代入は OK でも、`ExternalReceiver` 固有メンバへ触るには別途 Reflection 経由 (FieldInfo/PropertyInfo) が必要**。

設計上は「`AddComponent(Type)` は素で呼ぶ、戻り値は `Component` で受ける、以後 Reflection」で問題ないが、要件文がこれを「`AddComponent` 自体も Reflection 経路扱い」と読めるため、設計フェーズで明確化が必要。

### H-3. `[RequireComponent(typeof(uOscServer))]` 等の attribute がリフレクション化対象に含まれていない

EVMC4U の `ExternalReceiver` は MonoBehaviour であり、内部に `[RequireComponent(typeof(uOscServer))]` 等の attribute を持つ可能性がある (要 upstream 確認)。`AddComponent(typeof(ExternalReceiver))` した瞬間 Unity が require chain を解決して `uOscServer` を自動 attach する場合、現在のコードのように **明示的に `AddComponent<uOscServer>` してから `AddComponent<ExternalReceiver>` する順序** が壊れていないか、Reflection 化前後で挙動差を出さないか確認が必要。

### H-4. R-7.4「後方互換: 利用者が既存自作 asmdef を残しても compilable」の検証手順が無い

利用者が `Assets/EVMC4U/EVMC4U.asmdef` を **削除しないまま** 新パッケージへ更新した場合、EVMC4U 型は引き続き `EVMC4U` assembly に居る。Reflection 解決層は `EVMC4U.ExternalReceiver, EVMC4U` で見つけられなければならない。

逆に **新規利用者は asmdef を作らない** ので `EVMC4U.ExternalReceiver, Assembly-CSharp` で見つける。

つまり Reflection 解決層は **2 つのアセンブリ名にフォールバックする必要がある**。これが設計フェーズで明示されていないと実装で漏れる。

### H-5. `EVMC4U.ExternalReceiver` の inheritance を辿らない設計か

upstream EVMC4U の `ExternalReceiver` が他クラスを継承していて、`LatestRootLocalPosition` 等が **基底クラス側** に定義されている場合、`Type.GetField("LatestRootLocalPosition", BindingFlags.Public | BindingFlags.Instance)` で見つかるが、`BindingFlags.DeclaredOnly` を入れると逃す。R-2/R-3 の `BindingFlags` 戦略を design で確定する必要あり。

### H-6. Domain Reload OFF + IL2CPP の組み合わせ動作未確認

R-6.2 (Domain Reload OFF 下でも `SubsystemRegistration` でキャッシュリセット) と R-6.4 (IL2CPP では `link.xml` で stripping 防止) は別個の関心事だが、**両方有効な構成** (Editor: Domain Reload OFF / Build: IL2CPP) で組み合わせ動作するかは設計フェーズで明示的に検証手順化されていない。検証シナリオ A' / B' のいずれもこの組合せをカバーしていない。

---

## 🟢 Lower-Severity Findings (記録のみ)

### L-1. 「VMC Sender (送信側) 実装」がスコープ外と書かれているが、本 Spec の動機 (Reflection) とは関係が薄い
スコープ境界の "Out of Scope" にいる必要があるかは弱い。混乱を避けるため、近接 Spec で扱うと書かれている項目に集約する方が良い。

### L-2. 「`Assembly.LoadFrom` / `Assembly.Load(byte[])` 等で動的にアセンブリをロードする機構」をスコープ外と書いた点は明確で良い
が、これに対応する **「だから何ができないのか」** の利用者目線の影響がドキュメントに無い (利用者が自分で Reflection アクセスを足したい場合の guard rail としての記載がないため、将来 misuse の余地がある)。

### L-3. 「動作確認済み EVMC4U リビジョン」と書かれているが、現在の patched ExternalReceiver.cs は **upstream のどの commit** にあてた patch か不明
README / design に「base commit hash」を記録すべき。

### L-4. `RegistryConflictException` 関連の不変性が R-8.6 でしか触れられていない
現実装で `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 経路の Factory 登録は EVMC4U/uOSC 解決と独立している (Factory だけ登録、Source インスタンス化は Initialize 時)。Reflection 化しても自己登録経路自体には影響しないことを design.md で明示する価値あり。

### L-5. Builtin Avatar Provider の `BuiltinAvatarProviderConfig_VmcDemo.asset` は影響を受けない
Reflection 化は MoCap 経路のみ。Avatar Provider 側にはタッチしない。これも design で「変更ファイル一覧」に **無い** ことが正しいと明示する。

---

## ⚠️ 事実誤認の可能性 (要件文書の修正候補)

### F-1. R-7「`Assets/EVMC4U/` 配下に `EVMC4U.asmdef` を作成する作業が **不要** になる」は patch 工程を捨象している

→ C-1 と同じ。`Assets/EVMC4U/ExternalReceiver.cs` を patch する手順が **そもそも README に書かれていない** ため、「不要になる」と謳う作業は元々ドキュメント化されていない作業。要件文の表現として「`EVMC4U.asmdef` を作る作業は不要だが、EVMC4U の patch 適用 (もしくは fork 同梱) は引き続き必要」と是正すべき。

### F-2. R-2.2 のメンバ表に `LatestRootLocalPosition` / `LatestRootLocalRotation` が含まれているが、これらは upstream EVMC4U のメンバではない

→ C-1 と連動。R-2.2 のメンバ表を **「ローカルパッチ適用後の `ExternalReceiver`」前提** と注記する必要あり。さもなくば R-5.3「動作確認済み EVMC4U リビジョン」は「リビジョン + 当方 patch」と書かないと再現できない。

---

## Decisions Required Before Design Phase

| # | Topic | Options | Recommendation | Risk if deferred |
|---|-------|---------|----------------|------------------|
| C-1 | EVMC4U local patch との関係 | (a)-(f) | (e): スコープを「asmdef 撤廃のみ」に限定し、patch 工程は引き続き利用者責務として README に追記 | High: 動機の根拠が崩れる |
| C-2 | アセンブリ解決戦略 | candidate whitelist / 全 AppDomain 走査 / Type.GetType(FQN) | candidate whitelist (`EVMC4U` → `Assembly-CSharp` → 全走査) を順試行 | High: 後方互換時に解決失敗 |
| C-3 | Tests asmdef 戦略 | (A)-(E) | (D): Tests のみ EVMC4U/uOSC 参照を保持、Runtime のみ Reflection 化 | High: 実装後に書換爆発 |
| C-4 | 性能目標の現実的設定 | 0 byte/tick 統一 / Mono のみ 0 byte / IL2CPP は best-effort | ホットパスのみ CreateDelegate (Field 書込はホット外) で IL2CPP も 0 byte/tick を達成可能と判断 | Med: validation 段で要件未達 |
| C-5 | `EVMC4USharedReceiver.Receiver` 型 | object / 削除 / wrapper interface / internal+InternalsVisibleTo | internal 化 + テスト用 InternalsVisibleTo (Tests asmdef 経路と整合) | Med: API 破壊 |
| H-1 | uOSC port/autoStart の Field/Property | 上流確定後 Field のみ | Field のみ (`uOSC 2.2.0` 確認後固定) | Low: 採寸 1 回で確定 |
| H-2 | `AddComponent(Type)` の扱い | 素で呼ぶ / Reflection 化 | 素で呼ぶ (Unity API は静的依存)。R 文の表現修正のみ | Low |
| H-3 | RequireComponent 順序 | upstream 確認 | upstream 確認の上で順序維持 | Low |
| H-4 | アセンブリ名フォールバック検証 | 検証シナリオ追加 | 検証シナリオ A' を「asmdef なし」、B' を「asmdef あり」に分割 | Med: 後方互換破壊 |
| H-5 | BindingFlags 戦略 | Public+Instance / +DeclaredOnly | Public+Instance のみ (継承考慮) | Low |
| H-6 | Domain Reload OFF + IL2CPP | 検証シナリオ追加 | シナリオ C' を新設して両ON 構成を validation に組込 | Med |

---

## Recommended Next Steps

1. **requirements.md を是正する**:
   - R-2.2 / R-7.1 / R-7.6 に「ExternalReceiver.cs ローカルパッチが前提」の注記を追加。
   - R-1.5 の「design で確定」を「Tests asmdef は EVMC4U/uOSC 参照を保持する」と確定 (C-3 推奨案 D)。
   - R-9.3 を「ホットパスは 0 byte/tick、初期化経路は Invoke 許容」と分割。

2. **research.md を作成して以下を記録**:
   - C-1 の意思決定根拠 (option e 採用、patch 同梱しない理由として GPLv3 制約の確認)
   - C-2 の解決戦略 (candidate whitelist 順)
   - uOSC `2.2.0` の `port` / `autoStart` が field か property かの確定 (実コード読解結果)
   - EVMC4U 動作確認 commit hash と patch diff の要約

3. **design.md で必ず明文化**:
   - メンバ表 (FQN, FieldInfo or PropertyInfo or MethodInfo, 想定型, アクセス方向, ホットパス内/外)
   - アセンブリ解決順序 (`EVMC4U` → `Assembly-CSharp` → 全走査) と各段の失敗キャッシュ寿命
   - Mono / IL2CPP 別の Delegate 戦略 (CreateDelegate 可否表)
   - Tests asmdef references 構成 (新パッケージ Runtime とは非対称)
   - 検証シナリオ A' / B' / C' (後者は Domain Reload OFF + IL2CPP)

4. **tasks.md に分解時の注意**:
   - C-1 の patch 同梱 / 非同梱判断は **タスク 0** に置く (他タスクの前提)。
   - C-3 の Tests 戦略は **タスク 1** に置き、Runtime Reflection 化前にテスト境界を確定する。
   - C-4 の性能採寸は **`Initialize` 実装後の最初の検証** で実施し、Tick 経路の Reflection 設計を後段で再検討できる手順にする。

---

## Investigation Summary

- Rounds completed: 1 (Phase 1〜2 ベース、Phase 3 の対話質問は Auto Mode のため非対話で代替)
- Assumptions challenged: 16 (Critical 5, High 6, Low 5)
- Decisions surfaced as required: 11 (C-1〜H-6)
- 事実誤認候補: 2 (F-1, F-2)

### Key Discoveries (impact 順)

1. **EVMC4U のローカルパッチ前提が要件文書から欠落** (C-1 / F-1 / F-2): Reflection 化が「セットアップ簡素化」の動機を成立させない可能性。
2. **`Assembly-CSharp` フォールバックの解決戦略未定** (C-2 / H-4): 既存利用者と新規利用者で解決パスが異なる、双方サポートする必要。
3. **テスト書換コストの先送り** (C-3): R-1.5「design で確定」が実装フェーズで噴出するリスク。Tests のみ参照保持で逃すのが現実解。
4. **IL2CPP での性能目標達成性** (C-4): Expression Tree が IL2CPP で動かないため、ホットパスとそれ以外で戦略を分割する必要。
5. **Public 型署名の破壊変更** (C-5): `EVMC4USharedReceiver.Receiver` プロパティを残すかどうかが API 互換に直結。

### Remaining Risks (acknowledged)

- L-3: patch のベース commit hash が記録されていない (本 Spec とは別問題、predecessor の TODO)
- IL2CPP + Domain Reload OFF + Reflection 失敗キャッシュの三者組合せは validation 計画に未組込
- 利用者が手元の `Assets/EVMC4U/` を upstream で上書き更新したとき、patch 喪失で Tick が突然動かなくなる static failure mode は本 Spec の検出範囲外 (利用者運用責務)
