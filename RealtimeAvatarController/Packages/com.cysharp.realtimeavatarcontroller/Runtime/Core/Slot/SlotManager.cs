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
    /// 本クラスの 12.5 時点ではコア実装・weight クランプ・初期化失敗時の
    /// <c>Created → Disposed</c> 遷移に加え、<see cref="RemoveSlotAsync"/> の
    /// 厳密順序・例外耐性リソース解放を提供する:
    /// 正常系 Add/Remove・重複 slotId チェック・状態遷移通知・GetSlots/GetSlot・
    /// AddSlotAsync 内の <c>Mathf.Clamp01(settings.weight)</c>・
    /// Provider / Source Resolve・<see cref="IAvatarProvider.RequestAvatarAsync"/> 失敗時の
    /// 例外捕捉 → 部分リソース解放 → <see cref="SlotErrorCategory.InitFailure"/> 発行 → Disposed 遷移・
    /// RemoveSlotAsync の <c>Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release</c>
    /// 順序での解放と各段階の例外 catch + <see cref="Debug.LogWarning"/> 継続処理。
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
            // 初期化失敗の try-catch より前で発生させ、呼び出し側に伝播させる (Req 2.3)。
            _slotRegistry.AddSlot(settings.slotId, settings);
            var slotId = settings.slotId;

            IAvatarProvider provider = null;
            IMoCapSource source = null;
            GameObject avatar = null;
            try
            {
                provider = _providerRegistry.Resolve(settings.avatarProviderDescriptor);
                source = _moCapSourceRegistry.Resolve(settings.moCapSourceDescriptor);
                source.Initialize(settings.moCapSourceDescriptor.Config);
                avatar = await provider.RequestAvatarAsync(
                    settings.avatarProviderDescriptor.Config, cancellationToken);
            }
            catch (Exception ex)
            {
                // タスク 12.4 / Req 3.7, 3.8, 12.4:
                // Provider / Source Resolve・RequestAvatarAsync の例外を捕捉し、
                // 部分的に確保済みリソースを可能な限り解放してから Slot を Disposed に遷移させ、
                // ISlotErrorChannel に InitFailure を発行する。UniTask は正常完了とする。
                HandleInitFailure(slotId, provider, source, avatar, ex);
                return;
            }

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
        /// <c>Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release</c> の順で解放し、
        /// <see cref="SlotRegistry"/> から除去して Disposed 状態に遷移させる。
        /// 存在しない slotId の場合は <see cref="InvalidOperationException"/> をスローする。
        /// <para>
        /// Req 3.5: 各解放ステップで発生した例外は catch して <see cref="Debug.LogWarning"/> に記録し、
        /// 残余リソースの解放を継続する。Req 3.6 / 10.2: <see cref="IMoCapSource.Dispose"/> を直接呼ばず、
        /// 必ず <see cref="IMoCapSourceRegistry.Release"/> 経由で参照カウントをデクリメントする。
        /// </para>
        /// </summary>
        public UniTask RemoveSlotAsync(string slotId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var handle = _slotRegistry.GetSlot(slotId);
            if (handle == null)
                throw new InvalidOperationException(
                    $"slotId '{slotId}' は登録されていないため削除できません。");

            var previous = handle.State;
            _resources.TryGetValue(slotId, out var res);
            _resources.Remove(slotId);

            ReleaseSlotResources(slotId, res);

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

        /// <summary>
        /// 初期化失敗時の後処理。部分的に取得した Provider / Source / Avatar を解放し、
        /// Slot を <see cref="SlotRegistry"/> から除去して <see cref="SlotState.Disposed"/> 遷移イベントを
        /// 発行し、<see cref="ISlotErrorChannel"/> に <see cref="SlotErrorCategory.InitFailure"/> を通知する。
        /// クリーンアップ途中で発生した例外は握り潰し、残余リソース解放を継続する (Req 3.5)。
        /// </summary>
        private void HandleInitFailure(
            string slotId,
            IAvatarProvider provider,
            IMoCapSource source,
            GameObject avatar,
            Exception initException)
        {
            try
            {
                if (provider != null)
                {
                    if (avatar != null)
                    {
                        try { provider.ReleaseAvatar(avatar); }
                        catch (Exception cleanupEx) { Debug.LogWarning($"[SlotManager] InitFailure cleanup: ReleaseAvatar 失敗 ({slotId}): {cleanupEx}"); }
                    }
                    try { provider.Dispose(); }
                    catch (Exception cleanupEx) { Debug.LogWarning($"[SlotManager] InitFailure cleanup: Provider.Dispose 失敗 ({slotId}): {cleanupEx}"); }
                }
                if (source != null)
                {
                    try { _moCapSourceRegistry.Release(source); }
                    catch (Exception cleanupEx) { Debug.LogWarning($"[SlotManager] InitFailure cleanup: MoCapSourceRegistry.Release 失敗 ({slotId}): {cleanupEx}"); }
                }
            }
            finally
            {
                var handle = _slotRegistry.GetSlot(slotId);
                var previous = handle?.State ?? SlotState.Created;
                if (handle != null)
                {
                    try { _slotRegistry.RemoveSlot(slotId); }
                    catch (InvalidOperationException) { /* 既に除去済みの場合は無視 */ }
                }
                _stateChanged.OnNext(new SlotStateChangedEvent(slotId, previous, SlotState.Disposed));
                _errorChannel.Publish(new SlotError(slotId, SlotErrorCategory.InitFailure, initException, DateTime.UtcNow));
            }
        }

        /// <summary>
        /// <see cref="RemoveSlotAsync"/> からの共通リソース解放処理。
        /// 厳密順序: <c>Provider.ReleaseAvatar → Provider.Dispose → MoCapSourceRegistry.Release</c>。
        /// 各ステップの例外は catch して <see cref="Debug.LogWarning"/> に記録し、残余リソースの解放を継続する (Req 3.5)。
        /// <see cref="IMoCapSource.Dispose"/> は直接呼び出さず、必ず Registry 経由で参照カウントを管理する (Req 3.6 / 10.2)。
        /// </summary>
        private void ReleaseSlotResources(string slotId, SlotResources res)
        {
            if (res.Provider != null && res.Avatar != null)
            {
                try { res.Provider.ReleaseAvatar(res.Avatar); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SlotManager] Remove: Provider.ReleaseAvatar 失敗 ({slotId}): {ex}");
                }
            }
            if (res.Provider != null)
            {
                try { res.Provider.Dispose(); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SlotManager] Remove: Provider.Dispose 失敗 ({slotId}): {ex}");
                }
            }
            if (res.Source != null)
            {
                try { _moCapSourceRegistry.Release(res.Source); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SlotManager] Remove: MoCapSourceRegistry.Release 失敗 ({slotId}): {ex}");
                }
            }
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
