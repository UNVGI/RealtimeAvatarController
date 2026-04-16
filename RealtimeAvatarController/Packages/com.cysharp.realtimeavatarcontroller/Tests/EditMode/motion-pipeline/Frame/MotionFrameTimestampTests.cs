using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.Motion.Tests.Frame
{
    /// <summary>
    /// MotionFrame.Timestamp の EditMode 単体テスト。
    /// テスト観点:
    ///   - Stopwatch ベース値を渡した場合 Timestamp が正 (> 0)
    ///   - コンストラクタで渡した timestamp 値がプロパティから正しく読み取れる
    ///   - 連続生成した 2 フレームの Timestamp が単調増加している
    ///   - <c>Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency</c> 算出式で正値が得られる
    /// Requirements: Req 1, Req 2, Req 14
    /// </summary>
    [TestFixture]
    public class MotionFrameTimestampTests
    {
        private const int HumanoidMuscleCount = 95;

        private static HumanoidMotionFrame CreateFrame(double timestamp)
            => new HumanoidMotionFrame(
                timestamp,
                new float[HumanoidMuscleCount],
                Vector3.zero,
                Quaternion.identity);

        [Test]
        public void Timestamp_IsPositive()
        {
            double stopwatchSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

            var frame = CreateFrame(stopwatchSeconds);

            Assert.That(frame.Timestamp, Is.GreaterThan(0.0));
        }

        [Test]
        public void Timestamp_IsPreserved()
        {
            const double expected = 42.1234567;

            var frame = CreateFrame(expected);

            Assert.That(frame.Timestamp, Is.EqualTo(expected));
        }

        [Test]
        public void Timestamp_IsMonotonicallyIncreasing()
        {
            double t1 = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            var first = CreateFrame(t1);

            var spinWait = Stopwatch.StartNew();
            while (spinWait.ElapsedTicks == 0)
            {
                // busy-wait until Stopwatch tick advances to guarantee t2 > t1
            }

            double t2 = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            var second = CreateFrame(t2);

            Assert.That(second.Timestamp, Is.GreaterThan(first.Timestamp),
                "連続生成した 2 フレームの Timestamp は単調増加していなければならない");
        }

        [Test]
        public void Timestamp_IsCalculatedCorrectly()
        {
            long rawTicks = Stopwatch.GetTimestamp();
            double frequency = (double)Stopwatch.Frequency;
            double computed = rawTicks / frequency;

            var frame = CreateFrame(computed);

            Assert.That(frequency, Is.GreaterThan(0.0),
                "Stopwatch.Frequency は正値でなければならない");
            Assert.That(computed, Is.GreaterThan(0.0),
                "Stopwatch.GetTimestamp() / Stopwatch.Frequency は正値でなければならない");
            Assert.That(frame.Timestamp, Is.EqualTo(computed),
                "コンストラクタに渡した算出値が Timestamp にそのまま反映されること");
        }
    }
}
