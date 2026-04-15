# project-foundation 設計ドキュメント

> **フェーズ**: design  
> **言語**: ja  
> **Wave**: Wave B (並列波) — Wave A の `slot-core` design.md・`contracts.md` 6 章を前提とする

---

## 1. 概要

### 責務

`project-foundation` は Realtime Avatar Controller の**最上位基盤 Spec** であり、他 5 Spec すべての前提を整備する。本 Spec が確定する成果物は以下のとおり。

| 成果物 | 内容 |
|--------|------|
| Unity 6000.3.10f1 プロジェクト配置 | `RealtimeAvatarController/` ディレクトリにプロジェクトを配置 |
| UPM パッケージ雛形 | `package.json` を含む UPM 準拠のパッケージ構造 |
| アセンブリ定義ファイル (asmdef) 設計 | 機能部 / UI サンプル / Editor / Tests の全 asmdef JSON 雛形と依存関係図 |
| 名前空間規約 | `contracts.md` 6.2 章として確定済みの規約を本設計で参照・具体化 |
| 利用者向け README | OpenUPM scoped registry 追加手順・manifest.json スニペット |
| Domain Reload OFF 対応設計 | `RegistryLocator.ResetForTest()` の自動発火仕様 |
| CI 下地 (任意) | GitHub Actions による Unity ビルド検証の最小設定例 |

### Unity バージョン / プロジェクト配置

- **Unity バージョン**: `6000.3.10f1`
- **プロジェクトルート**: リポジトリ直下 `RealtimeAvatarController/`
- **UPM パッケージルート**: `RealtimeAvatarController/Packages/com.cysharp.realtimeavatarcontroller/`

> Unity プロジェクトの `Packages/` ディレクトリ内にパッケージを埋め込む形式 (Embedded Package) を採用し、git 単一リポジトリ内で開発と配布構造を一元管理する。

---

## 2. Unity プロジェクト構成

### 2.1 ProjectVersion.txt

Unity エディタは `RealtimeAvatarController/ProjectSettings/ProjectVersion.txt` に起動バージョンを記録する。

```
m_EditorVersion: 6000.3.10f1
m_EditorVersionWithRevision: 6000.3.10f1 (abcdef012345)
```

> `m_EditorVersionWithRevision` の revision ハッシュはインストール時に確定する。ここでは形式例として示す。

### 2.2 ProjectSettings の主要設定

| 設定項目 | 値 | ファイル |
|----------|----|---------|
| `scriptingBackend` | `Mono` (開発初期。IL2CPP への移行は将来) | `ProjectSettings.asset` |
| `apiCompatibilityLevel` | `.NET Standard 2.1` | `ProjectSettings.asset` |
| `enterPlayModeOptionsEnabled` | `true` | `EditorSettings.asset` |
| `enterPlayModeOptions` | `DisableDomainReload` | `EditorSettings.asset` |
| `scriptingDefineSymbols` | (初期は空) | `ProjectSettings.asset` |

> **Enter Play Mode Options (Domain Reload OFF)** はデフォルトで有効化する。Domain Reload OFF 環境での Registry 動作保証は第 7 章で設計する。

---

## 3. UPM パッケージ構造

### 3.1 パッケージ識別子・バージョン

| フィールド | 値 |
|------------|-----|
| `name` | `com.cysharp.realtimeavatarcontroller` |
| `displayName` | `Realtime Avatar Controller` |
| `version` | `0.1.0` |
| `unity` | `6000.3` |
| `unityRelease` | `10f1` |

### 3.2 package.json 完全版

```json
{
  "name": "com.cysharp.realtimeavatarcontroller",
  "displayName": "Realtime Avatar Controller",
  "version": "0.1.0",
  "unity": "6000.3",
  "unityRelease": "10f1",
  "description": "Runtime avatar controller for VTuber use cases. Provides slot-based MoCap source management, avatar provider abstraction, and motion pipeline for Unity.",
  "author": {
    "name": "Cysharp, Inc.",
    "email": "info@cysharp.co.jp",
    "url": "https://github.com/Cysharp"
  },
  "license": "MIT",
  "dependencies": {
    "com.neuecc.unirx": "7.1.0",
    "com.cysharp.unitask": "2.5.10"
  },
  "samples": [
    {
      "displayName": "UI Sample",
      "description": "Sample scene demonstrating Slot management and avatar control via UI.",
      "path": "Samples~/UI"
    }
  ],
  "keywords": [
    "avatar",
    "mocap",
    "vmc",
    "vtuber",
    "realtime"
  ]
}
```

#### dependencies バージョン根拠

| パッケージ | バージョン | 備考 |
|------------|-----------|------|
| `com.neuecc.unirx` | `7.1.0` | 2024 年時点の最新安定版。OpenUPM で配布。NuGet 依存なし |
| `com.cysharp.unitask` | `2.5.10` | 2024 年時点の最新安定版。OpenUPM で配布。`SlotManager.AddSlotAsync` で必須 |

> バージョン固定 (exact version) を採用し再現性を優先する。アップグレード時はパッケージ管理者が `package.json` を更新する。

### 3.3 Samples~ 配下の構成

```
Samples~/
└── UI/
    ├── Scenes/
    │   └── SampleScene.unity          # デモシーン (ui-sample Spec で作成)
    ├── Scripts/
    │   └── (UI サンプル C# スクリプト群)
    └── RealtimeAvatarController.Samples.UI.asmdef
```

> `Samples~` ディレクトリは Unity に自動インポートされない。利用者が Package Manager UI の「Import」ボタンを押すと `Assets/Samples/Realtime Avatar Controller/<version>/UI/` にコピーされる。

---

## 4. asmdef 構成

### 4.1 Runtime asmdef

#### 4.1.1 `RealtimeAvatarController.Core`

```json
{
  "name": "RealtimeAvatarController.Core",
  "rootNamespace": "RealtimeAvatarController.Core",
  "references": [
    "UniRx",
    "UniTask"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **担当 Spec**: slot-core
- **配置パス**: `Runtime/Core/RealtimeAvatarController.Core.asmdef`
- **役割**: Slot 抽象・各公開インターフェース群。UniRx / UniTask を直接参照する唯一の機能 asmdef。

#### 4.1.2 `RealtimeAvatarController.Motion`

```json
{
  "name": "RealtimeAvatarController.Motion",
  "rootNamespace": "RealtimeAvatarController.Motion",
  "references": [
    "RealtimeAvatarController.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **担当 Spec**: motion-pipeline
- **配置パス**: `Runtime/Motion/RealtimeAvatarController.Motion.asmdef`
- **役割**: モーション中立表現 (`MotionFrame` / `HumanoidMotionFrame`)・パイプライン処理。UniRx は Core 経由で間接利用。

#### 4.1.3 `RealtimeAvatarController.MoCap.VMC`

```json
{
  "name": "RealtimeAvatarController.MoCap.VMC",
  "rootNamespace": "RealtimeAvatarController.MoCap.VMC",
  "references": [
    "RealtimeAvatarController.Core",
    "RealtimeAvatarController.Motion"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **担当 Spec**: mocap-vmc
- **配置パス**: `Runtime/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.asmdef`
- **役割**: VMC OSC 受信具象実装。`IMoCapSource` の具象実装を提供する。UniRx / UniTask は Core 経由で間接利用。

#### 4.1.4 `RealtimeAvatarController.Avatar.Builtin`

```json
{
  "name": "RealtimeAvatarController.Avatar.Builtin",
  "rootNamespace": "RealtimeAvatarController.Avatar.Builtin",
  "references": [
    "RealtimeAvatarController.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **担当 Spec**: avatar-provider-builtin
- **配置パス**: `Runtime/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.asmdef`
- **役割**: ビルトインアバター供給具象実装。`IAvatarProvider` の具象実装を提供する。

#### 4.1.5 `RealtimeAvatarController.Samples.UI`

```json
{
  "name": "RealtimeAvatarController.Samples.UI",
  "rootNamespace": "RealtimeAvatarController.Samples.UI",
  "references": [
    "RealtimeAvatarController.Core",
    "RealtimeAvatarController.Motion",
    "RealtimeAvatarController.MoCap.VMC",
    "RealtimeAvatarController.Avatar.Builtin"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **担当 Spec**: ui-sample
- **配置パス**: `Samples~/UI/RealtimeAvatarController.Samples.UI.asmdef`
- **役割**: UI サンプル。機能部全アセンブリを参照するが、機能部は本 asmdef を参照しない (一方向依存)。`autoReferenced: false` として機能部ビルドへの混入を防ぐ。

### 4.2 Editor 専用 asmdef

各機能アセンブリに対応する Editor 専用 asmdef を定義する。`[UnityEditor.InitializeOnLoadMethod]` など `UnityEditor` API はこの Editor asmdef 内に配置する。

#### 4.2.1 `RealtimeAvatarController.Core.Editor`

```json
{
  "name": "RealtimeAvatarController.Core.Editor",
  "rootNamespace": "RealtimeAvatarController.Core.Editor",
  "references": [
    "RealtimeAvatarController.Core"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Editor/Core/RealtimeAvatarController.Core.Editor.asmdef`

#### 4.2.2 `RealtimeAvatarController.Motion.Editor`

```json
{
  "name": "RealtimeAvatarController.Motion.Editor",
  "rootNamespace": "RealtimeAvatarController.Motion.Editor",
  "references": [
    "RealtimeAvatarController.Motion"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Editor/Motion/RealtimeAvatarController.Motion.Editor.asmdef`

#### 4.2.3 `RealtimeAvatarController.MoCap.VMC.Editor`

```json
{
  "name": "RealtimeAvatarController.MoCap.VMC.Editor",
  "rootNamespace": "RealtimeAvatarController.MoCap.VMC.Editor",
  "references": [
    "RealtimeAvatarController.MoCap.VMC"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Editor/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.Editor.asmdef`

#### 4.2.4 `RealtimeAvatarController.Avatar.Builtin.Editor`

```json
{
  "name": "RealtimeAvatarController.Avatar.Builtin.Editor",
  "rootNamespace": "RealtimeAvatarController.Avatar.Builtin.Editor",
  "references": [
    "RealtimeAvatarController.Avatar.Builtin"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Editor/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.Editor.asmdef`

#### 4.2.5 `RealtimeAvatarController.Samples.UI.Editor` (任意)

```json
{
  "name": "RealtimeAvatarController.Samples.UI.Editor",
  "rootNamespace": "RealtimeAvatarController.Samples.UI.Editor",
  "references": [
    "RealtimeAvatarController.Samples.UI"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Samples~/UI/Editor/RealtimeAvatarController.Samples.UI.Editor.asmdef`
- **備考**: ui-sample 側で必要と判断した場合のみ作成する。

### 4.3 テスト専用 asmdef (Tests.EditMode / Tests.PlayMode)

各機能 Spec に EditMode / PlayMode の 2 系統を定義する。**slot-core / motion-pipeline / mocap-vmc / avatar-provider-builtin の 4 Spec は必須**。ui-sample は任意。

テスト asmdef の共通設定ルール:

| フィールド | 値 | 意味 |
|-----------|-----|------|
| `includePlatforms` | `[]` (空配列) | 全プラットフォーム対象。EditMode / PlayMode 区別は Unity Test Runner が制御 |
| `optionalUnityReferences` | `["TestAssemblies"]` | NUnit テストランナーへの参照を有効化 |
| `references` | 対応 Runtime asmdef のみ | 片方向依存。テスト asmdef 間の相互参照禁止 |
| `autoReferenced` | `false` | ランタイムビルドへのテストコード混入防止 |

#### 代表例: `RealtimeAvatarController.Core.Tests.EditMode`

```json
{
  "name": "RealtimeAvatarController.Core.Tests.EditMode",
  "rootNamespace": "RealtimeAvatarController.Core.Tests",
  "references": [
    "RealtimeAvatarController.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "optionalUnityReferences": ["TestAssemblies"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Tests/EditMode/slot-core/RealtimeAvatarController.Core.Tests.EditMode.asmdef`

#### 代表例: `RealtimeAvatarController.Core.Tests.PlayMode`

```json
{
  "name": "RealtimeAvatarController.Core.Tests.PlayMode",
  "rootNamespace": "RealtimeAvatarController.Core.Tests",
  "references": [
    "RealtimeAvatarController.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "optionalUnityReferences": ["TestAssemblies"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- **配置パス**: `Tests/PlayMode/slot-core/RealtimeAvatarController.Core.Tests.PlayMode.asmdef`

> 他の機能 Spec のテスト asmdef も同一パターンで作成する。詳細は第 10 章の配置方針を参照。

### 4.4 依存関係図

片方向依存の遵守を視覚化する。

```
UniRx / UniTask (外部ライブラリ)
        │
        ▼
RealtimeAvatarController.Core          ← slot-core 担当
        │
        ├──────────────────────────────────────────┐
        ▼                                          ▼
RealtimeAvatarController.Motion        RealtimeAvatarController.Avatar.Builtin
(motion-pipeline 担当)                 (avatar-provider-builtin 担当)
        │
        ▼
RealtimeAvatarController.MoCap.VMC     ← mocap-vmc 担当
        │
        └────────────────────────────────────────────────────────────┐
                                                                     ▼
                                                 RealtimeAvatarController.Samples.UI
                                                 (ui-sample 担当 / Samples~/ 配下)
```

**Editor asmdef は対応 Runtime asmdef のみを参照する (図から省略)**:

```
RealtimeAvatarController.Core
        ▲
        │ (片方向のみ)
RealtimeAvatarController.Core.Editor
```

**禁止依存 (いずれも違反禁止)**:

- Runtime asmdef → Editor asmdef への参照
- テスト asmdef 間の相互参照
- 機能部 asmdef → `RealtimeAvatarController.Samples.UI` への参照
- `RealtimeAvatarController.Motion` / `MoCap.VMC` / `Avatar.Builtin` → UniRx への直接参照

---

## 5. 名前空間規約

### 5.1 ルート名前空間

```
RealtimeAvatarController
```

### 5.2 サブ名前空間マッピング

| 名前空間 | 対応 asmdef | 用途 |
|---------|------------|------|
| `RealtimeAvatarController.Core` | `RealtimeAvatarController.Core` | Slot・各公開インターフェース (`SlotSettings`、`ISlotManager`、`RegistryLocator` 等) |
| `RealtimeAvatarController.Motion` | `RealtimeAvatarController.Motion` | モーションデータ中立表現・パイプライン (`MotionFrame`、`HumanoidMotionFrame` 等) |
| `RealtimeAvatarController.MoCap.VMC` | `RealtimeAvatarController.MoCap.VMC` | VMC OSC 受信具象実装 (`VMCMoCapSource` 等) |
| `RealtimeAvatarController.Avatar.Builtin` | `RealtimeAvatarController.Avatar.Builtin` | ビルトインアバター供給具象実装 (`BuiltinAvatarProvider` 等) |
| `RealtimeAvatarController.Samples.UI` | `RealtimeAvatarController.Samples.UI` | UI サンプル |

### 5.3 Editor サブ名前空間

エディタ限定コードには対応する機能名前空間に `.Editor` を付加する。

| 名前空間 | 対応 asmdef |
|---------|------------|
| `RealtimeAvatarController.Core.Editor` | `RealtimeAvatarController.Core.Editor` |
| `RealtimeAvatarController.Motion.Editor` | `RealtimeAvatarController.Motion.Editor` |
| `RealtimeAvatarController.MoCap.VMC.Editor` | `RealtimeAvatarController.MoCap.VMC.Editor` |
| `RealtimeAvatarController.Avatar.Builtin.Editor` | `RealtimeAvatarController.Avatar.Builtin.Editor` |
| `RealtimeAvatarController.Samples.UI.Editor` | `RealtimeAvatarController.Samples.UI.Editor` |

### 5.4 Tests サブ名前空間

テストコードには対応する機能名前空間に `.Tests` を付加する (EditMode / PlayMode の区別はパス・asmdef 名で管理し、名前空間は共通)。

| 名前空間 | 対応 asmdef 例 |
|---------|---------------|
| `RealtimeAvatarController.Core.Tests` | `RealtimeAvatarController.Core.Tests.EditMode` / `.PlayMode` |
| `RealtimeAvatarController.Motion.Tests` | `RealtimeAvatarController.Motion.Tests.EditMode` / `.PlayMode` |
| `RealtimeAvatarController.MoCap.VMC.Tests` | `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` / `.PlayMode` |
| `RealtimeAvatarController.Avatar.Builtin.Tests` | `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` / `.PlayMode` |

---

## 6. 利用者導入手順 (README)

本章の内容が `README.md` のインストールセクションとなる。

### 6.1 前提条件

- Unity 6000.3.10f1 以降
- OpenUPM CLI (任意、手動 `manifest.json` 編集でも可)

### 6.2 Step 1: OpenUPM scoped registry の追加

Unity プロジェクトの `Packages/manifest.json` を開き、`scopedRegistries` セクションに以下を追加する。

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.neuecc",
        "com.cysharp"
      ]
    }
  ]
}
```

> **既存の `scopedRegistries` がある場合**: 上記オブジェクトを配列に**追記**する (既存エントリは削除しない)。

### 6.3 Step 2: dependencies への追加

同じ `manifest.json` の `dependencies` セクションに以下を追加する。

```json
{
  "dependencies": {
    "com.cysharp.realtimeavatarcontroller": "0.1.0"
  }
}
```

### 6.4 manifest.json 完全スニペット例 (新規プロジェクト向け)

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.neuecc",
        "com.cysharp"
      ]
    }
  ],
  "dependencies": {
    "com.cysharp.realtimeavatarcontroller": "0.1.0",
    "com.unity.ugui": "2.0.0"
  }
}
```

> `com.unity.ugui` は例示用。実際のプロジェクト依存は適宜追加する。

### 6.5 Step 3: Package Manager UI での確認

1. Unity エディタの **Window > Package Manager** を開く
2. 左上のドロップダウンから **My Registries** を選択
3. `Realtime Avatar Controller` が表示されることを確認
4. **Install** ボタンをクリック (手順 6.3 で追記済みの場合はすでにインストール済みと表示される)

### 6.6 Step 4: UI サンプルのインポート

1. Package Manager の `Realtime Avatar Controller` エントリを選択
2. 右側の **Samples** セクションを展開
3. **UI Sample** の横にある **Import** ボタンをクリック
4. `Assets/Samples/Realtime Avatar Controller/<version>/UI/` にサンプルがコピーされる
5. `SampleScene.unity` を開いてデモを確認する

### 6.7 openupm-cli を使う場合 (任意・参考)

```bash
# プロジェクトルートで実行
openupm add com.cysharp.realtimeavatarcontroller
```

`openupm-cli` は scoped registry の追加と `dependencies` への追記を自動で行う。UniRx・UniTask も依存として自動解決される。

---

## 7. Domain Reload OFF 対応

### 7.1 背景

Unity Editor の **Enter Play Mode Options** で **Domain Reload** を無効化すると、Play Mode 遷移時にスクリプトドメインがリロードされなくなる。これにより:

- 静的フィールドの値が Play Mode をまたいで保持される
- `[RuntimeInitializeOnLoadMethod]` が**再実行**されるため、Registry への二重登録が発生するリスクがある

### 7.2 `RegistryLocator.ResetForTest()` の自動発火

`RegistryLocator` は `SubsystemRegistration` タイミングで静的フィールドをリセットするメソッドを保持する。

```csharp
namespace RealtimeAvatarController.Core
{
    public static class RegistryLocator
    {
        // ...

        /// <summary>
        /// Domain Reload OFF (Enter Play Mode 最適化) 設定下でも
        /// Play Mode 再起動のたびに自動呼び出しされ、Registry を空の状態にリセットする。
        /// テストコードからの直接呼び出しも許容する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
            // IFacialControllerRegistry / ILipSyncSourceRegistry も同パターン
        }
    }
}
```

**動作タイミング**:

| 状況 | `SubsystemRegistration` 発火 | 効果 |
|------|:---------------------------:|------|
| Domain Reload ON (通常設定) | Play Mode 開始時 (ドメインリセット後) | 静的フィールドはすでにリセット済みのため追加効果なし |
| Domain Reload OFF (Enter Play Mode 最適化) | Play Mode 開始時 | `ResetForTest()` でフィールドを null に戻し、以降の Factory 自己登録を正常化 |
| テストセットアップ / ティアダウン | 手動呼び出し | テスト間の Registry 汚染を防止 |

**呼び出し順序 (`SubsystemRegistration` の利用)**:

```
[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
  ↓  (ResetForTest で Registry をクリア)
[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]
  ↓  (各 Factory の自己登録)
Play Mode 開始
```

> `SubsystemRegistration` は Unity の `RuntimeInitializeLoadType` の中で**最も早い**タイミングであり、`AfterAssembliesLoaded` や `BeforeSceneLoad` よりも先に実行される。これにより Factory 自己登録より前に Registry がリセットされることが保証される。

### 7.3 Enter Play Mode Options の ProjectSettings 設定

```
ProjectSettings > Editor > Enter Play Mode Settings
  ☑ Enter Play Mode Options (Experimental)
    ☑ Reload Domain  ← OFF にする (チェックを外す)
    ☐ Reload Scene   ← ON のまま (チェックする) を推奨
```

`EditorSettings.asset` の対応フィールド:

```yaml
enterPlayModeOptionsEnabled: 1
enterPlayModeOptions: 1  # DisableDomainReload = 1
```

### 7.4 検証方法

以下の手順で Domain Reload OFF 環境での正常動作を確認する。

1. **ProjectSettings で Domain Reload OFF を有効化する**
2. **Unity Test Runner を開く** (Window > General > Test Runner)
3. **EditMode テストを実行する**: `RegistryLocator.ResetForTest()` を含むテストが PASS することを確認
4. **Play Mode を 2 回以上連続で開始・停止する**: コンソールに `RegistryConflictException` が出ないことを確認
5. **デバッグ確認用テスト (推奨)**:

```csharp
// Tests/EditMode/slot-core/ 配下に配置するテスト例
[Test]
public void ResetForTest_ClearsAllRegistries()
{
    // Arrange: 何らかの登録が存在する状態を作る
    // ...

    // Act: リセットを実行
    RegistryLocator.ResetForTest();

    // Assert: 新規インスタンスが生成されている (null でない)
    Assert.IsNotNull(RegistryLocator.ProviderRegistry);
    Assert.IsNotNull(RegistryLocator.MoCapSourceRegistry);
}
```

---

## 8. CI / ビルド検証の下地 (任意)

### 8.1 方針

初期段階では CI の構成は**任意**とする。ただし導入する場合の参考設定を以下に示す。

### 8.2 GitHub Actions 設定例 (Unity Builder)

```yaml
# .github/workflows/unity-build.yml
name: Unity Build Validation

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build:
    name: Build for StandaloneWindows64
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Library
        uses: actions/cache@v3
        with:
          path: RealtimeAvatarController/Library
          key: Library-StandaloneWindows64
          restore-keys: Library-

      - name: Build
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          unityVersion: 6000.3.10f1
          targetPlatform: StandaloneWindows64
          projectPath: RealtimeAvatarController
```

### 8.3 将来の拡張余地

| 項目 | 内容 |
|------|------|
| テスト実行 | `game-ci/unity-test-runner` を追加し EditMode / PlayMode テストを CI で実行 |
| コードカバレッジ | Unity Code Coverage パッケージ導入後、カバレッジレポートを artifact として保存 |
| OpenUPM 公開 | タグプッシュ時に `openupm-publish` Action でパッケージを公開 |

---

## 9. ディレクトリツリー完全版

リポジトリ直下からパッケージ内末端までの完全ツリー。

```
RealtimeAvatarController/   (リポジトリルート)
│
├── .github/
│   └── workflows/
│       └── unity-build.yml          # CI 設定 (任意)
│
├── .kiro/                           # Kiro Spec 管理ディレクトリ
│   ├── specs/
│   └── steering/
│
├── RealtimeAvatarController/        # Unity プロジェクトルート
│   ├── Assets/                      # プロジェクト固有アセット (パッケージ外)
│   │   └── .gitkeep
│   │
│   ├── Packages/
│   │   ├── manifest.json            # UPM 依存宣言 (scoped registry 含む)
│   │   └── com.cysharp.realtimeavatarcontroller/   # Embedded Package ルート
│   │       │
│   │       ├── package.json         # パッケージメタデータ・依存宣言
│   │       ├── README.md            # 利用者向け導入手順
│   │       ├── LICENSE
│   │       ├── CHANGELOG.md
│   │       │
│   │       ├── Runtime/             # ランタイムコード
│   │       │   ├── Core/
│   │       │   │   └── RealtimeAvatarController.Core.asmdef
│   │       │   ├── Motion/
│   │       │   │   └── RealtimeAvatarController.Motion.asmdef
│   │       │   ├── MoCap/
│   │       │   │   └── VMC/
│   │       │   │       └── RealtimeAvatarController.MoCap.VMC.asmdef
│   │       │   └── Avatar/
│   │       │       └── Builtin/
│   │       │           └── RealtimeAvatarController.Avatar.Builtin.asmdef
│   │       │
│   │       ├── Editor/              # エディタ専用コード
│   │       │   ├── Core/
│   │       │   │   └── RealtimeAvatarController.Core.Editor.asmdef
│   │       │   ├── Motion/
│   │       │   │   └── RealtimeAvatarController.Motion.Editor.asmdef
│   │       │   ├── MoCap/
│   │       │   │   └── VMC/
│   │       │   │       └── RealtimeAvatarController.MoCap.VMC.Editor.asmdef
│   │       │   └── Avatar/
│   │       │       └── Builtin/
│   │       │           └── RealtimeAvatarController.Avatar.Builtin.Editor.asmdef
│   │       │
│   │       ├── Tests/               # テストコード
│   │       │   ├── EditMode/
│   │       │   │   ├── slot-core/
│   │       │   │   │   └── RealtimeAvatarController.Core.Tests.EditMode.asmdef
│   │       │   │   ├── motion-pipeline/
│   │       │   │   │   └── RealtimeAvatarController.Motion.Tests.EditMode.asmdef
│   │       │   │   ├── mocap-vmc/
│   │       │   │   │   └── RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef
│   │       │   │   └── avatar-provider-builtin/
│   │       │   │       └── RealtimeAvatarController.Avatar.Builtin.Tests.EditMode.asmdef
│   │       │   └── PlayMode/
│   │       │       ├── slot-core/
│   │       │       │   └── RealtimeAvatarController.Core.Tests.PlayMode.asmdef
│   │       │       ├── motion-pipeline/
│   │       │       │   └── RealtimeAvatarController.Motion.Tests.PlayMode.asmdef
│   │       │       ├── mocap-vmc/
│   │       │       │   └── RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef
│   │       │       └── avatar-provider-builtin/
│   │       │           └── RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode.asmdef
│   │       │
│   │       └── Samples~/            # UPM Samples~ 機構 (自動インポートされない)
│   │           └── UI/
│   │               ├── Scenes/
│   │               │   └── SampleScene.unity
│   │               ├── Scripts/
│   │               └── RealtimeAvatarController.Samples.UI.asmdef
│   │
│   ├── ProjectSettings/
│   │   ├── ProjectVersion.txt       # Unity 6000.3.10f1
│   │   ├── ProjectSettings.asset    # スクリプティングバックエンド / API 互換レベル
│   │   └── EditorSettings.asset     # Enter Play Mode Options (Domain Reload OFF)
│   │
│   └── UserSettings/               # .gitignore 対象
│
├── .gitignore
├── .gitattributes
└── README.md                        # リポジトリ概要 (利用者向け導入手順へのリンク)
```

---

## 10. テスト asmdef 配置方針

### 10.1 配置ルール

| 項目 | 規則 |
|------|------|
| EditMode テストパス | `Tests/EditMode/<spec-name>/` |
| PlayMode テストパス | `Tests/PlayMode/<spec-name>/` |
| asmdef 命名 | `<RuntimeAsmdefName>.Tests.EditMode` / `<RuntimeAsmdefName>.Tests.PlayMode` |
| `rootNamespace` | `<RuntimeNamespace>.Tests` |
| `optionalUnityReferences` | `["TestAssemblies"]` (必須) |
| `includePlatforms` | `[]` (空配列、全プラットフォーム) |
| `autoReferenced` | `false` |
| `references` | 対応 Runtime asmdef のみ (片方向) |

### 10.2 テスト asmdef 一覧と担当

| asmdef 名 | 担当 Spec | 配置パス | 必須 |
|-----------|----------|---------|:----:|
| `RealtimeAvatarController.Core.Tests.EditMode` | slot-core | `Tests/EditMode/slot-core/` | ○ |
| `RealtimeAvatarController.Core.Tests.PlayMode` | slot-core | `Tests/PlayMode/slot-core/` | ○ |
| `RealtimeAvatarController.Motion.Tests.EditMode` | motion-pipeline | `Tests/EditMode/motion-pipeline/` | ○ |
| `RealtimeAvatarController.Motion.Tests.PlayMode` | motion-pipeline | `Tests/PlayMode/motion-pipeline/` | ○ |
| `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` | mocap-vmc | `Tests/EditMode/mocap-vmc/` | ○ |
| `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` | mocap-vmc | `Tests/PlayMode/mocap-vmc/` | ○ |
| `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` | avatar-provider-builtin | `Tests/EditMode/avatar-provider-builtin/` | ○ |
| `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` | avatar-provider-builtin | `Tests/PlayMode/avatar-provider-builtin/` | ○ |
| `RealtimeAvatarController.Samples.UI.Tests.EditMode` | ui-sample | `Tests/EditMode/ui-sample/` | 任意 |
| `RealtimeAvatarController.Samples.UI.Tests.PlayMode` | ui-sample | `Tests/PlayMode/ui-sample/` | 任意 |

### 10.3 asmdef 参照関係

```
RealtimeAvatarController.Core
        ▲
        │ (references: ["RealtimeAvatarController.Core"])
RealtimeAvatarController.Core.Tests.EditMode
RealtimeAvatarController.Core.Tests.PlayMode
```

各テスト asmdef は対応 Runtime asmdef **のみ**を参照する。例えば `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` は `RealtimeAvatarController.MoCap.VMC` のみを参照し、`RealtimeAvatarController.Core` への直接参照は持たない (Core 経由での推移的利用は可)。

### 10.4 Domain Reload OFF 環境でのテスト安全化

各テストクラスの `[SetUp]` または `[OneTimeSetUp]` / `[TearDown]` で `RegistryLocator.ResetForTest()` を呼び出し、テスト間の Registry 汚染を防止する。

```csharp
using NUnit.Framework;
using RealtimeAvatarController.Core;

[TestFixture]
public class SlotManagerTests
{
    [SetUp]
    public void SetUp()
    {
        RegistryLocator.ResetForTest();
    }

    [TearDown]
    public void TearDown()
    {
        RegistryLocator.ResetForTest();
    }

    // ... テストメソッド
}
```

### 10.5 カバレッジ目標

初期版ではカバレッジの定量目標を**設定しない**。目標値の設定は各 Spec の design / tasks フェーズで個別に検討する。

---

## 11. 公開 API サマリ (project-foundation 担当範囲)

本 Spec が確定する成果物の公開境界を記録する。具体的なクラス実装は各機能 Spec が担当する。

| 成果物 | 公開スコープ | 備考 |
|--------|------------|------|
| `package.json` | 利用者向け | パッケージ識別・依存宣言 |
| asmdef 構成 (Runtime / Editor / Tests) | 全 Spec 共通 | `contracts.md` 6 章にも記録 |
| 名前空間規約 | 全 Spec 共通 | `contracts.md` 6.2 章 |
| ディレクトリ構造 | 全 Spec 共通 | 第 9 章ツリー参照 |
| Domain Reload OFF 設計 | slot-core / 全テスト | `RegistryLocator` は slot-core が実装 |
| README 導入手順 | 利用者向け | 第 6 章参照 |
