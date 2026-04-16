using System.Collections.Generic;
using System.Linq;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IMoCapSourceRegistry の既定実装。
    /// Dictionary&lt;string, IMoCapSourceFactory&gt; で Factory を管理し、
    /// Dictionary&lt;MoCapSourceDescriptor, (IMoCapSource source, int refCount)&gt; で
    /// 同一 Descriptor に対するインスタンス共有と参照カウントを管理する。
    /// 同一 typeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// 参照カウントが 0 に到達した時点で IMoCapSource.Dispose() を呼ぶ。
    /// </summary>
    internal sealed class DefaultMoCapSourceRegistry : IMoCapSourceRegistry
    {
        private readonly Dictionary<string, IMoCapSourceFactory> _factories
            = new Dictionary<string, IMoCapSourceFactory>();

        private readonly Dictionary<MoCapSourceDescriptor, Entry> _instances
            = new Dictionary<MoCapSourceDescriptor, Entry>();

        /// <inheritdoc/>
        public void Register(string sourceTypeId, IMoCapSourceFactory factory)
        {
            if (_factories.ContainsKey(sourceTypeId))
                throw new RegistryConflictException(sourceTypeId, "IMoCapSourceRegistry");

            _factories.Add(sourceTypeId, factory);
        }

        /// <inheritdoc/>
        public IMoCapSource Resolve(MoCapSourceDescriptor descriptor)
        {
            if (_instances.TryGetValue(descriptor, out var entry))
            {
                entry.RefCount++;
                _instances[descriptor] = entry;
                return entry.Source;
            }

            if (!_factories.TryGetValue(descriptor.SourceTypeId, out var factory))
                throw new KeyNotFoundException(
                    $"typeId '{descriptor.SourceTypeId}' は IMoCapSourceRegistry に登録されていません。");

            var source = factory.Create(descriptor.Config);
            _instances[descriptor] = new Entry { Source = source, RefCount = 1, Descriptor = descriptor };
            return source;
        }

        /// <inheritdoc/>
        public void Release(IMoCapSource source)
        {
            if (source == null) return;

            MoCapSourceDescriptor targetKey = null;
            Entry targetEntry = default;
            foreach (var kv in _instances)
            {
                if (ReferenceEquals(kv.Value.Source, source))
                {
                    targetKey = kv.Key;
                    targetEntry = kv.Value;
                    break;
                }
            }

            // 管理外の IMoCapSource を Release しても何もしない (冪等性確保)
            if (targetKey == null) return;

            targetEntry.RefCount--;
            if (targetEntry.RefCount <= 0)
            {
                _instances.Remove(targetKey);
                targetEntry.Source.Dispose();
            }
            else
            {
                _instances[targetKey] = targetEntry;
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRegisteredTypeIds()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }

        private struct Entry
        {
            public IMoCapSource Source;
            public int RefCount;
            public MoCapSourceDescriptor Descriptor;
        }
    }
}
