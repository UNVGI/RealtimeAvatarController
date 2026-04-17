using UnityEditor;
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
    }
}
