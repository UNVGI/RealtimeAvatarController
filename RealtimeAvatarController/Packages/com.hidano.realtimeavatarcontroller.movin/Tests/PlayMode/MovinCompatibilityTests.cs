using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.MoCap.VMC;
using UniRx;
using uOSC;
using UnityEngine;
using CoreMotionFrame = RealtimeAvatarController.Core.MotionFrame;
using Object = UnityEngine.Object;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public sealed class MovinCompatibilityTests
    {
        private const string BonePoseAddress = "/VMC/Ext/Bone/Pos";
        private const string RootPoseAddress = "/VMC/Ext/Root/Pos";
        private const string RootBoneName = "MOVIN:Hips";
        private const string TargetBoneName = "MOVIN:Head";

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<IMoCapSource> _sources = new List<IMoCapSource>();
        private readonly List<ScriptableObject> _configs = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            DestroyMovinHosts();
            VMCSharedReceiver.ResetForTest();
            DestroyVmcHosts();
            RegistryLocator.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                _sources[i]?.Dispose();
            }
            _sources.Clear();

            DestroyMovinHosts();
            VMCSharedReceiver.ResetForTest();
            DestroyVmcHosts();

            foreach (var config in _configs)
            {
                if (config != null)
                {
                    Object.DestroyImmediate(config);
                }
            }
            _configs.Clear();

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void RegistryResolvesVmcAndMovin_WithDifferentPorts_AsIndependentSources()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;
            registry.Register(VMCMoCapSourceFactory.VmcSourceTypeId, new VMCMoCapSourceFactory());
            registry.Register(MovinMoCapSourceFactory.MovinSourceTypeId, new MovinMoCapSourceFactory());

            var vmcConfig = CreateVmcConfig(GetAvailableUdpPort());
            var movinConfig = CreateMovinConfig(GetAvailableUdpPort(vmcConfig.port));

            var vmcSource = registry.Resolve(new MoCapSourceDescriptor
            {
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = vmcConfig,
            });
            _sources.Add(vmcSource);

            var movinSource = registry.Resolve(new MoCapSourceDescriptor
            {
                SourceTypeId = MovinMoCapSourceFactory.MovinSourceTypeId,
                Config = movinConfig,
            });
            _sources.Add(movinSource);

            Assert.That(registry.GetRegisteredTypeIds(), Does.Contain(VMCMoCapSourceFactory.VmcSourceTypeId));
            Assert.That(registry.GetRegisteredTypeIds(), Does.Contain(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(vmcSource.SourceType, Is.EqualTo(VMCMoCapSourceFactory.VmcSourceTypeId));
            Assert.That(movinSource.SourceType, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(movinSource, Is.Not.SameAs(vmcSource));

            Assert.DoesNotThrow(() => vmcSource.Initialize(vmcConfig));
            Assert.DoesNotThrow(() => movinSource.Initialize(movinConfig));
        }

        [TestCase(8)]
        [TestCase(11)]
        [TestCase(14)]
        public void RootPoseOscMessage_WithSupportedArgumentCount_EmitsCompatibleSnapshot(int argumentCount)
        {
            var source = CreateInitializedMovinSource();
            var host = GetReceiverHost(source);
            var adapter = (IMovinReceiverAdapter)source;
            var received = new List<CoreMotionFrame>();
            Exception streamError = null;

            using var subscription = source.MotionStream.Subscribe(
                frame => received.Add(frame),
                ex => streamError = ex);

            var rootPosition = new Vector3(1f, 2f, 3f);
            var rootRotation = Quaternion.Euler(10f, 20f, 30f);
            var rootScale = new Vector3(1.1f, 1.2f, 1.3f);
            var rootOffset = new Vector3(0.4f, 0.5f, 0.6f);

            InvokeDispatchOscMessage(host, CreateRootPoseMessage(
                argumentCount,
                rootPosition,
                rootRotation,
                rootScale,
                rootOffset));
            InvokeDispatchOscMessage(host, new Message(
                BonePoseAddress,
                TargetBoneName,
                4f, 5f, 6f,
                0f, 0f, 0f, 1f));

            adapter.Tick();

            Assert.That(streamError, Is.Null);
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0], Is.TypeOf<MovinMotionFrame>());

            var frame = (MovinMotionFrame)received[0];
            Assert.That(frame.RootPose.HasValue, Is.True);

            var rootPose = frame.RootPose.Value;
            Assert.That(rootPose.BoneName, Is.EqualTo(RootBoneName));
            Assert.That(rootPose.LocalPosition, Is.EqualTo(rootPosition));
            Assert.That(Quaternion.Angle(rootRotation, rootPose.LocalRotation), Is.LessThan(1e-3f));

            if (argumentCount >= 11)
            {
                Assert.That(rootPose.LocalScale.HasValue, Is.True);
                Assert.That(rootPose.LocalScale.Value, Is.EqualTo(rootScale));
            }
            else
            {
                Assert.That(rootPose.LocalScale.HasValue, Is.False);
            }

            if (argumentCount == 14)
            {
                Assert.That(rootPose.LocalOffset.HasValue, Is.True);
                Assert.That(rootPose.LocalOffset.Value, Is.EqualTo(rootOffset));
            }
            else
            {
                Assert.That(rootPose.LocalOffset.HasValue, Is.False);
            }
        }

        [Test]
        public void Initialize_WithDuplicateMovinPort_PropagatesSocketException()
        {
            var port = GetAvailableUdpPort();
            var first = new MovinMoCapSource();
            _sources.Add(first);
            first.Initialize(CreateMovinConfig(port));

            var second = new MovinMoCapSource();
            _sources.Add(second);

            var exception = Assert.Throws<SocketException>(() => second.Initialize(CreateMovinConfig(port)));

            Assert.That(exception.SocketErrorCode, Is.EqualTo(SocketError.AddressAlreadyInUse));
            Assert.That(second.CurrentState, Is.EqualTo(MovinMoCapSource.State.Uninitialized));
        }

        private VMCMoCapSourceConfig CreateVmcConfig(int port)
        {
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            config.port = port;
            config.bindAddress = "0.0.0.0";
            _configs.Add(config);
            return config;
        }

        private MovinMoCapSourceConfig CreateMovinConfig(int port)
        {
            var config = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
            config.port = port;
            _configs.Add(config);
            return config;
        }

        private MovinMoCapSource CreateInitializedMovinSource()
        {
            var source = new MovinMoCapSource();
            _sources.Add(source);
            source.Initialize(CreateMovinConfig(GetAvailableUdpPort()));
            return source;
        }

        private static Message CreateRootPoseMessage(
            int argumentCount,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Vector3 offset)
        {
            var values = new List<object>
            {
                RootBoneName,
                position.x,
                position.y,
                position.z,
                rotation.x,
                rotation.y,
                rotation.z,
                rotation.w,
            };

            if (argumentCount >= 11)
            {
                values.Add(scale.x);
                values.Add(scale.y);
                values.Add(scale.z);
            }

            if (argumentCount == 14)
            {
                values.Add(offset.x);
                values.Add(offset.y);
                values.Add(offset.z);
            }

            Assert.That(values, Has.Count.EqualTo(argumentCount));
            return new Message(RootPoseAddress, values.ToArray());
        }

        private static MovinOscReceiverHost GetReceiverHost(MovinMoCapSource source)
        {
            var field = typeof(MovinMoCapSource).GetField("_receiverHost", PrivateInstance);
            Assert.That(field, Is.Not.Null);

            var host = field.GetValue(source) as MovinOscReceiverHost;
            Assert.That(host, Is.Not.Null);
            return host;
        }

        private static void InvokeDispatchOscMessage(MovinOscReceiverHost host, Message message)
        {
            var method = typeof(MovinOscReceiverHost).GetMethod("DispatchOscMessage", PrivateInstance);
            Assert.That(method, Is.Not.Null);

            try
            {
                method.Invoke(host, new object[] { message });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static int GetAvailableUdpPort(params int[] excludedPorts)
        {
            var excluded = new HashSet<int>(excludedPorts ?? Array.Empty<int>());
            for (var i = 0; i < 32; i++)
            {
                using var socket = new UdpClient(0);
                var port = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
                if (!excluded.Contains(port))
                {
                    return port;
                }
            }

            throw new InvalidOperationException("Could not allocate a distinct UDP port for the MOVIN compatibility test.");
        }

        private static void DestroyMovinHosts()
        {
            var hosts = Object.FindObjectsByType<MovinOscReceiverHost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host != null)
                {
                    Object.DestroyImmediate(host.gameObject);
                }
            }
        }

        private static void DestroyVmcHosts()
        {
            var hosts = Object.FindObjectsByType<VMCSharedReceiver>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host != null)
                {
                    Object.DestroyImmediate(host.gameObject);
                }
            }
        }
    }
}
