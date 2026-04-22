using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// DefaultProviderRegistry の EditMode テスト。
    /// テスト観点:
    ///   - Register 成功
    ///   - 同一 typeId 競合で RegistryConflictException
    ///   - Resolve 成功 (Factory.Create が呼ばれる)
    ///   - 未登録 typeId Resolve で KeyNotFoundException
    ///   - GetRegisteredTypeIds 結果確認
    /// Requirements: 9.1, 9.2, 9.3, 9.9
    /// </summary>
    [TestFixture]
    public class ProviderRegistryTests
    {
        private DefaultProviderRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _registry = new DefaultProviderRegistry();
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
            var factory = new StubAvatarProviderFactory();
            Assert.DoesNotThrow(() => _registry.Register("Builtin", factory));
        }

        [Test]
        public void Register_MultipleTypeIds_Succeeds()
        {
            _registry.Register("Builtin", new StubAvatarProviderFactory());
            _registry.Register("Addressable", new StubAvatarProviderFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(2));
            Assert.That(typeIds, Contains.Item("Builtin"));
            Assert.That(typeIds, Contains.Item("Addressable"));
        }

        // --- 同一 typeId 競合で RegistryConflictException (Req 9.9) ---

        [Test]
        public void Register_DuplicateTypeId_ThrowsRegistryConflictException()
        {
            _registry.Register("Builtin", new StubAvatarProviderFactory());

            var ex = Assert.Throws<RegistryConflictException>(
                () => _registry.Register("Builtin", new StubAvatarProviderFactory()));
            Assert.That(ex.TypeId, Is.EqualTo("Builtin"));
            Assert.That(ex.RegistryName, Is.EqualTo("IProviderRegistry"));
        }

        // --- Resolve 成功 (Req 9.2) ---

        [Test]
        public void Resolve_RegisteredTypeId_ReturnsProviderFromFactory()
        {
            var expectedProvider = new StubAvatarProvider();
            var factory = new StubAvatarProviderFactory { ProviderToReturn = expectedProvider };
            _registry.Register("Builtin", factory);

            var descriptor = new AvatarProviderDescriptor { ProviderTypeId = "Builtin" };
            var provider = _registry.Resolve(descriptor);

            Assert.That(provider, Is.SameAs(expectedProvider));
        }

        [Test]
        public void Resolve_PassesDescriptorConfigToFactory()
        {
            ProviderConfigBase capturedConfig = null;
            var factory = new StubAvatarProviderFactory
            {
                OnCreate = config => capturedConfig = config
            };
            _registry.Register("Builtin", factory);

            var config = UnityEngine.ScriptableObject.CreateInstance<StubProviderConfig>();
            var descriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = "Builtin",
                Config = config
            };
            _registry.Resolve(descriptor);

            Assert.That(capturedConfig, Is.SameAs(config));

            UnityEngine.Object.DestroyImmediate(config);
        }

        // --- 未登録 typeId Resolve で KeyNotFoundException (Req 9.3) ---

        [Test]
        public void Resolve_UnregisteredTypeId_ThrowsKeyNotFoundException()
        {
            var descriptor = new AvatarProviderDescriptor { ProviderTypeId = "Unknown" };

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
            _registry.Register("Builtin", new StubAvatarProviderFactory());
            _registry.Register("Addressable", new StubAvatarProviderFactory());
            _registry.Register("Custom", new StubAvatarProviderFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(3));
            Assert.That(typeIds, Contains.Item("Builtin"));
            Assert.That(typeIds, Contains.Item("Addressable"));
            Assert.That(typeIds, Contains.Item("Custom"));
        }

        [Test]
        public void GetRegisteredTypeIds_ReturnsReadOnlyList()
        {
            _registry.Register("Builtin", new StubAvatarProviderFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Is.InstanceOf<IReadOnlyList<string>>());
        }

        // --- Stub implementations for testing ---

        private class StubProviderConfig : ProviderConfigBase { }

        private class StubAvatarProvider : IAvatarProvider
        {
            public string ProviderType => "Stub";
            public UnityEngine.GameObject RequestAvatar(ProviderConfigBase config) => null;
            public Cysharp.Threading.Tasks.UniTask<UnityEngine.GameObject> RequestAvatarAsync(
                ProviderConfigBase config, System.Threading.CancellationToken cancellationToken = default)
                => Cysharp.Threading.Tasks.UniTask.FromResult<UnityEngine.GameObject>(null);
            public void ReleaseAvatar(UnityEngine.GameObject avatar) { }
            public void Dispose() { }
        }

        private class StubAvatarProviderFactory : IAvatarProviderFactory
        {
            public StubAvatarProvider ProviderToReturn { get; set; } = new StubAvatarProvider();
            public Action<ProviderConfigBase> OnCreate { get; set; }

            public IAvatarProvider Create(ProviderConfigBase config)
            {
                OnCreate?.Invoke(config);
                return ProviderToReturn;
            }
        }
    }
}
