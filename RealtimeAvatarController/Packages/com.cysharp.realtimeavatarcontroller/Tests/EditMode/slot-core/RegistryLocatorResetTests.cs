using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
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

        [Test]
        public void ResetForTest_AfterReset_RegistriesAreAccessible()
        {
            RegistryLocator.ResetForTest();

            Assert.DoesNotThrow(() => { var _ = RegistryLocator.ProviderRegistry; });
            Assert.DoesNotThrow(() => { var _ = RegistryLocator.MoCapSourceRegistry; });
        }
    }
}
