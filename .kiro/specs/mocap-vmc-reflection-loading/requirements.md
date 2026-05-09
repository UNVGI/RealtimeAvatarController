# Requirements Document

## Project Description (Input)
VMC reflection-based loading for optional dependency

本 Spec は先行 Spec `mocap-vmc-package-split` で別 Spec として繰り延べた "Reflection 化" (option ⑤) を扱う。`com.hidano.realtimeavatarcontroller.mocap-vmc` パッケージ (以下 "新パッケージ") の Runtime asmdef が現状 `references` に保持している `EVMC4U` および `uOSC.Runtime` の名前参照を撤廃し、これら任意依存ライブラリを実行時 Reflection 経由で解決する構造に置き換える。これにより、利用者は `Assets/EVMC4U/` 配下に自作 asmdef を作る必要がなくなり、VMC Sample のセットアップ手順が簡素化される。

## はじめに

本ドキュメントは `mocap-vmc-reflection-loading` Spec の要件を定義する。本 Spec は新パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` の Adapter 実装 (`EVMC4UMoCapSource` / `EVMC4USharedReceiver` / `VMCMoCapSourceFactory` / `VMCMoCapSourceConfig`) の **読み込み機構** のみを変更し、既存 VMC 仕様 (`HumanoidMotionFrame` 構造、属性ベース自己登録、共有 `ExternalReceiver` モデル、エラー通知方針、typeId `"VMC"`) は一切変更しない「読み込み機構リファクタリング」である。

### 採用方針

- **対象は `EVMC4U` / `uOSC` のみ**: Reflection 化対象は asmdef references 上の `EVMC4U` と `uOSC.Runtime` の 2 ライブラリに限定する。`UniRx` / `RealtimeAvatarController.Core` / `RealtimeAvatarController.Motion` 等の必須参照は据置とする (これらはコアパッケージ依存経由で常に存在する)。
- **コンパイル時依存の撤廃**: 新パッケージ Runtime asmdef の `references` から `"EVMC4U"` / `"uOSC.Runtime"` を削除し、`EVMC4U.ExternalReceiver` や `uOSC.uOscServer` の型名・メンバ名はソース上で文字列リテラル経由で解決する。
- **Reflection キャッシュ必須**: `Type.GetType` / `MethodInfo` / `FieldInfo` / `PropertyInfo` の解決結果は static フィールドへ 1 度だけキャッシュし、フレーム毎 Tick ループで再 Reflection しない (allocation / 性能要件)。
- **既存 SO アセット保全**: `VMCMoCapSourceConfig_Shared.asset` (GUID `5c4569b4a17944fba4667acebe26c25f`) を含む既存 SO アセットの `.meta` GUID は変更しない。
- **既存テストの位置づけ**: Reflection 化に伴い、EVMC4U / uOSC 型に対する直接 `using` を前提としたテスト (`EVMC4UMoCapSourceTests` 等) は、Reflection 経路を介した同等検証へ書き換えるか、`Assets/EVMC4U/` を準備した検証シナリオ B でのみ実行する分類とする (詳細は design フェーズで確定する)。
- **Editor / Runtime 共通動作**: Reflection 解決は Editor / Runtime 双方で同一動作を提供し、Editor Play Mode / Player Build / Domain Reload OFF いずれの構成でも正しく型解決ができる状態を維持する。

## スコープ境界

- **スコープ内**:
  - 新パッケージ Runtime asmdef (`RealtimeAvatarController.MoCap.VMC.asmdef`) の `references` から `"EVMC4U"` および `"uOSC.Runtime"` を削除する。
  - 新パッケージ Runtime ソース (`EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `VMCMoCapSourceConfig.cs` / `VMCMoCapSourceFactory.cs` / `AssemblyInfo.cs`) から `using EVMC4U;` および `using uOSC;` を削除する。
  - `EVMC4U.ExternalReceiver` の必要メンバ (`Model` / `RootPositionSynchronize` / `RootRotationSynchronize` / `LatestRootLocalPosition` / `LatestRootLocalRotation` / `GetBoneRotationsView()`) への Reflection 経由アクセス機構の新設。
  - `uOSC.uOscServer` の必要メンバ (`autoStart` / `port` / `StartServer()` / `StopServer()`) への Reflection 経由アクセス機構の新設。
  - 上記 Reflection アクセスの `MethodInfo` / `FieldInfo` / `PropertyInfo` キャッシュ層 (static cache) の新設。
  - `Type.GetType` 解決失敗 (利用者プロジェクトに EVMC4U / uOSC が未導入) 時の fallback 動作 (ソース起動失敗・診断的なエラー発行) の定義。
  - 利用者向け README / CHANGELOG の更新 (利用者側で `EVMC4U.asmdef` を作成する手順が **不要** になった旨)。
  - 新パッケージ Editor asmdef (`RealtimeAvatarController.MoCap.VMC.Editor.asmdef`) の `references` から (もし参照していれば) EVMC4U / uOSC を撤廃する整合作業。
  - 新パッケージ Tests asmdef の `references` から、Reflection 化に伴って不要となった EVMC4U / uOSC 名前参照を撤廃する整合作業 (テスト本体の書換要否は design フェーズで確定する)。
  - 検証シナリオ A' (新パッケージ + EVMC4U/uOSC 未導入時の compile 通過確認) と検証シナリオ B' (新パッケージ + EVMC4U/uOSC 導入時の動作同等性確認) の合否基準の明文化。

- **スコープ外**:
  - VMC プロトコル / OSC パース / 座標系変換 / ボーンマッピングの内部仕様変更 (引き続き EVMC4U に委譲)。
  - `IMoCapSource` / `IMoCapSourceFactory` / `IMoCapSourceRegistry` / `MoCapSourceConfigBase` 等の抽象 API シグネチャ変更。
  - typeId `"VMC"` の変更、`HumanoidMotionFrame` 形状の変更、属性ベース自己登録方式の変更、共有 `ExternalReceiver` のライフサイクル (refCount / DontDestroyOnLoad / SubsystemRegistration リセット) の変更。
  - EVMC4U 本家へのパッチ提案 (option ④、プロジェクト外活動)。
  - VMC 以外の MoCap source 実装 (Mediapipe / Webカメラ / Sensor 等)。
  - VMC Sender (送信側) 実装。
  - EVMC4U / uOSC を UPM 配布化する作業 (本家が UPM 化していない事実に依存する仮定は維持)。
  - `Assembly.LoadFrom` / `Assembly.Load(byte[])` 等で動的にアセンブリをロードする機構 (本 Spec は **既にプロジェクトにロード済み** のアセンブリから `Type.GetType` で型を引き当てるのみ)。
  - `il2cpp` における Reflection の link.xml 調整 (利用者プロジェクト側の事情。design フェーズで注意喚起のみ行い、強制要件にはしない)。

- **隣接 Spec / システムとの関係**:
  - `mocap-vmc-package-split` (前提 Spec): 本 Spec は当該 Spec 完了後の Phase で実施され、新パッケージのファイル配置・GUID・asmdef 名・依存方向 (一方向依存) を前提とする。
  - `mocap-vmc` (原 Spec): 既存 Adapter の **動作仕様 (parsing / emit / 共有モデル / エラー通知 / typeId)** は本 Spec で一切変更しない。本 Spec は当該仕様を維持したまま、参照解決経路のみを Reflection に置き換える。
  - `slot-core` (コアパッケージ): `IMoCapSource` / `IMoCapSourceFactory` / `MoCapSourceConfigBase` / `ISlotErrorChannel` / `RegistryLocator` への依存は据置で本 Spec の Reflection 化対象には含めない。
  - `motion-pipeline`: `HumanoidMotionFrame` 発行契約は据置。
  - 利用者プロジェクト: `Assets/EVMC4U/` への EVMC4U の `.unitypackage` 取り込みと `Assets/uOSC` (または `com.hecomi.uosc` / `com.hidano.uosc`) の導入は引き続き必要。本 Spec の達成により、利用者は **`EVMC4U.asmdef` を自作する作業は不要** になる。

---

## Requirements

### Requirement 1: 新パッケージ Runtime asmdef からの EVMC4U / uOSC 参照削除

**Objective:** As a 新パッケージのメンテナ, I want 新パッケージ Runtime asmdef のコンパイル時依存から EVMC4U / uOSC を切り離したい, so that 利用者が `EVMC4U.asmdef` を自作しない状態でも新パッケージがコンパイル可能となり、セットアップ手順を単純化できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall `Packages/com.hidano.realtimeavatarcontroller.mocap-vmc/Runtime/RealtimeAvatarController.MoCap.VMC.asmdef` の `references` 配列から `"EVMC4U"` および `"uOSC.Runtime"` を削除する。
2. The 新パッケージ Runtime asmdef shall 削除後の `references` として最低限 `"RealtimeAvatarController.Core"` / `"RealtimeAvatarController.Motion"` / `"UniRx"` のみを保持する (残存する必須参照の最終構成は design フェーズで確定する)。
3. When 利用者プロジェクトに EVMC4U / uOSC のいずれも導入されていない状態で新パッケージを単独導入した場合, the Unity Editor shall 新パッケージ Runtime asmdef のコンパイルを `EVMC4U` / `uOSC.Runtime` 名前参照不足に起因するエラー無く完了する。
4. The mocap-vmc-reflection-loading implementation shall 新パッケージ Runtime ソース全件 (`EVMC4UMoCapSource.cs` / `EVMC4USharedReceiver.cs` / `VMCMoCapSourceConfig.cs` / `VMCMoCapSourceFactory.cs` / `AssemblyInfo.cs` 等) から `using EVMC4U;` および `using uOSC;` を削除し、これら名前空間に属する型をソース上で直接識別子として参照しない状態にする。
5. The mocap-vmc-reflection-loading implementation shall 新パッケージ Editor asmdef (`RealtimeAvatarController.MoCap.VMC.Editor.asmdef`) と Tests asmdef (`RealtimeAvatarController.MoCap.VMC.Tests.EditMode.asmdef` / `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode.asmdef`) について、本 Spec で Reflection 化された型 (`EVMC4U.ExternalReceiver` / `uOSC.uOscServer`) を直接参照していた箇所を整理し、必要に応じて参照削除または Reflection 経路への置き換えを行う (テスト書換の最終方針は design フェーズで確定する)。
6. If 旧 asmdef references 配下に `"EVMC4U"` / `"uOSC.Runtime"` が残存していたとき, then 本 Spec の検証手順 shall 該当 asmdef を不合格として扱い、参照削除を完了させる。

---

### Requirement 2: EVMC4U `ExternalReceiver` への Reflection 経由アクセス

**Objective:** As a Adapter 実装, I want コンパイル時に `EVMC4U.ExternalReceiver` 型を参照しないままその必要メンバへアクセスしたい, so that EVMC4U を asmdef references で名前参照しなくても従来同等の Adapter 動作を提供できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall `EVMC4U.ExternalReceiver` 型 (アセンブリ修飾子は利用者プロジェクトの構成に依存する) を `Type.GetType("EVMC4U.ExternalReceiver, ...")` あるいは全 `AppDomain` アセンブリ走査による型名一致で 1 度だけ解決し、結果を static フィールドへキャッシュする。
2. The Reflection アクセス層 shall `EVMC4U.ExternalReceiver` の以下のメンバについてアクセサを提供する: `Model` (フィールド/プロパティ書込)、`RootPositionSynchronize` (書込)、`RootRotationSynchronize` (書込)、`LatestRootLocalPosition` (読込)、`LatestRootLocalRotation` (読込)、`GetBoneRotationsView()` メソッド (読込: `IReadOnlyDictionary<HumanBodyBones, Quaternion>` 互換の戻り値)。
3. When `EVMC4USharedReceiver.CreateInstance` が呼ばれたとき, the Reflection アクセス層 shall `gameObject.AddComponent` の引数を `Type` 渡しで指定して `ExternalReceiver` をシーンに追加し、初期設定 (`Model = null` / `RootPositionSynchronize = false` / `RootRotationSynchronize = false`) を Reflection 書込で適用する。
4. When `IEVMC4UMoCapAdapter.Tick` が呼ばれたとき, the Reflection アクセス層 shall `GetBoneRotationsView()` を Reflection 経由で呼び出し、戻り値を `IReadOnlyDictionary<HumanBodyBones, Quaternion>` として扱える形へ変換 (キャストまたは適応的列挙) して Adapter 側に返す。
5. The Reflection アクセス層 shall `MethodInfo` / `FieldInfo` / `PropertyInfo` の取得結果をすべて static フィールドへキャッシュし、Tick ごとに `Type.GetMethod` / `Type.GetField` を再実行しない。
6. While 共有 Receiver が生存中, the Reflection アクセス層 shall キャッシュ済み `MemberInfo` を使い続け、Domain Reload 発生時のみ `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 経路でキャッシュをクリアする。
7. The mocap-vmc-reflection-loading implementation shall `EVMC4U.ExternalReceiver` の Reflection 解決対象シグネチャ (型 FQN・メンバ名・引数型・戻り値型) を design フェーズで明文化し、以後 EVMC4U 本家側のシグネチャ変更追従が必要となった場合の参照点を README または design.md に記録する。

---

### Requirement 3: uOSC `uOscServer` への Reflection 経由アクセス

**Objective:** As a 共有 Receiver 実装, I want コンパイル時に `uOSC.uOscServer` 型を参照しないままポート設定と起動/停止を制御したい, so that uOSC を asmdef references で名前参照しなくても従来同等の受信ライフサイクルを提供できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall `uOSC.uOscServer` 型を `Type.GetType` または `AppDomain` アセンブリ走査で 1 度だけ解決し、結果を static フィールドへキャッシュする。
2. The Reflection アクセス層 shall `uOSC.uOscServer` の以下のメンバについてアクセサを提供する: `autoStart` (書込)、`port` (書込)、`StartServer()` (呼出)、`StopServer()` (呼出)。
3. When `EVMC4USharedReceiver.CreateInstance` が呼ばれたとき, the Reflection アクセス層 shall `gameObject.AddComponent` の引数を `Type` 渡しで指定して `uOscServer` をシーンに追加し、`autoStart = false` を Reflection 書込で適用する。
4. When `EVMC4USharedReceiver.ApplyReceiverSettings(int port)` が呼ばれたとき, the Reflection アクセス層 shall `StopServer()` → `port = port` → `StartServer()` の順に Reflection 経由で実行し、UDP ソケットの再バインドを保証する。
5. The Reflection アクセス層 shall `MethodInfo` / `FieldInfo` / `PropertyInfo` の取得結果をすべて static フィールドへキャッシュし、`ApplyReceiverSettings` 呼出ごとに再 Reflection しない。
6. If `uOSC.uOscServer` の `StartServer()` が `System.Net.Sockets.SocketException` を内部で送出した場合, then the Reflection 呼出 shall `TargetInvocationException.InnerException` を unwrap して呼び出し元 (`EVMC4UMoCapSource.Initialize`) へ `SocketException` をそのまま伝播する (既存仕様 `要件 8.4` 互換)。
7. The mocap-vmc-reflection-loading implementation shall `uOSC.uOscServer` の Reflection 解決対象シグネチャ (型 FQN・メンバ名・引数型・戻り値型) を design フェーズで明文化し、以後 uOSC 配布側 (`com.hecomi.uosc` / `com.hidano.uosc`) のシグネチャ変更追従が必要となった場合の参照点を README または design.md に記録する。

---

### Requirement 4: 任意依存欠落時のグレースフル失敗 (graceful failure)

**Objective:** As a 利用者, I want EVMC4U / uOSC をプロジェクトに導入していない状態で VMC ソースを起動しようとしたとき、Adapter が無音クラッシュせず明確な診断メッセージを返すこと, so that セットアップ不備に起因する起動失敗の原因を即座に特定できる。

#### Acceptance Criteria

1. When `Type.GetType` が `EVMC4U.ExternalReceiver` または `uOSC.uOscServer` の解決に失敗した場合, the Reflection アクセス層 shall 例外型 `InvalidOperationException` (もしくは新設の専用例外型。最終決定は design フェーズで行う) を生成し、メッセージに不足している型 FQN と利用者向けセットアップ手順への誘導文 (例: "Assets/EVMC4U/ への EVMC4U インポートと uOSC の導入が必要です") を含める。
2. When `EVMC4UMoCapSource.Initialize` が呼び出され、Reflection 解決が R-4.1 のとおり失敗した場合, the Adapter shall `ISlotErrorChannel.Publish` 経由で `SlotErrorCategory.InitFailure` (または同等カテゴリ) としてエラーを発行し、その上で `Initialize` の呼び出し元へ R-4.1 の例外を再スローする。
3. While Reflection 解決が一度失敗した状態, the Reflection アクセス層 shall その失敗を static フィールドにキャッシュし、後続の `Initialize` 呼出でも同等の例外を即座に返す (毎回アセンブリを総走査して負荷を増やさない)。
4. If 利用者が後から EVMC4U / uOSC をインポートし Domain Reload が発生した場合, then the Reflection アクセス層 shall `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 等のタイミングで失敗キャッシュを破棄し、再解決を試行する。
5. The Reflection アクセス層 shall 解決失敗時に Unity Console 上で `Debug.LogError` を 1 回だけ発行し (既存 `DefaultSlotErrorChannel` の抑制方針と整合)、ログメッセージに不足している型 FQN とセットアップ手順誘導文を含める。
6. The Reflection アクセス層 shall `EVMC4U.ExternalReceiver` のみ解決成功し `uOSC.uOscServer` の解決に失敗した場合 (またはその逆) を区別して報告し、利用者がどちらの依存を追加すべきかを誤認しない診断を提供する。

---

### Requirement 5: バージョン互換性 (Reflection シグネチャの固定化)

**Objective:** As a メンテナ, I want Reflection 経由でアクセスする EVMC4U / uOSC のメンバシグネチャが本 Spec 内で明示的に固定されていること, so that 上流ライブラリのアップデートで API が変更された際の互換確認ポイントが明確になり、回帰検証時の影響範囲を局所化できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall design.md に Reflection 経由でアクセスする EVMC4U メンバ表 (`EVMC4U.ExternalReceiver` の `Model` / `RootPositionSynchronize` / `RootRotationSynchronize` / `LatestRootLocalPosition` / `LatestRootLocalRotation` / `GetBoneRotationsView()`) を期待型・読書方向・想定セマンティクス付きで記載する。
2. The mocap-vmc-reflection-loading implementation shall design.md に Reflection 経由でアクセスする uOSC メンバ表 (`uOSC.uOscServer` の `autoStart` / `port` / `StartServer()` / `StopServer()`) を期待型・読書方向・想定セマンティクス付きで記載する。
3. The 新パッケージ README shall 動作確認済みの EVMC4U リビジョン (例: `gpsnmeajp/EasyVirtualMotionCaptureForUnity` の特定 commit / リリース) と uOSC のバージョン (例: `2.2.0`) を明記する。
4. If 上流ライブラリ側で R-5.1 / R-5.2 に列挙したメンバが改名・削除・シグネチャ変更された場合, then 本 Spec の Reflection アクセス層 shall その時点で `R-4` のグレースフル失敗経路に乗せ、利用者へセットアップ確認 (依存ライブラリのバージョン確認) を促すメッセージを発する。
5. The Reflection アクセス層 shall 1 つのメンバ取得失敗で関連する他メンバの探索を中断せず、解決可能な範囲のメンバ情報をログ出力に含めて、利用者がどのメンバが欠落しているかを判別できる診断を提供する。
6. The mocap-vmc-reflection-loading implementation shall 上流ライブラリのバージョン互換マトリクス更新手順 (どのファイルを編集して再検証するか) を design.md または README で記述する。

---

### Requirement 6: Editor / Runtime / Build 構成での同等動作

**Objective:** As a 利用者, I want Reflection 解決が Unity Editor (Edit Mode / Play Mode) と Player Build (Standalone / IL2CPP / Mono) のいずれの構成でも同一に動作すること, so that エディタで動いた挙動がビルド後も同じ条件で再現できる。

#### Acceptance Criteria

1. While Unity Editor Edit Mode, the Reflection アクセス層 shall アセンブリ走査時に Editor アセンブリ (`UnityEditor.dll` など) を不要に列挙対象に含めず、`UserAssembly` / `Assets-Csharp` 含む利用者アセンブリ群を優先して解決する (具体的な走査順序は design フェーズで確定する)。
2. While Unity Editor Play Mode, the Reflection アクセス層 shall Domain Reload OFF 設定下でも `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 経路でキャッシュを正しくリセットし、二重解決による不整合を発生させない。
3. When Mono バックエンドの Standalone Player ビルドで実行された場合, the Reflection アクセス層 shall Editor と同等の解決結果と Tick 駆動を再現する。
4. Where IL2CPP バックエンドが選択されている場合, the 新パッケージ README shall 利用者向けに `link.xml` または `[Preserve]` 属性等による `EVMC4U.ExternalReceiver` / `uOSC.uOscServer` の strip 防止手順への注意喚起を記述する (リフレクション対象が IL2CPP の Managed Stripping Level 設定によって除去されない手当)。
5. If Player Build 後の `Initialize` 呼出でリフレクション解決が IL2CPP stripping 起因で失敗した場合, then the Reflection アクセス層 shall R-4 と同一のグレースフル失敗経路に乗せ、診断ログで stripping 可能性に言及する。

---

### Requirement 7: セットアップ手順簡素化 (利用者作業の削減)

**Objective:** As a VMC 利用者, I want 新パッケージを導入するときに `Assets/EVMC4U/` 配下に自分で `EVMC4U.asmdef` を作成する作業が不要になること, so that EVMC4U の `.unitypackage` をインポートして uOSC を導入するだけで新パッケージが動作する。

#### Acceptance Criteria

1. The 新パッケージ README shall 「利用者は `Assets/EVMC4U/` 配下に `EVMC4U.asmdef` を作成する必要がない」旨を明記する。
2. The 新パッケージ README shall 利用者が必要とする最小手順を以下の順序で記述する: (a) コアパッケージ導入、(b) 新パッケージ導入、(c) `Assets/EVMC4U/` への EVMC4U `.unitypackage` 取込、(d) uOSC 導入 (`com.hecomi.uosc` / `com.hidano.uosc` / その他互換配布)。
3. When 利用者が R-7.2 の手順に従って構成を準備した場合, the Unity Editor shall 新パッケージ Runtime / Editor / Tests asmdef のコンパイルを成功させ、`SlotSettings_VMC_Slot1.asset` を用いた `VMCReceiveDemo.unity` シーンの動作 (VMC 受信 → `HumanoidMotionFrame` 発行 → Slot へのモーション適用) が再現できる。
4. The 新パッケージ CHANGELOG shall 本 Spec のリリースで利用者作業 (`EVMC4U.asmdef` 自作) が不要になった旨と、後方互換情報 (利用者が既存自作 asmdef を残しても新パッケージのコンパイルは引き続き成功する) を記録する。
5. The mocap-vmc-reflection-loading implementation shall コアパッケージ側 README (リポジトリルートまたはコアパッケージ内 README) を必要に応じて更新し、本 Spec 適用後の VMC セットアップ手順への参照を最新化する。
6. The mocap-vmc-reflection-loading implementation shall `mocap-vmc-package-split` Spec の Requirement 8 (新パッケージのコンパイル要件と利用者プロジェクト前提) で「`EVMC4U.asmdef` の作成手順を README に記載する」とした要件について、本 Spec 完了後に当該記述が **不要** になった旨を新パッケージ README で明確化する。

---

### Requirement 8: 既存 VMC 仕様の不変性 (parsing / emit / 共有モデル / typeId)

**Objective:** As a 既存テスト保守者・既存 SO 利用者, I want 本 Spec の適用が VMC parsing / emit / 共有 Receiver / typeId / エラー通知の **動作仕様** を一切変更しないこと, so that 既存テスト一式 (EditMode / PlayMode) と既存 SO アセットが本 Spec 完了後もそのまま動作する。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall typeId `"VMC"` (`VMCMoCapSourceFactory.VmcSourceTypeId`) を変更しない。
2. The mocap-vmc-reflection-loading implementation shall `HumanoidMotionFrame` 形状 (`Timestamp` / `Muscles` / `RootPosition` / `RootRotation` / `BoneLocalRotations`) を変更しない。
3. The mocap-vmc-reflection-loading implementation shall `EVMC4USharedReceiver` の以下の振る舞いを変更しない: DontDestroyOnLoad GameObject 1 個の `s_refCount` 生存管理、`SubsystemRegistration` での static リセット、`Subscribe` / `Unsubscribe` の冪等性、LateUpdate での Tick スナップショット駆動、Tick 内例外を `IEVMC4UMoCapAdapter.HandleTickException` に委譲する二重安全網。
4. The mocap-vmc-reflection-loading implementation shall `EVMC4UMoCapSource` の状態機械 (`Uninitialized` → `Running` → `Disposed`) と冪等な `Shutdown` / `Dispose` の挙動を変更しない。
5. The mocap-vmc-reflection-loading implementation shall `VMCMoCapSourceConfig` のシリアライズ表現 (`port` / `bindAddress` フィールド名・型) を変更せず、既存 `VMCMoCapSourceConfig_Shared.asset` の `.meta` GUID (`5c4569b4a17944fba4667acebe26c25f`) を含む既存 SO `.meta` を一切書き換えない。
6. The mocap-vmc-reflection-loading implementation shall `VMCMoCapSourceFactory` の自己登録経路 (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` ランタイム登録、Editor 側 `[InitializeOnLoadMethod]` 登録、`RegistryConflictException` の通知) を変更しない。
7. While 検証シナリオ B' 実行中, the 新パッケージ shall `mocap-vmc-package-split` Spec の Requirement 4 / Requirement 8 で実行されていた既存 VMC EditMode / PlayMode テスト一式を成功させる (テスト本体の Reflection 化対応有無は本 Spec で確定する書換方針に従う)。

---

### Requirement 9: 非機能要件 (性能・アロケーション)

**Objective:** As a ランタイム性能を意識する利用者, I want Reflection 経路化が Tick あたりのアロケーション・実行時間を顕著に増やさないこと, so that 1 Slot あたりの VMC Tick が従来同等のフレームレート影響に収まる。

#### Acceptance Criteria

1. The Reflection アクセス層 shall `Type.GetType` / `Type.GetField` / `Type.GetMethod` / `Type.GetProperty` をフレーム毎の Tick 経路で呼び出さず、初回解決時にのみ実行する。
2. The Reflection アクセス層 shall `MethodInfo.Invoke` / `FieldInfo.GetValue` / `FieldInfo.SetValue` / `PropertyInfo.GetValue` / `PropertyInfo.SetValue` の呼出を最適化するため、ホットパス (`Tick` / `ApplyReceiverSettings`) で利用するメンバについては可能な限り `Delegate.CreateDelegate` または `Expression Tree` ベースのオープンデリゲート化を行う (具体的手段は design フェーズで確定する)。
3. When Tick 1 回あたりの emit ループが実行される場合, the Reflection アクセス層 shall 追加で発生する GC アロケーションを `0 byte/tick` (定常状態) に抑える設計目標とし、`object[]` 引数配列等の per-call allocation を回避する手段 (キャッシュした引数配列 / オープンデリゲート / `out` パラメタ無し設計) を採用する。
4. The mocap-vmc-reflection-loading implementation shall 性能検証手順 (例: `Unity Profiler` または `PerformanceTestExtensions` を用いた Tick あたり時間/アロケーション計測) を design.md または validation 手順に明記し、Reflection 化前後で測定可能な比較基準を確立する。
5. The Reflection アクセス層 shall 解決済み MemberInfo / Delegate を `static readonly` フィールドへ格納し、複数 Adapter インスタンス間で共有することでメモリオーバーヘッドを最小化する。
6. While Domain Reload OFF 設定, the Reflection アクセス層 shall キャッシュ持ち越しによる stale 参照を発生させないため、`SubsystemRegistration` 経路でキャッシュを正しくクリアし、再 Play 開始時に新しい `MemberInfo` を再取得する。

---

### Requirement 10: ドキュメンテーション (README / CHANGELOG / steering)

**Objective:** As a 利用者・メンテナ, I want 新パッケージ README / CHANGELOG とプロジェクト steering ドキュメントが Reflection 化後の構造を正しく反映していること, so that 利用者は最新のセットアップ手順を理解でき、メンテナは Reflection 化されたシグネチャと依存マトリクスをプロジェクトメモリとして保持できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall 新パッケージ README を更新し、(a) Reflection 化により利用者作業が簡素化された旨、(b) 動作確認済み EVMC4U リビジョン / uOSC バージョン、(c) IL2CPP 利用時の strip 対策注意喚起、(d) `Assets/EVMC4U/` への asmdef 自作が **不要** であることを記載する。
2. The mocap-vmc-reflection-loading implementation shall 新パッケージ CHANGELOG に本 Spec による変更 (Reflection 化・asmdef references 削除・利用者手順簡素化) を版番号と共に記録する。
3. The mocap-vmc-reflection-loading implementation shall コアパッケージ側 README (リポジトリルートまたはコアパッケージ内 README) に変更がある場合は更新し、新パッケージのセットアップ手順への参照を最新化する。
4. The mocap-vmc-reflection-loading implementation shall `.kiro/steering/structure.md` を更新し、新パッケージが Reflection 経由で EVMC4U / uOSC を利用するため asmdef references 上の名前参照を持たない旨をプロジェクト構造記述に追加する。
5. Where `.kiro/steering/` 配下にプロジェクト構造を参照する他のドキュメント (例: `tech.md` / `product.md`) が存在する場合, the 該当ドキュメント shall 本 Spec 適用に伴う矛盾が無いか確認され、必要に応じて軽微な追記が行われる。
6. The mocap-vmc-reflection-loading implementation shall design.md に Reflection 解決対象シグネチャ表 (R-5.1 / R-5.2) を記載し、上流ライブラリのバージョンが更新された際にメンテナが参照する単一情報源として位置づける。

---

### Requirement 11: 受け入れ検証手順 (合否判定)

**Objective:** As a メンテナ・レビュアー, I want 本 Spec の完了判定に使う検証手順が明確に定義されていること, so that 実装完了時に客観的な合否判定ができ、回帰検証時にも同一手順を再現できる。

#### Acceptance Criteria

1. The mocap-vmc-reflection-loading implementation shall 検証シナリオ A' として、コアパッケージ + 新パッケージのみを導入し EVMC4U / uOSC を **未導入** とした状態で Unity Editor を起動し、新パッケージ Runtime / Editor / Tests asmdef がコンパイル成功することを確認する (`Initialize` 呼出はこの構成では行わない、または R-4.2 のとおり InitFailure 経路で失敗することを許容する)。
2. The mocap-vmc-reflection-loading implementation shall 検証シナリオ B' として、コアパッケージ + 新パッケージ + EVMC4U (`Assets/EVMC4U/` 取込、利用者 asmdef 自作 **なし**) + uOSC を導入した状態で Unity Test Runner を実行し、`RealtimeAvatarController.MoCap.VMC.Tests.EditMode` および `RealtimeAvatarController.MoCap.VMC.Tests.PlayMode` の全テストが成功することを確認する。
3. When 検証シナリオ B' において新パッケージの VMC サンプル (`VMCReceiveDemo.unity`) を Play Mode で実行した場合, the VMC サンプル shall VMC 受信 → `HumanoidMotionFrame` 発行 → Slot へのモーション適用が再現でき、`mocap-vmc-package-split` Spec の Requirement 10 検証シナリオ B と同一の動作を提供する。
4. When 検証シナリオ A' / B' のいずれにおいても, the 新パッケージ Runtime asmdef の `references` shall `"EVMC4U"` および `"uOSC.Runtime"` を含まない状態で検証を行うものとし、検証時点で残存していた場合は当該シナリオを不合格とする。
5. If 検証シナリオ A' または B' のいずれかが失敗した場合, then 本 Spec shall 未完了として扱い、原因の特定と修正後に検証を再実施する。
6. The mocap-vmc-reflection-loading implementation shall 検証シナリオ A' / B' の手順とチェック項目を本 Spec の design / tasks フェーズで具体化し、CI または手動検証手順 (例: `Unity.exe -batchmode -runTests`) として再現可能な形で記録する。
7. The mocap-vmc-reflection-loading implementation shall 性能要件 (R-9) 達成の検証として、Reflection 化前後の Tick あたりアロケーション計測 (Profiler または PerformanceTestExtensions) の合否基準を design / validation 段で確立する。

---
