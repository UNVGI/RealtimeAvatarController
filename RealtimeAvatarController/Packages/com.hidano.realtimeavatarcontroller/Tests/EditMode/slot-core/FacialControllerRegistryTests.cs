using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// DefaultFacialControllerRegistry の EditMode テスト。
    /// テスト観点:
    ///   - Register 成功
    ///   - 同一 typeId 競合で RegistryConflictException
    ///   - Resolve 成功 (Factory.Create が呼ばれる)
    ///   - 未登録 typeId Resolve で KeyNotFoundException
    ///   - GetRegisteredTypeIds 結果確認
    /// Requirements: 9.1, 9.9
    /// </summary>
    [TestFixture]
    public class FacialControllerRegistryTests
    {
        private DefaultFacialControllerRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _registry = new DefaultFacialControllerRegistry();
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
            var factory = new StubFacialControllerFactory();
            Assert.DoesNotThrow(() => _registry.Register("BlendShape", factory));
        }

        [Test]
        public void Register_MultipleTypeIds_Succeeds()
        {
            _registry.Register("BlendShape", new StubFacialControllerFactory());
            _registry.Register("ARKit", new StubFacialControllerFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(2));
            Assert.That(typeIds, Contains.Item("BlendShape"));
            Assert.That(typeIds, Contains.Item("ARKit"));
        }

        // --- 同一 typeId 競合で RegistryConflictException (Req 9.9) ---

        [Test]
        public void Register_DuplicateTypeId_ThrowsRegistryConflictException()
        {
            _registry.Register("BlendShape", new StubFacialControllerFactory());

            var ex = Assert.Throws<RegistryConflictException>(
                () => _registry.Register("BlendShape", new StubFacialControllerFactory()));
            Assert.That(ex.TypeId, Is.EqualTo("BlendShape"));
            Assert.That(ex.RegistryName, Is.EqualTo("IFacialControllerRegistry"));
        }

        // --- Resolve 成功 (Req 9.1) ---

        [Test]
        public void Resolve_RegisteredTypeId_ReturnsControllerFromFactory()
        {
            var expected = new StubFacialController();
            var factory = new StubFacialControllerFactory { ControllerToReturn = expected };
            _registry.Register("BlendShape", factory);

            var descriptor = new FacialControllerDescriptor { ControllerTypeId = "BlendShape" };
            var controller = _registry.Resolve(descriptor);

            Assert.That(controller, Is.SameAs(expected));
        }

        [Test]
        public void Resolve_PassesDescriptorConfigToFactory()
        {
            FacialControllerConfigBase capturedConfig = null;
            var factory = new StubFacialControllerFactory
            {
                OnCreate = config => capturedConfig = config
            };
            _registry.Register("BlendShape", factory);

            var config = UnityEngine.ScriptableObject.CreateInstance<StubFacialControllerConfig>();
            var descriptor = new FacialControllerDescriptor
            {
                ControllerTypeId = "BlendShape",
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
            var descriptor = new FacialControllerDescriptor { ControllerTypeId = "Unknown" };

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
            _registry.Register("BlendShape", new StubFacialControllerFactory());
            _registry.Register("ARKit", new StubFacialControllerFactory());
            _registry.Register("Custom", new StubFacialControllerFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(3));
            Assert.That(typeIds, Contains.Item("BlendShape"));
            Assert.That(typeIds, Contains.Item("ARKit"));
            Assert.That(typeIds, Contains.Item("Custom"));
        }

        [Test]
        public void GetRegisteredTypeIds_ReturnsReadOnlyList()
        {
            _registry.Register("BlendShape", new StubFacialControllerFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Is.InstanceOf<IReadOnlyList<string>>());
        }

        // --- Stub implementations for testing ---

        private class StubFacialControllerConfig : FacialControllerConfigBase { }

        private class StubFacialController : IFacialController
        {
            public void Initialize(UnityEngine.GameObject avatarRoot) { }
            public void ApplyFacialData(object facialData) { }
            public void Shutdown() { }
            public void Dispose() { }
        }

        private class StubFacialControllerFactory : IFacialControllerFactory
        {
            public StubFacialController ControllerToReturn { get; set; } = new StubFacialController();
            public Action<FacialControllerConfigBase> OnCreate { get; set; }

            public IFacialController Create(FacialControllerConfigBase config)
            {
                OnCreate?.Invoke(config);
                return ControllerToReturn;
            }
        }
    }
}
