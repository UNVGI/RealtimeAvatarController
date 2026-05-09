# Realtime Avatar Controller

VTuber ユースケース向けのランタイムアバターコントローラーです。Slot ベースの MoCap ソース管理、アバタープロバイダ抽象化、モーションパイプラインを Unity 向けに提供します。

## VMC 分離

VMC (Virtual Motion Capture) 受信機能は、コアパッケージから別パッケージ `com.hidano.realtimeavatarcontroller.mocap-vmc` に分離されました。VMC を利用するプロジェクトでは、既存のコアパッケージに加えて VMC パッケージを `Packages/manifest.json` の `dependencies` に追加してください。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "0.1.0",
    "com.hidano.realtimeavatarcontroller.mocap-vmc": "0.1.0"
  }
}
```

git URL で直接導入している場合は、同じ `dependencies` に VMC パッケージのパスも追加します。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "https://github.com/Hidano-Dev/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller",
    "com.hidano.realtimeavatarcontroller.mocap-vmc": "https://github.com/Hidano-Dev/RealtimeAvatarController.git?path=RealtimeAvatarController/Packages/com.hidano.realtimeavatarcontroller.mocap-vmc"
  }
}
```

VMC 側の導入手順、uOSC のみ依存する準備、VMC Sample の利用方法は [VMC パッケージ README](../com.hidano.realtimeavatarcontroller.mocap-vmc/README.md) を参照してください。

UI Sample は Stub MoCap Source 経由で動作するため、VMC パッケージや uOSC を導入しなくても Slot UI の検証を完結できます。

## 前提条件

- **Unity 6000.3.10f1** 以降
- **VMC 受信**: VMC パッケージは uOSC のみ依存します。導入手順と VMC Sample は [VMC パッケージ README](../com.hidano.realtimeavatarcontroller.mocap-vmc/README.md) を参照してください。

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

同じ `manifest.json` の `dependencies` セクションに、本パッケージと依存パッケージ (UniRx・UniTask・uOSC) を追加してください。

本パッケージは Unity 公式以外の依存パッケージのバージョンを `package.json` で固定しない方針のため、利用者プロジェクトの要件に合わせて選択してください。下記スニペットは[動作確認済みバージョン](#動作確認済みバージョン)です。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "0.1.0",
    "com.neuecc.unirx": "7.1.0",
    "com.cysharp.unitask": "2.5.10",
    "com.hecomi.uosc": "2.2.0"
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

> **注意**: git URL 方式でも依存パッケージ (`com.neuecc.unirx`・`com.cysharp.unitask`・`com.hecomi.uosc`) は別途 `dependencies` に追加する必要があります。Step 1 の scoped registry 追加も引き続き必須です。

## 動作確認済みバージョン

本パッケージは Unity 公式以外の依存パッケージのバージョンを `package.json` で**固定しません**。組み込み先プロジェクトの既存依存と衝突しないよう、利用者が `manifest.json` で各々選択する方針です。

以下は開発・動作確認に使用したバージョンです。Step 2 のスニペットと同一内容で、変更しなければそのまま動作します。

| パッケージ | バージョン | 取得元 |
|------------|-----------|--------|
| `com.neuecc.unirx` | `7.1.0` | OpenUPM |
| `com.cysharp.unitask` | `2.5.10` | OpenUPM |
| `com.hecomi.uosc` | `2.2.0` | npmjs |

これらと異なるバージョンを使う場合は `manifest.json` の `dependencies` の値を直接書き換えてください。本パッケージ側の制約はありません。

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。
