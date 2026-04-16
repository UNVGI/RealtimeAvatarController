using System;
using UniRx;

namespace RealtimeAvatarController.Core.Tests.Mocks
{
    /// <summary>
    /// Test double for <see cref="IMoCapSource"/>.
    /// Exposes a <see cref="Subject{T}"/> so tests can push frames into
    /// <see cref="MotionStream"/>, and counts Initialize / Shutdown / Dispose calls.
    /// </summary>
    internal sealed class MockMoCapSource : IMoCapSource
    {
        private readonly Subject<MotionFrame> _subject = new Subject<MotionFrame>();

        public string SourceType { get; set; } = "Mock";

        public int InitializeCallCount { get; private set; }
        public int ShutdownCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }
        public MoCapSourceConfigBase LastInitializedConfig { get; private set; }

        public IObservable<MotionFrame> MotionStream => _subject;

        public void Initialize(MoCapSourceConfigBase config)
        {
            InitializeCallCount++;
            LastInitializedConfig = config;
        }

        public void Shutdown() => ShutdownCallCount++;

        public void Dispose()
        {
            DisposeCallCount++;
            _subject.OnCompleted();
            _subject.Dispose();
        }

        /// <summary>
        /// Push a frame into the internal subject so subscribers receive it.
        /// Used by SlotManager tests to simulate MoCap input without a real source.
        /// </summary>
        public void Emit(MotionFrame frame) => _subject.OnNext(frame);
    }
}
