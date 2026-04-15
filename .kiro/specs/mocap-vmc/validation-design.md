# design.md 検証レポート — mocap-vmc

> **生成日**: 2026-04-15  
> **検証対象**: `.kiro/specs/mocap-vmc/design.md`  
> **参照ファイル**: `requirements.md`, `_shared/contracts.md`, `slot-core/design.md`  
> **総合判定**: **条件付き GO** (要対応: OSC ライブラリ選定の重大誤記 / 中優先指摘 2 件)

---

## 総合サマリー

| 観点 | 評価 | 詳細 |
|------|------|------|
| Requirements Traceability | ✅ 全 Req カバー | 要件 1〜10 すべて設計に反映 |
| Contract Compliance | ✅ 概ね準拠 | IMoCapSource シグネチャ・Config 基底型・Registry 契約と整合 |
| **OSC ライブラリ選定** | ❌ **重大誤記** | `com.yetanotherclown.osccore` は存在しない。正しくは `com.stella3d.osccore` |
| VMC プロトコル対応範囲 | ✅ | v2.5 明記、対応 OSC アドレス網羅 |
| VMC → HumanoidMotionFrame マッピング | ✅ | Bone 名・未受信ボーン処理・Root 取り扱いを設計 |
| Subject 実装選定 | ✅ | `Synchronize()` + `Publish().RefCount()` を明示 |
| エラーハンドリング | ✅ | OnError 非発行・VmcReceive/InitFailure カテゴリ設計が明確 |
| Factory 自動登録 | ✅ | ランタイム・エディタ双方のコード例あり |

---

## 1. Requirements Traceability

### 検証結果: ✅ 全要件カバー済み

| 要件 | 設計箇所 | カバー状態 |
|------|---------|-----------|
| 要件 1: VmcMoCapSource 実装 | §5.2, §6.1, §6.5 | ✅ |
| 要件 2: 受信スレッドモデル | §6.1, §13 | ✅ |
| 要件 3: 通信パラメータ設定 | §5.1, §9.2 | ✅ |
| 要件 4: Slot 紐付けと動的差替 | §9, §11, §12.2 | ✅ |
| 要件 5: VMC データ中立表現変換 | §6.3, §7 | ✅ |
| 要件 6: Sender スコープ外 | §1 (スコープ明示), 補足 | ✅ |
| 要件 7: エラー処理と診断 | §8 | ✅ |
| 要件 8: アセンブリ・名前空間・Registry 登録 | §10, §14 | ✅ |
| 要件 9: Config 型定義と Factory キャスト責務 | §5.1, §5.3, §10 | ✅ |
| 要件 10: テスト戦略 | §15 | ✅ |

**特記事項なし。** 要件 2-7 の timestamp 打刻責務 (dig ラウンド 4 確定事項) も §6.4 に設計記載あり。

---

## 2. Contract Compliance (contracts.md 2.1 / 2.2 章、slot-core/design.md §3.1)

### 2.1 IMoCapSource シグネチャ整合

**contracts.md 2.1 章最終シグネチャ:**

```csharp
public interface IMoCapSource : IDisposable
{
    string SourceType { get; }
    void Initialize(MoCapSourceConfigBase config);
    IObservable<MotionFrame> MotionStream { get; }
    void Shutdown();
}
```

**design.md §5.2 VmcMoCapSource 公開 API との対照:**

| メンバー | contracts.md | design.md §5.2 | 整合 |
|---------|:----------:|:-------------:|:---:|
| `string SourceType { get; }` | ✅ | ✅ | ✅ |
| `void Initialize(MoCapSourceConfigBase config)` | ✅ | ✅ | ✅ |
| `IObservable<MotionFrame> MotionStream { get; }` | ✅ | ✅ | ✅ |
| `void Shutdown()` | ✅ | ✅ | ✅ |
| `: IDisposable` | ✅ | ✅ (`Dispose()` 明示) | ✅ |

**判定: ✅ 完全整合**

### 2.2 モーションデータ中立表現整合 (contracts.md 2.2 章)

**HumanoidMotionFrame コンストラクタシグネチャ (contracts.md 確定):**

```csharp
public HumanoidMotionFrame(double timestamp, float[] muscles,
    Vector3 rootPosition, Quaternion rootRotation) : base(timestamp)
```

**design.md §6.4 との対照:**

```csharp
// design.md §6.4 の概念コード
var frame = new HumanoidMotionFrame(muscles, rootPosition, rootRotation, timestamp);
```

> ⚠️ **軽微な注意点** (中優先): §6.4 のコンセプトコードにおけるコンストラクタ引数の順序が contracts.md 2.2 章の最終シグネチャ `(double timestamp, float[] muscles, Vector3 rootPosition, Quaternion rootRotation)` と**逆順** (`timestamp` が末尾) になっている。コンセプトコードは説明用であり設計の本質ではないが、実装者が誤読するリスクがある。

**判定: ⚠️ 概念コードの引数順序が契約と不一致 (要修正または注記追加を推奨)**

### 2.3 Config 基底型階層整合 (contracts.md 1.5 章)

`VMCMoCapSourceConfig : MoCapSourceConfigBase` の設計は contracts.md 1.5 章の具象 Config 定義責務テーブルと完全に整合している。

**判定: ✅ 整合**

### 2.4 Registry/Factory 契約整合 (contracts.md 1.4 章)

- `IMoCapSourceFactory.Create(MoCapSourceConfigBase config)` シグネチャ: ✅ 整合
- 属性ベース自己登録 (`[RuntimeInitializeOnLoadMethod]` / `[InitializeOnLoadMethod]`): ✅ 整合
- `RegistryConflictException` 対応 (要件 9-9): ✅ 設計に明記

**判定: ✅ 整合**

---

## 3. OSC ライブラリ選定

### ❌ 重大誤記: パッケージ ID が存在しない

design.md §4 は以下を主張している:

> `com.yetanotherclown.osccore` として OpenUPM で公開されており…

**Web 検証結果:**

| 確認項目 | 結果 |
|---------|------|
| `com.yetanotherclown.osccore` の OpenUPM 登録 | **❌ 404 — 存在しない** |
| `com.yetanotherclown.osccore` の GitHub 検索 | **見つからない** |
| 実際の OscCore (stella3d) パッケージ名 | **`com.stella3d.osccore`** |
| `com.stella3d.osccore` の OpenUPM 登録 | ✅ 登録済み (確認 URL: `openupm.com/packages/com.stella3d.osccore/`) |
| OscCore (stella3d) 最新バージョン | `1.1.2` |
| OscCore (stella3d) 最小 Unity バージョン | `2019.4` |
| OscCore (stella3d) ライセンス | MIT |
| OscCore (stella3d) NuGet 依存 | なし |

**stella3d/OscCore に関する追加確認:**

- GitHub: `https://github.com/stella3d/OscCore`
- 最終リリース: v1.1.0 (2020年2月25日) — **最後のリリースから約4〜5年経過**
- Unity 6000.x 明示的な互換性確認: **GitHub 上に記載なし**
- OpenUPM には `com.stella3d.osccore` として登録済みであり、`package.json` の `name` フィールドも `com.stella3d.osccore` を使用

また、OscCore の VRChat フォーク (`vrchat/OscCore`) および VirtualCast フォーク (`com.stretchsense.osccore`) が存在するが、design.md ではいずれも言及されていない。

**design.md §4 の修正が必要な箇所:**

```json
// 誤: 存在しないパッケージ ID
"com.yetanotherclown.osccore": "1.x.x"

// 正: OpenUPM に実在するパッケージ ID
"com.stella3d.osccore": "1.1.2"
```

**追加懸念事項:**

- OscCore (stella3d) の最終リリースが 2020 年であり、**約 5 年間リリース更新がない**。Unity 6000.x での動作確認情報が公式にない点は、採用リスクとして明記すべきである。
- アクティブにメンテされている VRChat フォーク (`vrchat/OscCore`) の採用も検討候補となりうる。

**判定: ❌ 重大誤記 — design.md の修正が必要**

---

## 4. VMC プロトコル対応範囲

### 判定: ✅ 適切

| 確認項目 | 結果 |
|---------|------|
| VMC Protocol v2.5 明記 | ✅ §3 冒頭に明記 |
| `/VMC/Ext/Root/Pos` 対応 | ✅ 実装対象として明記 |
| `/VMC/Ext/Bone/Pos` 対応 | ✅ 実装対象として明記 |
| `/VMC/Ext/Blend/Val` 対応 | ✅ 受信のみ・変換対象外として明記 |
| `/VMC/Ext/Blend/Apply` 対応 | ✅ 受信のみ・変換対象外として明記 |
| `/VMC/Ext/OK` の扱い | ✅ スキップ (ログのみ) として明記 |
| `/VMC/Ext/T` の扱い | ✅ 使用しない (VMC v2.5 不安定理由) として明記 |

**特記事項なし。** 要件 5-1 の「OSC アドレスの最終リストは design フェーズで確定」の要求に対して、§3 のテーブルが十分な回答を提供している。

---

## 5. VMC → HumanoidMotionFrame マッピング

### 判定: ✅ 適切 (軽微な注意点あり)

| 確認項目 | 設計箇所 | 評価 |
|---------|---------|------|
| VMC Bone 名 ↔ HumanBodyBones 変換 | §7.1 `VmcBoneMapper` | ✅ 全 55 ボーン・`Enum.GetValues` による辞書初期化 |
| 未受信 Bone の扱い | §7.3 | ✅ `Muscles[i] = 0.0f` (アイドルポーズ) として明記 |
| Muscles 配列長 | §7.3 | ✅ `HumanTrait.MuscleCount = 95` に固定 |
| Root Position/Rotation 取り扱い | §7.2 | ✅ 引数形式・座標系 (Unity 左手 Y-up) の変換不要を明記 |
| BlendShape の扱い | §7.4 | ✅ 初期版は変換対象外と明記 |

**注意点:** §6.3 の「初期版の簡略方針」でボーン回転クォータニオンを直接 `HumanoidMotionFrame` に格納する「拡張フィールド」を検討している旨が記載されている。しかし contracts.md 2.2 章の `HumanoidMotionFrame` 最終シグネチャには `float[] Muscles` のみ定義されており、「クォータニオン格納用の拡張フィールド」は存在しない。

> ⚠️ **軽微な注意点** (中優先): §6.3 の簡略方針 (クォータニオン直格納の拡張フィールド) は contracts.md 2.2 章の `HumanoidMotionFrame` 最終シグネチャと矛盾する可能性がある。tasks フェーズで方針を確定する旨は記載されているが、設計ドキュメントとして「拡張フィールドの追加は contracts.md 2.2 章の変更を要する合意変更プロセスが必要」という注記があると明確になる。

---

## 6. Subject 実装選定

### 判定: ✅ 具体的かつ適切

| 確認項目 | 設計箇所 | 評価 |
|---------|---------|------|
| スレッドセーフ化手段 | §6.5 `Subject.Synchronize()` | ✅ UniRx 標準ラッパーを採用。`lock` 手動実装より信頼性が高い |
| マルチキャスト化 | §6.5 `Publish().RefCount()` | ✅ 購読者ゼロ時の自動 Disconnect 付き Hot Observable |
| コード例 | §6.5 | ✅ `_rawSubject`, `_subject`, `_stream` の 3 段構成を具体的に示す |
| 設計決定記録 | §補足テーブル | ✅ `BehaviorSubject` / `lock` との比較選定理由を記録 |

---

## 7. エラーハンドリング

### 判定: ✅ 十分に設計されている

| 確認項目 | 設計箇所 | 評価 |
|---------|---------|------|
| `OnError` 非発行の保証 | §8.3 | ✅ `try-catch` 全捕捉・`Subject.OnError()` 不使用を明記 |
| OSC パースエラー → `VmcReceive` 発行 | §8.1 テーブル, §8.2 | ✅ |
| ネットワーク切断 → `VmcReceive` 発行 | §8.1 テーブル, §6.1 (SocketException 捕捉) | ✅ |
| ポート競合 → `InitFailure` (SlotManager 側発行) | §8.1 テーブル | ✅ contracts.md 1.3 章の責務分担と整合 |
| `Debug.LogError` 抑制責務 | §8.2 | ✅ `DefaultSlotErrorChannel` 側の責務と明記 |
| ワーカー未ハンドル例外の処理 | §6.1 概念コード, §8.1 | ✅ |

---

## 8. Factory 自動登録

### 判定: ✅ 要件 9-7/9-8 を満たす具体的なコード例あり

| 確認項目 | 設計箇所 | 評価 |
|---------|---------|------|
| `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` ランタイム登録 | §10.1 | ✅ 完全なコード例あり |
| `[InitializeOnLoad]` クラス属性 + 静的コンストラクタ (エディタ登録) | §10.2 | ✅ 完全なコード例あり |
| `#if UNITY_EDITOR` ガードまたは Editor asmdef 分離 | §10.2 | ✅ `Editor/MoCap/VMC/` の独立 asmdef として分離 |
| `RegistryConflictException` 非握り潰し | §5.3 XML コメント | ✅ Domain Reload OFF 対応の注記あり |

**注意点:** §10.2 では `[InitializeOnLoad]` クラス属性 + 静的コンストラクタを採用しているが、§5.3 の API 仕様では `[UnityEditor.InitializeOnLoadMethod]` メソッド属性が示されており**記述が不統一**である。実装時にどちらかに統一する旨の注記はあるが、設計ドキュメントとして統一されていると望ましい。

---

## 9. Open Issues (設計上の未確定事項)

以下は design.md に「tasks フェーズで確定」として残されている事項:

| No. | 内容 | 場所 | 影響度 |
|----|------|------|--------|
| OI-1 | `VmcFrameBuilder` のフレームフラッシュタイミング (「最後のメッセージ受信後」か「一定時間経過後」か) | §6.3 | 中 |
| OI-2 | Bone クォータニオン直格納か `Muscles` 変換かの実装方針 | §6.3 | 高 (contracts.md 2.2 章整合に影響) |
| OI-3 | `[RuntimeInitializeOnLoadMethod]` と `[InitializeOnLoad]` の実装統一 | §10.2, §5.3 | 低 |

---

## 10. 指摘事項一覧

### ❌ 重大 (要修正)

| ID | 場所 | 内容 |
|----|------|------|
| **C-1** | §4 OSC ライブラリ選定・manifest.json 例 | `com.yetanotherclown.osccore` は OpenUPM に存在しない。正しいパッケージ ID は `com.stella3d.osccore` (バージョン `1.1.2`)。manifest.json 例および本文を修正すること。 |
| **C-2** | §4 OSC ライブラリ選定 | OscCore (stella3d) の最終リリースが 2020 年であり約 5 年間更新がない。Unity 6000.x での明示的な動作確認情報が公式 GitHub に存在しない。採用リスクを設計ドキュメントに記載するか、VRChat フォーク (`vrchat/OscCore`) 等のアクティブなフォークとの比較を追記することを推奨。 |

### ⚠️ 中優先 (推奨修正)

| ID | 場所 | 内容 |
|----|------|------|
| **M-1** | §6.4 概念コード | `new HumanoidMotionFrame(muscles, rootPosition, rootRotation, timestamp)` の引数順序が contracts.md 2.2 章の確定シグネチャ `(double timestamp, float[] muscles, Vector3 rootPosition, Quaternion rootRotation)` と異なる。コードコメントに「引数順序は contracts.md 2.2 章に従うこと」を注記するか、正しい順序に修正すること。 |
| **M-2** | §6.3 初期版の簡略方針 | クォータニオン直格納の「拡張フィールド」追加方針は、`HumanoidMotionFrame` の最終シグネチャ変更 (合意変更プロセス) を要する点を注記すること。 |

### ℹ️ 低優先 (情報・確認事項)

| ID | 場所 | 内容 |
|----|------|------|
| **L-1** | §5.3 vs §10.2 | `[UnityEditor.InitializeOnLoadMethod]` メソッド属性 (§5.3) と `[InitializeOnLoad]` クラス属性 (§10.2) の記述が不統一。tasks フェーズで統一することを明記済みだが、設計ドキュメントとして一本化が望ましい。 |
| **L-2** | §11.1 参照共有モデル | Descriptor の等価性が「同一 Config インスタンス」基準であると明示されており、同値・別インスタンスの場合に SocketException が発生するシナリオも記載済み。この仕様の意図 (ポート共有は同一インスタンスの意図的な共有のみ) をユーザー向けドキュメントやサンプルで補足することを将来検討すること。 |

---

## 検証経路メモ

- Skill `kiro:validate-design mocap-vmc` 経由での実行を試みたが、feature name パラメータが空のまま展開されたため手動検証に切り替えた。
- OSC ライブラリの実在性確認は WebFetch (OpenUPM) および WebSearch を使用して実施した。
- `com.yetanotherclown.osccore` の OpenUPM ページは 404 を返し、GitHub 検索でも該当ユーザー/リポジトリは確認できなかった。
- `com.stella3d.osccore` は OpenUPM に登録済みであり、GitHub (`stella3d/OscCore`) の `package.json` にも同名が使用されていることを確認した。
