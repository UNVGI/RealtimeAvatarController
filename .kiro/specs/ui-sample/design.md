# ui-sample 設計ドキュメント

> **フェーズ**: design  
> **言語**: ja  
> **Wave**: Wave B (並列波) — slot-core design.md (Wave A) を参照起点とする

---

## 1. 概要

### 責務範囲

`ui-sample` は Realtime Avatar Controller の機能部 API を実際に呼び出すデモ・サンプル UI を提供する Spec である。  
UPM の `Samples~` 機構を通じて配布し、利用者が Unity Package Manager からインポートして使用する。

**本 Spec が主要に提供する UI の場所は Unity Editor の Inspector パネルである。**  
スタンドアロンビルド (Player) 向けの実行時 GUI は本サンプルのスコープ外とする。  
VTuber システム側が独自の実行時 UI を構築する際の参考コードとして機能することを目的とする。

### UI の役割分担

| UI の種類 | 提供範囲 | 備考 |
|----------|---------|------|
| Editor Inspector 拡張 (`SlotSettingsEditor`) | **主要提供範囲** | `CustomEditor` による `SlotSettings` Inspector UI |
| ランタイム Canvas UI (`SlotManagementDemoUI`) | **任意提供範囲** | デモシーン上での動作確認用。スタンドアロンビルドは対象外 |
| エラー表示 UI (`SlotErrorPanel`) | **任意提供範囲** | `ISlotErrorChannel.Errors` 購読。デモシーン内に配置 |

### スコープ外の明示

- 機能部 API の新規定義 (各機能 Spec で完結済み)
- 機能部アセンブリへの UI フレームワーク依存の持ち込み
- 表情制御・リップシンクの UI (初期段階では具象実装なし)
- Addressable アバター Provider の UI

---

## 2. UI フレームワーク選択 (確定)

### 候補評価

| 観点 | UGUI (uGUI) | UI Toolkit |
|------|------------|------------|
| Unity 6 の推奨方針 | ランタイム用として引き続き利用可 | Editor UI の推奨。Unity 6 以降はランタイムでも本格対応 |
| Editor 拡張との親和性 | `SerializedProperty` + `EditorGUILayout` による手書き | `PropertyDrawer` / `CustomEditor` を UXML + USS で記述可 |
| 動的候補列挙の実装容易性 | `EditorGUILayout.Popup()` で `string[]` を渡すだけで完結 | `DropdownField` に `choices` を設定する。同等に実装可能 |
| サンプルとしての学習コスト | コード量少・Inspector 拡張の標準実装として学習コスト低 | UXML/USS の習得コストがあるが将来性が高い |
| Inspector 拡張のデファクト | `OnGUI` / `EditorGUILayout` は既存ドキュメントが豊富 | Unity 6 以降 Editor 向け推奨だが移行期間中 |

### 選定結果

**UGUI (UnityEngine.UI) + `EditorGUILayout` ベースの Inspector 拡張を採用する。**

**選定理由**:

1. **Editor Inspector 主眼方針との整合**: 本 Spec の主要提供範囲は Editor Inspector 上の編集体験であり、`EditorGUILayout` / `SerializedProperty` ベースの `CustomEditor` がもっとも実装コストが低い。
2. **動的候補列挙の実装容易性**: `EditorGUILayout.Popup(selectedIndex, typeIds.ToArray())` の 1 行で Registry 由来の動的ドロップダウンを実現できる。
3. **サンプルとしての学習コスト**: 既存 Unity ドキュメントの参照が容易で、利用者が本サンプルを読んで独自 Editor 拡張を実装しやすい。
4. **ランタイム Canvas UI**: デモシーン上の任意 UI は `UnityEngine.UI` (UGUI) の `Text` / `Button` / `Toggle` を使用する。こちらも既存の学習資料が豊富でサンプルとして適切。

### 採用バージョン

| ライブラリ | バージョン | 取得方法 |
|-----------|---------|---------|
| `UnityEngine.UI` (UGUI) | Unity 6000.3.10f1 同梱 | 組み込み |
| `UnityEditor` (`EditorGUILayout` 等) | Unity 6000.3.10f1 同梱 | 組み込み |
| UniRx (`com.neuecc.unirx`) | `7.1.0` 以上 | OpenUPM scoped registry |
| UniTask (`com.cysharp.unitask`) | `2.5.x` 以上 | OpenUPM scoped registry |

> **注記**: UI Toolkit は将来バージョンで移行候補とする。初期版では採用しない。

---

## 3. サンプルシーン構成

### 3.1 シーン一覧

| シーン名 | 配置パス | 目的 |
|---------|---------|------|
| `SlotManagementDemo` | `Samples~/UI/Scenes/SlotManagementDemo.unity` | 複数 Slot 同時稼働・参照共有・Fallback 視覚確認の総合デモ |

### 3.2 `SlotManagementDemo` シーン

#### 目的・シナリオ

1. **複数 Slot 同時稼働**: 2〜3 つの Slot を同時に起動し、各々が独立したアバターへ MoCap データを適用するシナリオ
2. **参照共有シナリオ**: 1 つの VMC MoCap ソース (同一 `sourceTypeId` + 同一 port) を 2 Slot が共有し、`IMoCapSourceRegistry` の参照カウント挙動を視覚確認
3. **Fallback 視覚確認**: `FallbackBehavior` を `HoldLastPose` / `TPose` / `Hide` に切り替え、エラー発生時の挙動差異を視覚確認
4. **エラー発生シミュレーション**: VMC 切断シミュレーション・初期化失敗シミュレーションを実行し、`ISlotErrorChannel` 経由でエラーパネルに表示

#### 主要 GameObject 構成

```
SlotManagementDemo (Scene root)
├── [SlotManager]                       // SlotManager MonoBehaviour ラッパー
│   └── SlotManagerBehaviour.cs         // SlotManager 初期化・ライフサイクル管理
├── Canvas                              // Screen Space - Overlay
│   ├── SlotManagementPanel             // Slot 一覧・追加・削除 UI (UGUI)
│   │   ├── SlotListScrollView          // Slot 一覧スクロールビュー
│   │   ├── AddSlotButton               // Slot 追加ボタン
│   │   └── SlotListItemTemplate        // プレハブ (非アクティブ)
│   ├── SlotDetailPanel                 // 選択 Slot の詳細設定 UI
│   │   ├── DisplayNameField            // 表示名入力
│   │   ├── WeightToggle                // Weight 二値スイッチ
│   │   ├── FallbackDropdown            // FallbackBehavior ドロップダウン
│   │   └── DeleteSlotButton            // Slot 削除ボタン
│   └── SlotErrorPanel                  // エラー表示パネル (右下固定)
│       ├── ErrorLogScrollView          // エラーログスクロールビュー
│       └── ClearErrorsButton           // エラークリアボタン
├── AvatarArea                          // アバター表示領域
│   ├── AvatarSlot_01                   // Slot 1 のアバター配置用 Transform
│   ├── AvatarSlot_02                   // Slot 2 のアバター配置用 Transform
│   └── AvatarSlot_03                   // Slot 3 のアバター配置用 Transform
├── Lighting                            // ディレクショナルライト
└── EventSystem                         // UGUI EventSystem
```

#### `SlotManagerBehaviour` MonoBehaviour 概要

```csharp
namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用 SlotManager ラッパー MonoBehaviour。
    /// Awake で SlotManager を初期化し、OnDestroy で Dispose する。
    /// </summary>
    public class SlotManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private SlotSettings[] initialSlots;  // Editor で事前設定する初期 Slot

        public SlotManager SlotManager { get; private set; }

        private void Awake()
        {
            SlotManager = new SlotManager(RegistryLocator.ProviderRegistry,
                                          RegistryLocator.MoCapSourceRegistry,
                                          RegistryLocator.ErrorChannel);
        }

        private void OnDestroy() => SlotManager?.Dispose();
    }
}
```

---

## 4. SlotSettings Inspector 拡張 (Editor)

### 4.1 CustomEditor 概要

`SlotSettings` (ScriptableObject) に対して `[CustomEditor(typeof(SlotSettings))]` を付与した `SlotSettingsEditor` クラスを定義する。  
配置アセンブリ: `RealtimeAvatarController.Samples.UI.Editor` (`Editor/` 以下)

```csharp
namespace RealtimeAvatarController.Samples.UI.Editor
{
    [CustomEditor(typeof(SlotSettings))]
    public class SlotSettingsEditor : UnityEditor.Editor
    {
        // --- SerializedProperty キャッシュ ---
        private SerializedProperty _slotIdProp;
        private SerializedProperty _displayNameProp;
        private SerializedProperty _weightProp;
        private SerializedProperty _fallbackBehaviorProp;
        private SerializedProperty _avatarProviderDescriptorProp;
        private SerializedProperty _moCapSourceDescriptorProp;

        // --- Registry キャッシュ ---
        private string[] _providerTypeIds   = System.Array.Empty<string>();
        private string[] _moCapSourceTypeIds = System.Array.Empty<string>();

        private void OnEnable()
        {
            // SerializedProperty のキャッシュ
            _slotIdProp                  = serializedObject.FindProperty("slotId");
            _displayNameProp             = serializedObject.FindProperty("displayName");
            _weightProp                  = serializedObject.FindProperty("weight");
            _fallbackBehaviorProp        = serializedObject.FindProperty("fallbackBehavior");
            _avatarProviderDescriptorProp = serializedObject.FindProperty("avatarProviderDescriptor");
            _moCapSourceDescriptorProp   = serializedObject.FindProperty("moCapSourceDescriptor");

            // Registry から候補列挙 (未初期化の場合は空配列)
            RefreshTypeIds();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_slotIdProp,       new GUIContent("Slot ID"));
            EditorGUILayout.PropertyField(_displayNameProp,  new GUIContent("表示名"));

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
        // ... (各描画メソッドは後述)
    }
}
```

### 4.2 typeId ドロップダウン (Registry 動的列挙)

`RefreshTypeIds()` を `OnEnable()` および「更新」ボタン押下時に呼び出す。

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
        // Registry 未初期化の場合は空配列にフォールバック
        Debug.LogWarning($"[SlotSettingsEditor] Registry 候補取得失敗: {ex.Message}");
        _providerTypeIds    = System.Array.Empty<string>();
        _moCapSourceTypeIds = System.Array.Empty<string>();
    }
}
```

**ドロップダウン描画 (Avatar Provider)**:

```csharp
private void DrawAvatarProviderSection()
{
    EditorGUILayout.LabelField("アバター Provider", EditorStyles.boldLabel);

    var typeIdProp = _avatarProviderDescriptorProp.FindPropertyRelative("ProviderTypeId");
    var configProp = _avatarProviderDescriptorProp.FindPropertyRelative("Config");

    if (_providerTypeIds.Length == 0)
    {
        EditorGUILayout.HelpBox("Registry に Provider が未登録です。\n[InitializeOnLoadMethod] が実行されているか確認してください。", MessageType.Warning);
        // 未登録時は手入力フィールドにフォールバック
        EditorGUILayout.PropertyField(typeIdProp, new GUIContent("Provider Type ID (手入力)"));
    }
    else
    {
        int currentIndex = System.Array.IndexOf(_providerTypeIds, typeIdProp.stringValue);
        if (currentIndex < 0) currentIndex = 0;
        int newIndex = EditorGUILayout.Popup("Provider Type", currentIndex, _providerTypeIds);
        typeIdProp.stringValue = _providerTypeIds[newIndex];
    }

    // ProviderConfigBase 派生 SO のドラッグ&ドロップ参照欄
    EditorGUILayout.PropertyField(configProp, new GUIContent("Provider Config (SO)"));

    if (GUILayout.Button("候補を更新"))
        RefreshTypeIds();
}
```

**MoCap ソース ドロップダウン** も同じパターンで実装する (`sourceTypeId` / `MoCapSourceConfigBase`)。

### 4.3 Fallback 設定 enum ドロップダウン

`SerializedProperty` の `enumDisplayNames` を使用し、`EditorGUILayout.PropertyField()` で自動的に enum ドロップダウンを描画する。  
`FallbackBehavior` の各値の表示名:

| 列挙値 | 表示文字列 |
|-------|----------|
| `HoldLastPose` | Hold Last Pose (最後のポーズを保持) |
| `TPose` | T-Pose (T ポーズに戻す) |
| `Hide` | Hide (アバターを非表示) |

### 4.4 Weight 二値トグル

```csharp
private void DrawWeightToggle()
{
    bool isActive = _weightProp.floatValue >= 0.5f;
    bool newActive = EditorGUILayout.Toggle("Weight 有効 (1.0 / 0.0)", isActive);
    if (newActive != isActive)
        _weightProp.floatValue = newActive ? 1.0f : 0.0f;
    EditorGUILayout.LabelField($"現在の Weight: {_weightProp.floatValue:F1}", EditorStyles.miniLabel);
}
```

---

## 5. Editor 起動時の Registry 初期化依存

### 5.1 前提条件

本 Inspector 拡張は、Editor 起動時に各 Factory の `[InitializeOnLoadMethod]` が実行されて  
`RegistryLocator.ProviderRegistry` / `RegistryLocator.MoCapSourceRegistry` へ Factory が登録済みであることを前提とする。

```
Editor 起動
  └── [InitializeOnLoadMethod] (各 Factory の Editor 登録メソッド)
        ├── BuiltinAvatarProviderFactory.RegisterEditor()
        │     → RegistryLocator.ProviderRegistry.Register("Builtin", ...)
        └── VMCMoCapSourceFactory.RegisterEditor()
              → RegistryLocator.MoCapSourceRegistry.Register("VMC", ...)
  └── SlotSettingsEditor.OnEnable()
        └── RefreshTypeIds()
              ├── RegistryLocator.ProviderRegistry.GetRegisteredTypeIds()   → ["Builtin"]
              └── RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds() → ["VMC"]
```

### 5.2 Registry 未初期化時のフォールバック表示

`GetRegisteredTypeIds()` が空配列を返す、または例外をスローする場合:

| 状況 | UI の挙動 |
|------|----------|
| `GetRegisteredTypeIds()` が空配列 | HelpBox (Warning) を表示し、`typeId` 手入力フィールドに切り替える |
| Registry インスタンスが null (未初期化) | `try-catch` で捕捉し、HelpBox (Error) を表示。手入力フィールドを表示 |
| 候補更新ボタン押下 | `RefreshTypeIds()` を再実行し、表示を更新 |

### 5.3 Domain Reload OFF 時の考慮

Unity の **Domain Reload を OFF** にした状態 (Enter Play Mode 最適化) では、  
Editor 再起動なしに Play Mode へ遷移するため `[InitializeOnLoadMethod]` が再実行されない場合がある。  
この場合、`RegistryLocator.ResetForTest()` が `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` により  
Registry をリセットしてから `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で各 Factory が再登録される。  
Inspector はドロップダウンを「候補を更新」ボタンで明示的に再取得できる。

---

## 6. ランタイム UI (任意機能)

デモシーン (`SlotManagementDemo`) 上に UGUI ベースの Canvas UI を配置する。  
**スタンドアロンビルド用の UI ではなく、Editor PlayMode でのデモ確認用である。**

### 6.1 Slot 一覧パネル (`SlotManagementPanel`)

```
SlotManagementPanel
├── Header: "Slot 管理"
├── [AddSlotButton] : Slot を 1 件追加 (SlotManager.AddSlotAsync 呼び出し)
└── SlotListScrollView
    └── SlotListItem (プレハブ、各 Slot 1 件)
        ├── SlotIdLabel        : Slot ID 表示
        ├── DisplayNameLabel   : 表示名
        ├── StateLabel         : ライフサイクル状態 (Created / Active / Inactive / Disposed)
        ├── WeightToggle       : 有効/無効 (1.0 / 0.0 切り替え)
        └── DeleteButton       : SlotRegistry.RemoveSlot() 呼び出し
```

### 6.2 Slot 詳細パネル (`SlotDetailPanel`)

Slot 一覧で選択した Slot の詳細設定を表示・編集する。  
Editor Inspector と異なり、Registry 候補のドロップダウン表示はランタイム `GetRegisteredTypeIds()` で動的に取得する。

```
SlotDetailPanel
├── DisplayNameInputField   : 表示名変更
├── ProviderTypeDropdown    : IAvatarProvider 種別 (Registry 動的列挙)
├── MoCapTypeDropdown       : IMoCapSource 種別 (Registry 動的列挙)
├── FallbackDropdown        : FallbackBehavior 選択
└── ApplyButton             : 変更を SlotSettings に反映
```

### 6.3 エラー表示パネル (`SlotErrorPanel`)

```csharp
namespace RealtimeAvatarController.Samples.UI
{
    public class SlotErrorPanel : MonoBehaviour
    {
        [SerializeField] private Transform _logContainer;
        [SerializeField] private GameObject _logItemPrefab;
        [SerializeField] private int _maxDisplayCount = 20;  // 最新 N 件リング

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
}
```

---

## 7. エラー表示 UI 詳細

### 7.1 表示方式

**画面右下に固定配置するスクロール可能なログパネル**を採用する。

| 設計要素 | 詳細 |
|---------|------|
| 配置 | Canvas 右下隅、常時表示 |
| スクロール | `ScrollRect` + `VerticalLayoutGroup` によるスクロール可能リスト |
| 最大表示件数 | 最新 20 件 (リングバッファ方式: 超過時は最古を Destroy) |
| エントリ表示内容 | タイムスタンプ / カテゴリ / Slot ID / 例外メッセージ先頭 |
| クリアボタン | 全エントリ削除 |
| カテゴリフィルタ | トグルグループで `VmcReceive` / `InitFailure` / `ApplyFailure` / `RegistryConflict` をフィルタリング可能 |

### 7.2 Core 側の LogError 抑制前提

- `SlotManager` が `Debug.LogError` を同一 `(SlotId, Category)` につき初回 1 フレームのみ出力 (以降抑制) することを前提とする。
- UI の `SlotErrorPanel` は `ISlotErrorChannel.Errors` の全件を受信する (抑制なし)。UI 側でフィルタリングする。

### 7.3 エラーシミュレーション

デモシーンに以下のシミュレーションボタンを用意する:

| ボタン | 動作 |
|-------|------|
| VMC 切断シミュレーション | 指定 Slot の `IMoCapSource` に対して接続タイムアウトを擬似的に発生させ、`SlotErrorCategory.VmcReceive` を `ISlotErrorChannel` に発行する (テスト用ヘルパーメソッド呼び出し) |
| 初期化失敗シミュレーション | 不正な `typeId` を持つ `SlotSettings` を `SlotManager.AddSlotAsync` に渡し、`SlotErrorCategory.InitFailure` を発生させる |
| Fallback 切り替えボタン | `FallbackBehavior` を `HoldLastPose` / `TPose` / `Hide` に切り替え、エラー発生時の視覚差異を確認 |

---

## 8. Samples~ 配置

### 8.1 ディレクトリ構造

```
Packages/com.realtimeavatarcontroller/          ← UPM パッケージルート
├── package.json
├── Runtime/
│   └── (機能部: slot-core 等)
├── Editor/
│   └── (機能部 Editor 拡張)
└── Samples~/
    └── UI/                                     ← UPM サンプルエントリ
        ├── Runtime/
        │   ├── RealtimeAvatarController.Samples.UI.asmdef
        │   ├── SlotManagerBehaviour.cs
        │   ├── SlotManagementPanelUI.cs
        │   ├── SlotListItemUI.cs
        │   ├── SlotDetailPanelUI.cs
        │   ├── SlotErrorPanel.cs
        │   └── ErrorSimulationHelper.cs
        ├── Editor/
        │   ├── RealtimeAvatarController.Samples.UI.Editor.asmdef
        │   └── SlotSettingsEditor.cs
        ├── Scenes/
        │   └── SlotManagementDemo.unity
        ├── Prefabs/
        │   ├── SlotListItem.prefab
        │   └── ErrorLogItem.prefab
        └── Tests/                               ← 任意
            ├── EditMode/
            │   ├── RealtimeAvatarController.Samples.UI.Tests.EditMode.asmdef
            │   └── SlotSettingsEditorTests.cs
            └── PlayMode/
                ├── RealtimeAvatarController.Samples.UI.Tests.PlayMode.asmdef
                └── SlotManagementDemoTests.cs
```

### 8.2 `package.json` の samples エントリ

```json
{
  "name": "com.realtimeavatarcontroller",
  "version": "0.1.0",
  "displayName": "Realtime Avatar Controller",
  "description": "VTuber avatar real-time motion control system.",
  "unity": "6000.3",
  "samples": [
    {
      "displayName": "UI Sample",
      "description": "Editor Inspector extensions and demo scene for Slot management UI.",
      "path": "Samples~/UI"
    }
  ]
}
```

---

## 9. 依存アセンブリ

### 9.1 `RealtimeAvatarController.Samples.UI` (Runtime asmdef)

| フィールド | 値 |
|-----------|---|
| `name` | `RealtimeAvatarController.Samples.UI` |
| `rootNamespace` | `RealtimeAvatarController.Samples.UI` |
| `references` | `RealtimeAvatarController.Core`, `RealtimeAvatarController.Motion`, `RealtimeAvatarController.MoCap.VMC`, `RealtimeAvatarController.Avatar.Builtin`, `UniRx`, `UniTask` |
| `includePlatforms` | [] (全プラットフォーム) |
| `autoReferenced` | false |

### 9.2 `RealtimeAvatarController.Samples.UI.Editor` (Editor asmdef)

| フィールド | 値 |
|-----------|---|
| `name` | `RealtimeAvatarController.Samples.UI.Editor` |
| `rootNamespace` | `RealtimeAvatarController.Samples.UI.Editor` |
| `references` | `RealtimeAvatarController.Samples.UI`, `RealtimeAvatarController.Core`, `RealtimeAvatarController.Core.Editor` |
| `includePlatforms` | `["Editor"]` |
| `autoReferenced` | false |

### 9.3 依存グラフ (ui-sample 視点)

```
RealtimeAvatarController.Samples.UI.Editor
  └── RealtimeAvatarController.Samples.UI (Runtime)
        ├── RealtimeAvatarController.Core           (slot-core)
        │     └── UniRx
        ├── RealtimeAvatarController.Motion          (motion-pipeline)
        ├── RealtimeAvatarController.MoCap.VMC       (mocap-vmc)
        └── RealtimeAvatarController.Avatar.Builtin  (avatar-provider-builtin)
```

> **一方向依存の厳守**: 機能部アセンブリ (`Core` / `Motion` / `MoCap.VMC` / `Avatar.Builtin`) は `Samples.UI` を参照しない。

---

## 10. 参照共有デモシナリオ

### 10.1 シナリオ概要

**「1 VMC ソースを 2 Slot で共有」シナリオ**  
1 つの VMC OSC 受信ポート (例: port 39539) に接続する MoCap ソースインスタンスを、2 つの Slot が共有する。  
各 Slot は異なるアバター (Prefab A / Prefab B) を保持し、同一のモーションデータが両アバターに適用される。

### 10.2 セットアップ手順

1. `SlotSettings_Shared_Slot1.asset` を作成:
   - `moCapSourceDescriptor.SourceTypeId = "VMC"`
   - `moCapSourceDescriptor.Config = VMCMoCapSourceConfig (port: 39539)`
   - `avatarProviderDescriptor.ProviderTypeId = "Builtin"`
   - `avatarProviderDescriptor.Config = BuiltinAvatarProviderConfig (avatarPrefab: AvatarA)`

2. `SlotSettings_Shared_Slot2.asset` を作成:
   - `moCapSourceDescriptor.SourceTypeId = "VMC"`
   - `moCapSourceDescriptor.Config = VMCMoCapSourceConfig (port: 39539)` ← **同一設定**
   - `avatarProviderDescriptor.ProviderTypeId = "Builtin"`
   - `avatarProviderDescriptor.Config = BuiltinAvatarProviderConfig (avatarPrefab: AvatarB)` ← 異なる

3. `SlotManager.AddSlotAsync(slot1Settings)` → `IMoCapSourceRegistry.Resolve()` が新規インスタンスを生成し参照カウント = 1
4. `SlotManager.AddSlotAsync(slot2Settings)` → `IMoCapSourceRegistry.Resolve()` が**同一インスタンスを返し**参照カウント = 2

### 10.3 `IMoCapSourceRegistry` 参照カウント挙動の視覚確認

```
デモシーン上の「参照カウント表示ラベル」
  → IMoCapSourceRegistry 内部の参照カウントをデバッグ表示 (デモ専用)
```

| 操作 | 参照カウント | 視覚的確認事項 |
|------|------------|--------------|
| Slot 1 追加 (VMC port:39539) | 1 | AvatarA がシーンに出現し、MoCap データで動作 |
| Slot 2 追加 (VMC port:39539) | 2 | AvatarB も同一ソースのモーションで動作。VMC 受信スレッドは 1 本のまま |
| Slot 1 削除 | 1 | AvatarA が消える。AvatarB は動作継続 (VMC ソースは維持) |
| Slot 2 削除 | 0 | AvatarB が消える。VMC ソースが Dispose される |

### 10.4 設計上の注意点

- UI 側から `IMoCapSource.Dispose()` を**直接呼ばない**。`IMoCapSourceRegistry.Release()` のみを使用する。
- 同一 Descriptor の等価判定は `SourceTypeId` + `Config` の内容比較で行う (詳細は `IMoCapSourceRegistry` 設計フェーズで確定)。
- デモシーン上の「参照カウント表示」はデバッグ専用の内部状態取得 API を用いる。本番用 API ではない。

---

## 11. テスト設計 (任意)

`ui-sample` のテストは**任意**とする (requirements.md / briefs/ui-sample.md の確定事項)。  
テストを作成する場合は以下の方針に従う。

### 11.1 EditMode テスト

| テスト対象 | 検証内容 |
|-----------|---------|
| `SlotSettingsEditor` ドロップダウン表示 | Registry に Factory を登録した状態で `OnInspectorGUI()` を呼び出した後、`_providerTypeIds` に登録 typeId が含まれることを確認 |
| Registry 未登録時のフォールバック | Registry 未登録状態で `RefreshTypeIds()` を呼び出した後、`_providerTypeIds` が空配列であることを確認 |
| Fallback UI の反映 | `SlotSettings.fallbackBehavior` を変更後、`SerializedProperty` に正しい enum 値が反映されることを確認 |
| Weight トグル | トグル ON/OFF 時に `SlotSettings.weight` が `1.0` / `0.0` に設定されることを確認 |

### 11.2 PlayMode テスト

| テスト対象 | 検証内容 |
|-----------|---------|
| デモシーン起動確認 | `SlotManagementDemo` シーンをロードし、`SlotManagerBehaviour` が初期化エラーなく Awake することを確認 |
| 参照共有シナリオ再現 | 同一 Descriptor を持つ 2 件の SlotSettings を `AddSlotAsync` した後、`IMoCapSourceRegistry` が同一インスタンスを返すことを確認 |
| Slot 削除後のエラーチャンネル | Slot 削除後に `ISlotErrorChannel.Errors` に不要なエラーが発行されないことを確認 |

### 11.3 テスト asmdef 命名

| asmdef 名 | 配置パス |
|----------|---------|
| `RealtimeAvatarController.Samples.UI.Tests.EditMode` | `Samples~/UI/Tests/EditMode/` |
| `RealtimeAvatarController.Samples.UI.Tests.PlayMode` | `Samples~/UI/Tests/PlayMode/` |

---

## 12. ファイル / ディレクトリ構成

### 12.1 `Samples~/UI/` 配下の全ファイル

```
Samples~/UI/
├── Runtime/
│   ├── RealtimeAvatarController.Samples.UI.asmdef
│   │     references: [Core, Motion, MoCap.VMC, Avatar.Builtin, UniRx, UniTask]
│   ├── SlotManagerBehaviour.cs
│   │     役割: MonoBehaviour ラッパー。SlotManager 初期化・Dispose
│   ├── SlotManagementPanelUI.cs
│   │     役割: Slot 一覧表示・追加ボタン・SlotListItem 生成管理
│   ├── SlotListItemUI.cs
│   │     役割: 1 Slot 分の表示 (ID / 状態 / Weight トグル / 削除ボタン)
│   ├── SlotDetailPanelUI.cs
│   │     役割: 選択 Slot の Provider/MoCapSource Dropdown・Fallback Dropdown・Apply ボタン
│   ├── SlotErrorPanel.cs
│   │     役割: ISlotErrorChannel.Errors 購読・エラーログ表示・最新 N 件管理
│   └── ErrorSimulationHelper.cs
│         役割: デモシーン用エラーシミュレーション (VMC 切断・InitFailure 擬似発生)
│
├── Editor/
│   ├── RealtimeAvatarController.Samples.UI.Editor.asmdef
│   │     references: [Samples.UI, Core, Core.Editor]
│   │     includePlatforms: ["Editor"]
│   └── SlotSettingsEditor.cs
│         役割: [CustomEditor(typeof(SlotSettings))] Inspector 拡張
│               - typeId ドロップダウン (Registry 動的列挙)
│               - Config SO ドラッグ&ドロップ欄
│               - Weight 二値トグル
│               - FallbackBehavior enum ドロップダウン
│
├── Scenes/
│   └── SlotManagementDemo.unity
│         役割: 複数 Slot / 参照共有 / Fallback / エラーシミュレーションの総合デモシーン
│
├── Prefabs/
│   ├── SlotListItem.prefab
│   │     内容: SlotIdLabel / DisplayNameLabel / StateLabel / WeightToggle / DeleteButton
│   └── ErrorLogItem.prefab
│         内容: Text (タイムスタンプ + カテゴリ + SlotId + メッセージ)
│
└── Tests/                               (任意)
    ├── EditMode/
    │   ├── RealtimeAvatarController.Samples.UI.Tests.EditMode.asmdef
    │   └── SlotSettingsEditorTests.cs
    └── PlayMode/
        ├── RealtimeAvatarController.Samples.UI.Tests.PlayMode.asmdef
        └── SlotManagementDemoTests.cs
```

### 12.2 主要クラス一覧

| クラス名 | 名前空間 | アセンブリ | 種別 |
|---------|---------|----------|------|
| `SlotManagerBehaviour` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | MonoBehaviour |
| `SlotManagementPanelUI` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | MonoBehaviour |
| `SlotListItemUI` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | MonoBehaviour |
| `SlotDetailPanelUI` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | MonoBehaviour |
| `SlotErrorPanel` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | MonoBehaviour |
| `ErrorSimulationHelper` | `RealtimeAvatarController.Samples.UI` | `Samples.UI` | static class |
| `SlotSettingsEditor` | `RealtimeAvatarController.Samples.UI.Editor` | `Samples.UI.Editor` | `UnityEditor.Editor` |

---

## 参照ドキュメント

- `.kiro/specs/_shared/contracts.md` — Spec 間公開 IF 契約 (1 章 Slot データモデル / 1.4 Registry / 1.7 エラーハンドリング / 1.8 Fallback)
- `.kiro/specs/_shared/spec-map.md` — 全 Spec 構成・依存グラフ
- `.kiro/specs/slot-core/design.md` — `SlotManager` / `ISlotErrorChannel` / `RegistryLocator` 公開 API 仕様
- `.kiro/specs/project-foundation/requirements.md` — asmdef 構成 / Samples~ 配置方針
- `.kiro/specs/_shared/briefs/ui-sample.md` — ui-sample Spec ブリーフィング
