# Design Validation Report — avatar-provider-builtin

> **検証日時**: 2026-04-15
> **検証対象**: `.kiro/specs/avatar-provider-builtin/design.md`
> **参照文書**: `contracts.md`、`slot-core/design.md`、`requirements.md`
> **総合評価**: **条件付き承認 (CONDITIONAL GO)**

---

## 総合サマリ

design.md は全体的に高品質であり、主要な設計判断が明確に記述されている。ただし後述する **Critical 1 件・Major 3 件** の問題を修正または明確化したうえでタスク生成フェーズへ進むことを推奨する。

---

## 1. Requirements Traceability

### 判定: PASS（軽微な補足欠落あり）

| Req | タイトル | design.md 対応箇所 | 判定 |
|:---:|---------|-----------------|:---:|
| Req 1 | BuiltinAvatarProvider の IAvatarProvider 実装・Registry 登録 | §1、§2、§3、§7 | ✅ |
| Req 2 | BuiltinAvatarProviderConfig 型定義と Prefab 参照 | §3、§4 | ✅ |
| Req 3 | Scene へのアバターインスタンス化 | §5 (RequestAvatar) | ✅ |
| Req 4 | アバターのライフサイクル管理 (1 Slot 1 インスタンス原則) | §5 (ReleaseAvatar、Dispose) | ✅ |
| Req 5 | Slot との紐付け (IProviderRegistry 経由) | §2 解決フロー | ✅ |
| Req 6 | 非同期 API の拡張余地 | §5 (RequestAvatarAsync)、§9 | ✅ |
| Req 7 | アセンブリ・名前空間境界および Factory 登録方式 | §7、§10 | ✅ |
| Req 8 | BuiltinAvatarProviderFactory の責務とキャスト処理 | §6 | ✅ |
| Req 9 | テスト戦略 (EditMode / PlayMode 両系統) | §11 | ✅ |

**補足**: Req 4 AC 4「管理外 GameObject のログ記録・非破棄」および AC 5「Dispose 後 RequestAvatar で ObjectDisposedException」については §5 で対応しているが、**Dispose 後の ReleaseAvatar 呼び出し時の挙動**（Req 4 AC 3 との組み合わせ）が設計上未記述。

---

## 2. Contract Compliance

### 判定: PASS（1 件の潜在的不整合あり）

#### contracts.md 1.4 章 (IProviderRegistry / IAvatarProviderFactory)

| 確認項目 | 判定 | 備考 |
|--------|:---:|------|
| `IAvatarProviderFactory.Create(ProviderConfigBase config)` シグネチャ | ✅ | §3・§6 で準拠 |
| `Register(string, IAvatarProviderFactory)` 呼び出し形式 | ✅ | §7 で `RegistryLocator.ProviderRegistry.Register(BuiltinProviderTypeId, new BuiltinAvatarProviderFactory())` と記述 |
| 競合時 `RegistryConflictException` スロー (上書き禁止) | ✅ | §11 テストケース `Register_DuplicateTypeId_ThrowsRegistryConflictException` で確認 |

#### contracts.md 1.5 章 (ProviderConfigBase 型階層)

| 確認項目 | 判定 | 備考 |
|--------|:---:|------|
| `BuiltinAvatarProviderConfig : ProviderConfigBase` 継承 | ✅ | §3・§4 で明示 |
| `ScriptableObject.CreateInstance` によるランタイム動的生成 (シナリオ Y) 対応 | ✅ | §4 で明示 |
| `avatarPrefab` の `public` フィールドによるシナリオ X/Y 両対応 | ✅ | §4 フィールド仕様表で明示 |

#### contracts.md 1.7 章 (ISlotErrorChannel)

| 確認項目 | 判定 | 備考 |
|--------|:---:|------|
| `ISlotErrorChannel.Publish(SlotError)` 呼び出し | ✅ | contracts.md 骨格には `Publish()` 記載なし、しかし **slot-core/design.md §3.8** で `void Publish(SlotError error)` が最終確定済みであり整合する |
| `SlotError(slotId, category, ex, DateTime.UtcNow)` コンストラクタ | ✅ | §5・§6 コード例で準拠 |
| `SlotErrorCategory.InitFailure` の使用 | ✅ | §8 InitFailure 発生パターン表で網羅 |

#### contracts.md 3 章 (IAvatarProvider 最終シグネチャ)

| 確認項目 | 判定 | 備考 |
|--------|:---:|------|
| `string ProviderType { get; }` | ✅ | §3 公開 API に記載 |
| `GameObject RequestAvatar(ProviderConfigBase config)` | ✅ | §3・§5 で準拠 |
| `UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken)` | ✅ | §3・§5 で準拠 |
| `void ReleaseAvatar(GameObject avatar)` | ✅ | §3・§5 で準拠 |
| `IDisposable` 実装 (`Dispose()`) | ✅ | §3・§5 で準拠 |

---

## 3. BuiltinAvatarProviderConfig 設計

### 判定: PASS

- シナリオ X (SO アセット経由) は `[CreateAssetMenu]` 属性付与で対応 — §4 で明確
- シナリオ Y (`ScriptableObject.CreateInstance` によるランタイム動的生成) は §4 でサンプルコード付きで明確
- `avatarPrefab` フィールドの型 (`GameObject`) および可視性 (`public`) が適切で、Req 2 AC 2 に準拠
- Factory が両シナリオの Config を同一キャストロジックで透過的に処理できる設計根拠が §4 末尾で明示されている

---

## 4. 同期/非同期 API 実装

### 判定: PASS

- `RequestAvatar` (同期版) は `Object.Instantiate` を直接呼び出す設計方針が §5 で明確
- `RequestAvatarAsync` は `UniTask.FromResult(RequestAvatar(config))` による即時完了ラップ — §5 で擬似コード付きで明確
- `UniTask.FromResult` ラップは contracts.md 3.1 章の「同期 Provider は同期完了の UniTask を返してよい」方針と整合

**軽微な補足欠落**: `RequestAvatarAsync` において CancellationToken を `cancellationToken.ThrowIfCancellationRequested()` でチェックする順序が同期版 `RequestAvatar` より前にあるが、`RequestAvatar` 内部で `ThrowIfDisposed()` を呼ぶ順序との組み合わせで例外が Dispose 後にどのような順序で発生するかが完全には明示されていない（軽微）。

---

## 5. Factory キャストロジック — **Major Issue #1**

### 判定: CONDITIONAL PASS（要明確化）

#### 合格項目

- `config as BuiltinAvatarProviderConfig` キャスト → null 時 `ArgumentException` スロー、エラーチャネル発行のロジックが §6 で明確
- null config 引数 (config 自体が null) の場合: キャスト結果が null となるため同一フローで `ArgumentException` がスローされる（暗黙的に対応）
- ステートレス設計が §6 で明示

#### 問題点

**[Major #1] `BuiltinAvatarProvider.RequestAvatar()` における config パラメータの二重性が未解消**

§3 の公開 API で `RequestAvatar(ProviderConfigBase config)` が定義されており、§5 の内部状態として `_config: BuiltinAvatarProviderConfig` フィールドも保持されている。コンストラクタで `config` を受け取りフィールドに格納しているにもかかわらず、`RequestAvatar(ProviderConfigBase config)` にも config 引数がある。

- `ResolveConfig(config)` というヘルパーメソッドが §5 擬似コードで呼ばれているが、**このメソッドの実装定義が design.md 内に存在しない**
- `ResolveConfig` が引数 `config` を使うのか、フィールド `_config` を優先するのか、あるいは null 時にフィールドにフォールバックするのかが不明
- contracts.md 3.1 章の `IAvatarProvider` シグネチャでは `RequestAvatar(ProviderConfigBase config)` に config 引数が存在するが、BuiltinAvatarProvider はコンストラクタ時点で Prefab 情報を持っているため、呼び出し時 config 引数が冗長または意図不明

**推奨対応**: `ResolveConfig()` の実装方針（引数 config を優先する / `_config` フィールドを使用する / 引数が null のときフィールドにフォールバック等）を design.md に明記すること。

---

## 6. 1 Slot 1 インスタンス原則

### 判定: PASS

- 参照共有非採用の設計根拠が §2「1 Slot 1 インスタンス原則」および §5「1 Slot 1 インスタンス原則」の両箇所で明確に記述されている
- `IMoCapSource` との比較を用いた差別化説明が適切
- `Instantiate` / `Destroy` のライフサイクル管理フローが §5 (RequestAvatar / ReleaseAvatar / Dispose) で明確
- `SlotManager` がライフサイクルを所有する責務分担が §2 解決フロー・§8 で明記

---

## 7. Factory 自動登録

### 判定: PASS（1 件の軽微な指摘あり）

- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` による Player/Editor Play Mode での自己登録が §7 で擬似コード付きで明確
- `[UnityEditor.InitializeOnLoadMethod]` による Editor 起動時登録が `#if UNITY_EDITOR` ガード内に配置されていることが §7 で明確
- `BuiltinProviderTypeId = "Builtin"` 定数が設けられておりハードコーディングを排除

**軽微な指摘**: Req 7 AC 5 および AC 6 では、Domain Reload OFF 環境での二重登録防止として `RegistryLocator.ResetForTest()` を `SubsystemRegistration` タイミングで呼び出す設計が requirements に記述されているが、design.md §7 には Domain Reload OFF 対応への言及がない。contracts.md 1.4 章・1.6 章で `RegistryLocator.ResetForTest()` の設計が確定されているため依存しているが、本 Spec 視点での記述追加が望ましい。

---

## 8. Addressable 拡張余地

### 判定: PASS

§9「Addressable Provider 拡張余地」が充実しており、以下の 4 点で変更不要であることの設計根拠が明確：

1. `IAvatarProvider` 抽象への完全準拠
2. `RequestAvatarAsync` が将来用フックとして機能
3. Registry/Factory の独立性（typeId の独立）
4. Config 型の独立性（共に `ProviderConfigBase` を継承するが互いに非依存）

同期/非同期 API の使い分け方針を表形式で示している点も適切。

---

## 9. ErrorChannel 連携 — **Major Issue #2**

### 判定: CONDITIONAL PASS（要修正）

#### 合格項目

- §8「InitFailure 発生パターン」表が 4 パターンを網羅しており発生経路が明確

#### 問題点

**[Major #2] `ThrowIfPrefabNull` における InitFailure 発行が擬似コードと仕様表で不整合**

§5 の `RequestAvatar` 擬似コードでは:
```
ThrowIfPrefabNull(builtinConfig.avatarPrefab);  // null Prefab ガード
try { Object.Instantiate(...) ... }
catch (Exception ex) { _errorChannel.Publish(...); throw; }
```

`ThrowIfPrefabNull` は try ブロックの外に置かれているため、**null Prefab 時には catch ブロックに入らず `ISlotErrorChannel` への発行が行われない**。しかし §8 のエラーパターン表には「`avatarPrefab` が null の状態で `RequestAvatar()` が呼ばれた → `ISlotErrorChannel` に `InitFailure` を発行後、`InvalidOperationException` をスロー」と記述されており、**矛盾**している。

`ThrowIfPrefabNull` は try ブロック内に入れるか、またはメソッド内で明示的にエラーチャネルを呼び出す設計を擬似コードに反映する必要がある。

---

## 10. Critical Issue — `ISlotErrorChannel.Publish()` インターフェース整合性

### 判定: CONDITIONAL PASS（確認・補足記述推奨）

**[Critical #1] contracts.md 1.7 章の `ISlotErrorChannel` 骨格に `Publish()` メソッドが記載されていない**

`contracts.md` 1.7 章の `ISlotErrorChannel` インターフェース骨格は以下のみを定義している：
```csharp
public interface ISlotErrorChannel
{
    IObservable<SlotError> Errors { get; }
}
```

一方 design.md §5・§6 では `_errorChannel.Publish(new SlotError(...))` を呼び出している。

**実際は** `slot-core/design.md §3.8` で `void Publish(SlotError error)` が最終確定されており設計として正しいが、contracts.md との不整合がある。これは contracts.md が design フェーズ後に更新されるべき「受け口」文書であり、slot-core design フェーズで確定したシグネチャが contracts.md にまだ反映されていない状態と解釈できる。

**推奨対応**: design.md の §8 または §2 境界表に「`ISlotErrorChannel` の最終シグネチャは slot-core/design.md §3.8 を参照」というノートを追加し、contracts.md との参照関係を明確にすること。または contracts.md の更新をトラッキングする Open Issue として記録すること。

---

## 11. Open Issues (design.md 記載なし)

design.md に Open Issues セクションが存在しない。以下の事項を明示的に Open Issues として記録することを推奨する：

| # | 事項 | 優先度 |
|:---:|------|:---:|
| OI-1 | `ResolveConfig()` ヘルパーメソッドの実装方針（引数 config vs フィールド `_config` の使い分け）| High |
| OI-2 | `ThrowIfPrefabNull` と `ISlotErrorChannel.Publish()` の try-catch 整合性 | High |
| OI-3 | `RequestAvatar(ProviderConfigBase config)` の config 引数の設計意図（コンストラクタ引数の `_config` との関係）| Medium |
| OI-4 | contracts.md 1.7 章への `Publish()` メソッド追記（契約更新） | Low |
| OI-5 | Domain Reload OFF 環境での二重登録対応の明示（§7 への補足） | Low |

---

## 検証結果サマリ

| 観点 | 判定 | 重大度 |
|------|:---:|:---:|
| Requirements Traceability | PASS | — |
| Contract Compliance | PASS | — |
| BuiltinAvatarProviderConfig 設計 | PASS | — |
| 同期/非同期 API 実装 | PASS | — |
| Factory キャストロジック | CONDITIONAL | **Major** |
| 1 Slot 1 インスタンス原則 | PASS | — |
| Factory 自動登録 | PASS | — |
| Addressable 拡張余地 | PASS | — |
| ErrorChannel 連携 | CONDITIONAL | **Major** |
| `Publish()` インターフェース整合性 | CONDITIONAL | **Critical** |
| Open Issues | 補足推奨 | Low |

### 要修正事項 (タスク生成前に対応推奨)

1. **[Critical]** contracts.md と slot-core/design.md の `ISlotErrorChannel.Publish()` 参照関係を design.md に明記
2. **[Major #1]** `ResolveConfig()` ヘルパーメソッドの定義と `RequestAvatar` の config 引数設計意図を design.md §5 に追記
3. **[Major #2]** `ThrowIfPrefabNull` を try ブロック内に移動するか、null Prefab 時の `_errorChannel.Publish()` 呼び出しを擬似コードに明示して §8 との整合性を確保

---

*本レポートは `.kiro/specs/avatar-provider-builtin/validation-design.md` として手動検証により生成された。*
