using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IProviderRegistry / IMoCapSourceRegistry 等への静的アクセスポイント。
    /// Editor 起動時・ランタイム起動時に同一インスタンスを共有する。
    /// Domain Reload OFF 対応: SubsystemRegistration タイミングで自動リセットする。
    /// テスト時は <see cref="ResetForTest"/> / Override*() を使用してインスタンスを差し替える。
    /// </summary>
    /// <remarks>
    /// Factory 自己登録パターン (design.md §3.15 / §4.5):
    /// <code>
    /// // ランタイム登録エントリポイント
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    /// private static void RegisterRuntime()
    /// {
    ///     try
    ///     {
    ///         RegistryLocator.ProviderRegistry.Register("TypeId", new ConcreteFactory());
    ///     }
    ///     catch (RegistryConflictException ex)
    ///     {
    ///         RegistryLocator.ErrorChannel.Publish(
    ///             new SlotError("", SlotErrorCategory.RegistryConflict, ex, System.DateTime.UtcNow));
    ///     }
    /// }
    /// </code>
    /// Domain Reload OFF 時でも <see cref="ResetForTest"/> が SubsystemRegistration で自動実行されるため二重登録は発生しない。
    /// </remarks>
    public static class RegistryLocator
    {
        // --- 公開プロパティ (遅延初期化: Interlocked.CompareExchange パターン) ---

        /// <summary>IProviderRegistry への静的アクセスポイント。遅延初期化 (Interlocked.CompareExchange によるスレッドセーフ)。</summary>
        public static IProviderRegistry ProviderRegistry
            => s_providerRegistry
               ?? Interlocked.CompareExchange(ref s_providerRegistry, new DefaultProviderRegistry(), null)
               ?? s_providerRegistry;

        /// <summary>IMoCapSourceRegistry への静的アクセスポイント。遅延初期化 (Interlocked.CompareExchange によるスレッドセーフ)。</summary>
        public static IMoCapSourceRegistry MoCapSourceRegistry
            => s_moCapSourceRegistry
               ?? Interlocked.CompareExchange(ref s_moCapSourceRegistry, new DefaultMoCapSourceRegistry(), null)
               ?? s_moCapSourceRegistry;

        /// <summary>IFacialControllerRegistry への静的アクセスポイント。遅延初期化 (将来用)。</summary>
        public static IFacialControllerRegistry FacialControllerRegistry
            => s_facialControllerRegistry
               ?? Interlocked.CompareExchange(ref s_facialControllerRegistry, new DefaultFacialControllerRegistry(), null)
               ?? s_facialControllerRegistry;

        /// <summary>ILipSyncSourceRegistry への静的アクセスポイント。遅延初期化 (将来用)。</summary>
        public static ILipSyncSourceRegistry LipSyncSourceRegistry
            => s_lipSyncSourceRegistry
               ?? Interlocked.CompareExchange(ref s_lipSyncSourceRegistry, new DefaultLipSyncSourceRegistry(), null)
               ?? s_lipSyncSourceRegistry;

        /// <summary>ISlotErrorChannel への静的アクセスポイント。遅延初期化 (Interlocked.CompareExchange によるスレッドセーフ)。</summary>
        public static ISlotErrorChannel ErrorChannel
            => s_errorChannel
               ?? Interlocked.CompareExchange(ref s_errorChannel, new DefaultSlotErrorChannel(), null)
               ?? s_errorChannel;

        // --- テスト・Domain Reload OFF 対応 API ---

        /// <summary>
        /// 全 Registry インスタンスと <see cref="s_suppressedErrors"/> をリセットする。
        /// Domain Reload OFF (Enter Play Mode 最適化) 設定下での二重登録防止に使用する。
        /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> タイミングで自動実行される。
        /// ユニットテストの [SetUp] / [TearDown] でも明示的に呼び出すこと。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTest()
        {
            s_providerRegistry = null;
            s_moCapSourceRegistry = null;
            s_facialControllerRegistry = null;
            s_lipSyncSourceRegistry = null;
            s_errorChannel = null;
            s_suppressedErrors.Clear();
        }

        /// <summary>ProviderRegistry インスタンスを差し替える (テスト用)。</summary>
        public static void OverrideProviderRegistry(IProviderRegistry registry)
            => s_providerRegistry = registry;

        /// <summary>MoCapSourceRegistry インスタンスを差し替える (テスト用)。</summary>
        public static void OverrideMoCapSourceRegistry(IMoCapSourceRegistry registry)
            => s_moCapSourceRegistry = registry;

        /// <summary>FacialControllerRegistry インスタンスを差し替える (テスト用)。</summary>
        public static void OverrideFacialControllerRegistry(IFacialControllerRegistry registry)
            => s_facialControllerRegistry = registry;

        /// <summary>LipSyncSourceRegistry インスタンスを差し替える (テスト用)。</summary>
        public static void OverrideLipSyncSourceRegistry(ILipSyncSourceRegistry registry)
            => s_lipSyncSourceRegistry = registry;

        /// <summary>ErrorChannel インスタンスを差し替える (テスト用)。</summary>
        public static void OverrideErrorChannel(ISlotErrorChannel channel)
            => s_errorChannel = channel;

        // --- 内部フィールド ---

        private static IProviderRegistry s_providerRegistry;
        private static IMoCapSourceRegistry s_moCapSourceRegistry;
        private static IFacialControllerRegistry s_facialControllerRegistry;
        private static ILipSyncSourceRegistry s_lipSyncSourceRegistry;
        private static ISlotErrorChannel s_errorChannel;

        /// <summary>
        /// Debug.LogError 抑制用 HashSet (<see cref="DefaultSlotErrorChannel"/> から参照)。
        /// 同一 (SlotId, Category) の 2 回目以降の <c>Debug.LogError</c> を抑制する。
        /// <see cref="ResetForTest"/> 呼び出し時にクリアされる。
        /// </summary>
        internal static readonly HashSet<(string SlotId, SlotErrorCategory Category)> s_suppressedErrors
            = new HashSet<(string, SlotErrorCategory)>();
    }
}
