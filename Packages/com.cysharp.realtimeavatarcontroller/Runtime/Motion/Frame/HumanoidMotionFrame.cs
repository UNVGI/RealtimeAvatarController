using System;
using UnityEngine;

namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// Humanoid 骨格向けモーションフレーム。
    /// Unity <c>HumanPose</c> 相当の Muscle 配列と Root 位置・回転を保持するイミュータブルクラス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全プロパティは読み取り専用であり、コンストラクタで完全初期化される。
    /// 外部からの書き換えは不可能 (イミュータブル設計)。
    /// </para>
    /// <para>
    /// <b>Muscles 配列の所有権 (OI-1 対応)</b>:
    /// コンストラクタに渡す <paramref name="muscles"/> 配列は内部でディープコピーされず、
    /// 参照をそのまま保持する (リアルタイム制約を優先する初期版方針)。
    /// 呼び出し元 (MoCap ソース) は配列の所有権をフレームに移譲すること。
    /// 外部から配列要素を書き換えた場合の挙動は未定義である。
    /// </para>
    /// </remarks>
    public sealed class HumanoidMotionFrame : MotionFrame
    {
        /// <inheritdoc/>
        public override SkeletonType SkeletonType => SkeletonType.Humanoid;

        /// <summary>
        /// Humanoid 骨格の Muscle 値配列。
        /// 要素数は <c>HumanTrait.MuscleCount</c> (= 95) に準拠する。
        /// 要素数が 0 の場合は「データなし / 初期化前」を示す無効フレームとして扱う。
        /// </summary>
        public float[] Muscles { get; }

        /// <summary>Root の位置 (Human Pose のボディ Position に相当)。</summary>
        public Vector3 RootPosition { get; }

        /// <summary>Root の回転 (Human Pose のボディ Rotation に相当)。</summary>
        public Quaternion RootRotation { get; }

        /// <summary>このフレームが有効データを持つかどうか (<c>Muscles.Length &gt; 0</c>)。</summary>
        public bool IsValid => Muscles.Length > 0;

        /// <summary>
        /// 有効フレームを生成するコンストラクタ。
        /// <paramref name="muscles"/> の要素数は <c>HumanTrait.MuscleCount</c> (95) と一致していること。
        /// <paramref name="muscles"/> に <c>null</c> を渡した場合は <see cref="Array.Empty{T}"/> を代入する
        /// (結果として <see cref="IsValid"/> は <c>false</c> となる)。
        /// </summary>
        /// <param name="timestamp">受信スレッドで打刻した Stopwatch ベース秒数。</param>
        /// <param name="muscles">
        /// Muscle 値配列。呼び出し元から所有権を移譲すること (内部コピー不要)。
        /// 詳細は本クラスの remarks を参照。
        /// </param>
        /// <param name="rootPosition">Root 位置。</param>
        /// <param name="rootRotation">Root 回転。</param>
        public HumanoidMotionFrame(
            double timestamp,
            float[] muscles,
            Vector3 rootPosition,
            Quaternion rootRotation)
            : base(timestamp)
        {
            Muscles = muscles ?? Array.Empty<float>();
            RootPosition = rootPosition;
            RootRotation = rootRotation;
        }

        /// <summary>
        /// 無効フレーム (<c>Muscles.Length == 0</c>) を生成するファクトリメソッド。
        /// MotionCache の初期化前状態や、MoCap 受信の一時欠落を表現するのに使用する。
        /// </summary>
        /// <param name="timestamp">Stopwatch ベースのタイムスタンプ秒数。</param>
        /// <returns><see cref="IsValid"/> が <c>false</c> となる無効フレーム。</returns>
        public static HumanoidMotionFrame CreateInvalid(double timestamp)
            => new HumanoidMotionFrame(timestamp, Array.Empty<float>(), Vector3.zero, Quaternion.identity);
    }
}
