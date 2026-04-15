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
- 以下の抽象インターフェースの定義 (シグネチャ確定は design フェーズ):
  - `IMoCapSource` **Push 型 (UniRx `IObservable<MotionFrame>`)**: `FetchLatestMotion()` は定義しない
  - `IAvatarProvider`
  - `IFacialController` (受け口のみ)
  - `ILipSyncSource` (受け口のみ)
- UniRx を `RealtimeAvatarController.Core` アセンブリの依存として追加

### アーキテクチャ方針 (dig ラウンド 1 反映)

#### Descriptor + Registry + Factory パターン
```
SlotSettings (serializable POCO / ScriptableObject 任意)
├── slotId / displayName / weight
├── AvatarProviderDescriptor  { providerTypeId: string, config: SO or JSON }
├── MoCapSourceDescriptor     { sourceTypeId: string,   config: SO or JSON }
├── FacialControllerDescriptor? (null 許容)
└── LipSyncSourceDescriptor?   (null 許容)

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

#### IMoCapSource Push モデル (UniRx)
```csharp
public interface IMoCapSource : IDisposable
{
    string SourceType { get; }
    void Initialize(/* config */);
    IObservable<MotionFrame> MotionStream { get; }  // Push 型; FetchLatestMotion() は廃止
    void Shutdown();
}
// 購読側: source.MotionStream.ObserveOnMainThread().Subscribe(frame => ...)
// マルチキャスト: MotionStream は Publish().RefCount() 等でラップ
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
