# Requirements Document

## Introduction

本ドキュメントは `avatar-provider-builtin` Spec の要件を定義する。本 Spec は `slot-core` が定義した `IAvatarProvider` 抽象インターフェースのビルトイン具象実装を提供し、Prefab として配置されたアバターを Slot へ供給する責務を担う。Addressable Asset System への将来的な拡張を阻害しない抽象遵守を設計上の重要な制約とする。

## Boundary Context

- **In scope**:
  - `IAvatarProvider` の具象実装 (`BuiltinAvatarProvider`)
  - Prefab 形式によるアバター指定と Scene 上へのインスタンス化
  - アバターのライフサイクル管理 (生成・破棄)
  - Slot との紐付け (SlotSettings 経由の Provider 参照)
  - `RealtimeAvatarController.Avatar.Builtin` アセンブリの定義
- **Out of scope**:
  - `IAvatarProvider` 抽象インターフェース自体の定義 (`slot-core` Spec が担当)
  - Addressable Provider の具象実装 (初期段階では実装しない)
  - アバターへのモーション適用 (`motion-pipeline` Spec が担当)
  - 表情制御・リップシンクの具象実装 (初期段階では対象外)
  - UI / サンプルシーン (`ui-sample` Spec が担当)
- **Adjacent expectations**:
  - `slot-core` が `IAvatarProvider` インターフェースおよび `SlotSettings` を提供している
  - `project-foundation` が Unity プロジェクトとアセンブリ定義の雛形を提供している
  - 将来 `avatar-provider-addressable` Spec が `IAvatarProvider` を実装する際、本 Spec の実装を変更する必要がない

---

## Requirements

### Requirement 1: BuiltinAvatarProvider の IAvatarProvider 実装

**Objective:** As a ツール統合者, I want `IAvatarProvider` の具象実装としてビルトインアバター供給クラスが存在すること, so that Slot にアバターを供給するための具体的な手段が提供される。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `IAvatarProvider` インターフェースを完全に実装し、コンパイルエラーなく `IAvatarProvider` 型として参照できる。
2. The BuiltinAvatarProvider shall `ProviderType` プロパティに `"Builtin"` を返す。
3. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` 名前空間に属する。
4. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` アセンブリ定義 (asmdef) 内に配置され、`RealtimeAvatarController.Core` アセンブリへの参照を持つ。
5. When 将来の Addressable Provider が `IAvatarProvider` を実装する場合, the BuiltinAvatarProvider shall BuiltinAvatarProvider 自体を変更せずに並列利用できる構造を持つ。

---

### Requirement 2: Prefab によるアバター指定

**Objective:** As a コンテンツ制作者, I want Unity プロジェクト内に配置した Prefab をアバターとして指定できること, so that プロジェクトに含まれるあらゆるアバター Prefab を Slot に割り当てられる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall アバター Prefab を `GameObject` 参照フィールドとして保持し、設定できる。
2. The BuiltinAvatarProvider shall Unity エディタ上で Prefab 参照を設定可能なシリアライズフィールドを持つ。
3. When Prefab 参照が設定されていない (null) 状態で `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 例外またはエラーを返し、null の GameObject を供給しない。
4. The BuiltinAvatarProvider shall プロジェクト内の任意の Prefab を受け付け、特定のアバター形式 (Humanoid/Generic) に限定しない。

---

### Requirement 3: Scene へのアバターインスタンス化

**Objective:** As a ランタイム統合者, I want `RequestAvatar()` 呼び出しにより Prefab が Scene 上にインスタンス化されること, so that アバターが実際の Scene に存在するオブジェクトとして利用できる。

#### Acceptance Criteria

1. When `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 設定済みの Prefab を `Object.Instantiate()` またはそれと等価な方法で Scene にインスタンス化し、生成した `GameObject` を返す。
2. When `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 同期的にインスタンス化を完了し、呼び出し元に即座に `GameObject` 参照を返す。
3. The BuiltinAvatarProvider shall インスタンス化した `GameObject` の参照を内部で追跡し、後の `ReleaseAvatar()` で解放できる状態を維持する。
4. When インスタンス化中に例外が発生した場合, the BuiltinAvatarProvider shall 例外を呼び出し元に伝播させ、不完全な `GameObject` を返さない。

---

### Requirement 4: アバターのライフサイクル管理

**Objective:** As a ランタイム統合者, I want アバターの生成から破棄までのライフサイクルが管理されること, so that 不要になったアバターのリソースが確実に解放され、メモリリークが防止される。

#### Acceptance Criteria

1. When `ReleaseAvatar(GameObject avatar)` が呼び出された場合, the BuiltinAvatarProvider shall 対象 `GameObject` を `Object.Destroy()` またはそれと等価な方法で Scene から除去する。
2. When `ReleaseAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall 内部追跡リストから該当 `GameObject` を除去する。
3. When `Dispose()` が呼び出された場合, the BuiltinAvatarProvider shall 追跡中の全 `GameObject` を破棄し、内部リソースをすべて解放する。
4. When `ReleaseAvatar()` に BuiltinAvatarProvider が供給していない `GameObject` が渡された場合, the BuiltinAvatarProvider shall エラーをログに記録し、その `GameObject` を破棄しない。
5. When `Dispose()` 後に `RequestAvatar()` が呼び出された場合, the BuiltinAvatarProvider shall `ObjectDisposedException` または相当するエラーを発生させる。

---

### Requirement 5: Slot との紐付け

**Objective:** As a ランタイム統合者, I want BuiltinAvatarProvider が SlotSettings を通じて Slot に紐付けられること, so that Slot 単位でアバターを供給・解放できる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `SlotSettings` の `avatarProvider` フィールドに `IAvatarProvider` 参照として設定可能である。
2. When SlotManager が Slot をアクティブ化する場合, the BuiltinAvatarProvider shall SlotManager から `RequestAvatar()` を呼び出された際に対象 Slot 向けのアバター `GameObject` を返す。
3. When SlotManager が Slot を破棄する場合, the BuiltinAvatarProvider shall SlotManager から `ReleaseAvatar()` を呼び出された際に対象アバターを確実に解放する。
4. The BuiltinAvatarProvider shall 複数の Slot から同一の Provider インスタンスを参照している場合でも、各 Slot に対して独立したアバターインスタンスを供給できる。

---

### Requirement 6: 非同期 API の拡張余地

**Objective:** As a 将来の実装者, I want `IAvatarProvider` の非同期 API シグネチャが BuiltinAvatarProvider に存在すること, so that Addressable Provider 実装との共通インターフェース契約を守れる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `IAvatarProvider` が定義する非同期版アバター要求 API (`RequestAvatarAsync()` 相当) の骨格を実装する。
2. When `RequestAvatarAsync()` がビルトイン Provider に対して呼び出された場合, the BuiltinAvatarProvider shall 同期的に完了する非同期処理 (即時完了タスク) として結果を返してよい。
3. The BuiltinAvatarProvider shall 非同期 API の具体的な戻り値型 (UniTask / Task&lt;GameObject&gt;) を design フェーズで確定できるよう、型の選択に依存しない骨格を要件上は許容する。

---

### Requirement 7: アセンブリ・名前空間境界

**Objective:** As a パッケージ利用者, I want BuiltinAvatarProvider が独立したアセンブリに配置されること, so that ビルトイン Provider が不要なプロジェクトでは参照を外せる。

#### Acceptance Criteria

1. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` アセンブリ定義 (`RealtimeAvatarController.Avatar.Builtin.asmdef`) に配置される。
2. The asmdef shall `RealtimeAvatarController.Core` アセンブリのみを参照し、`RealtimeAvatarController.Motion` など他の機能アセンブリには依存しない。
3. The BuiltinAvatarProvider shall `RealtimeAvatarController.Avatar.Builtin` 名前空間に属する。
4. When プロジェクトが `RealtimeAvatarController.Avatar.Builtin` アセンブリを参照しない場合, the BuiltinAvatarProvider shall 他のアセンブリのコンパイルに影響を与えない。
