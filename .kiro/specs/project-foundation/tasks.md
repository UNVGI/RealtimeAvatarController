# Tasks: project-foundation

> **フェーズ**: tasks  
> **言語**: ja  
> **生成日**: 2026-04-15  
> **対応 design.md**: 966 行 (§1〜§11)  
> **`/kiro:spec-run` 実行可能形式**

---

## タスク一覧

| ID | タイトル | 推定時間 | 状態 |
|----|---------|---------|------|
| T01 | Unity プロジェクトの作成と配置 | 60 分 | [ ] |
| T02 | UPM パッケージ構造の作成 (package.json) | 45 分 | [ ] |
| T03 | Packages/manifest.json に scoped registry を追加 | 30 分 | [ ] |
| T04 | ディレクトリツリーの全作成 | 60 分 | [ ] |
| T05 | Runtime asmdef 5 本の作成 | 60 分 | [ ] |
| T06 | Editor 専用 asmdef 5 本の作成 | 45 分 | [ ] |
| T07 | テスト専用 asmdef (EditMode) 4 本の作成 | 45 分 | [ ] |
| T08 | テスト専用 asmdef (PlayMode) 4 本の作成 | 45 分 | [ ] |
| T09 | Samples.UI asmdef の UniRx 直接参照追加 | 30 分 | [ ] |
| T10 | ProjectSettings.asset / EditorSettings.asset の設定 | 45 分 | [ ] |
| T11 | Domain Reload OFF 動作確認タスク | 60 分 | [ ] |
| T12 | 利用者向け README の作成 | 60 分 | [ ] |
| T13 | バージョン固定ポリシーの依存管理ドキュメント追記 | 30 分 | [ ] |
| T14 | CI 下地の設定 (任意) | 60 分 | [ ] |

---

## 詳細タスク

### T01: Unity プロジェクトの作成と配置

_Requirements: Req 1_

**目的**: Unity 6000.3.10f1 プロジェクトをリポジトリ直下 `RealtimeAvatarController/` に作成・配置する。

**作業手順**:

1. Unity Hub を起動し、`New project` をクリックする
2. Editor Version として `6000.3.10f1` を選択する (インストール済みでない場合は Unity Hub からインストールする)
3. テンプレートは `3D (Core)` または `Empty` を選択する
4. プロジェクト保存先として `<リポジトリルート>/RealtimeAvatarController/` を指定する
5. プロジェクトが `RealtimeAvatarController/ProjectSettings/ProjectVersion.txt` に以下の内容を含むことを確認する:
   ```
   m_EditorVersion: 6000.3.10f1
   ```
6. Unity エディタが起動してエラーなく表示されることを確認する
7. デフォルトシーン (`SampleScene.unity`) が `Assets/` 以下に存在することを確認する

**完了条件**:

- [ ] `RealtimeAvatarController/ProjectSettings/ProjectVersion.txt` が存在し、`m_EditorVersion: 6000.3.10f1` を含む
- [ ] Unity Hub からプロジェクトを開いたときにエラーが出ない
- [ ] `RealtimeAvatarController/Assets/` ディレクトリが存在する

---

### T02: UPM パッケージ構造の作成 (package.json)

_Requirements: Req 2, Req 8_

**目的**: UPM 配布可能なパッケージ雛形を `Packages/com.hidano.realtimeavatarcontroller/` に作成する。

**作業手順**:

1. `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/` ディレクトリを作成する
2. 以下の内容で `package.json` を作成する:

```json
{
  "name": "com.hidano.realtimeavatarcontroller",
  "displayName": "Realtime Avatar Controller",
  "version": "0.1.0",
  "unity": "6000.3",
  "unityRelease": "10f1",
  "description": "Runtime avatar controller for VTuber use cases. Provides slot-based MoCap source management, avatar provider abstraction, and motion pipeline for Unity.",
  "author": {
    "name": "Hidano",
    "email": "n.hidano@hidano-dev.com",
    "url": "https://github.com/NHidano"
  },
  "license": "MIT",
  "dependencies": {
    "com.neuecc.unirx": "7.1.0",
    "com.cysharp.unitask": "2.5.10",
    "com.hidano.uosc": "1.0.0"
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

3. `LICENSE` ファイルを MIT テキストで作成する
4. `CHANGELOG.md` を作成し、`## [0.1.0] - 2026-04-15` の初期エントリを記載する

**完了条件**:

- [ ] `Packages/com.hidano.realtimeavatarcontroller/package.json` が存在する
- [ ] `package.json` の `name` が `com.hidano.realtimeavatarcontroller` である
- [ ] `dependencies` に `com.neuecc.unirx: "7.1.0"`・`com.cysharp.unitask: "2.5.10"`・`com.hidano.uosc: "1.0.0"` の 3 本が exact version で宣言されている
- [ ] `samples` フィールドに UI Sample エントリが存在する
- [ ] `LICENSE` ファイルが存在する

---

### T03: Packages/manifest.json に scoped registry を追加

_Requirements: Req 8, Req 9_

**目的**: 開発用 `Packages/manifest.json` に OpenUPM および npm (hidano) の scoped registry を追加し、依存パッケージが解決できる状態にする。

**作業手順**:

1. `RealtimeAvatarController/Packages/manifest.json` を開く (Unity プロジェクト作成後に自動生成されている)
2. `scopedRegistries` セクションに以下の 2 レジストリを追加する:

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
    },
    {
      "name": "npm (hidano)",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "com.hidano"
      ]
    }
  ]
}
```

3. 既存の `dependencies` エントリは変更せず、上記 `scopedRegistries` を追記のみ行う
4. Unity エディタを再起動し、Package Manager > My Registries から `Realtime Avatar Controller` が表示されることを確認する

**完了条件**:

- [ ] `manifest.json` に `scopedRegistries` セクションが存在する
- [ ] OpenUPM (`https://package.openupm.com`、scopes: `com.neuecc`, `com.cysharp`) が登録されている
- [ ] npm (hidano) (`https://registry.npmjs.com`、scopes: `com.hidano`) が登録されている
- [ ] Unity Package Manager > My Registries に `Realtime Avatar Controller` が表示される

---

### T04: ディレクトリツリーの全作成

_Requirements: Req 2, Req 3, Req 5_

**目的**: design.md §9 に定義されたディレクトリ構造を完全に作成する。各ディレクトリに `.gitkeep` を配置して空ディレクトリを git 追跡対象にする。

**作業手順**:

パッケージルート `RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/` 以下に以下のディレクトリを作成する:

```
Runtime/
  Core/
  Motion/
  MoCap/
    VMC/
  Avatar/
    Builtin/
Editor/
  Core/
  Motion/
  MoCap/
    VMC/
  Avatar/
    Builtin/
Tests/
  EditMode/
    slot-core/
    motion-pipeline/
    mocap-vmc/
    avatar-provider-builtin/
  PlayMode/
    slot-core/
    motion-pipeline/
    mocap-vmc/
    avatar-provider-builtin/
Samples~/
  UI/
    Scenes/
    Scripts/
```

各末端ディレクトリ (C# ファイルが存在しないもの) に `.gitkeep` を配置する。

リポジトリルートに以下も作成する:
- `.github/workflows/` ディレクトリ (T14 の CI タスク用。空でよい)

**完了条件**:

- [ ] `Runtime/Core/`・`Runtime/Motion/`・`Runtime/MoCap/VMC/`・`Runtime/Avatar/Builtin/` が存在する
- [ ] `Editor/Core/`・`Editor/Motion/`・`Editor/MoCap/VMC/`・`Editor/Avatar/Builtin/` が存在する
- [ ] `Tests/EditMode/slot-core/`・`Tests/EditMode/motion-pipeline/`・`Tests/EditMode/mocap-vmc/`・`Tests/EditMode/avatar-provider-builtin/` が存在する
- [ ] `Tests/PlayMode/slot-core/`・`Tests/PlayMode/motion-pipeline/`・`Tests/PlayMode/mocap-vmc/`・`Tests/PlayMode/avatar-provider-builtin/` が存在する
- [ ] `Samples~/UI/Scenes/`・`Samples~/UI/Scripts/` が存在する
- [ ] 空ディレクトリに `.gitkeep` が配置されている

---

### T05: Runtime asmdef 5 本の作成

_Requirements: Req 3_

**目的**: 機能部 4 本 + UI サンプル 1 本の Runtime asmdef を JSON ファイルとして配置する。

**作業手順**:

以下の各ファイルを指定パスに作成する。

#### 5-1. `Runtime/Core/RealtimeAvatarController.Core.asmdef`

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

#### 5-2. `Runtime/Motion/RealtimeAvatarController.Motion.asmdef`

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

#### 5-3. `Runtime/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.asmdef`

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

#### 5-4. `Runtime/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.asmdef`

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

#### 5-5. `Samples~/UI/RealtimeAvatarController.Samples.UI.asmdef`

> **注意**: T09 でこの asmdef の `references` に `UniRx` を追加する。T05 では下記 JSON で作成し、T09 で修正する。

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

**完了条件**:

- [ ] 上記 5 本の `.asmdef` ファイルが指定パスに存在する
- [ ] `RealtimeAvatarController.Core` の `references` に `UniRx` と `UniTask` が含まれている
- [ ] `RealtimeAvatarController.Motion`・`MoCap.VMC`・`Avatar.Builtin` の `references` に `UniRx` が直接含まれていない
- [ ] `RealtimeAvatarController.Samples.UI` の `autoReferenced` が `false` である
- [ ] Unity エディタで asmdef エラー (コンパイルエラー) が出ないことを確認する

---

### T06: Editor 専用 asmdef 5 本の作成

_Requirements: Req 3_

**目的**: 各機能アセンブリに対応する Editor 専用 asmdef を配置する。

**作業手順**:

以下の各ファイルを指定パスに作成する。

#### 6-1. `Editor/Core/RealtimeAvatarController.Core.Editor.asmdef`

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

#### 6-2. `Editor/Motion/RealtimeAvatarController.Motion.Editor.asmdef`

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

#### 6-3. `Editor/MoCap/VMC/RealtimeAvatarController.MoCap.VMC.Editor.asmdef`

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

#### 6-4. `Editor/Avatar/Builtin/RealtimeAvatarController.Avatar.Builtin.Editor.asmdef`

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

#### 6-5. `Samples~/UI/Editor/RealtimeAvatarController.Samples.UI.Editor.asmdef`

> **備考**: ui-sample Spec 側で不要と判断した場合は作成しなくてよい (任意)。ただし `Samples~/UI/Editor/` ディレクトリ自体は T04 で作成済みであること。

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

**完了条件**:

- [ ] 上記 5 本の Editor asmdef ファイルが指定パスに存在する (5-5 は任意)
- [ ] 全 Editor asmdef の `includePlatforms` が `["Editor"]` である
- [ ] 各 Editor asmdef の `references` に対応する Runtime asmdef **のみ**が含まれている (逆依存なし)
- [ ] Runtime asmdef が Editor asmdef を参照していないことを確認する
- [ ] Unity エディタで asmdef エラーが出ないことを確認する

---

### T07: テスト専用 asmdef (EditMode) 4 本の作成

_Requirements: Req 3_

**目的**: 必須 4 Spec の EditMode テスト asmdef を配置する。`optionalUnityReferences: ["TestAssemblies"]` の設定を確実に含める。

**作業手順**:

以下の各ファイルを指定パスに作成する。

#### 7-1. `Tests/EditMode/slot-core/RealtimeAvatarController.Core.Tests.EditMode.asmdef`

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

#### 7-2. `Tests/EditMode/motion-pipeline/RealtimeAvatarController.Motion.Tests.EditMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Motion.Tests.EditMode",
  "rootNamespace": "RealtimeAvatarController.Motion.Tests",
  "references": [
    "RealtimeAvatarController.Motion"
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

#### 7-3. `Tests/EditMode/mocap-vmc/RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.MoCap.VMC.Tests.EditMode",
  "rootNamespace": "RealtimeAvatarController.MoCap.VMC.Tests",
  "references": [
    "RealtimeAvatarController.MoCap.VMC"
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

#### 7-4. `Tests/EditMode/avatar-provider-builtin/RealtimeAvatarController.Avatar.Builtin.Tests.EditMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Avatar.Builtin.Tests.EditMode",
  "rootNamespace": "RealtimeAvatarController.Avatar.Builtin.Tests",
  "references": [
    "RealtimeAvatarController.Avatar.Builtin"
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

**完了条件**:

- [ ] 上記 4 本の EditMode テスト asmdef が指定パスに存在する
- [ ] 全 asmdef に `"optionalUnityReferences": ["TestAssemblies"]` が設定されている (欠落禁止)
- [ ] 全 asmdef の `autoReferenced` が `false` である
- [ ] 各 asmdef の `references` に対応する Runtime asmdef **のみ**が含まれている
- [ ] Unity Test Runner (Window > General > Test Runner) の EditMode タブに各 asmdef が認識されることを確認する

---

### T08: テスト専用 asmdef (PlayMode) 4 本の作成

_Requirements: Req 3_

**目的**: 必須 4 Spec の PlayMode テスト asmdef を配置する。T07 と同様の設定で PlayMode 系を作成する。

**作業手順**:

以下の各ファイルを指定パスに作成する。

#### 8-1. `Tests/PlayMode/slot-core/RealtimeAvatarController.Core.Tests.PlayMode.asmdef`

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

#### 8-2. `Tests/PlayMode/motion-pipeline/RealtimeAvatarController.Motion.Tests.PlayMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Motion.Tests.PlayMode",
  "rootNamespace": "RealtimeAvatarController.Motion.Tests",
  "references": [
    "RealtimeAvatarController.Motion"
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

#### 8-3. `Tests/PlayMode/mocap-vmc/RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.MoCap.VMC.Tests.PlayMode",
  "rootNamespace": "RealtimeAvatarController.MoCap.VMC.Tests",
  "references": [
    "RealtimeAvatarController.MoCap.VMC"
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

#### 8-4. `Tests/PlayMode/avatar-provider-builtin/RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode.asmdef`

```json
{
  "name": "RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode",
  "rootNamespace": "RealtimeAvatarController.Avatar.Builtin.Tests",
  "references": [
    "RealtimeAvatarController.Avatar.Builtin"
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

**完了条件**:

- [ ] 上記 4 本の PlayMode テスト asmdef が指定パスに存在する
- [ ] 全 asmdef に `"optionalUnityReferences": ["TestAssemblies"]` が設定されている (欠落禁止)
- [ ] 全 asmdef の `autoReferenced` が `false` である
- [ ] Unity Test Runner の PlayMode タブに各 asmdef が認識されることを確認する

---

### T09: Samples.UI asmdef の UniRx 直接参照追加

_Requirements: Req 3 (AC-10 例外規則), contracts.md §6.1_

**目的**: validation-design.md で指摘された Minor #3 に対処する。`RealtimeAvatarController.Samples.UI.asmdef` の `references` に `UniRx` を追加し、contracts.md §6.1 の「Samples.UI は `.ObserveOnMainThread()` のため UniRx を直接参照する」例外規則に準拠させる。

**背景**: design.md 4.1.5 章の JSON には `UniRx` が含まれていないが、contracts.md §6.1 が上位確定値として UniRx の直接参照を明示例外として許容している。実装フェーズでの誤実装を防ぐため、本タスクで確実に修正する。

**作業手順**:

1. T05 で作成した `Samples~/UI/RealtimeAvatarController.Samples.UI.asmdef` を開く
2. `references` 配列に `"UniRx"` を追加する:

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

3. `UniTask` の直接参照は**追加しない** (contracts.md §6.1 の規定どおり: UniTask の Samples.UI 直接参照は現時点で技術的必要がないため不要)

**完了条件**:

- [ ] `Samples~/UI/RealtimeAvatarController.Samples.UI.asmdef` の `references` に `"UniRx"` が含まれている
- [ ] `references` に `"UniTask"` が含まれていない (意図的な非追加)
- [ ] Unity エディタでコンパイルエラーが発生しないことを確認する

---

### T10: ProjectSettings.asset / EditorSettings.asset の設定

_Requirements: Req 10_

**目的**: design.md §2.2 で定義された ProjectSettings を適用し、Enter Play Mode Options で Domain Reload OFF を有効化する。

**作業手順**:

1. Unity エディタで **Edit > Project Settings > Editor** を開く
2. **Enter Play Mode Settings** セクションで以下を設定する:
   - `Enter Play Mode Options (Experimental)` のチェックを **ON** にする
   - `Reload Domain` のチェックを **OFF** にする (Domain Reload を無効化)
   - `Reload Scene` のチェックは **ON** のまま残す
3. `RealtimeAvatarController/ProjectSettings/EditorSettings.asset` を開き、以下のフィールドが反映されていることを YAML で確認する:
   ```yaml
   enterPlayModeOptionsEnabled: 1
   enterPlayModeOptions: 1
   ```
4. `ProjectSettings.asset` で以下を確認する:
   - `scriptingBackend` が `Mono` (0) に設定されている
   - `apiCompatibilityLevel` が `.NET Standard 2.1` に設定されている
5. 変更を保存し、`ProjectSettings/EditorSettings.asset` が git 差分に含まれることを確認する

**完了条件**:

- [ ] `EditorSettings.asset` に `enterPlayModeOptionsEnabled: 1` が設定されている
- [ ] `EditorSettings.asset` に `enterPlayModeOptions: 1` (DisableDomainReload) が設定されている
- [ ] Unity エディタの Enter Play Mode Settings パネルで `Reload Domain` がチェックされていない状態になっている

---

### T11: Domain Reload OFF 動作確認タスク

_Requirements: Req 10_

**目的**: T10 で有効化した Domain Reload OFF 設定下で、Registry の初期化が正常に機能することを手動手順で確認する。

> **前提**: slot-core Spec で `RegistryLocator.ResetForTest()` が実装された後に実施する。本タスクは実装コードが存在しない段階では「検証手順の確立」として tasks.md に定義し、slot-core 実装完了後に実行する。

**作業手順**:

以下の検証手順を design.md §7.4 の 5 ステップに従い実施する。

1. **Domain Reload OFF が有効であることを確認する**: T10 完了後、`EditorSettings.asset` の `enterPlayModeOptions: 1` を確認する
2. **Unity Test Runner を開く**: Window > General > Test Runner
3. **EditMode テストを実行する**: `RealtimeAvatarController.Core.Tests.EditMode` の `RegistryLocator.ResetForTest()` を含むテストが PASS することを確認する
4. **Play Mode を 2 回以上連続で開始・停止する**: コンソールに `RegistryConflictException` が表示されないことを確認する
5. **デバッグ確認用テスト (推奨)**: `Tests/EditMode/slot-core/` 配下に以下のテストクラスを配置して実行する:

```csharp
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    [TestFixture]
    public class RegistryLocatorResetTests
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

        [Test]
        public void ResetForTest_AfterReset_RegistriesAreAccessible()
        {
            // Act
            RegistryLocator.ResetForTest();

            // Assert: リセット後も Registry に正常アクセスできる
            Assert.DoesNotThrow(() => { var _ = RegistryLocator.ProviderRegistry; });
            Assert.DoesNotThrow(() => { var _ = RegistryLocator.MoCapSourceRegistry; });
        }
    }
}
```

**完了条件**:

- [ ] Domain Reload OFF 設定下で Play Mode を 2 回連続開始・停止して `RegistryConflictException` が発生しない
- [ ] `RegistryLocator.ResetForTest()` を呼び出すテストが PASS する
- [ ] EditMode テストが Unity Test Runner で正常に認識・実行される

---

### T12: 利用者向け README の作成

_Requirements: Req 9_

**目的**: パッケージルートに `README.md` を作成し、design.md §6 の導入手順を完全に記載する。

**作業手順**:

`RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller/README.md` を以下の構成で作成する:

1. **パッケージ概要セクション**: パッケージの用途 (VTuber アバター制御・Slot ベース MoCap ソース管理・アバタープロバイダ抽象化・モーションパイプライン) を説明する
2. **前提条件セクション**: Unity 6000.3.10f1 以降、OpenUPM CLI (任意) を記載する
3. **Step 1: scoped registry の追加**: 以下の 2 つのレジストリを `manifest.json` に追加する手順を記載する:
   - OpenUPM (`https://package.openupm.com`、scopes: `com.neuecc`, `com.cysharp`)
   - npm (hidano) (`https://registry.npmjs.com`、scopes: `com.hidano`)
   - 既存 `scopedRegistries` への追記方式で説明し、既存エントリを破壊しないことを明記する
4. **Step 2: dependencies への追加**: `manifest.json` の `dependencies` に `com.hidano.realtimeavatarcontroller: "0.1.0"` を追加する手順
5. **Step 3: manifest.json 完全スニペット例**: 新規プロジェクト向けの完全 JSON 例 (design.md §6.4 を使用)
6. **Step 4: Package Manager UI での確認**: Window > Package Manager > My Registries から確認する手順
7. **Step 5: UI サンプルのインポート**: Package Manager の Samples セクションから UI Sample をインポートする手順
8. **補足: openupm-cli を使う場合 (任意)**: `openupm add com.hidano.realtimeavatarcontroller` のコマンド例と、npm (hidano) registry の手動追加が必要な旨の注意書き
9. **補足: git URL によるインストール (任意)**: `?path=` パラメータ付きの git URL と、依存パッケージの手動追加が必要な旨の注意書き
10. **バージョン固定ポリシーセクション**: 全依存パッケージを exact version で固定している旨と、アップグレード時は `package.json` を手動更新する方針を記載する

**完了条件**:

- [ ] `README.md` がパッケージルートに存在する
- [ ] 2 つの scoped registry (OpenUPM + npm (hidano)) の追加手順が記載されている
- [ ] manifest.json の完全スニペット例 (新規プロジェクト向け) が含まれている
- [ ] Package Manager UI での確認手順が含まれている
- [ ] UI Sample のインポート手順が含まれている
- [ ] git URL + `?path=` パラメータによるインストール手順が含まれている
- [ ] バージョン固定ポリシーの説明が含まれている

---

### T13: バージョン固定ポリシーの依存管理ドキュメント追記

_Requirements: Req 2 (validation-design.md Minor #1 引き継ぎ)_

**目的**: design.md §3.2 の「バージョン固定ポリシー (Minor #1 対応)」引き継ぎ事項を、利用者・開発者向けドキュメントに明記する。

**作業手順**:

1. T12 で作成した `README.md` の「バージョン固定ポリシー」セクション (または末尾の「依存パッケージ管理」セクション) に以下を追記する:
   - 全依存パッケージ (`com.neuecc.unirx: 7.1.0`・`com.cysharp.unitask: 2.5.10`・`com.hidano.uosc: 1.0.0`) を exact version で固定している旨
   - patch バージョンのアップグレード適用方針: セキュリティ修正等が必要な場合はパッケージ管理者が `package.json` を更新してリリースする
   - 利用者側でバージョンを変更する場合は `manifest.json` の `dependencies` を直接上書きする方法を案内する

2. `CHANGELOG.md` に依存パッケージのバージョン根拠を記載するセクションを追加する:
   ```markdown
   ## [0.1.0] - 2026-04-15
   
   ### 依存パッケージ
   
   | パッケージ | バージョン | 取得元 | 備考 |
   |------------|-----------|--------|------|
   | com.neuecc.unirx | 7.1.0 | OpenUPM | 2024 年時点の最新安定版。NuGet 依存なし |
   | com.cysharp.unitask | 2.5.10 | OpenUPM | 2024 年時点の最新安定版 |
   | com.hidano.uosc | 1.0.0 | npm (hidano) | VMC OSC 受信用。MIT ライセンス。SO_REUSEADDR 有効版 |
   ```

**完了条件**:

- [ ] `README.md` にバージョン固定ポリシーの説明が記載されている
- [ ] `CHANGELOG.md` に依存パッケージのバージョン根拠表が記載されている
- [ ] patch バージョンアップグレードの対応方針が明記されている

---

### T14: CI 下地の設定 (任意)

_Requirements: Req 7_

**目的**: GitHub Actions による Unity ビルド検証の最小設定ファイルを配置する。本タスクは任意であり、初期段階でスキップしてもよい。

**作業手順**:

1. `.github/workflows/` ディレクトリが存在することを確認する (T04 で作成済み)
2. `.github/workflows/unity-build.yml` を以下の内容で作成する:

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

3. GitHub リポジトリ設定で Secrets に `UNITY_LICENSE`・`UNITY_EMAIL`・`UNITY_PASSWORD` を登録するよう README または `.github/` の補足ドキュメントに記載する
4. 将来拡張予定を `README.md` の CI セクションに記載する:
   - `game-ci/unity-test-runner` による EditMode / PlayMode テスト実行
   - Unity Code Coverage パッケージ導入後のカバレッジレポート保存
   - タグプッシュ時の OpenUPM への自動公開

**完了条件**:

- [ ] `.github/workflows/unity-build.yml` が存在する (任意)
- [ ] Unity バージョンとプロジェクトパスが `6000.3.10f1` / `RealtimeAvatarController` に設定されている
- [ ] GitHub Actions が push 時に起動する設定になっている

---

## タスク依存関係

```
T01 (Unity プロジェクト作成)
  └─ T02 (package.json 作成)
  └─ T03 (manifest.json scoped registry)
  └─ T04 (ディレクトリツリー作成)
       ├─ T05 (Runtime asmdef 5 本)
       │    └─ T09 (Samples.UI UniRx 追加)
       ├─ T06 (Editor asmdef 5 本)
       ├─ T07 (Tests EditMode asmdef 4 本)
       └─ T08 (Tests PlayMode asmdef 4 本)
T10 (ProjectSettings 設定) ← T01 に依存
T11 (Domain Reload 動作確認) ← T10 + T07 + T08 + slot-core 実装完了後
T12 (README 作成) ← T02 完了後
T13 (バージョン固定ポリシー追記) ← T12 完了後
T14 (CI 下地、任意) ← T04 完了後
```

---

## 他 Spec との順序依存

> **project-foundation は他 5 Spec すべての実装前提基盤である。**

本 Spec (project-foundation) が完了してから以下の Spec の実装フェーズを開始すること。

| Spec 名 | 依存タスク | 備考 |
|---------|----------|------|
| `slot-core` | T01〜T08 (特に T05 Runtime asmdef, T07/T08 Tests asmdef) | `RealtimeAvatarController.Core` asmdef と Tests asmdef が必要 |
| `motion-pipeline` | T01〜T08 | `RealtimeAvatarController.Motion` asmdef と Tests asmdef が必要 |
| `mocap-vmc` | T01〜T08 | `RealtimeAvatarController.MoCap.VMC` asmdef と Tests asmdef が必要 |
| `avatar-provider-builtin` | T01〜T08 | `RealtimeAvatarController.Avatar.Builtin` asmdef と Tests asmdef が必要 |
| `ui-sample` | T01〜T09 (特に T09 Samples.UI UniRx 追加) | `RealtimeAvatarController.Samples.UI` asmdef の UniRx 参照が必要 |

---

## 備考: Spec 間引き継ぎ事項

- **slot-core 向け**: `RegistryLocator.ResetForTest()` の実装完了後、T11 (Domain Reload OFF 動作確認) を再実行すること
- **ui-sample 向け**: T09 完了により `Samples.UI.asmdef` に `UniRx` が追加済みであることを確認してから開発を開始すること
- **全 Spec 向け**: 各 Spec の asmdef (Runtime / Editor / Tests) の JSON 雛形は本 Spec の T05〜T08 で作成済み。実装フェーズでは `.cs` ファイルを該当ディレクトリに追加するだけでよい
