using System.Threading;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    public static class RegistryLocator
    {
        private static IProviderRegistry s_providerRegistry;
        private static IMoCapSourceRegistry s_moCapSourceRegistry;

        public static IProviderRegistry ProviderRegistry
        {
            get
            {
                if (s_providerRegistry == null)
                    Interlocked.CompareExchange(ref s_providerRegistry, new DefaultProviderRegistry(), null);
                return s_providerRegistry;
            }
        }

        public static IMoCapSourceRegistry MoCapSourceRegistry
        {
            get
            {
                if (s_moCapSourceRegistry == null)
                    Interlocked.CompareExchange(ref s_moCapSourceRegistry, new DefaultMoCapSourceRegistry(), null);
                return s_moCapSourceRegistry;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
        }

        private sealed class DefaultProviderRegistry : IProviderRegistry { }
        private sealed class DefaultMoCapSourceRegistry : IMoCapSourceRegistry { }
    }
}
