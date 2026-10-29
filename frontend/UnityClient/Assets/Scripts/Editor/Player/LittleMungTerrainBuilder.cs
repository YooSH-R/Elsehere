using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Player
{
    public static class LittleMungTerrainBuilder
    {
        public const string PrefabPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungTerrainPlayer.prefab";
        public const string ScenePath =
            "Assets/Scenes/Sandbox/LittleMungTerrainPlayerSandbox.unity";

        [MenuItem("Tools/A305/Player/Build Terrain Contact Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(LittleMungPoseBuilder.PrefabPath, PrefabPath);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var model = contents.GetComponentInChildren<Animator>().gameObject;
                var oldContact = model.GetComponent<LittleMungGroundContact>();
                if (oldContact != null)
                    Object.DestroyImmediate(oldContact);
                if (model.GetComponent<LittleMungTerrainContact>() == null)
                    model.AddComponent<LittleMungTerrainContact>();
                AttachSoleSkin(model);
                var armSettings = new SerializedObject(
                    model.GetComponent<LittleMungArmClearance>()
                );
                armSettings.FindProperty("handPadding").floatValue = .016f;
                armSettings.ApplyModifiedPropertiesWithoutUndo();
                var reference = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        LittleMungAdjustedRigBuilder.ModelPath
                    )
                );
                try
                {
                    var sourceAnimator = reference.GetComponent<Animator>();
                    var settings = new SerializedObject(
                        model.GetComponent<LittleMungTerrainContact>()
                    );
                    var up = settings.FindProperty("soleUp");
                    up.arraySize = 2;
                    up.GetArrayElementAtIndex(0).vector3Value = sourceAnimator
                        .GetBoneTransform(HumanBodyBones.LeftFoot)
                        .InverseTransformDirection(Vector3.up);
                    up.GetArrayElementAtIndex(1).vector3Value = sourceAnimator
                        .GetBoneTransform(HumanBodyBones.RightFoot)
                        .InverseTransformDirection(Vector3.up);
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                finally
                {
                    Object.DestroyImmediate(reference);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;
            AssetDatabase.CopyAsset(LittleMungPoseBuilder.ScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            var player = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                old.transform.parent,
                old.transform.position,
                old.transform.rotation
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera.gameObject);
            Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>모델 FBX를 다시 내보낸 뒤, 발바닥 스킨 복사본을 같은 에셋(GUID) 그대로 새 형태로 갱신한다. 프리팹은 건드리지 않는다.</summary>
        [MenuItem("Tools/A305/Player/Refresh Terrain Sole Skin")]
        public static void RefreshSoleSkins()
        {
            if (!BuilderTools.CanBuild())
                return;
            var source = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(LittleMungAdjustedRigBuilder.ModelPath)
            );
            try
            {
                var originals = source.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int skinIndex = 0; skinIndex < originals.Length; skinIndex++)
                    SaveSoleSkin(originals[skinIndex], skinIndex);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
            AssetDatabase.SaveAssets();
        }

        private static void AttachSoleSkin(GameObject model)
        {
            var source = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(LittleMungAdjustedRigBuilder.ModelPath)
            );
            try
            {
                var originals = source.GetComponentsInChildren<SkinnedMeshRenderer>();
                var targets = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int skinIndex = 0; skinIndex < originals.Length; skinIndex++)
                    targets[skinIndex].sharedMesh = SaveSoleSkin(originals[skinIndex], skinIndex);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        // 복사본은 모델에서 다시 만들 수 있는 파생 에셋이다. 모델 형태가 바뀌어도 따라가도록, 이미 있으면
        // 에셋을 지우지 않고 내용만 새로 채워 Female 등 이 메시를 쓰는 프리팹의 참조(GUID)를 유지한다.
        private static Mesh SaveSoleSkin(SkinnedMeshRenderer skin, int skinIndex)
        {
            const string folder = "Assets/Art/Player/LittleMung/TerrainSkin";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Art/Player/LittleMung", "TerrainSkin");
            string path = folder + "/Sole" + skinIndex + ".asset";
            var mesh = Object.Instantiate(skin.sharedMesh);
            mesh.name = "LittleMungTerrainSole" + skinIndex;
            StabilizeSoles(mesh, skin);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void StabilizeSoles(Mesh mesh, SkinnedMeshRenderer skin)
        {
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            var matrices = skin
                .bones.Select((bone, index) => bone.localToWorldMatrix * mesh.bindposes[index])
                .ToArray();
            var heights = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                var v = vertices[i];
                heights[i] = (
                    matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                    + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                    + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                    + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3
                ).y;
            }
            foreach (string prefix in new[] { "Left_", "Right_" })
            {
                var footBones = skin
                    .bones.Select(bone =>
                        bone.name.StartsWith(prefix + "Foot")
                        || bone.name.StartsWith(prefix + "Toes")
                    )
                    .ToArray();
                float Influence(BoneWeight w) =>
                    (footBones[w.boneIndex0] ? w.weight0 : 0)
                    + (footBones[w.boneIndex1] ? w.weight1 : 0)
                    + (footBones[w.boneIndex2] ? w.weight2 : 0)
                    + (footBones[w.boneIndex3] ? w.weight3 : 0);
                var selected = Enumerable
                    .Range(0, weights.Length)
                    .Where(i => Influence(weights[i]) >= .5f)
                    .ToArray();
                float bottom = selected.Min(i => heights[i]),
                    height = selected.Max(i => heights[i]) - bottom;
                foreach (int i in selected)
                {
                    // 밑창은 발/발가락만 따르고, 신발 윗부분으로 올라가며 원래 가중치에 연결한다.
                    float blend =
                        1
                        - Mathf.SmoothStep(
                            0,
                            1,
                            Mathf.InverseLerp(
                                bottom + height * .35f,
                                bottom + height * .7f,
                                heights[i]
                            )
                        );
                    var w = weights[i];
                    float sum = Influence(w);
                    float Weight(int bone, float value) =>
                        Mathf.Lerp(value, footBones[bone] ? value / sum : 0, blend);
                    w.weight0 = Weight(w.boneIndex0, w.weight0);
                    w.weight1 = Weight(w.boneIndex1, w.weight1);
                    w.weight2 = Weight(w.boneIndex2, w.weight2);
                    w.weight3 = Weight(w.boneIndex3, w.weight3);
                    weights[i] = w;
                }
            }
            mesh.boneWeights = weights;
        }
    }
}
