using System;
using System.Collections.Generic;

namespace RealtimeAvatarController.Core.Tests.Mocks
{
    /// <summary>
    /// Test double for <see cref="IMoCapSourceRegistry"/>.
    /// 内部に <see cref="DefaultMoCapSourceRegistry"/> を保持し委譲する薄いデコレータ。
    /// タスク 12.5: SlotManager.RemoveSlotAsync の呼び出し順序検証
    /// (<see cref="CallOrderRecorder"/>) と <see cref="ReleaseException"/> 注入による
    /// Registry.Release 失敗時のリソース解放継続挙動検証を可能にする。
    /// </summary>
    internal sealed class MockMoCapSourceRegistry : IMoCapSourceRegistry
    {
        private readonly DefaultMoCapSourceRegistry _inner = new DefaultMoCapSourceRegistry();

        public int ReleaseCallCount { get; private set; }
        public int ResolveCallCount { get; private set; }
        public IMoCapSource LastReleasedSource { get; private set; }
        public List<string> CallOrderRecorder { get; set; }
        public Exception ReleaseException { get; set; }

        public void Register(string sourceTypeId, IMoCapSourceFactory factory)
            => _inner.Register(sourceTypeId, factory);

        public IMoCapSource Resolve(MoCapSourceDescriptor descriptor)
        {
            ResolveCallCount++;
            return _inner.Resolve(descriptor);
        }

        public void Release(IMoCapSource source)
        {
            ReleaseCallCount++;
            LastReleasedSource = source;
            CallOrderRecorder?.Add("Registry.Release");
            if (ReleaseException != null) throw ReleaseException;
            _inner.Release(source);
        }

        public IReadOnlyList<string> GetRegisteredTypeIds() => _inner.GetRegisteredTypeIds();

        public bool TryGetFactory(string sourceTypeId, out IMoCapSourceFactory factory)
            => _inner.TryGetFactory(sourceTypeId, out factory);
    }
}
