using System;

namespace RealtimeAvatarController.Core.Tests.Mocks
{
    /// <summary>
    /// Test double for <see cref="IAvatarProviderFactory"/>.
    /// By default returns a fresh <see cref="MockAvatarProvider"/> per <see cref="Create"/> call;
    /// override <see cref="CreateFunc"/> to customize behavior (e.g. throw to exercise init-failure paths).
    /// </summary>
    internal sealed class MockAvatarProviderFactory : IAvatarProviderFactory
    {
        public int CreateCallCount { get; private set; }
        public ProviderConfigBase LastConfig { get; private set; }
        public MockAvatarProvider LastCreatedProvider { get; private set; }

        public Func<ProviderConfigBase, IAvatarProvider> CreateFunc { get; set; }
        public Exception CreateException { get; set; }

        public IAvatarProvider Create(ProviderConfigBase config)
        {
            CreateCallCount++;
            LastConfig = config;
            if (CreateException != null) throw CreateException;
            if (CreateFunc != null) return CreateFunc(config);

            var provider = new MockAvatarProvider();
            LastCreatedProvider = provider;
            return provider;
        }
    }
}
