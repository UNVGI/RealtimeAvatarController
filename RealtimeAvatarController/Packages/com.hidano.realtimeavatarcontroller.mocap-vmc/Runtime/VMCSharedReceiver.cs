using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    internal interface IVmcMoCapAdapter
    {
        void Tick();

        void HandleTickException(Exception exception);
    }

    public sealed class VMCSharedReceiver : MonoBehaviour, IVmcBoneRotationWriter
    {
        private const string HostGameObjectName = "VMCSharedReceiver";

        private static VMCSharedReceiver s_instance;
        private static int s_refCount;

        private uOSC.uOscServer _server;
        private Dictionary<HumanBodyBones, Quaternion> _writeRotations = new(64);
        private Vector3 _writeRootPosition;
        private Quaternion _writeRootRotation = Quaternion.identity;
        private readonly HashSet<IVmcMoCapAdapter> _subscribers = new();
        private readonly List<IVmcMoCapAdapter> _tickSnapshot = new();

        public static VMCSharedReceiver EnsureInstance()
        {
            if (s_instance == null)
            {
                s_instance = CreateInstance();
            }

            s_refCount++;
            return s_instance;
        }

        public void Release()
        {
            if (s_refCount <= 0)
            {
                return;
            }

            s_refCount--;
            if (s_refCount > 0)
            {
                return;
            }

            var instance = s_instance;
            var host = instance != null ? instance.gameObject : null;
            s_instance = null;
            s_refCount = 0;

            if (instance != null)
            {
                instance.DisposeReceiverResources();
            }

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

        public void ApplyReceiverSettings(int port)
        {
            EnsureServer();

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
            if (s_instance != null)
            {
                var instance = s_instance;
                var host = instance.gameObject;
                s_instance = null;
                s_refCount = 0;
                instance.DisposeReceiverResources();

                if (host != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(host);
                    }
                    else
                    {
                        DestroyImmediate(host);
                    }
                }

                return;
            }

            s_refCount = 0;
        }

        internal static VMCSharedReceiver InstanceForTest => s_instance;

        internal static int RefCountStaticForTest => s_refCount;

        private static VMCSharedReceiver CreateInstance()
        {
            var go = new GameObject(HostGameObjectName);
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

            if (s_instance == this)
            {
                s_instance = null;
                s_refCount = 0;
            }
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
            s_instance = null;
            s_refCount = 0;
        }
    }
}
