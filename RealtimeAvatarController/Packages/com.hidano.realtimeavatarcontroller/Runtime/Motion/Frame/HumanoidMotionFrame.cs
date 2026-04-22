using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// Humanoid 骨格向けモーションフレーム。
    /// Unity <c>HumanPose</c> 相当の Muscle 配列・Root 位置/回転に加え、
    /// (M-3) 親ローカル Bone 回転辞書を保持するイミュータブルクラス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全プロパティは読み取り専用であり、コンストラクタで完全初期化される。
    /// 外部からの書き換えは不可能 (イミュータブル設計)。
    /// </para>
    /// <para>
    /// <b>Muscles 配列の所有権 (OI-1 対応)</b>:
    /// コンストラクタに渡す <paramref name="Muscles"/> 配列は内部でディープコピーされず、
    /// 参照をそのまま保持する (リアルタイム制約を優先する初期版方針)。
    /// 呼び出し元 (MoCap ソース) は配列の所有権をフレームに移譲すること。
    /// 外部から配列要素を書き換えた場合の挙動は未定義である。
    /// </para>
    /// <para>
    /// <b>(M-3) BoneLocalRotations の運用</b>:
    /// VMC のように各ボーンの親ローカル回転クォータニオンを native 形式として emit する
    /// MoCap ソースは、<see cref="BoneLocalRotations"/> に辞書を格納する。
    /// <see cref="HumanoidMotionApplier"/> は MainThread で
    /// Transform.localRotation への書込 → <c>HumanPoseHandler.GetHumanPose</c> による Muscle 逆変換
    /// → <c>SetHumanPose</c> の経路で適用する。この場合 <see cref="Muscles"/> は無視される。
    /// 詳細は contracts.md §2.2 および motion-pipeline design.md §7.1.1 を参照。
    /// </para>
    /// </remarks>
    public sealed class HumanoidMotionFrame : MotionFrame
    {
        /// <inheritdoc/>
        public override SkeletonType SkeletonType => SkeletonType.Humanoid;

        /// <summary>
        /// Humanoid 骨格の Muscle 値配列。
        /// 要素数は <c>HumanTrait.MuscleCount</c> (= 95) に準拠する。
        /// 要素数が 0 かつ <see cref="BoneLocalRotations"/> も空の場合は「データなし / 初期化前」を示す無効フレームとして扱う。
        /// <see cref="BoneLocalRotations"/> 経路で適用する場合、呼び出し元は空配列を渡す (内容は Applier 側で無視される)。
        /// </summary>
        public float[] Muscles { get; }

        /// <summary>Root の位置 (Human Pose のボディ Position に相当)。</summary>
        public Vector3 RootPosition { get; }

        /// <summary>Root の回転 (Human Pose のボディ Rotation に相当)。</summary>
        public Quaternion RootRotation { get; }

        /// <summary>
        /// (M-3) 各ボーンの親ローカル座標系での回転辞書 (optional)。
        /// null または <c>Count == 0</c> の場合は従来の <see cref="Muscles"/> 経路でのみ適用される。
        /// 非 null かつ <c>Count &gt; 0</c> の場合、<see cref="HumanoidMotionApplier"/> は MainThread で
        /// Transform.localRotation 書込 → <c>GetHumanPose</c> による Muscle 逆変換 → <c>SetHumanPose</c>
        /// の経路で適用する (この経路では <see cref="Muscles"/> は無視される)。
        /// </summary>
        public IReadOnlyDictionary<HumanBodyBones, Quaternion> BoneLocalRotations { get; }

        /// <summary>
        /// このフレームが有効データを持つかどうか。
        /// <see cref="Muscles"/> または <see cref="BoneLocalRotations"/> のいずれか一方でも有効な内容があれば <c>true</c>。
        /// </summary>
        public bool IsValid
            => Muscles.Length > 0
               || (BoneLocalRotations != null && BoneLocalRotations.Count > 0);

        /// <summary>
        /// 既存コンストラクタ (互換維持)。<see cref="BoneLocalRotations"/> は <c>null</c>。
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
            : this(timestamp, muscles, rootPosition, rootRotation, null)
        {
        }

        /// <summary>
        /// (M-3) <see cref="BoneLocalRotations"/> 対応コンストラクタ。
        /// VMC などボーン回転クォータニオンを native 形式として emit する MoCap ソースが使用する。
        /// </summary>
        /// <param name="timestamp">受信スレッドで打刻した Stopwatch ベース秒数。</param>
        /// <param name="muscles">
        /// Muscle 値配列。呼び出し元から所有権を移譲すること (内部コピー不要)。
        /// <paramref name="boneLocalRotations"/> 経路で適用する場合、空配列を渡すことが許容される。
        /// </param>
        /// <param name="rootPosition">Root 位置。</param>
        /// <param name="rootRotation">Root 回転。</param>
        /// <param name="boneLocalRotations">
        /// 各ボーンの親ローカル回転辞書。null 可 (null の場合は従来の Muscles 経路で適用される)。
        /// 呼び出し元から所有権を移譲すること (Applier 側でコピーせず参照保持する)。
        /// </param>
        public HumanoidMotionFrame(
            double timestamp,
            float[] muscles,
            Vector3 rootPosition,
            Quaternion rootRotation,
            IReadOnlyDictionary<HumanBodyBones, Quaternion> boneLocalRotations)
            : base(timestamp)
        {
            Muscles = muscles ?? Array.Empty<float>();
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            BoneLocalRotations = boneLocalRotations;
        }

        /// <summary>
        /// 無効フレーム (<c>Muscles.Length == 0</c> かつ <see cref="BoneLocalRotations"/> が null) を生成するファクトリメソッド。
        /// MotionCache の初期化前状態や、MoCap 受信の一時欠落を表現するのに使用する。
        /// </summary>
        /// <param name="timestamp">Stopwatch ベースのタイムスタンプ秒数。</param>
        /// <returns><see cref="IsValid"/> が <c>false</c> となる無効フレーム。</returns>
        public static HumanoidMotionFrame CreateInvalid(double timestamp)
            => new HumanoidMotionFrame(timestamp, Array.Empty<float>(), Vector3.zero, Quaternion.identity);
    }
}
