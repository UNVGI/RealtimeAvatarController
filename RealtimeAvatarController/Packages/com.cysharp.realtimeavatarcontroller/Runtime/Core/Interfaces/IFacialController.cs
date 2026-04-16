using System;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// 表情制御の受け口インターフェース (受け口のみ。初期段階では具象実装なし)。
    /// SlotSettings.facialControllerDescriptor が null の場合、Slot に表情制御は割り当てられない。
    /// </summary>
    public interface IFacialController : IDisposable
    {
        /// <summary>
        /// 初期化。制御対象アバターの GameObject を渡す。
        /// </summary>
        void Initialize(GameObject avatarRoot);

        /// <summary>
        /// 表情データを適用する。
        /// 引数型 FacialData は将来の具象実装フェーズで確定する。
        /// 初期段階では object 型で受け取りキャストすることを許容する。
        /// </summary>
        void ApplyFacialData(object facialData);

        /// <summary>
        /// シャットダウン。IDisposable.Dispose() と等価。
        /// </summary>
        void Shutdown();
    }
}
