using System;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IMoCapSource の具象インスタンスを生成するファクトリ。
    /// 具象 Factory は IMoCapSourceFactory を実装し、属性ベース自動登録で IMoCapSourceRegistry に自己登録する。
    /// </summary>
    public interface IMoCapSourceFactory
    {
        /// <summary>
        /// config を元に IMoCapSource インスタンスを生成する。
        /// config は MoCapSourceConfigBase 派生型にキャストして使用すること。
        /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
        /// </summary>
        IMoCapSource Create(MoCapSourceConfigBase config);

        /// <summary>
        /// 既定値で初期化された MoCapSourceConfigBase 派生インスタンスを生成する。
        /// 高レベル接続 API (例: <c>RealtimeAvatarSession.AttachMoCapAsync</c>) が override config 未指定時に使用する。
        /// 戻り値は通常 <c>ScriptableObject.CreateInstance&lt;TConfig&gt;()</c> の結果であり、SO アセット非依存。
        /// </summary>
        MoCapSourceConfigBase CreateDefaultConfig();

        /// <summary>
        /// 与えられた <paramref name="source"/> と <paramref name="avatar"/> を結ぶ適用パイプラインを構築し、
        /// 結合されたライフサイクルを <see cref="IDisposable"/> として返す。Dispose 時に内部の applier・購読を解放する。
        /// MoCap プロトコルごとに必要な applier (MOVIN: 直接 Transform 書込 / VMC: Humanoid 経路) と
        /// MotionStream の購読を 1 メソッドに閉じ込めるための拡張点。
        /// 標準パイプライン (LateUpdate 駆動など) を必要とする実装は <see cref="System.NotSupportedException"/> をスローしてよい。
        /// </summary>
        IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config);
    }
}
