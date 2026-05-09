using NUnit.Framework;
using uOSC;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public class VmcOscMessageRouterTests
    {
        private const string BonePosAddress = "/VMC/Ext/Bone/Pos";
        private const string RootPosAddress = "/VMC/Ext/Root/Pos";

        [Test]
        public void TryParseBoneMessage_WithValidBoneMessage_ReturnsBoneAndRotation()
        {
            var message = CreateMessage(
                BonePosAddress,
                "Hips",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            var parsed = VmcOscMessageRouter.TryParseBoneMessage(
                in message,
                out var bone,
                out var rotation);

            Assert.That(parsed, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.Hips));
            Assert.That(rotation.x, Is.EqualTo(0.1f));
            Assert.That(rotation.y, Is.EqualTo(0.2f));
            Assert.That(rotation.z, Is.EqualTo(0.3f));
            Assert.That(rotation.w, Is.EqualTo(0.4f));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(9)]
        public void TryParseBoneMessage_WithLengthMismatch_ReturnsFalse(int argumentCount)
        {
            var message = CreateMessage(BonePosAddress, CreateArguments(BonePosAddress, argumentCount));

            var parsed = VmcOscMessageRouter.TryParseBoneMessage(
                in message,
                out _,
                out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseBoneMessage_WithTypeMismatch_ReturnsFalse()
        {
            var message = CreateMessage(
                BonePosAddress,
                "Hips",
                1f,
                2f,
                3f,
                "not-a-float",
                0.2f,
                0.3f,
                0.4f);

            var parsed = VmcOscMessageRouter.TryParseBoneMessage(
                in message,
                out _,
                out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseBoneMessage_WithUnknownBoneName_ReturnsFalse()
        {
            var message = CreateMessage(
                BonePosAddress,
                "Foo",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            var parsed = VmcOscMessageRouter.TryParseBoneMessage(
                in message,
                out _,
                out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseRootMessage_WithEightArguments_ReturnsPositionAndRotation()
        {
            var message = CreateMessage(
                RootPosAddress,
                "root",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            var parsed = VmcOscMessageRouter.TryParseRootMessage(
                in message,
                out var position,
                out var rotation);

            Assert.That(parsed, Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(rotation.x, Is.EqualTo(0.1f));
            Assert.That(rotation.y, Is.EqualTo(0.2f));
            Assert.That(rotation.z, Is.EqualTo(0.3f));
            Assert.That(rotation.w, Is.EqualTo(0.4f));
        }

        [Test]
        public void TryParseRootMessage_WithFourteenArguments_UsesFirstEightArguments()
        {
            var message = CreateMessage(
                RootPosAddress,
                "root",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f,
                10f,
                11f,
                12f,
                13f,
                14f,
                15f);

            var parsed = VmcOscMessageRouter.TryParseRootMessage(
                in message,
                out var position,
                out var rotation);

            Assert.That(parsed, Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(rotation.x, Is.EqualTo(0.1f));
            Assert.That(rotation.y, Is.EqualTo(0.2f));
            Assert.That(rotation.z, Is.EqualTo(0.3f));
            Assert.That(rotation.w, Is.EqualTo(0.4f));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(9)]
        public void TryParseRootMessage_WithLengthMismatch_ReturnsFalse(int argumentCount)
        {
            var message = CreateMessage(RootPosAddress, CreateArguments(RootPosAddress, argumentCount));

            var parsed = VmcOscMessageRouter.TryParseRootMessage(
                in message,
                out _,
                out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void TryParseRootMessage_WithTypeMismatch_ReturnsFalse()
        {
            var message = CreateMessage(
                RootPosAddress,
                "root",
                "not-a-float",
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            var parsed = VmcOscMessageRouter.TryParseRootMessage(
                in message,
                out _,
                out _);

            Assert.That(parsed, Is.False);
        }

        [Test]
        public void RouteMessage_WithBonePosEightArguments_WritesBoneRotationOnce()
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                BonePosAddress,
                "Hips",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            Route(message, writer);

            Assert.That(writer.BoneRotationWriteCount, Is.EqualTo(1));
            Assert.That(writer.RootWriteCount, Is.Zero);
            Assert.That(writer.LastBone, Is.EqualTo(HumanBodyBones.Hips));
            Assert.That(writer.LastBoneRotation.x, Is.EqualTo(0.1f));
            Assert.That(writer.LastBoneRotation.y, Is.EqualTo(0.2f));
            Assert.That(writer.LastBoneRotation.z, Is.EqualTo(0.3f));
            Assert.That(writer.LastBoneRotation.w, Is.EqualTo(0.4f));
        }

        [Test]
        public void RouteMessage_WithRootPosEightArguments_WritesRootOnce()
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                RootPosAddress,
                "root",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            Route(message, writer);

            Assert.That(writer.RootWriteCount, Is.EqualTo(1));
            Assert.That(writer.BoneRotationWriteCount, Is.Zero);
            Assert.That(writer.LastRootPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(writer.LastRootRotation.x, Is.EqualTo(0.1f));
            Assert.That(writer.LastRootRotation.y, Is.EqualTo(0.2f));
            Assert.That(writer.LastRootRotation.z, Is.EqualTo(0.3f));
            Assert.That(writer.LastRootRotation.w, Is.EqualTo(0.4f));
        }

        [Test]
        public void RouteMessage_WithRootPosFourteenArguments_UsesFirstEightArgumentsWithoutException()
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                RootPosAddress,
                "root",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f,
                10f,
                11f,
                12f,
                13f,
                14f,
                15f);

            Route(message, writer);

            Assert.That(writer.RootWriteCount, Is.EqualTo(1));
            Assert.That(writer.BoneRotationWriteCount, Is.Zero);
            Assert.That(writer.LastRootPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(writer.LastRootRotation.x, Is.EqualTo(0.1f));
            Assert.That(writer.LastRootRotation.y, Is.EqualTo(0.2f));
            Assert.That(writer.LastRootRotation.z, Is.EqualTo(0.3f));
            Assert.That(writer.LastRootRotation.w, Is.EqualTo(0.4f));
        }

        [TestCase(BonePosAddress, 0)]
        [TestCase(BonePosAddress, 7)]
        [TestCase(BonePosAddress, 9)]
        [TestCase(RootPosAddress, 0)]
        [TestCase(RootPosAddress, 7)]
        [TestCase(RootPosAddress, 9)]
        public void RouteMessage_WithInvalidArgumentLength_DoesNotCallWriterWithoutException(string address, int argumentCount)
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(address, CreateArguments(address, argumentCount));

            Route(message, writer);

            AssertNoWrites(writer);
        }

        [TestCase(BonePosAddress)]
        [TestCase(RootPosAddress)]
        public void RouteMessage_WithTypeMismatch_DoesNotCallWriterWithoutException(string address)
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                address,
                0f,
                "not-a-float",
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            Route(message, writer);

            AssertNoWrites(writer);
        }

        [Test]
        public void RouteMessage_WithUnknownBoneName_DoesNotCallWriterWithoutException()
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                BonePosAddress,
                "Foo",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            Route(message, writer);

            AssertNoWrites(writer);
        }

        [TestCase("/VMC/Ext/Blend/Apply")]
        [TestCase("/VMC/Ext/Blend/Val")]
        [TestCase("/VMC/Ext/Cam")]
        [TestCase("/VMC/Ext/Light")]
        [TestCase("/VMC/Ext/Hmd/Pos")]
        [TestCase("/VMC/Ext/Con/Pos")]
        [TestCase("/VMC/Ext/Tra/Pos")]
        [TestCase("/VMC/Ext/Setting/Color")]
        [TestCase("/VMC/Ext/OK")]
        [TestCase("/VMC/Ext/T")]
        [TestCase("/VMC/Ext/VRM")]
        [TestCase("/VMC/Ext/Root/T")]
        public void RouteMessage_WithSilentlyIgnoredOscAddress_DoesNotCallWriterWithoutException(string address)
        {
            var writer = new FakeVmcBoneRotationWriter();
            var message = CreateMessage(
                address,
                "Hips",
                1f,
                2f,
                3f,
                0.1f,
                0.2f,
                0.3f,
                0.4f);

            Route(message, writer);

            AssertNoWrites(writer);
        }

        private static Message CreateMessage(string address, params object[] values)
        {
            return new Message(address, values);
        }

        private static object[] CreateArguments(string address, int count)
        {
            var values = new object[count];

            if (count == 0)
            {
                return values;
            }

            values[0] = address == BonePosAddress ? "Hips" : "root";

            for (var i = 1; i < values.Length; i++)
            {
                values[i] = (float)i;
            }

            return values;
        }

        private static void Route(Message message, FakeVmcBoneRotationWriter writer)
        {
            Assert.DoesNotThrow(() => VmcOscMessageRouter.RouteMessage(in message, writer));
        }

        private static void AssertNoWrites(FakeVmcBoneRotationWriter writer)
        {
            Assert.That(writer.BoneRotationWriteCount, Is.Zero);
            Assert.That(writer.RootWriteCount, Is.Zero);
        }

        private sealed class FakeVmcBoneRotationWriter : IVmcBoneRotationWriter
        {
            public int BoneRotationWriteCount { get; private set; }
            public int RootWriteCount { get; private set; }
            public HumanBodyBones LastBone { get; private set; }
            public Quaternion LastBoneRotation { get; private set; }
            public Vector3 LastRootPosition { get; private set; }
            public Quaternion LastRootRotation { get; private set; }

            public void WriteBoneRotation(HumanBodyBones bone, Quaternion rotation)
            {
                BoneRotationWriteCount++;
                LastBone = bone;
                LastBoneRotation = rotation;
            }

            public void WriteRoot(Vector3 position, Quaternion rotation)
            {
                RootWriteCount++;
                LastRootPosition = position;
                LastRootRotation = rotation;
            }
        }
    }
}
