using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using CoreMotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinSlotBridgeTests
    {
        private const string RootBoneName = "MOVIN:Hips";
        private const string TargetBoneName = "MOVIN:Head";
        private const string BoneClass = "MOVIN";
        private const float TimeoutSeconds = 1.0f;

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [UnityTest]
        public IEnumerator MotionStream_AppliesOnlyMovinFrames_AndDisposeOnlyUnsubscribes()
        {
            var avatar = CreateAvatar(out var targetBone);
            using var source = new TestMoCapSource();
            using var applier = new MovinMotionApplier();
            applier.SetAvatar(avatar, RootBoneName, BoneClass);

            var bridge = new MovinSlotBridge(source, applier);

            source.Emit(new NonMovinFrame());
            yield return null;

            Assert.That(targetBone.localPosition, Is.EqualTo(Vector3.zero));

            var firstPosition = new Vector3(1f, 2f, 3f);
            var firstRotation = Quaternion.Euler(10f, 20f, 30f);
            var firstScale = new Vector3(1.5f, 1.6f, 1.7f);

            source.Emit(CreateMovinFrame(firstPosition, firstRotation, firstScale));
            yield return WaitUntilPosition(targetBone, firstPosition);

            Assert.That(targetBone.localPosition, Is.EqualTo(firstPosition));
            Assert.That(Quaternion.Angle(firstRotation, targetBone.localRotation), Is.LessThan(1e-3f));
            Assert.That(targetBone.localScale, Is.EqualTo(firstScale));

            bridge.Dispose();

            Assert.That(source.DisposeCallCount, Is.EqualTo(0));

            var secondPosition = new Vector3(4f, 5f, 6f);
            var secondRotation = Quaternion.Euler(40f, 50f, 60f);
            var secondScale = new Vector3(2f, 2.1f, 2.2f);
            var secondFrame = CreateMovinFrame(secondPosition, secondRotation, secondScale);

            source.Emit(secondFrame);
            yield return null;
            yield return null;

            Assert.That(targetBone.localPosition, Is.EqualTo(firstPosition));

            applier.Apply(secondFrame);

            Assert.That(targetBone.localPosition, Is.EqualTo(secondPosition));
            Assert.That(Quaternion.Angle(secondRotation, targetBone.localRotation), Is.LessThan(1e-3f));
            Assert.That(targetBone.localScale, Is.EqualTo(secondScale));

            bridge.Dispose();
            Assert.That(source.DisposeCallCount, Is.EqualTo(0));
        }

        private GameObject CreateAvatar(out Transform targetBone)
        {
            var avatar = new GameObject("movin-slot-bridge-avatar");
            _createdObjects.Add(avatar);

            var hips = new GameObject(RootBoneName);
            _createdObjects.Add(hips);
            hips.transform.SetParent(avatar.transform);

            var head = new GameObject(TargetBoneName);
            _createdObjects.Add(head);
            head.transform.SetParent(hips.transform);

            targetBone = head.transform;
            return avatar;
        }

        private static MovinMotionFrame CreateMovinFrame(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            return new MovinMotionFrame(
                timestamp: Time.realtimeSinceStartupAsDouble,
                bones: new Dictionary<string, MovinBonePose>
                {
                    { TargetBoneName, new MovinBonePose(position, rotation, scale) },
                });
        }

        private static IEnumerator WaitUntilPosition(Transform transform, Vector3 expectedPosition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (transform.localPosition != expectedPosition && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private sealed class TestMoCapSource : IMoCapSource
        {
            private readonly Subject<CoreMotionFrame> _subject = new Subject<CoreMotionFrame>();

            public string SourceType => "TEST";

            public int DisposeCallCount { get; private set; }

            public IObservable<CoreMotionFrame> MotionStream => _subject;

            public void Initialize(MoCapSourceConfigBase config)
            {
            }

            public void Shutdown()
            {
            }

            public void Dispose()
            {
                DisposeCallCount++;
                _subject.Dispose();
            }

            public void Emit(CoreMotionFrame frame)
            {
                _subject.OnNext(frame);
            }
        }

        private sealed class NonMovinFrame : CoreMotionFrame
        {
        }
    }
}
