using System;
using NUnit.Framework;
using RealtimeAvatarController.MoCap.VMC.Internal;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// VmcBoneMapper.TryGetBone() の VMC Bone 名 → Unity HumanBodyBones 変換 EditMode テスト
    /// (tasks.md タスク 3-1 / design.md §7.1 / requirements.md 要件 5-1, 10-2)。
    ///
    /// <para>
    /// TDD 先行作成: 本テストファイル作成時点では以下の型は未実装である。
    ///   - <c>RealtimeAvatarController.MoCap.VMC.Internal.VmcBoneMapper</c>
    /// したがって本ファイルはタスク 3-2 (VmcBoneMapper 実装) 完了までコンパイルエラーとなってよい
    /// (tasks.md タスク 3-1 の TDD 方針および tasks.md タスク 10-3 で最終完成)。
    /// </para>
    ///
    /// <para>
    /// 検証対象:
    ///   - Unity <c>HumanBodyBones</c> の全列挙値名 (<c>LastBone</c> 除く) を <c>TryGetBone</c> に渡すと
    ///     すべて <c>true</c> を返し、対応する <c>HumanBodyBones</c> 値が得られること
    ///     (要件 5-1 / design.md §7.1 全 55 ボーン対応)
    ///   - 未知のボーン名 (例: <c>"UnknownBone"</c>) を渡すと <c>TryGetBone</c> が <c>false</c> を返すこと
    ///   - <c>null</c> または空文字を渡しても例外をスローせず <c>false</c> を返すこと
    /// </para>
    /// </summary>
    [TestFixture]
    public class VmcBoneMapperTests
    {
        [Test]
        public void TryGetBone_AllHumanBodyBoneNames_ExceptLastBone_ReturnsTrueWithMatchingValue()
        {
            foreach (HumanBodyBones expected in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (expected == HumanBodyBones.LastBone)
                {
                    continue;
                }

                var name = expected.ToString();
                var found = VmcBoneMapper.TryGetBone(name, out var actual);

                Assert.IsTrue(found,
                    $"HumanBodyBones.{name} は VMC Bone 名として TryGetBone が true を返すべき (design.md §7.1)。");
                Assert.AreEqual(expected, actual,
                    $"HumanBodyBones.{name} の名前照合は同一の列挙値を返すべき。");
            }
        }

        [Test]
        public void TryGetBone_LastBoneName_ReturnsFalse()
        {
            // design.md §7.1: HumanBodyBones.LastBone は辞書登録対象外 (要件 5-1)。
            var found = VmcBoneMapper.TryGetBone(nameof(HumanBodyBones.LastBone), out var bone);

            Assert.IsFalse(found,
                "HumanBodyBones.LastBone は VMC マッピング対象外であるため false を返すべき。");
            Assert.AreEqual(default(HumanBodyBones), bone,
                "TryGetBone が false を返すとき、out 引数は default 値であるべき。");
        }

        [Test]
        public void TryGetBone_UnknownBoneName_ReturnsFalse()
        {
            var found = VmcBoneMapper.TryGetBone("UnknownBone", out var bone);

            Assert.IsFalse(found,
                "未知のボーン名は TryGetBone が false を返すべき。");
            Assert.AreEqual(default(HumanBodyBones), bone,
                "TryGetBone が false を返すとき、out 引数は default 値であるべき。");
        }

        [Test]
        public void TryGetBone_NullName_ReturnsFalse_WithoutThrowing()
        {
            HumanBodyBones bone = default;
            bool found = true;

            Assert.DoesNotThrow(() =>
            {
                found = VmcBoneMapper.TryGetBone(null, out bone);
            }, "null 入力時に TryGetBone は例外をスローしてはならない。");

            Assert.IsFalse(found,
                "null 入力時は TryGetBone が false を返すべき。");
            Assert.AreEqual(default(HumanBodyBones), bone,
                "TryGetBone が false を返すとき、out 引数は default 値であるべき。");
        }

        [Test]
        public void TryGetBone_EmptyName_ReturnsFalse_WithoutThrowing()
        {
            HumanBodyBones bone = default;
            bool found = true;

            Assert.DoesNotThrow(() =>
            {
                found = VmcBoneMapper.TryGetBone(string.Empty, out bone);
            }, "空文字入力時に TryGetBone は例外をスローしてはならない。");

            Assert.IsFalse(found,
                "空文字入力時は TryGetBone が false を返すべき。");
            Assert.AreEqual(default(HumanBodyBones), bone,
                "TryGetBone が false を返すとき、out 引数は default 値であるべき。");
        }

        [Test]
        public void TryGetBone_IsCaseSensitive_OrdinalComparison()
        {
            // design.md §7.1: StringComparer.Ordinal による O(1) 厳密照合。
            // 大文字小文字違いの "hips" は HumanBodyBones.Hips にマッチしないこと。
            var found = VmcBoneMapper.TryGetBone("hips", out var bone);

            Assert.IsFalse(found,
                "Ordinal 比較のため、ケース違い 'hips' は Hips にマッチしないべき (design.md §7.1)。");
            Assert.AreEqual(default(HumanBodyBones), bone,
                "TryGetBone が false を返すとき、out 引数は default 値であるべき。");
        }
    }
}
