using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// DefaultLipSyncSourceRegistry の EditMode テスト。
    /// テスト観点:
    ///   - Register 成功
    ///   - 同一 typeId 競合で RegistryConflictException
    ///   - Resolve 成功 (Factory.Create が呼ばれる)
    ///   - 未登録 typeId Resolve で KeyNotFoundException
    ///   - GetRegisteredTypeIds 結果確認
    /// Requirements: 9.1, 9.9
    /// </summary>
    [TestFixture]
    public class LipSyncSourceRegistryTests
    {
        private DefaultLipSyncSourceRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _registry = new DefaultLipSyncSourceRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        // --- Register 成功 ---

        [Test]
        public void Register_ValidTypeIdAndFactory_Succeeds()
        {
            var factory = new StubLipSyncSourceFactory();
            Assert.DoesNotThrow(() => _registry.Register("OVRLipSync", factory));
        }

        [Test]
        public void Register_MultipleTypeIds_Succeeds()
        {
            _registry.Register("OVRLipSync", new StubLipSyncSourceFactory());
            _registry.Register("Viseme", new StubLipSyncSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(2));
            Assert.That(typeIds, Contains.Item("OVRLipSync"));
            Assert.That(typeIds, Contains.Item("Viseme"));
        }

        // --- 同一 typeId 競合で RegistryConflictException (Req 9.9) ---

        [Test]
        public void Register_DuplicateTypeId_ThrowsRegistryConflictException()
        {
            _registry.Register("OVRLipSync", new StubLipSyncSourceFactory());

            var ex = Assert.Throws<RegistryConflictException>(
                () => _registry.Register("OVRLipSync", new StubLipSyncSourceFactory()));
            Assert.That(ex.TypeId, Is.EqualTo("OVRLipSync"));
            Assert.That(ex.RegistryName, Is.EqualTo("ILipSyncSourceRegistry"));
        }

        // --- Resolve 成功 (Req 9.1) ---

        [Test]
        public void Resolve_RegisteredTypeId_ReturnsSourceFromFactory()
        {
            var expected = new StubLipSyncSource();
            var factory = new StubLipSyncSourceFactory { SourceToReturn = expected };
            _registry.Register("OVRLipSync", factory);

            var descriptor = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync" };
            var source = _registry.Resolve(descriptor);

            Assert.That(source, Is.SameAs(expected));
        }

        [Test]
        public void Resolve_PassesDescriptorConfigToFactory()
        {
            LipSyncSourceConfigBase capturedConfig = null;
            var factory = new StubLipSyncSourceFactory
            {
                OnCreate = config => capturedConfig = config
            };
            _registry.Register("OVRLipSync", factory);

            var config = UnityEngine.ScriptableObject.CreateInstance<StubLipSyncSourceConfig>();
            var descriptor = new LipSyncSourceDescriptor
            {
                SourceTypeId = "OVRLipSync",
                Config = config
            };
            _registry.Resolve(descriptor);

            Assert.That(capturedConfig, Is.SameAs(config));

            UnityEngine.Object.DestroyImmediate(config);
        }

        // --- 未登録 typeId Resolve で KeyNotFoundException ---

        [Test]
        public void Resolve_UnregisteredTypeId_ThrowsKeyNotFoundException()
        {
            var descriptor = new LipSyncSourceDescriptor { SourceTypeId = "Unknown" };

            Assert.Throws<KeyNotFoundException>(() => _registry.Resolve(descriptor));
        }

        // --- GetRegisteredTypeIds (Req 9.1) ---

        [Test]
        public void GetRegisteredTypeIds_Empty_ReturnsEmptyList()
        {
            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Is.Not.Null);
            Assert.That(typeIds, Is.Empty);
        }

        [Test]
        public void GetRegisteredTypeIds_AfterRegistrations_ReturnsAllTypeIds()
        {
            _registry.Register("OVRLipSync", new StubLipSyncSourceFactory());
            _registry.Register("Viseme", new StubLipSyncSourceFactory());
            _registry.Register("Custom", new StubLipSyncSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(3));
            Assert.That(typeIds, Contains.Item("OVRLipSync"));
            Assert.That(typeIds, Contains.Item("Viseme"));
            Assert.That(typeIds, Contains.Item("Custom"));
        }

        [Test]
        public void GetRegisteredTypeIds_ReturnsReadOnlyList()
        {
            _registry.Register("OVRLipSync", new StubLipSyncSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Is.InstanceOf<IReadOnlyList<string>>());
        }

        // --- Stub implementations for testing ---

        private class StubLipSyncSourceConfig : LipSyncSourceConfigBase { }

        private class StubLipSyncSource : ILipSyncSource
        {
            public void Initialize(LipSyncSourceConfigBase config) { }
            public object FetchLatestLipSync() => null;
            public void Shutdown() { }
            public void Dispose() { }
        }

        private class StubLipSyncSourceFactory : ILipSyncSourceFactory
        {
            public StubLipSyncSource SourceToReturn { get; set; } = new StubLipSyncSource();
            public Action<LipSyncSourceConfigBase> OnCreate { get; set; }

            public ILipSyncSource Create(LipSyncSourceConfigBase config)
            {
                OnCreate?.Invoke(config);
                return SourceToReturn;
            }
        }
    }
}
