using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Characters
{
    // Inspection only. No model, prefab, scene or facial shape is created/saved.
    public static class NpcSpeechAudit
    {
        [Serializable]
        private sealed class Entry
        {
            public string scene;
            public string npc;
            public string code;
            public bool supported;
            public string[] missingChannels;
            public string[] missingEmotionChannels;
        }

        [Serializable]
        private sealed class Report
        {
            public List<Entry> npcs = new();
        }

        private static IEnumerable<Entry> Inspect(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var npc in root.GetComponentsInChildren<NpcIdentity>(true))
            {
                string[] missing = CharacterFaceController.MissingChannels(npc.transform, true);
                yield return new Entry
                {
                    scene = scene.path,
                    npc = npc.name,
                    code = npc.Code,
                    supported = missing.Length == 0,
                    missingChannels = missing,
                    missingEmotionChannels = CharacterFaceController.MissingEmotionChannels(
                        npc.transform
                    ),
                };
            }
        }

        [MenuItem("Tools/A305/Characters/Check NPC Speech Support")]
        public static void CheckLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var npc in Inspect(SceneManager.GetSceneAt(i)))
                    Debug.Log(
                        $"{npc.npc} ({npc.code}): "
                            + (
                                npc.supported
                                    ? "Face channels ready for automatic speech binding."
                                    : "Voice only; missing Blend Shapes: "
                                        + string.Join(", ", npc.missingChannels)
                            )
                    );
        }

        public static void ExportProductionReport()
        {
            string output = Environment.GetEnvironmentVariable("NPC_SPEECH_AUDIT_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "Set NPC_SPEECH_AUDIT_OUTPUT to a report file outside Assets."
                );
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var report = new Report();
            try
            {
                foreach (
                    string guid in AssetDatabase
                        .FindAssets("t:Scene", new[] { "Assets/Scenes/Production" })
                        .OrderBy(g => g)
                )
                {
                    var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
                    report.npcs.AddRange(Inspect(scene));
                }
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log(
                    $"NPC speech audit: {report.npcs.Count} placed NPCs, {report.npcs.Count(n => n.supported)} with complete face channels."
                );
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
