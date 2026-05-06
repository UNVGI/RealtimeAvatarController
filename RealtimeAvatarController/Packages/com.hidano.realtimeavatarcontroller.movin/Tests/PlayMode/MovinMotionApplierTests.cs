using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinMotionApplierTests
    {
        private const string RootBoneName = "mixamorig:Hips";
        private const string SpineBoneName = "mixamorig:Spine";
        private const string HeadBoneName = "mixamorig:Head";
        private const string MissingBoneName = "mixamorig:Missing";

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void TryBuild_WithThreeLevelMixamorigAvatar_BuildsNameLookupFromRootBone()
        {
            var avatar = CreateThreeLevelAvatar(out var hips, out var spine, out var head);

            var built = MovinBoneTable.TryBuild(
                avatar.transform,
                RootBoneName,
                boneClass: null,
                out var table);

            Assert.That(built, Is.True);
            Assert.That(table, Is.Not.Null);
            Assert.That(table, Has.Count.EqualTo(3));
            Assert.That(table[RootBoneName], Is.SameAs(hips));
            Assert.That(table[SpineBoneName], Is.SameAs(spine));
            Assert.That(table[HeadBoneName], Is.SameAs(head));
        }

        [Test]
        public void Apply_WithBoneAndRootPose_WritesMatchingTransformsAndSkipsMissingBone()
        {
            var avatar = CreateThreeLevelAvatar(out var hips, out var spine, out var head);
            using var applier = new MovinMotionApplier();

            Assert.DoesNotThrow(() => applier.SetAvatar(avatar, RootBoneName, boneClass: null));

            var hipsPosition = new Vector3(1f, 2f, 3f);
            var hipsRotation = Quaternion.Euler(10f, 20f, 30f);
            var hipsScale = new Vector3(1.2f, 1.3f, 1.4f);
            var spinePosition = new Vector3(4f, 5f, 6f);
            var spineRotation = Quaternion.Euler(40f, 50f, 60f);
            var headPosition = new Vector3(7f, 8f, 9f);
            var headRotation = Quaternion.Euler(70f, 80f, 90f);
            var headScale = new Vector3(0.8f, 0.9f, 1.1f);

            var frame = new MovinMotionFrame(
                timestamp: Time.realtimeSinceStartupAsDouble,
                bones: new Dictionary<string, MovinBonePose>
                {
                    { SpineBoneName, new MovinBonePose(spinePosition, spineRotation) },
                    { HeadBoneName, new MovinBonePose(headPosition, headRotation, headScale) },
                    { MissingBoneName, new MovinBonePose(Vector3.one * 100f, Quaternion.Euler(1f, 2f, 3f), Vector3.one * 9f) },
                },
                rootPose: new MovinRootPose(
                    RootBoneName,
                    hipsPosition,
                    hipsRotation,
                    hipsScale));

            Assert.DoesNotThrow(() => applier.Apply(frame));

            AssertTransform(hips, hipsPosition, hipsRotation, hipsScale);
            AssertTransform(spine, spinePosition, spineRotation, Vector3.one);
            AssertTransform(head, headPosition, headRotation, headScale);
            Assert.That(avatar.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(avatar.transform.localScale, Is.EqualTo(Vector3.one));
        }

        private GameObject CreateThreeLevelAvatar(
            out Transform hips,
            out Transform spine,
            out Transform head)
        {
            var avatar = new GameObject("movin-applier-avatar");
            _createdObjects.Add(avatar);

            hips = new GameObject(RootBoneName).transform;
            hips.SetParent(avatar.transform, false);

            spine = new GameObject(SpineBoneName).transform;
            spine.SetParent(hips, false);

            head = new GameObject(HeadBoneName).transform;
            head.SetParent(spine, false);

            return avatar;
        }

        private static void AssertTransform(
            Transform transform,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            Vector3 expectedScale)
        {
            Assert.That(transform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(expectedRotation, transform.localRotation), Is.LessThan(1e-3f));
            Assert.That(transform.localScale, Is.EqualTo(expectedScale));
        }
    }
}
