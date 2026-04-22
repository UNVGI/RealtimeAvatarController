using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using UniRx;
using UnityEngine;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

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

        /// <summary>
        /// <see cref="Initialize"/> で確保した共有 Receiver。<see cref="Shutdown"/> 時に
        /// <see cref="EVMC4USharedReceiver.Unsubscribe"/> / <see cref="EVMC4USharedReceiver.Release"/>
        /// する対象となる (task 4.5 / 4.7)。
        /// </summary>
        private EVMC4USharedReceiver _sharedReceiver;

        /// <summary>
        /// 前 Tick で emit したときの Bone Dictionary のハッシュ近似値 (task 4.6 / R-1)。
        /// 初期版は Count + 代表値の簡易ハッシュで dirty 判定する。
        /// 将来的には <see cref="EVMC4U.ExternalReceiver"/> 側に書込カウンタを追加する予定。
        /// </summary>
        private int _lastEmittedBoneCount = -1;
        private double _lastEmittedBoneHash = double.NaN;

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
        /// 処理フロー (design.md §4.2 / task 4.5):
        /// <list type="number">
        ///   <item>状態が <see cref="State.Uninitialized"/> 以外なら <see cref="InvalidOperationException"/> (要件 7.5)</item>
        ///   <item><paramref name="config"/> を <see cref="VMCMoCapSourceConfig"/> にキャスト。失敗時は
        ///         <see cref="ArgumentException"/> (メッセージに実型名を含める、要件 1.5)</item>
        ///   <item>ポート番号が 1025〜65535 の範囲外なら <see cref="ArgumentOutOfRangeException"/> (要件 5.3)</item>
        ///   <item><see cref="EVMC4USharedReceiver.EnsureInstance"/> で共有 Receiver を確保 (要件 1.6 / 2.1 / 2.2)</item>
        ///   <item><see cref="EVMC4USharedReceiver.ApplyReceiverSettings"/> で uOSC を該当 port で起動 (要件 1.7)。
        ///         <see cref="System.Net.Sockets.SocketException"/> は呼び出し元へ伝播 (要件 8.4)</item>
        ///   <item><see cref="EVMC4USharedReceiver.Subscribe"/> で LateUpdate Tick 対象に追加 (要件 4.3)</item>
        ///   <item>状態を <see cref="State.Running"/> に遷移</item>
        /// </list>
        /// </remarks>
        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"EVMC4UMoCapSource.Initialize は Uninitialized 状態でのみ呼び出せます (現在の状態: {_state})。");
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

            _sharedReceiver = EVMC4USharedReceiver.EnsureInstance();
            _sharedReceiver.ApplyReceiverSettings(vmcConfig.port);
            _sharedReceiver.Subscribe(this);

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

        // --- IEVMC4UMoCapAdapter (task 4.6 / 4.8) ---

        /// <summary>
        /// <see cref="EVMC4USharedReceiver"/> の LateUpdate から毎フレーム呼ばれる Tick 処理 (task 4.6 / design.md §5.1)。
        /// </summary>
        /// <remarks>
        /// 処理フロー:
        /// <list type="number">
        ///   <item><see cref="EVMC4U.ExternalReceiver.GetBoneRotationsView"/> で Bone 回転辞書ビューを取得</item>
        ///   <item>Count==0 なら早期 return (要件 3.5 空フレーム抑制)</item>
        ///   <item>Count + 代表値ハッシュで dirty 判定。前 Tick と同値なら emit スキップ (R-1)</item>
        ///   <item>新規 <see cref="Dictionary{TKey,TValue}"/> に値コピーして snapshot を取る (要件 3.6)</item>
        ///   <item><see cref="Stopwatch.GetTimestamp"/> ベースで Timestamp を打刻 (要件 3.3)</item>
        ///   <item>Root は <see cref="EVMC4U.ExternalReceiver.LatestRootLocalPosition"/> /
        ///         <see cref="EVMC4U.ExternalReceiver.LatestRootLocalRotation"/> から取得 (要件 3.4)</item>
        ///   <item><see cref="HumanoidMotionFrame"/> を構築 (Muscles は空配列、要件 3.2) し <see cref="_subject"/> に OnNext</item>
        /// </list>
        /// Tick 内例外は try/catch で捕捉して <see cref="PublishError"/> へ委譲する (要件 8.3)。
        /// <see cref="IObserver{T}.OnError"/> は絶対に呼ばない (要件 8.1)。
        /// BlendShape / 表情は参照しない (要件 3.7)。
        /// </remarks>
        void IEVMC4UMoCapAdapter.Tick()
        {
            if (_state != State.Running || _sharedReceiver == null)
            {
                return;
            }

            try
            {
                var receiver = _sharedReceiver.Receiver;
                if (receiver == null)
                {
                    return;
                }

                var view = receiver.GetBoneRotationsView();
                if (view == null || view.Count == 0)
                {
                    return;
                }

                if (!HasDirtyBoneData(view, out var currentHash))
                {
                    return;
                }

                var snapshot = new Dictionary<HumanBodyBones, Quaternion>(view.Count);
                foreach (var kv in view)
                {
                    snapshot[kv.Key] = kv.Value;
                }

                var timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                var rootPosition = receiver.LatestRootLocalPosition;
                var rootRotation = receiver.LatestRootLocalRotation;

                var frame = new HumanoidMotionFrame(
                    timestamp,
                    Array.Empty<float>(),
                    rootPosition,
                    rootRotation,
                    snapshot);

                _lastEmittedBoneCount = view.Count;
                _lastEmittedBoneHash = currentHash;

                _subject.OnNext(frame);
            }
            catch (Exception ex)
            {
                PublishError(SlotErrorCategory.VmcReceive, ex);
            }
        }

        void IEVMC4UMoCapAdapter.HandleTickException(Exception exception)
        {
            // task 4.8 で ErrorChannel 連携を行う。
        }

        /// <summary>
        /// <paramref name="view"/> の内容が前 Tick と比較して変化したかを判定する (R-1 / task 4.6)。
        /// Count + 代表値の簡易ハッシュ (x+y+z+w の総和) を用いる近似判定。
        /// </summary>
        /// <param name="view">Receiver から取得した Bone 回転辞書ビュー。</param>
        /// <param name="currentHash">計算された現在のハッシュ値 (出力)。</param>
        /// <returns>変化があれば <c>true</c>。変化がなければ <c>false</c>。</returns>
        private bool HasDirtyBoneData(
            IReadOnlyDictionary<HumanBodyBones, Quaternion> view,
            out double currentHash)
        {
            double hash = 0.0;
            foreach (var kv in view)
            {
                var q = kv.Value;
                hash += q.x + q.y + q.z + q.w;
            }
            currentHash = hash;

            if (view.Count != _lastEmittedBoneCount)
            {
                return true;
            }
            return currentHash != _lastEmittedBoneHash;
        }

        // --- エラー発行 (task 4.8 で詳細を詰める) ---

        private void PublishError(SlotErrorCategory category, Exception ex)
        {
            // task 4.8 で ErrorChannel への発行処理を追加する。
        }
    }
}
