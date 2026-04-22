using System.Collections.Generic;
using System.Linq;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// ILipSyncSourceRegistry の既定実装。
    /// Dictionary&lt;string, ILipSyncSourceFactory&gt; で内部管理し、
    /// 同一 sourceTypeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// </summary>
    internal sealed class DefaultLipSyncSourceRegistry : ILipSyncSourceRegistry
    {
        private readonly Dictionary<string, ILipSyncSourceFactory> _factories
            = new Dictionary<string, ILipSyncSourceFactory>();

        /// <inheritdoc/>
        public void Register(string sourceTypeId, ILipSyncSourceFactory factory)
        {
            if (_factories.ContainsKey(sourceTypeId))
                throw new RegistryConflictException(sourceTypeId, "ILipSyncSourceRegistry");

            _factories.Add(sourceTypeId, factory);
        }

        /// <inheritdoc/>
        public ILipSyncSource Resolve(LipSyncSourceDescriptor descriptor)
        {
            if (!_factories.TryGetValue(descriptor.SourceTypeId, out var factory))
                throw new KeyNotFoundException(
                    $"typeId '{descriptor.SourceTypeId}' は ILipSyncSourceRegistry に登録されていません。");

            return factory.Create(descriptor.Config);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRegisteredTypeIds()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }
    }
}
