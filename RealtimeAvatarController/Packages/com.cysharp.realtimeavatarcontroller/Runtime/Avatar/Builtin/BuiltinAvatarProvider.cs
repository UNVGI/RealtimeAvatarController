using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin
{
    /// <summary>
    /// IAvatarProvider のビルトイン具象実装。
    /// Object.Instantiate / Object.Destroy による Prefab ベースのアバター供給を担う。
    /// </summary>
    public sealed class BuiltinAvatarProvider : IAvatarProvider
    {
        private readonly BuiltinAvatarProviderConfig _config;
        private readonly ISlotErrorChannel _errorChannel;
        private readonly HashSet<GameObject> _managedAvatars = new HashSet<GameObject>();
        private bool _disposed;

        public string ProviderType => "Builtin";

        public BuiltinAvatarProvider(BuiltinAvatarProviderConfig config, ISlotErrorChannel errorChannel)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config;
            _errorChannel = errorChannel ?? RegistryLocator.ErrorChannel;
        }

        public GameObject RequestAvatar(ProviderConfigBase config)
        {
            throw new NotImplementedException();
        }

        public UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void ReleaseAvatar(GameObject avatar)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BuiltinAvatarProvider));
            }
        }
    }
}
