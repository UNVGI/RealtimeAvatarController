using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// Humanoid アバター向けモーション適用具象クラス。
    /// <see cref="HumanPoseHandler"/> を使用して <see cref="HumanoidMotionFrame"/> をアバターへ適用する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>スレッド要件</b>: 全メソッドは Unity メインスレッド (推奨: <c>LateUpdate</c>) からのみ呼び出すこと。
    /// </para>
    /// <para>
    /// <b>例外伝搬責務 (design.md §8.1 / §9.1)</b>: <see cref="Apply"/> 内の例外は catch せず、
    /// 呼び出し元 (SlotManager) に伝搬する。<see cref="FallbackBehavior"/> の実行は呼び出し元が
    /// <see cref="ExecuteFallback"/> を呼ぶことで行う。<c>ISlotErrorChannel</c> への発行も SlotManager 責務。
    /// </para>
    /// <para>
    /// <b>Hide 復帰</b>: <see cref="FallbackBehavior.Hide"/> 状態 (<c>_isFallbackHiding == true</c>) で
    /// <see cref="Apply"/> が正常完了した場合、内部で <see cref="Renderer.enabled"/> を <c>true</c> に
    /// 再設定して描画を復帰する。
    /// </para>
    /// <para>
    /// <b>(M-3 / 2026-04-22 修正版) 適用経路の分岐</b>: <see cref="HumanoidMotionFrame.BoneLocalRotations"/> の有無で分岐する。
    /// <list type="bullet">
    ///   <item><description>BoneLocalRotations が非 null かつ Count &gt; 0: Avatar root の position/rotation と各 bone の localRotation に直接書込 (HumanPoseHandler バイパス)</description></item>
    ///   <item><description>それ以外: 従来の Muscles 直接経路 (SetHumanPose に muscles + Root を渡す)</description></item>
    /// </list>
    /// 詳細は motion-pipeline design.md §7.1.1 参照。
    /// </para>
    /// </remarks>
    public sealed class HumanoidMotionApplier : IMotionApplier
    {
        private readonly string _slotId;
        private HumanPoseHandler _poseHandler;
        private Animator _animator;          // (M-3) BoneLocalRotations 経路で GetBoneTransform に使用
        private HumanPose _lastGoodPose;
        private HumanPose _workPose;         // (M-3) GetHumanPose / SetHumanPose 用の再利用バッファ
        private Renderer[] _renderers;
        private bool _isFallbackHiding;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="slotId">このアプライヤーが属する Slot の識別子。例外メッセージ生成に使用。</param>
        public HumanoidMotionApplier(string slotId)
        {
            _slotId = slotId;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// <c>null</c> を渡した場合は内部の <see cref="HumanPoseHandler"/> を破棄し、
        /// 以降の <see cref="Apply"/> 呼び出しをスキップする。
        /// </para>
        /// <para>
        /// 非 Humanoid アバター (または <see cref="Animator"/> コンポーネントを持たない GameObject) を
        /// 渡した場合は <see cref="InvalidOperationException"/> をスローする。
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="avatarRoot"/> が <see cref="Animator"/> を持たない、または
        /// <c>Animator.isHuman == false</c> の場合。
        /// </exception>
        public void SetAvatar(GameObject avatarRoot)
        {
            _poseHandler?.Dispose();
            _poseHandler = null;
            _animator = null;
            _renderers = null;
            _isFallbackHiding = false;

            if (avatarRoot == null)
            {
                return;
            }

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException(
                    $"[HumanoidMotionApplier][{_slotId}] GameObject '{avatarRoot.name}' に Animator コンポーネントがありません。");
            }
            if (!animator.isHuman)
            {
                throw new InvalidOperationException(
                    $"[HumanoidMotionApplier][{_slotId}] GameObject '{avatarRoot.name}' は Humanoid アバターではありません。");
            }

            _poseHandler = new HumanPoseHandler(animator.avatar, avatarRoot.transform);
            _animator = animator;
            _renderers = avatarRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            _lastGoodPose = new HumanPose();
            _poseHandler.GetHumanPose(ref _lastGoodPose);
            _workPose = new HumanPose();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// <b>スキップ条件 (例外なし)</b>:
        /// <paramref name="frame"/> が <c>null</c> / <see cref="HumanoidMotionFrame"/> 以外の型 /
        /// <c>IsValid == false</c> / <see cref="HumanPoseHandler"/> 未初期化 /
        /// <paramref name="weight"/> が <c>0.0</c> のいずれかの場合、適用をスキップする。
        /// </para>
        /// <para>
        /// 適用処理中に例外が発生した場合は catch せず呼び出し元に伝搬する。
        /// </para>
        /// </remarks>
        public void Apply(MotionFrame frame, float weight, SlotSettings settings)
        {
            if (_poseHandler == null)
            {
                return;
            }
            if (frame == null)
            {
                return;
            }
            if (!(frame is HumanoidMotionFrame humanoidFrame))
            {
                return;
            }
            if (!humanoidFrame.IsValid)
            {
                return;
            }
            if (weight == 0f)
            {
                return;
            }

            ApplyInternal(humanoidFrame);

            // _lastGoodPose は「直前に Apply した最終 pose (HumanPoseHandler 経由)」を保持する。
            // BoneLocalRotations 経路では ApplyInternal 終了時の Transform 状態から取得する方が正確。
            _poseHandler.GetHumanPose(ref _lastGoodPose);

            if (_isFallbackHiding)
            {
                RestoreRenderers();
            }
        }

        /// <summary>
        /// SlotManager の catch ブロックから呼び出される Fallback 処理実行メソッド。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="FallbackBehavior.HoldLastPose"/>: 何もしない (直前の正常ポーズを維持)。
        /// </para>
        /// <para>
        /// <see cref="FallbackBehavior.TPose"/>: 全 Muscle 値 0 / <c>bodyPosition = Vector3.up</c> /
        /// <c>bodyRotation = Quaternion.identity</c> のポーズを適用する。
        /// </para>
        /// <para>
        /// <see cref="FallbackBehavior.Hide"/>: アバターに紐付く全 <see cref="Renderer.enabled"/> を
        /// <c>false</c> にし、<c>_isFallbackHiding</c> フラグを立てる。次の正常 <see cref="Apply"/> で復帰する。
        /// </para>
        /// <para>
        /// <b>スレッド要件</b>: Unity メインスレッドからのみ呼び出すこと。
        /// </para>
        /// </remarks>
        /// <param name="behavior">実行する Fallback 挙動。</param>
        public void ExecuteFallback(FallbackBehavior behavior)
        {
            switch (behavior)
            {
                case FallbackBehavior.HoldLastPose:
                    break;

                case FallbackBehavior.TPose:
                    if (_poseHandler != null)
                    {
                        var tPose = new HumanPose
                        {
                            bodyPosition = Vector3.up,
                            bodyRotation = Quaternion.identity,
                            muscles = new float[HumanTrait.MuscleCount],
                        };
                        _poseHandler.SetHumanPose(ref tPose);
                    }
                    break;

                case FallbackBehavior.Hide:
                    if (_renderers != null)
                    {
                        foreach (var r in _renderers)
                        {
                            if (r != null) r.enabled = false;
                        }
                    }
                    _isFallbackHiding = true;
                    break;
            }
        }

        /// <summary>
        /// 内部リソースを解放する。<see cref="HumanPoseHandler"/> を破棄する。
        /// </summary>
        public void Dispose()
        {
            _poseHandler?.Dispose();
            _poseHandler = null;
            _animator = null;
            _renderers = null;
        }

        /// <summary>
        /// フレームを Humanoid アバターへ適用する。
        /// <see cref="HumanoidMotionFrame.BoneLocalRotations"/> の有無で経路が分岐する
        /// (M-3 / 2026-04-22 修正版: 経路 A は Transform 直接書込)。
        /// </summary>
        private void ApplyInternal(HumanoidMotionFrame humanoidFrame)
        {
            var boneRotations = humanoidFrame.BoneLocalRotations;
            bool useBoneRotationPath = boneRotations != null && boneRotations.Count > 0;

            if (useBoneRotationPath && _animator != null)
            {
                // === 経路 A: BoneLocalRotations 経由 (Transform 直接書込) ===
                // HumanPoseHandler の Muscle 逆算経路では近似誤差でボーン姿勢がずれるため、
                // VMC など native な parent-local rotation を送るソースでは Transform を直接更新する
                // (motion-pipeline design.md §7.1.1 / contracts.md §2.2 M-3 方針修正)。

                // 各ボーンの parent-local rotation を Animator.GetBoneTransform().localRotation に直接書込む
                foreach (var kv in boneRotations)
                {
                    var boneTf = _animator.GetBoneTransform(kv.Key);
                    if (boneTf != null)
                    {
                        boneTf.localRotation = kv.Value;
                    }
                }

                // NOTE (M-3 2026-04-22): Avatar root (Animator.transform) への position / rotation 書込は
                // 初期版では行わない。VMC Protocol の Root/Pos は「アバター親ノード」姿勢を意図するが、
                // 多くの送信アプリ (VMagicMirror / VSeeFace 等) は Hips の global 姿勢を Bone rotation に
                // 含めて送るため、Avatar root に RootRotation を書くと Hips と二重回転になり姿勢が破綻する。
                // 一般的な VMC 受信実装でもデフォルトで Root Transform への書込は無効化されている (Inspector option)。
                // 必要に応じて将来オプションとして追加する。
            }
            else
            {
                // === 経路 B: Muscles 直接経路 (従来) ===
                var pose = new HumanPose
                {
                    bodyPosition = humanoidFrame.RootPosition,
                    bodyRotation = humanoidFrame.RootRotation,
                    muscles = humanoidFrame.Muscles,
                };
                _poseHandler.SetHumanPose(ref pose);
            }
        }

        private void RestoreRenderers()
        {
            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null) r.enabled = true;
                }
            }
            _isFallbackHiding = false;
        }
    }
}
