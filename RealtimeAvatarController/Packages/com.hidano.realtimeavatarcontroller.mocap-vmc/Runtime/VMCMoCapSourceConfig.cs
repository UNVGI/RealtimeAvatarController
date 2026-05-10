using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// VMC 受信ソースの設定 (MoCapSourceDescriptor.Config として使用)。
    /// SO アセット編集 (シナリオ X) と ScriptableObject.CreateInstance 動的生成 (シナリオ Y) の両方を許容する。
    /// Inspector 上で MoCapSourceDescriptor.Config フィールドへのドラッグ&amp;ドロップ参照設定が可能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Open Issue L-2 対応</b>:
    /// <c>bindAddress</c> のデフォルト値は requirements.md 要件 3-3 で <c>"127.0.0.1"</c> と規定されていたが、
    /// design.md §5.1 にて外部 VMC 送信ソース対応のため <c>"0.0.0.0"</c> (全インターフェース受信) に合意変更された。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "RealtimeAvatarController/MoCap/VMC Config",
        fileName = "VMCMoCapSourceConfig")]
    public class VMCMoCapSourceConfig : MoCapSourceConfigBase
    {
        /// <summary>
        /// VMC データ受信ポート番号。有効範囲: 1025〜65535。
        /// デフォルト: 39539 (VMC プロトコル標準ポート)。
        /// </summary>
        [Range(1025, 65535)]
        public int port = 39539;

        /// <summary>
        /// 受信アドレス (IPv4 文字列)。
        /// デフォルト: "0.0.0.0" (全インターフェースで受信)。
        /// </summary>
        public string bindAddress = "0.0.0.0";
    }
}
