using System.Linq;
using UnityEditor;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI.Editor
{
    [CustomEditor(typeof(SlotSettings))]
    public class SlotSettingsEditor : UnityEditor.Editor
    {
        // --- SerializedProperty キャッシュ ---
        private SerializedProperty _slotIdProp;
        private SerializedProperty _displayNameProp;
        private SerializedProperty _weightProp;
        private SerializedProperty _fallbackBehaviorProp;
        private SerializedProperty _avatarProviderDescriptorProp;
        private SerializedProperty _moCapSourceDescriptorProp;

        // --- Registry キャッシュ ---
        private string[] _providerTypeIds    = System.Array.Empty<string>();
        private string[] _moCapSourceTypeIds = System.Array.Empty<string>();

        private void OnEnable()
        {
            _slotIdProp                   = serializedObject.FindProperty("slotId");
            _displayNameProp              = serializedObject.FindProperty("displayName");
            _weightProp                   = serializedObject.FindProperty("weight");
            _fallbackBehaviorProp         = serializedObject.FindProperty("fallbackBehavior");
            _avatarProviderDescriptorProp = serializedObject.FindProperty("avatarProviderDescriptor");
            _moCapSourceDescriptorProp    = serializedObject.FindProperty("moCapSourceDescriptor");

            RefreshTypeIds();
        }

        private void RefreshTypeIds()
        {
            try
            {
                _providerTypeIds    = RegistryLocator.ProviderRegistry
                                                     .GetRegisteredTypeIds()
                                                     .ToArray();
                _moCapSourceTypeIds = RegistryLocator.MoCapSourceRegistry
                                                     .GetRegisteredTypeIds()
                                                     .ToArray();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SlotSettingsEditor] Registry 候補取得失敗: {ex.Message}");
                _providerTypeIds    = System.Array.Empty<string>();
                _moCapSourceTypeIds = System.Array.Empty<string>();
            }
        }
    }
}
