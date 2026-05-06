using System.Collections.Generic;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IMoCapSource 具象型の登録・解決・参照共有・候補列挙を担う Registry。
    /// 同一 Descriptor に対して複数の Resolve() が呼ばれた場合、同一インスタンスを返す (参照カウント管理)。
    /// 同一 typeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// </summary>
    public interface IMoCapSourceRegistry
    {
        /// <summary>
        /// sourceTypeId をキーとして IMoCapSourceFactory を登録する。
        /// 同一 sourceTypeId が既に登録されている場合は <see cref="RegistryConflictException"/> をスローする。
        /// </summary>
        void Register(string sourceTypeId, IMoCapSourceFactory factory);

        /// <summary>
        /// MoCapSourceDescriptor に対応する IMoCapSource インスタンスを返す。
        /// 同一 Descriptor に対して既にインスタンスが存在する場合は参照を共有し、参照カウントをインクリメントする。
        /// 未登録 typeId の場合は <see cref="KeyNotFoundException"/> をスローする。
        /// </summary>
        IMoCapSource Resolve(MoCapSourceDescriptor descriptor);

        /// <summary>
        /// IMoCapSource の参照を解放する。参照カウントをデクリメントし、0 になった時点で Dispose() を呼ぶ。
        /// </summary>
        void Release(IMoCapSource source);

        /// <summary>
        /// 登録済みの sourceTypeId 一覧を返す。エディタ UI が利用可能な候補を列挙するために使用する。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();

        /// <summary>
        /// 登録済み Factory を返す。未登録の場合は false を返す。
        /// 高レベル接続 API (例: <c>RealtimeAvatarSession.AttachMoCapAsync</c>) が
        /// <c>IMoCapSourceFactory.CreateDefaultConfig</c> / <c>CreateApplierBridge</c> を呼ぶために使用する。
        /// </summary>
        bool TryGetFactory(string sourceTypeId, out IMoCapSourceFactory factory);
    }
}
