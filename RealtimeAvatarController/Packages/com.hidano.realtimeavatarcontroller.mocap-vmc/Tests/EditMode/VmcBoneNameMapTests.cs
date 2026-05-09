using System;
using NUnit.Framework;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public class VmcBoneNameMapTests
    {
        [Test]
        public void TryGetValue_ResolvesAllHumanBodyBonesExceptLastBone()
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                Assert.That(VmcBoneNameMap.TryGetValue(bone.ToString(), out var resolved), Is.True,
                    $"{bone} should resolve from its PascalCase VMC bone name.");
                Assert.That(resolved, Is.EqualTo(bone),
                    $"{bone} should resolve to the matching HumanBodyBones value.");
            }
        }

        [Test]
        public void TryGetValue_WithLowercaseName_ReturnsFalse()
        {
            Assert.That(VmcBoneNameMap.TryGetValue("hips", out _), Is.False,
                "Bone name matching should be case-sensitive; only PascalCase names such as Hips should resolve.");
        }

        [Test]
        public void TryGetValue_WithUnknownName_ReturnsFalse()
        {
            Assert.That(VmcBoneNameMap.TryGetValue("Foo", out _), Is.False,
                "Unknown bone names should not resolve.");
        }

        [Test]
        public void TryGetValue_WithNullName_ReturnsFalseWithoutException()
        {
            var result = true;

            Assert.DoesNotThrow(() => result = VmcBoneNameMap.TryGetValue(null, out _),
                "Null input should be ignored without throwing.");
            Assert.That(result, Is.False, "Null input should not resolve.");
        }
    }
}
