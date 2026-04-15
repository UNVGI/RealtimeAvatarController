# Brief: slot-core

## Spec 責務
Slot 概念を中核とするデータモデル・ライフサイクル管理 API を提供し、Spec 間で共有する公開 IF 群を定義する。

## 依存
`project-foundation`

## 位置付け (重要)
**Wave 1 の先行波 Spec**。本 Spec の requirements エージェントは、自身の requirements.md 生成に加え、`_shared/contracts.md` の 1〜5 章を埋める責務を持つ。これが完了するまで他 5 Spec は Wave 2 で待機する。

## スコープ

### 実装する
- Slot データモデル (Descriptor ベース POCO: AvatarProviderDescriptor / MoCapSourceDescriptor / FacialControllerDescriptor? / LipSyncSourceDescriptor? + Weight / slotId / displayName)
- SlotRegistry / SlotManager 相当の動的追加・削除 API
- Slot 設定のシリアライズ可能な構造 (POCO / ScriptableObject / JSON 許容; ScriptableObject は任意)
- Slot ライフサイクル (生成・破棄; IMoCapSource の所有権は MoCapSourceRegistry に委譲)
- ProviderRegistry / SourceRegistry (typeId → Factory 解決、利用可能候補列挙)
- MoCapSourceRegistry (参照共有 / 参照カウントベース解放)
- **RegistryLocator** (dig ラウンド 3 確定):
  - `IProviderRegistry` / `IMoCapSourceRegistry` への静的アクセスポイント
  - Editor / Runtime 共有の同一インスタンスを提供
  - テスト用 `ResetForTest()` / `Override*()` API
  - Domain Reload OFF 対応の `SubsystemRegistration` タイミングでの自動リセット
- **ISlotErrorChannel / SlotError / SlotErrorCategory** (dig ラウンド 3 確定):
  - UniRx ベースの Slot エラー通知チャネル
  - `SlotError` クラス: `SlotId` / `Category` / `Exception` / `Timestamp`
  - `SlotErrorCategory` 列挙体: `VmcReceive` / `InitFailure` / `ApplyFailure` / `RegistryConflict`
  - Debug.LogError 抑制ポリシー (同一 (SlotId, Category) 初回 1F のみ、HashSet で追跡)
- **FallbackBehavior 列挙体** (dig ラウンド 3 確定):
  - `HoldLastPose` (デフォルト) / `TPose` / `Hide`
  - `SlotSettings.fallbackBehavior` フィールドに使用
- **属性ベース自動登録機構** (dig ラウンド 3 確定):
  - `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `[UnityEditor.InitializeOnLoadMethod]`
  - 具象 Factory 側が上記属性で `RegistryLocator` 経由で自己登録するパターンを `slot-core` が提供
  - 利用者の自前 Factory も同じ仕組みで登録可能
- **Config 基底型階層の定義** (dig ラウンド 2 確定):
  - `ProviderConfigBase : ScriptableObject` (アバター Provider Config の抽象基底)
  - `MoCapSourceConfigBase : ScriptableObject` (MoCap ソース Config の抽象基底)
  - `FacialControllerConfigBase : ScriptableObject` (表情制御 Config の抽象基底)
  - `LipSyncSourceConfigBase : ScriptableObject` (リップシンク Config の抽象基底)
  - 具象 Config は各担当 Spec が基底クラスを継承して定義する
  - Factory の `Create` 引数型は各基底クラス型; 具象実装側でキャストして取得
- 以下の抽象インターフェースの定義 (シグネチャ確定は design フェーズ):
  - `IMoCapSource` **Push 型 (UniRx `IObservable<MotionFrame>`)**: `FetchLatestMotion()` は定義しない。`MotionStream` は `OnError` を発行しない
  - `IAvatarProvider`
  - `IFacialController` (受け口のみ)
  - `ILipSyncSource` (受け口のみ)
- **UniRx (`com.neuecc.unirx`) を `RealtimeAvatarController.Core` アセンブリの依存として追加** (R3 は採用しない)
  - OpenUPM の scoped registry 経由で取得; NuGet 依存なし
  - `ObserveOnMainThread()` 等の UniRx 拡張メソッドが利用可能
- **Weight 初期版固定方針** (dig ラウンド 2 確定):
  - `SlotSettings.weight` フィールドは残す (将来の複数ソース混合用フック)
  - 初期版 (1 Slot 1 MoCap source) では `weight` は常に `1.0`
  - `0.0` (skip) と `1.0` (full apply) の二値動作のみが初期版の有効値
  - `0.0 < weight < 1.0` の中間値セマンティクスは将来定義予定

### アーキテクチャ方針 (dig ラウンド 1 反映)

#### Descriptor + Registry + Factory パターン
```
SlotSettings (serializable POCO / ScriptableObject 任意)
├── slotId / displayName
├── weight  ← 初期版は常に 1.0 (フィールドは将来の複数ソース混合用フックとして残す)
├── AvatarProviderDescriptor  { providerTypeId: string, config: ProviderConfigBase }
├── MoCapSourceDescriptor     { sourceTypeId: string,   config: MoCapSourceConfigBase }
├── FacialControllerDescriptor? { controllerTypeId: string, config: FacialControllerConfigBase } (null 許容)
└── LipSyncSourceDescriptor?   { sourceTypeId: string, config: LipSyncSourceConfigBase } (null 許容)

Config 基底型階層 (slot-core が定義):
  ProviderConfigBase : ScriptableObject        ← AvatarProviderDescriptor.Config の型
  MoCapSourceConfigBase : ScriptableObject     ← MoCapSourceDescriptor.Config の型
  FacialControllerConfigBase : ScriptableObject
  LipSyncSourceConfigBase : ScriptableObject
  ※ 具象 Config は担当 Spec が基底を継承して定義 (例: BuiltinAvatarProviderConfig)
  ※ Factory 側は config as BuiltinAvatarProviderConfig でキャストして取得

IProviderRegistry
  - Register(typeId, IAvatarProviderFactory)
  - Resolve(AvatarProviderDescriptor) → IAvatarProvider
  - GetRegisteredTypeIds() → IReadOnlyList<string>

IMoCapSourceRegistry
  - Register(typeId, IMoCapSourceFactory)
  - Resolve(MoCapSourceDescriptor) → IMoCapSource  ※参照共有
  - Release(IMoCapSource)                           ※参照カウントデクリメント
  - GetRegisteredTypeIds() → IReadOnlyList<string>
```

#### IMoCapSource Push モデル (UniRx `com.neuecc.unirx` 採用 / R3 不採用)
```csharp
// using UniRx; が必要 (ObserveOnMainThread() は UniRx の拡張メソッド)
// IObservable<MotionFrame> は System.IObservable<T> — UniRx Subject<T> がこれを実装する
public interface IMoCapSource : IDisposable
{
    string SourceType { get; }
    void Initialize(/* MoCapSourceConfigBase config */);
    IObservable<MotionFrame> MotionStream { get; }  // Push 型; FetchLatestMotion() は廃止
    void Shutdown();
}
// 購読側: source.MotionStream.ObserveOnMainThread().Subscribe(frame => ...)
// マルチキャスト: MotionStream は Publish().RefCount() 等でラップ
// パッケージ: com.neuecc.unirx (OpenUPM scoped registry) / NuGet 依存なし
```

#### MoCap ソース参照共有モデル
- 複数 Slot が同一 `IMoCapSource` インスタンスを参照共有
- 所有権: `MoCapSourceRegistry` が参照カウントで管理
- Slot 破棄時: `SlotManager` は `MoCapSourceRegistry.Release()` を呼び出すのみ
- 旧制約「同一ポートへの複数バインド禁止」は撤回

### スコープ外
- 抽象の具象実装 (それぞれ mocap-vmc / avatar-provider-builtin / 対象外 / 対象外)
- モーション中立表現 (motion-pipeline Spec)
- モーション適用処理 (motion-pipeline Spec)

## 参照必須ドキュメント
- `.kiro/specs/_shared/spec-map.md`
- `.kiro/specs/_shared/contracts.md`

## 契約ドキュメントへの寄与 (Wave 1 責務)
本エージェントは `contracts.md` の以下を埋める:
- 1 章: Slot データモデル
- 2.1 章: `IMoCapSource` シグネチャ
- 3.1 章: `IAvatarProvider` シグネチャ
- 4.1 章: `IFacialController` シグネチャ
- 5.1 章: `ILipSyncSource` シグネチャ

注: 2.2 章 (モーションデータ中立表現) は motion-pipeline エージェントが Wave 2 で埋める。

## 出力物
- `.kiro/specs/slot-core/requirements.md`
- `.kiro/specs/slot-core/spec.json`
- `.kiro/specs/_shared/contracts.md` (編集)

## 実行手順
1. Skill ツールで `kiro:spec-init` を呼び、feature 名 `slot-core` として初期化
2. Skill ツールで `kiro:spec-requirements` を呼び、requirements.md を生成
3. 生成された requirements.md を本 Brief と `spec-map.md` の内容に沿って編集・確定
4. `contracts.md` の 1〜5 章を編集 (2.2 章は除く)

## 言語
Markdown 出力は日本語
