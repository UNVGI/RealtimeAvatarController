using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <see cref="VMCMoCapSourceFactory"/> 経由で <see cref="IMoCapSourceRegistry.Resolve"/> が
    /// 同一 <see cref="VMCMoCapSourceConfig"/> に対して同一 <see cref="EVMC4UMoCapSource"/> を
    /// 共有することを検証する PlayMode テスト
    /// (tasks.md タスク 5.3 / design.md §4.5 / requirements.md 要件 2.1, 5.6, 12.4, 12.5)。
    ///
    /// <para>
    /// 検証観点:
    ///   - 同一 <see cref="VMCMoCapSourceConfig"/> インスタンスを持つ <see cref="MoCapSourceDescriptor"/>
    ///     を 2 回 Resolve すると、参照等価 (<see cref="object.ReferenceEquals"/>) な
    ///     <see cref="EVMC4UMoCapSource"/> が返される (要件 5.6 / Descriptor の Config 参照等価)。
    ///   - 返却された Adapter は <see cref="EVMC4UMoCapSource"/> 型である (要件 5.5)。
    ///   - 別インスタンスの <see cref="VMCMoCapSourceConfig"/> (port 違い) に対しては
    ///     別の Adapter が返る (参照共有の粒度は Config 単位)。
    /// </para>
    ///
    /// <para>
    /// テスト独立性: <see cref="RegistryLocator.ResetForTest"/> を <c>[SetUp]</c>/<c>[TearDown]</c>
    /// の両方で呼び出す (要件 12.5)。<see cref="VMCMoCapSourceFactory"/> は
    /// <c>[RuntimeInitializeOnLoadMethod]</c> による自己登録に依存せず、
    /// <c>[SetUp]</c> で明示的に <see cref="IMoCapSourceRegistry.Register"/> する。
    /// Adapter は <see cref="IMoCapSource.Initialize"/> を呼ばない (実ポート bind を避けるため)
    /// — Registry の参照共有は Config 参照等価のみで判定されるため Initialize 前でも検証可能。
    /// </para>
    /// </summary>
    [TestFixture]
    public class EVMC4UMoCapSourceSharingTests
    {
        private VMCMoCapSourceConfig _configA;
        private VMCMoCapSourceConfig _configB;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            RegistryLocator.MoCapSourceRegistry.Register(
                VMCMoCapSourceFactory.VmcSourceTypeId,
                new VMCMoCapSourceFactory());

            _configA = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _configA.port = 49510;

            _configB = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _configB.port = 49511;
        }

        [TearDown]
        public void TearDown()
        {
            if (_configA != null)
            {
                UnityEngine.Object.DestroyImmediate(_configA);
                _configA = null;
            }

            if (_configB != null)
            {
                UnityEngine.Object.DestroyImmediate(_configB);
                _configB = null;
            }

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Resolve_SameConfigTwice_ReturnsSameEVMC4UMoCapSourceInstance()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;

            var descriptor1 = new MoCapSourceDescriptor
            {
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = _configA,
            };
            var descriptor2 = new MoCapSourceDescriptor
            {
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = _configA,
            };

            var first = registry.Resolve(descriptor1);
            var second = registry.Resolve(descriptor2);

            try
            {
                Assert.That(first, Is.InstanceOf<EVMC4UMoCapSource>(),
                    "Factory.Create 経由で Resolve される型は EVMC4UMoCapSource であるべき (要件 5.5)。");
                Assert.That(second, Is.SameAs(first),
                    "同一 VMCMoCapSourceConfig に対する 2 回の Resolve は参照等価な同一 Adapter を返すべき (要件 2.1, 5.6)。");
            }
            finally
            {
                registry.Release(second);
                registry.Release(first);
            }
        }

        [Test]
        public void Resolve_DifferentConfigs_ReturnsDifferentEVMC4UMoCapSourceInstances()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;

            var descriptorA = new MoCapSourceDescriptor
            {
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = _configA,
            };
            var descriptorB = new MoCapSourceDescriptor
            {
                SourceTypeId = VMCMoCapSourceFactory.VmcSourceTypeId,
                Config = _configB,
            };

            var adapterA = registry.Resolve(descriptorA);
            var adapterB = registry.Resolve(descriptorB);

            try
            {
                Assert.That(adapterA, Is.InstanceOf<EVMC4UMoCapSource>(),
                    "port=49510 の Config に対しても EVMC4UMoCapSource が返されるべき (要件 5.5)。");
                Assert.That(adapterB, Is.InstanceOf<EVMC4UMoCapSource>(),
                    "port=49511 の Config に対しても EVMC4UMoCapSource が返されるべき (要件 5.5)。");
                Assert.That(adapterB, Is.Not.SameAs(adapterA),
                    "別 Config インスタンスに対しては別の Adapter が返されるべき (参照共有の粒度は Config 単位 / 要件 5.6)。");
            }
            finally
            {
                registry.Release(adapterA);
                registry.Release(adapterB);
            }
        }
    }
}
