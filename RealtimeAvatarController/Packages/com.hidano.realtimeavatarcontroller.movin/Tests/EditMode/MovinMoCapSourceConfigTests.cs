using System;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinMoCapSourceConfigTests
    {
        private MovinMoCapSourceConfig _config;
        private OtherMoCapSourceConfig _otherConfig;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
            _config.port = GetAvailableUdpPort();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            if (_otherConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_otherConfig);
                _otherConfig = null;
            }

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Initialize_WithMovinConfigAsBaseAndValidPort_Completes()
        {
            var source = new MovinMoCapSource();
            MoCapSourceConfigBase config = _config;

            try
            {
                Assert.DoesNotThrow(() => source.Initialize(config));
                Assert.That(source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Running));
            }
            finally
            {
                source.Shutdown();
            }
        }

        [Test]
        public void Initialize_WithPortBelowRange_ThrowsArgumentOutOfRangeException()
        {
            var source = new MovinMoCapSource();
            _config.port = 0;

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => source.Initialize(_config));

            Assert.That(ex.ParamName, Is.EqualTo("port"));
            Assert.That(source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Uninitialized));
        }

        [Test]
        public void Initialize_WithWrongConfigType_ThrowsArgumentException_WithTypeNameInMessage()
        {
            var source = new MovinMoCapSource();
            _otherConfig = ScriptableObject.CreateInstance<OtherMoCapSourceConfig>();

            var ex = Assert.Throws<ArgumentException>(() => source.Initialize(_otherConfig));

            Assert.That(ex.Message, Does.Contain(nameof(OtherMoCapSourceConfig)));
            Assert.That(source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Uninitialized));
        }

        private static int GetAvailableUdpPort()
        {
            using (var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                return ((IPEndPoint)client.Client.LocalEndPoint).Port;
            }
        }

        private sealed class OtherMoCapSourceConfig : MoCapSourceConfigBase
        {
        }
    }
}
