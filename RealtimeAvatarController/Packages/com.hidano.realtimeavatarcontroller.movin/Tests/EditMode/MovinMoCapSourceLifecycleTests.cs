using System;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinMoCapSourceLifecycleTests
    {
        private MovinMoCapSourceConfig _config;
        private MovinMoCapSource _source;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _config = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
            _config.port = GetAvailableUdpPort();
            _source = new MovinMoCapSource();
        }

        [TearDown]
        public void TearDown()
        {
            if (_source != null)
            {
                _source.Shutdown();
                _source = null;
            }

            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Initialize_CalledTwice_ThrowsInvalidOperationException()
        {
            _source.Initialize(_config);

            var ex = Assert.Throws<InvalidOperationException>(() => _source.Initialize(_config));

            Assert.That(ex.Message, Does.Contain("Running"));
            Assert.That(_source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Running));
        }

        [Test]
        public void Shutdown_CalledMultipleTimes_IsIdempotentAndLeavesDisposed()
        {
            _source.Initialize(_config);

            Assert.DoesNotThrow(() => _source.Shutdown());
            Assert.DoesNotThrow(() => _source.Shutdown());

            Assert.That(_source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Disposed));
        }

        [Test]
        public void Dispose_CalledMultipleTimes_IsIdempotentAndLeavesDisposed()
        {
            _source.Initialize(_config);

            Assert.DoesNotThrow(() => _source.Dispose());
            Assert.DoesNotThrow(() => _source.Dispose());

            Assert.That(_source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Disposed));
        }

        [Test]
        public void Initialize_AfterShutdown_ThrowsInvalidOperationException()
        {
            _source.Initialize(_config);
            _source.Shutdown();

            var ex = Assert.Throws<InvalidOperationException>(() => _source.Initialize(_config));

            Assert.That(ex.Message, Does.Contain("Disposed"));
            Assert.That(_source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Disposed));
        }

        [Test]
        public void Initialize_AfterDispose_ThrowsInvalidOperationException()
        {
            _source.Initialize(_config);
            _source.Dispose();

            var ex = Assert.Throws<InvalidOperationException>(() => _source.Initialize(_config));

            Assert.That(ex.Message, Does.Contain("Disposed"));
            Assert.That(_source.CurrentState, Is.EqualTo(MovinMoCapSource.State.Disposed));
        }

        private static int GetAvailableUdpPort()
        {
            using (var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                return ((IPEndPoint)client.Client.LocalEndPoint).Port;
            }
        }
    }
}
