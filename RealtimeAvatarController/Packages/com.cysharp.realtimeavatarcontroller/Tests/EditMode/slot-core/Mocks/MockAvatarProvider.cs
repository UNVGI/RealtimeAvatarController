using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealtimeAvatarController.Core.Tests.Mocks
{
    /// <summary>
    /// Test double for <see cref="IAvatarProvider"/>.
    /// Counts Dispose / RequestAvatar / RequestAvatarAsync / ReleaseAvatar invocations so SlotManager
    /// tests can verify the correct call order without a real provider implementation.
    /// </summary>
    internal sealed class MockAvatarProvider : IAvatarProvider
    {
        public string ProviderType { get; set; } = "Mock";

        public int DisposeCallCount { get; private set; }
        public int RequestAvatarCallCount { get; private set; }
        public int RequestAvatarAsyncCallCount { get; private set; }
        public int ReleaseAvatarCallCount { get; private set; }
        public GameObject LastReleasedAvatar { get; private set; }

        public Func<ProviderConfigBase, GameObject> RequestAvatarFunc { get; set; }
        public Func<ProviderConfigBase, CancellationToken, UniTask<GameObject>> RequestAvatarAsyncFunc { get; set; }
        public Action<GameObject> OnReleaseAvatar { get; set; }
        public Exception RequestAvatarException { get; set; }

        public GameObject RequestAvatar(ProviderConfigBase config)
        {
            RequestAvatarCallCount++;
            if (RequestAvatarException != null) throw RequestAvatarException;
            return RequestAvatarFunc != null
                ? RequestAvatarFunc(config)
                : new GameObject("MockAvatar");
        }

        public UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)
        {
            RequestAvatarAsyncCallCount++;
            if (RequestAvatarException != null) throw RequestAvatarException;
            if (RequestAvatarAsyncFunc != null) return RequestAvatarAsyncFunc(config, cancellationToken);
            return UniTask.FromResult(new GameObject("MockAvatar"));
        }

        public void ReleaseAvatar(GameObject avatar)
        {
            ReleaseAvatarCallCount++;
            LastReleasedAvatar = avatar;
            OnReleaseAvatar?.Invoke(avatar);
        }

        public void Dispose() => DisposeCallCount++;
    }
}
