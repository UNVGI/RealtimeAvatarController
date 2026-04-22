using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.Motion.Tests.PlayMode.Fixtures
{
    /// <summary>
    /// Motion-pipeline PlayMode テスト (タスク 7-2 / 7-3) で利用する
    /// 最小 Humanoid アバターのランタイムビルダー (タスク 7-1)。
    ///
    /// design.md §13.3 / tasks.md §7-1 / OI-3 で確定した
    /// テスト用 Humanoid Prefab のフィクスチャ実体。
    ///
    /// <para>
    /// ## なぜ .prefab ファイルではなくビルダーなのか
    /// Unity の Humanoid <see cref="Avatar"/> は元来 FBX インポータが
    /// 骨格パスを参照して焼き上げるアセットであり、Avatar アセット無しに
    /// .prefab を YAML で記述しても <see cref="Animator.isHuman"/> が
    /// <c>true</c> にならない。FBX を持ち込まずに Humanoid 適合を成立させる
    /// 唯一の現実解は <see cref="AvatarBuilder.BuildHumanAvatar"/> による
    /// プログラマティック生成であるため、テスト時に都度組み立てる方式とする。
    /// </para>
    ///
    /// <para>
    /// ## 構成
    /// ・Hips をルートとする 15 個の Humanoid 必須ボーンの GameObject 階層を生成<br/>
    /// ・<see cref="HumanDescription"/> を組み立て <see cref="AvatarBuilder.BuildHumanAvatar"/> を呼ぶ<br/>
    /// ・<see cref="Animator"/> に焼き上げた Avatar を割り当て<br/>
    /// ・<see cref="Renderer.enabled"/> トグル検証用に最小の <see cref="SkinnedMeshRenderer"/> を 1 件付与
    /// </para>
    ///
    /// <para>
    /// ## スレッド要件
    /// Unity API を直接呼ぶため、必ず Unity メインスレッドから呼び出すこと。
    /// </para>
    /// </summary>
    public static class TestHumanoidAvatarBuilder
    {
        /// <summary>
        /// 必須 Humanoid ボーン (15 本)。
        /// </summary>
        private static readonly string[] RequiredBoneNames =
        {
            "Hips",
            "Spine",
            "Head",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
        };

        /// <summary>
        /// テスト用 Humanoid アバターを生成して返す。
        /// 戻り値の <see cref="GameObject"/> はテスト終了時に
        /// <see cref="Object.Destroy(Object)"/> で破棄すること。
        /// </summary>
        public static GameObject Build(string name = "TestHumanoidAvatar")
        {
            var root = new GameObject(name);

            var bones = CreateBoneHierarchy(root);
            var avatar = BuildAvatar(root, bones);

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            AttachSkinnedMeshRenderer(root, bones["Hips"]);

            return root;
        }

        private static Dictionary<string, Transform> CreateBoneHierarchy(GameObject root)
        {
            var bones = new Dictionary<string, Transform>();

            var hips = NewBone("Hips", root.transform, new Vector3(0f, 1.0f, 0f));
            var spine = NewBone("Spine", hips, new Vector3(0f, 0.2f, 0f));
            var head = NewBone("Head", spine, new Vector3(0f, 0.4f, 0f));

            var leftUpperLeg = NewBone("LeftUpperLeg", hips, new Vector3(0.1f, -0.05f, 0f));
            var leftLowerLeg = NewBone("LeftLowerLeg", leftUpperLeg, new Vector3(0f, -0.45f, 0f));
            var leftFoot = NewBone("LeftFoot", leftLowerLeg, new Vector3(0f, -0.45f, 0.05f));

            var rightUpperLeg = NewBone("RightUpperLeg", hips, new Vector3(-0.1f, -0.05f, 0f));
            var rightLowerLeg = NewBone("RightLowerLeg", rightUpperLeg, new Vector3(0f, -0.45f, 0f));
            var rightFoot = NewBone("RightFoot", rightLowerLeg, new Vector3(0f, -0.45f, 0.05f));

            var leftUpperArm = NewBone("LeftUpperArm", spine, new Vector3(0.18f, 0.35f, 0f));
            var leftLowerArm = NewBone("LeftLowerArm", leftUpperArm, new Vector3(0.25f, 0f, 0f));
            var leftHand = NewBone("LeftHand", leftLowerArm, new Vector3(0.25f, 0f, 0f));

            var rightUpperArm = NewBone("RightUpperArm", spine, new Vector3(-0.18f, 0.35f, 0f));
            var rightLowerArm = NewBone("RightLowerArm", rightUpperArm, new Vector3(-0.25f, 0f, 0f));
            var rightHand = NewBone("RightHand", rightLowerArm, new Vector3(-0.25f, 0f, 0f));

            bones["Hips"] = hips;
            bones["Spine"] = spine;
            bones["Head"] = head;
            bones["LeftUpperLeg"] = leftUpperLeg;
            bones["LeftLowerLeg"] = leftLowerLeg;
            bones["LeftFoot"] = leftFoot;
            bones["RightUpperLeg"] = rightUpperLeg;
            bones["RightLowerLeg"] = rightLowerLeg;
            bones["RightFoot"] = rightFoot;
            bones["LeftUpperArm"] = leftUpperArm;
            bones["LeftLowerArm"] = leftLowerArm;
            bones["LeftHand"] = leftHand;
            bones["RightUpperArm"] = rightUpperArm;
            bones["RightLowerArm"] = rightLowerArm;
            bones["RightHand"] = rightHand;

            return bones;
        }

        private static Transform NewBone(string boneName, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(boneName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static Avatar BuildAvatar(GameObject root, Dictionary<string, Transform> bones)
        {
            var humanBones = new List<HumanBone>(RequiredBoneNames.Length);
            foreach (var humanName in RequiredBoneNames)
            {
                var hb = new HumanBone
                {
                    humanName = humanName,
                    boneName = humanName,
                    limit = new HumanLimit { useDefaultValues = true },
                };
                humanBones.Add(hb);
            }

            var skeleton = new List<SkeletonBone> { ToSkeletonBone(root.transform) };
            CollectSkeleton(root.transform, skeleton);

            var description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0.0f,
                hasTranslationDoF = false,
            };

            var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = "TestHumanoidAvatar";
            return avatar;
        }

        private static void CollectSkeleton(Transform t, List<SkeletonBone> sink)
        {
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                sink.Add(ToSkeletonBone(child));
                CollectSkeleton(child, sink);
            }
        }

        private static SkeletonBone ToSkeletonBone(Transform t)
        {
            return new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale,
            };
        }

        private static void AttachSkinnedMeshRenderer(GameObject root, Transform rootBone)
        {
            var meshHolder = new GameObject("Body");
            meshHolder.transform.SetParent(root.transform, worldPositionStays: false);

            var smr = meshHolder.AddComponent<SkinnedMeshRenderer>();
            smr.rootBone = rootBone;

            var mesh = new Mesh
            {
                name = "TestHumanoidStubMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0.1f, 0f),
                    new Vector3(0.1f, 0f, 0f),
                },
                triangles = new[] { 0, 1, 2 },
                boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                },
                bindposes = new[] { rootBone.worldToLocalMatrix * root.transform.localToWorldMatrix },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            smr.sharedMesh = mesh;
            smr.bones = new[] { rootBone };
        }
    }
}
