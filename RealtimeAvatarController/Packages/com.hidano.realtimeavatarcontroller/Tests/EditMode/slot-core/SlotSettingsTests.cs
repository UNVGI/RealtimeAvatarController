using System;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// SlotSettings の EditMode テスト。
    /// テスト観点:
    ///   - Validate() 成功 (全必須フィールドが設定されている場合)
    ///   - slotId 欠落 (null / 空文字) で InvalidOperationException
    ///   - displayName 欠落 (null / 空文字) で InvalidOperationException
    ///   - avatarProviderDescriptor null で InvalidOperationException
    ///   - avatarProviderDescriptor.ProviderTypeId 欠落で InvalidOperationException
    ///   - moCapSourceDescriptor null で InvalidOperationException
    ///   - moCapSourceDescriptor.SourceTypeId 欠落で InvalidOperationException
    ///   - ScriptableObject.CreateInstance&lt;SlotSettings&gt;() でインスタンス生成後フィールドに値を直接セットして Validate() 成功
    ///   - 各フィールドのデフォルト値 (weight = 1.0f, fallbackBehavior = HoldLastPose)
    ///   - facialControllerDescriptor / lipSyncSourceDescriptor は null のままでも Validate() 成功
    /// Requirements: 1.1, 1.6, 1.7, 1.8, 14.3
    /// </summary>
    [TestFixture]
    public class SlotSettingsTests
    {
        private class ConcreteProviderConfig : ProviderConfigBase { }
        private class ConcreteMoCapSourceConfig : MoCapSourceConfigBase { }

        private SlotSettings _settings;
        private ConcreteProviderConfig _providerConfig;
        private ConcreteMoCapSourceConfig _moCapConfig;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _providerConfig = ScriptableObject.CreateInstance<ConcreteProviderConfig>();
            _moCapConfig = ScriptableObject.CreateInstance<ConcreteMoCapSourceConfig>();

            _settings.slotId = "slot-1";
            _settings.displayName = "Slot 1";
            _settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = "Builtin",
                Config = _providerConfig,
            };
            _settings.moCapSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = "VMC",
                Config = _moCapConfig,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_providerConfig != null) UnityEngine.Object.DestroyImmediate(_providerConfig);
            if (_moCapConfig != null) UnityEngine.Object.DestroyImmediate(_moCapConfig);
        }

        // --- 型定義 ---

        [Test]
        public void SlotSettings_InheritsFromScriptableObject()
        {
            Assert.That(typeof(SlotSettings).IsSubclassOf(typeof(ScriptableObject)), Is.True);
        }

        [Test]
        public void SlotSettings_HasSerializableAttribute()
        {
            Assert.That(
                typeof(SlotSettings).GetCustomAttributes(typeof(SerializableAttribute), false).Length,
                Is.GreaterThan(0));
        }

        // --- CreateInstance ランタイム動的生成 (Req 1.8) ---

        [Test]
        public void CreateInstance_ProducesValidInstance()
        {
            var instance = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance, Is.InstanceOf<SlotSettings>());
                Assert.That(instance, Is.InstanceOf<ScriptableObject>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CreateInstance_DefaultWeight_Is1()
        {
            var instance = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(instance.weight, Is.EqualTo(1.0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CreateInstance_DefaultFallbackBehavior_IsHoldLastPose()
        {
            var instance = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(instance.fallbackBehavior, Is.EqualTo(FallbackBehavior.HoldLastPose));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CreateInstance_DefaultFacialControllerDescriptor_IsNull()
        {
            var instance = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(instance.facialControllerDescriptor, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CreateInstance_DefaultLipSyncSourceDescriptor_IsNull()
        {
            var instance = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(instance.lipSyncSourceDescriptor, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // --- Validate() 成功ケース (Req 1.1, 1.8) ---

        [Test]
        public void Validate_AllRequiredFieldsSet_Succeeds()
        {
            Assert.DoesNotThrow(() => _settings.Validate());
        }

        [Test]
        public void Validate_OptionalFieldsNull_Succeeds()
        {
            _settings.facialControllerDescriptor = null;
            _settings.lipSyncSourceDescriptor = null;

            Assert.DoesNotThrow(() => _settings.Validate());
        }

        [Test]
        public void Validate_CreateInstanceThenSetFields_Succeeds()
        {
            var dynamic = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                dynamic.slotId = "dynamic-slot";
                dynamic.displayName = "Dynamic Slot";
                dynamic.avatarProviderDescriptor = new AvatarProviderDescriptor
                {
                    ProviderTypeId = "Builtin",
                    Config = _providerConfig,
                };
                dynamic.moCapSourceDescriptor = new MoCapSourceDescriptor
                {
                    SourceTypeId = "VMC",
                    Config = _moCapConfig,
                };

                Assert.DoesNotThrow(() => dynamic.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dynamic);
            }
        }

        // --- Validate() 失敗ケース: slotId 欠落 (Req 1.6) ---

        [Test]
        public void Validate_SlotIdNull_ThrowsInvalidOperationException()
        {
            _settings.slotId = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_SlotIdEmpty_ThrowsInvalidOperationException()
        {
            _settings.slotId = string.Empty;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        // --- Validate() 失敗ケース: displayName 欠落 ---

        [Test]
        public void Validate_DisplayNameNull_ThrowsInvalidOperationException()
        {
            _settings.displayName = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_DisplayNameEmpty_ThrowsInvalidOperationException()
        {
            _settings.displayName = string.Empty;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        // --- Validate() 失敗ケース: avatarProviderDescriptor 欠落 (Req 1.1, 1.7) ---

        [Test]
        public void Validate_AvatarProviderDescriptorNull_ThrowsInvalidOperationException()
        {
            _settings.avatarProviderDescriptor = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_AvatarProviderTypeIdNull_ThrowsInvalidOperationException()
        {
            _settings.avatarProviderDescriptor.ProviderTypeId = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_AvatarProviderTypeIdEmpty_ThrowsInvalidOperationException()
        {
            _settings.avatarProviderDescriptor.ProviderTypeId = string.Empty;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        // --- Validate() 失敗ケース: moCapSourceDescriptor 欠落 (Req 1.1) ---

        [Test]
        public void Validate_MoCapSourceDescriptorNull_ThrowsInvalidOperationException()
        {
            _settings.moCapSourceDescriptor = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_MoCapSourceTypeIdNull_ThrowsInvalidOperationException()
        {
            _settings.moCapSourceDescriptor.SourceTypeId = null;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        [Test]
        public void Validate_MoCapSourceTypeIdEmpty_ThrowsInvalidOperationException()
        {
            _settings.moCapSourceDescriptor.SourceTypeId = string.Empty;

            Assert.Throws<InvalidOperationException>(() => _settings.Validate());
        }

        // --- フィールドアクセス性 (Req 1.1) ---

        [Test]
        public void Fields_CanBeAssignedAndRetrieved()
        {
            var settings = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                settings.slotId = "abc";
                settings.displayName = "Abc";
                settings.weight = 0.5f;
                settings.fallbackBehavior = FallbackBehavior.TPose;

                Assert.That(settings.slotId, Is.EqualTo("abc"));
                Assert.That(settings.displayName, Is.EqualTo("Abc"));
                Assert.That(settings.weight, Is.EqualTo(0.5f));
                Assert.That(settings.fallbackBehavior, Is.EqualTo(FallbackBehavior.TPose));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
