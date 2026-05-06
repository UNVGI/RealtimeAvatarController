using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Minimal OSC + VMC Protocol UDP receiver for Unity (no external packages).
/// - Listens on UDP (default 11235) and parses OSC messages/bundles.
/// - Dispatches key VMC addresses like /VMC/Ext/Bone/Pos, /VMC/Ext/Root/Pos, /VMC/Ext/Blend/Val, /VMC/Ext/Blend/Apply, /VMC/Ext/T, devices, camera, etc.
/// - Thread-safe: network thread enqueues parsed messages; Unity main thread processes them in Update().
/// 
/// References (VMC spec uses OSC over UDP):
/// https://protocol.vmc.info/english.html
/// </summary>
public class VMCReceiver : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("UDP port to listen on.")]
    public int listenPort = 11235;

    [Tooltip("Optional: bind to a specific local IP (blank for Any).")]
    public string bindAddress = "";

    [Tooltip("Log incoming OSC addresses for debugging.")]
    public bool verboseLogging = false;

    [Header("Coordinate Conversion")]
    [Tooltip("If your avatar/world uses a right-handed coordinate system, you may need to adapt here. VMC and Unity are both left-handed (Y up), so typically no change.")]
    public bool passthroughUnityCoordinates = true;

    private Thread _thread;
    private UdpClient _udp;
    private IPEndPoint _remoteAny;
    private volatile bool _running;

    // Message queue processed on main thread
    private readonly ConcurrentQueue<OSCMessage> _queue = new ConcurrentQueue<OSCMessage>();

    // --- Events you can subscribe to ---
    public event Action<int, int, int, int> OnOk; // loaded, calibState, calibMode, trackingStatus (some are optional per version)
    public event Action<float> OnTime;
    public event Action<string, Vector3, Quaternion, Vector3?, Vector3?> OnRootPose; // name, pos, rot, (opt)scale, (opt)offset
    public event Action<string, Vector3, Quaternion> OnBonePose; // HumanBodyBones name
    public event Action<string, float> OnBlendShapeValue; // name, value
    public event Action OnBlendShapeApply;
    public event Action<string, Vector3, Quaternion, float> OnCamera; // name, pos, rot, fov
    public event Action<string, Vector3, Quaternion> OnHmdPos;
    public event Action<string, Vector3, Quaternion> OnControllerPos;
    public event Action<string, Vector3, Quaternion> OnTrackerPos;

    // Optional: public getters for last-known states
    public readonly Dictionary<string, (Vector3 pos, Quaternion rot)> BonePoses = new();
    public readonly Dictionary<string, float> BlendshapeValues = new();

    protected virtual void OnEnable()
    {
        StartReceiver();
    }

    protected virtual void OnDisable()
    {
        StopReceiver();
    }

    public void StartReceiver()
    {
        if (_running) return;
        try
        {
            _remoteAny = new IPEndPoint(IPAddress.Any, 0);
            var local = string.IsNullOrWhiteSpace(bindAddress) ? IPAddress.Any : IPAddress.Parse(bindAddress);
            _udp = new UdpClient(new IPEndPoint(local, listenPort));
            _udp.Client.ReceiveBufferSize = 1 << 20; // 1MB
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "VMCReceiver" };
            _thread.Start();
            Debug.Log($"VMCReceiver listening on {local}:{listenPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"VMCReceiver failed to start: {ex}");
            _running = false;
            _udp?.Close();
            _udp = null;
        }
    }

    public void StopReceiver()
    {
        _running = false;
        try { _udp?.Close(); } catch { /* ignore */ }
        _udp = null;
        try { _thread?.Join(100); } catch { /* ignore */ }
        _thread = null;
    }

    private void ReceiveLoop()
    {
        while (_running)
        {
            try
            {
                var data = _udp.Receive(ref _remoteAny);
                OSCParser.ParsePacket(data, 0, data.Length, (msg) =>
                {
                    _queue.Enqueue(msg);
                });
            }
            catch (SocketException)
            {
                // likely closing; ignore
                if (!_running) break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VMCReceiver receive error: {ex.Message}");
            }
        }
    }

    protected virtual void Update()
    {
        int safety = 10000; // process up to N msgs per frame to avoid stalls
        while (safety-- > 0 && _queue.TryDequeue(out var msg))
        {
            try { DispatchVMC(msg); }
            catch (Exception ex) { Debug.LogWarning($"Dispatch error for {msg.Address}: {ex.Message}"); }
        }
    }

    private void DispatchVMC(OSCMessage msg)
    {
        if (verboseLogging)
        {
            Debug.Log($"OSC {msg.Address} {msg.Types} [{string.Join(", ", msg.Args)}]");
        }

        switch (msg.Address)
        {
            case "/VMC/Ext/OK":
                // V2.0: (int loaded)
                // V2.5: (int loaded) (int calibState) (int calibMode)
                // V2.7: (int loaded) (int calibState) (int calibMode) (int trackingStatus)
                var ok = OSCArgReader.Ints(msg);
                int loaded = ok.Length > 0 ? ok[0] : 0;
                int calibState = ok.Length > 1 ? ok[1] : -1;
                int calibMode = ok.Length > 2 ? ok[2] : -1;
                int tracking = ok.Length > 3 ? ok[3] : -1;
                OnOk?.Invoke(loaded, calibState, calibMode, tracking);
                break;

            case "/VMC/Ext/T":
                if (msg.Args.Length >= 1 && msg.Args[0] is float t) OnTime?.Invoke(t);
                break;

            case "/VMC/Ext/Root/Pos":
                // v2.0: name, pos(xyz), rot(xyzw)
                // v2.1: + scale(xyz), offset(xyz)
                if (msg.Args.Length >= 8 && msg.Args[0] is string rootName)
                {
                    var p = new Vector3((float)msg.Args[1], (float)msg.Args[2], (float)msg.Args[3]);
                    var q = new Quaternion((float)msg.Args[4], (float)msg.Args[5], (float)msg.Args[6], (float)msg.Args[7]);
                    Vector3? s = null, o = null;
                    if (msg.Args.Length >= 11)
                        s = new Vector3((float)msg.Args[8], (float)msg.Args[9], (float)msg.Args[10]);
                    if (msg.Args.Length >= 14)
                        o = new Vector3((float)msg.Args[11], (float)msg.Args[12], (float)msg.Args[13]);
                    if (passthroughUnityCoordinates)
                    {
                        OnRootPose?.Invoke(rootName, p, q, s, o);
                    }
                    else
                    {
                        OnRootPose?.Invoke(rootName, ConvertCoords(p), ConvertRot(q), s, o);
                    }
                }
                break;

            case "/VMC/Ext/Bone/Pos":
                if (msg.Args.Length >= 8 && msg.Args[0] is string boneName)
                {
                    var p = new Vector3((float)msg.Args[1], (float)msg.Args[2], (float)msg.Args[3]);
                    var q = new Quaternion((float)msg.Args[4], (float)msg.Args[5], (float)msg.Args[6], (float)msg.Args[7]);
                    if (!passthroughUnityCoordinates)
                    {
                        p = ConvertCoords(p);
                        q = ConvertRot(q);
                    }
                    BonePoses[boneName] = (p, q);
                    OnBonePose?.Invoke(boneName, p, q);
                }
                break;

            case "/VMC/Ext/Blend/Val":
                if (msg.Args.Length >= 2 && msg.Args[0] is string bsName && msg.Args[1] is float val)
                {
                    BlendshapeValues[bsName] = val;
                    OnBlendShapeValue?.Invoke(bsName, val);
                }
                break;

            case "/VMC/Ext/Blend/Apply":
                OnBlendShapeApply?.Invoke();
                break;

            case "/VMC/Ext/Cam":
                if (msg.Args.Length >= 9 && msg.Args[0] is string camName)
                {
                    var p = new Vector3((float)msg.Args[1], (float)msg.Args[2], (float)msg.Args[3]);
                    var q = new Quaternion((float)msg.Args[4], (float)msg.Args[5], (float)msg.Args[6], (float)msg.Args[7]);
                    float fov = (float)msg.Args[8];
                    if (!passthroughUnityCoordinates) { p = ConvertCoords(p); q = ConvertRot(q); }
                    OnCamera?.Invoke(camName, p, q, fov);
                }
                break;

            case "/VMC/Ext/Hmd/Pos":
            case "/VMC/Ext/Hmd/Pos/Local":
                DispatchDevice(msg, OnHmdPos);
                break;
            case "/VMC/Ext/Con/Pos":
            case "/VMC/Ext/Con/Pos/Local":
                DispatchDevice(msg, OnControllerPos);
                break;
            case "/VMC/Ext/Tra/Pos":
            case "/VMC/Ext/Tra/Pos/Local":
                DispatchDevice(msg, OnTrackerPos);
                break;

            default:
                // ignore other addresses per spec
                break;
        }
    }

    private void DispatchDevice(OSCMessage msg, Action<string, Vector3, Quaternion> cb)
    {
        if (msg.Args.Length >= 8 && msg.Args[0] is string serial)
        {
            var p = new Vector3((float)msg.Args[1], (float)msg.Args[2], (float)msg.Args[3]);
            var q = new Quaternion((float)msg.Args[4], (float)msg.Args[5], (float)msg.Args[6], (float)msg.Args[7]);
            if (!passthroughUnityCoordinates) { p = ConvertCoords(p); q = ConvertRot(q); }
            cb?.Invoke(serial, p, q);
        }
    }

    private static Vector3 ConvertCoords(Vector3 v)
    {
        // Placeholder: if you need to convert between coordinate systems, do it here.
        return v;
    }

    private static Quaternion ConvertRot(Quaternion q)
    {
        // Placeholder for rotation conversion if needed.
        return q;
    }
}

#region OSC Low-Level

public class OSCMessage
{
    public string Address;
    public string Types; // e.g. ",sff"
    public object[] Args;
}

public static class OSCParser
{
    public static void ParsePacket(byte[] data, int offset, int length, Action<OSCMessage> onMessage)
    {
        // Bundle or message?
        if (IsBundle(data, offset))
        {
            ParseBundle(data, offset, length, onMessage);
        }
        else
        {
            var msg = ParseMessage(data, offset, length);
            if (msg != null) onMessage(msg);
        }
    }

    private static bool IsBundle(byte[] data, int offset)
    {
        if (data.Length - offset < 8) return false;
        // "#bundle" with trailing null and padding to 8 bytes
        return data[offset] == (byte)'#' && data[offset + 1] == (byte)'b';
    }

    private static void ParseBundle(byte[] data, int offset, int length, Action<OSCMessage> onMessage)
    {
        int idx = offset;
        string tag = ReadPaddedString(data, ref idx); // "#bundle"
        if (tag != "#bundle") return;
        idx += 8; // timetag (NTP 64-bit), skip
        while (idx < offset + length)
        {
            if (idx + 4 > data.Length) break;
            int elemSize = ReadIntBE(data, ref idx);
            if (elemSize <= 0 || idx + elemSize > data.Length) break;
            ParsePacket(data, idx, elemSize, onMessage);
            idx += elemSize;
        }
    }

    private static OSCMessage ParseMessage(byte[] data, int offset, int length)
    {
        int idx = offset;
        string address = ReadPaddedString(data, ref idx);
        if (string.IsNullOrEmpty(address)) return null;
        if (idx >= offset + length) return null;
        string types = ReadPaddedString(data, ref idx); // starts with ','
        if (string.IsNullOrEmpty(types) || types[0] != ',') return null;

        var args = new List<object>();
        for (int i = 1; i < types.Length; i++)
        {
            char t = types[i];
            switch (t)
            {
                case 'i': args.Add(ReadIntBE(data, ref idx)); break;
                case 'f': args.Add(ReadFloatBE(data, ref idx)); break;
                case 's': args.Add(ReadPaddedString(data, ref idx)); break;
                case 'b': args.Add(ReadBlob(data, ref idx)); break;
                // extend as needed (e.g., 'h', 'd', 'T', 'F')
                default:
                    // Skip unsupported type safely if possible
                    Debug.LogWarning($"Unsupported OSC arg type '{t}' in {address}");
                    return new OSCMessage { Address = address, Types = types, Args = args.ToArray() };
            }
        }

        return new OSCMessage { Address = address, Types = types, Args = args.ToArray() };
    }

    private static string ReadPaddedString(byte[] data, ref int idx)
    {
        int start = idx;
        while (idx < data.Length && data[idx] != 0) idx++;
        var str = Encoding.UTF8.GetString(data, start, idx - start);
        // skip null and pad to 4-byte boundary
        idx++;
        while ((idx - start) % 4 != 0) idx++;
        return str;
    }

    private static byte[] ReadBlob(byte[] data, ref int idx)
    {
        int len = ReadIntBE(data, ref idx);
        var blob = new byte[len];
        Buffer.BlockCopy(data, idx, blob, 0, len);
        idx += len;
        while (idx % 4 != 0) idx++; // padding
        return blob;
    }

    private static int ReadIntBE(byte[] data, ref int idx)
    {
        int v = (data[idx] << 24) | (data[idx + 1] << 16) | (data[idx + 2] << 8) | data[idx + 3];
        idx += 4;
        return v;
    }

    private static float ReadFloatBE(byte[] data, ref int idx)
    {
        if (BitConverter.IsLittleEndian)
        {
            // copy and reverse for big-endian network order
            byte[] tmp = new byte[4];
            tmp[0] = data[idx + 3];
            tmp[1] = data[idx + 2];
            tmp[2] = data[idx + 1];
            tmp[3] = data[idx + 0];
            idx += 4;
            return BitConverter.ToSingle(tmp, 0);
        }
        else
        {
            float f = BitConverter.ToSingle(data, idx);
            idx += 4;
            return f;
        }
    }
}

public static class OSCArgReader
{
    public static int[] Ints(OSCMessage msg)
    {
        var list = new List<int>(msg.Args.Length);
        foreach (var a in msg.Args)
        {
            if (a is int i) list.Add(i);
            else if (a is float f) list.Add((int)f);
        }
        return list.ToArray();
    }
}

#endregion

/// <summary>
/// Simple example: logs a few VMC messages and shows how to subscribe.
/// Add this component alongside VMCReceiver in your scene.
/// </summary>
public class VMCExampleLogger : MonoBehaviour
{
    public VMCReceiver receiver;

    void Reset()
    {
        receiver = GetComponent<VMCReceiver>();
        if (!receiver) receiver = gameObject.AddComponent<VMCReceiver>();
    }

    void OnEnable()
    {
        if (!receiver) receiver = GetComponent<VMCReceiver>();
        if (!receiver) return;

        receiver.OnOk += (loaded, cstate, cmode, track) =>
        {
            Debug.Log($"/VMC/Ext/OK loaded={loaded} calibState={cstate} calibMode={cmode} tracking={track}");
        };
        receiver.OnTime += t => Debug.Log($"/VMC/Ext/T time={t}");
        receiver.OnRootPose += (name, p, q, s, o) => Debug.Log($"Root {name} p={p} q={q} s={s} o={o}");
        receiver.OnBonePose += (name, p, q) => {
            if (name == "Hips" || name == "Head")
                Debug.Log($"Bone {name} p={p} q={q}");
        };
        receiver.OnBlendShapeValue += (n, v) =>
        {
            if (n == "A" || n == "Joy") Debug.Log($"Blend {n}={v}");
        };
        receiver.OnBlendShapeApply += () => Debug.Log("Blend Apply");
    }
}
