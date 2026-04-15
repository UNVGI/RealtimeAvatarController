# Requirements Document

## Introduction

本ドキュメントは `project-foundation` Spec の要件を定義する。`project-foundation` は Realtime Avatar Controller における最上位基盤 Spec であり、Unity プロジェクトの作成・配置・UPM パッケージ構造の整備・アセンブリ分割・名前空間規約の確定を担う Wave 2 先行 Spec である。本 Spec は他 5 Spec すべての前提基盤を提供し、具体的なクラス実装は行わない。

## Boundary Context

- **In scope**:
  - Unity 6000.3.10f1 プロジェクトの作成とリポジトリ配置
  - UPM 配布可能なパッケージ雛形 (`package.json` 同梱)
  - アセンブリ定義ファイル (asmdef) の構成設計: 機能部 / UI サンプル分離
  - `Samples~` 機構による UI サンプル同梱ルートの設定
  - 名前空間規約の確定
  - 最小限の CI / ビルド検証の下地 (任意)
  - `_shared/contracts.md` の 6.1 章・6.2 章の確定
- **Out of scope**:
  - 具体的なクラス・インターフェースの実装 (各機能 Spec 担当)
  - UI サンプルの中身 (ui-sample Spec)
  - Slot 管理・MoCap・アバター供給の実装 (各担当 Spec)
- **Adjacent expectations**:
  - 本 Spec が確定した asmdef 構成を、他 5 Spec の設計フェーズが参照する
  - `slot-core` は `RealtimeAvatarController.Core` アセンブリに収まる前提で設計する
  - `ui-sample` は `RealtimeAvatarController.Samples.UI` アセンブリを `Samples~` 以下に配置する前提で設計する

---

## Requirements

### Requirement 1: Unity プロジェクトの作成と配置

**Objective:** As a 開発者, I want Unity 6000.3.10f1 プロジェクトがリポジトリ直下の所定ディレクトリに配置されていること, so that チーム全員が同一バージョンの Unity で開発を開始できる。

#### Acceptance Criteria

1. The Unity project shall Unity エディタバージョン `6000.3.10f1` をターゲットとして作成される。
2. The Unity project shall リポジトリルート直下の `RealtimeAvatarController/` ディレクトリに配置される。
3. The Unity project shall Unity Package Manager (UPM) でパッケージとして配布可能な構造を持つルートディレクトリを含む。
4. When 開発者が Unity Hub からプロジェクトを開いた場合, the Unity project shall エラーなく起動できる。

---

### Requirement 2: UPM パッケージ雛形

**Objective:** As a パッケージ利用者, I want UPM で直接インストール可能なパッケージ構造が整備されていること, so that 外部プロジェクトから Realtime Avatar Controller を依存として追加できる。

#### Acceptance Criteria

1. The package shall UPM の仕様に準拠した `package.json` ファイルをパッケージルートに配置する。
2. The `package.json` shall パッケージ名 (`name`)・バージョン (`version`)・表示名 (`displayName`)・説明 (`description`)・Unity 最小バージョン (`unity`) の各フィールドを含む。
3. The package shall `Runtime/`・`Editor/` のディレクトリ構造に従い、ランタイムコードとエディタ拡張コードを分離して配置できる。
4. The package shall UPM の `Samples~` 機構に対応したサンプルディレクトリ構造を提供する (詳細は Requirement 5 を参照)。
5. When git URL 経由で UPM インストールを実行した場合, the package shall Unity プロジェクトに正常にインポートされる。
6. The `package.json` shall `dependencies` フィールドに `com.neuecc.unirx` を最新安定版として宣言する (具体バージョンは design フェーズで確定する)。

---

### Requirement 3: アセンブリ定義ファイル (asmdef) の構成

**Objective:** As a 開発者, I want 機能部とUIサンプルがアセンブリレベルで分離されていること, so that 機能部が UI フレームワークに依存せずビルドでき、個別機能の変更が他アセンブリに影響しない。

#### Acceptance Criteria

1. The project shall 以下の **Runtime** asmdef ファイルを定義する:
   - `RealtimeAvatarController.Core` — Slot 抽象・各公開インターフェース群 (slot-core 担当)
   - `RealtimeAvatarController.Motion` — モーション中立表現・パイプライン (motion-pipeline 担当)
   - `RealtimeAvatarController.MoCap.VMC` — VMC OSC 受信具象実装 (mocap-vmc 担当)
   - `RealtimeAvatarController.Avatar.Builtin` — ビルトインアバター供給具象実装 (avatar-provider-builtin 担当)
   - `RealtimeAvatarController.Samples.UI` — UI サンプル (ui-sample 担当、`Samples~` 以下に配置)
2. The project shall 以下の **Editor 専用** asmdef ファイルを定義する (各機能 Spec の実装フェーズで作成する):
   - `RealtimeAvatarController.Core.Editor` — Core アセンブリ向けエディタ拡張 (slot-core 担当)
   - `RealtimeAvatarController.Motion.Editor` — Motion アセンブリ向けエディタ拡張 (motion-pipeline 担当)
   - `RealtimeAvatarController.MoCap.VMC.Editor` — VMC アセンブリ向けエディタ拡張 (mocap-vmc 担当)
   - `RealtimeAvatarController.Avatar.Builtin.Editor` — Avatar.Builtin アセンブリ向けエディタ拡張 (avatar-provider-builtin 担当)
   - `RealtimeAvatarController.Samples.UI.Editor` — UI サンプル向けエディタ拡張 (ui-sample 担当、必要な場合)
3. The Editor asmdef files shall `includePlatforms` に `Editor` のみを指定し、対応する Runtime asmdef を `references` に追加する片方向依存とする。Runtime asmdef は Editor asmdef を参照しない。
4. The Editor asmdef files shall `[UnityEditor.InitializeOnLoadMethod]` など `UnityEditor` 名前空間の API の使用を許容する。これにより Factory 自己登録の Editor 起動時フックを Editor asmdef 内に配置できる。
5. When Runtime asmdef 内に `UnityEditor` API を配置する必要がある場合, the project shall `#if UNITY_EDITOR` による条件コンパイルを代替手段として許容する (各 Spec の判断に委ねる)。
6. The `RealtimeAvatarController.Samples.UI` asmdef shall 機能部アセンブリ (`RealtimeAvatarController.Core` 等) を参照できるが、機能部アセンブリは `RealtimeAvatarController.Samples.UI` を参照しない (一方向依存)。
7. The asmdef files shall `Packages/` または `Assets/` 内の適切な配置パスに対応した名前空間のディレクトリに配置される。
8. When 機能部アセンブリのみを対象としたビルドを実行した場合, the build shall UI フレームワーク (UGUI / UIToolkit 等) への依存なくコンパイルが成功する。
9. Each asmdef shall `rootNamespace` フィールドを `contracts.md` 6.2 章で確定した名前空間規約に従い設定する。
10. The `RealtimeAvatarController.Core` asmdef shall `references` に UniRx (`UniRx` アセンブリ名) を追加し、UniRx が提供する `IObservable<T>` 拡張・`Subject<T>` 等を直接利用できるようにする。
11. The asmdef files for `RealtimeAvatarController.Motion`・`RealtimeAvatarController.MoCap.VMC`・`RealtimeAvatarController.Avatar.Builtin` shall UniRx の asmdef を直接 `references` に追加せず、`RealtimeAvatarController.Core` 経由で間接的に UniRx の型を利用する。ただし各アセンブリが UniRx の拡張メソッドを直接呼び出す必要が生じた場合は design フェーズで要否を判断する。
12. The project shall 以下の **テスト専用** asmdef ファイルを定義する (各機能 Spec の実装フェーズで作成する。**slot-core / motion-pipeline / mocap-vmc / avatar-provider-builtin の 4 Spec は必須**、ui-sample は任意):

    | asmdef 名 | 担当 Spec | 配置パス (パッケージルート相対) | 備考 |
    |-----------|----------|-------------------------------|------|
    | `RealtimeAvatarController.Core.Tests.EditMode` | slot-core | `Tests/EditMode/slot-core/` | EditMode テスト (必須) |
    | `RealtimeAvatarController.Core.Tests.PlayMode` | slot-core | `Tests/PlayMode/slot-core/` | PlayMode テスト (必須) |
    | `RealtimeAvatarController.Motion.Tests.EditMode` | motion-pipeline | `Tests/EditMode/motion-pipeline/` | EditMode テスト (必須) |
    | `RealtimeAvatarController.Motion.Tests.PlayMode` | motion-pipeline | `Tests/PlayMode/motion-pipeline/` | PlayMode テスト (必須) |
    | `RealtimeAvatarController.MoCap.VMC.Tests.EditMode` | mocap-vmc | `Tests/EditMode/mocap-vmc/` | EditMode テスト (必須) |
    | `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` | mocap-vmc | `Tests/PlayMode/mocap-vmc/` | PlayMode テスト (必須) |
    | `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode` | avatar-provider-builtin | `Tests/EditMode/avatar-provider-builtin/` | EditMode テスト (必須) |
    | `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode` | avatar-provider-builtin | `Tests/PlayMode/avatar-provider-builtin/` | PlayMode テスト (必須) |
    | `RealtimeAvatarController.Samples.UI.Tests.EditMode` | ui-sample | `Tests/EditMode/ui-sample/` | EditMode テスト (**任意**) |
    | `RealtimeAvatarController.Samples.UI.Tests.PlayMode` | ui-sample | `Tests/PlayMode/ui-sample/` | PlayMode テスト (**任意**) |

13. The test asmdef files shall 以下の設定を持つ:
    - `optionalUnityReferences` に `TestAssemblies` を設定し、Unity Test Runner の NUnit 参照を有効化する
    - `includePlatforms` は `[]` (空配列) とし、全プラットフォームを対象とする (EditMode / PlayMode の制御は Unity Test Runner が行う)
    - 対応する Runtime asmdef のみを `references` に追加する片方向依存とする
    - テスト asmdef 間の相互参照を禁止する
14. The test asmdef name convention shall `<RuntimeAsmdefName>.Tests.EditMode` / `<RuntimeAsmdefName>.Tests.PlayMode` の形式に従う (例: `RealtimeAvatarController.Core` の EditMode テストは `RealtimeAvatarController.Core.Tests.EditMode`)。
15. The project shall テストコードのカバレッジ定量目標を**初期版では設定しない**。カバレッジ目標の設定は design / tasks フェーズにて改めて検討する。

---

### Requirement 10: Domain Reload OFF 設定への対応

**Objective:** As a 開発者, I want Unity Editor の Enter Play Mode Options (Domain Reload OFF) を有効化した状態でも本システムが正常に動作すること, so that Play Mode 遷移の高速化を享受しながら Registry や Factory の初期化が破綻しない。

#### Acceptance Criteria

1. The project shall Unity Editor の Enter Play Mode Options で Domain Reload を無効化した状態 (Enter Play Mode 最適化) においても、Registry の初期化・Factory 自己登録が正常に機能することを要件とする。
2. The `RegistryLocator` shall `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` を付与した `ResetForTest()` メソッド (または同等の Reset 機構) を持ち、Domain Reload OFF 環境でのプレイモード再起動時に Registry を自動リセットする。これにより Factory の `[RuntimeInitializeOnLoadMethod]` 再実行時に同一 typeId の二重登録が発生することを防ぐ。
3. The project shall Domain Reload OFF 設定下での以下の動作を確認するテストを推奨する:
   - Editor 上でプレイモード開始 → 停止 → 再開を連続して実行した際に `RegistryConflictException` が発生しないこと
   - `RegistryLocator.ResetForTest()` 呼び出し後、Registry が空の状態に戻ること
   - 再起動後に Factory の自己登録が再度正常に実行されること
4. The `RegistryLocator.ResetForTest()` shall テストコードからの直接呼び出しも許容し、EditMode テスト asmdef および PlayMode テスト asmdef の両方から呼び出して Registry 差し替え・リセットに利用できる。具体的には、各 `*.Tests.EditMode` asmdef と `*.Tests.PlayMode` asmdef のセットアップ / ティアダウンフェーズで呼び出すことで Domain Reload OFF 環境下でも二重登録が発生しないことを保証する。
5. When Domain Reload ON の通常設定下では, the Registry shall Domain Reload 時に自動的にリセットされるため `ResetForTest()` の明示呼び出しは不要である。ただし明示呼び出しによる副作用は生じない設計とする。

---

### Requirement 4: 名前空間規約の確定

**Objective:** As a 開発者, I want プロジェクト全体に適用される名前空間規約が文書化されていること, so that コード記述時に一貫した名前空間を選択できる。

#### Acceptance Criteria

1. The project shall ルート名前空間を `RealtimeAvatarController` として確定する。
2. The namespace convention shall 各 Spec の担当領域を以下のサブ名前空間にマッピングする:
   - `RealtimeAvatarController.Core` — Slot・各公開インターフェース
   - `RealtimeAvatarController.Motion` — モーションデータ・パイプライン
   - `RealtimeAvatarController.MoCap.VMC` — VMC 受信実装
   - `RealtimeAvatarController.Avatar.Builtin` — ビルトインアバター供給実装
   - `RealtimeAvatarController.Samples.UI` — UI サンプル
3. The namespace convention shall エディタ限定コードに `Editor` サブ名前空間 (例: `RealtimeAvatarController.Core.Editor`) を採用する。
4. The namespace convention shall `_shared/contracts.md` の 6.2 章として文書化される。

---

### Requirement 5: `Samples~` 機構による UI サンプル同梱

**Objective:** As a パッケージ利用者, I want UI サンプルを任意にインポートできること, so that サンプルを必要としないプロジェクトへの影響を最小化しながら学習用シーンを提供できる。

#### Acceptance Criteria

1. The package shall UPM 仕様の `Samples~` ディレクトリをパッケージルートに設ける。
2. The `Samples~` directory shall 少なくとも UI サンプル用のサブディレクトリ (例: `Samples~/UI/`) を持つ。
3. The `package.json` shall `samples` フィールドに UI サンプルエントリを記述し、Unity パッケージマネージャー上でインポートボタンが表示される。
4. When 利用者が Package Manager UI からサンプルをインポートした場合, the sample shall プロジェクトの `Assets/Samples/` 以下にコピーされ、正常に参照できる。
5. The `Samples~` directory shall バージョン管理対象として git に追跡される (`.gitignore` で除外しない)。

---

### Requirement 6: 機能部と UI フレームワークの非依存性

**Objective:** As a 開発者, I want 機能部が特定 UI フレームワークに非依存であること, so that 任意の UI フレームワークから機能部を利用でき、テスト実行環境でも UI なしで動作できる。

#### Acceptance Criteria

1. The `RealtimeAvatarController.Core` asmdef shall UI フレームワーク (UGUI / UIToolkit / TextMeshPro 等) への明示的な参照を持たない。
2. The `RealtimeAvatarController.Motion` asmdef shall UI フレームワークへの依存を持たない。
3. The `RealtimeAvatarController.MoCap.VMC` asmdef shall UI フレームワークへの依存を持たない。
4. The `RealtimeAvatarController.Avatar.Builtin` asmdef shall UI フレームワークへの依存を持たない。
5. When 機能部アセンブリのみを含む Edit Mode テストプロジェクトでビルドを実行した場合, the build shall UI フレームワークのパッケージが存在しなくても成功する。

---

### Requirement 7: CI / ビルド検証の下地 (任意)

**Objective:** As a 開発者, I want 最小限の CI 設定が存在すること, so that プッシュのたびにビルド検証が自動実行され、基盤の健全性を維持できる。

#### Acceptance Criteria

1. Where CI を構成する場合, the CI configuration shall Unity 6000.3.10f1 を使用したビルド検証ステップを含む。
2. Where CI を構成する場合, the CI configuration shall `RealtimeAvatarController/` ディレクトリをプロジェクトパスとして設定する。
3. The CI configuration is optional for the initial phase; ただし配置する場合はリポジトリルートの `.github/workflows/` または同等のディレクトリに格納する。

---

### Requirement 8: UniRx UPM 依存の宣言

**Objective:** As a パッケージ利用者, I want 本パッケージが UniRx への依存を `package.json` で宣言していること, so that UPM の依存解決によって UniRx が自動的に取得される。

#### Acceptance Criteria

1. The `package.json` shall `dependencies` フィールドに `"com.neuecc.unirx": "<最新安定版>"` を宣言する (具体バージョン値は design フェーズで確定する)。
2. The `com.neuecc.unirx` パッケージは OpenUPM scoped registry (`https://package.openupm.com`、スコープ `com.neuecc`) から取得する前提とする。
3. 本パッケージの UPM 依存に NuGet 関連パッケージは含まない。UniRx が依存を追加しない唯一の scoped registry として OpenUPM 1 個のみで導入が完結することを確認する。
4. When 利用者が本パッケージを UPM でインストールした場合, UniRx が未インストール環境では UPM が `com.neuecc.unirx` を自動的に解決できる状態にある (ただし scoped registry が登録済みであること。詳細は Requirement 9)。

---

### Requirement 9: 利用者向け README への OpenUPM scoped registry 導入手順の記載

**Objective:** As a パッケージ利用者, I want README に UniRx 取得のための OpenUPM scoped registry 追加手順が記載されていること, so that 初めて本パッケージを導入する際に迷わず環境をセットアップできる。

#### Acceptance Criteria

1. The README shall 利用者が本パッケージをインストールする前提として、Unity プロジェクトの `Packages/manifest.json` に以下の scoped registry を追加する手順を説明する。
2. The README shall 以下の内容を含む `manifest.json` スニペットを掲載する:
   ```json
   {
     "scopedRegistries": [
       {
         "name": "OpenUPM",
         "url": "https://package.openupm.com",
         "scopes": [
           "com.neuecc"
         ]
       }
     ]
   }
   ```
3. The README shall `scopedRegistries` を追加した後に `com.cysharp.realtimeavatarcontroller`（仮称）を `dependencies` に追加する手順を示す。パッケージ名の確定は design フェーズで行う。
4. The README shall 手動編集に加えて `openupm-cli` コマンド例 (`openupm add com.neuecc.unirx`) を参考情報として掲載してよい (必須ではなく任意)。
5. The README のスニペット例は `Packages/manifest.json` の既存エントリを破壊しないよう、追記形式 (マージ) で説明すること。
