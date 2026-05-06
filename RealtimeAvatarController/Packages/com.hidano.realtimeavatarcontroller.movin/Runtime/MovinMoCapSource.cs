using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// MOVIN OSC source that emits snapshot frames from the latest received bone poses.
    /// </summary>
    public sealed class MovinMoCapSource : IMoCapSource, IDisposable, IMovinReceiverAdapter
    {
        internal enum State
        {
            Uninitialized,
            Running,
            Disposed,
        }

        private const string MovinSourceTypeId = "MOVIN";

        private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();
        private readonly ISubject<MotionFrame> _subject;
        private readonly IObservable<MotionFrame> _stream;
        private readonly Dictionary<string, MovinBonePose> _latestBones =
            new Dictionary<string, MovinBonePose>();

        private MovinRootPose? _latestRootPose;
        private MovinOscReceiverHost _receiverHost;
        private State _state = State.Uninitialized;
        private bool _bindAddressWarningLogged;

        public MovinMoCapSource()
        {
            _subject = _rawSubject.Synchronize();
            _stream = _subject.Publish().RefCount();
        }

        public string SourceType => MovinSourceTypeId;

        public IObservable<MotionFrame> MotionStream => _stream;

        internal State CurrentState => _state;

        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"MovinMoCapSource.Initialize can only be called while Uninitialized. Current state: {_state}.");
            }

            var movinConfig = config as MovinMoCapSourceConfig;
            if (movinConfig == null)
            {
                var actualTypeName = config?.GetType().Name ?? "null";
                throw new ArgumentException(
                    $"MovinMoCapSourceConfig is required, but {actualTypeName} was provided.",
                    nameof(config));
            }

            if (movinConfig.port < 1 || movinConfig.port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movinConfig.port),
                    movinConfig.port,
                    "MovinMoCapSourceConfig.port must be between 1 and 65535.");
            }

            WarnIfBindAddressIsInformational(movinConfig.bindAddress);

            MovinOscReceiverHost host = null;
            try
            {
                host = MovinOscReceiverHost.Create(this);
                host.ApplyReceiverSettings(movinConfig.port);
                _receiverHost = host;
                _state = State.Running;
            }
            catch
            {
                if (host != null)
                {
                    host.Shutdown();
                }

                throw;
            }
        }

        public void Shutdown()
        {
            if (_state == State.Disposed)
            {
                return;
            }

            _state = State.Disposed;

            if (_receiverHost != null)
            {
                _receiverHost.Shutdown();
                _receiverHost = null;
            }

            _latestBones.Clear();
            _latestRootPose = null;

            _rawSubject.OnCompleted();
            _rawSubject.Dispose();
        }

        public void Dispose()
        {
            Shutdown();
        }

        void IMovinReceiverAdapter.HandleBonePose(
            string boneName,
            Vector3 localPos,
            Quaternion localRot)
        {
            if (_state != State.Running)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(boneName))
                {
                    return;
                }

                _latestBones[boneName] = new MovinBonePose(localPos, localRot);
            }
            catch (Exception ex)
            {
                PublishError(ex);
            }
        }

        void IMovinReceiverAdapter.HandleRootPose(
            string boneName,
            Vector3 localPos,
            Quaternion localRot,
            Vector3? localScale,
            Vector3? localOffset)
        {
            if (_state != State.Running)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(boneName))
                {
                    return;
                }

                _latestRootPose = new MovinRootPose(
                    boneName,
                    localPos,
                    localRot,
                    localScale,
                    localOffset);
            }
            catch (Exception ex)
            {
                PublishError(ex);
            }
        }

        void IMovinReceiverAdapter.Tick()
        {
            if (_state != State.Running || _latestBones.Count == 0)
            {
                return;
            }

            try
            {
                var snapshot = new Dictionary<string, MovinBonePose>(_latestBones.Count);
                foreach (var pair in _latestBones)
                {
                    snapshot[pair.Key] = pair.Value;
                }

                var timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                var frame = new MovinMotionFrame(timestamp, snapshot, _latestRootPose);
                _subject.OnNext(frame);
            }
            catch (Exception ex)
            {
                PublishError(ex);
            }
        }

        void IMovinReceiverAdapter.HandleTickException(Exception exception)
        {
            PublishError(exception);
        }

        private void WarnIfBindAddressIsInformational(string bindAddress)
        {
            if (_bindAddressWarningLogged || string.IsNullOrEmpty(bindAddress))
            {
                return;
            }

            _bindAddressWarningLogged = true;
            UnityEngine.Debug.LogWarning(
                $"[MovinMoCapSource] bindAddress '{bindAddress}' is informational only in uOSC 1.0.0. Server binds to all interfaces.");
        }

        private static void PublishError(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            RegistryLocator.ErrorChannel.Publish(
                new SlotError(MovinSourceTypeId, SlotErrorCategory.VmcReceive, ex, DateTime.UtcNow));
        }
    }
}
