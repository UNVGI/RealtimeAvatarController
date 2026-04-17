using System;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// VMC プロトコル (OSC 受信) の <see cref="IMoCapSource"/> 具象実装
    /// (design.md §5.2 / §9, requirements.md 要件 1-1, 1-2, 4-5)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>骨格実装 (タスク 7-1)</b>: 本コミットでは内部状態管理 (<see cref="State"/>) と
    /// <see cref="SourceType"/>、コンストラクタ、<see cref="IDisposable"/> 実装のみを提供する。
    /// UniRx Subject (タスク 7-2)・<see cref="Initialize"/> (タスク 7-3)・
    /// <see cref="Shutdown"/> / <see cref="Dispose"/> (タスク 7-4)・
    /// <c>PublishError</c> (タスク 7-5) は後続タスクで実装する。
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
        }

        /// <inheritdoc />
        /// <remarks>タスク 7-3 で実装する。</remarks>
        public void Initialize(MoCapSourceConfigBase config)
        {
            throw new NotImplementedException("VmcMoCapSource.Initialize はタスク 7-3 で実装される予定です。");
        }

        /// <inheritdoc />
        /// <remarks>タスク 7-2 で UniRx Subject を用いて実装する。</remarks>
        public IObservable<MotionFrame> MotionStream
            => throw new NotImplementedException("VmcMoCapSource.MotionStream はタスク 7-2 で実装される予定です。");

        /// <inheritdoc />
        /// <remarks>タスク 7-4 で実装する。</remarks>
        public void Shutdown()
        {
            throw new NotImplementedException("VmcMoCapSource.Shutdown はタスク 7-4 で実装される予定です。");
        }

        /// <inheritdoc />
        /// <remarks><see cref="Shutdown"/> と等価。タスク 7-4 で実装する。</remarks>
        public void Dispose()
        {
            throw new NotImplementedException("VmcMoCapSource.Dispose はタスク 7-4 で実装される予定です。");
        }
    }
}
