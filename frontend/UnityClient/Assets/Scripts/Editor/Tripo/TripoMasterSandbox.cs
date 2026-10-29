using System;
using System.IO;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoMasterSandbox
    {
        public static string Build(TripoAssetRecord record, string outputFolder = null)
        {
            BuilderTools.ThrowIfPlaying();
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
            var rig = TripoMasterRig.Inspect(visual, record.commonBody);
            if (!rig.Passed)
                throw new InvalidOperationException(string.Join("\n", rig.Errors));
            string name = Path.GetFileName(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(record.sourceModel))
            );
            string prefabs = outputFolder ?? "Assets/Prefabs/Player/Tripo";
            string scenes = outputFolder ?? "Assets/Scenes/Sandbox/Tripo";
            BuilderTools.EnsureFolder(prefabs);
            BuilderTools.EnsureFolder(scenes);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                prefabs + "/" + (outputFolder == null ? name : "ReviewPlayer") + ".prefab"
            );
            string scenePath = AssetDatabase.GenerateUniqueAssetPath(
                scenes + "/" + (outputFolder == null ? name : "Review") + ".unity"
            );
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                LittleMungStairBuilder.PrefabPath
            );
            var player = Object.Instantiate(source);
            try
            {
                Object.DestroyImmediate(player.transform.Find("Visual").gameObject);
                var replacement = (GameObject)
                    PrefabUtility.InstantiatePrefab(visual, player.transform);
                replacement.name = "Visual";
                var female = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                    "Assets/Data/Player/Characters/F.asset"
                );
                female.FitController(player.GetComponent<CharacterController>());
                BuilderTools.SetReference(
                    player.GetComponent<CharacterAnimationDriver>(),
                    "animator",
                    replacement.GetComponentInChildren<Animator>()
                );
                PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
            if (!AssetDatabase.CopyAsset(LittleMungStairBuilder.ScenePath, scenePath))
                throw new IOException("검수 씬 복사 실패");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var old = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                var camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                var instance = BuilderTools.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                    old.transform.parent,
                    old.transform.position,
                    old.transform.rotation
                );
                PlayerSandboxBuilder.ConnectPlayerAndCamera(instance, camera.gameObject);
                Object.DestroyImmediate(old.gameObject);
                EditorSceneManager.SaveScene(scene);
                return scenePath;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void ApplyFromCommandLine()
        {
            string path = Environment.GetEnvironmentVariable("TRIPO_MASTER_RECORD");
            var record = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(path);
            if (record == null)
                throw new InvalidOperationException("TRIPO_MASTER_RECORD 작업 기록을 지정하세요.");
            var visual = TripoMasterWorkflow.Apply(record);
            string scene = Build(record);
            string report = Environment.GetEnvironmentVariable("TRIPO_MASTER_REPORT");
            if (!string.IsNullOrEmpty(report))
                File.WriteAllText(
                    report,
                    JsonUtility.ToJson(
                        new Result
                        {
                            visual = AssetDatabase.GetAssetPath(visual),
                            scene = scene,
                            errors = record.validationErrors.ToArray(),
                        },
                        true
                    )
                );
        }

        [Serializable]
        private sealed class Result
        {
            public string visual;
            public string scene;
            public string[] errors;
        }
    }
}
