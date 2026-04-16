using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot のライフサイクル管理オーケストレータ。
    /// 動的な Slot 追加・削除・状態遷移を担う。
    /// 内部で <see cref="SlotRegistry"/> を保持し、コンストラクタ注入された
    /// <see cref="IProviderRegistry"/> / <see cref="IMoCapSourceRegistry"/> / <see cref="ISlotErrorChannel"/>
    /// に対して Provider / MoCapSource の解決・解放・エラー発行を委譲する。
    /// <para>
    /// 本クラスの 12.3 時点ではコア実装と weight クランプを提供する:
    /// 正常系 Add/Remove・重複 slotId チェック・状態遷移通知・GetSlots/GetSlot・
    /// AddSlotAsync 内の <c>Mathf.Clamp01(settings.weight)</c>。
    /// 初期化失敗時の Disposed 遷移 (タスク 12.4)・詳細リソース解放 (タスク 12.5)・
    /// ApplyFailure/フォールバック (タスク 12.6)・Dispose 全 Slot 解放 (タスク 12.7) は後続タスクで拡張する。
    /// </para>
    /// <para>
    /// TODO (validation-design.md [N-2]): Inactive ⇄ Active 遷移 API は設計予約済みで未実装。
    /// </para>
    /// </summary>
    public sealed class SlotManager : IDisposable
    {
        private readonly IProviderRegistry _providerRegistry;
        private readonly IMoCapSourceRegistry _moCapSourceRegistry;
        private readonly ISlotErrorChannel _errorChannel;
        private readonly SlotRegistry _slotRegistry = new SlotRegistry();
        private readonly Dictionary<string, SlotResources> _resources
            = new Dictionary<string, SlotResources>();
        private readonly Subject<SlotStateChangedEvent> _stateChanged
            = new Subject<SlotStateChangedEvent>();

        private bool _disposed;

        /// <summary>
        /// Slot 状態変化通知ストリーム。
        /// UniRx <see cref="Subject{T}"/> ベース。購読側は ObserveOnMainThread() で受信すること。
        /// </summary>
        public IObservable<SlotStateChangedEvent> OnSlotStateChanged => _stateChanged;

        /// <summary>
        /// SlotManager を生成する。テスト容易性のため全依存はコンストラクタ注入を採用する。
        /// </summary>
        /// <param name="providerRegistry">Avatar Provider の Registry。</param>
        /// <param name="moCapSourceRegistry">MoCap Source の Registry (参照カウント管理)。</param>
        /// <param name="errorChannel">Slot エラー通知チャネル。</param>
        public SlotManager(
            IProviderRegistry providerRegistry,
            IMoCapSourceRegistry moCapSourceRegistry,
            ISlotErrorChannel errorChannel)
        {
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _moCapSourceRegistry = moCapSourceRegistry ?? throw new ArgumentNullException(nameof(moCapSourceRegistry));
            _errorChannel = errorChannel ?? throw new ArgumentNullException(nameof(errorChannel));
        }

        /// <summary>
        /// Slot を非同期で追加する。
        /// <see cref="SlotSettings.Validate"/> を実行し、<see cref="SlotRegistry"/> に登録後、
        /// Provider / MoCapSource を Resolve してアバターを要求、成功後に Active 状態に遷移する。
        /// 同一 slotId が既に存在する場合は <see cref="InvalidOperationException"/> をスローする。
        /// SO アセット経由でも <see cref="ScriptableObject.CreateInstance(Type)"/> 動的生成経由でも
        /// 区別なく受け付ける。
        /// </summary>
        public async UniTask AddSlotAsync(SlotSettings settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ThrowIfDisposed();

            settings.Validate();

            // weight クランプ (タスク 12.3 / Req 1.5): SlotSettings 側ではクランプせず、
            // SlotManager 側で Mathf.Clamp01 により 0.0〜1.0 に収める。
            settings.weight = Mathf.Clamp01(settings.weight);

            // 重複 slotId は SlotRegistry.AddSlot が InvalidOperationException をスロー。
            _slotRegistry.AddSlot(settings.slotId, settings);
            var slotId = settings.slotId;

            var provider = _providerRegistry.Resolve(settings.avatarProviderDescriptor);
            var source = _moCapSourceRegistry.Resolve(settings.moCapSourceDescriptor);
            source.Initialize(settings.moCapSourceDescriptor.Config);
            var avatar = await provider.RequestAvatarAsync(
                settings.avatarProviderDescriptor.Config, cancellationToken);

            _resources[slotId] = new SlotResources
            {
                Provider = provider,
                Source = source,
                Avatar = avatar,
            };

            TransitionState(slotId, SlotState.Active);
        }

        /// <summary>
        /// 指定した slotId の Slot を削除する。
        /// Provider のアバター解放・Dispose、MoCapSourceRegistry.Release を順に実行し、
        /// <see cref="SlotRegistry"/> から除去して Disposed 状態に遷移させる。
        /// 存在しない slotId の場合は <see cref="InvalidOperationException"/> をスローする。
        /// </summary>
        public UniTask RemoveSlotAsync(string slotId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var handle = _slotRegistry.GetSlot(slotId);
            if (handle == null)
                throw new InvalidOperationException(
                    $"slotId '{slotId}' は登録されていないため削除できません。");

            var previous = handle.State;

            if (_resources.TryGetValue(slotId, out var res))
            {
                if (res.Provider != null)
                {
                    if (res.Avatar != null) res.Provider.ReleaseAvatar(res.Avatar);
                    res.Provider.Dispose();
                }
                if (res.Source != null) _moCapSourceRegistry.Release(res.Source);
                _resources.Remove(slotId);
            }

            _slotRegistry.RemoveSlot(slotId);
            _stateChanged.OnNext(new SlotStateChangedEvent(slotId, previous, SlotState.Disposed));
            return UniTask.CompletedTask;
        }

        /// <summary>登録済み Slot の一覧を返す。</summary>
        public IReadOnlyList<SlotHandle> GetSlots() => _slotRegistry.GetAllSlots();

        /// <summary>指定した slotId の <see cref="SlotHandle"/> を返す。存在しない場合は null を返す。</summary>
        public SlotHandle GetSlot(string slotId) => _slotRegistry.GetSlot(slotId);

        /// <summary>
        /// <see cref="OnSlotStateChanged"/> Subject を Complete する。
        /// 全 Slot の解放処理はタスク 12.7 で拡張する。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stateChanged.OnCompleted();
            _stateChanged.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SlotManager));
        }

        private void TransitionState(string slotId, SlotState newState)
        {
            var handle = _slotRegistry.GetSlot(slotId);
            if (handle == null) return;
            var previous = handle.State;
            if (previous == newState) return;

            _slotRegistry.UpdateSlotState(slotId, newState);
            _stateChanged.OnNext(new SlotStateChangedEvent(slotId, previous, newState));
        }

        /// <summary>
        /// Slot に紐付く外部リソースのスナップショット。
        /// RemoveSlotAsync / Dispose で Provider.ReleaseAvatar → Provider.Dispose → Registry.Release の順に解放する。
        /// </summary>
        private struct SlotResources
        {
            public IAvatarProvider Provider;
            public IMoCapSource Source;
            public GameObject Avatar;
        }
    }
}
