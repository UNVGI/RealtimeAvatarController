# Requirements Document

## Introduction

本ドキュメントは `slot-core` Spec の要件を定義する。`slot-core` は Realtime Avatar Controller の中核 Spec であり、Slot 概念に基づくデータモデル・ライフサイクル管理 API を提供するとともに、他 5 Spec が参照する公開抽象インターフェース (`IMoCapSource` / `IAvatarProvider` / `IFacialController` / `ILipSyncSource`) を定義する Wave 1 先行 Spec である。

## Boundary Context

- **In scope**:
  - Slot データモデル (設定項目・シリアライズ構造)
  - SlotRegistry / SlotManager の動的追加・削除 API
  - Slot ライフサイクル (生成・破棄・リソース所有)
  - 抽象インターフェース 4 種の定義 (`IMoCapSource` / `IAvatarProvider` / `IFacialController` / `ILipSyncSource`)
  - `_shared/contracts.md` の 1 章・2.1 章・3.1 章・4.1 章・5.1 章の確定
- **Out of scope**:
  - 抽象インターフェースの具象実装 (各担当 Spec で実施)
  - モーション中立表現の定義 (motion-pipeline Spec)
  - モーション適用処理 (motion-pipeline Spec)
  - 表情制御・リップシンクの具象実装 (初期段階では対象外)
  - UI / サンプルシーン (ui-sample Spec)
- **Adjacent expectations**:
  - `project-foundation` が Unity プロジェクト・アセンブリ定義を提供する前提で設計する
  - `mocap-vmc` は `IMoCapSource` を実装する; `avatar-provider-builtin` は `IAvatarProvider` を実装する
  - `motion-pipeline` は本 Spec が定義する Slot データモデルを参照し、モーションデータ中立表現 (2.2 章) を確定させる

---

## Requirements

### Requirement 1: Slot データモデル

**Objective:** As a ツール統合者, I want Slot が必要な設定項目を一箇所で保持できること, so that アバター・モーション・表情制御の設定を統一的に管理できる。

#### Acceptance Criteria

1. The SlotSettings shall アバター参照 (`IAvatarProvider` 型)・MoCap ソース参照 (`IMoCapSource` 型)・Facial Controller 参照 (`IFacialController` 型、null 許容)・LipSync Source 参照 (`ILipSyncSource` 型、null 許容)・Weight 値 (float 0.0〜1.0)・Slot 識別子 (string)・表示名 (string) の各フィールドを保持する。
2. The SlotSettings shall Unity の ScriptableObject として定義され、エディタおよびランタイムでシリアライズ・デシリアライズ可能である。
3. When Weight 値に 0.0〜1.0 の範囲外の値が設定された場合, the SlotSettings shall 値を 0.0〜1.0 にクランプして保持する。
4. The SlotSettings shall Slot 識別子フィールドを一意に識別するための主キーとして使用できる。
5. When シリアライズ形式として ScriptableObject を採用する場合, the SlotSettings shall JSON へのエクスポートおよびインポートをサポートできる拡張余地を持つ。

---

### Requirement 2: SlotRegistry / SlotManager の動的管理 API

**Objective:** As a ランタイム統合者, I want Slot を実行時に動的に追加・削除・取得できること, so that 複数アバター・複数ソースの組み合わせをユーザー操作に応じて切り替えられる。

#### Acceptance Criteria

1. When 新規 Slot の追加が要求された場合, the SlotRegistry shall SlotSettings を登録し、一意の Slot 識別子で参照可能にする。
2. When 既存 Slot の削除が要求された場合, the SlotRegistry shall 対象 Slot を登録から除去し、関連リソースの解放トリガーを発行する。
3. When 同一 Slot 識別子で重複登録が試みられた場合, the SlotRegistry shall 例外またはエラーを返し、既存 Slot を上書きしない。
4. The SlotRegistry shall 登録済み Slot の一覧を取得できる API を提供する。
5. When Slot 識別子を指定して検索した場合, the SlotRegistry shall 対象の SlotSettings を返す。If 指定した識別子が存在しない場合, the SlotRegistry shall null または Not Found を示す値を返す。
6. The SlotManager shall SlotRegistry を通じて Slot の追加・削除・設定変更のオーケストレーションを担い、ライフサイクルイベントを外部に通知できる。
7. While SlotManager が初期化済み状態である間, the SlotManager shall スレッドセーフな Slot 参照の読み取り操作をサポートする。

---

### Requirement 3: Slot ライフサイクル

**Objective:** As a ランタイム統合者, I want Slot の生成・破棄が明確に管理されること, so that リソースリークを防ぎ、予測可能なリソース所有関係を維持できる。

#### Acceptance Criteria

1. When Slot が生成された場合, the SlotManager shall 関連する `IAvatarProvider`・`IMoCapSource` の初期化をトリガーできる。
2. When Slot が破棄された場合, the SlotManager shall 関連する `IAvatarProvider`・`IMoCapSource` の解放処理をトリガーし、リソースを確実に解放する。
3. The Slot shall 生成 (Created)・アクティブ (Active)・非アクティブ (Inactive)・破棄済み (Disposed) の各ライフサイクル状態を持つ。
4. When Slot の状態が変化した場合, the SlotManager shall 状態変化イベントを購読者に通知する。
5. If Slot 破棄中に例外が発生した場合, the SlotManager shall 例外をキャッチしてログに記録し、残余リソースの解放を継続する。
6. The SlotSettings shall ScriptableObject として Unity エディタ上でアセットとして保存・管理できる。

---

### Requirement 4: `IMoCapSource` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `IMoCapSource` の骨格が定義されること, so that mocap-vmc Spec が具象実装を作成できる。

#### Acceptance Criteria

1. The IMoCapSource shall 初期化メソッド・破棄メソッドの骨格を持つ。
2. The IMoCapSource shall モーションデータを取得する API の骨格を持つ (pull 型・push 型・イベント型の選択は design フェーズで確定する)。
3. The IMoCapSource shall ソース種別を識別するメタデータプロパティの骨格を持つ。
4. The IMoCapSource shall 通信パラメータ (ポート番号等) を Slot 単位で設定できる構造の骨格を持つ。
5. The IMoCapSource shall スレッド安全性の要求 (メインスレッド外からの呼び出し可否) を設計上明示できる骨格を持つ。

---

### Requirement 5: `IAvatarProvider` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `IAvatarProvider` の骨格が定義されること, so that avatar-provider-builtin Spec が具象実装を作成できる。

#### Acceptance Criteria

1. The IAvatarProvider shall アバターを要求するメソッドの骨格を持ち、同期・非同期のいずれの具象実装も許容するシグネチャとする。
2. The IAvatarProvider shall アバター (GameObject / Prefab インスタンス) を解放するメソッドの骨格を持つ。
3. The IAvatarProvider shall Provider 種別を識別するメタデータプロパティの骨格を持つ。
4. Where Addressable Provider を将来実装する場合, the IAvatarProvider shall 非同期要求 API を通じて拡張できる余地を持つ。

---

### Requirement 6: `IFacialController` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `IFacialController` の受け口骨格が定義されること, so that 将来の具象実装がこのインターフェースに準拠できる。

#### Acceptance Criteria

1. The IFacialController shall 表情データを適用するメソッドの骨格を持つ (引数型の最終決定は design フェーズ)。
2. The IFacialController shall 初期化・解放メソッドの骨格を持つ。
3. The SlotSettings shall `IFacialController` 参照フィールドに null を許容し、Slot に表情制御を割り当てない状態を有効とする。

---

### Requirement 7: `ILipSyncSource` 抽象インターフェース定義

**Objective:** As a Spec 設計者, I want `ILipSyncSource` の受け口骨格が定義されること, so that 将来の具象実装がこのインターフェースに準拠できる。

#### Acceptance Criteria

1. The ILipSyncSource shall リップシンクデータを取得または適用するメソッドの骨格を持つ (引数型の最終決定は design フェーズ)。
2. The ILipSyncSource shall 初期化・解放メソッドの骨格を持つ。
3. The SlotSettings shall `ILipSyncSource` 参照フィールドに null を許容し、Slot にリップシンクを割り当てない状態を有効とする。

---

### Requirement 8: 設定シリアライズ可能性

**Objective:** As a ユーザー, I want Slot 設定を保存・復元できること, so that アバター構成をプロジェクト間で再利用できる。

#### Acceptance Criteria

1. The SlotSettings shall Unity の標準シリアライズ機構 (ScriptableObject) によりディスクへの保存・読み込みが可能である。
2. The SlotRegistry shall 登録済み Slot 一覧を外部から列挙できる API を提供し、シリアライズツールからアクセス可能にする。
3. When SlotSettings をシリアライズする際に `IMoCapSource` 等のインターフェース参照が含まれる場合, the SlotSettings shall 参照をシリアライズ可能な形式 (例: アセット参照または型名) で保持できる。
