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
    /// <b>スレッド設計 (M-3 改訂 2026-04-22)</b>: 本クラスは <see cref="VmcOscAdapter"/> 経由で
    /// uOSC の <c>onDataReceived</c> から呼ばれ、uOSC の実装では <c>onDataReceived</c> は
    /// <c>uOscServer.Update</c> 内で Invoke されるため **MainThread** で動く。
    /// また <see cref="TryFlush"/> も <see cref="VmcOscAdapter.Tick"/> 経由で MainThread から呼ばれる。
    /// したがって本クラスはロックを持たない。
    /// Unity メインスレッド専用 API (<c>HumanPoseHandler</c> 等) は使用しない。
    /// </para>
    /// <para>
    /// <b>受信即キャッシュ・Tick で flush の分離モデル (M-3 改訂)</b>:
    /// VMC Protocol 仕様上、OSC message stream から frame 境界を検知する手段は定義されていない
    /// (1 frame = 1 bundle = 1 UDP packet の保証はなく、MTU 1500 byte 制約で分割される可能性あり)。
    /// EVMC4U (<c>gpsnmeajp/EasyVirtualMotionCaptureForUnity</c>) が実績のある分離モデルに倣い、
    /// 受信 (<see cref="SetRoot"/> / <see cref="SetBone"/>) は辞書に蓄積するだけに留め、
    /// <see cref="TryFlush"/> は外部 Tick (VmcOscAdapter.Tick → VmcTickDriver.LateUpdate) から呼ばれる。
    /// </para>
    /// <para>
    /// <b>欠損 bone の扱い</b>: <see cref="TryFlush"/> 成功後も <c>_bones</c> 辞書は **clear しない**。
    /// ある Tick 周期内に欠けた bone は前回受信値を維持する。これにより VMC の任意送信周期
    /// (全 bone が毎周期送られない場合) に対して安定した pose を保持する。
    /// </para>
    /// <para>
    /// <b>Dirty フラグ</b>: 前回 <see cref="TryFlush"/> 以降に 1 度でも <see cref="SetRoot"/> /
    /// <see cref="SetBone"/> が呼ばれた場合のみ <see cref="TryFlush"/> が <c>true</c> を返す。
    /// 更新がないのに無駄に <c>OnNext</c> を発火しないための最適化。
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
        /// 前回 <see cref="TryFlush"/> 以降に 1 度でも受信があったかを示すフラグ。
        /// <see cref="SetRoot"/> / <see cref="SetBone"/> で true に、<see cref="TryFlush"/> 成功で false に戻る。
        /// </summary>
        private bool _dirty;

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
            _dirty = true;
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
            _dirty = true;
        }

        /// <summary>
        /// 蓄積済みのボーン / Root 状態から <see cref="HumanoidMotionFrame"/> を 1 本構築する (M-3 改訂版 2026-04-22)。
        /// 通常は <see cref="VmcOscAdapter.Tick"/> (<c>VmcTickDriver.LateUpdate</c> 経由) から呼ばれる。
        /// </summary>
        /// <param name="frame">構築に成功した場合はフレーム、失敗時は <c>null</c>。</param>
        /// <returns>
        /// 前回 <see cref="TryFlush"/> 以降に 1 度以上受信があり、かつ蓄積ボーンが 1 件以上あれば <c>true</c>。
        /// それ以外は <c>false</c>。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>(M-3 改訂)</b> 内部辞書 <c>_bones</c> は <b>Clear しない</b>。
        /// 欠損 bone (今回周期で送られなかった bone) は前回受信値が保持される。
        /// Frame 生成時は新規辞書にスナップショットをコピーして渡すため、
        /// Frame 側から内部辞書への書換えは発生しない (イミュータブル契約を維持)。
        /// </para>
        /// <para>
        /// <b>Dirty フラグ</b>: <see cref="SetRoot"/> / <see cref="SetBone"/> で true に、本メソッド成功で false に戻る。
        /// 連続する Tick で更新がなければ <c>false</c> を返して <c>OnNext</c> 発火を抑制する。
        /// </para>
        /// <para>
        /// <b>Muscle 変換について</b>: <see cref="HumanoidMotionFrame.Muscles"/> には空配列を渡し、
        /// Applier (MainThread) が Transform 直接書込で適用する (motion-pipeline §7.1.1)。
        /// </para>
        /// </remarks>
        public bool TryFlush(out HumanoidMotionFrame frame)
        {
            if (!_dirty || _bones.Count == 0)
            {
                frame = null;
                return false;
            }

            // Bone 辞書のスナップショットコピー (Frame イミュータビリティ保持のため別インスタンスを用意)
            // rotation と position を両方渡す (EVMC4U 準拠 / M-3 追補 2026-04-22)
            var boneRotations = new Dictionary<HumanBodyBones, Quaternion>(_bones.Count);
            var bonePositions = new Dictionary<HumanBodyBones, UnityEngine.Vector3>(_bones.Count);
            foreach (var kv in _bones)
            {
                boneRotations[kv.Key] = kv.Value.rotation;
                bonePositions[kv.Key] = kv.Value.position;
            }

            // design.md §6.4: Stopwatch ベースの秒数でタイムスタンプを打刻する。
            double timestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            frame = new HumanoidMotionFrame(
                timestamp,
                Array.Empty<float>(),
                _rootPosition,
                _rootRotation,
                boneRotations,
                bonePositions);

            // NOTE (M-3 改訂): _bones.Clear() は呼ばない。欠損 bone は前回値を保持する。
            _dirty = false;
            return true;
        }
    }
}
