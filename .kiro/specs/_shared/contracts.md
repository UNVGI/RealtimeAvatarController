# Spec 間公開契約

本ドキュメントは、Spec 間で共有される公開 IF・データ契約・命名・アセンブリ境界を定義する。

## 位置付け

- **主執筆**: `slot-core` Spec の requirements エージェント (Wave 1)
- **参照**: 他 5 Spec の requirements / design エージェント (Wave 2 以降)
- **更新**: design フェーズでシグネチャを確定、以降は合意変更のみ

## 記入ガイド

各セクションには `<!-- TODO: slot-core agent -->` マーカーを置いている。Wave 1 のエージェントは、このマーカーを削除したうえでセクションを埋めること。

---

## 1. Slot データモデル

<!-- TODO: slot-core agent - Slot 構造体 / クラス / ScriptableObject の骨格を記述 -->

### 1.1 保持する設定項目

想定される項目:
- Slot 識別子 (ID)
- 紐付けアバター参照
- 紐付け MoCap ソース参照
- 紐付け Facial Controller 参照 (抽象)
- 紐付け LipSync Source 参照 (抽象)
- Weight 値 (0.0〜1.0)
- Slot 名 / 表示名など運用メタデータ

### 1.2 シリアライズ形式

<!-- TODO: slot-core agent - ScriptableObject / JSON / MonoBehaviour のどれを採用するか -->

### 1.3 ライフサイクル

<!-- TODO: slot-core agent - 生成・破棄タイミング、リソース所有関係 -->

---

## 2. MoCap ソース抽象

### 2.1 `IMoCapSource` (仮) シグネチャ

<!-- TODO: slot-core agent - 以下観点を埋める -->
- 初期化 / 破棄
- モーションデータ取得 API (pull 型 / push 型 / イベント型のどれか)
- ソース種別メタデータ
- 通信パラメータ設定 (Slot 単位)
- スレッド安全性の要求

### 2.2 モーションデータ中立表現

<!-- TODO: motion-pipeline agent (Wave 2) - slot-core と合意の上で型を定義 -->

想定される内容:
- Humanoid 向け: HumanPose 相当 (Muscle 値 + Root)
- Generic 向け: Transform 配列 (Wave 2 では抽象のみ)

---

## 3. アバター供給抽象

### 3.1 `IAvatarProvider` (仮) シグネチャ

<!-- TODO: slot-core agent - 以下観点を埋める -->
- アバター要求 API (同期 / 非同期)
- アバター解放 API
- Provider 種別メタデータ
- 供給結果 (Prefab インスタンス / GameObject 参照)

### 3.2 Addressable 拡張余地

- 初期段階で Addressable Provider は実装しない
- `IAvatarProvider` は同期・非同期のいずれの具象実装も許容するシグネチャとする

---

## 4. 表情制御抽象 (受け口のみ)

### 4.1 `IFacialController` (仮) シグネチャ

<!-- TODO: slot-core agent - 骨格のみ定義。実装はしない -->

### 4.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。

---

## 5. リップシンク抽象 (受け口のみ)

### 5.1 `ILipSyncSource` (仮) シグネチャ

<!-- TODO: slot-core agent - 骨格のみ定義。実装はしない -->

### 5.2 備考

初期段階では具象実装は存在しない。Slot に対して null / 未割当が許容される。

---

## 6. アセンブリ / 名前空間境界

### 6.1 アセンブリ定義 (asmdef) の想定構成

<!-- TODO: project-foundation agent - 命名確定 -->

想定例:
- `RealtimeAvatarController.Core` (slot-core + 抽象群)
- `RealtimeAvatarController.Motion` (motion-pipeline)
- `RealtimeAvatarController.MoCap.VMC` (mocap-vmc)
- `RealtimeAvatarController.Avatar.Builtin` (avatar-provider-builtin)
- `RealtimeAvatarController.Samples.UI` (ui-sample / Samples~ 内)

### 6.2 名前空間規約

<!-- TODO: project-foundation agent - 命名確定 -->

ルート名前空間候補: `RealtimeAvatarController.*`

---

## 7. 変更管理

- 本ドキュメントは Spec 間の**公式合意**として扱う
- requirements フェーズ以降の変更は、影響を受ける全 Spec の担当エージェント (または人間レビュー) で合意が必要
- シグネチャ確定は design フェーズで完了させる
