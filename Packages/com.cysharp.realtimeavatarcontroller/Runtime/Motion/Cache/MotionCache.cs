using System;
using System.Threading;
using RealtimeAvatarController.Core;
using UniRx;

namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// Slot 単位の最新モーションフレームキャッシュ。
    /// <see cref="IMoCapSource.MotionStream"/> を購読し、受信ワーカースレッドで最新フレームをアトミックに書き込む。
    /// Unity メインスレッドからの読み出し (<see cref="LatestFrame"/>) は <see cref="Volatile.Read{T}"/> によりロックフリー。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>スレッドモデル (design.md §5.1 — 方式 B 採用)</b>:
    /// 受信スレッドで <see cref="Interlocked.Exchange{T}"/> によりアトミックに参照を更新し、
    /// メインスレッドで <see cref="Volatile.Read{T}"/> により読み出す。
    /// <c>ObserveOnMainThread()</c> は使用しない (高頻度フレームでの UniRx キュー蓄積を回避するため)。
    /// </para>
    /// <para>
    /// <b>ライフサイクル</b>:
    /// <see cref="IMoCapSource"/> 本体の <see cref="IDisposable.Dispose"/> は<b>絶対に呼び出さない</b>。
    /// <see cref="MotionCache"/> はソースの所有権を持たず、購読の <see cref="IDisposable.Dispose"/> のみを管理する。
    /// ソース本体のライフサイクルは <c>MoCapSourceRegistry</c> (slot-core) が参照カウントで管理する。
    /// </para>
    /// <para>
    /// <b>OnError コールバック省略</b>:
    /// contracts.md §2.1 / slot-core design.md §3.1 により <see cref="IMoCapSource.MotionStream"/> は
    /// <c>OnError</c> を発行しない契約のため、<c>Subscribe</c> の <c>onError</c> コールバックは指定しない。
    /// </para>
    /// </remarks>
    public sealed class MotionCache : IDisposable
    {
        private volatile MotionFrame _latestFrame;
        private IDisposable _subscription;

        /// <summary>
        /// コンストラクタ。生成直後は購読を開始しない。
        /// 購読は <see cref="SetSource"/> 呼び出しで開始する。
        /// </summary>
        public MotionCache()
        {
        }

        /// <summary>
        /// 購読する MoCap ソースを設定 / 切り替える。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>スレッド要件</b>: Unity メインスレッドからのみ呼び出すこと。
        /// </para>
        /// <para>
        /// 旧購読を <see cref="IDisposable.Dispose"/> で解除してから新ソースを購読する。
        /// <paramref name="source"/> に <c>null</c> を渡した場合は購読のみ解除し、
        /// <see cref="LatestFrame"/> は直前のフレームを保持する (前フレーム維持)。
        /// </para>
        /// <para>
        /// <b>IMoCapSource Dispose 禁止</b>: <see cref="IMoCapSource"/> 本体の
        /// <see cref="IDisposable.Dispose"/> は呼び出さない (購読解除のみを行う)。
        /// </para>
        /// </remarks>
        /// <param name="source">購読対象の MoCap ソース。<c>null</c> で購読解除。</param>
        public void SetSource(IMoCapSource source)
        {
            _subscription?.Dispose();
            _subscription = null;

            if (source == null)
            {
                return;
            }

            _subscription = source.MotionStream.Subscribe(OnReceiveCoreFrame);
        }

        /// <summary>
        /// 最新のモーションフレームを返す。
        /// フレームが未到着の場合は <c>null</c> を返す。
        /// </summary>
        /// <remarks>
        /// メインスレッドから呼び出すことを前提とするが、
        /// <see cref="Volatile.Read{T}"/> による参照読み出しはスレッドセーフ。
        /// </remarks>
        public MotionFrame LatestFrame => Volatile.Read(ref _latestFrame);

        /// <summary>
        /// 購読を解除し内部リソースを解放する。
        /// </summary>
        /// <remarks>
        /// <see cref="IMoCapSource"/> 本体の <see cref="IDisposable.Dispose"/> は<b>呼び出さない</b>。
        /// 購読の <see cref="IDisposable.Dispose"/> のみを実行する。
        /// </remarks>
        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        // 受信ワーカースレッドから OnNext で呼ばれる。
        // contracts.md §2.2 統合後に Core.MotionFrame と Motion.MotionFrame が一致した時点では
        // _subscription = source.MotionStream.Subscribe(OnReceive) と直接渡す形に置換可能。
        private void OnReceiveCoreFrame(global::RealtimeAvatarController.Core.MotionFrame coreFrame)
        {
            if ((object)coreFrame is MotionFrame frame)
            {
                OnReceive(frame);
            }
        }

        private void OnReceive(MotionFrame frame)
        {
            Interlocked.Exchange(ref _latestFrame, frame);
        }
    }
}
