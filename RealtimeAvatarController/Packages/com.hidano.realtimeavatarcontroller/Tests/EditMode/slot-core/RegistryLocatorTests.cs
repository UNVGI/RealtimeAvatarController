using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// RegistryLocator の EditMode テスト。
    /// テスト観点:
    ///   - 最初のアクセスで既定インスタンスが生成されること (遅延初期化)
    ///   - 同一プロパティへの複数回アクセスで同一インスタンスが返ること
    ///   - ResetForTest() 後に新インスタンスが生成されること
    ///   - ResetForTest() で s_suppressedErrors がクリアされること
    ///   - Override*() でモック差し替えが反映されること
    ///   - [SetUp] / [TearDown] で ResetForTest を呼ぶこと
    /// Requirements: 11.3, 11.4, 11.5, 14.3
    /// </summary>
    [TestFixture]
    public class RegistryLocatorTests
    {
        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        // --- 遅延初期化: 最初のアクセスで既定インスタンスが生成される (Req 11.5) ---

        [Test]
        public void ProviderRegistry_FirstAccess_ReturnsDefaultInstance()
        {
            var registry = RegistryLocator.ProviderRegistry;

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry, Is.InstanceOf<DefaultProviderRegistry>());
        }

        [Test]
        public void MoCapSourceRegistry_FirstAccess_ReturnsDefaultInstance()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry, Is.InstanceOf<DefaultMoCapSourceRegistry>());
        }

        [Test]
        public void FacialControllerRegistry_FirstAccess_ReturnsDefaultInstance()
        {
            var registry = RegistryLocator.FacialControllerRegistry;

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry, Is.InstanceOf<DefaultFacialControllerRegistry>());
        }

        [Test]
        public void LipSyncSourceRegistry_FirstAccess_ReturnsDefaultInstance()
        {
            var registry = RegistryLocator.LipSyncSourceRegistry;

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry, Is.InstanceOf<DefaultLipSyncSourceRegistry>());
        }

        [Test]
        public void ErrorChannel_FirstAccess_ReturnsDefaultInstance()
        {
            var channel = RegistryLocator.ErrorChannel;

            Assert.That(channel, Is.Not.Null);
            Assert.That(channel, Is.InstanceOf<DefaultSlotErrorChannel>());
        }

        // --- 遅延初期化: 複数回アクセスで同一インスタンスが返る ---

        [Test]
        public void ProviderRegistry_MultipleAccess_ReturnsSameInstance()
        {
            var first = RegistryLocator.ProviderRegistry;
            var second = RegistryLocator.ProviderRegistry;

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void MoCapSourceRegistry_MultipleAccess_ReturnsSameInstance()
        {
            var first = RegistryLocator.MoCapSourceRegistry;
            var second = RegistryLocator.MoCapSourceRegistry;

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void FacialControllerRegistry_MultipleAccess_ReturnsSameInstance()
        {
            var first = RegistryLocator.FacialControllerRegistry;
            var second = RegistryLocator.FacialControllerRegistry;

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void LipSyncSourceRegistry_MultipleAccess_ReturnsSameInstance()
        {
            var first = RegistryLocator.LipSyncSourceRegistry;
            var second = RegistryLocator.LipSyncSourceRegistry;

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void ErrorChannel_MultipleAccess_ReturnsSameInstance()
        {
            var first = RegistryLocator.ErrorChannel;
            var second = RegistryLocator.ErrorChannel;

            Assert.That(second, Is.SameAs(first));
        }

        // --- ResetForTest 後に新インスタンスが生成される (Req 11.4) ---

        [Test]
        public void ResetForTest_ProviderRegistry_CreatesNewInstanceAfterReset()
        {
            var before = RegistryLocator.ProviderRegistry;

            RegistryLocator.ResetForTest();
            var after = RegistryLocator.ProviderRegistry;

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after, Is.InstanceOf<DefaultProviderRegistry>());
        }

        [Test]
        public void ResetForTest_MoCapSourceRegistry_CreatesNewInstanceAfterReset()
        {
            var before = RegistryLocator.MoCapSourceRegistry;

            RegistryLocator.ResetForTest();
            var after = RegistryLocator.MoCapSourceRegistry;

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after, Is.InstanceOf<DefaultMoCapSourceRegistry>());
        }

        [Test]
        public void ResetForTest_FacialControllerRegistry_CreatesNewInstanceAfterReset()
        {
            var before = RegistryLocator.FacialControllerRegistry;

            RegistryLocator.ResetForTest();
            var after = RegistryLocator.FacialControllerRegistry;

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after, Is.InstanceOf<DefaultFacialControllerRegistry>());
        }

        [Test]
        public void ResetForTest_LipSyncSourceRegistry_CreatesNewInstanceAfterReset()
        {
            var before = RegistryLocator.LipSyncSourceRegistry;

            RegistryLocator.ResetForTest();
            var after = RegistryLocator.LipSyncSourceRegistry;

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after, Is.InstanceOf<DefaultLipSyncSourceRegistry>());
        }

        [Test]
        public void ResetForTest_ErrorChannel_CreatesNewInstanceAfterReset()
        {
            var before = RegistryLocator.ErrorChannel;

            RegistryLocator.ResetForTest();
            var after = RegistryLocator.ErrorChannel;

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after, Is.InstanceOf<DefaultSlotErrorChannel>());
        }

        // --- ResetForTest で s_suppressedErrors がクリアされる (Req 11.4) ---

        [Test]
        public void ResetForTest_ClearsSuppressedErrorsHashSet()
        {
            RegistryLocator.s_suppressedErrors.Add(("slotA", SlotErrorCategory.InitFailure));
            RegistryLocator.s_suppressedErrors.Add(("slotB", SlotErrorCategory.ApplyFailure));
            Assert.That(RegistryLocator.s_suppressedErrors, Is.Not.Empty, "前提: ResetForTest 呼び出し前に要素が存在すること");

            RegistryLocator.ResetForTest();

            Assert.That(RegistryLocator.s_suppressedErrors, Is.Empty);
        }

        // --- Override*() でモック差し替えが反映される (Req 11.3) ---

        [Test]
        public void OverrideProviderRegistry_ReflectsMockInGetter()
        {
            var mock = new StubProviderRegistry();
            RegistryLocator.OverrideProviderRegistry(mock);

            Assert.That(RegistryLocator.ProviderRegistry, Is.SameAs(mock));
        }

        [Test]
        public void OverrideMoCapSourceRegistry_ReflectsMockInGetter()
        {
            var mock = new StubMoCapSourceRegistry();
            RegistryLocator.OverrideMoCapSourceRegistry(mock);

            Assert.That(RegistryLocator.MoCapSourceRegistry, Is.SameAs(mock));
        }

        [Test]
        public void OverrideFacialControllerRegistry_ReflectsMockInGetter()
        {
            var mock = new StubFacialControllerRegistry();
            RegistryLocator.OverrideFacialControllerRegistry(mock);

            Assert.That(RegistryLocator.FacialControllerRegistry, Is.SameAs(mock));
        }

        [Test]
        public void OverrideLipSyncSourceRegistry_ReflectsMockInGetter()
        {
            var mock = new StubLipSyncSourceRegistry();
            RegistryLocator.OverrideLipSyncSourceRegistry(mock);

            Assert.That(RegistryLocator.LipSyncSourceRegistry, Is.SameAs(mock));
        }

        [Test]
        public void OverrideErrorChannel_ReflectsMockInGetter()
        {
            var mock = new StubSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(mock);

            Assert.That(RegistryLocator.ErrorChannel, Is.SameAs(mock));
        }

        [Test]
        public void Override_AfterDefaultAccess_ReplacesExistingInstance()
        {
            // 先に既定インスタンスを生成させる
            var defaultInstance = RegistryLocator.ProviderRegistry;
            Assert.That(defaultInstance, Is.InstanceOf<DefaultProviderRegistry>());

            var mock = new StubProviderRegistry();
            RegistryLocator.OverrideProviderRegistry(mock);

            Assert.That(RegistryLocator.ProviderRegistry, Is.SameAs(mock));
            Assert.That(RegistryLocator.ProviderRegistry, Is.Not.SameAs(defaultInstance));
        }

        [Test]
        public void ResetForTest_AfterOverride_RestoresDefaultOnNextAccess()
        {
            var mock = new StubProviderRegistry();
            RegistryLocator.OverrideProviderRegistry(mock);
            Assert.That(RegistryLocator.ProviderRegistry, Is.SameAs(mock));

            RegistryLocator.ResetForTest();

            var restored = RegistryLocator.ProviderRegistry;
            Assert.That(restored, Is.Not.SameAs(mock));
            Assert.That(restored, Is.InstanceOf<DefaultProviderRegistry>());
        }

        // --- Stub implementations for Override tests ---

        private class StubProviderRegistry : IProviderRegistry
        {
            public void Register(string providerTypeId, IAvatarProviderFactory factory) { }
            public IAvatarProvider Resolve(AvatarProviderDescriptor descriptor) => null;
            public IReadOnlyList<string> GetRegisteredTypeIds() => Array.Empty<string>();
        }

        private class StubMoCapSourceRegistry : IMoCapSourceRegistry
        {
            public void Register(string sourceTypeId, IMoCapSourceFactory factory) { }
            public IMoCapSource Resolve(MoCapSourceDescriptor descriptor) => null;
            public void Release(IMoCapSource source) { }
            public IReadOnlyList<string> GetRegisteredTypeIds() => Array.Empty<string>();
            public bool TryGetFactory(string sourceTypeId, out IMoCapSourceFactory factory) { factory = null; return false; }
        }

        private class StubFacialControllerRegistry : IFacialControllerRegistry
        {
            public void Register(string controllerTypeId, IFacialControllerFactory factory) { }
            public IFacialController Resolve(FacialControllerDescriptor descriptor) => null;
            public IReadOnlyList<string> GetRegisteredTypeIds() => Array.Empty<string>();
        }

        private class StubLipSyncSourceRegistry : ILipSyncSourceRegistry
        {
            public void Register(string sourceTypeId, ILipSyncSourceFactory factory) { }
            public ILipSyncSource Resolve(LipSyncSourceDescriptor descriptor) => null;
            public IReadOnlyList<string> GetRegisteredTypeIds() => Array.Empty<string>();
        }

        private class StubSlotErrorChannel : ISlotErrorChannel
        {
            private readonly Subject<SlotError> _subject = new Subject<SlotError>();
            public IObservable<SlotError> Errors => _subject;
            public void Publish(SlotError error) => _subject.OnNext(error);
        }
    }
}
