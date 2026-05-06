using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// DefaultMoCapSourceRegistry の EditMode テスト。
    /// テスト観点:
    ///   - Register 成功 / 同一 typeId 競合で RegistryConflictException (Req 9.9)
    ///   - Resolve 成功 / 同一 Descriptor → 同一インスタンス返却 (Req 10.1)
    ///   - 参照カウントの増減 (Resolve で +1 / Release で -1)
    ///   - 参照カウントが 0 になった時点で IMoCapSource.Dispose() が呼ばれる (Req 10.3)
    ///   - 未登録 typeId Resolve で KeyNotFoundException (Req 9.6)
    ///   - GetRegisteredTypeIds の内容確認 (Req 9.7)
    /// Requirements: 9.4, 9.5, 9.6, 9.9, 10.1, 10.3
    /// </summary>
    [TestFixture]
    public class MoCapSourceRegistryTests
    {
        private DefaultMoCapSourceRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _registry = new DefaultMoCapSourceRegistry();
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
            var factory = new StubMoCapSourceFactory();
            Assert.DoesNotThrow(() => _registry.Register("VMC", factory));
        }

        [Test]
        public void Register_MultipleTypeIds_Succeeds()
        {
            _registry.Register("VMC", new StubMoCapSourceFactory());
            _registry.Register("MotionBuilder", new StubMoCapSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(2));
            Assert.That(typeIds, Contains.Item("VMC"));
            Assert.That(typeIds, Contains.Item("MotionBuilder"));
        }

        // --- 同一 typeId 競合で RegistryConflictException (Req 9.9) ---

        [Test]
        public void Register_DuplicateTypeId_ThrowsRegistryConflictException()
        {
            _registry.Register("VMC", new StubMoCapSourceFactory());

            var ex = Assert.Throws<RegistryConflictException>(
                () => _registry.Register("VMC", new StubMoCapSourceFactory()));
            Assert.That(ex.TypeId, Is.EqualTo("VMC"));
            Assert.That(ex.RegistryName, Is.EqualTo("IMoCapSourceRegistry"));
        }

        // --- Resolve 成功 (Req 9.5) ---

        [Test]
        public void Resolve_RegisteredTypeId_ReturnsSourceFromFactory()
        {
            var expectedSource = new StubMoCapSource();
            var factory = new StubMoCapSourceFactory { SourceToReturn = expectedSource };
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };
                var source = _registry.Resolve(descriptor);

                Assert.That(source, Is.SameAs(expectedSource));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Resolve_PassesDescriptorConfigToFactory()
        {
            MoCapSourceConfigBase capturedConfig = null;
            var factory = new StubMoCapSourceFactory
            {
                OnCreate = config => capturedConfig = config
            };
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };
                _registry.Resolve(descriptor);

                Assert.That(capturedConfig, Is.SameAs(config));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // --- 参照共有 (Req 10.1) ---

        [Test]
        public void Resolve_SameDescriptor_ReturnsSameInstance()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };

                var source1 = _registry.Resolve(descriptor);
                var source2 = _registry.Resolve(descriptor);

                Assert.That(source2, Is.SameAs(source1));
                Assert.That(factory.CreateCallCount, Is.EqualTo(1),
                    "同一 Descriptor で 2 回目の Resolve() 時は Factory.Create が呼ばれないこと");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Resolve_EquivalentDescriptor_ReturnsSameInstance()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptorA = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };
                var descriptorB = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };

                var source1 = _registry.Resolve(descriptorA);
                var source2 = _registry.Resolve(descriptorB);

                Assert.That(source2, Is.SameAs(source1));
                Assert.That(factory.CreateCallCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Resolve_DifferentDescriptors_ReturnsDifferentInstances()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var configA = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            var configB = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptorA = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = configA };
                var descriptorB = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = configB };

                var sourceA = _registry.Resolve(descriptorA);
                var sourceB = _registry.Resolve(descriptorB);

                Assert.That(sourceB, Is.Not.SameAs(sourceA));
                Assert.That(factory.CreateCallCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configA);
                UnityEngine.Object.DestroyImmediate(configB);
            }
        }

        // --- 未登録 typeId Resolve で KeyNotFoundException (Req 9.6) ---

        [Test]
        public void Resolve_UnregisteredTypeId_ThrowsKeyNotFoundException()
        {
            var descriptor = new MoCapSourceDescriptor { SourceTypeId = "Unknown" };

            Assert.Throws<KeyNotFoundException>(() => _registry.Resolve(descriptor));
        }

        // --- Release と参照カウント / Dispose (Req 10.3) ---

        [Test]
        public void Release_DecrementsRefCount_DoesNotDisposeUntilZero()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };

                var source = (StubMoCapSource)_registry.Resolve(descriptor);
                _registry.Resolve(descriptor);

                _registry.Release(source);

                Assert.That(source.DisposeCallCount, Is.EqualTo(0),
                    "参照カウントが 0 になる前に Dispose() が呼ばれてはならない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Release_WhenRefCountReachesZero_DisposesSource()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };

                var source = (StubMoCapSource)_registry.Resolve(descriptor);
                _registry.Resolve(descriptor);

                _registry.Release(source);
                _registry.Release(source);

                Assert.That(source.DisposeCallCount, Is.EqualTo(1),
                    "参照カウントが 0 になった時点で Dispose() がちょうど 1 回呼ばれること");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Release_AfterDispose_ResolvingAgainCreatesNewInstance()
        {
            var factory = new StubMoCapSourceFactory();
            _registry.Register("VMC", factory);

            var config = ScriptableObject.CreateInstance<StubMoCapSourceConfig>();
            try
            {
                var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = config };

                var source1 = _registry.Resolve(descriptor);
                _registry.Release(source1);

                var source2 = _registry.Resolve(descriptor);

                Assert.That(source2, Is.Not.SameAs(source1),
                    "Dispose 済みインスタンスが再利用されてはならない");
                Assert.That(factory.CreateCallCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Release_UnmanagedSource_DoesNotThrow()
        {
            // 登録外の IMoCapSource を Release しても例外にならないことを確認する
            // (SlotManager の解放処理が冪等であるために必要)
            var stranger = new StubMoCapSource();
            Assert.DoesNotThrow(() => _registry.Release(stranger));
            Assert.That(stranger.DisposeCallCount, Is.EqualTo(0));
        }

        // --- GetRegisteredTypeIds (Req 9.7) ---

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
            _registry.Register("VMC", new StubMoCapSourceFactory());
            _registry.Register("MotionBuilder", new StubMoCapSourceFactory());
            _registry.Register("Custom", new StubMoCapSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Has.Count.EqualTo(3));
            Assert.That(typeIds, Contains.Item("VMC"));
            Assert.That(typeIds, Contains.Item("MotionBuilder"));
            Assert.That(typeIds, Contains.Item("Custom"));
        }

        [Test]
        public void GetRegisteredTypeIds_ReturnsReadOnlyList()
        {
            _registry.Register("VMC", new StubMoCapSourceFactory());

            var typeIds = _registry.GetRegisteredTypeIds();
            Assert.That(typeIds, Is.InstanceOf<IReadOnlyList<string>>());
        }

        // --- Stub implementations for testing ---

        private class StubMoCapSourceConfig : MoCapSourceConfigBase { }

        private class StubMoCapSource : IMoCapSource
        {
            public int DisposeCallCount { get; private set; }
            public string SourceType => "Stub";
            public IObservable<MotionFrame> MotionStream => null;
            public void Initialize(MoCapSourceConfigBase config) { }
            public void Shutdown() { }
            public void Dispose() => DisposeCallCount++;
        }

        private class StubMoCapSourceFactory : IMoCapSourceFactory
        {
            public StubMoCapSource SourceToReturn { get; set; }
            public Action<MoCapSourceConfigBase> OnCreate { get; set; }
            public int CreateCallCount { get; private set; }

            public IMoCapSource Create(MoCapSourceConfigBase config)
            {
                CreateCallCount++;
                OnCreate?.Invoke(config);
                // 各呼び出しで新しいインスタンスを返す (参照共有は Registry の責務)
                return SourceToReturn ?? new StubMoCapSource();
            }

            public MoCapSourceConfigBase CreateDefaultConfig() => null;

            public IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config) => new NoopDisposable();

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
