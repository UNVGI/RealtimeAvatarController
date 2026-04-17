// VMC プロトコルの OSC アドレスハンドリング構造は以下を参考に実装:
// gpsnmeajp/EasyVirtualMotionCaptureForUnity (EVMC4U)
// https://github.com/gpsnmeajp/EasyVirtualMotionCaptureForUnity
// Copyright (c) 2019 gpsnmeajp, MIT License
using System;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC.Internal
{
    /// <summary>
    /// VMC プロトコル OSC メッセージのアドレスプレフィックスルーティングを担う内部クラス
    /// (design.md §6.2 / requirements.md 要件 5-1, 5-2, 5-4, 7-2)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>com.hidano.uosc</c> (<see cref="uOSC"/>) の受信コールバックから渡される
    /// OSC メッセージ (<see cref="Message"/>) を、アドレスごとに <see cref="VmcFrameBuilder"/>
    /// への転送・スキップ・無視へ振り分ける薄いルータ。OSC パース自体は uOSC が担う。
    /// </para>
    /// <para>
    /// <see cref="VmcFrameBuilder"/> への転送中に例外が発生した場合 (引数不足・型不一致等) は
    /// 例外を呼び出し元へ伝播させず、コンストラクタで受け取った <c>onError</c> コールバックへ
    /// 通知する (design.md §8.1, §8.2: <c>PublishError</c> 相当の委譲先)。
    /// </para>
    /// </remarks>
    internal sealed class VmcMessageRouter
    {
        private readonly VmcFrameBuilder _frameBuilder;
        private readonly Action<Exception> _onError;

        /// <summary>
        /// <see cref="VmcMessageRouter"/> を生成する。
        /// </summary>
        /// <param name="frameBuilder">Bone/Root 受信値を蓄積する <see cref="VmcFrameBuilder"/>。</param>
        /// <param name="onError">
        /// 引数不足や型不一致等の非致命エラー通知先 (<c>VmcOscAdapter.PublishError</c> 相当)。
        /// <c>null</c> の場合は例外通知がスキップされる。
        /// </param>
        public VmcMessageRouter(VmcFrameBuilder frameBuilder, Action<Exception> onError)
        {
            _frameBuilder = frameBuilder ?? throw new ArgumentNullException(nameof(frameBuilder));
            _onError = onError;
        }

        /// <summary>
        /// OSC アドレスに応じて受信メッセージを振り分ける。
        /// </summary>
        /// <param name="address">OSC アドレス (例: <c>/VMC/Ext/Bone/Pos</c>)。</param>
        /// <param name="data">uOSC が受信した OSC メッセージ本体。</param>
        /// <remarks>
        /// 例外はすべて捕捉し、<c>onError</c> コールバックに委譲する (呼び出し元へは伝播しない)。
        /// 未知アドレスは無視される。
        /// </remarks>
        public void Route(string address, Message data)
        {
            try
            {
                switch (address)
                {
                    case "/VMC/Ext/Root/Pos":
                        _frameBuilder.SetRoot(data);
                        break;
                    case "/VMC/Ext/Bone/Pos":
                        _frameBuilder.SetBone(data);
                        break;
                    case "/VMC/Ext/Blend/Val":
                        // 初期版: 受信のみ・変換スキップ (design.md §7.4)
                        break;
                    case "/VMC/Ext/Blend/Apply":
                        // 初期版: 受信のみ・変換スキップ (design.md §7.4)
                        break;
                    case "/VMC/Ext/OK":
                        // 疎通確認。初期版ではログのみ・処理なし (design.md §3)
                        break;
                    case "/VMC/Ext/T":
                        // VMC v2.5 の不安定タイムスタンプは使用しない (design.md §3)
                        break;
                    default:
                        // 未知アドレスは無視 (design.md §6.2)
                        break;
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
        }
    }
}
