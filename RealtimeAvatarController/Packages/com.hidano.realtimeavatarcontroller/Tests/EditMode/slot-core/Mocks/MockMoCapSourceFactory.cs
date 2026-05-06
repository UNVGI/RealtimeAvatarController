using System;
using UnityEngine;

namespace RealtimeAvatarController.Core.Tests.Mocks
{
    /// <summary>
    /// Test double for <see cref="IMoCapSourceFactory"/>.
    /// By default returns a fresh <see cref="MockMoCapSource"/> per <see cref="Create"/> call;
    /// override <see cref="CreateFunc"/> to inject custom sources or exceptions.
    /// </summary>
    internal sealed class MockMoCapSourceFactory : IMoCapSourceFactory
    {
        public int CreateCallCount { get; private set; }
        public MoCapSourceConfigBase LastConfig { get; private set; }
        public MockMoCapSource LastCreatedSource { get; private set; }

        public Func<MoCapSourceConfigBase, IMoCapSource> CreateFunc { get; set; }
        public Exception CreateException { get; set; }

        public Func<MoCapSourceConfigBase> CreateDefaultConfigFunc { get; set; }
        public Func<IMoCapSource, GameObject, MoCapSourceConfigBase, IDisposable> CreateApplierBridgeFunc { get; set; }

        public IMoCapSource Create(MoCapSourceConfigBase config)
        {
            CreateCallCount++;
            LastConfig = config;
            if (CreateException != null) throw CreateException;
            if (CreateFunc != null) return CreateFunc(config);

            var source = new MockMoCapSource();
            LastCreatedSource = source;
            return source;
        }

        public MoCapSourceConfigBase CreateDefaultConfig()
        {
            return CreateDefaultConfigFunc != null ? CreateDefaultConfigFunc() : null;
        }

        public IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config)
        {
            return CreateApplierBridgeFunc != null
                ? CreateApplierBridgeFunc(source, avatar, config)
                : new NoopDisposable();
        }

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
