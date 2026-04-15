# Design Validation Report: slot-core

## 検証日時
2026-04-15

## 総合評価
⚠️ Conditional Pass

設計全体の品質は高く、API シグネチャの確定度・シーケンス図の充実度・スレッドモデルの明示など、多くの観点で優れた設計ドキュメントである。ただし、以下に列挙する軽微〜中程度の不整合・未確定事項が残存しており、Tasks フェーズ開始前に確認・解消することを推奨する。

---

## 要件トレーサビリティマトリクス

| Req | タイトル | カバー章 | 状態 |
|-----|---------|---------|------|
| Req 1 | Slot データモデル (Descriptor ベース) | §3.10–3.11, §5.1–5.2 | ✅ |
| Req 2 | SlotRegistry / SlotManager 動的管理 API | §3.14, §4.1, §6.1–6.2 | ✅ |
| Req 3 | Slot ライフサイクルとリソース所有権 | §4.1–4.2, §6.2–6.3 | ✅ |
| Req 4 | `IMoCapSource` 抽象インターフェース定義 | §3.1, §7.3, §8.1 | ✅ |
| Req 5 | `IAvatarProvider` 抽象インターフェース定義 | §3.2 | ✅ |
| Req 6 | `IFacialController` 抽象インターフェース定義 | §3.3 | ✅ |
| Req 7 | `ILipSyncSource` 抽象インターフェース定義 | §3.4 | ✅ |
| Req 8 | 設定シリアライズ可能性 (POCO / SO / JSON 許容) | §5.4 | ✅ |
| Req 8.5 | Config 基底型階層の定義 | §3.9 | ✅ |
| Req 9 | ProviderRegistry / SourceRegistry の動的登録と候補列挙 | §3.6, §4.5–4.6 | ✅ |
| Req 10 | MoCap ソース参照共有ライフサイクル | §6.4, §7.2 | ✅ |
| Req 11 | RegistryLocator の定義 | §3.7, §4.4 | ✅ |
| Req 12 | ISlotErrorChannel / SlotError / SlotErrorCategory | §3.8, §4.3, §8.1–8.2 | ✅ |
| Req 13 | FallbackBehavior 列挙体と SlotSettings への組み込み | §3.12, §11.2 | ⚠️ 一部未確定 |
| Req 14 | テスト戦略 (EditMode / PlayMode 両系統) | §9.3–9.4, §10.1–10.3 | ✅ |

**未対応・不完全な要件**: なし (全 14 要件にカバー章が存在する)

---

## 契約整合性

### contracts.md 各章との整合確認

| contracts.md 章 | 対応 design.md 章 | 整合状態 | 備考 |
|----------------|-----------------|---------|------|
| 1.1 SlotSettings フィールド一覧 | §3.11, §5.1 | ✅ 整合 | — |
| 1.2 シリアライズ形式 | §5.4 | ✅ 整合 | — |
| 1.3 ライフサイクル | §4.1, contracts §1.3 | ⚠️ 軽微な差異あり | 後述 |
| 1.4 ProviderRegistry / SourceRegistry 契約 | §3.6, §4.5–4.6 | ✅ 整合 | — |
| 1.5 Config 基底型階層 | §3.9 | ✅ 整合 | — |
| 1.6 RegistryLocator 契約 | §3.7 | ⚠️ 差異あり | 後述 |
| 1.7 エラーハンドリング契約 | §3.8, §8.1–8.2 | ✅ 整合 | — |
| 1.8 Fallback 挙動契約 | §3.12, §11.2 | ⚠️ 未確定事項 | 後述 |
| 2.1 IMoCapSource シグネチャ | §3.1 | ✅ 整合 | — |
| 3 (IAvatarProvider) | §3.2 | ✅ 整合 | — |
| 4 (IFacialController) | §3.3 | ✅ 整合 | — |
| 5 (ILipSyncSource) | §3.4 | ✅ 整合 | — |

### 指摘事項

**[C-1] contracts.md 1.6 章と design.md §3.7 の RegistryLocator シグネチャ差異**

contracts.md 1.6 章の骨格では `GetOrCreate<T>(ref T field)` ヘルパーメソッドを使用しているが、design.md §3.7 では null 合体演算子 (`??`) による直接的な遅延初期化を採用している。機能的等価だが、実装方式が二文書間で異なる。実装フェーズで混乱が生じないよう、どちらを正とするか明記が必要。

また contracts.md 1.6 章の `RegistryLocator` には `IFacialControllerRegistry` / `ILipSyncSourceRegistry` / `ISlotErrorChannel` のプロパティと `Override*()` メソッドが存在しないが、design.md §3.7 では追加定義されている。contracts.md が design.md より古い記述のままであり、Wave B 参照時に混乱を招く可能性がある。

**[C-2] contracts.md 1.3 章と design.md §4.1 のライフサイクル遷移の細部差異**

contracts.md 1.3 章の「破棄タイミング」に `IMoCapSource` の `MoCapSourceRegistry` 経由の解放が記載されているが、`Inactive → Disposed` 遷移のトリガーが `RemoveSlotAsync()` のみの記載であり、`SlotManager.Dispose()` による一括破棄ケースが明示されていない。design.md §4.1 の状態遷移図ではこれが明示されており、contracts.md 側の記述が不完全。

---

## API 網羅性

全公開インターフェースのシグネチャ確定状況を確認した。

| 型 | シグネチャ確定 | 備考 |
|---|:---:|------|
| `IMoCapSource` | ✅ | §3.1 に完全なシグネチャ |
| `IAvatarProvider` | ✅ | §3.2 に完全なシグネチャ |
| `IFacialController` | ⚠️ | `ApplyFacialData(object)` の `object` 型は将来置換前提。初期段階では許容 |
| `ILipSyncSource` | ⚠️ | `FetchLatestLipSync(): object` の戻り値型は将来置換前提。初期段階では許容 |
| `IAvatarProviderFactory` | ✅ | §3.5 |
| `IMoCapSourceFactory` | ✅ | §3.5 |
| `IFacialControllerFactory` | ✅ (将来用) | §3.5 |
| `ILipSyncSourceFactory` | ✅ (将来用) | §3.5 |
| `IProviderRegistry` | ✅ | §3.6 |
| `IMoCapSourceRegistry` | ✅ | §3.6 |
| `IFacialControllerRegistry` | ✅ (将来用) | §3.6 |
| `ILipSyncSourceRegistry` | ✅ (将来用) | §3.6 |
| `RegistryLocator` | ✅ | §3.7 |
| `ISlotErrorChannel` | ✅ | §3.8 |
| `SlotError` | ✅ | §3.8 |
| `SlotErrorCategory` | ✅ | §3.8 |
| `ProviderConfigBase` 等 Config 基底 | ✅ | §3.9 |
| `AvatarProviderDescriptor` 等 Descriptor | ⚠️ | 後述 [A-1] |
| `SlotSettings` | ✅ | §3.11 |
| `SlotManager` | ⚠️ | 後述 [A-2] |
| `FallbackBehavior` | ✅ | §3.12 |
| `RegistryConflictException` | ✅ | §3.13 |
| `SlotHandle` / `SlotStateChangedEvent` / `SlotState` | ✅ | §3.14 |

### 指摘事項

**[A-1] Descriptor の `Equals()` メソッドが `IEquatable<T>` を実装していない**

design.md §3.10 の `AvatarProviderDescriptor` と `MoCapSourceDescriptor` は `bool Equals(T other)` メソッドを定義しているが、`System.IEquatable<T>` インターフェースを明示的に実装しておらず、`object.Equals(object)` と `GetHashCode()` のオーバーライドもない。`IMoCapSourceRegistry` の参照共有判定は Descriptor の等価性に依存するため、辞書キーとして使用する場合は `GetHashCode()` の実装が必須となる。Tasks フェーズで実装対象として明示すること。

**[A-2] `SlotManager` の `InactivateSlotAsync` / `ReactivateSlotAsync` が未定義**

`SlotState` に `Inactive` 状態が定義されており、状態遷移図でも `Active ⇄ Inactive` 遷移が「将来機能」として存在するが、これらを引き起こす公開 API がない。初期版スコープ外として明示するか、あるいは将来 API 名を仮置きしておくことを推奨する。

**[A-3] `SlotRegistry` の公開インターフェースが未定義**

§9.1 でファイル `SlotRegistry.cs` が存在するが、design.md 内に `SlotRegistry` の公開 API シグネチャが記載されていない。`SlotManager` から内部的に使用される非公開クラスであるなら、その旨を明記すること。

---

## スレッド安全性

### 評価

§7 (スレッドモデル) は非常に充実しており、各コンポーネントのスレッド境界が表形式で明示されている。

| 観点 | 状態 | 備考 |
|------|------|------|
| Registry / Locator スレッド境界 | ✅ 明確 | §7.2 表で起動時のみ ⚠️ として許容を明記 |
| MotionStream の受信スレッド → メインスレッド復帰 | ✅ 明確 | `ObserveOnMainThread()` 使用を明示 |
| ErrorChannel のスレッド境界 | ✅ 明確 | §7.4 で `Subject.Synchronize()` 推奨を記載 |
| RegistryLocator 遅延初期化のスレッド競合 | ⚠️ | 後述 [T-1] |

### 指摘事項

**[T-1] RegistryLocator 遅延初期化のスレッド競合が「許容」のみで終わっている**

§4.4 で「`lock` は使用せず、null チェック + 代入のみ」「マルチスレッド競合が発生しうるが許容する」と記載されている。ただし、C# のメモリモデル上、フィールドへの null チェック + 代入はアトミックではなく、可視性の問題が生じうる。`volatile` キーワードの付与または `Interlocked.CompareExchange` の使用を Tasks フェーズで明示的に検討すること。

**[T-2] `DefaultSlotErrorChannel._subject` の `Subject.Synchronize()` ラップが推奨止まり**

§7.4 では `Subject.Synchronize()` の使用を推奨しているが、§4.3 の実装コードでは `new Subject<SlotError>()` のみで `Synchronize()` が適用されていない。受信ワーカースレッドから `VmcReceive` エラーが `Publish()` される可能性がある場合は、実装段階で必ず `Synchronize()` を適用すること。

---

## エラーハンドリング

| 観点 | 状態 | 備考 |
|------|------|------|
| 全 SlotErrorCategory の発生箇所定義 | ✅ | §8.1 に表形式で定義 |
| Debug.LogError 抑制の具体実装 | ✅ | §4.3, §8.2 に実装コード付きで定義 |
| 初期化失敗時の Disposed 遷移 | ✅ | §4.2 にシーケンス図あり |
| `RegistryConflict` の ErrorChannel 発行経路 | ⚠️ | 後述 [E-1] |
| `ApplyFailure` の発生経路と SlotManager 呼び出しタイミング | ⚠️ | 後述 [E-2] |

### 指摘事項

**[E-1] `RegistryConflict` カテゴリの ErrorChannel 発行経路が未定義**

§8.1 の表では `RegistryConflict` エラーの「発行責任」として「Registry」と記載されているが、Registry インターフェース (`IProviderRegistry` / `IMoCapSourceRegistry`) には `ISlotErrorChannel` への参照が存在しない。`RegistryConflictException` をスローするだけで ErrorChannel には発行しない設計なのか、あるいは呼び出し元 (Factory の `RegisterRuntime()` など) が `try-catch` して発行するのかが不明確。contracts.md 1.7 章にも同記述があり矛盾する。実装方針を一つに確定すること。

**[E-2] `ApplyFailure` の発生箇所が motion-pipeline 担当部分と重複**

§8.1 では `ApplyFailure` の「発生箇所」として「モーション適用処理 (motion-pipeline / SlotManager)」と記載されている。しかし motion-pipeline は別 Spec であり、slot-core の `SlotManager` がどこまで `ApplyFailure` を捕捉・通知する責務を持つかが不明瞭。slot-core スコープで `SlotManager` が担う `ApplyFailure` の発生ケースを具体的に列挙しておく必要がある。

---

## ライフサイクル

| 観点 | 状態 | 備考 |
|------|------|------|
| Created / Active / Inactive / Disposed の4状態定義 | ✅ | §4.1 状態遷移図・`SlotState` enum で明確 |
| 初期化失敗時の Created → Disposed 遷移 | ✅ | §4.2 シーケンス図あり |
| 破棄中例外のキャッチ継続 | ✅ | §4.1 遷移表・contracts.md 1.3 章で明示 |
| `IMoCapSource` ライフサイクル所有権の分離 | ✅ | §6.2, §6.4 で参照カウント管理を明示 |
| Inactive ⇄ Active の遷移 API | ⚠️ | [A-2] と同件。将来機能だが API 名未定義 |
| フォールバック状態からの回復挙動 | ❌ | 後述 [L-1] |

### 指摘事項

**[L-1] フォールバック状態からの回復挙動が全面的に未定義**

Req 13.5 および contracts.md 1.8 章の「フォールバック状態からの回復方法は tasks フェーズで確定」という記述のとおり、本 design では未定義である。ただし `FallbackBehavior.HoldLastPose` 中に正常なフレームが再び届いた場合の挙動が tasks フェーズ以降に持ち越されていることを、Tasks フェーズ開始時に必ず確認すること。motion-pipeline との境界が関係するため、wave B との合意が必要。

---

## テスト容易性

| 観点 | 状態 | 備考 |
|------|------|------|
| EditMode テスト用 asmdef 定義 | ✅ | §9.3 にファイル一覧まで定義 |
| PlayMode テスト用 asmdef 定義 | ✅ | §9.4 |
| RegistryLocator.ResetForTest の使用方法 | ✅ | §10.3 に NUnit SetUp/TearDown パターンのコード例あり |
| EditMode での [RuntimeInitializeOnLoadMethod] 未実行の対処 | ✅ | §10.3 の注意点に明示 |
| テスト用 Mock の配置場所 | ✅ | §9.3 の Mocks/ ディレクトリで明示 |
| SlotManager の EditMode テスト戦略 | ⚠️ | 後述 [TE-1] |

### 指摘事項

**[TE-1] `SlotManager.AddSlotAsync()` の EditMode テスト戦略が不完全**

§10.1 の `SlotManagerTests` 検証内容に「追加・削除・重複エラー」「状態遷移通知」が列挙されているが、`SlotManager` は UniTask を使用する非同期 API である。EditMode テストでは UniTask の await サポートに注意が必要 (`[UnityTest]` コルーチンでは UniTask を直接 await できない)。`UniTask.ToCoroutine()` または `UniTask.WaitUntil()` のラッパーを使用するか、NUnit の `async Task` テストメソッドに `UniTask.ToTask()` で変換するかを Tasks フェーズで方針決定すること。

---

## 未解決事項 (Open Issues)

design.md §11.3 で明示されている Wave B との合意事項に加え、以下を追加で整理した。

| ID | 事項 | 重要度 | 対処タイミング |
|----|------|:---:|--------------|
| OI-1 | `Descriptor.Equals()` + `GetHashCode()` の実装 (参照共有キー判定に必須) | 高 | Tasks フェーズで実装タスク化 |
| OI-2 | `RegistryLocator` 遅延初期化の `volatile` / `Interlocked` 対応 | 中 | Tasks フェーズで検討 |
| OI-3 | `DefaultSlotErrorChannel` の `Subject.Synchronize()` 適用 | 中 | 実装タスクに明記 |
| OI-4 | `RegistryConflict` の ErrorChannel 発行経路の一本化 | 中 | Tasks フェーズで確定 |
| OI-5 | `SlotRegistry` の内部/外部公開スコープの明記 | 低 | Tasks フェーズで確定 |
| OI-6 | `ApplyFailure` における slot-core 担当範囲の motion-pipeline との境界確定 | 中 | Wave B との合意 |
| OI-7 | フォールバック回復挙動 (Req 13.5) の詳細設計 | 中 | motion-pipeline Wave B 合意後 |
| OI-8 | `SlotManager` Inactive API 名の仮置き (将来機能) | 低 | Tasks フェーズで必要なら定義 |
| OI-9 | UniTask EditMode テストのラッパー戦略決定 | 中 | Tasks フェーズ |
| OI-10 | contracts.md 1.6 章の RegistryLocator 骨格を design.md §3.7 に合わせて更新 | 低 | 任意 (Wave B 参照前に実施推奨) |

---

## 推奨される修正 (あれば)

1. **必須**: `AvatarProviderDescriptor` / `MoCapSourceDescriptor` に `IEquatable<T>` + `GetHashCode()` の実装仕様を design.md に追記する (OI-1)。
2. **推奨**: `RegistryConflict` の ErrorChannel 発行経路を一本化し、Registry 実装が呼び出し元に例外を返す設計か、内部で発行する設計かを明記する (OI-4)。
3. **推奨**: `DefaultSlotErrorChannel._subject` に `Subject.Synchronize()` を適用する方針を §4.3 のコードに反映する (OI-3)。
4. **推奨**: contracts.md 1.6 章の `RegistryLocator` 骨格を design.md §3.7 の最終版に同期する (OI-10)。設計ドキュメントの二重管理は Wave B エージェントの混乱を招く。
5. **任意**: `SlotRegistry` を非公開 (internal) クラスとして明記し、シグネチャを省略する旨を一行追記する (OI-5)。

---

## Tasks フェーズへの引き継ぎ事項

1. **`Descriptor.GetHashCode()` 実装タスク**: `IMoCapSourceRegistry` の参照共有は Descriptor の等価判定に依存する。`MoCapSourceDescriptor.GetHashCode()` を正しく実装しないと `Dictionary` / `HashSet` ベースの参照カウント実装が壊れる。Tasks リストの最優先アイテムとして追加すること。

2. **`RegistryConflict` ErrorChannel 発行の実装方針決定**: Registry の `Register()` が `RegistryConflictException` をスローする際に `ISlotErrorChannel` にも発行するかどうかを Tasks フェーズで決定し、実装タスクを作成すること。

3. **UniTask EditMode テスト戦略の決定**: `SlotManager` の非同期 API をテストする際の UniTask ↔ NUnit 橋渡し方法を選択し、テストテンプレートを Mocks/ と並べて用意すること。

4. **フォールバック回復挙動の設計**: Req 13.5 および §11.3 のとおり未定義。motion-pipeline Wave B が確定した後に slot-core Tasks フェーズで設計を追記すること。

5. **`RegistryLocator` static フィールドのメモリ可視性対応**: `volatile` キーワードまたは `Interlocked.CompareExchange` のどちらを採用するか Tasks フェーズで決定し、`DefaultProviderRegistry` / `DefaultMoCapSourceRegistry` のインスタンス化コードとともにタスク化すること。
