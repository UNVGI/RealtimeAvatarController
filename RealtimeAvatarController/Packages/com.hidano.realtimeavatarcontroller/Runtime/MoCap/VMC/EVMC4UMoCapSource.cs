using System;
using RealtimeAvatarController.Core;
using UniRx;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// EVMC4U <see cref="EVMC4U.ExternalReceiver"/> を LateUpdate で snapshot し
    /// <see cref="RealtimeAvatarController.Motion.HumanoidMotionFrame"/> を発行する
    /// <see cref="IMoCapSource"/> Adapter 実装 (design.md §4.2, §5.1)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ライフサイクル (要件 7.5)</b>:
    /// <c>Uninitialized</c> ──<see cref="Initialize"/>──▶ <c>Running</c>
    ///   ──<see cref="Shutdown"/> / <see cref="Dispose"/>──▶ <c>Disposed</c>。
    /// </para>
    /// <para>
    /// <b>Tick 駆動</b>: <see cref="EVMC4USharedReceiver"/> の LateUpdate から
    /// <see cref="IEVMC4UMoCapAdapter.Tick"/> が呼ばれる。内部 Dictionary を snapshot して
    /// <see cref="MotionStream"/> に <see cref="IObserver{T}.OnNext"/> 発行する。
    /// </para>
    /// <para>
    /// 本ファイルは Phase 4 のサブタスクを段階的に積み上げる:
    /// 4.3 でクラス骨格と状態機械、4.4 で Subject + Publish/RefCount、
    /// 4.5 で <see cref="Initialize"/>、4.6 で <see cref="IEVMC4UMoCapAdapter.Tick"/>、
    /// 4.7 で <see cref="Shutdown"/> / <see cref="Dispose"/>、4.8 で PublishError を実装する。
    /// </para>
    /// </remarks>
    public sealed class EVMC4UMoCapSource : IMoCapSource, IDisposable, IEVMC4UMoCapAdapter
    {
        /// <summary>
        /// Adapter の内部ライフサイクル状態 (要件 7.5 / design.md §5.1)。
        /// </summary>
        internal enum State
        {
            /// <summary>生成直後。Tick 駆動・Subscribe 未実施。</summary>
            Uninitialized,

            /// <summary><see cref="Initialize"/> 完了後。LateUpdate Tick が有効。</summary>
            Running,

            /// <summary><see cref="Shutdown"/> / <see cref="Dispose"/> 完了後。再使用不可。</summary>
            Disposed,
        }

        private readonly string _slotId;
        private readonly ISlotErrorChannel _errorChannel;

        /// <summary>
        /// Tick から <c>OnNext</c> が呼ばれる素の <see cref="Subject{T}"/>。
        /// <see cref="Shutdown"/> 時に <c>OnCompleted</c> / <c>Dispose</c> の終端操作対象となる (task 4.4 / 4.7)。
        /// </summary>
        private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();

        /// <summary>
        /// <see cref="_rawSubject"/> に <c>Subject.Synchronize()</c> を適用したスレッドセーフな発行口。
        /// MainThread LateUpdate 起点でも将来のワーカー起点 emit でも直列化を保証する (task 4.4)。
        /// </summary>
        private readonly ISubject<MotionFrame> _subject;

        /// <summary>
        /// <see cref="_subject"/> を <c>Publish().RefCount()</c> でマルチキャスト化した Hot Observable
        /// (要件 4.7, task 4.4)。複数購読者が同一ストリームを共有し、購読者ゼロで接続解除される。
        /// </summary>
        private readonly IObservable<MotionFrame> _stream;

        private State _state = State.Uninitialized;

        /// <inheritdoc />
        public string SourceType => "VMC";

        /// <summary>現在の内部ライフサイクル状態 (テスト・診断用)。</summary>
        internal State CurrentState => _state;

        /// <summary>
        /// <see cref="VMCMoCapSourceFactory"/> 経由で生成される Factory 専用コンストラクタ。
        /// </summary>
        /// <param name="slotId">発行する <see cref="SlotError"/> に付与する Slot 識別子。</param>
        /// <param name="errorChannel">エラー発行先の <see cref="ISlotErrorChannel"/>。</param>
        internal EVMC4UMoCapSource(string slotId, ISlotErrorChannel errorChannel)
        {
            _slotId = slotId ?? string.Empty;
            _errorChannel = errorChannel ?? throw new ArgumentNullException(nameof(errorChannel));

            _subject = _rawSubject.Synchronize();
            _stream = _subject.Publish().RefCount();
        }

        /// <inheritdoc />
        /// <remarks>
        /// 4.3 時点では最小限の状態遷移のみ行う (Uninitialized → Running)。
        /// 型キャスト・port 範囲検証・SharedReceiver 組み立ては task 4.5 で追加される。
        /// </remarks>
        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"EVMC4UMoCapSource.Initialize は Uninitialized 状態でのみ呼び出せます (現在の状態: {_state})。");
            }

            _state = State.Running;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="_rawSubject"/> を <c>Subject.Synchronize()</c>.<c>Publish()</c>.<c>RefCount()</c>
        /// でマルチキャスト化した Hot Observable を返す (要件 1.4, 4.7 / design.md §4.2)。
        /// <see cref="Initialize"/> 完了前でも購読は許容され、<see cref="State.Running"/> 到達後に OnNext が流れる
        /// (要件 1.9)。
        /// </remarks>
        public IObservable<MotionFrame> MotionStream => _stream;

        /// <inheritdoc />
        /// <remarks>
        /// 4.3 時点では最小限の状態遷移のみ行う。<see cref="_rawSubject"/> の <c>OnCompleted</c> /
        /// SharedReceiver からの Unsubscribe / Release は task 4.7 で追加される。
        /// </remarks>
        public void Shutdown()
        {
            if (_state == State.Disposed)
            {
                return;
            }

            _state = State.Disposed;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Shutdown();
        }

        // --- IEVMC4UMoCapAdapter (task 4.6 / 4.8 で実装する) ---

        void IEVMC4UMoCapAdapter.Tick()
        {
            // task 4.6 で実装する。
        }

        void IEVMC4UMoCapAdapter.HandleTickException(Exception exception)
        {
            // task 4.8 で実装する。
        }
    }
}
