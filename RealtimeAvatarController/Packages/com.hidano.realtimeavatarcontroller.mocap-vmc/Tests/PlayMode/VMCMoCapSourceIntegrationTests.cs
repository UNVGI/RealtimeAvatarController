using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using uOSC;
using UnityEngine;
using UnityEngine.TestTools;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public sealed class VMCMoCapSourceIntegrationTests
    {
        private const int TestPort = 49502;
        private const float DefaultTimeoutSeconds = 5.0f;
        private const string BonePosAddress = "/VMC/Ext/Bone/Pos";

        private VMCMoCapSourceConfig _config;
        private VMCMoCapSource _source;
        private FrameRecorder _recorder;
        private IDisposable _subscription;
        private GameObject _clientHost;
        private uOscClient _client;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            VMCSharedReceiver.ResetForTest();

            _config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _config.port = TestPort;
            _config.bindAddress = "0.0.0.0";

            _source = new VMCMoCapSource(
                slotId: "slot-vmc-integration",
                errorChannel: RegistryLocator.ErrorChannel);
            _source.Initialize(_config);

            _recorder = new FrameRecorder();
            _subscription = _source.MotionStream.Subscribe(_recorder);
        }

        [TearDown]
        public void TearDown()
        {
            if (_clientHost != null)
            {
                UnityEngine.Object.DestroyImmediate(_clientHost);
                _clientHost = null;
                _client = null;
            }

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

        [Test]
        public void ForceTickForTest_EmitsInjectedBoneAndRoot()
        {
            var expectedRotation = Quaternion.Euler(11f, 22f, 33f);
            var expectedRootPosition = new Vector3(1f, 2f, 3f);
            var expectedRootRotation = Quaternion.Euler(4f, 5f, 6f);

            _source.InjectBoneRotationForTest(HumanBodyBones.LeftHand, expectedRotation);
            _source.InjectRootForTest(expectedRootPosition, expectedRootRotation);
            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(1));
            var frame = AssertHumanoidFrame(_recorder.Frames[0]);
            Assert.That(frame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand), Is.True);
            AssertQuaternion(frame.BoneLocalRotations[HumanBodyBones.LeftHand], expectedRotation);
            Assert.That(frame.RootPosition, Is.EqualTo(expectedRootPosition));
            AssertQuaternion(frame.RootRotation, expectedRootRotation);
            Assert.That(_recorder.Errors, Is.Empty);
        }

        [Test]
        public void ForceTickForTest_DoesNotEmit_WhenNoNewInjection()
        {
            _source.InjectBoneRotationForTest(HumanBodyBones.LeftHand, Quaternion.identity);
            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(1));

            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(1));
            Assert.That(_recorder.Errors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MotionStream_ReceivesLoopbackBonePacket_FromUOscClient()
        {
            StartLoopbackClient();

            var expectedRotation = Quaternion.Euler(15f, 25f, 35f);
            SendBone(HumanBodyBones.LeftHand, expectedRotation);

            yield return WaitForFrame(
                frame => frame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand),
                DefaultTimeoutSeconds);

            var received = FindFrame(frame => frame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand));
            Assert.That(received, Is.Not.Null);
            AssertQuaternion(received.BoneLocalRotations[HumanBodyBones.LeftHand], expectedRotation);
            Assert.That(_recorder.Errors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MotionStream_ReceivesLoopbackAllBones_InSingleFrame()
        {
            StartLoopbackClient();

            var expectedBones = EnumerateHumanoidBones();
            var expectedRotation = Quaternion.Euler(5f, 10f, 15f);

            Assert.That(expectedBones.Count, Is.EqualTo(55));
            SendAllBonesBundle(expectedBones, expectedRotation);

            yield return WaitForFrame(
                frame => ContainsAllBones(frame, expectedBones),
                DefaultTimeoutSeconds);

            var received = FindFrame(frame => ContainsAllBones(frame, expectedBones));
            Assert.That(received, Is.Not.Null);
            Assert.That(received.BoneLocalRotations.Count, Is.EqualTo(55));

            foreach (var bone in expectedBones)
            {
                Assert.That(received.BoneLocalRotations.ContainsKey(bone), Is.True, bone.ToString());
                AssertQuaternion(received.BoneLocalRotations[bone], expectedRotation);
            }

            Assert.That(_recorder.Errors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MotionStream_CompletesAfterShutdown_WithoutError()
        {
            _source.InjectBoneRotationForTest(HumanBodyBones.Hips, Quaternion.identity);
            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(1));
            Assert.That(_recorder.Errors, Is.Empty);

            _source.Shutdown();
            yield return null;

            Assert.That(_recorder.Completed, Is.True);
            Assert.That(_recorder.Errors, Is.Empty);
        }

        private void StartLoopbackClient()
        {
            if (_client != null)
            {
                return;
            }

            _clientHost = new GameObject("VMCMoCapSourceIntegrationTests.uOscClient");
            _clientHost.SetActive(false);
            _client = _clientHost.AddComponent<uOscClient>();
            _client.address = "127.0.0.1";
            _client.port = TestPort;
            _client.maxQueueSize = 256;
            _client.dataTransimissionInterval = 0f;
            _clientHost.SetActive(true);
        }

        private void SendBone(HumanBodyBones bone, Quaternion rotation)
        {
            _client.Send(
                BonePosAddress,
                bone.ToString(),
                0f,
                0f,
                0f,
                rotation.x,
                rotation.y,
                rotation.z,
                rotation.w);
        }

        private void SendAllBonesBundle(IReadOnlyList<HumanBodyBones> bones, Quaternion rotation)
        {
            var bundle = new Bundle();
            for (var i = 0; i < bones.Count; i++)
            {
                bundle.Add(new Message(
                    BonePosAddress,
                    bones[i].ToString(),
                    0f,
                    0f,
                    0f,
                    rotation.x,
                    rotation.y,
                    rotation.z,
                    rotation.w));
            }

            _client.Send(bundle);
        }

        private IEnumerator WaitForFrame(Predicate<HumanoidMotionFrame> predicate, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (FindFrame(predicate) == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private HumanoidMotionFrame FindFrame(Predicate<HumanoidMotionFrame> predicate)
        {
            for (var i = 0; i < _recorder.Frames.Count; i++)
            {
                if (_recorder.Frames[i] is HumanoidMotionFrame frame && predicate(frame))
                {
                    return frame;
                }
            }

            return null;
        }

        private static List<HumanBodyBones> EnumerateHumanoidBones()
        {
            var bones = new List<HumanBodyBones>(55);
            for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                bones.Add(bone);
            }

            return bones;
        }

        private static bool ContainsAllBones(
            HumanoidMotionFrame frame,
            IReadOnlyList<HumanBodyBones> expectedBones)
        {
            for (var i = 0; i < expectedBones.Count; i++)
            {
                if (!frame.BoneLocalRotations.ContainsKey(expectedBones[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static HumanoidMotionFrame AssertHumanoidFrame(MotionFrame frame)
        {
            Assert.That(frame, Is.InstanceOf<HumanoidMotionFrame>());
            var humanoid = (HumanoidMotionFrame)frame;
            Assert.That(humanoid.BoneLocalRotations, Is.Not.Null);
            Assert.That(humanoid.Muscles, Is.Not.Null);
            Assert.That(humanoid.Muscles.Length, Is.EqualTo(0));
            Assert.That(humanoid.IsValid, Is.True);
            return humanoid;
        }

        private static void AssertQuaternion(Quaternion actual, Quaternion expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(0.0001f));
        }

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
