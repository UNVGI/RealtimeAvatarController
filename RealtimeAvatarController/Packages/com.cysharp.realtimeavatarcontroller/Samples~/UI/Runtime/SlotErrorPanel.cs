using UnityEngine;
using UnityEngine.UI;
using UniRx;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// <see cref="ISlotErrorChannel"/> を購読し、発生したエラーを UI 上にリアルタイム表示するパネル。
    /// design.md §6.3 のコードスニペットに準拠。
    /// <see cref="RegistryLocator.ErrorChannel"/> の <see cref="ISlotErrorChannel.Errors"/> を
    /// <c>.ObserveOnMainThread().Subscribe()</c> で購読し、最新 N 件のみをリング表示する。
    /// design.md §7.1 のカテゴリフィルタ (VmcReceive / InitFailure / ApplyFailure / RegistryConflict)
    /// をトグルで切り替え可能。Toggle が未アサイン (null) の場合は全件通過する後方互換動作を採る。
    /// </summary>
    public class SlotErrorPanel : MonoBehaviour
    {
        [SerializeField] private Transform _logContainer;
        [SerializeField] private GameObject _logItemPrefab;
        [SerializeField, Tooltip("表示するエラーログの最大件数。超過した古いエントリは破棄される。")]
        private int _maxDisplayCount = 20;

        [Header("Category Filters (任意)")]
        [SerializeField, Tooltip("VmcReceive カテゴリを表示する。未アサイン時は常時有効扱い。")]
        private Toggle _vmcReceiveToggle;
        [SerializeField, Tooltip("InitFailure カテゴリを表示する。未アサイン時は常時有効扱い。")]
        private Toggle _initFailureToggle;
        [SerializeField, Tooltip("ApplyFailure カテゴリを表示する。未アサイン時は常時有効扱い。")]
        private Toggle _applyFailureToggle;
        [SerializeField, Tooltip("RegistryConflict カテゴリを表示する。未アサイン時は常時有効扱い。")]
        private Toggle _registryConflictToggle;

        private CompositeDisposable _disposables;

        private void Start()
        {
            _disposables = new CompositeDisposable();

            RegistryLocator.ErrorChannel.Errors
                .ObserveOnMainThread()
                .Subscribe(OnSlotError)
                .AddTo(_disposables);
        }

        private void OnDestroy() => _disposables?.Dispose();

        /// <summary>
        /// ログコンテナ内の全エラーログ GameObject を破棄する。
        /// ClearErrorsButton の OnClick イベントから呼び出される (T14-4)。
        /// </summary>
        public void ClearAllErrors()
        {
            if (_logContainer == null) return;
            for (int i = _logContainer.childCount - 1; i >= 0; i--)
                Destroy(_logContainer.GetChild(i).gameObject);
        }

        private void OnSlotError(SlotError error)
        {
            if (_logContainer == null || _logItemPrefab == null) return;
            if (!IsCategoryEnabled(error.Category)) return;

            if (_logContainer.childCount >= _maxDisplayCount)
                Destroy(_logContainer.GetChild(0).gameObject);

            var item = Instantiate(_logItemPrefab, _logContainer);
            var text = item.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text =
                    $"[{error.Timestamp:HH:mm:ss}] [{error.Category}] Slot:{error.SlotId}\n{error.Exception?.Message}";
            }
        }

        private bool IsCategoryEnabled(SlotErrorCategory category)
        {
            switch (category)
            {
                case SlotErrorCategory.VmcReceive:      return _vmcReceiveToggle      == null || _vmcReceiveToggle.isOn;
                case SlotErrorCategory.InitFailure:     return _initFailureToggle     == null || _initFailureToggle.isOn;
                case SlotErrorCategory.ApplyFailure:    return _applyFailureToggle    == null || _applyFailureToggle.isOn;
                case SlotErrorCategory.RegistryConflict:return _registryConflictToggle== null || _registryConflictToggle.isOn;
                default: return true;
            }
        }
    }
}
