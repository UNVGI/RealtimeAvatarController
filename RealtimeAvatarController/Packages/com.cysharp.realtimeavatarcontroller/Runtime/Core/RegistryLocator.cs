using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    public static class RegistryLocator
    {
        private static IProviderRegistry s_providerRegistry;
        private static IMoCapSourceRegistry s_moCapSourceRegistry;
        private static ISlotErrorChannel s_errorChannel;

        /// <summary>Debug.LogError 抑制用 HashSet (DefaultSlotErrorChannel から参照)。</summary>
        internal static HashSet<(string SlotId, SlotErrorCategory Category)> s_suppressedErrors
            = new HashSet<(string, SlotErrorCategory)>();

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

        public static ISlotErrorChannel ErrorChannel
        {
            get
            {
                if (s_errorChannel == null)
                    Interlocked.CompareExchange(ref s_errorChannel, new DefaultSlotErrorChannel(), null);
                return s_errorChannel;
            }
        }

        public static void OverrideErrorChannel(ISlotErrorChannel channel)
            => s_errorChannel = channel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
            s_errorChannel = null;
            s_suppressedErrors?.Clear();
        }

        private sealed class DefaultProviderRegistry : IProviderRegistry { }
        private sealed class DefaultMoCapSourceRegistry : IMoCapSourceRegistry { }
    }
}
