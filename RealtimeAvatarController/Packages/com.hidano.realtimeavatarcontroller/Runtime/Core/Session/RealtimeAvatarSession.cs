using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Avatar.Scene;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// 高レベル接続 API。
    /// VTuber 統合側は <c>RealtimeAvatarSession.AttachMoCapAsync(avatar, "MOVIN")</c> 1 行で
    /// MoCap ソースをアバターへ紐づけられる。SO アセットは生成不要。
    /// 内部で SlotManager・SceneAvatarProvider・MoCap factory のデフォルト config を合成し、
    /// Slot Active 後に <see cref="IMoCapSourceFactory.CreateApplierBridge"/> を呼んで適用パイプラインを構築する。
    /// </summary>
    public static class RealtimeAvatarSession
    {
        private static SlotManager s_slotManager;

        /// <summary>
        /// 登録済み MoCap typeId 一覧。Inspector dropdown / UI 候補列挙に使う。
        /// MoCap パッケージを Package Manager で導入するだけで自動的にこの一覧へ追加される。
        /// </summary>
        public static IReadOnlyList<string> AvailableMoCapTypes
            => RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds();

        /// <summary>
        /// 既存のシーン内 GameObject を <paramref name="mocapTypeId"/> の MoCap ソースに紐づける。
        /// </summary>
        /// <param name="avatar">適用先のシーン内 GameObject。所有権は呼び出し側に残る (Provider は破棄しない)。</param>
        /// <param name="mocapTypeId">MoCap typeId (例: "MOVIN")。<see cref="AvailableMoCapTypes"/> から選ぶ。</param>
        /// <param name="configOverride">省略時は factory の <see cref="IMoCapSourceFactory.CreateDefaultConfig"/> を使う。port 等を変えたいときだけ渡す。</param>
        /// <param name="slotId">省略時は GUID 由来の自動採番。複数同時 attach する場合は明示する。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>
        /// この接続を解放するための <see cref="AttachedSession"/>。Dispose で applier-bridge と Slot を順に解放する。
        /// </returns>
        public static async UniTask<AttachedSession> AttachMoCapAsync(
            GameObject avatar,
            string mocapTypeId,
            MoCapSourceConfigBase configOverride = null,
            string slotId = null,
            CancellationToken cancellationToken = default)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            if (string.IsNullOrEmpty(mocapTypeId))
                throw new ArgumentException("mocapTypeId is required.", nameof(mocapTypeId));

            var registry = RegistryLocator.MoCapSourceRegistry;
            if (!registry.TryGetFactory(mocapTypeId, out var factory))
            {
                throw new ArgumentException(
                    $"MoCap typeId '{mocapTypeId}' is not registered. Available: [{string.Join(", ", AvailableMoCapTypes)}]",
                    nameof(mocapTypeId));
            }

            var resolvedSlotId = string.IsNullOrEmpty(slotId)
                ? $"session-{Guid.NewGuid():N}"
                : slotId;

            var sceneProviderConfig = ScriptableObject.CreateInstance<SceneAvatarProviderConfig>();
            sceneProviderConfig.sceneAvatar = avatar;

            var mocapConfig = configOverride != null ? configOverride : factory.CreateDefaultConfig();
            if (mocapConfig == null)
            {
                throw new InvalidOperationException(
                    $"MoCap factory '{mocapTypeId}' returned null from CreateDefaultConfig() and no configOverride was provided.");
            }

            var settings = ScriptableObject.CreateInstance<SlotSettings>();
            settings.slotId = resolvedSlotId;
            settings.displayName = resolvedSlotId;
            settings.weight = 1.0f;
            settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = SceneAvatarProviderFactory.SceneProviderTypeId,
                Config = sceneProviderConfig,
            };
            settings.moCapSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = mocapTypeId,
                Config = mocapConfig,
            };

            var slotManager = GetOrCreateSlotManager();
            await slotManager.AddSlotAsync(settings, cancellationToken);

            if (!slotManager.TryGetSlotResources(resolvedSlotId, out var source, out _))
            {
                throw new InvalidOperationException(
                    $"Slot '{resolvedSlotId}' resources are not available; AddSlotAsync may have failed (check ErrorChannel for InitFailure).");
            }

            IDisposable applierBridge;
            try
            {
                applierBridge = factory.CreateApplierBridge(source, avatar, mocapConfig);
            }
            catch
            {
                await slotManager.RemoveSlotAsync(resolvedSlotId, cancellationToken);
                throw;
            }

            return new AttachedSession(slotManager, resolvedSlotId, applierBridge);
        }

        /// <summary>
        /// 内部で保持する SlotManager を破棄する。テスト用 / アプリ終了時の明示的クリーンアップ用。
        /// </summary>
        public static void Shutdown()
        {
            s_slotManager?.Dispose();
            s_slotManager = null;
        }

        private static SlotManager GetOrCreateSlotManager()
        {
            if (s_slotManager == null)
            {
                s_slotManager = new SlotManager(
                    RegistryLocator.ProviderRegistry,
                    RegistryLocator.MoCapSourceRegistry,
                    RegistryLocator.ErrorChannel);
            }
            return s_slotManager;
        }
    }

    /// <summary>
    /// <see cref="RealtimeAvatarSession.AttachMoCapAsync"/> の戻り値。
    /// Dispose で applier-bridge → Slot の順に解放する。
    /// </summary>
    public sealed class AttachedSession : IDisposable
    {
        private readonly SlotManager _slotManager;
        private IDisposable _applierBridge;
        private bool _disposed;

        /// <summary>この接続が紐づく Slot id。</summary>
        public string SlotId { get; }

        internal AttachedSession(SlotManager slotManager, string slotId, IDisposable applierBridge)
        {
            _slotManager = slotManager;
            SlotId = slotId;
            _applierBridge = applierBridge;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _applierBridge?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AttachedSession] applier-bridge Dispose 中に例外 (slotId={SlotId}): {ex}");
            }
            _applierBridge = null;

            try
            {
                if (_slotManager != null && _slotManager.GetSlot(SlotId) != null)
                {
                    _slotManager.RemoveSlotAsync(SlotId).Forget();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AttachedSession] Slot 解放中に例外 (slotId={SlotId}): {ex}");
            }
        }
    }
}
