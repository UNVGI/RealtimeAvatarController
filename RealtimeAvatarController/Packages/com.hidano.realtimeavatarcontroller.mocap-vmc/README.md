# Realtime Avatar Controller MoCap VMC

`com.hidano.realtimeavatarcontroller.mocap-vmc` は、Realtime Avatar Controller 向けの VMC MoCap Source パッケージです。
EVMC4U の `ExternalReceiver` を利用して VMC を受信し、コアパッケージの MoCap Source / Motion Pipeline へ接続するための実装、Editor 連携、VMC サンプルを提供します。

## インストール

1. `Packages/manifest.json` の `dependencies` に、コアパッケージとこのパッケージを追加します。

   ```json
   {
     "dependencies": {
       "com.hidano.realtimeavatarcontroller": "0.1.0",
       "com.hidano.realtimeavatarcontroller.mocap-vmc": "0.1.0"
     }
   }
   ```

   Git URL やローカルパスで導入する場合も、両方のパッケージを同じ互換バージョンで指定してください。

2. uOSC を `Packages/manifest.json` の `dependencies` に追加します。

   ```json
   {
     "dependencies": {
       "com.hidano.uosc": "<使用する uOSC のバージョン>"
     }
   }
   ```

   利用している uOSC 配布元に応じて、パッケージ名とバージョンはプロジェクト側の設定に合わせてください。このパッケージの asmdef は `uOSC.Runtime` を参照します。

3. EVMC4U の公式 unitypackage をインポートし、`Assets/EVMC4U/` に配置します。

4. `Assets/EVMC4U/EVMC4U.asmdef` をユーザー側で作成します。

   EVMC4U の公式 unitypackage には asmdef が含まれていません。一方、このパッケージの asmdef は `EVMC4U` という名前のアセンブリを参照します。EVMC4U が `Assembly-CSharp` に入ったままだと asmdef 参照を解決できないため、利用プロジェクト側で `EVMC4U` アセンブリを定義する必要があります。

   最小構成は次のとおりです。

   ```json
   {
     "name": "EVMC4U",
     "references": [
       "uOSC.Runtime"
     ]
   }
   ```

5. バージョン互換性を確認します。

   このパッケージ `0.1.0` は、`com.hidano.realtimeavatarcontroller` `0.1.0` を前提にしています。`manifest.json` に指定したコアパッケージのバージョンが、このパッケージの `package.json` にある `dependencies.com.hidano.realtimeavatarcontroller` と一致していることを確認してください。

## 既存 mocap-vmc spec から継承する仕様

既存の `mocap-vmc` spec で定義済みの VMC 受信仕様は、このパッケージへ移動した後も変更しません。

- `typeId="VMC"` による MoCap Source 識別
- 属性ベースの自己登録
- 共有 `ExternalReceiver` による受信モデル
- `HumanoidMotionFrame` の発行

## 既知の制限

- Reflection ベースの読み込み、つまり option ⑤ はまだ実装していません。これは別 spec で対応する予定です。
- EVMC4U upstream の unitypackage には asmdef が同梱されていないため、利用プロジェクト側で `EVMC4U.asmdef` を作成する必要があります。
