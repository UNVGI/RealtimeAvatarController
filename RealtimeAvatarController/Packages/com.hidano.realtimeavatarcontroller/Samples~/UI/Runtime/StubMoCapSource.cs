using System;
using RealtimeAvatarController.Core;
using UniRx;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.Samples.UI
{
    public sealed class StubMoCapSource : IMoCapSource, IDisposable
    {
        public const string StubSourceTypeId = "Stub";

        internal enum State
        {
            Uninitialized,
            Running,
            Disposed,
        }

        private readonly Subject<MotionFrame> _rawSubject = new Subject<MotionFrame>();
        private readonly ISubject<MotionFrame> _subject;
        private readonly IObservable<MotionFrame> _stream;

        private State _state = State.Uninitialized;

        public StubMoCapSource()
        {
            _subject = _rawSubject.Synchronize();
            _stream = _subject.Publish().RefCount();
        }

        public string SourceType => StubSourceTypeId;

        internal State CurrentState => _state;

        public IObservable<MotionFrame> MotionStream => _stream;

        public void Initialize(MoCapSourceConfigBase config)
        {
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"StubMoCapSource.Initialize can only be called in the Uninitialized state. Current state: {_state}.");
            }

            var stubConfig = config as StubMoCapSourceConfig;
            if (stubConfig == null)
            {
                throw new ArgumentException(
                    $"StubMoCapSourceConfig is required, but {config?.GetType().Name ?? "null"} was provided.",
                    nameof(config));
            }

            _state = State.Running;
        }

        public void Shutdown()
        {
            if (_state == State.Disposed)
            {
                return;
            }

            _rawSubject.OnCompleted();
            _rawSubject.Dispose();

            _state = State.Disposed;
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
