# Realtime Avatar Controller

VTuber ユースケース向けのランタイムアバターコントローラーです。Slot ベースの MoCap ソース管理、アバタープロバイダ抽象化、モーションパイプラインを Unity 向けに提供します。

## 前提条件

- **Unity 6000.3.10f1** 以降
- **EVMC4U** (`Assets/EVMC4U/` に `.unitypackage` を取り込み): VMC (Virtual Motion Capture) 受信機能を利用する場合に必須。`RealtimeAvatarController.MoCap.VMC` アセンブリは EVMC4U の `ExternalReceiver` / `uOscServer` を内部で参照するため、未導入のままでは当該アセンブリがコンパイルエラーになる。
  - 配布形態: `.unitypackage` のみ (UPM / OpenUPM / npm 配布なし)
  - 取得元: <https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity>

## インストール

### Step 1: scoped registry の追加

Unity プロジェクトの `Packages/manifest.json` を開き、`scopedRegistries` セクションに以下の **2 つのレジストリ**を追加してください。両レジストリの追加が必須です。

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
      "name": "npmjs",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "com.hecomi"
      ]
    }
  ]
}
```

> **既存の `scopedRegistries` がある場合**: 上記 2 つのオブジェクトを配列に**追記**してください。既存エントリは削除しないでください。
>
> - **OpenUPM** (`https://package.openupm.com`): UniRx (`com.neuecc`) および UniTask (`com.cysharp`) の取得に使用します。
> - **npmjs** (`https://registry.npmjs.com`): OSC ライブラリ `com.hecomi.uosc` の取得に使用します。

### Step 2: dependencies への追加

同じ `manifest.json` の `dependencies` セクションに以下を追加してください。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "0.1.0"
  }
}
```

### Step 3: UI サンプルのインポート

1. Package Manager の `Realtime Avatar Controller` エントリを選択
2. 右側の **Samples** セクションを展開
3. **UI Sample** の横にある **Import** ボタンをクリック
4. `Assets/Samples/Realtime Avatar Controller/<version>/UI/` にサンプルがコピーされます
5. `SampleScene.unity` を開いてデモを確認してください

## 補足: git URL によるインストール (任意)

scoped registry を使わずに git URL で直接インストールする場合は、`?path=` パラメータでパッケージパスを指定してください。

`Packages/manifest.json` の `dependencies` に以下のように記述します。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "https://github.com/Hidano-Dev/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller"
  }
}
```

> **注意**: git URL インストールでは Unity が依存パッケージ (`com.neuecc.unirx`・`com.cysharp.unitask`・`com.hecomi.uosc`) を自動解決しません。Step 1 の scoped registry 追加と各依存パッケージの手動追加が別途必要になります。通常のインストールには **scoped registry 方式 (Step 1・Step 2) を推奨**します。

## バージョン固定ポリシー

本パッケージは全依存パッケージを **exact version** で固定し、再現性を優先しています。

| パッケージ | バージョン | 取得元 |
|------------|-----------|--------|
| `com.neuecc.unirx` | `7.1.0` | OpenUPM |
| `com.cysharp.unitask` | `2.5.10` | OpenUPM |
| `com.hecomi.uosc` | `2.2.0` | npmjs |

### アップグレード方針

- **パッケージ管理者**: セキュリティ修正等が必要な場合は `package.json` の `dependencies` を更新してリリースします。
- **利用者が個別にバージョンを変更する場合**: 自プロジェクトの `Packages/manifest.json` の `dependencies` に対象パッケージとバージョンを直接記述することで、`package.json` の宣言を上書きできます。

```json
{
  "dependencies": {
    "com.neuecc.unirx": "7.2.0"
  }
}
```

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。
