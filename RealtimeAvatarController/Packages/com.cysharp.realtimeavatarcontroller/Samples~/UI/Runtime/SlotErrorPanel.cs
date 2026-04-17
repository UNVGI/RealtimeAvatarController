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
    /// </summary>
    public class SlotErrorPanel : MonoBehaviour
    {
        [SerializeField] private Transform _logContainer;
        [SerializeField] private GameObject _logItemPrefab;
        [SerializeField, Tooltip("表示するエラーログの最大件数。超過した古いエントリは破棄される。")]
        private int _maxDisplayCount = 20;

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

        private void OnSlotError(SlotError error)
        {
            if (_logContainer == null || _logItemPrefab == null) return;

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
    }
}
