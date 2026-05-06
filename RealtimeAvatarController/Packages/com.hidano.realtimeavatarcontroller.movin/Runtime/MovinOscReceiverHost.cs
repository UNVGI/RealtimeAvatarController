using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using uOSC;

namespace RealtimeAvatarController.MoCap.Movin
{
    internal interface IMovinReceiverAdapter
    {
        void HandleBonePose(string boneName, Vector3 localPos, Quaternion localRot);

        void HandleRootPose(
            string boneName,
            Vector3 localPos,
            Quaternion localRot,
            Vector3? localScale,
            Vector3? localOffset);

        void Tick();

        void HandleTickException(Exception exception);
    }

    internal sealed class MovinOscReceiverHost : MonoBehaviour
    {
        private const string HostGameObjectName = "[MOVIN OSC Receiver Host]";
        private const string BonePoseAddress = "/VMC/Ext/Bone/Pos";
        private const string RootPoseAddress = "/VMC/Ext/Root/Pos";
        private const int PoseArgumentCount = 8;
        private const int RootPoseWithScaleArgumentCount = 11;
        private const int RootPoseWithScaleAndOffsetArgumentCount = 14;

        private static MovinOscReceiverHost s_lastCreatedHost;
        private static readonly HashSet<int> s_activePorts = new HashSet<int>();

        private uOscServer _server;
        private IMovinReceiverAdapter _adapter;
        private bool _isShutdown;
        private int _boundPort;

        public static MovinOscReceiverHost Create(IMovinReceiverAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            var go = new GameObject(HostGameObjectName);
            go.SetActive(false);

            var server = go.AddComponent<uOscServer>();
            server.autoStart = false;

            var host = go.AddComponent<MovinOscReceiverHost>();
            host._server = server;
            host._adapter = adapter;
            server.onDataReceived.AddListener(host.HandleOscMessage);

            s_lastCreatedHost = host;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.SetActive(true);
            return host;
        }

        public void ApplyReceiverSettings(int port)
        {
            if (_server == null)
            {
                return;
            }

            if (_boundPort == port && _server.isRunning)
            {
                return;
            }

            ReleasePortReservation();
            _server.StopServer();
            ThrowIfPortUnavailable(port);
            _server.port = port;
            _server.StartServer();

            if (!_server.isRunning)
            {
                throw new SocketException((int)SocketError.AddressAlreadyInUse);
            }

            _boundPort = port;
            s_activePorts.Add(port);
        }

        public void Shutdown()
        {
            if (_isShutdown)
            {
                return;
            }

            _isShutdown = true;
            DetachServer();

            if (s_lastCreatedHost == this)
            {
                s_lastCreatedHost = null;
            }

            var host = gameObject;
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

        private void LateUpdate()
        {
            if (_isShutdown || _adapter == null)
            {
                return;
            }

            try
            {
                _adapter.Tick();
            }
            catch (Exception ex)
            {
                NotifyAdapterException(ex);
            }
        }

        private void OnDestroy()
        {
            _isShutdown = true;
            DetachServer();

            if (s_lastCreatedHost == this)
            {
                s_lastCreatedHost = null;
            }
        }

        private void DetachServer()
        {
            ReleasePortReservation();

            if (_server != null)
            {
                _server.onDataReceived.RemoveListener(HandleOscMessage);
                _server.StopServer();
                _server = null;
            }

            _adapter = null;
        }

        private void ReleasePortReservation()
        {
            if (_boundPort <= 0)
            {
                return;
            }

            s_activePorts.Remove(_boundPort);
            _boundPort = 0;
        }

        private static void ThrowIfPortUnavailable(int port)
        {
            if (s_activePorts.Contains(port) || IsUdpPortActive(port))
            {
                throw new SocketException((int)SocketError.AddressAlreadyInUse);
            }
        }

        private static bool IsUdpPortActive(int port)
        {
            try
            {
                var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
                for (var i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i].Port == port)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void HandleOscMessage(Message message)
        {
            if (_isShutdown || _adapter == null)
            {
                return;
            }

            try
            {
                DispatchOscMessage(message);
            }
            catch (Exception ex)
            {
                NotifyAdapterException(ex);
            }
        }

        private void DispatchOscMessage(Message message)
        {
            if (message.address == BonePoseAddress)
            {
                if (TryReadBonePose(message.values, out var boneName, out var localPos, out var localRot))
                {
                    _adapter.HandleBonePose(boneName, localPos, localRot);
                }
                return;
            }

            if (message.address == RootPoseAddress &&
                TryReadRootPose(
                    message.values,
                    out var rootBoneName,
                    out var rootLocalPos,
                    out var rootLocalRot,
                    out var rootLocalScale,
                    out var rootLocalOffset))
            {
                _adapter.HandleRootPose(
                    rootBoneName,
                    rootLocalPos,
                    rootLocalRot,
                    rootLocalScale,
                    rootLocalOffset);
            }
        }

        private void NotifyAdapterException(Exception exception)
        {
            if (exception == null || _adapter == null)
            {
                return;
            }

            try
            {
                _adapter.HandleTickException(exception);
            }
            catch
            {
                // Keep uOSC dispatch and LateUpdate alive even if error publication fails.
            }
        }

        private static bool TryReadBonePose(
            object[] values,
            out string boneName,
            out Vector3 localPos,
            out Quaternion localRot)
        {
            boneName = null;
            localPos = default;
            localRot = default;

            return values != null &&
                   values.Length == PoseArgumentCount &&
                   TryReadString(values, 0, out boneName) &&
                   !string.IsNullOrEmpty(boneName) &&
                   TryReadVector3(values, 1, out localPos) &&
                   TryReadQuaternion(values, 4, out localRot);
        }

        private static bool TryReadRootPose(
            object[] values,
            out string boneName,
            out Vector3 localPos,
            out Quaternion localRot,
            out Vector3? localScale,
            out Vector3? localOffset)
        {
            boneName = null;
            localPos = default;
            localRot = default;
            localScale = null;
            localOffset = null;

            if (values == null ||
                (values.Length != PoseArgumentCount &&
                 values.Length != RootPoseWithScaleArgumentCount &&
                 values.Length != RootPoseWithScaleAndOffsetArgumentCount) ||
                !TryReadString(values, 0, out boneName) ||
                string.IsNullOrEmpty(boneName) ||
                !TryReadVector3(values, 1, out localPos) ||
                !TryReadQuaternion(values, 4, out localRot))
            {
                return false;
            }

            if (values.Length >= RootPoseWithScaleArgumentCount)
            {
                if (!TryReadVector3(values, 8, out var scale))
                {
                    return false;
                }
                localScale = scale;
            }

            if (values.Length == RootPoseWithScaleAndOffsetArgumentCount)
            {
                if (!TryReadVector3(values, 11, out var offset))
                {
                    return false;
                }
                localOffset = offset;
            }

            return true;
        }

        private static bool TryReadString(object[] values, int index, out string result)
        {
            if (values[index] is string value)
            {
                result = value;
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryReadVector3(object[] values, int startIndex, out Vector3 result)
        {
            result = default;
            if (!TryReadFloat(values, startIndex, out var x) ||
                !TryReadFloat(values, startIndex + 1, out var y) ||
                !TryReadFloat(values, startIndex + 2, out var z))
            {
                return false;
            }

            result = new Vector3(x, y, z);
            return true;
        }

        private static bool TryReadQuaternion(object[] values, int startIndex, out Quaternion result)
        {
            result = default;
            if (!TryReadFloat(values, startIndex, out var x) ||
                !TryReadFloat(values, startIndex + 1, out var y) ||
                !TryReadFloat(values, startIndex + 2, out var z) ||
                !TryReadFloat(values, startIndex + 3, out var w))
            {
                return false;
            }

            result = new Quaternion(x, y, z, w);
            return true;
        }

        private static bool TryReadFloat(object[] values, int index, out float result)
        {
            if (values[index] is float value)
            {
                result = value;
                return true;
            }

            result = default;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            s_lastCreatedHost = null;
            s_activePorts.Clear();
        }
    }
}
