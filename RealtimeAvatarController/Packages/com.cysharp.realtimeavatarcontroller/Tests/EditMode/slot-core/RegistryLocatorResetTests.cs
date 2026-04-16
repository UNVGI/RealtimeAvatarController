using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// RegistryLocator.ResetForTest() の SubsystemRegistration 自動実行検証 (Task 13.2)。
    /// EditMode テストでは [RuntimeInitializeOnLoadMethod] 属性は実行されないため、
    /// 起動タイミングのシミュレートは ResetForTest() の直接呼び出しで代替する。
    /// 同時に、属性が正しく付与されていることをリフレクションで検証することで、
    /// Domain Reload OFF (Enter Play Mode 最適化) 時の自動リセット配線が破損していないことを保証する。
    /// Requirements: 11.3, 9.8
    /// </summary>
    [TestFixture]
    public class RegistryLocatorResetTests
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

        // --- SubsystemRegistration 自動実行配線の検証 (リフレクション) ---

        [Test]
        public void ResetForTest_HasRuntimeInitializeOnLoadMethodAttribute()
        {
            var method = typeof(RegistryLocator).GetMethod(
                nameof(RegistryLocator.ResetForTest),
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "ResetForTest メソッドが見つからない");

            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attribute, Is.Not.Null,
                "ResetForTest に [RuntimeInitializeOnLoadMethod] 属性が付与されていない");
            Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration),
                "ResetForTest の RuntimeInitializeLoadType は SubsystemRegistration である必要がある");
        }

        // --- SubsystemRegistration タイミングを直接呼び出しで代替し、効果を検証する ---

        [Test]
        public void ResetForTest_SimulatedSubsystemRegistration_ReplacesProviderRegistryWithNewInstance()
        {
            // 起動前にインスタンスを生成しておく (前フレーム / 前 Domain の残骸を模倣)
            var before = RegistryLocator.ProviderRegistry;
            Assert.That(before, Is.Not.Null);

            // SubsystemRegistration 自動実行と等価な処理を直接呼び出して代替する
            RegistryLocator.ResetForTest();

            var after = RegistryLocator.ProviderRegistry;
            Assert.That(after, Is.Not.Null);
            Assert.That(after, Is.Not.SameAs(before),
                "ResetForTest 後の ProviderRegistry は新インスタンスでなければならない");
            Assert.That(after, Is.InstanceOf<DefaultProviderRegistry>());
        }

        [Test]
        public void ResetForTest_SimulatedSubsystemRegistration_ClearsSuppressedErrors()
        {
            // 起動前に抑制エントリを残しておく (前 Domain の残骸を模倣)
            RegistryLocator.s_suppressedErrors.Add(("slotA", SlotErrorCategory.InitFailure));
            RegistryLocator.s_suppressedErrors.Add(("slotB", SlotErrorCategory.RegistryConflict));
            Assert.That(RegistryLocator.s_suppressedErrors, Is.Not.Empty,
                "前提: ResetForTest 呼び出し前に抑制エントリが存在すること");

            // SubsystemRegistration 自動実行と等価な処理を直接呼び出して代替する
            RegistryLocator.ResetForTest();

            Assert.That(RegistryLocator.s_suppressedErrors, Is.Empty,
                "ResetForTest 後は s_suppressedErrors がクリアされていなければならない");
        }

        [Test]
        public void ResetForTest_AfterReset_RegistriesAreAccessible()
        {
            RegistryLocator.ResetForTest();

            Assert.DoesNotThrow(() => { var _ = RegistryLocator.ProviderRegistry; });
            Assert.DoesNotThrow(() => { var _ = RegistryLocator.MoCapSourceRegistry; });
        }
    }
}
