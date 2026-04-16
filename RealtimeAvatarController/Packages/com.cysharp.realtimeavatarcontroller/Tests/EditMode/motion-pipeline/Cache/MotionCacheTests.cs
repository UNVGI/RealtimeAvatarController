using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using CoreMotionFrame = RealtimeAvatarController.Core.MotionFrame;
using MotionMotionFrame = RealtimeAvatarController.Motion.MotionFrame;

namespace RealtimeAvatarController.Motion.Tests.Cache
{
    /// <summary>
    /// MotionCache の EditMode 単体テスト。
    /// テスト観点 (design.md §3.8 / §5):
    ///   - SetSource() 前は LatestFrame == null
    ///   - Subject.OnNext(frame) 送信後、LatestFrame が更新される
    ///   - SetSource(newSource) 切替で旧 Subject の OnNext が反映されない (旧購読解除)
    ///   - SetSource(null) で購読解除されるが LatestFrame は保持される (前フレーム維持)
    ///   - Dispose() 後、Subject.OnNext が反映されない (購読解除)
    ///   - MotionCache.Dispose() / SetSource(null) で IMoCapSource.Dispose() を呼ばない
    ///     (ライフサイクル境界 — design.md §5.5)
    /// スタブ戦略: UniRx の <see cref="Subject{T}"/> を使った <see cref="IMoCapSource"/> スタブを実装する。
    /// Requirements: Req 4, Req 7, Req 8, Req 10, Req 14
    /// </summary>
    [TestFixture]
    public class MotionCacheTests
    {
        private const int HumanoidMuscleCount = 95;

        private static HumanoidMotionFrame CreateValidFrame(double timestamp = 1.0)
            => new HumanoidMotionFrame(
                timestamp,
                new float[HumanoidMuscleCount],
                Vector3.zero,
                Quaternion.identity);

        // --- LatestFrame_BeforeSetSource_IsNull ---

        [Test]
        public void LatestFrame_BeforeSetSource_IsNull()
        {
            using var cache = new MotionCache();

            Assert.That(cache.LatestFrame, Is.Null,
                "SetSource() 呼び出し前は LatestFrame は null でなければならない");
        }

        // --- LatestFrame_AfterOnNext_IsUpdated ---

        [Test]
        public void LatestFrame_AfterOnNext_IsUpdated()
        {
            using var cache = new MotionCache();
            var source = new StubMoCapSource();
            cache.SetSource(source);

            var frame = CreateValidFrame(timestamp: 1.0);
            source.Emit(frame);

            Assert.That(cache.LatestFrame, Is.SameAs(frame),
                "Subject.OnNext(frame) 送信後、LatestFrame は受信した frame を保持する");
        }

        // --- SetSource_SwitchesSource_UnsubscribesOld ---

        [Test]
        public void SetSource_SwitchesSource_UnsubscribesOld()
        {
            using var cache = new MotionCache();
            var oldSource = new StubMoCapSource();
            var newSource = new StubMoCapSource();

            cache.SetSource(oldSource);

            var initialFrame = CreateValidFrame(timestamp: 1.0);
            oldSource.Emit(initialFrame);
            Assert.That(cache.LatestFrame, Is.SameAs(initialFrame),
                "前提: 切替前は旧ソースからのフレームが反映されている");

            cache.SetSource(newSource);

            var orphanFrame = CreateValidFrame(timestamp: 2.0);
            oldSource.Emit(orphanFrame);

            Assert.That(cache.LatestFrame, Is.Not.SameAs(orphanFrame),
                "切替後の旧ソース OnNext は LatestFrame を更新してはならない (旧購読解除)");
            Assert.That(cache.LatestFrame, Is.SameAs(initialFrame),
                "旧購読解除後も切替直前のフレームが LatestFrame として保持される");
        }

        // --- SetSource_WithNull_UnsubscribesButKeepsLatestFrame ---

        [Test]
        public void SetSource_WithNull_UnsubscribesButKeepsLatestFrame()
        {
            using var cache = new MotionCache();
            var source = new StubMoCapSource();
            cache.SetSource(source);

            var frame = CreateValidFrame(timestamp: 1.0);
            source.Emit(frame);
            Assert.That(cache.LatestFrame, Is.SameAs(frame),
                "前提: SetSource(null) 前は LatestFrame に frame が保持されている");

            cache.SetSource(null);

            Assert.That(cache.LatestFrame, Is.SameAs(frame),
                "SetSource(null) は購読のみ解除し、直前の LatestFrame を保持する (design.md §5.4)");

            var orphanFrame = CreateValidFrame(timestamp: 2.0);
            source.Emit(orphanFrame);

            Assert.That(cache.LatestFrame, Is.SameAs(frame),
                "SetSource(null) 後の旧ソース OnNext は LatestFrame を更新しない");
        }

        // --- Dispose_UnsubscribesStream ---

        [Test]
        public void Dispose_UnsubscribesStream()
        {
            var cache = new MotionCache();
            var source = new StubMoCapSource();
            cache.SetSource(source);

            cache.Dispose();

            var orphanFrame = CreateValidFrame(timestamp: 1.0);
            source.Emit(orphanFrame);

            Assert.That(cache.LatestFrame, Is.Not.SameAs(orphanFrame),
                "Dispose() 後の OnNext は LatestFrame を更新してはならない (購読解除済み)");
        }

        // --- Dispose_DoesNotCallIMoCapSourceDispose ---

        [Test]
        public void Dispose_DoesNotCallIMoCapSourceDispose()
        {
            var cache = new MotionCache();
            var source = new StubMoCapSource();
            cache.SetSource(source);

            cache.Dispose();

            Assert.That(source.DisposeCallCount, Is.EqualTo(0),
                "MotionCache.Dispose() は IMoCapSource.Dispose() を呼び出してはならない (design.md §5.5)");
        }

        // --- SetSource_WithNull_DoesNotCallIMoCapSourceDispose ---

        [Test]
        public void SetSource_WithNull_DoesNotCallIMoCapSourceDispose()
        {
            using var cache = new MotionCache();
            var source = new StubMoCapSource();
            cache.SetSource(source);

            cache.SetSource(null);

            Assert.That(source.DisposeCallCount, Is.EqualTo(0),
                "SetSource(null) は購読解除のみ行い、IMoCapSource.Dispose() を呼び出してはならない");
        }

        [Test]
        public void SetSource_SwitchesSource_DoesNotCallOldIMoCapSourceDispose()
        {
            using var cache = new MotionCache();
            var oldSource = new StubMoCapSource();
            var newSource = new StubMoCapSource();
            cache.SetSource(oldSource);

            cache.SetSource(newSource);

            Assert.That(oldSource.DisposeCallCount, Is.EqualTo(0),
                "ソース切替時、旧 IMoCapSource.Dispose() は呼び出されない (MoCapSourceRegistry の責務)");
            Assert.That(newSource.DisposeCallCount, Is.EqualTo(0),
                "新 IMoCapSource.Dispose() も呼び出されない");
        }

        // --- スタブ実装 ---

        /// <summary>
        /// テスト用 <see cref="IMoCapSource"/> スタブ。
        /// 内部の <see cref="Subject{T}"/> に対して <see cref="Emit"/> でフレームを送信できる。
        /// <see cref="DisposeCallCount"/> で <see cref="IDisposable.Dispose"/> 呼び出し回数を検証する。
        /// </summary>
        /// <remarks>
        /// <see cref="MotionMotionFrame"/> インスタンスを <see cref="CoreMotionFrame"/> 型パラメータの
        /// Subject に流すため、<see cref="Unsafe.As{T}(object)"/> で参照型の再解釈を行う。
        /// 実行時オブジェクトの型は <see cref="MotionMotionFrame"/> のまま保たれるため、
        /// MotionCache 側で <c>is Motion.MotionFrame</c> 判定が成立する。
        /// contracts.md §2.2 の Core.MotionFrame プレースホルダー統合後は、
        /// この再解釈は不要となり純粋な参照代入に置換可能。
        /// </remarks>
        private sealed class StubMoCapSource : IMoCapSource
        {
            private readonly Subject<CoreMotionFrame> _subject = new Subject<CoreMotionFrame>();

            public string SourceType => "Stub";
            public IObservable<CoreMotionFrame> MotionStream => _subject;
            public int DisposeCallCount { get; private set; }

            public void Initialize(MoCapSourceConfigBase config) { }
            public void Shutdown() { }

            public void Dispose()
            {
                DisposeCallCount++;
            }

            public void Emit(MotionMotionFrame frame)
            {
                // 参照型の再解釈: ランタイムオブジェクトの実体は MotionMotionFrame のまま。
                // Subject<CoreMotionFrame> の静的型チェックのみを回避する。
                _subject.OnNext(Unsafe.As<CoreMotionFrame>(frame));
            }
        }
    }
}
