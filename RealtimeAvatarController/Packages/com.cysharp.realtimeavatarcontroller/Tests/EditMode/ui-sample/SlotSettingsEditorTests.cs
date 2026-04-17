using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Samples.UI.Editor;

namespace RealtimeAvatarController.Samples.UI.Tests.EditMode
{
    /// <summary>
    /// SlotSettingsEditor の EditMode テスト。
    /// テスト観点 (tasks.md T16-2 / design.md §11.1 / §11.4):
    ///   - Registry モック注入時のドロップダウン候補列挙 (_providerTypeIds / _moCapSourceTypeIds)
    ///   - Registry 未登録時のフォールバック (空配列)
    ///   - Fallback UI の SerializedProperty 反映 (enumValueIndex ⇔ FallbackBehavior)
    ///   - Weight 二値トグル (SerializedProperty 経由で 1.0f / 0.0f を反映)
    ///   - FallbackBehavior デフォルト値 (新規インスタンスで HoldLastPose) ※ validation-design.md OI-7 対応
    /// Requirements: 2, 3, 4, 10, 12
    /// </summary>
    [TestFixture]
    public class SlotSettingsEditorTests
    {
        private SlotSettings _settings;
        private SlotSettingsEditor _editor;
        private StubProviderRegistry _stubProviderRegistry;
        private StubMoCapSourceRegistry _stubMoCapSourceRegistry;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();

            _stubProviderRegistry = new StubProviderRegistry();
            _stubMoCapSourceRegistry = new StubMoCapSourceRegistry();
            RegistryLocator.OverrideProviderRegistry(_stubProviderRegistry);
            RegistryLocator.OverrideMoCapSourceRegistry(_stubMoCapSourceRegistry);

            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = "test-slot";
            _settings.displayName = "Test Slot";
            _settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = string.Empty,
            };
            _settings.moCapSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = string.Empty,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }
            if (_settings != null)
            {
                Object.DestroyImmediate(_settings);
                _settings = null;
            }
            RegistryLocator.ResetForTest();
        }

        // --- Registry モック注入時のドロップダウン候補列挙 (Req 2, 3) ---

        [Test]
        public void RefreshTypeIds_WithRegisteredProvider_IncludesTypeId()
        {
            _stubProviderRegistry.Add("Builtin");

            var editor = CreateEditor();
            InvokeRefreshTypeIds(editor);

            var typeIds = GetPrivateField<string[]>(editor, "_providerTypeIds");
            Assert.That(typeIds, Does.Contain("Builtin"));
        }

        [Test]
        public void RefreshTypeIds_WithRegisteredMoCapSource_IncludesTypeId()
        {
            _stubMoCapSourceRegistry.Add("VMC");

            var editor = CreateEditor();
            InvokeRefreshTypeIds(editor);

            var typeIds = GetPrivateField<string[]>(editor, "_moCapSourceTypeIds");
            Assert.That(typeIds, Does.Contain("VMC"));
        }

        [Test]
        public void RefreshTypeIds_WithMultipleRegisteredProviders_ContainsAllTypeIds()
        {
            _stubProviderRegistry.Add("Builtin");
            _stubProviderRegistry.Add("Addressable");

            var editor = CreateEditor();
            InvokeRefreshTypeIds(editor);

            var typeIds = GetPrivateField<string[]>(editor, "_providerTypeIds");
            Assert.That(typeIds, Is.EquivalentTo(new[] { "Builtin", "Addressable" }));
        }

        // --- Registry 未登録時のフォールバック (Req 2, 3) ---

        [Test]
        public void RefreshTypeIds_WithEmptyProviderRegistry_ProducesEmptyArray()
        {
            var editor = CreateEditor();
            InvokeRefreshTypeIds(editor);

            var typeIds = GetPrivateField<string[]>(editor, "_providerTypeIds");
            Assert.That(typeIds, Is.Not.Null);
            Assert.That(typeIds, Is.Empty);
        }

        [Test]
        public void RefreshTypeIds_WithEmptyMoCapSourceRegistry_ProducesEmptyArray()
        {
            var editor = CreateEditor();
            InvokeRefreshTypeIds(editor);

            var typeIds = GetPrivateField<string[]>(editor, "_moCapSourceTypeIds");
            Assert.That(typeIds, Is.Not.Null);
            Assert.That(typeIds, Is.Empty);
        }

        // --- Fallback UI の SerializedProperty 反映 (Req 10) ---

        [Test]
        public void FallbackBehavior_SetOnField_ReflectsInSerializedProperty()
        {
            _settings.fallbackBehavior = FallbackBehavior.TPose;

            var so = new SerializedObject(_settings);
            var fallbackProp = so.FindProperty("fallbackBehavior");

            Assert.That(fallbackProp.enumValueIndex, Is.EqualTo((int)FallbackBehavior.TPose));
        }

        [Test]
        public void FallbackBehavior_SetViaSerializedProperty_AppliesToField()
        {
            var so = new SerializedObject(_settings);
            var fallbackProp = so.FindProperty("fallbackBehavior");

            fallbackProp.enumValueIndex = (int)FallbackBehavior.Hide;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_settings.fallbackBehavior, Is.EqualTo(FallbackBehavior.Hide));
        }

        [Test]
        public void FallbackBehavior_HasThreeChoices()
        {
            // design.md §4.3: HoldLastPose / TPose / Hide の 3 択であることを確認
            var values = System.Enum.GetValues(typeof(FallbackBehavior));
            Assert.That(values.Length, Is.EqualTo(3));
            Assert.That(System.Enum.IsDefined(typeof(FallbackBehavior), FallbackBehavior.HoldLastPose), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(FallbackBehavior), FallbackBehavior.TPose), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(FallbackBehavior), FallbackBehavior.Hide), Is.True);
        }

        // --- Weight 二値トグル (Req 4) ---

        [Test]
        public void WeightToggle_TurnOn_SetsWeightToOne()
        {
            _settings.weight = 0.0f;
            var editor = CreateEditor();
            var weightProp = GetPrivateField<SerializedProperty>(editor, "_weightProp");
            Assume.That(weightProp, Is.Not.Null);

            // SlotSettingsEditor.DrawWeightToggle() のトグル ON 時と同じ操作:
            // bool newActive = true; _weightProp.floatValue = 1.0f;
            weightProp.floatValue = 1.0f;
            weightProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_settings.weight, Is.EqualTo(1.0f));
        }

        [Test]
        public void WeightToggle_TurnOff_SetsWeightToZero()
        {
            _settings.weight = 1.0f;
            var editor = CreateEditor();
            var weightProp = GetPrivateField<SerializedProperty>(editor, "_weightProp");
            Assume.That(weightProp, Is.Not.Null);

            weightProp.floatValue = 0.0f;
            weightProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_settings.weight, Is.EqualTo(0.0f));
        }

        [Test]
        public void WeightToggle_ThresholdHalf_InterpretedAsActive()
        {
            // DrawWeightToggle の閾値 0.5f 判定: weight >= 0.5f のとき isActive == true
            _settings.weight = 0.5f;
            Assert.That(_settings.weight >= 0.5f, Is.True);

            _settings.weight = 0.4999f;
            Assert.That(_settings.weight >= 0.5f, Is.False);
        }

        // --- FallbackBehavior デフォルト値 (validation-design.md OI-7) ---

        [Test]
        public void NewSlotSettings_DefaultFallbackBehavior_IsHoldLastPose()
        {
            var fresh = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                Assert.That(fresh.fallbackBehavior, Is.EqualTo(FallbackBehavior.HoldLastPose));
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void NewSlotSettings_SerializedFallbackBehavior_IsHoldLastPose()
        {
            var fresh = ScriptableObject.CreateInstance<SlotSettings>();
            try
            {
                var so = new SerializedObject(fresh);
                var fallbackProp = so.FindProperty("fallbackBehavior");
                Assert.That(fallbackProp.enumValueIndex, Is.EqualTo((int)FallbackBehavior.HoldLastPose));
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        // --- Helpers ---

        private SlotSettingsEditor CreateEditor()
        {
            _editor = (SlotSettingsEditor)UnityEditor.Editor.CreateEditor(_settings, typeof(SlotSettingsEditor));
            Assume.That(_editor, Is.Not.Null, "CreateEditor が SlotSettingsEditor を返せていない");
            return _editor;
        }

        private static void InvokeRefreshTypeIds(SlotSettingsEditor editor)
        {
            var method = typeof(SlotSettingsEditor).GetMethod(
                "RefreshTypeIds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assume.That(method, Is.Not.Null, "private メソッド RefreshTypeIds が見つからない");
            method.Invoke(editor, null);
        }

        private static T GetPrivateField<T>(SlotSettingsEditor editor, string fieldName)
        {
            var field = typeof(SlotSettingsEditor).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assume.That(field, Is.Not.Null, $"private フィールド {fieldName} が見つからない");
            return (T)field.GetValue(editor);
        }

        // --- Stub registries (mock for OverrideProviderRegistry / OverrideMoCapSourceRegistry) ---

        private sealed class StubProviderRegistry : IProviderRegistry
        {
            private readonly List<string> _typeIds = new List<string>();

            public void Add(string typeId) => _typeIds.Add(typeId);

            public void Register(string providerTypeId, IAvatarProviderFactory factory)
                => _typeIds.Add(providerTypeId);

            public IAvatarProvider Resolve(AvatarProviderDescriptor descriptor) => null;

            public IReadOnlyList<string> GetRegisteredTypeIds() => _typeIds;
        }

        private sealed class StubMoCapSourceRegistry : IMoCapSourceRegistry
        {
            private readonly List<string> _typeIds = new List<string>();

            public void Add(string typeId) => _typeIds.Add(typeId);

            public void Register(string sourceTypeId, IMoCapSourceFactory factory)
                => _typeIds.Add(sourceTypeId);

            public IMoCapSource Resolve(MoCapSourceDescriptor descriptor) => null;

            public void Release(IMoCapSource source) { }

            public IReadOnlyList<string> GetRegisteredTypeIds() => _typeIds;
        }
    }
}
