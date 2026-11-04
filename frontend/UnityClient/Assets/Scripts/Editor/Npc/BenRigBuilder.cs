using System;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.Npc;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// Ben의 도형 외형을 보존하며 관절과 전용 걷기 동작을 만든다.
    /// 짧은 다리·큰 머리에 맞춘 Generic rig이며 Humanoid 생활 클립으로 교체하지 않는다.
    /// 이미 만든 모델과 클립은 Inspector에서 조절한 내용을 보존한다.
    /// </summary>
    public static class BenRigBuilder
    {
        public const string VisualPath = "Assets/Prefabs/Npc/BenVisual.prefab";
        public const string ArtFolder = "Assets/Art/Characters/Npc/Ben";
        private const string SourcePath =
            "Assets/Prefabs/World/TownProduction/LittleMungV7NpcVisual.prefab";
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const float WalkSpeed = 1.1f;
        private const float CycleSeconds = .55f;

        [MenuItem("Tools/A305/Npc/Build Ben Rig")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            GameObject prefab = PrepareVisual();
            var scene = EditorSceneManager.OpenScene(TownPath);
            NpcIdentity ben = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "TOWN_PASSERBY");
            Transform visual = ben.transform.Find("TownVisual");
            if (visual == null)
                throw new InvalidOperationException("Ben의 기존 TownVisual이 없습니다.");

            string currentVisualPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                visual.gameObject
            );
            if (currentVisualPath != VisualPath)
            {
                if (currentVisualPath != SourcePath)
                    throw new InvalidOperationException(
                        "Ben의 외형이 바뀌어 자동 교체하지 않습니다: " + currentVisualPath
                    );
                Vector3 position = visual.localPosition;
                Quaternion rotation = visual.localRotation;
                Vector3 scale = visual.localScale;
                Object.DestroyImmediate(visual.gameObject);
                visual = BuilderTools
                    .InstantiatePrefab(prefab, ben.transform, position, rotation)
                    .transform;
                visual.name = "TownVisual";
                visual.localScale = scale;
            }
            BuilderTools.SetReference(
                ben.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            BenRigPreview.BuildSandboxAndCapture();
            Debug.Log("Ben rig connected: movement uses Speed; standing resets the walk pose.");
        }

        private static GameObject PrepareVisual()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(ArtFolder);
            var root = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath)
                );
            // 새 Ben 전용 복사본에서만 계층을 바꾼다. 상점 등이 쓰는 공용 도형 프리팹은 보존한다.
            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );
            root.name = "BenVisual";
            try
            {
                Transform model = root.transform.GetChild(0);
                Transform[] parts = model.Cast<Transform>().ToArray();
                Transform hips = Bone(model, model, "Hips", new Vector3(0, .27f, 0));
                Transform spine = Bone(model, hips, "Spine", new Vector3(0, .43f, 0));
                Transform head = Bone(model, spine, "Head", new Vector3(0, .58f, 0));
                foreach (Transform part in parts)
                {
                    if (part.name == "Jacket")
                        part.SetParent(spine, true);
                    else if (part.name == "Shorts")
                        part.SetParent(hips, true);
                    else if (part.localPosition.y > .6f)
                        part.SetParent(head, true);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    string prefix = side < 0 ? "Left" : "Right";
                    Transform upperArm = Bone(
                        model,
                        spine,
                        prefix + "UpperArm",
                        new Vector3(side * .23f, .48f, 0)
                    );
                    Transform lowerArm = Bone(
                        model,
                        upperArm,
                        prefix + "LowerArm",
                        new Vector3(side * .26f, .34f, 0)
                    );
                    Transform hand = Bone(
                        model,
                        lowerArm,
                        prefix + "Hand",
                        new Vector3(side * .27f, .28f, 0)
                    );
                    Transform upperLeg = Bone(
                        model,
                        hips,
                        prefix + "UpperLeg",
                        new Vector3(side * .1f, .255f, 0)
                    );
                    Transform lowerLeg = Bone(
                        model,
                        upperLeg,
                        prefix + "LowerLeg",
                        new Vector3(side * .1f, .15f, 0)
                    );
                    Transform foot = Bone(
                        model,
                        lowerLeg,
                        prefix + "Foot",
                        new Vector3(side * .1f, .07f, .01f)
                    );
                    Transform Part(string name) =>
                        parts.Single(part =>
                            part.name == name
                            && Mathf.Sign(model.InverseTransformPoint(part.position).x) == side
                        );
                    SkinLimb(
                        Part("Sleeve"),
                        model,
                        hips,
                        upperArm,
                        lowerArm,
                        prefix + "Sleeve",
                        .34f
                    );
                    SkinLimb(Part("Leg"), model, hips, upperLeg, lowerLeg, prefix + "Leg", .15f);
                    Part("Hand").SetParent(hand, true);
                    Part("Shoe").SetParent(foot, true);
                }
                var animator = model.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = PrepareController(model);
                return PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform Bone(
            Transform model,
            Transform parent,
            string name,
            Vector3 position
        )
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = model.TransformPoint(position);
            return bone;
        }

        private static void SkinLimb(
            Transform part,
            Transform model,
            Transform hips,
            Transform upper,
            Transform lower,
            string name,
            float jointHeight
        )
        {
            var filter = part.GetComponent<MeshFilter>();
            var renderer = part.GetComponent<MeshRenderer>();
            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = name;
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float height = model.InverseTransformPoint(part.TransformPoint(vertices[i])).y;
                // 관절 부근에서만 두 뼈의 영향을 섞어 원래 둥근 팔다리의 윤곽을 유지한다.
                float upperWeight = Mathf.SmoothStep(
                    0,
                    1,
                    Mathf.InverseLerp(jointHeight - .035f, jointHeight + .035f, height)
                );
                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = upperWeight,
                    boneIndex1 = 1,
                    weight1 = 1 - upperWeight,
                };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = new[]
            {
                upper.worldToLocalMatrix * part.localToWorldMatrix,
                lower.worldToLocalMatrix * part.localToWorldMatrix,
            };
            AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + name + ".asset");
            Material[] materials = renderer.sharedMaterials;
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
            var skin = part.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterials = materials;
            skin.bones = new[] { upper, lower };
            skin.rootBone = hips;
            skin.quality = SkinQuality.Bone2;
            // 움직인 팔다리가 정지 자세의 범위를 벗어나도 화면에서 사라지지 않게 한다.
            skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 6f);
            part.name = name + "Mesh";
        }

        private static RuntimeAnimatorController PrepareController(Transform model)
        {
            string path = ArtFolder + "/BenLocomotion.controller";
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (existing != null)
                return existing;
            AnimationClip idle = CreateMotion(model, false);
            AnimationClip walk = CreateMotion(model, true);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            AnimatorState state = controller.CreateBlendTreeInController(
                "Locomotion",
                out BlendTree tree
            );
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0);
            tree.AddChild(walk, WalkSpeed);
            state.speed = 1f / WalkSpeed;
            state.speedParameter = "Speed";
            state.speedParameterActive = true;
            controller.layers[0].stateMachine.defaultState = state;
            return controller;
        }

        private static AnimationClip CreateMotion(Transform model, bool walking)
        {
            string name = walking ? "BenWalk" : "BenIdle";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                ArtFolder + "/" + name + ".anim"
            );
            if (existing != null)
                return existing;
            var clip = new AnimationClip { name = name, frameRate = 30 };
            Transform[] bones = model.GetComponentsInChildren<Transform>();
            foreach (int side in new[] { -1, 1 })
            {
                string prefix = side < 0 ? "Left" : "Right";
                float Phase(float time) => time * Mathf.PI * 2 + (side < 0 ? 0 : Mathf.PI);
                float Thigh(float time) => -30f * Mathf.Cos(Phase(time));
                float Knee(float time) => 40f * Mathf.Max(0, -Mathf.Sin(Phase(time)));
                Rotation(prefix + "UpperLeg", Thigh);
                Rotation(prefix + "LowerLeg", Knee);
                Rotation(prefix + "Foot", time => -Thigh(time) - Knee(time));
                Rotation(prefix + "UpperArm", time => 24f * Mathf.Cos(Phase(time)));
                Rotation(prefix + "LowerArm", time => -12f * Mathf.Max(0, Mathf.Cos(Phase(time))));
            }
            Transform hips = bones.Single(bone => bone.name == "Hips");
            Curve(
                clip,
                model,
                hips,
                "m_LocalPosition.y",
                time =>
                    hips.localPosition.y
                    + (walking ? .012f * Mathf.Pow(Mathf.Sin(time * Mathf.PI * 2), 2) : 0)
            );
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, ArtFolder + "/" + name + ".anim");
            return clip;

            void Rotation(string boneName, Func<float, float> angle)
            {
                Transform bone = bones.Single(candidate => candidate.name == boneName);
                Curve(
                    clip,
                    model,
                    bone,
                    "localEulerAnglesRaw.x",
                    time => walking ? angle(time) : 0
                );
            }
        }

        private static void Curve(
            AnimationClip clip,
            Transform model,
            Transform bone,
            string property,
            Func<float, float> sample
        )
        {
            var curve = new AnimationCurve();
            const int samples = 32;
            for (int i = 0; i <= samples; i++)
                curve.AddKey(CycleSeconds * i / samples, sample((float)i / samples));
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    AnimationUtility.CalculateTransformPath(bone, model),
                    typeof(Transform),
                    property
                ),
                curve
            );
        }
    }
}
