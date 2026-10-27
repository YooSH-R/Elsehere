using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Player
{
    public static class LittleMungPoseBuilder
    {
        public const string PrefabPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungPosePlayer.prefab";
        public const string ScenePath = "Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity";

        [MenuItem("Tools/A305/Player/Build Contact And Arm Clearance Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                AssetDatabase.CopyAsset(LittleMungAdjustedRigBuilder.PrefabPath, PrefabPath);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var model = contents.GetComponentInChildren<Animator>().gameObject;
                if (model.GetComponent<LittleMungGroundContact>() == null)
                    model.AddComponent<LittleMungGroundContact>();
                if (model.GetComponent<LittleMungArmClearance>() == null)
                    model.AddComponent<LittleMungArmClearance>();
                // CPU에서 평가하는 네 가중치와 실제 렌더 품질을 맞춘다.
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    skin.quality = SkinQuality.Bone4;
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;
            AssetDatabase.CopyAsset(LittleMungAdjustedRigBuilder.ScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var previous = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            var player = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                previous.transform.parent,
                previous.transform.position,
                previous.transform.rotation
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera.gameObject);
            Object.DestroyImmediate(previous.gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
