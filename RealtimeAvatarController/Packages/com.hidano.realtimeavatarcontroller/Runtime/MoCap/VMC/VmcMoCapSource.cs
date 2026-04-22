using System;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.MoCap.VMC.Internal;
using UniRx;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// VMC プロトコル (OSC 受信) の <see cref="IMoCapSource"/> 具象実装
    /// (design.md §5.2 / §9, requirements.md 要件 1-1, 1-2, 4-5)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>骨格実装 (タスク 7-1)</b>: 内部状態管理 (<see cref="State"/>) と
    /// <see cref="SourceType"/>、コンストラクタ、<see cref="IDisposable"/> 骨格を提供する。
    /// </para>
    /// <para>
    /// <b>UniRx Subject とマルチキャストストリーム (タスク 7-2)</b>:
    /// <see cref="_rawSubject"/> に対し <c>Subject.Synchronize()</c> でスレッドセーフ化したものを
    /// <see cref="_subject"/> として保持し、さらに <c>Publish().RefCount()</c> でマルチキャスト化した
    /// <see cref="_stream"/> を <see cref="MotionStream"/> として公開する (design.md §6.5)。
    /// </para>
    /// <para>
    /// <b>Initialize (タスク 7-3)</b>: config の型・ポート範囲・状態遷移をバリデートし、
    /// <see cref="VmcFrameBuilder"/> / <see cref="VmcMessageRouter"/> / <see cref="VmcOscAdapter"/>
    /// を組み立てて uOSC 受信を開始する (design.md §9.2)。
    /// </para>
    /// <para>
    /// <b>Shutdown / Dispose (タスク 7-4)</b>: <see cref="VmcOscAdapter.Shutdown"/> でソケット閉鎖・
    /// 受信停止を行い、<see cref="_rawSubject"/> に対して <c>OnCompleted()</c> / <c>Dispose()</c> を
    /// 呼び出すことで購読者を終端させる (design.md §9.3)。冪等に実装される。
    /// </para>
    /// <para>
    /// <b>PublishError (タスク 7-5)</b>: <see cref="PublishError"/> ヘルパーで
    /// <see cref="ISlotErrorChannel.Publish"/> に <see cref="SlotError"/> を発行する
    /// (design.md §8.2)。<c>Debug.LogError</c> の抑制は <c>DefaultSlotErrorChannel</c> 側が担うため
    /// 本クラスでは持たない。<see cref="MotionStream"/> の <c>OnError()</c> は一切発行しない
    /// (design.md §8.3)。
    /// </para>
    /// <para>
    /// <b>ライフサイクル (design.md §9.1)</b>:
    /// <c>Uninitialized</c> ──<see cref="Initialize"/>──▶ <c>Running</c>
    ///   ──<see cref="Shutdown"/> / <see cref="Dispose"/>──▶ <c>Disposed</c>。
    /// Slot 側から直接 <see cref="Dispose"/> を呼び出してはならない
    /// (<c>MoCapSourceRegistry.Release</c> が参照カウント 0 で呼び出す)。
    /// </para>
    /// </remarks>
    public sealed class VmcMoCapSource : IMoCapSource
    {
        /// <summary>
        /// <see cref="VmcMoCapSource"/> の内部ライフサイクル状態 (design.md §9.1)。
        /// </summary>
        internal enum State
        {
            /// <summary>生成直後。ソケット・スレッドなし。</summary>
            Uninitialized,

            /// <summary><see cref="Initialize"/> 完了後。受信ループ稼働中。</summary>
            Running,

            /// <summary><see cref="Shutdown"/> / <see cref="Dispose"/> 完了後。再使用不可。</summary>
            Disposed,
        }

        private readonly string _slotId;
        private readonly ISlotErrorChannel _errorChannel;

        /// <summary>
        /// 受信スレッドから <c>OnNext</c> が呼ばれる素の <see cref="Subject{T}"/>。
        /// <see cref="Shutdown"/> / <see cref="Dispose"/> 時に <c>OnCompleted</c> および
        /// <c>Dispose</c> を呼ぶ終端操作の対象となる (design.md §6.5 / §9.3)。
        /// </summary>
        private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();

        /// <summary>
        /// <see cref="_rawSubject"/> に <c>Subject.Synchronize()</c> を適用したスレッドセーフな
        /// 発行口。ワーカースレッドからの同時 <c>OnNext</c> をロックで直列化する (design.md §6.5)。
        /// </summary>
        private readonly ISubject<MotionFrame> _subject;

        /// <summary>
        /// <see cref="_subject"/> を <c>Publish().RefCount()</c> でマルチキャスト化した
        /// ストリーム。複数購読者が同一ストリームを共有でき、購読者ゼロ時に接続が解除される
        /// Hot Observable として機能する (design.md §6.5)。
        /// </summary>
        private readonly IObservable<MotionFrame> _stream;

        /// <summary>
        /// <see cref="Initialize"/> で組み立てられる VMC 受信パイプライン構成要素。
        /// <see cref="Shutdown"/> までは生存し、再使用 (再 <see cref="Initialize"/>) は許容しない。
        /// </summary>
        private VmcFrameBuilder _frameBuilder;
        private VmcMessageRouter _router;
        private VmcOscAdapter _adapter;

        private State _state = State.Uninitialized;

        /// <summary>ソース種別識別子。常に <c>"VMC"</c> を返す (requirements.md 要件 1-2)。</summary>
        public string SourceType => "VMC";

        /// <summary>
        /// 現在の内部ライフサイクル状態 (テスト・診断用の internal プロパティ)。
        /// </summary>
        internal State CurrentState => _state;

        /// <summary>
        /// <see cref="VMCMoCapSourceFactory"/> 経由で生成される Factory 専用コンストラクタ。
        /// </summary>
        /// <param name="slotId">
        /// 発行する <see cref="SlotError"/> に付与する Slot 識別子。
        /// <see cref="VMCMoCapSourceFactory"/> は生成時点では <see cref="string.Empty"/> を渡し、
        /// <c>MoCapSourceRegistry</c> が後から設定する (design.md §10.1)。
        /// </param>
        /// <param name="errorChannel">
        /// エラー発行先の <see cref="ISlotErrorChannel"/>。
        /// <c>RegistryLocator.ErrorChannel</c> 相当のインスタンスを受け取る。
        /// </param>
        internal VmcMoCapSource(string slotId, ISlotErrorChannel errorChannel)
        {
            _slotId = slotId ?? string.Empty;
            _errorChannel = errorChannel ?? throw new ArgumentNullException(nameof(errorChannel));

            _subject = _rawSubject.Synchronize();
            _stream = _subject.Publish().RefCount();
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// 処理フロー (design.md §9.2):
        /// <list type="number">
        ///   <item>状態が <see cref="State.Uninitialized"/> 以外なら <see cref="InvalidOperationException"/></item>
        ///   <item><paramref name="config"/> を <see cref="VMCMoCapSourceConfig"/> にキャストできなければ
        ///         <see cref="ArgumentException"/> (メッセージに実型名を含める)</item>
        ///   <item>ポート番号が 1025〜65535 の範囲外なら <see cref="ArgumentOutOfRangeException"/></item>
        ///   <item><see cref="VmcFrameBuilder"/> / <see cref="VmcMessageRouter"/> /
        ///         <see cref="VmcOscAdapter"/> を組み立て、<c>bindAddress</c> / <c>port</c> で uOSC 受信を開始</item>
        ///   <item>内部状態を <see cref="State.Running"/> に遷移</item>
        /// </list>
        /// </para>
        /// <para>
        /// ポート競合 (<see cref="System.Net.Sockets.SocketException"/>) は呼び出し元
        /// (<c>SlotManager</c>) に伝播する (<c>InitFailure</c> カテゴリで通知される)。
        /// </para>
        /// </remarks>
        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"VmcMoCapSource.Initialize は Uninitialized 状態でのみ呼び出せます (現在の状態: {_state})。");
            }

            var vmcConfig = config as VMCMoCapSourceConfig;
            if (vmcConfig == null)
            {
                var actualTypeName = config?.GetType().Name ?? "null";
                throw new ArgumentException(
                    $"VMCMoCapSourceConfig が必要ですが {actualTypeName} が渡されました。",
                    nameof(config));
            }

            if (vmcConfig.port < 1025 || vmcConfig.port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(config),
                    vmcConfig.port,
                    "VMCMoCapSourceConfig.port は 1025〜65535 の範囲で指定してください。");
            }

            _frameBuilder = new VmcFrameBuilder();
            _router = new VmcMessageRouter(
                _frameBuilder,
                onError: ex => PublishError(SlotErrorCategory.VmcReceive, ex));
            _adapter = new VmcOscAdapter(
                _router,
                _frameBuilder,
                _subject,
                errorHandler: PublishError);

            _adapter.Initialize(vmcConfig.bindAddress, vmcConfig.port);

            _state = State.Running;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="_subject"/> (<c>Subject.Synchronize()</c> 済み) を
        /// <c>Publish().RefCount()</c> でマルチキャスト化したストリームを返す (design.md §6.5)。
        /// 購読側は <c>.ObserveOnMainThread()</c> でメインスレッドに同期すること。
        /// </remarks>
        public IObservable<MotionFrame> MotionStream => _stream;

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// 処理フロー (design.md §9.3 / タスク 7-4):
        /// <list type="number">
        ///   <item>状態が <see cref="State.Disposed"/> ならば即時 return (冪等)</item>
        ///   <item><see cref="VmcOscAdapter.Shutdown"/> でソケット閉鎖・受信コールバック解除</item>
        ///   <item><see cref="_rawSubject"/>.OnCompleted() で購読者へ終端通知</item>
        ///   <item><see cref="_rawSubject"/>.Dispose() でリソース解放</item>
        ///   <item>内部状態を <see cref="State.Disposed"/> に遷移</item>
        /// </list>
        /// </para>
        /// <para>
        /// <see cref="State.Uninitialized"/> 状態での呼び出しも冪等に扱い、
        /// <see cref="_adapter"/> が未構築であれば skip する。
        /// </para>
        /// </remarks>
        public void Shutdown()
        {
            if (_state == State.Disposed)
            {
                return;
            }

            if (_adapter != null)
            {
                _adapter.Shutdown();
            }

            _rawSubject.OnCompleted();
            _rawSubject.Dispose();

            _state = State.Disposed;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="Shutdown"/> と等価 (design.md §9.3 / タスク 7-4)。
        /// <c>IDisposable</c> 経由での解放と <see cref="IMoCapSource.Shutdown"/> を同一処理に集約する。
        /// </remarks>
        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// <see cref="ISlotErrorChannel"/> へ <see cref="SlotError"/> を発行する内部ヘルパー
        /// (design.md §8.2 / タスク 7-5)。
        /// </summary>
        /// <param name="category">発行するエラーのカテゴリ。</param>
        /// <param name="ex">発行する例外。</param>
        /// <remarks>
        /// <para>
        /// <c>Debug.LogError</c> の抑制制御は <c>DefaultSlotErrorChannel</c> 側 (同一 Slot/Category に
        /// 対する 1 フレーム 1 回のログ抑制) が担うため、本メソッドは単に
        /// <see cref="ISlotErrorChannel.Publish"/> に委譲する (design.md §8.2 の注記)。
        /// </para>
        /// <para>
        /// <see cref="MotionStream"/> の <c>OnError()</c> は本メソッドからも呼び出し元からも
        /// 一切発行しない (design.md §8.3)。ワーカー側の例外はすべてエラーチャネル経由で通知し、
        /// ストリームはライフサイクル完了時の <c>OnCompleted()</c> でのみ終端する。
        /// </para>
        /// </remarks>
        private void PublishError(SlotErrorCategory category, Exception ex)
        {
            _errorChannel.Publish(new SlotError(_slotId, category, ex, DateTime.UtcNow));
        }
    }
}
