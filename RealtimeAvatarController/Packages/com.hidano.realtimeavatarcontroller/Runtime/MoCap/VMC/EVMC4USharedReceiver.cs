using System;
using System.Collections.Generic;
using EVMC4U;
using UnityEngine;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// <see cref="EVMC4USharedReceiver"/> の <c>LateUpdate</c> Tick 駆動対象となる Adapter が実装する契約
    /// (tasks.md 3.5 / design.md §5.1)。
    /// </summary>
    /// <remarks>
    /// タスク 3.5 時点では具象実装 (<c>EVMC4UMoCapSource</c>) は Phase 4 で登場するため、
    /// 本インタフェースを介して共有 Receiver 側は具象型に依存しない形で Tick 駆動を提供する。
    /// Phase 4 の <c>EVMC4UMoCapSource</c> は本インタフェースを実装する。
    /// </remarks>
    internal interface IEVMC4UMoCapAdapter
    {
        /// <summary>LateUpdate 毎に呼び出される Tick。内部 Dictionary を snapshot して OnNext を発行する想定。</summary>
        void Tick();

        /// <summary>Tick 実行中に発生した例外を受け取り、Adapter 自身の ErrorChannel 経由で公開させる (要件 8.3)。</summary>
        void HandleTickException(Exception exception);
    }

    /// <summary>
    /// プロセスワイド単一の EVMC4U <see cref="ExternalReceiver"/> をホストする共有コンポーネント
    /// (design.md §4.3 / requirements.md 要件 2.1, 2.2, 2.3, 2.4, 4.4)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>責務</b>:
    /// <list type="bullet">
    ///   <item>DontDestroyOnLoad GameObject 1 個を <c>s_refCount</c> で生存管理する</item>
    ///   <item>生成時に <see cref="ExternalReceiver.Model"/> を <c>null</c>、
    ///         <see cref="ExternalReceiver.RootPositionSynchronize"/> / <see cref="ExternalReceiver.RootRotationSynchronize"/>
    ///         を <c>false</c> に強制する (要件 2.4)</item>
    ///   <item><see cref="uOscServer.autoStart"/> を <c>false</c> に設定し、
    ///         <c>ApplyReceiverSettings</c> (task 3.6) で明示的にポート bind する</item>
    /// </list>
    /// </para>
    /// <para>
    /// 本ファイルは Phase 3 のサブタスクを段階的に積み上げる:
    /// 3.2 でシングルトン骨格を実装し、3.3 で Domain Reload OFF 向け静的クリア、
    /// 3.5 で Subscribe / LateUpdate、3.6 で <c>ApplyReceiverSettings</c> を追加する。
    /// </para>
    /// </remarks>
    public sealed class EVMC4USharedReceiver : MonoBehaviour
    {
        private const string HostGameObjectName = "[EVMC4U Shared Receiver]";

        private static EVMC4USharedReceiver s_instance;
        private static int s_refCount;

        private ExternalReceiver _receiver;
        private uOscServer _server;
        private readonly HashSet<IEVMC4UMoCapAdapter> _subscribers = new HashSet<IEVMC4UMoCapAdapter>();

        /// <summary>共有 <see cref="ExternalReceiver"/> への read-only アクセス (design.md §4.3)。</summary>
        public ExternalReceiver Receiver => _receiver;

        /// <summary>
        /// 共有 <see cref="EVMC4USharedReceiver"/> を確保する。未生成ならシーン非依存の
        /// GameObject と <see cref="uOscServer"/> / <see cref="ExternalReceiver"/> を新規生成し、
        /// 既に生成済みなら同一インスタンスを返しつつ refCount をインクリメントする
        /// (要件 2.1, 2.2)。
        /// </summary>
        public static EVMC4USharedReceiver EnsureInstance()
        {
            if (s_instance == null)
            {
                s_instance = CreateInstance();
            }

            s_refCount++;
            return s_instance;
        }

        /// <summary>
        /// 参照カウントをデクリメントする。0 到達時に共有 GameObject を破棄する (要件 2.3)。
        /// EditMode テスト向けに、Play 中でない場合は <see cref="Object.DestroyImmediate(Object)"/>
        /// を用いる。
        /// </summary>
        public void Release()
        {
            if (s_refCount <= 0)
            {
                return;
            }

            s_refCount--;
            if (s_refCount > 0)
            {
                return;
            }

            s_refCount = 0;
            var host = s_instance != null ? s_instance.gameObject : null;
            s_instance = null;

            if (host == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(host);
            }
            else
            {
                DestroyImmediate(host);
            }
        }

        private static EVMC4USharedReceiver CreateInstance()
        {
            var go = new GameObject(HostGameObjectName);
            // uOscServer が Awake/OnEnable で autoStart を参照する前に構成できるよう、
            // 生成直後は GameObject を非アクティブにする。
            go.SetActive(false);

            var server = go.AddComponent<uOscServer>();
            server.autoStart = false;

            var receiver = go.AddComponent<ExternalReceiver>();
            receiver.Model = null;
            receiver.RootPositionSynchronize = false;
            receiver.RootRotationSynchronize = false;

            var shared = go.AddComponent<EVMC4USharedReceiver>();
            shared._server = server;
            shared._receiver = receiver;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.SetActive(true);
            return shared;
        }

        // --- Subscribe / LateUpdate Tick 駆動 (task 3.5 / 要件 4.3, 4.4, 8.3) ---

        /// <summary>
        /// <paramref name="adapter"/> を LateUpdate Tick の駆動対象に追加する。
        /// 重複登録は <see cref="HashSet{T}"/> によって自動的に無視される。
        /// </summary>
        internal void Subscribe(IEVMC4UMoCapAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }
            _subscribers.Add(adapter);
        }

        /// <summary>
        /// <paramref name="adapter"/> を LateUpdate Tick の駆動対象から除外する。
        /// 未登録でも例外は投げない (冪等)。
        /// </summary>
        internal void Unsubscribe(IEVMC4UMoCapAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }
            _subscribers.Remove(adapter);
        }

        private void LateUpdate()
        {
            if (_subscribers.Count == 0)
            {
                return;
            }

            // 列挙中に Unsubscribe されても安全なようスナップショットを取る。
            var snapshot = new IEVMC4UMoCapAdapter[_subscribers.Count];
            _subscribers.CopyTo(snapshot);

            for (var i = 0; i < snapshot.Length; i++)
            {
                var adapter = snapshot[i];
                if (adapter == null)
                {
                    continue;
                }
                try
                {
                    adapter.Tick();
                }
                catch (Exception ex)
                {
                    // 他の Adapter の Tick を止めないよう、該当 Adapter のみに通知して続行する (要件 8.3)。
                    try
                    {
                        adapter.HandleTickException(ex);
                    }
                    catch
                    {
                        // HandleTickException 自体で更に例外が起きても LateUpdate 全体は続行する。
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
                s_refCount = 0;
            }
        }

        // --- Domain Reload OFF 向け静的クリア (task 3.3 / 要件 6.4) ---

        /// <summary>
        /// Enter Play Mode 最適化 (Domain Reload OFF) 下でも static フィールドが
        /// 持ち越されないよう、<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> 段で
        /// <c>s_instance</c> / <c>s_refCount</c> を強制的にリセットする。
        /// </summary>
        /// <remarks>
        /// <para>
        /// GameObject への参照は触らない。Unity はこの時点で旧シーン上の GameObject を既に破棄済みなので、
        /// ここで追加の <c>Destroy</c> を呼ぶと二重破棄・null 参照の温床になるためである
        /// (design.md §4.3 / §13)。
        /// </para>
        /// <para>
        /// 本処理と <see cref="RealtimeAvatarController.Core.RegistryLocator.ResetForTest"/> は
        /// 同じ <c>SubsystemRegistration</c> タイミングで実行されるが、いずれも static を null に戻すだけで
        /// 相互依存が無いため実行順序は不問 (task 3.4 参照)。
        /// </para>
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            s_instance = null;
            s_refCount = 0;
        }

        // --- テスト専用 API (task 3.1 / 3.3) ---

        /// <summary>
        /// 静的状態 (<see cref="s_instance"/> / <see cref="s_refCount"/>) を強制的にリセットする。
        /// テストの [SetUp] / [TearDown] から呼ぶほか、task 3.3 で
        /// <c>RuntimeInitializeOnLoadMethod</c> 属性からも呼ばれる。
        /// 既存 GameObject が残っていれば破棄する。
        /// </summary>
        public static void ResetForTest()
        {
            if (s_instance != null)
            {
                var host = s_instance.gameObject;
                s_instance = null;
                s_refCount = 0;
                if (host != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(host);
                    }
                    else
                    {
                        DestroyImmediate(host);
                    }
                }
                return;
            }

            s_refCount = 0;
        }

        /// <summary>テスト専用: 現在の static singleton 参照を露出する。</summary>
        public static EVMC4USharedReceiver InstanceForTest => s_instance;

        /// <summary>テスト専用: 現在の static refCount を露出する。</summary>
        public static int RefCountStaticForTest => s_refCount;

        /// <summary>テスト専用: インスタンスメソッド経由で現 refCount を露出する。</summary>
        public int RefCountForTest => s_refCount;
    }
}
