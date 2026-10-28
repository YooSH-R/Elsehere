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

        private static void AttachSoleSkin(GameObject model)
        {
            const string folder = "Assets/Art/Player/LittleMung/TerrainSkin";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Art/Player/LittleMung", "TerrainSkin");
            var source = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(LittleMungAdjustedRigBuilder.ModelPath)
            );
            try
            {
                var originals = source.GetComponentsInChildren<SkinnedMeshRenderer>();
                var targets = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int skinIndex = 0; skinIndex < originals.Length; skinIndex++)
                {
                    string path = folder + "/Sole" + skinIndex + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        var skin = originals[skinIndex];
                        mesh = Object.Instantiate(skin.sharedMesh);
                        mesh.name = "LittleMungTerrainSole" + skinIndex;
                        StabilizeSoles(mesh, skin);
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    targets[skinIndex].sharedMesh = mesh;
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
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
