using System.Collections.Generic;
using System.Linq;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IProviderRegistry の既定実装。
    /// Dictionary&lt;string, IAvatarProviderFactory&gt; で内部管理し、
    /// 同一 typeId の二重登録は RegistryConflictException をスローする。
    /// </summary>
    internal sealed class DefaultProviderRegistry : IProviderRegistry
    {
        private readonly Dictionary<string, IAvatarProviderFactory> _factories
            = new Dictionary<string, IAvatarProviderFactory>();

        /// <inheritdoc/>
        public void Register(string providerTypeId, IAvatarProviderFactory factory)
        {
            if (_factories.ContainsKey(providerTypeId))
                throw new RegistryConflictException(providerTypeId, "IProviderRegistry");

            _factories.Add(providerTypeId, factory);
        }

        /// <inheritdoc/>
        public IAvatarProvider Resolve(AvatarProviderDescriptor descriptor)
        {
            if (!_factories.TryGetValue(descriptor.ProviderTypeId, out var factory))
                throw new KeyNotFoundException(
                    $"typeId '{descriptor.ProviderTypeId}' は IProviderRegistry に登録されていません。");

            return factory.Create(descriptor.Config);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRegisteredTypeIds()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }
    }
}
