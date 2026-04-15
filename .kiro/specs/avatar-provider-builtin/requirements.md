# Requirements Document

## Introduction

本ドキュメントは `avatar-provider-builtin` Spec の要件を定義する。本 Spec は `slot-core` が定義した `IAvatarProvider` 抽象インターフェースのビルトイン具象実装を提供し、Prefab として配置されたアバターを Slot へ供給する責務を担う。

Wave A および dig ラウンド 2・3・4 で確定した設計方針に従い、以下の原則を採用する:

- **Descriptor / Registry / Factory モデル**: `SlotSettings.avatarProviderDescriptor` (`AvatarProviderDescriptor`) に基づいて `IProviderRegistry` が `BuiltinAvatarProviderFactory` を解決し、`BuiltinAvatarProvider` を生成する
- **typeId = `"Builtin"`**: ビルトイン Provider の識別子は `"Builtin"` で固定
- **参照共有は採用しない**: `IMoCapSource` と異なり、`IAvatarProvider` は 1 Slot に対して 1 インスタンスを割り当てる原則 (SlotManager が所有・破棄を管理)
- **Config 型階層**: `slot-core` が定義した `ProviderConfigBase : ScriptableObject` (contracts.md 1.5 章) を基底とし、本 Spec が `BuiltinAvatarProviderConfig : ProviderConfigBase` を具象 Config として定義する責務を持つ
- **BuiltinAvatarProviderConfig のランタイム動的生成 (dig ラウンド 4 確定)**: `BuiltinAvatarProviderConfig` は Unity エディタ上でのアセット作成 (シナリオ X) に加え、`ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` によるランタイム動的生成 (シナリオ Y) も公式にサポートする。動的生成時は `avatarPrefab` 等の `public` フィールドを直接セットして設定値を与える。Factory は SO アセット経由・ランタイム動的生成のいずれで生成した Config も同一経路 (`IAvatarProviderFactory.Create()`) で透過的に処理できる
- **Factory のキャスト責務**: `BuiltinAvatarProviderFactory` は `IAvatarProviderFactory.Create(ProviderConfigBase config)` の引数を `BuiltinAvatarProviderConfig` にキャストして使用し、キャスト失敗時は `ArgumentException` を `ISlotErrorChannel` に `SlotErrorCategory.InitFailure` で発行したうえで上位にスローする
- **属性ベース自己登録 (dig ラウンド 3 確定)**: `BuiltinAvatarProviderFactory` は `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` および `[UnityEditor.InitializeOnLoadMethod]` により `RegistryLocator.ProviderRegistry` に自己登録する。Editor 用コードは `RealtimeAvatarController.Avatar.Builtin.Editor` asmdef または `#if UNITY_EDITOR` ガードに配置する
- **エラー通知**: Instantiate 失敗等のランタイムエラーは `ISlotErrorChannel` に `SlotErrorCategory.InitFailure` で発行し、例外を上位の `SlotManager` に伝播させる (抑制ロジックは `slot-core` の責務)
- **UniRx の間接依存**: UniRx (`com.neuecc.unirx`) は本 Spec が直接使用する可能性は低いが、`RealtimeAvatarController.Core` アセンブリ経由で間接的に存在することを認識する

## Boundary Context

- **In scope**:
  - `IAvatarProvider` の具象実装 (`BuiltinAvatarProvider`)
  - `BuiltinAvatarProviderFactory` の実装 (`IAvatarProviderFactory` を実装し、Descriptor から `BuiltinAvatarProvider` インスタンスを生成する責務)
  - `IProviderRegistry` への `typeId="Builtin"` の Factory 登録 (起動時)
  - Prefab 形式によるアバター指定 (`AvatarProviderDescriptor.Config` に `BuiltinAvatarProviderConfig` を格納)
  - Scene 上へのインスタンス化
  - アバターのライフサイクル管理 (生成・破棄)
  - `RealtimeAvatarController.Avatar.Builtin` アセンブリの定義
- **Out of scope**:
  - `IAvatarProvider` / `IProviderRegistry` / `IAvatarProviderFactory` / `AvatarProviderDescriptor` / `ProviderConfigBase` (基底 Config 型) の抽象定義 (`slot-core` Spec が担当)
  - Addressable Provider の具象実装 (初期段階では実装しない)
  - アバターへのモーション適用 (`motion-pipeline` Spec が担当)
  - 表情制御・リップシンクの具象実装 (初期段階では対象外)
  - UI / サンプルシーン (`ui-sample` Spec が担当)
  - `IMoCapSource` で採用している参照共有モデルの `IAvatarProvider` への適用
- **Adjacent expectations**:
  - `slot-core` が `IAvatarProvider`・`IProviderRegistry`・`IAvatarProviderFactory`・`AvatarProviderDescriptor`・`SlotSettings`・`ProviderConfigBase` (1.5 章) を提供している
  - `project-foundation` が Unity プロジェクトとアセンブリ定義の雛形を提供している
  - 将来 `avatar-provider-addressable` Spec が `IAvatarProvider` を実装する際、本 Spec の実装を変更する必要がない

---

## Requirements

### Requirement 1: BuiltinAvatarProvider の IAvatarProvider 実装および Registry 登録

**Objective:** As a ツール統合者, I want `IAvatarProvider` の具象実装としてビルトインアバター供給クラスが存在し、`IProviderRegistry` に `typeId="Builtin"` で登録されること, so that Slot にアバターを供給するための具体的な手段が Registry 経由で利用できる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `IAvatarProvider` インターフェース (contracts.md 3 章骨格) を完全に実装し、コンパイルエラーなく `IAvatarProvider` 型として参照できる。
2. The BuiltinAvatarProvider shall `ProviderType` プロパティに `"Builtin"` を返す。
3. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` 名前空間に属する。
4. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` アセンブリ定義 (asmdef) 内に配置され、`RealtimeAvatarController.Core` アセンブリへの参照を持つ。
5. When アプリケーションが起動した場合, `BuiltinAvatarProviderFactory` shall `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 属性が付与された静的メソッドにより `RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory())` を呼び出し、`typeId="Builtin"` の Factory をランタイム起動時 (シーンロード前) に登録する。
6. When Unity Editor が起動またはコンパイル完了した場合, `BuiltinAvatarProviderFactory` shall `[UnityEditor.InitializeOnLoadMethod]` 属性が付与された静的メソッドにより同じく `RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory())` を呼び出し、エディタ UI での候補列挙が可能な状態にする。この Editor 用コードは `RealtimeAvatarController.Avatar.Builtin.Editor` asmdef または `#if UNITY_EDITOR` ガード内に配置する。
7. When 同一 `typeId="Builtin"` の Factory が既に登録された状態で `Register()` が呼ばれた場合, `BuiltinAvatarProviderFactory` shall `RegistryConflictException` 相当の例外をスローし、上書きしない (contracts.md 1.4 章の競合ポリシー準拠)。
8. When 将来の Addressable Provider が `IAvatarProvider` を実装する場合, the BuiltinAvatarProvider shall BuiltinAvatarProvider 自体を変更せずに並列利用できる構造を持つ。

---

### Requirement 2: BuiltinAvatarProviderConfig 型定義と Prefab 参照

**Objective:** As a コンテンツ制作者, I want Unity プロジェクト内に配置した Prefab をアバターとして指定できること, so that プロジェクトに含まれるあらゆるアバター Prefab を Slot に割り当てられる。

#### Acceptance Criteria

1. 本 Spec は `ProviderConfigBase` (contracts.md 1.5 章、`slot-core` が定義) を継承した具象 Config 型 `BuiltinAvatarProviderConfig : ProviderConfigBase` を定義する責務を持つ。`BuiltinAvatarProviderConfig` は `RealtimeAvatarController.Avatar.Builtin` 名前空間・アセンブリに配置される。
2. `BuiltinAvatarProviderConfig` shall `avatarPrefab` フィールド (型: `GameObject`) を `public` フィールドとして持ち、Inspector でのドラッグ&ドロップによるアバター Prefab 参照の設定を可能にする。`avatarPrefab` が `public` であることは、ランタイム動的生成 (下記 AC 3) においてコードからの直接セットも可能にする前提である。
3. `BuiltinAvatarProviderConfig` は **Unity エディタ上でのアセット作成・保存 (シナリオ X)** および **`ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` によるランタイム動的生成 (シナリオ Y)** の両方をサポートする。ランタイム動的生成時は `avatarPrefab` 等の `public` フィールドを直接セットすることで設定値を与える。`BuiltinAvatarProviderFactory.Create()` はどちらのシナリオで生成した Config を受け取っても同一のキャスト・インスタンス化ロジックで処理できる。
4. `BuiltinAvatarProviderConfig` は `ScriptableObject` を基底とする (`ProviderConfigBase` 継承により自動的に充足される) ため、`AvatarProviderDescriptor.Config` フィールドへの型安全な参照が可能である。
5. When Prefab 参照 (`avatarPrefab`) が設定されていない (null) 状態で `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 例外またはエラーを返し、null の `GameObject` を供給しない。
6. The BuiltinAvatarProvider shall プロジェクト内の任意の Prefab を受け付け、特定のアバター形式 (Humanoid / Generic) に限定しない。

---

### Requirement 3: Scene へのアバターインスタンス化

**Objective:** As a ランタイム統合者, I want `RequestAvatar()` 呼び出しにより Prefab が Scene 上にインスタンス化されること, so that アバターが実際の Scene に存在するオブジェクトとして利用できる。

#### Acceptance Criteria

1. When `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 設定済みの Prefab を `Object.Instantiate()` またはそれと等価な方法で Scene にインスタンス化し、生成した `GameObject` を返す。
2. When `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 同期的にインスタンス化を完了し、呼び出し元に即座に `GameObject` 参照を返す。
3. The BuiltinAvatarProvider shall インスタンス化した `GameObject` の参照を内部で追跡し、後の `ReleaseAvatar()` で解放できる状態を維持する。
4. When インスタンス化中に例外が発生した場合 (null Prefab・`Object.Instantiate()` 失敗等), the BuiltinAvatarProvider shall 例外を捕捉して `ISlotErrorChannel` に `SlotErrorCategory.InitFailure` で発行したうえで上位の `SlotManager` に再スローし、不完全な `GameObject` を返さない。

---

### Requirement 4: アバターのライフサイクル管理 (1 Slot 1 インスタンス原則)

**Objective:** As a ランタイム統合者, I want アバターの生成から破棄までのライフサイクルが SlotManager によって管理されること, so that 不要になったアバターのリソースが確実に解放され、メモリリークが防止される。

#### Acceptance Criteria

1. When `ReleaseAvatar(GameObject avatar)` が呼び出された場合, the BuiltinAvatarProvider shall 対象 `GameObject` を `Object.Destroy()` またはそれと等価な方法で Scene から除去する。
2. When `ReleaseAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 内部追跡リストから該当 `GameObject` を除去する。
3. When `Dispose()` が呼び出された場合, the BuiltinAvatarProvider shall 追跡中の全 `GameObject` を破棄し、内部リソースをすべて解放する。
4. When `ReleaseAvatar()` に BuiltinAvatarProvider が供給していない `GameObject` が渡された場合, the BuiltinAvatarProvider shall エラーをログに記録し、その `GameObject` を破棄しない。
5. When `Dispose()` 後に `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall `ObjectDisposedException` または相当するエラーを発生させる。
6. The BuiltinAvatarProvider shall **1 Slot に対して 1 インスタンスを供給する原則**に従い、`IMoCapSource` で採用している参照共有モデル (複数 Slot が同一インスタンスを参照・`MoCapSourceRegistry` による参照カウント管理) を採用しない。アバター Provider のライフサイクルは `SlotManager` が直接管理し、Slot の破棄と同時にアバター `GameObject` を破棄する。

---

### Requirement 5: Slot との紐付け (IProviderRegistry 経由)

**Objective:** As a ランタイム統合者, I want `SlotSettings.avatarProviderDescriptor` を用いて `IProviderRegistry` 経由で `BuiltinAvatarProvider` を取得し、Slot に紐付けられること, so that Slot 単位でアバターを供給・解放できる。

#### Acceptance Criteria

1. When `SlotManager` が Slot をアクティブ化する場合, `SlotManager` shall `IProviderRegistry.Resolve(slotSettings.avatarProviderDescriptor)` を呼び出して `IAvatarProvider` インスタンスを取得し、そのインスタンスの `RequestAvatar()` を呼び出してアバター `GameObject` を取得する。
2. The BuiltinAvatarProvider shall `IProviderRegistry.Resolve()` 呼び出し時に `BuiltinAvatarProviderFactory.Create()` を通じてインスタンス化される。
3. When SlotManager が Slot を破棄する場合, the BuiltinAvatarProvider shall `SlotManager` から `ReleaseAvatar()` および `Dispose()` を呼び出された際に対象アバターを確実に解放する。
4. The BuiltinAvatarProvider shall 複数の Slot が異なる `BuiltinAvatarProvider` インスタンスを保有する構造において、各 Slot に対して独立したアバターインスタンスを供給できる。

---

### Requirement 6: 非同期 API の拡張余地

**Objective:** As a 将来の実装者, I want `IAvatarProvider` の非同期 API シグネチャが BuiltinAvatarProvider に存在すること, so that Addressable Provider 実装との共通インターフェース契約を守れる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `IAvatarProvider` が定義する非同期版アバター要求 API (`RequestAvatarAsync()` 相当) の骨格を実装する。
2. When `RequestAvatarAsync()` がビルトイン Provider に対して呼び出された場合, the BuiltinAvatarProvider shall 同期的に完了する非同期処理 (即時完了タスク) として結果を返してよい。
3. The BuiltinAvatarProvider shall 非同期 API の具体的な戻り値型 (UniTask / Task&lt;GameObject&gt;) を design フェーズで確定できるよう、型の選択に依存しない骨格を要件上は許容する。

---

### Requirement 7: アセンブリ・名前空間境界および Factory 登録方式

**Objective:** As a パッケージ利用者, I want BuiltinAvatarProvider が独立したアセンブリに配置され、IProviderRegistry への Factory 登録が明示的に管理されること, so that ビルトイン Provider が不要なプロジェクトでは参照を外せる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` アセンブリ定義 (`RealtimeAvatarController.Avatar.Builtin.asmdef`) に配置される。
2. The asmdef shall `RealtimeAvatarController.Core` アセンブリのみを参照し、`RealtimeAvatarController.Motion` など他の機能アセンブリには依存しない。
3. The BuiltinAvatarProvider および `BuiltinAvatarProviderFactory` は `RealtimeAvatarController.Avatar.Builtin` 名前空間に属する。
4. When プロジェクトが `RealtimeAvatarController.Avatar.Builtin` アセンブリを参照しない場合, the BuiltinAvatarProvider shall 他のアセンブリのコンパイルに影響を与えない。
5. `BuiltinAvatarProviderFactory` の `IProviderRegistry` への登録方式として、**属性ベース自己登録** (dig ラウンド 3 確定) を採用する。`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` でランタイム、`[UnityEditor.InitializeOnLoadMethod]` でエディタ、それぞれの起動タイミングに `RegistryLocator.ProviderRegistry.Register("Builtin", new BuiltinAvatarProviderFactory())` を呼び出す 2 メソッドを `BuiltinAvatarProviderFactory` クラス内に実装する。Editor 用の `[InitializeOnLoadMethod]` メソッドは `RealtimeAvatarController.Avatar.Builtin.Editor` asmdef または `#if UNITY_EDITOR` ガードに配置し、Player ビルド時に含まれないようにする。
6. Domain Reload OFF 設定下での再実行による二重登録が問題となる場合、`RegistryLocator.ResetForTest()` (SubsystemRegistration タイミング) を使用して事前クリアを行う。本 Spec は `ResetForTest()` を実装する責務を持たないが、その存在を前提として二重登録エラーが発生しないことを期待する。

---

### Requirement 8: BuiltinAvatarProviderFactory の責務とキャスト処理

**Objective:** As a システム設計者, I want `IAvatarProviderFactory` を実装する `BuiltinAvatarProviderFactory` が存在すること, so that `IProviderRegistry` が Descriptor から `BuiltinAvatarProvider` インスタンスを生成できる。

#### Acceptance Criteria

1. `BuiltinAvatarProviderFactory` shall `IAvatarProviderFactory` インターフェース (contracts.md 1.4 章骨格) を完全に実装する。`IAvatarProviderFactory.Create()` のシグネチャは `IAvatarProvider Create(ProviderConfigBase config)` である (contracts.md 1.4 章参照)。
2. When `BuiltinAvatarProviderFactory.Create(ProviderConfigBase config)` が呼び出された場合, `BuiltinAvatarProviderFactory` shall 引数 `config` を `BuiltinAvatarProviderConfig` へキャスト (`config as BuiltinAvatarProviderConfig`) し、キャスト成功時に新規 `BuiltinAvatarProvider` インスタンスを生成して返す。
3. When `Create()` に渡された `config` が `BuiltinAvatarProviderConfig` にキャストできない場合 (キャスト結果が null の場合)、`BuiltinAvatarProviderFactory` shall `ArgumentException` (またはそれに相当する例外) をスローし、不正なインスタンスを生成しない。エラーメッセージには期待型 (`BuiltinAvatarProviderConfig`) と実際の型名を含める。また、この例外はスロー前に `ISlotErrorChannel` へ `SlotErrorCategory.InitFailure` で発行する。例外の上位捕捉と Slot の `Disposed` 状態遷移は `SlotManager` (`slot-core`) の責務であり、本 Spec は発行のみを担う。
4. `BuiltinAvatarProviderFactory` shall `RealtimeAvatarController.Avatar.Builtin` 名前空間に属し、同アセンブリに配置される。
5. `BuiltinAvatarProviderFactory` shall ステートレスに設計し、複数回の `Create()` 呼び出しが互いに干渉しない。
6. `BuiltinAvatarProviderFactory` は `ISlotErrorChannel` をコンストラクタ引数または `Create()` 呼び出し時の引数 (どちらを採用するかは design フェーズで確定) として受け取り、エラー発行に使用できる。エラーチャネルが未設定 (null) の場合は `Debug.LogError` にフォールバックしてよい。

---

### Requirement 9: テスト戦略 (EditMode / PlayMode 両系統)

**Objective:** As a 開発者, I want `avatar-provider-builtin` の機能が EditMode・PlayMode の両テスト系統によって自動検証されること, so that リグレッションを早期に検出しながら安全に開発を進められる。

#### Acceptance Criteria

1. 本 Spec の自動テストは **EditMode テスト用 asmdef** と **PlayMode テスト用 asmdef** の 2 系統を用意する。各 asmdef の命名は以下のとおりとする。
   - EditMode: `RealtimeAvatarController.Avatar.Builtin.Tests.EditMode`
   - PlayMode: `RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode`
2. EditMode テスト asmdef (`RealtimeAvatarController.Avatar.Builtin.Tests.EditMode`) は以下の検証項目を対象とする。
   - `BuiltinAvatarProviderFactory.Create()` への正しい Config 型渡しによるキャスト成功と `BuiltinAvatarProvider` インスタンス生成
   - `BuiltinAvatarProviderFactory.Create()` への不正な Config 型渡しによるキャスト失敗時の `ArgumentException` スローおよび `ISlotErrorChannel` への `InitFailure` 発行
   - `IProviderRegistry.Resolve()` 経由での `BuiltinAvatarProviderFactory` 取得 (Factory の自己登録が行われていることの確認)
   - `BuiltinAvatarProvider.ReleaseAvatar()` および `Dispose()` の呼び出しシーケンス検証 (Unity ランタイム非依存の範囲)
   - 属性ベース自己登録 (`[RuntimeInitializeOnLoadMethod]` / `[InitializeOnLoadMethod]`) が `RegistryLocator.ProviderRegistry` に `typeId="Builtin"` で Factory を登録することの確認
3. PlayMode テスト asmdef (`RealtimeAvatarController.Avatar.Builtin.Tests.PlayMode`) は以下の検証項目を対象とする。
   - `BuiltinAvatarProviderConfig` の SO アセット経由生成 (シナリオ X) および `ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>()` によるランタイム動的生成 (シナリオ Y) の両方から `RequestAvatar()` を呼び出して Prefab が Scene 上にインスタンス化されることの確認
   - `ReleaseAvatar()` 呼び出し後にインスタンスが破棄 (Destroy) されることの確認
   - `Dispose()` 後に `RequestAvatar()` を呼び出した場合に `ObjectDisposedException` または相当するエラーが発生することの確認 (Disposed 遷移の検証)
4. 初期版においてカバレッジ数値目標は設定しない。テスト対象は上記 AC 2・3 に列挙した項目に限定し、過剰なテスト拡張は行わない。
5. 各テスト asmdef は `RealtimeAvatarController.Avatar.Builtin` アセンブリおよび `RealtimeAvatarController.Core` アセンブリを参照する。テスト固有の依存 (NUnit, UnityEngine.TestRunner 等) 以外の外部依存は持たない。
