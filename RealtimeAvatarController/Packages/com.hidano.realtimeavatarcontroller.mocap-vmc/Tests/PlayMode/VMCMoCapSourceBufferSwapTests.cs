using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using UnityEngine;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public sealed class VMCMoCapSourceBufferSwapTests
    {
        private const int TestPort = 49533;

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
                slotId: "slot-vmc-buffer",
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

        [Test]
        public void ForceTickForTest_SwapsInjectedWriteBufferToReadBuffer_AndClearsNextWriteBuffer()
        {
            var firstRotation = Quaternion.Euler(10f, 20f, 30f);
            _source.InjectBoneRotationForTest(HumanBodyBones.LeftHand, firstRotation);

            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(1));
            var firstFrame = AssertHumanoidFrame(_recorder.Frames[0]);
            Assert.That(firstFrame.BoneLocalRotations, Is.SameAs(_source.ReadBufferForTest));
            Assert.That(firstFrame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand), Is.True);
            Assert.That(firstFrame.BoneLocalRotations[HumanBodyBones.LeftHand], Is.EqualTo(firstRotation));
            Assert.That(_source.WriteBufferForTest.Count, Is.EqualTo(0));

            var secondRotation = Quaternion.Euler(40f, 50f, 60f);
            _source.InjectBoneRotationForTest(HumanBodyBones.Head, secondRotation);

            _source.ForceTickForTest();

            Assert.That(_recorder.Frames.Count, Is.EqualTo(2));
            var secondFrame = AssertHumanoidFrame(_recorder.Frames[1]);
            Assert.That(secondFrame.BoneLocalRotations, Is.SameAs(_source.ReadBufferForTest));
            Assert.That(secondFrame.BoneLocalRotations.ContainsKey(HumanBodyBones.Head), Is.True);
            Assert.That(secondFrame.BoneLocalRotations[HumanBodyBones.Head], Is.EqualTo(secondRotation));
            Assert.That(secondFrame.BoneLocalRotations.ContainsKey(HumanBodyBones.LeftHand), Is.False);
            Assert.That(_source.WriteBufferForTest.Count, Is.EqualTo(0));
            Assert.That(_recorder.Errors, Is.Empty);
        }

        [Test]
        public void ForceTickForTest_DoesNotEmitFrame_WhenWriteBufferIsEmpty()
        {
            _source.ForceTickForTest();

            Assert.That(_recorder.Frames, Is.Empty);
            Assert.That(_source.ReadBufferForTest.Count, Is.EqualTo(0));
            Assert.That(_source.WriteBufferForTest.Count, Is.EqualTo(0));
            Assert.That(_recorder.Errors, Is.Empty);
        }

        private static HumanoidMotionFrame AssertHumanoidFrame(MotionFrame frame)
        {
            Assert.That(frame, Is.InstanceOf<HumanoidMotionFrame>());
            var humanoid = (HumanoidMotionFrame)frame;
            Assert.That(humanoid.BoneLocalRotations, Is.Not.Null);
            return humanoid;
        }

        private sealed class FrameRecorder : IObserver<MotionFrame>
        {
            public List<MotionFrame> Frames { get; } = new List<MotionFrame>();

            public List<Exception> Errors { get; } = new List<Exception>();

            public void OnNext(MotionFrame value) => Frames.Add(value);

            public void OnError(Exception error) => Errors.Add(error);

            public void OnCompleted()
            {
            }
        }
    }
}
