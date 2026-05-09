using System.Collections;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public sealed class VMCMoCapSourceSharingTests
    {
        private const int PortA = 49540;
        private const int PortB = 49541;

        private VMCMoCapSourceConfig _configA;
        private VMCMoCapSourceConfig _configB;
        private VMCMoCapSource _sourceA;
        private VMCMoCapSource _sourceB;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            VMCSharedReceiver.ResetForTest();

            _configA = CreateConfig(PortA);
            _configB = CreateConfig(PortB);
        }

        [TearDown]
        public void TearDown()
        {
            _sourceA?.Dispose();
            _sourceA = null;

            _sourceB?.Dispose();
            _sourceB = null;

            if (_configA != null)
            {
                Object.DestroyImmediate(_configA);
                _configA = null;
            }

            if (_configB != null)
            {
                Object.DestroyImmediate(_configB);
                _configB = null;
            }

            VMCSharedReceiver.ResetForTest();
            RegistryLocator.ResetForTest();
        }

        [UnityTest]
        public IEnumerator Initialize_TwoSourcesWithSameConfig_SharesSingleReceiverByRefCount()
        {
            _sourceA = CreateSource("slot-vmc-sharing-a");
            _sourceB = CreateSource("slot-vmc-sharing-b");

            _sourceA.Initialize(_configA);
            _sourceB.Initialize(_configA);

            var receiver = _sourceA.SharedReceiverForTest;
            var host = receiver.gameObject;

            Assert.That(_sourceB.SharedReceiverForTest, Is.SameAs(receiver));
            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.EqualTo(1));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(2));
            Assert.That(receiver.RefCountForTest, Is.EqualTo(2));
            Assert.That(host.GetComponent<uOSC.uOscServer>().port, Is.EqualTo(PortA));

            _sourceA.Dispose();
            _sourceA = null;

            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.EqualTo(1));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(1));
            Assert.That(receiver.RefCountForTest, Is.EqualTo(1));
            Assert.That(host == null, Is.False);

            _sourceB.Dispose();
            _sourceB = null;
            yield return null;

            Assert.That(VMCSharedReceiver.InstanceForTest, Is.Null);
            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.Zero);
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.Zero);
            Assert.That(host == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Initialize_DifferentConfigPorts_CreatesIndependentReceiversAndReleasesSeparately()
        {
            _sourceA = CreateSource("slot-vmc-sharing-port-a");
            _sourceB = CreateSource("slot-vmc-sharing-port-b");

            _sourceA.Initialize(_configA);
            _sourceB.Initialize(_configB);

            var receiverA = _sourceA.SharedReceiverForTest;
            var receiverB = _sourceB.SharedReceiverForTest;
            var hostA = receiverA.gameObject;
            var hostB = receiverB.gameObject;

            Assert.That(receiverA, Is.Not.SameAs(receiverB));
            Assert.That(receiverA.RefCountForTest, Is.EqualTo(1));
            Assert.That(receiverB.RefCountForTest, Is.EqualTo(1));
            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.EqualTo(2));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(2));
            Assert.That(hostA.GetComponent<uOSC.uOscServer>().port, Is.EqualTo(PortA));
            Assert.That(hostB.GetComponent<uOSC.uOscServer>().port, Is.EqualTo(PortB));

            _sourceA.Dispose();
            _sourceA = null;
            yield return null;

            Assert.That(hostA == null, Is.True);
            Assert.That(hostB == null, Is.False);
            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.EqualTo(1));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(1));
            Assert.That(receiverB.RefCountForTest, Is.EqualTo(1));

            _sourceB.Dispose();
            _sourceB = null;
            yield return null;

            Assert.That(VMCSharedReceiver.InstanceForTest, Is.Null);
            Assert.That(VMCSharedReceiver.InstanceCountForTest, Is.Zero);
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.Zero);
            Assert.That(hostB == null, Is.True);
        }

        private static VMCMoCapSource CreateSource(string slotId)
        {
            return new VMCMoCapSource(slotId, RegistryLocator.ErrorChannel);
        }

        private static VMCMoCapSourceConfig CreateConfig(int port)
        {
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            config.bindAddress = "0.0.0.0";
            config.port = port;
            return config;
        }
    }
}
