using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.Motion.Tests.Frame
{
    /// <summary>
    /// HumanoidMotionFrame の EditMode 単体テスト。
    /// テスト観点:
    ///   - Muscles 配列の要素数 95 で <see cref="HumanoidMotionFrame.IsValid"/> が true
    ///   - Muscles 配列の要素数 0 (CreateInvalid) で <see cref="HumanoidMotionFrame.IsValid"/> が false
    ///   - コンストラクタに null を渡した場合 <see cref="Array.Empty{T}"/> にフォールバック
    ///   - RootPosition / RootRotation の読み取り専用性 (コンストラクタ値の保持)
    ///   - SkeletonType が Humanoid
    ///   - CreateInvalid(timestamp) が IsValid == false かつ Timestamp 保持のフレームを返す
    ///   - (M-3) BoneLocalRotations の保持・null フォールバック・IsValid 影響
    /// Requirements: Req 1, Req 2, Req 3, Req 14, M-3 合意変更
    /// </summary>
    [TestFixture]
    public class HumanoidMotionFrameTests
    {
        private const int HumanoidMuscleCount = 95;

        [Test]
        public void IsValid_WhenMusclesLength95_ReturnsTrue()
        {
            var muscles = new float[HumanoidMuscleCount];
            var frame = new HumanoidMotionFrame(
                timestamp: 1.0,
                muscles: muscles,
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity);

            Assert.That(frame.IsValid, Is.True);
            Assert.That(frame.Muscles.Length, Is.EqualTo(HumanoidMuscleCount));
        }

        [Test]
        public void IsValid_WhenMusclesLengthZero_ReturnsFalse()
        {
            var frame = HumanoidMotionFrame.CreateInvalid(timestamp: 2.0);

            Assert.That(frame.IsValid, Is.False);
            Assert.That(frame.Muscles.Length, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_WhenMusclesIsNull_DefaultsToEmptyArray()
        {
            var frame = new HumanoidMotionFrame(
                timestamp: 3.0,
                muscles: null,
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity);

            Assert.That(frame.Muscles, Is.Not.Null);
            Assert.That(frame.Muscles, Is.SameAs(Array.Empty<float>()));
            Assert.That(frame.IsValid, Is.False);
        }

        [Test]
        public void RootPosition_IsReadOnly()
        {
            var expected = new Vector3(1.0f, 2.0f, 3.0f);
            var frame = new HumanoidMotionFrame(
                timestamp: 4.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: expected,
                rootRotation: Quaternion.identity);

            Assert.That(frame.RootPosition, Is.EqualTo(expected));

            var property = typeof(HumanoidMotionFrame).GetProperty(nameof(HumanoidMotionFrame.RootPosition));
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False, "RootPosition は読み取り専用プロパティでなければならない");
        }

        [Test]
        public void RootRotation_IsReadOnly()
        {
            var expected = Quaternion.Euler(30.0f, 60.0f, 90.0f);
            var frame = new HumanoidMotionFrame(
                timestamp: 5.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: Vector3.zero,
                rootRotation: expected);

            Assert.That(frame.RootRotation, Is.EqualTo(expected));

            var property = typeof(HumanoidMotionFrame).GetProperty(nameof(HumanoidMotionFrame.RootRotation));
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False, "RootRotation は読み取り専用プロパティでなければならない");
        }

        [Test]
        public void SkeletonType_IsHumanoid()
        {
            var frame = new HumanoidMotionFrame(
                timestamp: 6.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity);

            Assert.That(frame.SkeletonType, Is.EqualTo(SkeletonType.Humanoid));
        }

        [Test]
        public void CreateInvalid_ReturnsInvalidFrame()
        {
            const double timestamp = 7.5;

            var frame = HumanoidMotionFrame.CreateInvalid(timestamp);

            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.IsValid, Is.False);
            Assert.That(frame.Timestamp, Is.EqualTo(timestamp));
            Assert.That(frame.Muscles, Is.Not.Null);
            Assert.That(frame.Muscles.Length, Is.EqualTo(0));
            Assert.That(frame.SkeletonType, Is.EqualTo(SkeletonType.Humanoid));
        }

        // ===== M-3: BoneLocalRotations 対応テスト =====

        [Test]
        public void BoneLocalRotations_LegacyConstructor_IsNull()
        {
            // 既存 4 引数コンストラクタは BoneLocalRotations を null にセットする (後方互換)。
            var frame = new HumanoidMotionFrame(
                timestamp: 1.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity);

            Assert.That(frame.BoneLocalRotations, Is.Null);
        }

        [Test]
        public void BoneLocalRotations_NewConstructor_PreservesDictionary()
        {
            var expected = new Dictionary<HumanBodyBones, Quaternion>
            {
                { HumanBodyBones.Hips, Quaternion.Euler(10f, 20f, 30f) },
                { HumanBodyBones.Head, Quaternion.Euler(40f, 50f, 60f) },
            };

            var frame = new HumanoidMotionFrame(
                timestamp: 2.0,
                muscles: Array.Empty<float>(),
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: expected);

            Assert.That(frame.BoneLocalRotations, Is.Not.Null);
            Assert.That(frame.BoneLocalRotations.Count, Is.EqualTo(2));
            Assert.That(frame.BoneLocalRotations[HumanBodyBones.Hips], Is.EqualTo(expected[HumanBodyBones.Hips]));
            Assert.That(frame.BoneLocalRotations[HumanBodyBones.Head], Is.EqualTo(expected[HumanBodyBones.Head]));
        }

        [Test]
        public void BoneLocalRotations_AllowsNullInNewConstructor()
        {
            // 新コンストラクタで null を渡しても例外を出さず、以降は BoneLocalRotations==null 扱いとなる。
            var frame = new HumanoidMotionFrame(
                timestamp: 3.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: null);

            Assert.That(frame.BoneLocalRotations, Is.Null);
            Assert.That(frame.IsValid, Is.True, "Muscles が埋まっていれば BoneLocalRotations が null でも有効");
        }

        [Test]
        public void IsValid_WhenMusclesEmptyButBoneRotationsProvided_ReturnsTrue()
        {
            // Muscles が空でも BoneLocalRotations が 1 件以上あれば有効フレーム扱い。
            var bones = new Dictionary<HumanBodyBones, Quaternion>
            {
                { HumanBodyBones.Hips, Quaternion.identity },
            };

            var frame = new HumanoidMotionFrame(
                timestamp: 4.0,
                muscles: Array.Empty<float>(),
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: bones);

            Assert.That(frame.IsValid, Is.True);
        }

        [Test]
        public void IsValid_WhenBothMusclesAndBoneRotationsEmpty_ReturnsFalse()
        {
            var emptyBones = new Dictionary<HumanBodyBones, Quaternion>();

            var frame = new HumanoidMotionFrame(
                timestamp: 5.0,
                muscles: Array.Empty<float>(),
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity,
                boneLocalRotations: emptyBones);

            Assert.That(frame.IsValid, Is.False);
        }

        [Test]
        public void BoneLocalRotations_IsReadOnly()
        {
            var property = typeof(HumanoidMotionFrame).GetProperty(nameof(HumanoidMotionFrame.BoneLocalRotations));
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False, "BoneLocalRotations は読み取り専用プロパティでなければならない");
        }
    }
}
