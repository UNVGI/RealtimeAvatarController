using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    public class ConfigBaseTests
    {
        /// <summary>
        /// テスト用の具象サブクラス。abstract クラスは直接 CreateInstance できないため。
        /// </summary>
        private class ConcreteProviderConfig : ProviderConfigBase { }

        [Test]
        public void ProviderConfigBase_InheritsFromScriptableObject()
        {
            Assert.That(typeof(ProviderConfigBase).IsSubclassOf(typeof(ScriptableObject)), Is.True);
        }

        [Test]
        public void ProviderConfigBase_IsAbstract()
        {
            Assert.That(typeof(ProviderConfigBase).IsAbstract, Is.True);
        }

        [Test]
        public void ProviderConfigBase_ConcreteSubclass_CanBeCreatedViaCreateInstance()
        {
            var instance = ScriptableObject.CreateInstance<ConcreteProviderConfig>();
            try
            {
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance, Is.InstanceOf<ProviderConfigBase>());
                Assert.That(instance, Is.InstanceOf<ScriptableObject>());
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
