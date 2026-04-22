using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモ専用: <see cref="IMoCapSourceRegistry"/> 内部の参照カウントを UI 上に表示するデバッグラベル。
    /// 既定実装 <c>DefaultMoCapSourceRegistry</c> の private フィールド <c>_instances</c> と
    /// nested struct <c>Entry.RefCount</c> をリフレクションで読み出し、
    /// <see cref="MoCapSourceDescriptor"/> ごとの参照カウントを表示する。
    /// design.md §10.3 の参照共有シナリオ視覚確認に用いるデモ専用 UI であり、
    /// 本番コードから利用する API ではない。
    /// </summary>
    public class MoCapSourceRegistryDebugLabel : MonoBehaviour
    {
        [SerializeField] private SlotManagerBehaviour slotManagerBehaviour;
        [SerializeField] private Text label;
        [SerializeField, Tooltip("ポーリング間隔 (秒)。0 以下の場合はポーリングせずイベント駆動のみ")]
        private float pollingInterval = 0.5f;

        private IMoCapSourceRegistry _registry;
        private FieldInfo _instancesField;
        private FieldInfo _refCountField;
        private CompositeDisposable _disposables;
        private float _pollingTimer;

        private void Start()
        {
            _registry = RegistryLocator.MoCapSourceRegistry;
            CacheReflection();

            _disposables = new CompositeDisposable();

            var slotManager = slotManagerBehaviour != null ? slotManagerBehaviour.SlotManager : null;
            if (slotManager != null)
            {
                slotManager.OnSlotStateChanged
                    .ObserveOnMainThread()
                    .Subscribe(_ => Refresh())
                    .AddTo(_disposables);
            }

            Refresh();
        }

        private void Update()
        {
            if (pollingInterval <= 0f) return;
            _pollingTimer += Time.deltaTime;
            if (_pollingTimer >= pollingInterval)
            {
                _pollingTimer = 0f;
                Refresh();
            }
        }

        private void OnDestroy() => _disposables?.Dispose();

        private void CacheReflection()
        {
            if (_registry == null) return;

            var type = _registry.GetType();
            _instancesField = type.GetField("_instances", BindingFlags.Instance | BindingFlags.NonPublic);

            var entryType = type.GetNestedType("Entry", BindingFlags.NonPublic);
            if (entryType != null)
            {
                _refCountField = entryType.GetField("RefCount", BindingFlags.Instance | BindingFlags.Public);
            }
        }

        private void Refresh()
        {
            if (label == null) return;

            if (_registry == null || _instancesField == null)
            {
                label.text = "参照カウント: (内部状態を取得できません)";
                return;
            }

            if (!(_instancesField.GetValue(_registry) is IDictionary instances))
            {
                label.text = "参照カウント: (内部状態を取得できません)";
                return;
            }

            if (instances.Count == 0)
            {
                label.text = "参照カウント: 0 件";
                return;
            }

            var sb = new StringBuilder();
            sb.Append("参照カウント:");
            foreach (DictionaryEntry kv in instances)
            {
                var descriptor = kv.Key as MoCapSourceDescriptor;
                var refCount = _refCountField != null ? _refCountField.GetValue(kv.Value) : "?";
                var typeId = descriptor != null && !string.IsNullOrEmpty(descriptor.SourceTypeId) ? descriptor.SourceTypeId : "?";
                var configName = descriptor != null && descriptor.Config != null ? descriptor.Config.name : "(null)";
                sb.Append("\n  [").Append(typeId).Append("] Config=").Append(configName).Append(" → ").Append(refCount);
            }

            label.text = sb.ToString();
        }
    }
}
