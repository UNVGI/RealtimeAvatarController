# 実装タスク — ui-sample

> **フェーズ**: tasks  
> **言語**: ja  
> **生成日**: 2026-04-15  
> **前提**: design.md (GO 承認済み) / validation-design.md (GO) に基づく

---

## タスク一覧

- [T1] Samples~/UI ディレクトリ構成とアセンブリ定義の整備
- [T2] SlotSettingsEditor — CustomEditor 骨格と OnEnable / RefreshTypeIds
- [T3] SlotSettingsEditor — アバター Provider セクション (typeId ドロップダウン + Config 参照欄)
- [T4] SlotSettingsEditor — MoCap ソースセクション (typeId ドロップダウン + 未割り当て処理 + Config 参照欄)
- [T5] SlotSettingsEditor — Fallback enum ドロップダウン (デフォルト値 HoldLastPose の明示)
- [T6] SlotSettingsEditor — Weight 二値トグル (0.5f 閾値コメント付き)
- [T7] SlotSettingsEditor — Registry 未初期化時の Graceful Degradation (HelpBox + 手入力フォールバック)
- [T8] デモシーン — SlotManagementDemo シーン作成と GameObject 配置
- [T9] デモシーン — SlotManagerBehaviour MonoBehaviour 実装
- [T10] デモシーン — ランタイム Canvas UI スクリプト配線 (SlotManagementPanelUI / SlotListItemUI / SlotDetailPanelUI)
- [T11] デモシーン — 参照共有シナリオ用アセット作成とシーン内配線
- [T12] デモシーン — エラーシミュレーションシナリオの組み込み (ErrorSimulationHelper)
- [T13] デモシーン — Fallback 視覚確認シナリオの組み込み
- [T14] SlotErrorPanel — ErrorChannel 購読 UI 実装
- [T15] package.json samples エントリ登録
- [T16] テスト asmdef と EditMode テスト実装 (任意)
- [T17] テスト asmdef と PlayMode テスト実装 (任意)

---

## T1: Samples~/UI ディレクトリ構成とアセンブリ定義の整備

_Requirements: 6, 7_

### 概要

`Samples~/UI/` 以下のディレクトリ構造を design.md §8.1 に従って作成し、Runtime・Editor の 2 本の asmdef を配置する。

### Leaf タスク

#### T1-1: ディレクトリ骨格の作成

- `Packages/com.realtimeavatarcontroller/Samples~/UI/Runtime/` を作成する
- `Packages/com.realtimeavatarcontroller/Samples~/UI/Editor/` を作成する
- `Packages/com.realtimeavatarcontroller/Samples~/UI/Scenes/` を作成する
- `Packages/com.realtimeavatarcontroller/Samples~/UI/Prefabs/` を作成する

**完了条件**: 上記4ディレクトリが存在すること

#### T1-2: Runtime asmdef の作成

ファイル: `Samples~/UI/Runtime/RealtimeAvatarController.Samples.UI.asmdef`

```json
{
  "name": "RealtimeAvatarController.Samples.UI",
  "rootNamespace": "RealtimeAvatarController.Samples.UI",
  "references": [
    "RealtimeAvatarController.Core",
    "RealtimeAvatarController.Motion",
    "RealtimeAvatarController.MoCap.VMC",
    "RealtimeAvatarController.Avatar.Builtin",
    "UniRx"
  ],
  "includePlatforms": [],
  "autoReferenced": false
}
```

> **UniRx 直接参照の根拠**: `SlotErrorPanel` が `.ObserveOnMainThread()` 拡張メソッドを直接呼び出すため、contracts.md §6.1 例外条項に基づき UniRx を直接参照する。UniTask の直接参照は技術的に不要であるため含めない。

**完了条件**: asmdef が正しい references を持ち、Unity Editor でコンパイルエラーなし

#### T1-3: Editor asmdef の作成

ファイル: `Samples~/UI/Editor/RealtimeAvatarController.Samples.UI.Editor.asmdef`

```json
{
  "name": "RealtimeAvatarController.Samples.UI.Editor",
  "rootNamespace": "RealtimeAvatarController.Samples.UI.Editor",
  "references": [
    "RealtimeAvatarController.Samples.UI",
    "RealtimeAvatarController.Core",
    "RealtimeAvatarController.Core.Editor"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

**完了条件**: asmdef が `includePlatforms: ["Editor"]` であり、UniRx/UniTask の直接参照を持たないこと

---

## T2: SlotSettingsEditor — CustomEditor 骨格と OnEnable / RefreshTypeIds

_Requirements: 2, 3_

### 概要

`[CustomEditor(typeof(SlotSettings))]` を付与した `SlotSettingsEditor` クラスの骨格を実装する。`OnEnable()` での SerializedProperty キャッシュと `RefreshTypeIds()` を含む。

### Leaf タスク

#### T2-1: SlotSettingsEditor クラス骨格の作成

ファイル: `Samples~/UI/Editor/SlotSettingsEditor.cs`

- namespace: `RealtimeAvatarController.Samples.UI.Editor`
- `[CustomEditor(typeof(SlotSettings))]` 属性付与
- `UnityEditor.Editor` を継承
- SerializedProperty フィールドのキャッシュ変数を宣言:
  - `_slotIdProp`, `_displayNameProp`, `_weightProp`, `_fallbackBehaviorProp`
  - `_avatarProviderDescriptorProp`, `_moCapSourceDescriptorProp`
- Registry キャッシュ変数を宣言:
  - `_providerTypeIds = System.Array.Empty<string>()`
  - `_moCapSourceTypeIds = System.Array.Empty<string>()`

**完了条件**: クラスが Unity Editor でコンパイルエラーなし

#### T2-2: OnEnable() の実装

`OnEnable()` で以下を実装する:

1. `serializedObject.FindProperty()` で全 SerializedProperty をキャッシュする
   - `"slotId"`, `"displayName"`, `"weight"`, `"fallbackBehavior"`
   - `"avatarProviderDescriptor"`, `"moCapSourceDescriptor"`
2. `RefreshTypeIds()` を呼び出す

**完了条件**: Inspector を開いた際に OnEnable が実行され、プロパティがキャッシュされること

#### T2-3: RefreshTypeIds() の実装

```csharp
private void RefreshTypeIds()
{
    try
    {
        _providerTypeIds    = RegistryLocator.ProviderRegistry
                                             .GetRegisteredTypeIds()
                                             .ToArray();
        _moCapSourceTypeIds = RegistryLocator.MoCapSourceRegistry
                                             .GetRegisteredTypeIds()
                                             .ToArray();
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning($"[SlotSettingsEditor] Registry 候補取得失敗: {ex.Message}");
        _providerTypeIds    = System.Array.Empty<string>();
        _moCapSourceTypeIds = System.Array.Empty<string>();
    }
}
```

**完了条件**: Registry 未初期化時に例外がスローされず、空配列が設定されること

#### T2-4: OnInspectorGUI() 骨格の実装

```csharp
public override void OnInspectorGUI()
{
    serializedObject.Update();

    EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
    EditorGUILayout.PropertyField(_slotIdProp,      new GUIContent("Slot ID"));
    EditorGUILayout.PropertyField(_displayNameProp, new GUIContent("表示名"));

    EditorGUILayout.Space();
    DrawAvatarProviderSection();

    EditorGUILayout.Space();
    DrawMoCapSourceSection();

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("モーション設定", EditorStyles.boldLabel);
    DrawWeightToggle();

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("フォールバック設定", EditorStyles.boldLabel);
    EditorGUILayout.PropertyField(_fallbackBehaviorProp, new GUIContent("フォールバック挙動"));

    serializedObject.ApplyModifiedProperties();
}
```

**完了条件**: Inspector で SlotSettings アセットを選択した際に、上記レイアウトが表示されること

---

## T3: SlotSettingsEditor — アバター Provider セクション

_Requirements: 2_

### 概要

`DrawAvatarProviderSection()` を実装する。`RegistryLocator.ProviderRegistry.GetRegisteredTypeIds()` の結果を `EditorGUILayout.Popup()` で表示し、`ProviderConfigBase` 派生 SO のドラッグ&ドロップ参照欄を提供する。

### Leaf タスク

#### T3-1: DrawAvatarProviderSection() の実装

design.md §4.2 のコードスニペットに従い実装する:

1. セクションラベル「アバター Provider」を表示
2. `_avatarProviderDescriptorProp.FindPropertyRelative("ProviderTypeId")` で typeIdProp を取得
3. `_avatarProviderDescriptorProp.FindPropertyRelative("Config")` で configProp を取得
4. `_providerTypeIds.Length == 0` の場合: HelpBox (Warning) を表示し、手入力フィールドにフォールバック
5. それ以外: 現在の typeId から `currentIndex` を計算し `EditorGUILayout.Popup()` でドロップダウン表示
6. `EditorGUILayout.PropertyField(configProp, new GUIContent("Provider Config (SO)"))` で Config 参照欄を表示
7. 「候補を更新」ボタンで `RefreshTypeIds()` を呼び出す

**完了条件**:
- Registry に "Builtin" が登録されている場合、ドロップダウンに "Builtin" が表示されること
- Registry 未登録の場合、HelpBox と手入力フィールドが表示されること
- Config 欄に `ProviderConfigBase` 派生 SO をドラッグ&ドロップして参照設定できること

---

## T4: SlotSettingsEditor — MoCap ソースセクション

_Requirements: 3_

### 概要

`DrawMoCapSourceSection()` を実装する。先頭に「(未割り当て)」選択肢を追加し、`Release()` 呼び出しは SlotManager 経由のみ (Editor 直接呼び出し不要) の方針を厳守する。

### Leaf タスク

#### T4-1: DrawMoCapSourceSection() の実装

design.md §4.2 の `DrawMoCapSourceSection` コードスニペットに従い実装する:

1. セクションラベル「MoCap ソース」を表示
2. `_moCapSourceDescriptorProp.FindPropertyRelative("SourceTypeId")` / `"Config"` を取得
3. `_moCapSourceTypeIds.Length > 0` の場合: 先頭に `"(未割り当て)"` を追加した options 配列を構築
4. `options == null` の場合: HelpBox (Warning) + 手入力フィールドにフォールバック
5. 現在 typeId が空文字列または null → `currentIndex = 0` (未割り当て)
6. それ以外: `Array.IndexOf(_moCapSourceTypeIds, typeIdProp.stringValue) + 1` (オフセット +1)
7. `newIndex == 0` の場合: `typeIdProp.stringValue = string.Empty` に設定
8. `newIndex > 0` の場合: `_moCapSourceTypeIds[newIndex - 1]` を設定
9. `EditorGUILayout.PropertyField(configProp, ...)` で Config 参照欄を表示
10. 「候補を更新」ボタンで `RefreshTypeIds()` を呼び出す

> **重要**: `IMoCapSourceRegistry.Release()` は `SlotSettingsEditor` からは呼び出さない。Release は `SlotManager.RemoveSlot()` 経由のみで行う。`SlotSettingsEditor` はデータ編集ツールであり `IMoCapSource` インスタンスを直接保持しない。

**完了条件**:
- ドロップダウン先頭に「(未割り当て)」が常に表示されること
- 「(未割り当て)」を選択すると `SourceTypeId` が空文字列になること
- Registry に "VMC" が登録されている場合、ドロップダウンに "VMC" が表示されること
- Config 欄に `MoCapSourceConfigBase` 派生 SO をドラッグ&ドロップして参照設定できること

---

## T5: SlotSettingsEditor — Fallback enum ドロップダウン

_Requirements: 10_

### 概要

`FallbackBehavior` enum を `EditorGUILayout.PropertyField()` で自動描画する。validation-design.md OI-7 の指摘に対応し、デフォルト値 `HoldLastPose` の表示を明示するコメントを実装に含める。

### Leaf タスク

#### T5-1: Fallback ドロップダウン描画の確認と FallbackBehavior 表示名の検証

`OnInspectorGUI()` 内の以下のコードが正しく機能することを確認する:

```csharp
EditorGUILayout.LabelField("フォールバック設定", EditorStyles.boldLabel);
EditorGUILayout.PropertyField(_fallbackBehaviorProp, new GUIContent("フォールバック挙動"));
// デフォルト値: HoldLastPose (contracts.md §1.8 / SlotSettings 初期値に従う)
// 選択肢: HoldLastPose (最後のポーズを保持) / TPose (T ポーズに戻す) / Hide (アバターを非表示)
```

`FallbackBehavior` enum の表示名テーブル (design.md §4.3 準拠):

| 列挙値 | 表示文字列 |
|-------|----------|
| `HoldLastPose` | Hold Last Pose (最後のポーズを保持) |
| `TPose` | T-Pose (T ポーズに戻す) |
| `Hide` | Hide (アバターを非表示) |

`SlotSettings` が `FallbackBehavior fallbackBehavior = FallbackBehavior.HoldLastPose;` をデフォルト値として持つことを確認し、Inspector で新規 `SlotSettings` を作成した際にデフォルト値が `HoldLastPose` であることを確認する。

**完了条件**:
- Inspector に `FallbackBehavior` のドロップダウンが表示されること
- 3 つの選択肢 (HoldLastPose / TPose / Hide) がすべて表示されること
- 新規作成時のデフォルト値が `HoldLastPose` であること

---

## T6: SlotSettingsEditor — Weight 二値トグル

_Requirements: 4_

### 概要

`DrawWeightToggle()` を実装する。validation-design.md OI-8 の指摘に対応し、`0.5f` 閾値の意図を説明するコメントを実装に含める。

### Leaf タスク

#### T6-1: DrawWeightToggle() の実装

```csharp
private void DrawWeightToggle()
{
    // 閾値 0.5f: 初期版は 0.0 (skip) / 1.0 (full apply) の二値のみ有効。
    // 0.5f 境界は「どちらに近いか」で二値に丸める変換用閾値であり、
    // 中間値セマンティクスは将来の複数ソース混合シナリオで定義予定。
    bool isActive = _weightProp.floatValue >= 0.5f;
    bool newActive = EditorGUILayout.Toggle("Weight 有効 (1.0 / 0.0)", isActive);
    if (newActive != isActive)
        _weightProp.floatValue = newActive ? 1.0f : 0.0f;
    EditorGUILayout.LabelField($"現在の Weight: {_weightProp.floatValue:F1}", EditorStyles.miniLabel);
}
```

**完了条件**:
- トグル ON → `weight = 1.0f` に設定されること
- トグル OFF → `weight = 0.0f` に設定されること
- 現在の weight 値が「現在の Weight: X.X」ラベルに表示されること
- 0.5f 閾値にコメントが付与されること

---

## T7: SlotSettingsEditor — Registry 未初期化時の Graceful Degradation

_Requirements: 2, 3_

### 概要

Registry が未初期化の場合の HelpBox 表示と手入力フォールバックを T3・T4 の実装に組み込む。design.md §5.2 のフォールバック表示テーブルに準拠する。

### Leaf タスク

#### T7-1: Provider Registry 未初期化時の HelpBox 実装

T3-1 の `DrawAvatarProviderSection()` 内で以下を確認・実装する:

```csharp
if (_providerTypeIds.Length == 0)
{
    EditorGUILayout.HelpBox(
        "Registry に Provider が未登録です。\n[InitializeOnLoadMethod] が実行されているか確認してください。",
        MessageType.Warning);
    EditorGUILayout.PropertyField(typeIdProp, new GUIContent("Provider Type ID (手入力)"));
}
```

**完了条件**: Registry が空の状態で Inspector を開いた際に Warning HelpBox と手入力フィールドが表示されること

#### T7-2: MoCap Source Registry 未初期化時の HelpBox 実装

T4-1 の `DrawMoCapSourceSection()` 内で以下を確認・実装する:

```csharp
if (options == null)
{
    EditorGUILayout.HelpBox(
        "Registry に MoCapSource が未登録です。\n[InitializeOnLoadMethod] が実行されているか確認してください。",
        MessageType.Warning);
    EditorGUILayout.PropertyField(typeIdProp, new GUIContent("Source Type ID (手入力)"));
}
```

**完了条件**: MoCap Source Registry が空の状態で Inspector を開いた際に Warning HelpBox と手入力フィールドが表示されること

#### T7-3: try-catch による null Registry 対応の確認

T2-3 の `RefreshTypeIds()` の `try-catch` が `Registry` インスタンスが null の場合も含めて例外を捕捉することを確認する。`MessageType.Warning` を表示する実装であることを確認する。

**完了条件**: Registry 完全未初期化状態でも `NullReferenceException` が UI に伝播しないこと

---

## T8: デモシーン — SlotManagementDemo シーン作成と GameObject 配置

_Requirements: 5, 6_

### 概要

`Samples~/UI/Scenes/SlotManagementDemo.unity` シーンを作成し、design.md §3.2 の GameObject 構成に従って階層を配置する。

### Leaf タスク

#### T8-1: SlotManagementDemo シーンの新規作成

Unity Editor でシーンを新規作成し `Samples~/UI/Scenes/SlotManagementDemo.unity` として保存する。

**完了条件**: シーンファイルが所定のパスに存在すること

#### T8-2: SlotManager GameObject の配置

シーンルートに `[SlotManager]` という名前の空 GameObject を作成する。`SlotManagerBehaviour` スクリプト (T9 で実装) をアタッチするためのプレースホルダーとして配置する。

**完了条件**: `[SlotManager]` GameObject がシーンに存在すること

#### T8-3: Canvas 階層の配置

Screen Space - Overlay Canvas を配置し、以下の子 GameObject を作成する:

```
Canvas (Screen Space - Overlay)
├── SlotManagementPanel
│   ├── Header (Text: "Slot 管理")
│   ├── AddSlotButton (Button: "Slot を追加")
│   └── SlotListScrollView (ScrollRect)
├── SlotDetailPanel
│   ├── DisplayNameField (InputField: 表示名)
│   ├── WeightToggle (Toggle: Weight 有効/無効)
│   ├── FallbackDropdown (Dropdown: FallbackBehavior)
│   └── DeleteSlotButton (Button: "Slot を削除")
└── SlotErrorPanel
    ├── ErrorLogScrollView (ScrollRect)
    └── ClearErrorsButton (Button: "エラーをクリア")
```

`EventSystem` も配置する。

**完了条件**: Canvas 階層が上記構成で存在し、UGUI コンポーネントが正しく設定されていること

#### T8-4: AvatarArea と Lighting の配置

```
AvatarArea (空 GameObject)
├── AvatarSlot_01 (空 Transform: Slot 1 のアバター配置位置)
├── AvatarSlot_02 (空 Transform: Slot 2 のアバター配置位置)
└── AvatarSlot_03 (空 Transform: Slot 3 のアバター配置位置)
Lighting (Directional Light)
```

**完了条件**: AvatarArea と 3 つの AvatarSlot が適切な位置に配置されていること

---

## T9: デモシーン — SlotManagerBehaviour MonoBehaviour 実装

_Requirements: 1, 5_

### 概要

デモシーン用の `SlotManagerBehaviour` MonoBehaviour を実装する。design.md §3.2 のコードスニペットに準拠する。

### Leaf タスク

#### T9-1: SlotManagerBehaviour.cs の作成

ファイル: `Samples~/UI/Runtime/SlotManagerBehaviour.cs`

```csharp
namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用 SlotManager ラッパー MonoBehaviour。
    /// Awake で SlotManager を初期化し、OnDestroy で Dispose する。
    /// </summary>
    public class SlotManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private SlotSettings[] initialSlots; // Editor で事前設定する初期 Slot

        public SlotManager SlotManager { get; private set; }

        private void Awake()
        {
            SlotManager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);

            // initialSlots が設定されている場合は起動時に追加
            if (initialSlots != null)
            {
                foreach (var settings in initialSlots)
                {
                    if (settings != null)
                        _ = SlotManager.AddSlotAsync(settings);
                }
            }
        }

        private void OnDestroy() => SlotManager?.Dispose();
    }
}
```

**完了条件**:
- スクリプトがコンパイルエラーなし
- `[SlotManager]` GameObject にアタッチでき、Play Mode で Awake が実行されること
- `initialSlots` フィールドが Inspector で設定可能であること

#### T9-2: SlotManagerBehaviour を [SlotManager] にアタッチ

T8-2 で作成した `[SlotManager]` GameObject に `SlotManagerBehaviour` をアタッチする。

**完了条件**: `[SlotManager]` GameObject に `SlotManagerBehaviour` がアタッチされていること

---

## T10: デモシーン — ランタイム Canvas UI スクリプト配線

_Requirements: 1, 4, 5_

### 概要

Canvas 上のランタイム UI コンポーネント (SlotManagementPanelUI / SlotListItemUI / SlotDetailPanelUI) を実装し、T8 で配置した GameObject に配線する。

### Leaf タスク

#### T10-1: SlotListItemUI プレハブと スクリプトの作成

ファイル: `Samples~/UI/Runtime/SlotListItemUI.cs`  
プレハブ: `Samples~/UI/Prefabs/SlotListItem.prefab`

- SlotId ラベル / 表示名ラベル / 状態ラベルの表示
- Weight トグル (有効/無効で `SlotSettings.weight` を 1.0 / 0.0 に変更)
- 削除ボタン (コールバックで `SlotManager.RemoveSlot()` を呼び出す)

**完了条件**: プレハブが生成でき、SlotId / displayName / 状態が正しく表示されること

#### T10-2: SlotManagementPanelUI スクリプトの作成と配線

ファイル: `Samples~/UI/Runtime/SlotManagementPanelUI.cs`

- `AddSlotButton` クリック時に `SlotManager.AddSlotAsync()` を呼び出す
- `SlotListScrollView` 内に `SlotListItem` プレハブをインスタンス化して Slot 一覧を表示する
- Slot の追加・削除に応じてリストをリアルタイムに更新する

`SlotManagementPanel` GameObject に `SlotManagementPanelUI` をアタッチし、Button / ScrollView の参照を Inspector で設定する。

**完了条件**:
- 「Slot を追加」ボタン押下で新規 Slot が一覧に追加されること
- 削除ボタン押下で対象 Slot が一覧から削除されること

#### T10-3: SlotDetailPanelUI スクリプトの作成と配線

ファイル: `Samples~/UI/Runtime/SlotDetailPanelUI.cs`

- `DisplayNameInputField` の変更を `SlotSettings.displayName` に反映する
- `WeightToggle` の変更を `SlotSettings.weight` (1.0 / 0.0) に反映する
- `FallbackDropdown` に `FallbackBehavior` の選択肢を動的生成し、変更を `SlotSettings.fallbackBehavior` に反映する
- `DeleteSlotButton` クリック時に `SlotManager.RemoveSlot()` を呼び出す

**完了条件**: 選択中 Slot の設定変更が `SlotSettings` フィールドに正しく反映されること

---

## T11: デモシーン — 参照共有シナリオ用アセット作成とシーン内配線

_Requirements: 5_

### 概要

design.md §10 に従い、1 つの VMC MoCap ソースを 2 つの Slot で共有するシナリオ用アセットを作成し、デモシーン内に配線する。

### Leaf タスク

#### T11-1: 参照共有シナリオ用 SlotSettings アセットの作成

以下の 2 つの `SlotSettings` ScriptableObject アセットを作成する:

**SlotSettings_Shared_Slot1.asset**:
- `slotId = "shared-slot-01"`
- `displayName = "共有テスト Slot 1 (AvatarA)"`
- `moCapSourceDescriptor.SourceTypeId = "VMC"`
- `moCapSourceDescriptor.Config = VMCMoCapSourceConfig (port: 39539)` ← **共有 Config SO**
- `avatarProviderDescriptor.ProviderTypeId = "Builtin"`
- `avatarProviderDescriptor.Config = BuiltinAvatarProviderConfig (avatarPrefab: AvatarA)`

**SlotSettings_Shared_Slot2.asset**:
- `slotId = "shared-slot-02"`
- `displayName = "共有テスト Slot 2 (AvatarB)"`
- `moCapSourceDescriptor.SourceTypeId = "VMC"`
- `moCapSourceDescriptor.Config = VMCMoCapSourceConfig (port: 39539)` ← **同一 Config SO アセットを参照**
- `avatarProviderDescriptor.ProviderTypeId = "Builtin"`
- `avatarProviderDescriptor.Config = BuiltinAvatarProviderConfig (avatarPrefab: AvatarB)` ← 異なる

> **重要**: 2 つの `SlotSettings` が**同一の `VMCMoCapSourceConfig` SO アセット**を参照することで `IMoCapSourceRegistry` の参照共有が成立する。Config の内容が同じ別 SO では参照等価にならないため、必ず同一オブジェクトを参照すること (contracts.md §1.1 Descriptor 等価判定方針に基づく)。

**完了条件**: 2 つのアセットが存在し、`moCapSourceDescriptor.Config` が同一 SO アセットを参照していること

#### T11-2: SlotManagerBehaviour の initialSlots への参照共有用アセット設定

`SlotManagerBehaviour.initialSlots` に T11-1 で作成した 2 つのアセットを設定する。

**完了条件**: Play Mode 開始時に 2 つの Slot が自動追加されること

#### T11-3: 参照カウント表示ラベルのデバッグ実装 (デモ専用)

`SlotManagementPanel` に参照カウント表示ラベルを追加し、デバッグ専用 API で `IMoCapSourceRegistry` の内部参照カウントを表示する (design.md §10.3 準拠)。

| 操作 | 期待されるカウント |
|------|--------------|
| Slot 1 追加 (VMC port:39539) | 1 |
| Slot 2 追加 (VMC port:39539) | 2 |
| Slot 1 削除 | 1 |
| Slot 2 削除 | 0 |

**完了条件**: デモシーン上で参照カウントが操作に応じて変化することを視覚的に確認できること

---

## T12: デモシーン — エラーシミュレーションシナリオの組み込み

_Requirements: 5_

### 概要

design.md §7.3 に従い、VMC 切断シミュレーション・初期化失敗シミュレーションのボタンと `ErrorSimulationHelper` を実装する。

### Leaf タスク

#### T12-1: ErrorSimulationHelper.cs の作成

ファイル: `Samples~/UI/Runtime/ErrorSimulationHelper.cs`

- `SimulateVmcDisconnect(string slotId)`: 指定 Slot の `IMoCapSource` に接続タイムアウトを擬似発生させ `SlotErrorCategory.VmcReceive` を `ISlotErrorChannel` に発行するテスト用ヘルパーメソッド
- `SimulateInitFailure()`: 不正な typeId (`"INVALID_TYPE"`) を持つ `SlotSettings` を `SlotManager.AddSlotAsync` に渡し、`SlotErrorCategory.InitFailure` を発生させる

**完了条件**: ヘルパーメソッドがコンパイルエラーなし

#### T12-2: エラーシミュレーションボタンのシーン配置と配線

Canvas に「エラーシミュレーション」ボタングループを追加する:

- 「VMC 切断シミュレーション」ボタン → `ErrorSimulationHelper.SimulateVmcDisconnect()` を呼び出す
- 「初期化失敗シミュレーション」ボタン → `ErrorSimulationHelper.SimulateInitFailure()` を呼び出す

各ボタンを T14 で実装する `SlotErrorPanel` と組み合わせて、エラーパネルへの表示を確認できるようにする。

**完了条件**:
- ボタン押下でエラーが `ISlotErrorChannel` に発行されること
- 発行されたエラーが `SlotErrorPanel` に表示されること (T14 と連携)

---

## T13: デモシーン — Fallback 視覚確認シナリオの組み込み

_Requirements: 5, 10_

### 概要

`FallbackBehavior` の 3 種類 (HoldLastPose / TPose / Hide) をシーン内で切り替え、エラー発生時のアバター挙動差異を視覚確認できるシナリオを組み込む。

### Leaf タスク

#### T13-1: Fallback 切り替えボタンのシーン配置と配線

Canvas に「Fallback 切り替え」ボタングループを追加する:

- 「HoldLastPose に設定」ボタン → 選択中 Slot の `SlotSettings.fallbackBehavior = FallbackBehavior.HoldLastPose`
- 「TPose に設定」ボタン → `FallbackBehavior.TPose`
- 「Hide に設定」ボタン → `FallbackBehavior.Hide`

**完了条件**: ボタン押下で選択中 Slot の `fallbackBehavior` が変更されること

#### T13-2: Fallback 挙動の視覚確認手順のシーン内ドキュメント

デモシーン内の UI に以下の操作手順を表示するテキストラベルを追加する (デバッグ支援目的):

```
Fallback 視覚確認手順:
1. Slot を追加してアバターと VMC ソースを設定
2. Fallback 切り替えボタンで挙動を選択
3. エラーシミュレーションボタンでエラーを発生
4. アバターの挙動変化 (HoldLastPose/TPose/Hide) を確認
```

**完了条件**: 操作手順ラベルがシーン内に表示されること

---

## T14: SlotErrorPanel — ErrorChannel 購読 UI 実装

_Requirements: 11_

### 概要

`SlotErrorPanel` MonoBehaviour を実装する。`RegistryLocator.ErrorChannel.Errors` を `.ObserveOnMainThread().Subscribe()` で購読し、最新 20 件のエラーをスクロール可能なパネルに表示する。

### Leaf タスク

#### T14-1: ErrorLogItem プレハブの作成

ファイル: `Samples~/UI/Prefabs/ErrorLogItem.prefab`

エラーログ 1 件を表示する UI プレハブを作成する:
- `Text` コンポーネントでエラー情報を表示
- 表示フォーマット: `[HH:mm:ss] [Category] Slot:SlotId\nException.Message`

**完了条件**: プレハブが生成でき、Text に任意の文字列を設定できること

#### T14-2: SlotErrorPanel.cs の作成と配線

ファイル: `Samples~/UI/Runtime/SlotErrorPanel.cs`

design.md §6.3 のコードスニペットに従い実装する:

```csharp
public class SlotErrorPanel : MonoBehaviour
{
    [SerializeField] private Transform _logContainer;
    [SerializeField] private GameObject _logItemPrefab;
    [SerializeField] private int _maxDisplayCount = 20; // 最新 N 件リング

    private CompositeDisposable _disposables;

    private void Start()
    {
        _disposables = new CompositeDisposable();

        RegistryLocator.ErrorChannel.Errors
            .ObserveOnMainThread()
            .Subscribe(OnSlotError)
            .AddTo(_disposables);
    }

    private void OnDestroy() => _disposables?.Dispose();

    private void OnSlotError(SlotError error)
    {
        // 最新 N 件を超えた場合、古いエントリを削除
        if (_logContainer.childCount >= _maxDisplayCount)
            Destroy(_logContainer.GetChild(0).gameObject);

        var item = Instantiate(_logItemPrefab, _logContainer);
        item.GetComponentInChildren<Text>().text =
            $"[{error.Timestamp:HH:mm:ss}] [{error.Category}] Slot:{error.SlotId}\n{error.Exception?.Message}";
    }
}
```

`SlotErrorPanel` GameObject に `SlotErrorPanel.cs` をアタッチし、`_logContainer` / `_logItemPrefab` の参照を Inspector で設定する。

**完了条件**:
- `ISlotErrorChannel.Errors` の購読が `Start()` で開始されること
- エラー発生時にパネルに新しいエントリが追加されること
- 20 件を超えた場合に古いエントリが削除されること
- `OnDestroy()` で購読が解除されること

#### T14-3: カテゴリフィルタ UI の実装 (任意)

design.md §7.1 に記載のカテゴリフィルタ (トグルグループ) を実装する:
- `VmcReceive` / `InitFailure` / `ApplyFailure` / `RegistryConflict` のトグル
- トグル OFF のカテゴリのエラーはパネルに追加しない

> **注意**: 本タスクは任意実装である。T14-2 の基本実装が完了後に追加する。

**完了条件**: カテゴリ別フィルタリングが機能し、選択したカテゴリのエラーのみ表示されること

#### T14-4: エラークリアボタンの配線

`ClearErrorsButton` 押下時に `_logContainer` 内の全 GameObject を `Destroy()` する処理を実装し、ボタンのクリックイベントに配線する。

**完了条件**: 「エラーをクリア」ボタン押下でパネルが空になること

---

## T15: package.json samples エントリ登録

_Requirements: 6_

### 概要

UPM の `package.json` に `Samples~/UI` へのサンプルエントリを追加する。

### Leaf タスク

#### T15-1: package.json への samples エントリ追加

`Packages/com.realtimeavatarcontroller/package.json` に以下の `samples` エントリを追加する (design.md §8.2 準拠):

```json
{
  "samples": [
    {
      "displayName": "UI Sample",
      "description": "Editor Inspector extensions and demo scene for Slot management UI.",
      "path": "Samples~/UI"
    }
  ]
}
```

**完了条件**:
- `package.json` に `samples` キーが存在し、正しいエントリが登録されていること
- Unity Package Manager でパッケージを確認した際に「UI Sample」インポートオプションが表示されること

---

## T16: テスト asmdef と EditMode テスト実装 (任意)

_Requirements: 12_

> **本タスクは任意実装である。** プロジェクトの運用判断に従い、必要な場合のみ実装する。

### 概要

`Tests/EditMode/ui-sample/` に `SlotSettingsEditor` の EditMode テストを実装する。テストコードが `Samples~` に混入しないよう、パッケージルート直下の `Tests/` に配置する (design.md §8.1 / §11.3 準拠)。

### Leaf タスク

#### T16-1: EditMode テスト asmdef の作成

ファイル: `Tests/EditMode/ui-sample/RealtimeAvatarController.Samples.UI.Tests.EditMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Samples.UI.Tests.EditMode",
  "references": [
    "RealtimeAvatarController.Samples.UI",
    "RealtimeAvatarController.Samples.UI.Editor",
    "RealtimeAvatarController.Core",
    "UnityEditor.TestRunner",
    "UnityEngine.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
```

**完了条件**: asmdef がコンパイルエラーなし

#### T16-2: SlotSettingsEditorTests.cs の作成

ファイル: `Tests/EditMode/ui-sample/SlotSettingsEditorTests.cs`

design.md §11.1 および §11.4 のモック注入方針に従い以下のテストを実装する:

- **ドロップダウン表示確認**: `RegistryLocator.OverrideProviderRegistry(mockRegistry)` でモック注入後、`RefreshTypeIds()` を呼び出した際に `_providerTypeIds` に登録 typeId が含まれること
- **Registry 未登録時のフォールバック**: Registry 未登録状態で `RefreshTypeIds()` を呼び出した後、`_providerTypeIds` が空配列であること
- **Fallback UI の反映**: `SlotSettings.fallbackBehavior` を変更後、`SerializedProperty` に正しい enum 値が反映されること
- **Weight トグル**: トグル ON/OFF 時に `SlotSettings.weight` が `1.0` / `0.0` に設定されること
- **FallbackBehavior デフォルト値**: 新規 `SlotSettings` インスタンスの `fallbackBehavior` が `HoldLastPose` であること (validation-design.md OI-7 対応)

`SetUp` / `TearDown` で `RegistryLocator.OverrideXxx()` / `RegistryLocator.ResetForTest()` を使用する (design.md §11.4 準拠)。

**完了条件**: 全テストが Unity Test Runner で pass すること

---

## T17: テスト asmdef と PlayMode テスト実装 (任意)

_Requirements: 12_

> **本タスクは任意実装である。** プロジェクトの運用判断に従い、必要な場合のみ実装する。

### 概要

`Tests/PlayMode/ui-sample/` に `SlotManagementDemo` シーンの PlayMode テストを実装する。

### Leaf タスク

#### T17-1: PlayMode テスト asmdef の作成

ファイル: `Tests/PlayMode/ui-sample/RealtimeAvatarController.Samples.UI.Tests.PlayMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Samples.UI.Tests.PlayMode",
  "references": [
    "RealtimeAvatarController.Samples.UI",
    "RealtimeAvatarController.Core",
    "UnityEngine.TestRunner"
  ],
  "includePlatforms": [],
  "autoReferenced": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
```

**完了条件**: asmdef がコンパイルエラーなし

#### T17-2: SlotManagementDemoTests.cs の作成

ファイル: `Tests/PlayMode/ui-sample/SlotManagementDemoTests.cs`

design.md §11.2 に従い以下のテストを実装する:

- **デモシーン起動確認**: `SceneManager.LoadSceneAsync("SlotManagementDemo")` でシーンをロードし、`SlotManagerBehaviour` が初期化エラーなく `Awake` することを確認
- **参照共有シナリオ再現**: 同一 `MoCapSourceDescriptor` (同一 Config SO アセット参照) を持つ 2 件の `SlotSettings` を `AddSlotAsync` した後、`IMoCapSourceRegistry` が同一の `IMoCapSource` インスタンスを返すことを確認
- **Slot 削除後のエラーチャンネル**: Slot 削除後に `ISlotErrorChannel.Errors` に不要なエラーが発行されないことを確認

**完了条件**: 全テストが Unity Test Runner (PlayMode) で pass すること

---

## スコープ外の明示

以下のタスクは **initial 版ではタスク化しない** (design.md §12 / validation-design.md §9 確定事項):

### Req 9 — UniRx MotionStream デバッグプレビュー (将来拡張)

`IMoCapSource.MotionStream` のリアルタイムプレビュー UI は initial 版では実装しない。将来的に `MotionStream` の受信フレーム数・タイムスタンプ表示が必要になった場合は、以下の方針で実装できる余地を残す:
- `IMoCapSource.MotionStream.ObserveOnMainThread().Subscribe()` で購読
- 受信フレーム数・最新タイムスタンプを `SlotDetailPanel` 内のデバッグラベルに表示

### Req 13 シナリオ Y — ランタイム動的生成 SlotSettings の動的生成後保存 UI (将来拡張)

`ScriptableObject.CreateInstance<SlotSettings>()` で動的生成した Slot の保存 UI はスコープ外。  
ランタイム動的生成そのものは contracts.md §1.1 で公式許容済みであり、UI サンプルがこれを妨げないことを最低条件とする (シナリオ Y を妨げない設計は達成済み)。
