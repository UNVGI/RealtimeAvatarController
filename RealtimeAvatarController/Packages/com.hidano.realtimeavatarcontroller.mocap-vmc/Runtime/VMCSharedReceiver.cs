using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    public interface IVmcMoCapAdapter
    {
        void Tick();

        void HandleTickException(Exception exception);
    }

    public sealed class VMCSharedReceiver : MonoBehaviour, IVmcBoneRotationWriter
    {
        private const string HostGameObjectName = "VMCSharedReceiver";

        private static readonly Dictionary<VMCMoCapSourceConfig, ReceiverEntry> s_configEntries =
            new Dictionary<VMCMoCapSourceConfig, ReceiverEntry>(ConfigReferenceComparer.Instance);

        private static VMCSharedReceiver s_defaultInstance;
        private static int s_defaultRefCount;

        private static VMCSharedReceiver s_instance;
        private static int s_refCount;

        private uOSC.uOscServer _server;
        private Dictionary<HumanBodyBones, Quaternion> _writeRotations = new Dictionary<HumanBodyBones, Quaternion>(64);
        private Vector3 _writeRootPosition;
        private Quaternion _writeRootRotation = Quaternion.identity;
        private VMCMoCapSourceConfig _configKey;
        private int _localRefCount;
        // MainThread only: Subscribe/Unsubscribe and Update-driven Tick iteration run on Unity's
        // main thread, so this HashSet does not require locking or thread-safe add/remove.
        private readonly HashSet<IVmcMoCapAdapter> _subscribers = new HashSet<IVmcMoCapAdapter>();
        private readonly List<IVmcMoCapAdapter> _tickSnapshot = new List<IVmcMoCapAdapter>();

        public static VMCSharedReceiver EnsureInstance()
        {
            if (s_defaultInstance == null)
            {
                s_defaultInstance = CreateInstance(null);
            }

            s_defaultRefCount++;
            s_defaultInstance._localRefCount = s_defaultRefCount;
            RefreshTestStatics();
            return s_defaultInstance;
        }

        internal static VMCSharedReceiver EnsureInstance(VMCMoCapSourceConfig config)
        {
            if (config == null)
            {
                return EnsureInstance();
            }

            if (s_configEntries.TryGetValue(config, out var entry))
            {
                entry.RefCount++;
                entry.Receiver._localRefCount = entry.RefCount;
                s_configEntries[config] = entry;
                RefreshTestStatics();
                return entry.Receiver;
            }

            var receiver = CreateInstance(config);
            receiver._configKey = config;
            receiver._localRefCount = 1;
            s_configEntries.Add(config, new ReceiverEntry { Receiver = receiver, RefCount = 1 });
            RefreshTestStatics();
            return receiver;
        }

        public void Release()
        {
            if (!ReferenceEquals(_configKey, null))
            {
                ReleaseConfigInstance();
                return;
            }

            ReleaseDefaultInstance();
        }

        public void ApplyReceiverSettings(int port)
        {
            EnsureServer();

            if (_server.isRunning && _server.port == port)
            {
                return;
            }

            _server.StopServer();
            _server.autoStart = false;
            _server.port = port;
            _server.StartServer();

            if (!_server.isRunning)
            {
                throw new InvalidOperationException("VMC OSC port bind failed: " + port);
            }
        }

        public void WriteBoneRotation(HumanBodyBones bone, Quaternion rotation)
        {
            _writeRotations[bone] = rotation;
        }

        public void WriteRoot(Vector3 position, Quaternion rotation)
        {
            _writeRootPosition = position;
            _writeRootRotation = rotation;
        }

        internal void Subscribe(IVmcMoCapAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            _subscribers.Add(adapter);
        }

        internal void Unsubscribe(IVmcMoCapAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            _subscribers.Remove(adapter);
        }

        internal Dictionary<HumanBodyBones, Quaternion> ReadAndClearWriteBuffer(
            out Vector3 rootPosition,
            out Quaternion rootRotation)
        {
            rootPosition = _writeRootPosition;
            rootRotation = _writeRootRotation;

            var snapshot = _writeRotations;
            _writeRotations = new Dictionary<HumanBodyBones, Quaternion>(64);
            return snapshot;
        }

        internal static void ResetForTest()
        {
            var receivers = new List<VMCSharedReceiver>();
            if (s_defaultInstance != null)
            {
                receivers.Add(s_defaultInstance);
            }

            foreach (var entry in s_configEntries.Values)
            {
                if (entry.Receiver != null && !receivers.Contains(entry.Receiver))
                {
                    receivers.Add(entry.Receiver);
                }
            }

            s_defaultInstance = null;
            s_defaultRefCount = 0;
            s_configEntries.Clear();
            s_instance = null;
            s_refCount = 0;

            for (var i = 0; i < receivers.Count; i++)
            {
                DestroyReceiverHost(receivers[i]);
            }
        }

        internal static VMCSharedReceiver InstanceForTest => s_instance;

        internal static int RefCountStaticForTest => s_refCount;

        internal static int InstanceCountForTest
        {
            get
            {
                var count = s_defaultInstance != null ? 1 : 0;
                count += s_configEntries.Count;
                return count;
            }
        }

        internal int RefCountForTest => _localRefCount;

        private static VMCSharedReceiver CreateInstance(VMCMoCapSourceConfig config)
        {
            var go = new GameObject(CreateHostName(config));
            go.SetActive(false);

            var receiver = go.AddComponent<VMCSharedReceiver>();
            var server = go.AddComponent<uOSC.uOscServer>();
            server.autoStart = false;
            receiver.Initialize(server);

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.SetActive(true);
            return receiver;
        }

        private static string CreateHostName(VMCMoCapSourceConfig config)
        {
            return config == null ? HostGameObjectName : HostGameObjectName + ":" + config.port;
        }

        private void Initialize(uOSC.uOscServer server)
        {
            _server = server;
            _server.autoStart = false;
            _server.onDataReceived.RemoveListener(OnOscMessage);
            _server.onDataReceived.AddListener(OnOscMessage);
        }

        private void EnsureServer()
        {
            if (_server == null)
            {
                _server = GetComponent<uOSC.uOscServer>();
                if (_server == null)
                {
                    _server = gameObject.AddComponent<uOSC.uOscServer>();
                }
            }

            _server.autoStart = false;
            _server.onDataReceived.RemoveListener(OnOscMessage);
            _server.onDataReceived.AddListener(OnOscMessage);
        }

        private void ReleaseConfigInstance()
        {
            if (!s_configEntries.TryGetValue(_configKey, out var entry) || !ReferenceEquals(entry.Receiver, this))
            {
                return;
            }

            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                _localRefCount = entry.RefCount;
                s_configEntries[_configKey] = entry;
                RefreshTestStatics();
                return;
            }

            s_configEntries.Remove(_configKey);
            _localRefCount = 0;
            RefreshTestStatics();
            DestroyReceiverHost(this);
        }

        private void ReleaseDefaultInstance()
        {
            if (!ReferenceEquals(s_defaultInstance, this) || s_defaultRefCount <= 0)
            {
                return;
            }

            s_defaultRefCount--;
            if (s_defaultRefCount > 0)
            {
                _localRefCount = s_defaultRefCount;
                RefreshTestStatics();
                return;
            }

            s_defaultInstance = null;
            _localRefCount = 0;
            RefreshTestStatics();
            DestroyReceiverHost(this);
        }

        private static void DestroyReceiverHost(VMCSharedReceiver receiver)
        {
            if (receiver == null)
            {
                return;
            }

            var host = receiver.gameObject;
            receiver._configKey = null;
            receiver._localRefCount = 0;
            receiver.DisposeReceiverResources();

            if (host == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(host);
            }
            else
            {
                DestroyImmediate(host);
            }
        }

        private static void RefreshTestStatics()
        {
            var totalRefCount = s_defaultRefCount;
            VMCSharedReceiver first = s_defaultInstance;

            foreach (var entry in s_configEntries.Values)
            {
                totalRefCount += entry.RefCount;
                if (first == null)
                {
                    first = entry.Receiver;
                }
            }

            s_refCount = totalRefCount;
            s_instance = first;
        }

        private void OnOscMessage(uOSC.Message message)
        {
            VmcOscMessageRouter.RouteMessage(in message, this);
        }

        private void Update()
        {
            if (_subscribers.Count == 0)
            {
                return;
            }

            _tickSnapshot.Clear();
            foreach (var subscriber in _subscribers)
            {
                _tickSnapshot.Add(subscriber);
            }

            for (var i = 0; i < _tickSnapshot.Count; i++)
            {
                var adapter = _tickSnapshot[i];
                if (adapter == null)
                {
                    continue;
                }

                try
                {
                    adapter.Tick();
                }
                catch (Exception ex)
                {
                    try
                    {
                        adapter.HandleTickException(ex);
                    }
                    catch
                    {
                    }
                }
            }

            _tickSnapshot.Clear();
        }

        private void OnDestroy()
        {
            DisposeReceiverResources();

            if (ReferenceEquals(s_defaultInstance, this))
            {
                s_defaultInstance = null;
                s_defaultRefCount = 0;
            }

            if (!ReferenceEquals(_configKey, null)
                && s_configEntries.TryGetValue(_configKey, out var entry)
                && ReferenceEquals(entry.Receiver, this))
            {
                s_configEntries.Remove(_configKey);
            }

            RefreshTestStatics();
        }

        private void DisposeReceiverResources()
        {
            if (_server != null)
            {
                _server.onDataReceived.RemoveListener(OnOscMessage);
                _server.StopServer();
            }

            _subscribers.Clear();
            _tickSnapshot.Clear();
            _writeRotations.Clear();
            _writeRootPosition = default;
            _writeRootRotation = Quaternion.identity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            s_defaultInstance = null;
            s_defaultRefCount = 0;
            s_configEntries.Clear();
            s_instance = null;
            s_refCount = 0;
        }

        private struct ReceiverEntry
        {
            public VMCSharedReceiver Receiver;
            public int RefCount;
        }

        private sealed class ConfigReferenceComparer : IEqualityComparer<VMCMoCapSourceConfig>
        {
            public static readonly ConfigReferenceComparer Instance = new ConfigReferenceComparer();

            public bool Equals(VMCMoCapSourceConfig x, VMCMoCapSourceConfig y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(VMCMoCapSourceConfig obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
