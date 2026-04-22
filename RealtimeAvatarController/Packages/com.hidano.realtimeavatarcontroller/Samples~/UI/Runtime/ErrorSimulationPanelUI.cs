using UnityEngine;
using UnityEngine.UI;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用のエラーシミュレーションボタン配線クラス。
    /// design.md §7.3 に従い「VMC 切断シミュレーション」「初期化失敗シミュレーション」
    /// の 2 ボタンを <see cref="ErrorSimulationHelper"/> に結びつけ、発行されたエラーが
    /// <see cref="ISlotErrorChannel"/> 経由で <c>SlotErrorPanel</c> に表示されることを確認するための薄い橋渡しを提供する。
    /// </summary>
    public class ErrorSimulationPanelUI : MonoBehaviour
    {
        [SerializeField] private SlotManagerBehaviour slotManagerBehaviour;
        [SerializeField] private SlotDetailPanelUI slotDetailPanel;
        [SerializeField] private Button vmcDisconnectButton;
        [SerializeField] private Button initFailureButton;

        [Tooltip("SlotDetailPanel で Slot が選択されていない場合に VMC 切断シミュレーションで使用するフォールバック Slot ID。")]
        [SerializeField] private string fallbackSlotId = "shared-slot-01";

        private void Start()
        {
            if (vmcDisconnectButton != null)
            {
                vmcDisconnectButton.onClick.RemoveListener(OnVmcDisconnectClicked);
                vmcDisconnectButton.onClick.AddListener(OnVmcDisconnectClicked);
            }
            if (initFailureButton != null)
            {
                initFailureButton.onClick.RemoveListener(OnInitFailureClicked);
                initFailureButton.onClick.AddListener(OnInitFailureClicked);
            }
        }

        private void OnDestroy()
        {
            if (vmcDisconnectButton != null) vmcDisconnectButton.onClick.RemoveListener(OnVmcDisconnectClicked);
            if (initFailureButton != null) initFailureButton.onClick.RemoveListener(OnInitFailureClicked);
        }

        private void OnVmcDisconnectClicked()
        {
            var slotId = ResolveTargetSlotId();
            ErrorSimulationHelper.SimulateVmcDisconnect(slotId);
        }

        private void OnInitFailureClicked()
        {
            var slotManager = slotManagerBehaviour != null ? slotManagerBehaviour.SlotManager : null;
            ErrorSimulationHelper.SimulateInitFailure(slotManager);
        }

        private string ResolveTargetSlotId()
        {
            if (slotDetailPanel != null && slotDetailPanel.CurrentSlot != null)
                return slotDetailPanel.CurrentSlot.SlotId;
            return fallbackSlotId ?? string.Empty;
        }
    }
}
