# Realtime Avatar Controller

VTuber ユースケース向けのランタイムアバターコントローラーです。Slot ベースの MoCap ソース管理、アバタープロバイダ抽象化、モーションパイプラインを Unity 向けに提供します。

## 前提条件

- **Unity 6000.3.10f1** 以降
- **OpenUPM CLI** (任意 — 手動 `manifest.json` 編集でも可)

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
      "name": "npm (hidano)",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "com.hidano"
      ]
    }
  ]
}
```

> **既存の `scopedRegistries` がある場合**: 上記 2 つのオブジェクトを配列に**追記**してください。既存エントリは削除しないでください。
>
> - **OpenUPM** (`https://package.openupm.com`): UniRx (`com.neuecc`) および UniTask (`com.cysharp`) の取得に使用します。
> - **npm (hidano)** (`https://registry.npmjs.com`): OSC ライブラリ `com.hidano.uosc` の取得に使用します。

### Step 2: dependencies への追加

同じ `manifest.json` の `dependencies` セクションに以下を追加してください。

```json
{
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "0.1.0"
  }
}
```

### Step 3: manifest.json 完全スニペット例 (新規プロジェクト向け)

新規プロジェクトで最初から導入する場合は、以下の完全な `manifest.json` を参考にしてください。

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
  ],
  "dependencies": {
    "com.hidano.realtimeavatarcontroller": "0.1.0",
    "com.unity.ugui": "2.0.0"
  }
}
```

> `com.unity.ugui` は例示用です。実際のプロジェクト依存は適宜追加してください。`com.hidano` スコープの `npm (hidano)` registry は `com.hidano.uosc` を解決するために必須です。

### Step 4: Package Manager UI での確認

1. Unity エディタの **Window > Package Manager** を開く
2. 左上のドロップダウンから **My Registries** を選択
3. `Realtime Avatar Controller` が表示されることを確認
4. **Install** ボタンをクリック (Step 2 で追記済みの場合はすでにインストール済みと表示されます)

### Step 5: UI サンプルのインポート

1. Package Manager の `Realtime Avatar Controller` エントリを選択
2. 右側の **Samples** セクションを展開
3. **UI Sample** の横にある **Import** ボタンをクリック
4. `Assets/Samples/Realtime Avatar Controller/<version>/UI/` にサンプルがコピーされます
5. `SampleScene.unity` を開いてデモを確認してください

## 補足: openupm-cli を使う場合 (任意)

```bash
# プロジェクトルートで実行
openupm add com.hidano.realtimeavatarcontroller
```

`openupm-cli` は scoped registry の追加と `dependencies` への追記を自動で行います。UniRx・UniTask も依存として自動解決されます。

> **注意**: `openupm-cli` は OpenUPM レジストリの scoped registry のみを自動追加します。`com.hidano.uosc` の取得に必要な npm (hidano) registry は **手動で追加**する必要があります。Step 1 のスニペットを参照してください。

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

> **注意**: git URL インストールでは Unity が依存パッケージ (`com.neuecc.unirx`・`com.cysharp.unitask`・`com.hidano.uosc`) を自動解決しません。Step 1 の scoped registry 追加と各依存パッケージの手動追加が別途必要になります。通常のインストールには **scoped registry 方式 (Step 1〜3) を推奨**します。

## バージョン固定ポリシー

本パッケージは全依存パッケージを **exact version** で固定し、再現性を優先しています。

| パッケージ | バージョン | 取得元 |
|------------|-----------|--------|
| `com.neuecc.unirx` | `7.1.0` | OpenUPM |
| `com.cysharp.unitask` | `2.5.10` | OpenUPM |
| `com.hidano.uosc` | `1.0.0` | npm (hidano) |

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
