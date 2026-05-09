using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using UniRx;
using UnityEngine;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// Native VMC MoCap source backed by <see cref="VMCSharedReceiver"/>.
    /// </summary>
    public sealed class VMCMoCapSource : IMoCapSource, IDisposable, IVmcMoCapAdapter
    {
        internal enum State
        {
            Uninitialized,
            Running,
            Disposed,
        }

        private readonly string _slotId;
        private readonly ISlotErrorChannel _errorChannel;
        private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();
        private readonly ISubject<MotionFrame> _subject;
        private readonly IObservable<MotionFrame> _stream;
        private readonly Dictionary<HumanBodyBones, Quaternion> _bufferA;
        private readonly Dictionary<HumanBodyBones, Quaternion> _bufferB;

        private Dictionary<HumanBodyBones, Quaternion> _writeBufferRef;
        private Dictionary<HumanBodyBones, Quaternion> _readBufferRef;
        private VMCSharedReceiver _sharedReceiver;
        private Vector3 _writeRootPosition;
        private Quaternion _writeRootRotation = Quaternion.identity;
        private Vector3 _readRootPosition;
        private Quaternion _readRootRotation = Quaternion.identity;
        private bool _hasInjectedDataForTest;
        private Exception _tickExceptionForTest;
        private State _state = State.Uninitialized;

        public string SourceType => "VMC";

        public IObservable<MotionFrame> MotionStream => _stream;

        internal State CurrentState => _state;

        internal IReadOnlyDictionary<HumanBodyBones, Quaternion> ReadBufferForTest => _readBufferRef;

        internal IReadOnlyDictionary<HumanBodyBones, Quaternion> WriteBufferForTest => _writeBufferRef;

        internal VMCSharedReceiver SharedReceiverForTest => _sharedReceiver;

        internal VMCMoCapSource(string slotId, ISlotErrorChannel errorChannel)
        {
            _slotId = slotId ?? string.Empty;
            _errorChannel = errorChannel ?? throw new ArgumentNullException(nameof(errorChannel));

            _bufferA = new Dictionary<HumanBodyBones, Quaternion>(64);
            _bufferB = new Dictionary<HumanBodyBones, Quaternion>(64);
            _writeBufferRef = _bufferA;
            _readBufferRef = _bufferB;

            _subject = _rawSubject.Synchronize();
            _stream = _subject.Publish().RefCount();
        }

        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"VMCMoCapSource.Initialize can only be called while Uninitialized. Current state: {_state}.");
            }

            if (!(config is VMCMoCapSourceConfig vmcConfig))
            {
                var actualTypeName = config?.GetType().Name ?? "null";
                throw new ArgumentException(
                    $"VMCMoCapSourceConfig is required, but {actualTypeName} was provided.",
                    nameof(config));
            }

            if (vmcConfig.port < 1025 || vmcConfig.port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(config),
                    vmcConfig.port,
                    "VMCMoCapSourceConfig.port must be in the range 1025..65535.");
            }

            _sharedReceiver = VMCSharedReceiver.EnsureInstance(vmcConfig);
            try
            {
                _sharedReceiver.ApplyReceiverSettings(vmcConfig.port);
                _sharedReceiver.Subscribe(this);
                _state = State.Running;
            }
            catch
            {
                _sharedReceiver.Release();
                _sharedReceiver = null;
                throw;
            }
        }

        public void Initialize(VMCMoCapSourceConfig config)
        {
            Initialize((MoCapSourceConfigBase)config);
        }

        public void Shutdown()
        {
            if (_state == State.Disposed)
            {
                return;
            }

            if (_sharedReceiver != null)
            {
                _sharedReceiver.Unsubscribe(this);
                _sharedReceiver.Release();
                _sharedReceiver = null;
            }

            _subject.OnCompleted();
            _rawSubject.Dispose();
            _state = State.Disposed;
        }

        public void Dispose()
        {
            Shutdown();
        }

        void IVmcMoCapAdapter.Tick()
        {
            if (_state != State.Running || _sharedReceiver == null)
            {
                return;
            }

            if (_tickExceptionForTest != null)
            {
                var exception = _tickExceptionForTest;
                _tickExceptionForTest = null;
                throw exception;
            }

            if (_hasInjectedDataForTest)
            {
                _hasInjectedDataForTest = false;
            }
            else
            {
                _writeBufferRef.Clear();
                var receiverBuffer = _sharedReceiver.ReadAndClearWriteBuffer(
                    out _writeRootPosition,
                    out _writeRootRotation);

                foreach (var kv in receiverBuffer)
                {
                    _writeBufferRef[kv.Key] = kv.Value;
                }
            }

            SwapBuffers();

            _readRootPosition = _writeRootPosition;
            _readRootRotation = _writeRootRotation;
            _writeBufferRef.Clear();

            if (_readBufferRef.Count == 0)
            {
                return;
            }

            var timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            var frame = new HumanoidMotionFrame(
                timestamp,
                Array.Empty<float>(),
                _readRootPosition,
                _readRootRotation,
                _readBufferRef);

            _subject.OnNext(frame);
        }

        void IVmcMoCapAdapter.HandleTickException(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            PublishError(SlotErrorCategory.VmcReceive, exception);
        }

        internal void InjectBoneRotationForTest(HumanBodyBones bone, Quaternion rotation)
        {
            PrepareInjectedFrameForTest();
            _writeBufferRef[bone] = rotation;
        }

        internal void InjectRootForTest(Vector3 position, Quaternion rotation)
        {
            PrepareInjectedFrameForTest();
            _writeRootPosition = position;
            _writeRootRotation = rotation;
        }

        internal void ForceTickForTest()
        {
            try
            {
                ((IVmcMoCapAdapter)this).Tick();
            }
            catch (Exception ex)
            {
                ((IVmcMoCapAdapter)this).HandleTickException(ex);
            }
        }

        internal void ThrowOnNextTickForTest(Exception exception)
        {
            _tickExceptionForTest = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        private void SwapBuffers()
        {
            var previousReadBuffer = _readBufferRef;
            _readBufferRef = _writeBufferRef;
            _writeBufferRef = previousReadBuffer;
        }

        private void PrepareInjectedFrameForTest()
        {
            if (_hasInjectedDataForTest)
            {
                return;
            }

            _writeBufferRef.Clear();
            _writeRootPosition = default;
            _writeRootRotation = Quaternion.identity;
            _hasInjectedDataForTest = true;
        }

        private void PublishError(SlotErrorCategory category, Exception ex)
        {
            _errorChannel.Publish(new SlotError(_slotId, category, ex, DateTime.UtcNow));
        }
    }
}
