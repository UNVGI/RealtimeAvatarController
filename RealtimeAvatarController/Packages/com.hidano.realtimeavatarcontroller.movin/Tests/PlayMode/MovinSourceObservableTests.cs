using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;
using CoreMotionFrame = RealtimeAvatarController.Core.MotionFrame;
using Object = UnityEngine.Object;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinSourceObservableTests
    {
        private const string RootBoneName = "MOVIN:Hips";
        private const string TargetBoneName = "MOVIN:Head";

        private CapturingSlotErrorChannel _errorChannel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _errorChannel = new CapturingSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(_errorChannel);
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Tick_AfterBoneAndRootPose_EmitsMovinMotionFrameSnapshot()
        {
            using var source = CreateInitializedSource();
            var adapter = (IMovinReceiverAdapter)source;
            var received = new List<CoreMotionFrame>();
            Exception streamError = null;

            using var subscription = source.MotionStream.Subscribe(
                frame => received.Add(frame),
                ex => streamError = ex);

            var bonePosition = new Vector3(1f, 2f, 3f);
            var boneRotation = Quaternion.Euler(10f, 20f, 30f);
            var rootPosition = new Vector3(4f, 5f, 6f);
            var rootRotation = Quaternion.Euler(40f, 50f, 60f);
            var rootScale = new Vector3(1.1f, 1.2f, 1.3f);
            var rootOffset = new Vector3(0.1f, 0.2f, 0.3f);

            adapter.HandleBonePose(TargetBoneName, bonePosition, boneRotation);
            adapter.HandleRootPose(RootBoneName, rootPosition, rootRotation, rootScale, rootOffset);
            adapter.Tick();

            Assert.That(streamError, Is.Null);
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0], Is.TypeOf<MovinMotionFrame>());

            var frame = (MovinMotionFrame)received[0];
            Assert.That(frame.Bones, Has.Count.EqualTo(1));
            Assert.That(frame.Bones.ContainsKey(TargetBoneName), Is.True);

            var bonePose = frame.Bones[TargetBoneName];
            Assert.That(bonePose.LocalPosition, Is.EqualTo(bonePosition));
            Assert.That(Quaternion.Angle(boneRotation, bonePose.LocalRotation), Is.LessThan(1e-3f));
            Assert.That(bonePose.LocalScale, Is.Null);

            Assert.That(frame.RootPose.HasValue, Is.True);
            var rootPose = frame.RootPose.Value;
            Assert.That(rootPose.BoneName, Is.EqualTo(RootBoneName));
            Assert.That(rootPose.LocalPosition, Is.EqualTo(rootPosition));
            Assert.That(Quaternion.Angle(rootRotation, rootPose.LocalRotation), Is.LessThan(1e-3f));
            Assert.That(rootPose.LocalScale.HasValue, Is.True);
            Assert.That(rootPose.LocalScale.Value, Is.EqualTo(rootScale));
            Assert.That(rootPose.LocalOffset.HasValue, Is.True);
            Assert.That(rootPose.LocalOffset.Value, Is.EqualTo(rootOffset));
            Assert.That(_errorChannel.PublishedErrors, Is.Empty);
        }

        [Test]
        public void Tick_WithNoBones_DoesNotEmitFrameOrError()
        {
            using var source = CreateInitializedSource();
            var adapter = (IMovinReceiverAdapter)source;
            var received = new List<CoreMotionFrame>();
            Exception streamError = null;

            using var subscription = source.MotionStream.Subscribe(
                frame => received.Add(frame),
                ex => streamError = ex);

            adapter.HandleRootPose(
                RootBoneName,
                Vector3.one,
                Quaternion.identity,
                localScale: null,
                localOffset: null);
            adapter.Tick();

            Assert.That(received, Is.Empty);
            Assert.That(streamError, Is.Null);
            Assert.That(_errorChannel.PublishedErrors, Is.Empty);
        }

        [Test]
        public void Tick_WhenObserverThrows_PublishesVmcReceiveAndKeepsStreamAlive()
        {
            using var source = CreateInitializedSource();
            var adapter = (IMovinReceiverAdapter)source;
            var expectedException = new InvalidOperationException("observer failed during MOVIN source tick");
            Exception streamError = null;

            using (source.MotionStream.Subscribe(
                       _ => throw expectedException,
                       ex => streamError = ex))
            {
                adapter.HandleBonePose(TargetBoneName, Vector3.one, Quaternion.identity);

                Assert.DoesNotThrow(() => adapter.Tick());
            }

            Assert.That(streamError, Is.Null);
            Assert.That(_errorChannel.PublishedErrors, Has.Count.EqualTo(1));

            var error = _errorChannel.PublishedErrors[0];
            Assert.That(error.SlotId, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.VmcReceive));
            Assert.That(error.Exception, Is.SameAs(expectedException));

            var received = new List<CoreMotionFrame>();
            using var subscription = source.MotionStream.Subscribe(
                frame => received.Add(frame),
                ex => streamError = ex);

            adapter.Tick();

            Assert.That(streamError, Is.Null);
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0], Is.TypeOf<MovinMotionFrame>());
            Assert.That(_errorChannel.PublishedErrors, Has.Count.EqualTo(1));
        }

        private static MovinMoCapSource CreateInitializedSource()
        {
            var source = new MovinMoCapSource();
            var config = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();

            try
            {
                config.port = GetAvailableUdpPort();
                source.Initialize(config);
                return source;
            }
            catch
            {
                source.Dispose();
                throw;
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static int GetAvailableUdpPort()
        {
            using var socket = new UdpClient(0);
            return ((IPEndPoint)socket.Client.LocalEndPoint).Port;
        }

        private sealed class CapturingSlotErrorChannel : ISlotErrorChannel
        {
            private readonly Subject<SlotError> _errors = new Subject<SlotError>();

            public List<SlotError> PublishedErrors { get; } = new List<SlotError>();

            public IObservable<SlotError> Errors => _errors;

            public void Publish(SlotError error)
            {
                PublishedErrors.Add(error);
                _errors.OnNext(error);
            }
        }
    }
}
