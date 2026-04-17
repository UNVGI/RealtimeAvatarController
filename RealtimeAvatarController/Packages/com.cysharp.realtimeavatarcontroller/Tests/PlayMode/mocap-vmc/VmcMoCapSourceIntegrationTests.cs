using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;
using MotionFrame = RealtimeAvatarController.Motion.MotionFrame;
using HumanoidMotionFrame = RealtimeAvatarController.Motion.HumanoidMotionFrame;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <c>mocap-vmc</c> PlayMode 統合テスト
    /// (tasks.md タスク 11-2 / design.md §15.2 / requirements.md 要件 2-1, 2-2, 2-7, 5-1, 5-2, 7-1, 7-2, 10-3, 10-4, 10-5)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>検証範囲</b>: <see cref="UdpOscSenderTestDouble"/> から <see cref="IPAddress.Loopback"/>:<see cref="TestPort"/>
    /// 宛に送信した OSC パケットが <see cref="VMCMoCapSourceFactory"/> 経由で生成・初期化された
    /// <see cref="VmcMoCapSource"/> の <see cref="IMoCapSource.MotionStream"/> に到達するまでの End-to-End。
    /// uOSC (com.hidano.uosc) UDP 受信 → <c>VmcOscAdapter</c> → <c>VmcMessageRouter</c> →
    /// <c>VmcFrameBuilder</c> → <c>Subject&lt;MotionFrame&gt;</c> の経路全てを実ネットワークで通す。
    /// これにより tasks.md タスク 1-1 の Unity 6000.3 互換性チェックリスト
    /// 「uOSC 受信コールバックが呼ばれること」も満たす。
    /// </para>
    /// <para>
    /// <b>非同期受信の待機方針</b>: <c>uOscServer</c> はワーカースレッドで UDP を読み取り、
    /// <c>Update()</c> でメインスレッドにディスパッチするため、送信直後にフレーム到着を確認できない。
    /// 各テストは <see cref="WaitForFrames"/> ヘルパーで <see cref="Time.realtimeSinceStartup"/> を
    /// 用いた指数なし固定秒タイムアウトのフレームポーリングで待機する。
    /// （UniRx <c>Timeout()</c> を使わないのは PlayMode テスト asmdef の依存を最小化するため
    /// — tasks.md タスク 1-5 で <c>UniRx</c> 依存は必須とされていない。）
    /// </para>
    /// <para>
    /// <b>テスト独立性</b>: <c>[SetUp]</c>/<c>[TearDown]</c> で <see cref="RegistryLocator.ResetForTest"/> を
    /// 呼び出し、<see cref="VMCMoCapSourceFactory"/> の <c>[RuntimeInitializeOnLoadMethod]</c> 自己登録や
    /// 他テストからの登録状態汚染を排除する (要件 10-5)。固定ポート <see cref="TestPort"/> を使用するため、
    /// 各テストの <c>[TearDown]</c> で必ず <see cref="VmcMoCapSource.Shutdown"/> を呼びソケットを解放する。
    /// </para>
    /// </remarks>
    [TestFixture]
    public class VmcMoCapSourceIntegrationTests
    {
        /// <summary>
        /// テスト専用 UDP ポート (tasks.md タスク 11-2 規定値)。
        /// VMC 標準ポート 39539 と衝突しないよう 45678 を使用する。
        /// </summary>
        private const int TestPort = 45678;

        /// <summary>受信フレーム到着待ちの既定タイムアウト秒数。</summary>
        private const float DefaultTimeoutSeconds = 2.0f;

        private VMCMoCapSourceConfig _config;
        private IMoCapSource _source;
        private UdpOscSenderTestDouble _sender;
        private FrameRecorder _recorder;
        private IDisposable _subscription;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _config.port = TestPort;
            _config.bindAddress = "0.0.0.0";

            _source = new VMCMoCapSourceFactory().Create(_config);
            _source.Initialize(_config);

            _recorder = new FrameRecorder();
            _subscription = _source.MotionStream.Subscribe(_recorder);

            _sender = new UdpOscSenderTestDouble(TestPort);
        }

        [TearDown]
        public void TearDown()
        {
            _subscription?.Dispose();
            _subscription = null;

            _sender?.Dispose();
            _sender = null;

            _source?.Dispose();
            _source = null;

            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            RegistryLocator.ResetForTest();
        }

        // --- ケース 1: ローカル UDP 送信 → MotionStream 受信 (要件 2-1, 2-2, 10-3) ---

        /// <summary>
        /// <see cref="UdpOscSenderTestDouble.SendBonePos"/> が送信した
        /// <c>/VMC/Ext/Bone/Pos</c> パケットが <see cref="IMoCapSource.MotionStream"/> 経由で
        /// 観測可能となること。tasks.md タスク 1-1 の uOSC 受信コールバック呼び出し確認も兼ねる。
        /// </summary>
        [UnityTest]
        public IEnumerator MotionStream_ReceivesFrame_WhenBonePacketSent()
        {
            _sender.SendBonePos("Hips", new Vector3(1f, 2f, 3f), Quaternion.identity);

            yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

            Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                "Bone パケット送信後は MotionStream 経由で 1 件以上のフレームを受信するべき (要件 2-1, 2-2, 10-3)。");
            Assert.That(_recorder.Errors, Is.Empty,
                "正常パケット受信時に MotionStream の OnError は発行されないべき (要件 7-1)。");
            Assert.That(_recorder.Completed, Is.False,
                "Shutdown 前に MotionStream の OnCompleted が発行されてはならない (要件 1-4 反例)。");
        }

        // --- ケース 2: Root / Bone データの往復正確性 (要件 5-1, 5-2) ---

        /// <summary>
        /// 送信した <c>/VMC/Ext/Root/Pos</c> の位置・回転が
        /// <see cref="HumanoidMotionFrame.RootPosition"/> / <see cref="HumanoidMotionFrame.RootRotation"/>
        /// にそのまま転写されること、および <see cref="HumanoidMotionFrame.Muscles"/> 配列長が
        /// <see cref="HumanTrait.MuscleCount"/> (= 95) に固定されること (design.md §7.2 / §7.3)。
        /// </summary>
        [UnityTest]
        public IEnumerator MotionStream_DeliversRootAndBoneData_Roundtrip()
        {
            var rootPosition = new Vector3(0.5f, 1.5f, -0.25f);
            var rootRotation = Quaternion.Euler(10f, 20f, 30f);

            _sender.SendRootPos(rootPosition, rootRotation);
            yield return null;
            _sender.SendBonePos("Hips", new Vector3(0f, 1f, 0f), Quaternion.identity);

            yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

            Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                "Root + Bone パケット送信後はフレームが届くべき。");

            var frame = _recorder.Frames[0] as HumanoidMotionFrame;
            Assert.That(frame, Is.Not.Null,
                "MotionStream から流れるフレームは HumanoidMotionFrame であるべき (design.md §7)。");

            AssertVector3Approximately(rootPosition, frame.RootPosition, tolerance: 1e-3f, label: "RootPosition");
            AssertQuaternionApproximately(rootRotation, frame.RootRotation, tolerance: 1e-3f, label: "RootRotation");

            Assert.That(frame.Muscles, Is.Not.Null);
            Assert.That(frame.Muscles.Length, Is.EqualTo(HumanTrait.MuscleCount),
                "Muscles 配列長は HumanTrait.MuscleCount に固定されるべき (design.md §7.3)。");
        }

        // --- ケース 3: timestamp の単調増加 (要件 2-7) ---

        /// <summary>
        /// 連続して <c>/VMC/Ext/Bone/Pos</c> を送信した場合、受信フレームの
        /// <see cref="MotionFrame.Timestamp"/> が単調増加すること
        /// (Stopwatch ベース打刻 / design.md §6.4)。
        /// </summary>
        [UnityTest]
        public IEnumerator MotionStream_TimestampMonotonicallyIncreases()
        {
            _sender.SendBonePos("Hips", Vector3.zero, Quaternion.identity);
            yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

            yield return new WaitForSeconds(0.05f);
            _sender.SendBonePos("Spine", Vector3.zero, Quaternion.identity);
            yield return WaitForFrames(_recorder, expectedCount: 2, timeoutSeconds: DefaultTimeoutSeconds);

            yield return new WaitForSeconds(0.05f);
            _sender.SendBonePos("Chest", Vector3.zero, Quaternion.identity);
            yield return WaitForFrames(_recorder, expectedCount: 3, timeoutSeconds: DefaultTimeoutSeconds);

            Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(3),
                "3 回の Bone 送信に対して少なくとも 3 フレームが受信されるべき。");

            for (int i = 1; i < _recorder.Frames.Count; i++)
            {
                Assert.That(
                    _recorder.Frames[i].Timestamp,
                    Is.GreaterThan(_recorder.Frames[i - 1].Timestamp),
                    $"Frames[{i}].Timestamp は Frames[{i - 1}].Timestamp より大きくなるべき (要件 2-7)。");
            }
        }

        // --- ケース 4: パースエラー時にストリームが継続する (要件 7-1, 7-2) ---

        /// <summary>
        /// 不正バイト列を 1 度送信した後でも MotionStream は終端せず、
        /// 続く正常パケットを受信できること。<c>OnError</c> は発行されないこと
        /// (design.md §8.1 / §8.3)。
        /// </summary>
        [UnityTest]
        public IEnumerator MotionStream_ContinuesAfterParseError()
        {
            _sender.SendInvalidPacket();
            yield return new WaitForSeconds(0.1f);

            _sender.SendBonePos("Hips", Vector3.zero, Quaternion.identity);
            yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

            Assert.That(_recorder.Errors, Is.Empty,
                "OSC パースエラーが発生しても MotionStream の OnError は発行されないべき (要件 7-1, 7-2)。");
            Assert.That(_recorder.Completed, Is.False,
                "OSC パースエラーで MotionStream が終端してはならない (design.md §8.3)。");
            Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                "不正パケット受信後も次の正常パケットは MotionStream に届くべき。");
        }

        // --- ケース 5: Shutdown() 後に MotionStream が完了する (要件 1-4) ---

        /// <summary>
        /// <see cref="IMoCapSource.Shutdown"/> 呼び出しにより MotionStream の <c>OnCompleted</c> が
        /// 発行され、購読者が終端通知を受け取ること (design.md §9.3 ステップ 5)。
        /// </summary>
        [UnityTest]
        public IEnumerator MotionStream_CompletesAfterShutdown()
        {
            _sender.SendBonePos("Hips", Vector3.zero, Quaternion.identity);
            yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

            Assume.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                "前提: Shutdown 前に少なくとも 1 フレーム受信済みであること。");

            _source.Shutdown();
            yield return null;

            Assert.That(_recorder.Completed, Is.True,
                "Shutdown 後に MotionStream の OnCompleted が発行されるべき (要件 1-4 / design.md §9.3)。");
            Assert.That(_recorder.Errors, Is.Empty,
                "Shutdown 経路でも OnError は発行されないべき (design.md §8.3)。");
        }

        // --- ヘルパー ---

        /// <summary>
        /// <paramref name="recorder"/>.Frames が <paramref name="expectedCount"/> 件以上に達するまで
        /// PlayMode のフレームを進めながら待機する。<paramref name="timeoutSeconds"/> 秒経過しても
        /// 達しない場合は単に return し、呼び出し側の <c>Assert</c> に判定を委ねる。
        /// </summary>
        private static IEnumerator WaitForFrames(FrameRecorder recorder, int expectedCount, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (recorder.Frames.Count < expectedCount && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static void AssertVector3Approximately(Vector3 expected, Vector3 actual, float tolerance, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{label}.y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), $"{label}.z");
        }

        private static void AssertQuaternionApproximately(Quaternion expected, Quaternion actual, float tolerance, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{label}.y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), $"{label}.z");
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(tolerance), $"{label}.w");
        }

        /// <summary>
        /// MotionStream 観測結果を蓄積する単純な <see cref="IObserver{T}"/> 実装。
        /// メインスレッド上の <c>uOscServer.Update()</c> から呼ばれる前提のためロックは持たない。
        /// </summary>
        private sealed class FrameRecorder : IObserver<MotionFrame>
        {
            public List<MotionFrame> Frames { get; } = new List<MotionFrame>();
            public List<Exception> Errors { get; } = new List<Exception>();
            public bool Completed { get; private set; }

            public void OnNext(MotionFrame value) => Frames.Add(value);
            public void OnError(Exception error) => Errors.Add(error);
            public void OnCompleted() => Completed = true;
        }
    }
}
