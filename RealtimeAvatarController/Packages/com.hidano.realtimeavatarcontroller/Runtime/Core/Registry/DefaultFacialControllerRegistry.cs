using System.Collections.Generic;
using System.Linq;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IFacialControllerRegistry の既定実装。
    /// Dictionary&lt;string, IFacialControllerFactory&gt; で内部管理し、
    /// 同一 controllerTypeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// </summary>
    internal sealed class DefaultFacialControllerRegistry : IFacialControllerRegistry
    {
        private readonly Dictionary<string, IFacialControllerFactory> _factories
            = new Dictionary<string, IFacialControllerFactory>();

        /// <inheritdoc/>
        public void Register(string controllerTypeId, IFacialControllerFactory factory)
        {
            if (_factories.ContainsKey(controllerTypeId))
                throw new RegistryConflictException(controllerTypeId, "IFacialControllerRegistry");

            _factories.Add(controllerTypeId, factory);
        }

        /// <inheritdoc/>
        public IFacialController Resolve(FacialControllerDescriptor descriptor)
        {
            if (!_factories.TryGetValue(descriptor.ControllerTypeId, out var factory))
                throw new KeyNotFoundException(
                    $"typeId '{descriptor.ControllerTypeId}' は IFacialControllerRegistry に登録されていません。");

            return factory.Create(descriptor.Config);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRegisteredTypeIds()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }
    }
}
