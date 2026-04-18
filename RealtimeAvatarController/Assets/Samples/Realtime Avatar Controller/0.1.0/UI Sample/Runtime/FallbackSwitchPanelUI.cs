using UnityEngine;
using UnityEngine.UI;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用の Fallback 挙動切り替えボタングループ配線クラス。
    /// design.md §4.3 / tasks.md T13-1 に従い、選択中 Slot の
    /// <see cref="SlotSettings.fallbackBehavior"/> を HoldLastPose / TPose / Hide の
    /// 3 種へボタン押下で切り替えるための薄い橋渡しを提供する。
    /// 選択中 Slot は <see cref="SlotDetailPanelUI.CurrentSlot"/> から取得する。
    /// </summary>
    public class FallbackSwitchPanelUI : MonoBehaviour
    {
        [SerializeField] private SlotDetailPanelUI slotDetailPanel;
        [SerializeField] private Button holdLastPoseButton;
        [SerializeField] private Button tPoseButton;
        [SerializeField] private Button hideButton;

        private void Start()
        {
            if (holdLastPoseButton != null)
            {
                holdLastPoseButton.onClick.RemoveListener(OnHoldLastPoseClicked);
                holdLastPoseButton.onClick.AddListener(OnHoldLastPoseClicked);
            }
            if (tPoseButton != null)
            {
                tPoseButton.onClick.RemoveListener(OnTPoseClicked);
                tPoseButton.onClick.AddListener(OnTPoseClicked);
            }
            if (hideButton != null)
            {
                hideButton.onClick.RemoveListener(OnHideClicked);
                hideButton.onClick.AddListener(OnHideClicked);
            }
        }

        private void OnDestroy()
        {
            if (holdLastPoseButton != null) holdLastPoseButton.onClick.RemoveListener(OnHoldLastPoseClicked);
            if (tPoseButton != null) tPoseButton.onClick.RemoveListener(OnTPoseClicked);
            if (hideButton != null) hideButton.onClick.RemoveListener(OnHideClicked);
        }

        private void OnHoldLastPoseClicked() => ApplyFallback(FallbackBehavior.HoldLastPose);
        private void OnTPoseClicked() => ApplyFallback(FallbackBehavior.TPose);
        private void OnHideClicked() => ApplyFallback(FallbackBehavior.Hide);

        private void ApplyFallback(FallbackBehavior behavior)
        {
            if (slotDetailPanel == null)
            {
                Debug.LogWarning("[FallbackSwitchPanelUI] SlotDetailPanel 参照が未設定のため Fallback を切り替えできません。");
                return;
            }
            var slot = slotDetailPanel.CurrentSlot;
            if (slot?.Settings == null)
            {
                Debug.LogWarning("[FallbackSwitchPanelUI] 選択中 Slot がありません。先に Slot を選択してください。");
                return;
            }
            slot.Settings.fallbackBehavior = behavior;
            // ドロップダウン表示を最新値に同期する (Settings 直接変更では SlotDetailPanelUI の購読が発火しないため)。
            slotDetailPanel.BindSlot(slot);
        }
    }
}
