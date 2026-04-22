using EVMC4U;
using UnityEngine;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC
{
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

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
                s_refCount = 0;
            }
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
