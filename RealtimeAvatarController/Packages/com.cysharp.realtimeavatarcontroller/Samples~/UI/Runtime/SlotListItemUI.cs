using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// Slot 一覧の 1 行分の表示・操作を担う UI コンポーネント。
    /// SlotId / 表示名 / ライフサイクル状態の表示、Weight 二値トグル、削除ボタンを保持する。
    /// 行全体のクリックは <see cref="IPointerClickHandler"/> 経由で検出し、
    /// <see cref="OnSelectRequested"/> を発火して親 Panel に選択を通知する。
    /// </summary>
    public class SlotListItemUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Text slotIdLabel;
        [SerializeField] private Text displayNameLabel;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Toggle weightToggle;
        [SerializeField] private Button deleteButton;

        private SlotManager _slotManager;
        private SlotHandle _handle;
        private string _slotId;
        private SlotSettings _settings;
        private CompositeDisposable _disposables;

        public string SlotId => _slotId;

        public Action<SlotHandle> OnSelectRequested { get; set; }

        public void Bind(SlotHandle handle, SlotManager slotManager)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            if (slotManager == null) throw new ArgumentNullException(nameof(slotManager));

            _slotManager = slotManager;
            _handle      = handle;
            _slotId      = handle.SlotId;
            _settings    = handle.Settings;

            if (slotIdLabel      != null) slotIdLabel.text      = _slotId ?? string.Empty;
            if (displayNameLabel != null) displayNameLabel.text = handle.DisplayName ?? string.Empty;

            UpdateStateLabel(handle.State);

            if (weightToggle != null)
            {
                bool isActive = _settings != null && _settings.weight >= 0.5f;
                weightToggle.SetIsOnWithoutNotify(isActive);
                weightToggle.onValueChanged.RemoveAllListeners();
                weightToggle.onValueChanged.AddListener(OnWeightToggleChanged);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OnDeleteButtonClicked);
            }

            _disposables?.Dispose();
            _disposables = new CompositeDisposable();
            _slotManager.OnSlotStateChanged
                .Where(e => e.SlotId == _slotId)
                .ObserveOnMainThread()
                .Subscribe(e => UpdateStateLabel(e.NewState))
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }

        private void UpdateStateLabel(SlotState state)
        {
            if (stateLabel != null) stateLabel.text = state.ToString();
        }

        private void OnWeightToggleChanged(bool isOn)
        {
            if (_settings == null) return;
            // 0.5f 閾値の二値モデル: トグル ON → 1.0 / OFF → 0.0 (design.md §4.4 / OI-8 準拠)
            _settings.weight = isOn ? 1.0f : 0.0f;
        }

        private void OnDeleteButtonClicked()
        {
            if (_slotManager == null || string.IsNullOrEmpty(_slotId)) return;
            _ = _slotManager.RemoveSlotAsync(_slotId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_handle == null) return;
            OnSelectRequested?.Invoke(_handle);
        }
    }
}
