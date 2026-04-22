using System.Collections.Generic;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IFacialController 具象型の登録・解決を担う Registry (将来用)。
    /// 同一 typeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// </summary>
    public interface IFacialControllerRegistry
    {
        /// <summary>
        /// controllerTypeId をキーとして IFacialControllerFactory を登録する。
        /// 同一 controllerTypeId が既に登録されている場合は <see cref="RegistryConflictException"/> をスローする。
        /// </summary>
        void Register(string controllerTypeId, IFacialControllerFactory factory);

        /// <summary>
        /// FacialControllerDescriptor に対応する IFacialController インスタンスを生成して返す。
        /// 未登録 typeId の場合は <see cref="KeyNotFoundException"/> をスローする。
        /// </summary>
        IFacialController Resolve(FacialControllerDescriptor descriptor);

        /// <summary>
        /// 登録済みの controllerTypeId 一覧を返す。エディタ UI が利用可能な候補を列挙するために使用する。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }
}
