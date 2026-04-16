# design.md 検証レポート — mocap-vmc (再検証版)

> **生成日**: 2026-04-15 (初回) → **再検証日**: 2026-04-15 (2 回目)
> **検証対象**: `.kiro/specs/mocap-vmc/design.md`
> **参照ファイル**: `requirements.md`, `_shared/contracts.md`
> **総合判定**: **GO** (前回の重大指摘 2 件および中/低優先指摘はすべて解消済み)

---

## 総合サマリー

| 観点 | 評価 | 詳細 |
|------|------|------|
| Requirements Traceability | ✅ 全 Req カバー | 要件 1〜10 すべて設計に反映 |
| Contract Compliance | ✅ 完全準拠 | IMoCapSource シグネチャ・Config 基底型・Registry 契約と整合 |
| **OSC ライブラリ選定** | ✅ **解消** | `com.hidano.uosc 1.0.0` に修正済み。npm registry 実在確認済み |
| **Unity 6 対応明記** | ✅ **解消** | tasks フェーズ実機検証として明記済み |
| HumanoidMotionFrame 引数順序 | ✅ **解消** | §6.4 で正しい順序に修正済み、コメントで明示 |
| Quaternion 拡張フィールド方針 | ✅ **解消** | §6.3 に採用しない旨と合意変更プロセス要件を明記 |
| `[InitializeOnLoadMethod]` 記述統一 | ✅ **解消** | `[UnityEditor.InitializeOnLoadMethod]` に統一済み |
| EVMC4U 参考実装帰属 | ✅ 明記済み | §4・§6.2 に MIT ライセンス帰属および実装ファイルへの記載義務を明示 |
| Factory 自己登録エラーハンドリング | ✅ 実装済み | try-catch + ErrorChannel.Publish パターンがコード例に明示 |
| uOSC コールバック受信モデル | ✅ 反映済み | §6.1・§6.2 でコールバックモデルを採用と明示 |
| エラーハンドリング | ✅ 適切 | OnError 非発行・VmcReceive/InitFailure カテゴリ設計が明確 |
| スレッドモデル | ✅ 適切 | §13 に責務分担テーブルとスレッドセーフティ確保方針を記載 |

---

## 前回指摘 解消状況テーブル

| ID | 優先度 | 内容 | 解消状況 | 根拠箇所 |
|----|--------|------|----------|----------|
| **C-1** | ❌ 重大 | OSC ライブラリ ID 誤記 (`com.yetanotherclown.osccore`) | ✅ **解消** | §4 の選定表・manifest.json 例・本文すべてを `com.hidano.uosc 1.0.0` に変更済み。旧 ID の残存なし。 |
| **C-2** | ❌ 重大 | Unity 6 対応の明記 | ✅ **解消** | §4「Unity 6 互換性について」注釈に「tasks フェーズでの実機検証、またはリリース前検証項目として実施する」と明記済み |
| **M-1** | ⚠️ 中優先 | HumanoidMotionFrame コンストラクタ引数順序 | ✅ **解消** | §6.4 の概念コードが `new HumanoidMotionFrame(timestamp, muscles, rootPosition, rootRotation)` に修正済み。「引数順序は contracts.md 2.2 章の確定シグネチャに従う」コメントも追記済み |
| **M-2** | ⚠️ 中優先 | Quaternion 直格納の拡張フィールド方針 | ✅ **解消** | §6.3 に `[M-2 解決]` ブロックを追加し、「拡張フィールドは採用しない」および「contracts.md 2.2 章の合意変更プロセスが必要」を明記済み |
| **L-1** | ℹ️ 低優先 | `[InitializeOnLoad]` / `[InitializeOnLoadMethod]` 記述統一 | ✅ **解消** | §10.2 のエディタ登録コードが `[UnityEditor.InitializeOnLoadMethod]` メソッド属性に変更済み。§10.2 末尾に `[L-1 解決]` 注記あり。§5.3 API 仕様コードも同属性で統一済み |

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
| 要件 6: Sender スコープ外 | §1 (スコープ明示) | ✅ |
| 要件 7: エラー処理と診断 | §8 | ✅ |
| 要件 8: アセンブリ・名前空間・Registry 登録 | §10, §14 | ✅ |
| 要件 9: Config 型定義と Factory キャスト責務 | §5.1, §5.3, §10 | ✅ |
| 要件 10: テスト戦略 | §15 | ✅ |

**特記事項なし。** 要件 2-7 の timestamp 打刻責務 (dig ラウンド 4 確定事項) も §6.4 に設計記載あり。

---

## 2. Contract Compliance (contracts.md 2.1 / 2.2 章)

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

**design.md §6.4 との対照 (再検証):**

```csharp
// design.md §6.4 の修正後コード
// 引数順序は contracts.md 2.2 章の確定シグネチャに従う:
// HumanoidMotionFrame(double timestamp, float[] muscles, Vector3 rootPosition, Quaternion rootRotation)
var frame = new HumanoidMotionFrame(timestamp, muscles, rootPosition, rootRotation);
```

前回指摘 M-1 の引数順序不一致は修正済み。コメントによる明示も追加されており、実装者の誤読リスクは解消されている。

**判定: ✅ 完全整合**

### 2.3 Config 基底型階層整合 (contracts.md 1.5 章)

`VMCMoCapSourceConfig : MoCapSourceConfigBase` の設計は contracts.md 1.5 章の具象 Config 定義責務テーブルと完全に整合している。

**判定: ✅ 整合**

### 2.4 Registry/Factory 契約整合 (contracts.md 1.4 章)

- `IMoCapSourceFactory.Create(MoCapSourceConfigBase config)` シグネチャ: ✅ 整合
- 属性ベース自己登録 (`[RuntimeInitializeOnLoadMethod]` / `[UnityEditor.InitializeOnLoadMethod]`): ✅ 整合
- `RegistryConflictException` 対応 (要件 9-9): ✅ 設計に明記

**判定: ✅ 整合**

---

## 3. OSC ライブラリ選定 (再検証)

### ✅ 解消確認: `com.hidano.uosc 1.0.0`

**Web 検証結果 (npm registry: `https://registry.npmjs.com/com.hidano.uosc`):**

| 確認項目 | 結果 |
|---------|------|
| パッケージ存在確認 | ✅ **登録済み** |
| バージョン 1.0.0 の存在 | ✅ **確認済み (latest)** |
| 説明 | "uOSC with SO_REUSEADDR option enabled for Unity." |
| ライセンス | MIT |
| 最終更新 | 2026年2月1日 |
| キーワード | osc, network, udp |
| 作者 | hidano |

前回指摘の `com.yetanotherclown.osccore` (存在しない誤記) は完全に除去され、全箇所が `com.hidano.uosc` に置き換えられている。manifest.json 例も修正済み。

**判定: ✅ 解消確認済み。npm registry での実在確認完了**

### Unity 6 互換性記載 (C-2 再確認)

§4 の注釈:

> **Unity 6 互換性について**: hecomi/uOSC は Unity 2017 以降に対応している。`com.hidano.uosc` としての Unity 6000.3 での明示的な動作確認は tasks フェーズでの実機検証、またはリリース前検証項目として実施する。

tasks フェーズでの検証項目として明示されており、前回指摘 C-2 の要求を満たしている。

**判定: ✅ 解消確認済み**

---

## 4. EVMC4U 参考実装帰属記述の評価

### ✅ 適切に明記済み

§4「VMC プロトコル解析方針」および §6.2 に以下の内容が記載されている:

- リポジトリ URL: `https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity`
- ライセンス: MIT (Copyright (c) 2019 gpsnmeajp) と明記
- 採用方針: 丸ごと取り込まず、OSC アドレスハンドリング部を**参考実装**として自前実装する旨を明示
- MonoBehaviour ベースのアーキテクチャ衝突を回避する理由も記載
- **帰属表記義務**: 実装ファイル `VmcMessageRouter.cs` のヘッダーコメントにリポジトリ URL とライセンス帰属を明記することが設計上の義務として記載されている

**品質評価**: 参照元ライセンス (MIT)・参照範囲 (アドレスハンドリング構造のみ)・帰属表記の実装義務の 3 点が明示されており、ライセンスコンプライアンス上の問題はない。

---

## 5. Factory 自己登録 try-catch + ErrorChannel Publish パターン

### ✅ 実装済み

§5.3 API 仕様コードおよび §10.1・§10.2 のエントリコードに、以下のパターンが明示されている:

```csharp
try
{
    RegistryLocator.MoCapSourceRegistry.Register("VMC", new VMCMoCapSourceFactory());
}
catch (RegistryConflictException ex)
{
    RegistryLocator.ErrorChannel.Publish(
        SlotErrorCategory.RegistryConflict,
        ex,
        "VMCMoCapSourceFactory.RegisterRuntime: typeId=\"VMC\" は既に登録済みです。");
}
```

ランタイム登録 (§10.1) とエディタ登録 (§10.2) の双方に同パターンが適用されており、例外の握り潰しがないことが確認できる。

**判定: ✅ 要件 9-9 の「RegistryConflictException を握り潰さない」要求を満たす**

---

## 6. uOSC コールバック受信モデルの反映確認

### ✅ 適切に反映済み

§6.1 の概念コードおよびデータフロー図 (§2) に、uOSC のコールバック受信モデルが以下のとおり記述されている:

- `VmcOscAdapter` が uOSC のコールバック (`OnOscMessageReceived`) を受け取り `VmcMessageRouter` へ転送する薄いアダプタ層として設計されている
- OSC パース自体は uOSC 側が担い、`VmcMessageRouter` は address と引数リストを解釈するルータとして機能する
- §14 のファイル構成に `VmcOscAdapter.cs` が `Internal/` 配下に独立ファイルとして配置されている
- §12.1 シーケンス図でも `uOSC → VmcOscAdapter → VmcMessageRouter` の流れが明示されている

**判定: ✅ コールバックモデルが設計全体で一貫して反映されている**

---

## 7. 新規発見事項

### 7.1 軽微な注意点: `bindAddress` デフォルト値の変更

requirements.md 要件 3-3 では受信アドレスのデフォルトを `127.0.0.1` (ローカルホストのみ) と規定しているが、design.md §5.1 の `VMCMoCapSourceConfig` では `bindAddress = "0.0.0.0"` (全インターフェース) をデフォルト値として採用している。

```csharp
// requirements.md 要件 3-3: デフォルト 127.0.0.1
// design.md §5.1 実装: デフォルト "0.0.0.0"
public string bindAddress = "0.0.0.0";
```

`0.0.0.0` バインドは外部 PC の VMC 送信ツールからも受信できる実用的な選択であり、VMC 利用シナリオとしては適切と考えられる。ただし requirements.md との差異が設計書に明示的に注記されていない点は、実装者が混乱するリスクがある。

**影響度**: 低 (実用上は `0.0.0.0` の方が適切な可能性が高い)
**推奨対応**: §5.1 に「デフォルト値を `0.0.0.0` に変更した理由 (外部 VMC 送信ソース対応)」を注記するか、requirements.md 要件 3-3 を設計フェーズの合意として更新する。tasks フェーズでの確認事項として残すことも可。

### 7.2 Open Issues の更新確認

前回の Open Issues テーブル (OI-1〜OI-3) のうち、OI-3 (`[InitializeOnLoad]` / `[InitializeOnLoadMethod]` 統一) は解消済み。OI-1・OI-2 は tasks フェーズで確定される未確定事項として適切に管理されている。

| No. | 内容 | 状態 |
|----|------|------|
| OI-1 | `VmcFrameBuilder` のフレームフラッシュタイミング | 未確定 (tasks フェーズで確定予定) |
| OI-2 | Bone クォータニオン直格納か `Muscles` 変換かの実装方針 | 未確定 (ただし拡張フィールド追加は採用しない旨を §6.3 で明記済み) |
| OI-3 | `[RuntimeInitializeOnLoadMethod]` と `[InitializeOnLoad]` の実装統一 | ✅ 解消済み |

---

## 8. 指摘事項一覧 (再検証後)

### ❌ 重大 (要修正)

なし

### ⚠️ 中優先 (推奨修正)

なし

### ℹ️ 低優先 (情報・確認事項)

| ID | 場所 | 内容 |
|----|------|------|
| **L-new-1** | §5.1 `VMCMoCapSourceConfig` | `bindAddress` のデフォルト値が requirements.md 要件 3-3 (`127.0.0.1`) と design (`0.0.0.0`) で異なる。実用上は `0.0.0.0` が適切だが、設計書に変更理由の注記があると望ましい。 |

---

## 9. 総合判定

**判定: GO**

前回の重大指摘 (C-1: OSC ライブラリ ID 誤記、C-2: Unity 6 対応明記) および中/低優先指摘 (M-1: コンストラクタ引数順序、M-2: 拡張フィールド方針、L-1: 属性記述統一) はすべて解消済み。`com.hidano.uosc 1.0.0` の npm registry 実在も確認された。EVMC4U 参考実装の帰属記述は MIT ライセンス・帰属範囲・実装ファイルへの記載義務の 3 点を網羅しており品質は高い。新規発見の `bindAddress` デフォルト値相違は低優先であり、tasks フェーズへの繰り越しで問題ない。

**次のアクション**: `/kiro:spec-tasks mocap-vmc` で tasks フェーズへ進行可能。

---

## 検証経路メモ

- Skill `kiro:validate-design mocap-vmc` はパラメータ展開の不具合により手動検証に切り替え。
- `com.hidano.uosc` の実在確認は WebFetch (`https://registry.npmjs.com/com.hidano.uosc`) で実施。バージョン 1.0.0 が latest として登録されていることを確認。
- design.md の全セクション (§1〜§15、付録) を通読し、前回指摘 5 件の解消状況および新規矛盾を確認した。
