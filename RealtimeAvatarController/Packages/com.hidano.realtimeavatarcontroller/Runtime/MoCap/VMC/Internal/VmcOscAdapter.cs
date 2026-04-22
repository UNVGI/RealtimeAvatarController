using System;
using System.Net.Sockets;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC.Internal
{
    /// <summary>
    /// <c>com.hidano.uosc</c> の <see cref="uOscServer"/> が通知する受信メッセージを
    /// <see cref="VmcMessageRouter"/> へ転送し、<see cref="VmcFrameBuilder"/> が完成させた
    /// <see cref="HumanoidMotionFrame"/> を <see cref="ISubject{T}"/> 経由で発行する薄いアダプタ層
    /// (design.md §6.1 / §9.2 / §9.3, requirements.md 要件 2-1, 2-2, 2-5, 2-6, 3-4, 3-5, 7-2)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>責務境界</b>: 本クラスは uOSC 受信ライフサイクル (Initialize / Shutdown) と
    /// 受信コールバックを <see cref="VmcMessageRouter"/> / <see cref="VmcFrameBuilder"/> に
    /// 委譲することのみを担う。OSC パース・アドレスディスパッチ・フレーム組み立ては
    /// それぞれ uOSC / <see cref="VmcMessageRouter"/> / <see cref="VmcFrameBuilder"/> の責務である。
    /// </para>
    /// <para>
    /// <b>スレッド前提</b>: <see cref="uOscServer"/> は <c>Update()</c> 内で受信キューを
    /// ディスパッチするためコールバックはメインスレッド上で呼ばれるが、設計上は
    /// 「uOSC 受信コールバック」として扱い、<see cref="ISubject{T}"/> 側の
    /// <c>Subject.Synchronize()</c> (呼び出し側で適用) によりマルチスレッド耐性を担保する。
    /// </para>
    /// <para>
    /// <b>bindAddress の扱い</b>: <c>com.hidano.uosc 1.0.0</c> の <see cref="uOscServer"/> は
    /// 内部で <c>IPAddress.IPv6Any</c> (IPv6Only = 0, ReuseAddress = 1) に固定バインドするため、
    /// VMCMoCapSourceConfig で指定される <c>bindAddress</c> 値は本アダプタでは未使用となる。
    /// この挙動は design.md §5.1 (L-2 対応) で合意済みの「全インターフェース受信」方針と整合する。
    /// </para>
    /// </remarks>
    internal sealed class VmcOscAdapter
    {
        private readonly VmcMessageRouter _router;
        private readonly VmcFrameBuilder _frameBuilder;
        private readonly ISubject<MotionFrame> _subject;
        private readonly Action<SlotErrorCategory, Exception> _errorHandler;

        private GameObject _serverObject;
        private uOscServer _server;
        private VmcTickDriver _tickDriver;   // (M-3 改訂) LateUpdate で Tick を呼び出す駆動 MonoBehaviour
        private UnityAction<Message> _listener;

        /// <summary>
        /// <see cref="VmcOscAdapter"/> を生成する。
        /// </summary>
        /// <param name="router">受信アドレス振り分け用 <see cref="VmcMessageRouter"/>。</param>
        /// <param name="frameBuilder">受信ボーンを <see cref="HumanoidMotionFrame"/> へ集約する <see cref="VmcFrameBuilder"/>。</param>
        /// <param name="subject">完成したフレームを発行する UniRx Subject (<c>Synchronize()</c> 済みを推奨)。</param>
        /// <param name="errorHandler">
        /// 受信コールバック中に発生した例外を <see cref="SlotErrorCategory.VmcReceive"/> として通知するハンドラ
        /// (<c>VmcMoCapSource.PublishError</c> 相当)。
        /// </param>
        public VmcOscAdapter(
            VmcMessageRouter router,
            VmcFrameBuilder frameBuilder,
            ISubject<MotionFrame> subject,
            Action<SlotErrorCategory, Exception> errorHandler)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _frameBuilder = frameBuilder ?? throw new ArgumentNullException(nameof(frameBuilder));
            _subject = subject ?? throw new ArgumentNullException(nameof(subject));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        /// <summary>
        /// uOSC 受信オブジェクト (<see cref="uOscServer"/>) を指定ポートで開始し、
        /// 受信コールバックを本アダプタへ紐付ける (design.md §9.2 ステップ 4)。
        /// </summary>
        /// <param name="bindAddress">
        /// 受信バインドアドレス。<c>com.hidano.uosc 1.0.0</c> 仕様により現状は未使用
        /// (<c>IPAddress.IPv6Any</c> 固定)。将来 uOSC が bindAddress 指定を受け付けた場合に備えて
        /// シグネチャを保持する。
        /// </param>
        /// <param name="port">UDP 受信ポート。</param>
        /// <exception cref="InvalidOperationException">既に <see cref="Initialize"/> 済みの場合。</exception>
        /// <exception cref="SocketException">
        /// 指定ポートへのバインドに失敗した場合 (ポート競合等)。
        /// <c>com.hidano.uosc</c> 内部では例外を握り潰すため、<see cref="uOscServer.isRunning"/> を
        /// 明示チェックし、bind 失敗時に本メソッドから送出する。
        /// </exception>
        public void Initialize(string bindAddress, int port)
        {
            if (_server != null)
            {
                throw new InvalidOperationException("VmcOscAdapter は既に初期化されています。Shutdown() 後に再初期化してください。");
            }

            _serverObject = new GameObject("VmcOscAdapter.uOscServer");
            // Hierarchy から確認できるように Hide は外して DontSave のみ保持する。
            // Scene には保存されないが、デバッグ時に uOscServer の Status / Messages を Inspector で確認できる。
            _serverObject.hideFlags = HideFlags.DontSave;
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_serverObject);
            }

            _server = _serverObject.AddComponent<uOscServer>();
            _server.port = port;
            _server.autoStart = false;

            _listener = OnDataReceived;
            _server.onDataReceived.AddListener(_listener);

            // (M-3 改訂) LateUpdate で Tick を駆動する MonoBehaviour を同 GameObject に追加
            _tickDriver = _serverObject.AddComponent<VmcTickDriver>();
            _tickDriver.Configure(Tick);

            _server.StartServer();

            if (!_server.isRunning)
            {
                Shutdown();
                throw new SocketException((int)SocketError.AddressAlreadyInUse);
            }
        }

        /// <summary>
        /// uOSC 受信を停止しコールバックを解除する (design.md §9.3)。
        /// </summary>
        /// <remarks>
        /// 二重呼び出しは無視する (冪等)。既に <c>Shutdown</c> 済みの場合でも例外を投げない。
        /// </remarks>
        public void Shutdown()
        {
            if (_server != null)
            {
                if (_listener != null)
                {
                    _server.onDataReceived.RemoveListener(_listener);
                }

                try
                {
                    _server.StopServer();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VmcOscAdapter] uOscServer.StopServer 中に例外が発生しました: {ex}");
                }

                _server = null;
                _listener = null;
            }

            // _tickDriver は _serverObject 配下に AddComponent されているため、
            // 以下の Destroy(_serverObject) で一緒に破棄される。参照だけクリア。
            _tickDriver = null;

            if (_serverObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_serverObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_serverObject);
                }
                _serverObject = null;
            }
        }

        /// <summary>
        /// <see cref="uOscServer.onDataReceived"/> から呼ばれる受信コールバック本体
        /// (design.md §6.1 改訂版)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// (M-3 改訂 2026-04-22) <see cref="VmcMessageRouter.Route"/> で <see cref="VmcFrameBuilder"/> に
        /// 受信値をキャッシュするのみ。<see cref="VmcFrameBuilder.TryFlush"/> / <see cref="ISubject{T}.OnNext"/> は
        /// ここでは呼ばない。VMC Protocol 仕様上 OSC stream から frame 境界は検知不能なため、
        /// flush は <see cref="Tick"/> (<c>VmcTickDriver.LateUpdate</c> 経由) に分離する。
        /// </para>
        /// <para>
        /// uOSC の実装により本メソッドは Unity MainThread で Invoke される
        /// (<c>uOscServer.Update</c> が <c>parser_</c> の Queue から dequeue して <c>onDataReceived.Invoke</c> するため)。
        /// </para>
        /// <para>
        /// 例外は全捕捉し <see cref="_errorHandler"/> に <see cref="SlotErrorCategory.VmcReceive"/> として委譲する。
        /// </para>
        /// </remarks>
        private void OnDataReceived(Message message)
        {
            try
            {
                _router.Route(message.address, message);
                // M-3 改訂: TryFlush / OnNext はここで呼ばない。Tick() で bundle 境界に依存しない flush を行う
            }
            catch (Exception ex)
            {
                try
                {
                    _errorHandler(SlotErrorCategory.VmcReceive, ex);
                }
                catch (Exception handlerEx)
                {
                    Debug.LogError($"[VmcOscAdapter] errorHandler 内で例外が発生しました: {handlerEx}");
                }
            }
        }

        /// <summary>
        /// MainThread (Unity LateUpdate) から定期的に呼ばれ、前回 Tick 以降に更新があれば
        /// <see cref="VmcFrameBuilder.TryFlush"/> で <see cref="HumanoidMotionFrame"/> を組み立てて
        /// <see cref="ISubject{T}.OnNext"/> で発行する (M-3 改訂 2026-04-22)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="VmcTickDriver"/> MonoBehaviour の <c>LateUpdate</c> から呼ばれる。
        /// MainThread 前提のため lock は不要。
        /// </para>
        /// <para>
        /// 例外は全捕捉し <see cref="_errorHandler"/> に <see cref="SlotErrorCategory.VmcReceive"/> として委譲する。
        /// </para>
        /// </remarks>
        internal void Tick()
        {
            try
            {
                if (_frameBuilder.TryFlush(out var frame))
                {
                    _subject.OnNext(frame);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _errorHandler(SlotErrorCategory.VmcReceive, ex);
                }
                catch (Exception handlerEx)
                {
                    Debug.LogError($"[VmcOscAdapter] Tick errorHandler 内で例外が発生しました: {handlerEx}");
                }
            }
        }
    }
}
