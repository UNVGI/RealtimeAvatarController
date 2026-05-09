using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;
using HumanoidMotionFrame = RealtimeAvatarController.Motion.HumanoidMotionFrame;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <see cref="VMCMoCapSource"/> の Dictionary 注入経路を検証する PlayMode 統合テスト
    /// (tasks.md タスク 4.2 / design.md §4.2, §5.1 /
    /// requirements.md 要件 3.1, 3.2, 3.5, 3.6, 4.5, 4.6, 4.7, 8.1, 12.3, 12.5, 12.7)。
    ///
    /// <para>
    /// 検証対象:
    ///   - VMCSharedReceiver → InjectBoneRotationForTest → LateUpdate Tick → MotionStream OnNext
    ///   - 再注入なしの次フレームでは OnNext が発行されないこと (要件 3.5 _dirty 判定)
    ///   - 55 bone 全注入フレームで全ボーンが BoneLocalRotations に含まれること
    ///   - Muscles.Length == 0 / IsValid == true (要件 3.2)
    ///   - MotionStream.OnError が一度も呼ばれないこと (要件 8.1)
    ///   - Shutdown 後に OnCompleted が発行されること (要件 4.5)
    /// </para>
    ///
    /// <para>
    /// テスト独立性: <c>[SetUp]</c>/<c>[TearDown]</c> で
    /// <see cref="RegistryLocator.ResetForTest"/> / <see cref="VMCSharedReceiver.ResetForTest"/>
    /// を呼び出す (要件 12.5)。UDP を介さないため実ポート bind 不要。
    /// </para>
    /// </summary>
    [TestFixture]
    public class VMCMoCapSourceIntegrationTests
    {
        /// <summary>テスト専用 UDP ポート (衝突しにくい高位ポートを採用)。</summary>
        private const int TestPort = 49502;

        /// <summary>受信フレーム到着待ちの既定タイムアウト秒数。</summary>
        private const float DefaultTimeoutSeconds = 2.0f;

        private VMCMoCapSourceConfig _config;
        private VMCMoCapSource _source;
        private FrameRecorder _recorder;
        private IDisposable _subscription;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            VMCSharedReceiver.ResetForTest();

            _config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _config.port = TestPort;
            _config.bindAddress = "0.0.0.0";

            _source = new VMCMoCapSource(
                slotId: string.Empty,
                errorChannel: RegistryLocator.ErrorChannel);
            _source.Initialize(_config);

            _recorder = new FrameRecorder();
            _subscription = _source.MotionStream.Subscribe(_recorder);
        }

        [TearDown]
        public void TearDown()
        {
            _subscription?.Dispose();
            _subscription = null;

            _source?.Dispose();
            _source = null;

            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            VMCSharedReceiver.ResetForTest();
            RegistryLocator.ResetForTest();
        }

        // --- ケース 1: 1 bone 注入 → OnNext (要件 3.1, 12.3, 12.7) ---

        [UnityTest]
        public IEnumerator MotionStream_EmitsFrame_WithInjectedBoneRotation()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                var expected = Quaternion.Euler(11f, 22f, 33f);
                shared.WriteBoneRotation(HumanBodyBones.LeftHand, expected);

                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

                Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                    "Bone 注入直後の LateUpdate で MotionStream に 1 件以上のフレームが届くべき (要件 3.1)。");

                var frame = _recorder.Frames[0] as HumanoidMotionFrame;
                Assert.That(frame, Is.Not.Null, "フレームは HumanoidMotionFrame であるべき。");
                Assert.That(frame.BoneLocalRotations, Is.Not.Null, "BoneLocalRotations は非 null であるべき (要件 3.1)。");
                Assert.That(frame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand), Is.True,
                    "注入した LeftHand が BoneLocalRotations に含まれるべき。");
                Assert.That(frame.BoneLocalRotations[HumanBodyBones.LeftHand], Is.EqualTo(expected),
                    "BoneLocalRotations[LeftHand] は注入した回転値と一致するべき。");
            }
            finally
            {
                shared.Release();
            }
        }

        // --- ケース 2: 再注入なしで次フレームが進んだ場合は OnNext が発行されない (要件 3.5) ---

        [UnityTest]
        public IEnumerator MotionStream_DoesNotEmit_WhenNoNewInjection()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                shared.WriteBoneRotation(HumanBodyBones.LeftHand, Quaternion.Euler(1f, 2f, 3f));

                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

                Assume.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                    "前提: 1 回目の注入で少なくとも 1 フレーム受信済み。");
                var countAfterFirst = _recorder.Frames.Count;

                yield return null;
                yield return null;

                Assert.That(_recorder.Frames.Count, Is.EqualTo(countAfterFirst),
                    "再注入なしで Tick が進んでも OnNext は追加発行されないべき (要件 3.5 _dirty 判定)。");
            }
            finally
            {
                shared.Release();
            }
        }

        // --- ケース 3: 55 bone 全注入フレームで全ボーンが含まれる (要件 3.1) ---

        [UnityTest]
        public IEnumerator MotionStream_EmitsFrame_WithAllBonesIncluded()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                var injected = new List<HumanBodyBones>();
                var q = Quaternion.Euler(5f, 10f, 15f);
                for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
                {
                    shared.WriteBoneRotation(bone, q);
                    injected.Add(bone);
                }

                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

                Assert.That(_recorder.Frames.Count, Is.GreaterThanOrEqualTo(1),
                    "全 bone 注入後 1 フレーム待機すると OnNext が発行されるべき。");

                var frame = _recorder.Frames[_recorder.Frames.Count - 1] as HumanoidMotionFrame;
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.BoneLocalRotations, Is.Not.Null);
                foreach (var bone in injected)
                {
                    Assert.That(frame.BoneLocalRotations.ContainsKey(bone), Is.True,
                        $"BoneLocalRotations は注入したボーン {bone} を含むべき。");
                }
            }
            finally
            {
                shared.Release();
            }
        }

        // --- ケース 4: Muscles.Length == 0 / IsValid == true (要件 3.2) ---

        [UnityTest]
        public IEnumerator MotionStream_Frame_HasEmptyMuscles_AndIsValid()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                shared.WriteBoneRotation(HumanBodyBones.Spine, Quaternion.identity);

                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

                var frame = _recorder.Frames[0] as HumanoidMotionFrame;
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.Muscles, Is.Not.Null, "Muscles は null であってはならない。");
                Assert.That(frame.Muscles.Length, Is.EqualTo(0),
                    "VMC 経路では Muscles は常に空配列であるべき (要件 3.2)。");
                Assert.That(frame.IsValid, Is.True,
                    "BoneLocalRotations に要素があれば IsValid は true になるべき (要件 3.2)。");
            }
            finally
            {
                shared.Release();
            }
        }

        // --- ケース 5: OnError が一度も呼ばれない (要件 8.1) ---

        [UnityTest]
        public IEnumerator MotionStream_OnError_IsNeverInvoked()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                shared.WriteBoneRotation(HumanBodyBones.Head, Quaternion.identity);

                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);

                Assert.That(_recorder.Errors, Is.Empty,
                    "正常系でも異常系でも MotionStream.OnError は発行されないべき (要件 8.1)。");
            }
            finally
            {
                shared.Release();
            }
        }

        // --- ケース 6: Shutdown 後に OnCompleted が発行される (要件 4.5) ---

        [UnityTest]
        public IEnumerator MotionStream_CompletesAfterShutdown()
        {
            var shared = VMCSharedReceiver.EnsureInstance();
            try
            {
                shared.WriteBoneRotation(HumanBodyBones.Hips, Quaternion.identity);
                yield return WaitForFrames(_recorder, expectedCount: 1, timeoutSeconds: DefaultTimeoutSeconds);
            }
            finally
            {
                shared.Release();
            }

            _source.Shutdown();
            yield return null;

            Assert.That(_recorder.Completed, Is.True,
                "Shutdown 後に MotionStream の OnCompleted が発行されるべき (要件 4.5)。");
            Assert.That(_recorder.Errors, Is.Empty,
                "Shutdown 経路でも OnError は発行されないべき (要件 8.1)。");
        }

        // --- ヘルパー ---

        /// <summary>
        /// <paramref name="recorder"/>.Frames が <paramref name="expectedCount"/> 件以上に達するまで
        /// PlayMode のフレームを進めながら待機する。タイムアウト時はそのまま return し、
        /// 呼び出し側 <c>Assert</c> が判定する。
        /// </summary>
        private static IEnumerator WaitForFrames(FrameRecorder recorder, int expectedCount, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (recorder.Frames.Count < expectedCount && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>
        /// MotionStream 観測結果を蓄積する単純な <see cref="IObserver{T}"/> 実装。
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
