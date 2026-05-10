using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using uOSC;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public class VMCSharedReceiverTests
    {
        [SetUp]
        public void SetUp()
        {
            VMCSharedReceiver.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            VMCSharedReceiver.ResetForTest();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void EnsureInstance_FirstCall_CreatesHostGameObjectWithServerAndReceiverComponents()
        {
            var receiver = VMCSharedReceiver.EnsureInstance();

            Assert.That(receiver, Is.Not.Null);
            Assert.That(receiver.gameObject, Is.Not.Null);
            Assert.That(receiver.gameObject.GetComponent<VMCSharedReceiver>(), Is.SameAs(receiver));
            Assert.That(receiver.gameObject.GetComponent<uOscServer>(), Is.Not.Null);
            Assert.That(VMCSharedReceiver.InstanceForTest, Is.SameAs(receiver));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(1));
        }

        [Test]
        public void EnsureInstance_SecondCall_ReturnsSameInstanceAndIncrementsRefCount()
        {
            var first = VMCSharedReceiver.EnsureInstance();
            var second = VMCSharedReceiver.EnsureInstance();

            Assert.That(second, Is.SameAs(first));
            Assert.That(VMCSharedReceiver.InstanceForTest, Is.SameAs(first));
            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(2));
        }

        [Test]
        public void Release_DecrementsRefCountAndDestroysHostWhenCountReachesZero()
        {
            var receiver = VMCSharedReceiver.EnsureInstance();
            VMCSharedReceiver.EnsureInstance();
            var host = receiver.gameObject;

            receiver.Release();

            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.EqualTo(1));
            Assert.That(VMCSharedReceiver.InstanceForTest, Is.SameAs(receiver));
            Assert.That(host == null, Is.False);

            receiver.Release();

            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.Zero);
            Assert.That(VMCSharedReceiver.InstanceForTest, Is.Null);
            Assert.That(host == null, Is.True);
        }

        [Test]
        public void ResetForTest_ClearsSubsystemRegistrationStateAndDestroysExistingHost()
        {
            var receiver = VMCSharedReceiver.EnsureInstance();
            VMCSharedReceiver.EnsureInstance();
            var host = receiver.gameObject;

            VMCSharedReceiver.ResetForTest();

            Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.Zero);
            Assert.That(VMCSharedReceiver.InstanceForTest, Is.Null);
            Assert.That(host == null, Is.True);
        }

        [Test]
        public void SubsystemRegistrationReset_ClearsStaticInstanceAndRefCount()
        {
            var receiver = VMCSharedReceiver.EnsureInstance();
            VMCSharedReceiver.EnsureInstance();
            var host = receiver.gameObject;

            try
            {
                InvokeSubsystemRegistrationReset();

                Assert.That(VMCSharedReceiver.RefCountStaticForTest, Is.Zero);
                Assert.That(VMCSharedReceiver.InstanceForTest, Is.Null);
            }
            finally
            {
                if (host != null)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void ApplyReceiverSettings_WhenServerStartLeavesServerNotRunning_ThrowsInvalidOperationException()
        {
            var receiver = VMCSharedReceiver.EnsureInstance();

            using (var occupiedSocket = ReserveUdpPort(out var occupiedPort))
            {
                LogAssert.ignoreFailingMessages = true;

                var exception = Assert.Throws<InvalidOperationException>(
                    () => receiver.ApplyReceiverSettings(occupiedPort));

                Assert.That(exception, Is.Not.Null);
            }
        }

        private static Socket ReserveUdpPort(out int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                ExclusiveAddressUse = true
            };
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            port = ((IPEndPoint)socket.LocalEndPoint).Port;
            return socket;
        }

        private static void InvokeSubsystemRegistrationReset()
        {
            var method = typeof(VMCSharedReceiver).GetMethod(
                "ResetStaticsOnSubsystemRegistration",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);

            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));

            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
