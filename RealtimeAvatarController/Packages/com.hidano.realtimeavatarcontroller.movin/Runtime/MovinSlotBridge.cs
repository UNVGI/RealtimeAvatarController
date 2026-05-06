using System;
using RealtimeAvatarController.Core;
using UniRx;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Connects an IMoCapSource motion stream to a MOVIN applier.
    /// </summary>
    public sealed class MovinSlotBridge : IDisposable
    {
        private IDisposable _subscription;

        public MovinSlotBridge(IMoCapSource source, MovinMotionApplier applier)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (applier == null)
            {
                throw new ArgumentNullException(nameof(applier));
            }

            var stream = source.MotionStream;
            if (stream == null)
            {
                throw new ArgumentException("source.MotionStream must not be null.", nameof(source));
            }

            _subscription = stream
                .ObserveOnMainThread()
                .Subscribe(frame =>
                {
                    if (frame is MovinMotionFrame movinFrame)
                    {
                        applier.Apply(movinFrame);
                    }
                });
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
