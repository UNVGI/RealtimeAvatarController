using System.Collections.Generic;
using System.Diagnostics;
using RealtimeAvatarController.Motion;
using UnityEngine;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC.Internal
{
    /// <summary>
    /// VMC プロトコルで受信した Bone / Root 更新を蓄積し、<see cref="HumanoidMotionFrame"/> に
    /// フラッシュする内部ビルダ (design.md §6.3 / §7.2 / §7.3, requirements.md 要件 2-7, 5-1, 5-3, 5-4, 7-3)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>スレッド設計</b>: design.md §12 の通りワーカースレッド (uOSC 受信コールバック) 専用であり、
    /// メインスレッドから直接アクセスしない。したがって本クラスはロックを持たない。
    /// </para>
    /// <para>
    /// <b>フラッシュ方針 (OI-1 初期版)</b>: <see cref="SetBone"/> を 1 度以上受信した状態で
    /// <see cref="TryFlush"/> が呼ばれると、その時点までに蓄積されたボーンデータから
    /// <see cref="HumanoidMotionFrame"/> を 1 本生成し、内部ボーン辞書をリセットする。
    /// Bone 未受信の場合は無効フレームを生成せず <c>false</c> を返す。
    /// </para>
    /// <para>
    /// <b>Root の永続化</b>: Root は Bone より低頻度で送られる可能性があるため、
    /// <see cref="TryFlush"/> 後も直近の値を保持する (次フレームでも同じ Root を参照可能)。
    /// 初期状態は <see cref="Vector3.zero"/> / <see cref="Quaternion.identity"/>。
    /// </para>
    /// <para>
    /// <b>Muscle 変換 (初期版・簡易実装)</b>: design.md §6.3 に基づき <c>HumanPoseHandler</c> は使用せず、
    /// 受信したボーン回転クォータニオンをオイラー角へ分解し、<see cref="HumanTrait.MuscleFromBone"/>
    /// で解決したインデックス位置に正規化値 (度 / 180°) を格納する。未受信ボーンに対応する
    /// muscle インデックスは <c>0.0f</c> (アイドルポーズ = ゼロ回転) のままとする (design.md §7.3)。
    /// </para>
    /// </remarks>
    internal sealed class VmcFrameBuilder
    {
        private readonly Dictionary<HumanBodyBones, (Vector3 position, Quaternion rotation)> _bones
            = new Dictionary<HumanBodyBones, (Vector3 position, Quaternion rotation)>();

        private Vector3 _rootPosition = Vector3.zero;
        private Quaternion _rootRotation = Quaternion.identity;

        /// <summary>
        /// <c>/VMC/Ext/Root/Pos</c> の引数 <c>(string name, float px, float py, float pz, float qx, float qy, float qz, float qw)</c>
        /// から Root の位置・回転を更新する (design.md §7.2)。
        /// </summary>
        /// <param name="data">uOSC 受信メッセージ。</param>
        /// <remarks>
        /// 引数不足・型不一致時は例外 (<see cref="System.IndexOutOfRangeException"/> 等) をそのまま伝播する。
        /// 呼び出し元 <see cref="VmcMessageRouter"/> が try-catch で捕捉し <c>PublishError</c> に委譲する。
        /// </remarks>
        public void SetRoot(Message data)
        {
            var values = data.values;
            // values[0] は name 文字列。初期版では参照しない。
            var px = (float)values[1];
            var py = (float)values[2];
            var pz = (float)values[3];
            var qx = (float)values[4];
            var qy = (float)values[5];
            var qz = (float)values[6];
            var qw = (float)values[7];

            _rootPosition = new Vector3(px, py, pz);
            _rootRotation = new Quaternion(qx, qy, qz, qw);
        }

        /// <summary>
        /// <c>/VMC/Ext/Bone/Pos</c> の引数 <c>(string boneName, float px, float py, float pz, float qx, float qy, float qz, float qw)</c>
        /// から単一ボーンの位置・回転を蓄積する (design.md §6.3)。
        /// </summary>
        /// <param name="data">uOSC 受信メッセージ。</param>
        /// <remarks>
        /// ボーン名が <see cref="VmcBoneMapper.TryGetBone"/> で解決できない場合は黙って破棄する
        /// (未知ボーンは無視する方針 / design.md §6.2 と整合)。
        /// 引数不足・型不一致時は例外をそのまま伝播する (SetRoot と同様)。
        /// </remarks>
        public void SetBone(Message data)
        {
            var values = data.values;
            var boneName = (string)values[0];
            var px = (float)values[1];
            var py = (float)values[2];
            var pz = (float)values[3];
            var qx = (float)values[4];
            var qy = (float)values[5];
            var qz = (float)values[6];
            var qw = (float)values[7];

            if (!VmcBoneMapper.TryGetBone(boneName, out var bone))
            {
                return;
            }

            _bones[bone] = (new Vector3(px, py, pz), new Quaternion(qx, qy, qz, qw));
        }

        /// <summary>
        /// 蓄積済みのボーン状態から <see cref="HumanoidMotionFrame"/> を 1 本構築する。
        /// </summary>
        /// <param name="frame">構築に成功した場合はフレーム、失敗時は <c>null</c>。</param>
        /// <returns>
        /// 蓄積ボーンが 1 件以上あれば <c>true</c>、ボーン未受信なら <c>false</c>
        /// (design.md §7.3 / tasks.md タスク 5-1: 無効フレーム判定)。
        /// </returns>
        /// <remarks>
        /// 成功時は内部ボーン辞書をリセットし次フレーム受信に備える。Root 状態は保持する。
        /// </remarks>
        public bool TryFlush(out HumanoidMotionFrame frame)
        {
            if (_bones.Count == 0)
            {
                frame = null;
                return false;
            }

            var muscles = new float[HumanTrait.MuscleCount];
            foreach (var kv in _bones)
            {
                var bone = kv.Key;
                var rotation = kv.Value.rotation;
                WriteBoneMuscles(muscles, bone, rotation);
            }

            // design.md §6.4: Stopwatch ベースの秒数でタイムスタンプを打刻する。
            double timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            frame = new HumanoidMotionFrame(timestamp, muscles, _rootPosition, _rootRotation);

            _bones.Clear();
            return true;
        }

        /// <summary>
        /// ボーン回転クォータニオンを Muscle 値配列の該当インデックスへ書き込む (初期版・簡易実装)。
        /// </summary>
        private static void WriteBoneMuscles(float[] muscles, HumanBodyBones bone, Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            var boneIndex = (int)bone;

            for (int dof = 0; dof < 3; dof++)
            {
                int muscleIndex = HumanTrait.MuscleFromBone(boneIndex, dof);
                if (muscleIndex < 0 || muscleIndex >= muscles.Length)
                {
                    continue;
                }
                muscles[muscleIndex] = Normalize(euler[dof]) / 180f;
            }
        }

        /// <summary>Unity の <c>Quaternion.eulerAngles</c> は 0〜360° を返すため、-180〜180° に正規化する。</summary>
        private static float Normalize(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
