using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Player
{
    public static class LittleMungStairBuilder
    {
        public const string PrefabPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungStairPlayer.prefab";
        public const string ScenePath = "Assets/Scenes/Sandbox/LittleMungStairPlayerSandbox.unity";

        [MenuItem("Tools/A305/Player/Build Predictive Stair Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            const string settingsPath = "Assets/Data/Player/LittleMung/StairLocomotion.asset";
            var config = AssetDatabase.LoadAssetAtPath<StairLocomotionSettings>(settingsPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<StairLocomotionSettings>();
                AssetDatabase.CreateAsset(config, settingsPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(LittleMungPoseBuilder.PrefabPath, PrefabPath);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = contents.GetComponentInChildren<Animator>();
                // Reuse the verified sole-only weight copy; its geometry and UVs match the Pose model.
                var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int index = 0; index < skins.Length; index++)
                {
                    var sole = AssetDatabase.LoadAssetAtPath<Mesh>(
                        "Assets/Art/Player/LittleMung/TerrainSkin/Sole" + index + ".asset"
                    );
                    if (sole != null)
                        skins[index].sharedMesh = sole;
                }
                var armSettings = new SerializedObject(
                    animator.GetComponent<LittleMungArmClearance>()
                );
                armSettings.FindProperty("handPadding").floatValue = .016f;
                armSettings.ApplyModifiedPropertiesWithoutUndo();
                var component = animator.GetComponent<LittleMungStairLocomotion>();
                if (component == null)
                    component = animator.gameObject.AddComponent<LittleMungStairLocomotion>();
                var reference = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        LittleMungAdjustedRigBuilder.ModelPath
                    )
                );
                try
                {
                    var serial = new SerializedObject(component);
                    serial.FindProperty("settings").objectReferenceValue = config;
                    var up = serial.FindProperty("soleUp");
                    up.arraySize = 2;
                    var source = reference.GetComponent<Animator>();
                    for (int side = 0; side < 2; side++)
                        up.GetArrayElementAtIndex(side).vector3Value = source
                            .GetBoneTransform(
                                side == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot
                            )
                            .InverseTransformDirection(Vector3.up);
                    serial.ApplyModifiedPropertiesWithoutUndo();
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
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
            }
            var stairScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("StairTopLanding") == null)
            {
                var landing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                landing.name = "StairTopLanding";
                landing.transform.position = new Vector3(-3, .2f, 4.25f);
                landing.transform.localScale = new Vector3(2, .4f, 2);
            }
            EditorSceneManager.SaveScene(stairScene);
            AssetDatabase.SaveAssets();
        }
    }
}
