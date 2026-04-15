# Requirements Document

## Introduction

本ドキュメントは `ui-sample` Spec の要件を定義する。`ui-sample` は Realtime Avatar Controller の機能部 API を実際に呼び出すサンプル UI を提供し、UPM の `Samples~` 機構を通じて配布する。全 5 Spec (`project-foundation` / `slot-core` / `motion-pipeline` / `mocap-vmc` / `avatar-provider-builtin`) が定義する公開 API を消費側として利用し、Slot 追加・削除・設定変更・アバター選択・MoCap ソース設定・Weight 操作を UI 上から実行できるデモシーンを構成する。

本サンプルは**検証デモ**の位置付けであり、運用における本命の組込み先は別 VTuber システムを想定している。

アバターおよび MoCap ソースの候補は、ハードコードではなく `IProviderRegistry.GetRegisteredTypeIds()` / `IMoCapSourceRegistry.GetRegisteredTypeIds()` を介した**動的列挙**によって取得する。Slot の設定データは Descriptor ベース (`AvatarProviderDescriptor` / `MoCapSourceDescriptor`) を使用する。

## Boundary Context

- **In scope**:
  - Slot の追加・削除・設定を操作する UI
  - 各 Slot のアバター選択・MoCap ソース設定・Weight 操作
  - UI 経由での挙動確認用デモシーン (参照共有シナリオを含む)
  - UPM `Samples~` ディレクトリへの配置と配布機構
  - サンプル専用アセンブリ定義 (`RealtimeAvatarController.Samples.UI`)
- **Out of scope**:
  - 機能部 API の新規定義 (各機能 Spec で完結している前提)
  - 機能部アセンブリへの UI フレームワーク依存の持ち込み
  - 運用向けの本番 UI 実装
  - 表情制御・リップシンクの UI (初期段階では具象実装が存在しないため対象外)
  - Addressable アバター Provider の UI (初期段階では対象外)
- **Adjacent expectations**:
  - `project-foundation` が Unity プロジェクト・アセンブリ定義・`Samples~` ディレクトリ構成を提供する
  - `slot-core` の `SlotRegistry` / `SlotManager` / `IProviderRegistry` / `IMoCapSourceRegistry` API を呼び出す
  - `motion-pipeline` の Weight 適用 API を呼び出す
  - `mocap-vmc` の VMC MoCap ソース具象実装を UI から設定する
  - `avatar-provider-builtin` のビルトイン Provider を UI からアバターとして選択する

---

## Requirements

### Requirement 1: Slot 操作 UI

**Objective:** As a 開発者・検証者, I want UI 上から Slot を追加・削除・設定変更できること, so that 機能部の SlotRegistry / SlotManager API が正しく動作することを視覚的に確認できる。

#### Acceptance Criteria

1. The UI shall `SlotRegistry.AddSlot()` を呼び出して新規 Slot を作成するボタンを提供する。
2. The UI shall 登録済み Slot の一覧を表示し、各 Slot の識別子・表示名・ライフサイクル状態を可視化する。
3. The UI shall 選択した Slot に対して `SlotRegistry.RemoveSlot()` を呼び出す削除操作を提供する。
4. When Slot の状態 (Created / Active / Inactive / Disposed) が変化した場合, the UI shall 一覧表示をリアルタイムに更新する。
5. The UI shall Slot の表示名を編集できる入力フィールドを提供し、変更を `SlotSettings.displayName` に反映する。
6. The UI shall 各 Slot の `SlotSettings` に含まれる Descriptor 情報 (`AvatarProviderDescriptor` / `MoCapSourceDescriptor`) を編集する設定パネルへのアクセス手段を提供する。

---

### Requirement 2: アバター選択 UI

**Objective:** As a 開発者・検証者, I want 各 Slot に対してアバターを選択・切り替えできること, so that `IAvatarProvider` を通じたアバター供給フローが正しく機能することを確認できる。

#### Acceptance Criteria

1. The UI shall `IProviderRegistry.GetRegisteredTypeIds()` を呼び出して取得した Provider 種別一覧を動的に列挙し、ドロップダウンまたはリストとして表示する。選択肢はハードコードしない。
2. When アバター Provider 種別が選択された場合, the UI shall 選択した `providerTypeId` を持つ `AvatarProviderDescriptor` を構築し、`SlotSettings.avatarProviderDescriptor` に反映する。
3. When `AvatarProviderDescriptor` が確定した場合, the UI shall `IAvatarProvider.RequestAvatar()` (同期版) を呼び出し、デモシーン内にアバターをインスタンス化する。
4. When 別のアバターに切り替えた場合, the UI shall 旧アバターに対して `IAvatarProvider.ReleaseAvatar()` を呼び出してから新しいアバターを要求する。
5. The UI shall 現在 Slot に割り当てられているアバターの名称・Provider 種別 (`AvatarProviderDescriptor.ProviderTypeId`) を表示する。
6. When Slot が削除された場合, the UI shall 紐付けられたアバターを自動的に解放する処理を呼び出す。
7. The UI は `IProviderRegistry` への参照を機能部 API として受け取る。UI 層が `IProviderRegistry` の具象実装を直接生成することはしない。

---

### Requirement 3: MoCap ソース設定 UI

**Objective:** As a 開発者・検証者, I want 各 Slot に対して MoCap ソースを設定・切り替えできること, so that `IMoCapSource` の VMC 実装が正しくモーションデータを受信できることを確認できる。

#### Acceptance Criteria

1. The UI shall `IMoCapSourceRegistry.GetRegisteredTypeIds()` を呼び出して取得した MoCap ソース種別一覧を動的に列挙し、ドロップダウンまたはリストとして表示する。選択肢はハードコードしない。
2. When MoCap ソース種別が選択された場合, the UI shall 選択した `sourceTypeId` を持つ `MoCapSourceDescriptor` を構築し、`SlotSettings.moCapSourceDescriptor` に反映する。
3. The UI shall 選択した MoCap ソース種別に応じた接続パラメータ (VMC の場合は受信ポート番号等) を入力できるフィールドを提供し、入力値を `MoCapSourceDescriptor.Config` に反映する。
4. When `MoCapSourceDescriptor` が確定した場合, the UI shall `IMoCapSourceRegistry.Resolve()` を通じてソースを取得・初期化する。
5. The UI shall 複数 Slot に対して同一の `sourceTypeId` と同一の接続パラメータを持つ `MoCapSourceDescriptor` を設定できる。これにより `IMoCapSourceRegistry` が同一インスタンスの参照共有を行うことを UI 側で妨げない。
6. The UI shall 現在 Slot に割り当てられている MoCap ソースの種別 (`MoCapSourceDescriptor.SourceTypeId`) と接続状態を表示する。
7. When MoCap ソースが切り替えられた場合, the UI shall `IMoCapSourceRegistry.Release()` を通じて旧ソースの参照を解放してから新しい Descriptor を設定する。Slot 側から直接 `IMoCapSource.Dispose()` を呼び出さない。
8. The UI shall 未割り当て状態 (ソースなし) を選択できる操作を提供する。
9. The UI は `IMoCapSourceRegistry` への参照を機能部 API として受け取る。UI 層が `IMoCapSourceRegistry` の具象実装を直接生成することはしない。

---

### Requirement 4: Weight 操作 UI

**Objective:** As a 開発者・検証者, I want 各 Slot のモーション合成 Weight を UI から変更できること, so that `motion-pipeline` の Weight 適用処理が正しく機能することを確認できる。

#### Acceptance Criteria

1. The UI shall 各 Slot に対して Weight 値 (0.0〜1.0) を調整できるスライダーまたは数値入力フィールドを提供する。
2. When Weight 値が変更された場合, the UI shall 即座に `SlotSettings.weight` フィールドを更新し、`motion-pipeline` の Weight 適用に反映させる。
3. The UI shall 現在の Weight 値を数値として表示する。
4. When Weight 入力に 0.0〜1.0 の範囲外の値が入力された場合, the UI shall 入力値を 0.0〜1.0 にクランプして表示・反映する (機能部の `SlotSettings` クランプ仕様と整合する)。

---

### Requirement 5: 挙動確認用デモシーン

**Objective:** As a 開発者・検証者, I want UI の操作結果がシーン内のアバターに即座に反映されることを確認できること, so that 機能部 API の統合動作・参照共有動作を視覚的にデモンストレーションできる。

#### Acceptance Criteria

1. The demo scene shall `Samples~` ディレクトリ内に配置され、UPM パッケージのサンプルとしてインポート可能である。
2. The demo scene shall Slot 操作 UI・アバター選択 UI・MoCap ソース設定 UI・Weight 操作 UI の全コンポーネントを一画面に配置する。
3. When Slot を追加してアバターと MoCap ソースを設定した場合, the demo scene shall シーン内の指定エリアにアバターが表示され、MoCap データを受信するとアバターが動作を開始する。
4. The demo scene shall 複数 Slot を同時に稼働させ、Weight を変化させたときのモーション合成結果を視覚的に確認できる。
5. The demo scene shall **1 つの VMC MoCap ソース (同一 `sourceTypeId` + 同一 port 設定) を複数の Slot が参照共有するシナリオ**を含む。このシナリオにより、`IMoCapSourceRegistry` の参照共有機能が正しく動作すること (同一インスタンスが再利用されること) を視覚的に確認できる。
6. The demo scene shall エラー状態 (MoCap 未接続・アバター未割当等) を UI 上に可視化する。

---

### Requirement 6: UPM Samples~ 配布機構

**Objective:** As a パッケージ利用者, I want UPM 経由でサンプルをインポートできること, so that UPM パッケージを導入した任意のプロジェクトで本サンプルをすぐに試せる。

#### Acceptance Criteria

1. The ui-sample shall UPM パッケージの `Samples~` ディレクトリ直下に配置され、`package.json` の `samples` エントリに登録されている。
2. When Unity Package Manager の「Import」操作を実行した場合, the ui-sample shall プロジェクトの `Assets/Samples/` 以下に正しくコピーされる。
3. The ui-sample shall インポート後に追加設定なしでデモシーンを開いて実行できる状態になる。
4. The ui-sample shall サンプル専用のアセンブリ定義 (`RealtimeAvatarController.Samples.UI.asmdef`) を含み、機能部アセンブリを参照する。

---

### Requirement 7: UI 層と機能部の分離

**Objective:** As a アーキテクト, I want UI 層が機能部の API のみを通じて機能部を利用すること, so that 機能部が UI フレームワークに依存しない状態を保ちながら UI サンプルを提供できる。

#### Acceptance Criteria

1. The ui-sample assembly (`RealtimeAvatarController.Samples.UI`) shall 機能部アセンブリ群 (`RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` / `RealtimeAvatarController.MoCap.VMC` / `RealtimeAvatarController.Avatar.Builtin`) を参照する一方向の依存のみを持つ。
2. The 機能部アセンブリ群 shall `RealtimeAvatarController.Samples.UI` を参照しない (逆方向依存の禁止)。
3. The ui-sample shall 機能部コード内に UI フレームワーク固有の型 (UnityEngine.UI / UnityEngine.UIElements 等) を含めることを禁止する。
4. The UI層 shall `SlotRegistry` / `SlotManager` / `IProviderRegistry` / `IMoCapSourceRegistry` / `IAvatarProvider` / `IMoCapSource` の公開 API のみを通じて機能部を操作する。

---

### Requirement 8: UI フレームワーク選択

**Objective:** As a 設計者, I want 採用する UI フレームワークが明確に比較・検討されること, so that design フェーズで最適な UI フレームワークを選択できる。

#### Acceptance Criteria

1. The design phase shall UGUI (Unity UI) と UI Toolkit (UIElements) の 2 候補を評価する。
2. The requirements phase shall 以下の比較観点を design フェーズへの入力として提示する:

   **UGUI (Unity UI)**
   - 利点: Unity 2019 以降の幅広いバージョンで実績があり、既存のサンプル・ドキュメントが豊富
   - 利点: インスペクターで直感的に配置でき、サンプル UI としての学習コストが低い
   - 考慮点: Unity 6000.3 以降では UI Toolkit が主流となりつつあり、将来のサポート方向が異なる
   - 考慮点: Runtime UI 向けの Canvas / EventSystem セットアップが必要
   - 注記: `IProviderRegistry` / `IMoCapSourceRegistry` の列挙結果を動的ドロップダウンに反映する実装は UGUI の `TMP_Dropdown` 等で実現可能

   **UI Toolkit (UIElements)**
   - 利点: Unity 6000.3.10f1 (本プロジェクトの採用バージョン) で Runtime UI として安定利用可能
   - 利点: UXML / USS による宣言的 UI 記述でコードと UI 定義を分離しやすい
   - 利点: Unity が推奨する将来的な UI 実装方針に沿っており、長期メンテナンスに有利
   - 考慮点: 学習コストが UGUI より高く、既存コミュニティリソースが少ない
   - 考慮点: 一部のサードパーティとの統合で互換性の確認が必要
   - 注記: `DropdownField` / `ListView` を用いて `IProviderRegistry` / `IMoCapSourceRegistry` の列挙結果を動的に表示する実装は UI Toolkit で実現可能

3. The design phase shall Registry 列挙結果をドロップダウン等で動的表示する「動的候補列挙 UI」が UGUI・UI Toolkit のいずれでも実現可能であることを確認した上で、いずれか一方を選定し、選定理由を design ドキュメントに記載する。
4. When UI フレームワークを選定した場合, the design phase shall 選定したフレームワークに依存するコードをサンプルアセンブリ内に封じ込め、機能部への汚染を防ぐ設計を採用する。

---

### Requirement 9: UniRx による MotionStream 購読 (オプション)

**Objective:** As a 開発者・検証者, I want デバッグ目的で MoCap ソースから流れるモーションデータをリアルタイムにプレビューできること, so that モーションパイプラインに到達する前段のデータ受信状況を確認できる。

> **注意**: 本要件は**オプション**扱いである。デバッグ補助機能として有用だが、サンプル UI の必須動作要件ではなく、design フェーズで実装有無を判断する。

#### Acceptance Criteria

1. When `IMoCapSource` インスタンスへの参照が UI 側に渡された場合, the UI shall `IMoCapSource.MotionStream` (UniRx `IObservable<MotionFrame>`) を購読し、受信フレーム数や最新タイムスタンプ等のデバッグ情報をリアルタイムに表示できる。
2. The UI shall `MotionStream` の購読時に `.ObserveOnMainThread()` を使用して Unity メインスレッド上でデータを受け取る。
3. The UI shall Slot の削除・MoCap ソースの切り替え時に購読を適切に解除し (Dispose)、メモリリークを防ぐ。
4. UniRx は UI 層の**必須依存ではなく**、`MotionStream` 購読機能を使用するコンポーネントにのみ条件付きで依存する設計を推奨する。`IMoCapSource` インスタンスが UI に渡されない場合、UniRx なしでも UI のコア機能が動作すること。
