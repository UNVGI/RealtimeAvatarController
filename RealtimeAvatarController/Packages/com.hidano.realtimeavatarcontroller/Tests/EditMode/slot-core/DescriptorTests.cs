using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    public class AvatarProviderDescriptorTests
    {
        private class ConcreteProviderConfig : ProviderConfigBase { }

        private ConcreteProviderConfig _configA;
        private ConcreteProviderConfig _configB;

        [SetUp]
        public void SetUp()
        {
            _configA = ScriptableObject.CreateInstance<ConcreteProviderConfig>();
            _configB = ScriptableObject.CreateInstance<ConcreteProviderConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_configA != null) Object.DestroyImmediate(_configA);
            if (_configB != null) Object.DestroyImmediate(_configB);
        }

        [Test]
        public void IsSerializable()
        {
            Assert.That(typeof(AvatarProviderDescriptor).GetCustomAttributes(typeof(System.SerializableAttribute), false).Length, Is.GreaterThan(0));
        }

        [Test]
        public void IsSealed()
        {
            Assert.That(typeof(AvatarProviderDescriptor).IsSealed, Is.True);
        }

        [Test]
        public void ImplementsIEquatable()
        {
            Assert.That(typeof(System.IEquatable<AvatarProviderDescriptor>).IsAssignableFrom(typeof(AvatarProviderDescriptor)), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_SameConfigReference_ReturnsTrue()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_DifferentConfigReference_ReturnsFalse()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentTypeId_SameConfigReference_ReturnsFalse()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Addressable", Config = _configA };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a.Equals(null), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_WorksCorrectly()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.Equals((object)null), Is.False);
            Assert.That(a.Equals("not a descriptor"), Is.False);
        }

        [Test]
        public void GetHashCode_SameTypeIdAndConfig_ReturnsSameValue()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentConfig_MayDiffer()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configB };

            // Different references should typically produce different hash codes
            // (not guaranteed but highly likely with RuntimeHelpers.GetHashCode)
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void OperatorEquals_BothNull_ReturnsTrue()
        {
            AvatarProviderDescriptor a = null;
            AvatarProviderDescriptor b = null;

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void OperatorEquals_OneNull_ReturnsFalse()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a == null, Is.False);
            Assert.That(null == a, Is.False);
        }

        [Test]
        public void OperatorNotEquals_DifferentDescriptors_ReturnsTrue()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configB };

            Assert.That(a != b, Is.True);
        }

        [Test]
        public void OperatorEquals_SameValues_ReturnsTrue()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void CanBeUsedAsDictionaryKey()
        {
            var descriptor = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var dict = new Dictionary<AvatarProviderDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            Assert.That(dict.ContainsKey(lookupKey), Is.True);
            Assert.That(dict[lookupKey], Is.EqualTo("value"));
        }

        [Test]
        public void DictionaryKey_DifferentConfig_NotFound()
        {
            var descriptor = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var dict = new Dictionary<AvatarProviderDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configB };
            Assert.That(dict.ContainsKey(lookupKey), Is.False);
        }

        [Test]
        public void GetHashCode_NullTypeIdAndConfig_DoesNotThrow()
        {
            var descriptor = new AvatarProviderDescriptor { ProviderTypeId = null, Config = null };

            Assert.DoesNotThrow(() => descriptor.GetHashCode());
        }

        [Test]
        public void Equals_NullTypeIdFields_WorksCorrectly()
        {
            var a = new AvatarProviderDescriptor { ProviderTypeId = null, Config = null };
            var b = new AvatarProviderDescriptor { ProviderTypeId = null, Config = null };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void ConfigEquality_UsesReferenceEquals_NotValueEquals()
        {
            // Even if two configs are "logically equivalent", different instances should be non-equal
            var a = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configA };
            var b = new AvatarProviderDescriptor { ProviderTypeId = "Builtin", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }
    }

    public class MoCapSourceDescriptorTests
    {
        private class ConcreteMoCapSourceConfig : MoCapSourceConfigBase { }

        private ConcreteMoCapSourceConfig _configA;
        private ConcreteMoCapSourceConfig _configB;

        [SetUp]
        public void SetUp()
        {
            _configA = ScriptableObject.CreateInstance<ConcreteMoCapSourceConfig>();
            _configB = ScriptableObject.CreateInstance<ConcreteMoCapSourceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_configA != null) Object.DestroyImmediate(_configA);
            if (_configB != null) Object.DestroyImmediate(_configB);
        }

        [Test]
        public void IsSerializable()
        {
            Assert.That(typeof(MoCapSourceDescriptor).GetCustomAttributes(typeof(System.SerializableAttribute), false).Length, Is.GreaterThan(0));
        }

        [Test]
        public void IsSealed()
        {
            Assert.That(typeof(MoCapSourceDescriptor).IsSealed, Is.True);
        }

        [Test]
        public void ImplementsIEquatable()
        {
            Assert.That(typeof(System.IEquatable<MoCapSourceDescriptor>).IsAssignableFrom(typeof(MoCapSourceDescriptor)), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_SameConfigReference_ReturnsTrue()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_DifferentConfigReference_ReturnsFalse()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentTypeId_SameConfigReference_ReturnsFalse()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "MotionBuilder", Config = _configA };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a.Equals(null), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_WorksCorrectly()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.Equals((object)null), Is.False);
            Assert.That(a.Equals("not a descriptor"), Is.False);
        }

        [Test]
        public void GetHashCode_SameTypeIdAndConfig_ReturnsSameValue()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentConfig_MayDiffer()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configB };

            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void OperatorEquals_BothNull_ReturnsTrue()
        {
            MoCapSourceDescriptor a = null;
            MoCapSourceDescriptor b = null;

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void OperatorEquals_OneNull_ReturnsFalse()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a == null, Is.False);
            Assert.That(null == a, Is.False);
        }

        [Test]
        public void OperatorNotEquals_DifferentDescriptors_ReturnsTrue()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configB };

            Assert.That(a != b, Is.True);
        }

        [Test]
        public void OperatorEquals_SameValues_ReturnsTrue()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void CanBeUsedAsDictionaryKey()
        {
            var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var dict = new Dictionary<MoCapSourceDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            Assert.That(dict.ContainsKey(lookupKey), Is.True);
            Assert.That(dict[lookupKey], Is.EqualTo("value"));
        }

        [Test]
        public void DictionaryKey_DifferentConfig_NotFound()
        {
            var descriptor = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var dict = new Dictionary<MoCapSourceDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configB };
            Assert.That(dict.ContainsKey(lookupKey), Is.False);
        }

        [Test]
        public void GetHashCode_NullTypeIdAndConfig_DoesNotThrow()
        {
            var descriptor = new MoCapSourceDescriptor { SourceTypeId = null, Config = null };

            Assert.DoesNotThrow(() => descriptor.GetHashCode());
        }

        [Test]
        public void Equals_NullTypeIdFields_WorksCorrectly()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = null, Config = null };
            var b = new MoCapSourceDescriptor { SourceTypeId = null, Config = null };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void ConfigEquality_UsesReferenceEquals_NotValueEquals()
        {
            var a = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configA };
            var b = new MoCapSourceDescriptor { SourceTypeId = "VMC", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }
    }

    public class FacialControllerDescriptorTests
    {
        private class ConcreteFacialControllerConfig : FacialControllerConfigBase { }

        private ConcreteFacialControllerConfig _configA;
        private ConcreteFacialControllerConfig _configB;

        [SetUp]
        public void SetUp()
        {
            _configA = ScriptableObject.CreateInstance<ConcreteFacialControllerConfig>();
            _configB = ScriptableObject.CreateInstance<ConcreteFacialControllerConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_configA != null) Object.DestroyImmediate(_configA);
            if (_configB != null) Object.DestroyImmediate(_configB);
        }

        [Test]
        public void IsSerializable()
        {
            Assert.That(typeof(FacialControllerDescriptor).GetCustomAttributes(typeof(System.SerializableAttribute), false).Length, Is.GreaterThan(0));
        }

        [Test]
        public void IsSealed()
        {
            Assert.That(typeof(FacialControllerDescriptor).IsSealed, Is.True);
        }

        [Test]
        public void ImplementsIEquatable()
        {
            Assert.That(typeof(System.IEquatable<FacialControllerDescriptor>).IsAssignableFrom(typeof(FacialControllerDescriptor)), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_SameConfigReference_ReturnsTrue()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_DifferentConfigReference_ReturnsFalse()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentTypeId_SameConfigReference_ReturnsFalse()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "ARKit", Config = _configA };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a.Equals(null), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_WorksCorrectly()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.Equals((object)null), Is.False);
            Assert.That(a.Equals("not a descriptor"), Is.False);
        }

        [Test]
        public void GetHashCode_SameTypeIdAndConfig_ReturnsSameValue()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentConfig_MayDiffer()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configB };

            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void OperatorEquals_BothNull_ReturnsTrue()
        {
            FacialControllerDescriptor a = null;
            FacialControllerDescriptor b = null;

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void OperatorEquals_OneNull_ReturnsFalse()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a == null, Is.False);
            Assert.That(null == a, Is.False);
        }

        [Test]
        public void OperatorNotEquals_DifferentDescriptors_ReturnsTrue()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configB };

            Assert.That(a != b, Is.True);
        }

        [Test]
        public void OperatorEquals_SameValues_ReturnsTrue()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void CanBeUsedAsDictionaryKey()
        {
            var descriptor = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var dict = new Dictionary<FacialControllerDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            Assert.That(dict.ContainsKey(lookupKey), Is.True);
            Assert.That(dict[lookupKey], Is.EqualTo("value"));
        }

        [Test]
        public void DictionaryKey_DifferentConfig_NotFound()
        {
            var descriptor = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var dict = new Dictionary<FacialControllerDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configB };
            Assert.That(dict.ContainsKey(lookupKey), Is.False);
        }

        [Test]
        public void GetHashCode_NullTypeIdAndConfig_DoesNotThrow()
        {
            var descriptor = new FacialControllerDescriptor { ControllerTypeId = null, Config = null };

            Assert.DoesNotThrow(() => descriptor.GetHashCode());
        }

        [Test]
        public void Equals_NullTypeIdFields_WorksCorrectly()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = null, Config = null };
            var b = new FacialControllerDescriptor { ControllerTypeId = null, Config = null };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void ConfigEquality_UsesReferenceEquals_NotValueEquals()
        {
            var a = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configA };
            var b = new FacialControllerDescriptor { ControllerTypeId = "BlendShape", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }
    }

    public class LipSyncSourceDescriptorTests
    {
        private class ConcreteLipSyncSourceConfig : LipSyncSourceConfigBase { }

        private ConcreteLipSyncSourceConfig _configA;
        private ConcreteLipSyncSourceConfig _configB;

        [SetUp]
        public void SetUp()
        {
            _configA = ScriptableObject.CreateInstance<ConcreteLipSyncSourceConfig>();
            _configB = ScriptableObject.CreateInstance<ConcreteLipSyncSourceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_configA != null) Object.DestroyImmediate(_configA);
            if (_configB != null) Object.DestroyImmediate(_configB);
        }

        [Test]
        public void IsSerializable()
        {
            Assert.That(typeof(LipSyncSourceDescriptor).GetCustomAttributes(typeof(System.SerializableAttribute), false).Length, Is.GreaterThan(0));
        }

        [Test]
        public void IsSealed()
        {
            Assert.That(typeof(LipSyncSourceDescriptor).IsSealed, Is.True);
        }

        [Test]
        public void ImplementsIEquatable()
        {
            Assert.That(typeof(System.IEquatable<LipSyncSourceDescriptor>).IsAssignableFrom(typeof(LipSyncSourceDescriptor)), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_SameConfigReference_ReturnsTrue()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void Equals_SameTypeId_DifferentConfigReference_ReturnsFalse()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentTypeId_SameConfigReference_ReturnsFalse()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "Viseme", Config = _configA };

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a.Equals(null), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_WorksCorrectly()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.Equals((object)null), Is.False);
            Assert.That(a.Equals("not a descriptor"), Is.False);
        }

        [Test]
        public void GetHashCode_SameTypeIdAndConfig_ReturnsSameValue()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentConfig_MayDiffer()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configB };

            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void OperatorEquals_BothNull_ReturnsTrue()
        {
            LipSyncSourceDescriptor a = null;
            LipSyncSourceDescriptor b = null;

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void OperatorEquals_OneNull_ReturnsFalse()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a == null, Is.False);
            Assert.That(null == a, Is.False);
        }

        [Test]
        public void OperatorNotEquals_DifferentDescriptors_ReturnsTrue()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configB };

            Assert.That(a != b, Is.True);
        }

        [Test]
        public void OperatorEquals_SameValues_ReturnsTrue()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };

            Assert.That(a == b, Is.True);
        }

        [Test]
        public void CanBeUsedAsDictionaryKey()
        {
            var descriptor = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var dict = new Dictionary<LipSyncSourceDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            Assert.That(dict.ContainsKey(lookupKey), Is.True);
            Assert.That(dict[lookupKey], Is.EqualTo("value"));
        }

        [Test]
        public void DictionaryKey_DifferentConfig_NotFound()
        {
            var descriptor = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var dict = new Dictionary<LipSyncSourceDescriptor, string>();

            dict[descriptor] = "value";

            var lookupKey = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configB };
            Assert.That(dict.ContainsKey(lookupKey), Is.False);
        }

        [Test]
        public void GetHashCode_NullTypeIdAndConfig_DoesNotThrow()
        {
            var descriptor = new LipSyncSourceDescriptor { SourceTypeId = null, Config = null };

            Assert.DoesNotThrow(() => descriptor.GetHashCode());
        }

        [Test]
        public void Equals_NullTypeIdFields_WorksCorrectly()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = null, Config = null };
            var b = new LipSyncSourceDescriptor { SourceTypeId = null, Config = null };

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void ConfigEquality_UsesReferenceEquals_NotValueEquals()
        {
            var a = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configA };
            var b = new LipSyncSourceDescriptor { SourceTypeId = "OVRLipSync", Config = _configB };

            Assert.That(a.Equals(b), Is.False);
        }
    }
}
