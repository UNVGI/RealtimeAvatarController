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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_slotIdProp,      new GUIContent("Slot ID"));
            EditorGUILayout.PropertyField(_displayNameProp, new GUIContent("表示名"));

            EditorGUILayout.Space();
            DrawAvatarProviderSection();

            EditorGUILayout.Space();
            DrawMoCapSourceSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("モーション設定", EditorStyles.boldLabel);
            DrawWeightToggle();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("フォールバック設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_fallbackBehaviorProp, new GUIContent("フォールバック挙動"));
            // デフォルト値: HoldLastPose (contracts.md §1.8 / SlotSettings 初期値に従う)
            // 選択肢: HoldLastPose (最後のポーズを保持) / TPose (T ポーズに戻す) / Hide (アバターを非表示)

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAvatarProviderSection()
        {
            EditorGUILayout.LabelField("アバター Provider", EditorStyles.boldLabel);

            var typeIdProp = _avatarProviderDescriptorProp.FindPropertyRelative("ProviderTypeId");
            var configProp = _avatarProviderDescriptorProp.FindPropertyRelative("Config");

            if (_providerTypeIds.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Registry に Provider が未登録です。\n[InitializeOnLoadMethod] が実行されているか確認してください。",
                    MessageType.Warning);
                EditorGUILayout.PropertyField(typeIdProp, new GUIContent("Provider Type ID (手入力)"));
            }
            else
            {
                int currentIndex = System.Array.IndexOf(_providerTypeIds, typeIdProp.stringValue);
                if (currentIndex < 0) currentIndex = 0;
                int newIndex = EditorGUILayout.Popup("Provider Type", currentIndex, _providerTypeIds);
                typeIdProp.stringValue = _providerTypeIds[newIndex];
            }

            EditorGUILayout.PropertyField(configProp, new GUIContent("Provider Config (SO)"));

            if (GUILayout.Button("候補を更新"))
                RefreshTypeIds();
        }

        private void DrawMoCapSourceSection()
        {
            EditorGUILayout.LabelField("MoCap ソース", EditorStyles.boldLabel);

            var typeIdProp = _moCapSourceDescriptorProp.FindPropertyRelative("SourceTypeId");
            var configProp = _moCapSourceDescriptorProp.FindPropertyRelative("Config");

            string[] options = _moCapSourceTypeIds.Length > 0
                ? new[] { "(未割り当て)" }.Concat(_moCapSourceTypeIds).ToArray()
                : null;

            if (options == null)
            {
                EditorGUILayout.HelpBox(
                    "Registry に MoCapSource が未登録です。\n[InitializeOnLoadMethod] が実行されているか確認してください。",
                    MessageType.Warning);
                EditorGUILayout.PropertyField(typeIdProp, new GUIContent("Source Type ID (手入力)"));
            }
            else
            {
                int currentIndex = string.IsNullOrEmpty(typeIdProp.stringValue)
                    ? 0
                    : System.Array.IndexOf(_moCapSourceTypeIds, typeIdProp.stringValue) + 1;
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup("MoCap Source Type", currentIndex, options);

                if (newIndex == 0)
                    typeIdProp.stringValue = string.Empty;
                else
                    typeIdProp.stringValue = _moCapSourceTypeIds[newIndex - 1];
            }

            EditorGUILayout.PropertyField(configProp, new GUIContent("MoCap Source Config (SO)"));

            if (GUILayout.Button("候補を更新"))
                RefreshTypeIds();
        }

        private void DrawWeightToggle()
        {
            // T6-1 で実装予定
        }
    }
}
