using System;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinMoCapSourceFactoryTests
    {
        private MovinMoCapSourceConfig _config;
        private OtherMoCapSourceConfig _otherConfig;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            if (_otherConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_otherConfig);
                _otherConfig = null;
            }

            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Create_WithMovinConfigAsBase_ReturnsMovinMoCapSource()
        {
            var factory = new MovinMoCapSourceFactory();
            MoCapSourceConfigBase config = _config;

            var source = factory.Create(config);

            Assert.That(source, Is.InstanceOf<MovinMoCapSource>());
            Assert.That(source.SourceType, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
        }

        [Test]
        public void Create_WithWrongConfigType_ThrowsArgumentException_WithTypeNameInMessage()
        {
            var factory = new MovinMoCapSourceFactory();
            _otherConfig = ScriptableObject.CreateInstance<OtherMoCapSourceConfig>();

            var ex = Assert.Throws<ArgumentException>(() => factory.Create(_otherConfig));

            Assert.That(ex.Message, Does.Contain(nameof(OtherMoCapSourceConfig)));
        }

        [Test]
        public void Create_WithNullConfig_ThrowsArgumentException()
        {
            var factory = new MovinMoCapSourceFactory();

            var ex = Assert.Throws<ArgumentException>(() => factory.Create(null));

            Assert.That(ex.Message, Does.Contain("null"));
        }

        private sealed class OtherMoCapSourceConfig : MoCapSourceConfigBase
        {
        }
    }
}
