using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// リップシンクデータの取得を提供するソースインターフェース (受け口のみ。初期段階では具象実装なし)。
    /// SlotSettings.lipSyncSourceDescriptor が null の場合、Slot にリップシンクは割り当てられない。
    /// </summary>
    public interface ILipSyncSource : IDisposable
    {
        /// <summary>
        /// 初期化。リップシンクパラメータを格納した Config を渡す。
        /// </summary>
        void Initialize(LipSyncSourceConfigBase config);

        /// <summary>
        /// 最新のリップシンクデータを取得する。
        /// 戻り値型は将来の具象実装フェーズで確定する。
        /// 初期段階では object 型で返しキャストすることを許容する。
        /// </summary>
        object FetchLatestLipSync();

        /// <summary>
        /// シャットダウン。リソース解放を行う。
        /// IDisposable.Dispose() と等価。
        /// </summary>
        void Shutdown();
    }
}
