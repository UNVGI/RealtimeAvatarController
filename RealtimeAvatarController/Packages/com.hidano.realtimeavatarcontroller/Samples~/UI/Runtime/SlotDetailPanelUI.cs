using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// 選択中 Slot の詳細設定パネル UI。
    /// DisplayName / Weight / FallbackBehavior の編集と DeleteSlotButton 経由の
    /// <see cref="SlotManager.RemoveSlotAsync"/> 呼び出しを担う。
    /// </summary>
    public class SlotDetailPanelUI : MonoBehaviour
    {
        [SerializeField] private SlotManagerBehaviour slotManagerBehaviour;
        [SerializeField] private InputField displayNameInputField;
        [SerializeField] private Toggle weightToggle;
        [SerializeField] private Dropdown fallbackDropdown;
        [SerializeField] private Button deleteSlotButton;

        private SlotManager _slotManager;
        private SlotHandle _currentSlot;
        private CompositeDisposable _disposables;
        private readonly List<FallbackBehavior> _fallbackOrder = new List<FallbackBehavior>
        {
            FallbackBehavior.HoldLastPose,
            FallbackBehavior.TPose,
            FallbackBehavior.Hide,
        };

        public SlotHandle CurrentSlot => _currentSlot;

        private void Start()
        {
            _slotManager = slotManagerBehaviour != null ? slotManagerBehaviour.SlotManager : null;

            InitializeFallbackDropdown();

            if (displayNameInputField != null)
            {
                displayNameInputField.onValueChanged.RemoveListener(OnDisplayNameChanged);
                displayNameInputField.onValueChanged.AddListener(OnDisplayNameChanged);
            }
            if (weightToggle != null)
            {
                weightToggle.onValueChanged.RemoveListener(OnWeightToggleChanged);
                weightToggle.onValueChanged.AddListener(OnWeightToggleChanged);
            }
            if (fallbackDropdown != null)
            {
                fallbackDropdown.onValueChanged.RemoveListener(OnFallbackDropdownChanged);
                fallbackDropdown.onValueChanged.AddListener(OnFallbackDropdownChanged);
            }
            if (deleteSlotButton != null)
            {
                deleteSlotButton.onClick.RemoveListener(OnDeleteButtonClicked);
                deleteSlotButton.onClick.AddListener(OnDeleteButtonClicked);
            }

            ClearBindings();

            if (_slotManager == null)
            {
                Debug.LogWarning("[SlotDetailPanelUI] SlotManager が未初期化のため詳細反映は行いません。");
                return;
            }

            _disposables = new CompositeDisposable();
            _slotManager.OnSlotStateChanged
                .ObserveOnMainThread()
                .Subscribe(OnSlotStateChanged)
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
            if (displayNameInputField != null) displayNameInputField.onValueChanged.RemoveListener(OnDisplayNameChanged);
            if (weightToggle != null) weightToggle.onValueChanged.RemoveListener(OnWeightToggleChanged);
            if (fallbackDropdown != null) fallbackDropdown.onValueChanged.RemoveListener(OnFallbackDropdownChanged);
            if (deleteSlotButton != null) deleteSlotButton.onClick.RemoveListener(OnDeleteButtonClicked);
        }

        /// <summary>対象 Slot を詳細パネルに表示する。null の場合は表示をクリアする。</summary>
        public void BindSlot(SlotHandle handle)
        {
            _currentSlot = handle;
            if (_currentSlot == null || _currentSlot.Settings == null)
            {
                ClearBindings();
                return;
            }

            var settings = _currentSlot.Settings;

            if (displayNameInputField != null)
                displayNameInputField.SetTextWithoutNotify(settings.displayName ?? string.Empty);
            if (weightToggle != null)
                weightToggle.SetIsOnWithoutNotify(settings.weight >= 0.5f);
            if (fallbackDropdown != null && _fallbackOrder.Count > 0)
            {
                int index = _fallbackOrder.IndexOf(settings.fallbackBehavior);
                if (index < 0) index = 0;
                fallbackDropdown.SetValueWithoutNotify(index);
                fallbackDropdown.RefreshShownValue();
            }
        }

        private void ClearBindings()
        {
            if (displayNameInputField != null) displayNameInputField.SetTextWithoutNotify(string.Empty);
            if (weightToggle != null) weightToggle.SetIsOnWithoutNotify(false);
            if (fallbackDropdown != null && _fallbackOrder.Count > 0)
            {
                fallbackDropdown.SetValueWithoutNotify(0);
                fallbackDropdown.RefreshShownValue();
            }
        }

        private void InitializeFallbackDropdown()
        {
            if (fallbackDropdown == null) return;
            fallbackDropdown.ClearOptions();
            fallbackDropdown.AddOptions(new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("HoldLastPose"),
                new Dropdown.OptionData("TPose"),
                new Dropdown.OptionData("Hide"),
            });
        }

        private void OnSlotStateChanged(SlotStateChangedEvent e)
        {
            if (_currentSlot == null || e == null) return;
            if (e.SlotId != _currentSlot.SlotId) return;

            if (e.NewState == SlotState.Disposed)
            {
                _currentSlot = null;
                ClearBindings();
                return;
            }

            if (_slotManager != null)
            {
                var handle = _slotManager.GetSlot(e.SlotId);
                if (handle != null) BindSlot(handle);
            }
        }

        private void OnDisplayNameChanged(string value)
        {
            if (_currentSlot?.Settings == null) return;
            _currentSlot.Settings.displayName = value ?? string.Empty;
        }

        private void OnWeightToggleChanged(bool isOn)
        {
            if (_currentSlot?.Settings == null) return;
            // 0.5f 閾値の二値モデル: ON → 1.0 / OFF → 0.0 (design.md §4.4 / OI-8 準拠)
            _currentSlot.Settings.weight = isOn ? 1.0f : 0.0f;
        }

        private void OnFallbackDropdownChanged(int index)
        {
            if (_currentSlot?.Settings == null) return;
            if (index < 0 || index >= _fallbackOrder.Count) return;
            _currentSlot.Settings.fallbackBehavior = _fallbackOrder[index];
        }

        private void OnDeleteButtonClicked()
        {
            if (_slotManager == null || _currentSlot == null) return;
            _ = _slotManager.RemoveSlotAsync(_currentSlot.SlotId);
        }
    }
}
