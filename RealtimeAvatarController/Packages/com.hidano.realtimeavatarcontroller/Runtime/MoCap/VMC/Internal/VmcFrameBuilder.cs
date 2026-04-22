using System;
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
    /// <b>スレッド設計</b>: design.md §13.1 の通りワーカースレッド (uOSC 受信コールバック) 専用であり、
    /// メインスレッドから直接アクセスしない。したがって本クラスはロックを持たない。
    /// Unity メインスレッド専用 API (<c>HumanPoseHandler</c> 等) は使用しない。
    /// </para>
    /// <para>
    /// <b>(M-3 更新) フラッシュ方針</b>: <see cref="SetBone"/> を 1 度以上受信した状態で
    /// <see cref="TryFlush"/> が呼ばれると、蓄積された Bone 辞書を
    /// <see cref="HumanoidMotionFrame.BoneLocalRotations"/> にそのまま格納して発行する。
    /// Muscle 変換は <see cref="HumanoidMotionApplier"/> が MainThread で実施するため、
    /// 本ビルダは <see cref="HumanoidMotionFrame.Muscles"/> に空配列を渡す。
    /// Bone 未受信の場合は無効フレームを生成せず <c>false</c> を返す。
    /// </para>
    /// <para>
    /// <b>Root の永続化</b>: Root は Bone より低頻度で送られる可能性があるため、
    /// <see cref="TryFlush"/> 後も直近の値を保持する (次フレームでも同じ Root を参照可能)。
    /// 初期状態は <see cref="Vector3.zero"/> / <see cref="Quaternion.identity"/>。
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
        /// 蓄積済みのボーン状態から <see cref="HumanoidMotionFrame"/> を 1 本構築する (M-3 対応版)。
        /// </summary>
        /// <param name="frame">構築に成功した場合はフレーム、失敗時は <c>null</c>。</param>
        /// <returns>
        /// 蓄積ボーンが 1 件以上あれば <c>true</c>、ボーン未受信なら <c>false</c>
        /// (design.md §7.3 / tasks.md タスク 5-1: 無効フレーム判定)。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 成功時は蓄積した Bone 辞書を新しい <c>IReadOnlyDictionary&lt;HumanBodyBones, Quaternion&gt;</c> に
        /// コピーし、<see cref="HumanoidMotionFrame.BoneLocalRotations"/> として渡す。
        /// 内部辞書をリセットし次フレーム受信に備える。Root 状態は保持する。
        /// </para>
        /// <para>
        /// <b>Muscle 変換について (M-3 更新)</b>: Unity の <c>HumanPoseHandler</c> は MainThread 専用のため、
        /// 本ビルダ (ワーカースレッド) では Bone → Muscle 変換を行わない。
        /// Muscle 逆変換は <see cref="HumanoidMotionApplier"/> が MainThread で実施する。
        /// したがって <see cref="HumanoidMotionFrame.Muscles"/> には空配列を渡す。
        /// </para>
        /// </remarks>
        public bool TryFlush(out HumanoidMotionFrame frame)
        {
            if (_bones.Count == 0)
            {
                frame = null;
                return false;
            }

            // Bone 辞書を (parent-local rotation) → IReadOnlyDictionary<HumanBodyBones, Quaternion> にコピー
            // Frame 生成後も辞書参照が Applier 側に渡ることを考慮し、内部辞書とは別インスタンスを用意する
            var boneRotations = new Dictionary<HumanBodyBones, Quaternion>(_bones.Count);
            foreach (var kv in _bones)
            {
                boneRotations[kv.Key] = kv.Value.rotation;
            }

            // design.md §6.4: Stopwatch ベースの秒数でタイムスタンプを打刻する。
            double timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            frame = new HumanoidMotionFrame(
                timestamp,
                Array.Empty<float>(),  // Muscles は使わない (Applier 側で BoneLocalRotations から逆変換)
                _rootPosition,
                _rootRotation,
                boneRotations);

            _bones.Clear();
            return true;
        }
    }
}
