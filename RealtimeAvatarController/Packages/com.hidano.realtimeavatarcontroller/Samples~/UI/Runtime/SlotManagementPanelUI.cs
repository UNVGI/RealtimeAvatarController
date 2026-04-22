using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// Slot 一覧パネルの UI ロジック。
    /// AddSlot ボタン押下時に <see cref="SlotManager.AddSlotAsync"/> を呼び出し、
    /// <see cref="SlotManager.OnSlotStateChanged"/> の購読結果を用いて
    /// <see cref="SlotListItemUI"/> プレハブを動的にインスタンス化・破棄することで
    /// Slot 一覧表示をリアルタイム更新する。
    /// </summary>
    public class SlotManagementPanelUI : MonoBehaviour
    {
        [SerializeField] private SlotManagerBehaviour slotManagerBehaviour;
        [SerializeField] private Button addSlotButton;
        [SerializeField] private ScrollRect slotListScrollView;
        [SerializeField] private Transform slotListContent;
        [SerializeField] private GameObject slotListItemPrefab;
        [SerializeField] private SlotSettings addSlotTemplate;
        [SerializeField] private SlotDetailPanelUI detailPanel;

        private SlotManager _slotManager;
        private readonly Dictionary<string, SlotListItemUI> _items = new Dictionary<string, SlotListItemUI>();
        private CompositeDisposable _disposables;

        private void Start()
        {
            _slotManager = slotManagerBehaviour != null ? slotManagerBehaviour.SlotManager : null;
            if (_slotManager == null)
            {
                Debug.LogWarning("[SlotManagementPanelUI] SlotManager が未初期化のためリスト更新を行いません。");
                return;
            }

            _disposables = new CompositeDisposable();

            if (addSlotButton != null)
            {
                addSlotButton.onClick.RemoveListener(OnAddSlotClicked);
                addSlotButton.onClick.AddListener(OnAddSlotClicked);
            }

            _slotManager.OnSlotStateChanged
                .ObserveOnMainThread()
                .Subscribe(OnSlotStateChanged)
                .AddTo(_disposables);

            RefreshList();
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
            if (addSlotButton != null)
                addSlotButton.onClick.RemoveListener(OnAddSlotClicked);
        }

        private Transform GetItemContainer()
        {
            if (slotListContent != null) return slotListContent;
            if (slotListScrollView != null && slotListScrollView.content != null)
                return slotListScrollView.content;
            if (slotListScrollView != null) return slotListScrollView.transform;
            return transform;
        }

        private void OnAddSlotClicked()
        {
            if (_slotManager == null) return;
            if (addSlotTemplate == null)
            {
                Debug.LogWarning("[SlotManagementPanelUI] addSlotTemplate が未設定のため Slot を追加できません。Inspector で SlotSettings アセットを割り当ててください。");
                return;
            }

            var clone = Instantiate(addSlotTemplate);
            var prefix = string.IsNullOrEmpty(addSlotTemplate.slotId) ? "slot" : addSlotTemplate.slotId;
            clone.slotId = $"{prefix}-{Guid.NewGuid():N}";
            _ = _slotManager.AddSlotAsync(clone);
        }

        private void OnSlotStateChanged(SlotStateChangedEvent e)
        {
            if (e == null) return;

            if (e.NewState == SlotState.Disposed)
            {
                if (_items.TryGetValue(e.SlotId, out var item))
                {
                    if (item != null) Destroy(item.gameObject);
                    _items.Remove(e.SlotId);
                }
                return;
            }

            if (!_items.ContainsKey(e.SlotId))
            {
                var handle = _slotManager.GetSlot(e.SlotId);
                if (handle != null) SpawnItem(handle);
            }
        }

        private void RefreshList()
        {
            foreach (var kv in _items)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _items.Clear();

            foreach (var handle in _slotManager.GetSlots())
            {
                SpawnItem(handle);
            }
        }

        private void SpawnItem(SlotHandle handle)
        {
            if (slotListItemPrefab == null)
            {
                Debug.LogWarning("[SlotManagementPanelUI] slotListItemPrefab が未設定のため Slot 行を生成できません。");
                return;
            }

            var instance = Instantiate(slotListItemPrefab, GetItemContainer());
            var item = instance.GetComponent<SlotListItemUI>();
            if (item == null)
            {
                Debug.LogWarning("[SlotManagementPanelUI] SlotListItem プレハブに SlotListItemUI コンポーネントがありません。");
                Destroy(instance);
                return;
            }

            item.Bind(handle, _slotManager);
            if (detailPanel != null)
            {
                item.OnSelectRequested = h => detailPanel.BindSlot(h);
            }
            _items[handle.SlotId] = item;
        }
    }
}
