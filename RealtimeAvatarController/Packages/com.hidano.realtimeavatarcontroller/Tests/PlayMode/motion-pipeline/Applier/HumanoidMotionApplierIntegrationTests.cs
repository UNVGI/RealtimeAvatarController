using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using RealtimeAvatarController.Motion.Tests.PlayMode.Fixtures;

namespace RealtimeAvatarController.Motion.Tests.PlayMode.Applier
{
    /// <summary>
    /// HumanoidMotionApplier の PlayMode 統合テスト (tasks.md §7-2)。
    ///
    /// <para>
    /// 検証対象:
    ///   - 実 Humanoid アバター (<see cref="TestHumanoidAvatarBuilder"/>) に対する
    ///     <see cref="HumanoidMotionApplier.Apply"/> でボーン回転が変化すること
    ///   - 非 Humanoid GameObject に <see cref="HumanoidMotionApplier.SetAvatar"/> を呼ぶと
    ///     <see cref="InvalidOperationException"/> を投げること
    ///   - <see cref="HumanoidMotionApplier.SetAvatar"/> に <c>null</c> を渡すと
    ///     以降の <see cref="HumanoidMotionApplier.Apply"/> が例外なくスキップされること
    ///   - アバター切替時に旧 <see cref="HumanPoseHandler"/> が破棄され、新アバターへ
    ///     適用が切り替わること
    /// </para>
    ///
    /// <para>
    /// EditMode (tasks.md §6-5) では <see cref="HumanPoseHandler"/> を構築できないため、
    /// Humanoid 構成済み <see cref="Avatar"/> を必要とする経路はここで担保する
    /// (design.md §13.2)。
    /// </para>
    ///
    /// Requirements: Req 6, Req 12, Req 14
    /// </summary>
    [TestFixture]
    public class HumanoidMotionApplierIntegrationTests
    {
        private const string TestSlotId = "motion-pipeline-integration-slot";

        private SlotSettings _settings;
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = TestSlotId;
            _settings.displayName = "Motion Pipeline Integration Slot";
            _settings.weight = 1.0f;
            _settings.fallbackBehavior = FallbackBehavior.HoldLastPose;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdAvatars)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _createdAvatars.Clear();

            if (_settings != null)
            {
                UnityEngine.Object.DestroyImmediate(_settings);
                _settings = null;
            }
        }

        // --- Apply_WithValidHumanoidFrame_ChangesAvatarPose ---

        [Test]
        public void Apply_WithValidHumanoidFrame_ChangesAvatarPose()
        {
            // 実 HumanPoseHandler 経由で HumanoidMotionFrame をアバターへ適用し、
            // ボーン (LeftUpperArm / Spine / Hips) の localRotation が初期状態 (identity) から
            // 有意に変化することを検証する。design.md §7 の適用ルート正常系を担保する。
            var avatar = BuildAvatar("integration-avatar-apply");
            var leftUpperArm = FindChild(avatar.transform, "LeftUpperArm");
            var spine = FindChild(avatar.transform, "Spine");
            var hips = FindChild(avatar.transform, "Hips");
            Assert.That(leftUpperArm, Is.Not.Null, "前提: LeftUpperArm ボーンが存在する");
            Assert.That(spine, Is.Not.Null, "前提: Spine ボーンが存在する");
            Assert.That(hips, Is.Not.Null, "前提: Hips ボーンが存在する");

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            var initialLeftArm = leftUpperArm.localRotation;
            var initialSpine = spine.localRotation;
            var initialHips = hips.localRotation;

            var frame = CreateNonTrivialFrame();

            Assert.DoesNotThrow(
                () => applier.Apply(frame, 1.0f, _settings),
                "Humanoid フレームの Apply は例外なく完了する");

            bool leftArmChanged = Quaternion.Angle(initialLeftArm, leftUpperArm.localRotation) > 0.1f;
            bool spineChanged = Quaternion.Angle(initialSpine, spine.localRotation) > 0.1f;
            bool hipsChanged = Quaternion.Angle(initialHips, hips.localRotation) > 0.1f;

            Assert.That(leftArmChanged || spineChanged || hipsChanged, Is.True,
                "Apply 後、少なくともいずれかのボーン (LeftUpperArm / Spine / Hips) の localRotation が変化している");
        }

        // --- SetAvatar_WithNonHumanoidObject_ThrowsInvalidOperationException ---

        [Test]
        public void SetAvatar_WithNonHumanoidObject_ThrowsInvalidOperationException()
        {
            // Animator を持たない GameObject を渡した場合、SetAvatar は
            // InvalidOperationException をスローする (HumanoidMotionApplier.SetAvatar 契約)。
            var nonHumanoid = new GameObject("non-humanoid-object");
            _createdAvatars.Add(nonHumanoid);

            using var applier = new HumanoidMotionApplier(TestSlotId);

            var ex = Assert.Throws<InvalidOperationException>(
                () => applier.SetAvatar(nonHumanoid),
                "Animator の無い GameObject では InvalidOperationException を投げる");
            Assert.That(ex.Message, Does.Contain(TestSlotId),
                "例外メッセージに slotId が含まれる");
        }

        // --- SetAvatar_WithNull_SkipsNextApply ---

        [Test]
        public void SetAvatar_WithNull_SkipsNextApply()
        {
            // SetAvatar(null) はアバターを切り離し、以降の Apply は例外なくスキップされる
            // (IMotionApplier.SetAvatar / HumanoidMotionApplier.Apply のスキップ条件)。
            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(null);

            var frame = CreateNonTrivialFrame();

            Assert.DoesNotThrow(
                () => applier.Apply(frame, 1.0f, _settings),
                "_poseHandler == null の状態で Apply は例外なくスキップする");
        }

        // --- SetAvatar_Switch_ReInitializesPoseHandler ---

        [Test]
        public void SetAvatar_Switch_ReInitializesPoseHandler()
        {
            // アバター切替時の挙動 (design.md §7 / Req 12):
            //   1. 旧 HumanPoseHandler が Dispose され、_poseHandler が新アバターのものに入れ替わる
            //   2. 切替後の Apply は新アバターのボーンにのみ反映される (旧アバターは変化しない)
            var avatarA = BuildAvatar("integration-avatar-A");
            var avatarB = BuildAvatar("integration-avatar-B");
            var armA = FindChild(avatarA.transform, "LeftUpperArm");
            var armB = FindChild(avatarB.transform, "LeftUpperArm");
            var spineA = FindChild(avatarA.transform, "Spine");
            var spineB = FindChild(avatarB.transform, "Spine");
            var hipsA = FindChild(avatarA.transform, "Hips");
            var hipsB = FindChild(avatarB.transform, "Hips");

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatarA);
            var handlerA = GetPoseHandler(applier);
            Assert.That(handlerA, Is.Not.Null, "SetAvatar(avatarA) 後、_poseHandler が初期化されている");

            // アバター切替
            applier.SetAvatar(avatarB);
            var handlerB = GetPoseHandler(applier);
            Assert.That(handlerB, Is.Not.Null, "SetAvatar(avatarB) 後、_poseHandler が新規初期化されている");
            Assert.That(ReferenceEquals(handlerA, handlerB), Is.False,
                "アバター切替で旧 HumanPoseHandler が破棄され、新インスタンスに差し替わっている");

            // 切替前の avatarA のボーン初期状態をスナップショット
            var preA_arm = armA.localRotation;
            var preA_spine = spineA.localRotation;
            var preA_hips = hipsA.localRotation;
            var preB_arm = armB.localRotation;
            var preB_spine = spineB.localRotation;
            var preB_hips = hipsB.localRotation;

            applier.Apply(CreateNonTrivialFrame(), 1.0f, _settings);

            // 旧アバター (avatarA) はボーン回転が変化していない
            Assert.That(Quaternion.Angle(preA_arm, armA.localRotation), Is.LessThan(0.1f),
                "切替後の Apply は avatarA の LeftUpperArm に影響しない");
            Assert.That(Quaternion.Angle(preA_spine, spineA.localRotation), Is.LessThan(0.1f),
                "切替後の Apply は avatarA の Spine に影響しない");
            Assert.That(Quaternion.Angle(preA_hips, hipsA.localRotation), Is.LessThan(0.1f),
                "切替後の Apply は avatarA の Hips に影響しない");

            // 新アバター (avatarB) はいずれかのボーンが変化している
            bool bArmChanged = Quaternion.Angle(preB_arm, armB.localRotation) > 0.1f;
            bool bSpineChanged = Quaternion.Angle(preB_spine, spineB.localRotation) > 0.1f;
            bool bHipsChanged = Quaternion.Angle(preB_hips, hipsB.localRotation) > 0.1f;
            Assert.That(bArmChanged || bSpineChanged || bHipsChanged, Is.True,
                "切替後の Apply は avatarB に反映される (いずれかのボーン localRotation が変化)");
        }

        // === M-3: BoneLocalRotations 経路の PlayMode 統合テスト ===

        /// <summary>
        /// <see cref="HumanoidMotionFrame.BoneLocalRotations"/> に含めた bone が
        /// Animator の Transform.localRotation に反映されること (SetHumanPose 経由の
        /// Muscle 逆変換パスが機能していること) を検証する。
        /// M-3 合意変更 (contracts.md §2.2 / motion-pipeline design.md §7.1.1)。
        /// </summary>
        [Test]
        public void Apply_WithBoneLocalRotations_AppliesToAvatarBones()
        {
            var avatar = BuildAvatar("integration-avatar-bone-rotations");
            var leftUpperArm = FindChild(avatar.transform, "LeftUpperArm");
            var spine = FindChild(avatar.transform, "Spine");
            Assert.That(leftUpperArm, Is.Not.Null, "前提: LeftUpperArm ボーンが存在する");
            Assert.That(spine, Is.Not.Null, "前提: Spine ボーンが存在する");

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            var initialLeftArm = leftUpperArm.localRotation;
            var initialSpine = spine.localRotation;

            // VMC 経路相当のフレーム: Muscles は空、BoneLocalRotations のみ指定
            var boneRotations = new Dictionary<HumanBodyBones, Quaternion>
            {
                { HumanBodyBones.LeftUpperArm, Quaternion.Euler(45f, 30f, 15f) },
                { HumanBodyBones.Spine,        Quaternion.Euler(10f, 0f, 20f) },
            };
            var frame = new HumanoidMotionFrame(
                timestamp: 1.0,
                muscles: Array.Empty<float>(),
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: boneRotations);

            Assert.DoesNotThrow(
                () => applier.Apply(frame, 1.0f, _settings),
                "BoneLocalRotations 経路の Apply は例外なく完了する");

            // Humanoid rig の muscle constraint を経由するため元 rotation とは完全一致しないが、
            // 初期姿勢からは有意に変化しているはず。
            bool leftArmChanged = Quaternion.Angle(initialLeftArm, leftUpperArm.localRotation) > 0.1f;
            bool spineChanged = Quaternion.Angle(initialSpine, spine.localRotation) > 0.1f;
            Assert.That(leftArmChanged || spineChanged, Is.True,
                "BoneLocalRotations 経由の Apply 後、指定ボーンのいずれかが回転していること");
        }

        /// <summary>
        /// <see cref="HumanoidMotionFrame.BoneLocalRotations"/> が非 null かつ Count &gt; 0 の場合、
        /// <see cref="HumanoidMotionFrame.Muscles"/> が空配列でも Apply がスキップされないこと
        /// (IsValid 判定が BoneLocalRotations も考慮していること) を検証する。
        /// </summary>
        [Test]
        public void Apply_WithBoneLocalRotationsOnly_IsNotSkippedDespiteEmptyMuscles()
        {
            var avatar = BuildAvatar("integration-avatar-bones-only");
            var hips = FindChild(avatar.transform, "Hips");
            Assert.That(hips, Is.Not.Null, "前提: Hips ボーンが存在する");

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            var initialHipsRot = hips.localRotation;

            var frame = new HumanoidMotionFrame(
                timestamp: 2.0,
                muscles: Array.Empty<float>(),
                rootPosition: new Vector3(0f, 1.0f, 0f),
                rootRotation: Quaternion.Euler(0f, 45f, 0f),
                boneLocalRotations: new Dictionary<HumanBodyBones, Quaternion>
                {
                    { HumanBodyBones.Hips, Quaternion.Euler(20f, 0f, 0f) },
                });

            Assert.That(frame.IsValid, Is.True,
                "Muscles が空でも BoneLocalRotations があれば IsValid は true");

            Assert.DoesNotThrow(
                () => applier.Apply(frame, 1.0f, _settings),
                "Muscles 空の BoneLocalRotations フレームは Apply される (スキップされない)");

            // RootRotation が反映されている (SetHumanPose の bodyRotation)
            var worldRot = hips.rotation;
            Assert.That(Quaternion.Angle(initialHipsRot, hips.localRotation) > 0.01f
                        || Quaternion.Angle(Quaternion.identity, worldRot) > 0.01f,
                Is.True,
                "Muscles 空でも BoneLocalRotations 経由で Hips または Root に変化が反映される");
        }

        /// <summary>
        /// <see cref="HumanoidMotionFrame.BoneLocalRotations"/> が空辞書の場合、
        /// 従来の Muscles 経路で適用されることを検証する (後方互換性)。
        /// </summary>
        [Test]
        public void Apply_WithEmptyBoneLocalRotations_FallsBackToMusclePath()
        {
            var avatar = BuildAvatar("integration-avatar-fallback-muscle");
            var leftUpperArm = FindChild(avatar.transform, "LeftUpperArm");
            Assert.That(leftUpperArm, Is.Not.Null);

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);
            var initialRot = leftUpperArm.localRotation;

            // BoneLocalRotations は空辞書。Muscles 経路が使われるべき。
            var muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < muscles.Length; i++) muscles[i] = 0.4f;

            var frame = new HumanoidMotionFrame(
                timestamp: 3.0,
                muscles: muscles,
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: new Dictionary<HumanBodyBones, Quaternion>());  // empty

            Assert.DoesNotThrow(() => applier.Apply(frame, 1.0f, _settings));

            // Muscles 経路が効いていれば LeftUpperArm が変化する (従来の CreateNonTrivialFrame と同値な動作)
            Assert.That(Quaternion.Angle(initialRot, leftUpperArm.localRotation), Is.GreaterThan(0.1f),
                "BoneLocalRotations が空の場合は従来の Muscles 経路で Apply される");
        }

        // --- helpers ---

        private GameObject BuildAvatar(string name)
        {
            var go = TestHumanoidAvatarBuilder.Build(name);
            _createdAvatars.Add(go);
            return go;
        }

        private static HumanoidMotionFrame CreateNonTrivialFrame()
        {
            var muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < muscles.Length; i++)
            {
                muscles[i] = 0.4f;
            }
            return new HumanoidMotionFrame(
                timestamp: 1.0,
                muscles: muscles,
                rootPosition: new Vector3(0f, 1.0f, 0f),
                rootRotation: Quaternion.Euler(15f, 30f, 10f));
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindChild(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static HumanPoseHandler GetPoseHandler(HumanoidMotionApplier applier)
        {
            var field = typeof(HumanoidMotionApplier)
                .GetField("_poseHandler", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Field '_poseHandler' not found on HumanoidMotionApplier");
            return (HumanPoseHandler)field.GetValue(applier);
        }
    }
}
