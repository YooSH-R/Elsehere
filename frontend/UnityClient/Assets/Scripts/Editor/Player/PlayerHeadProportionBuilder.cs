using System.Linq;
using A305.EditorTools.Characters;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    public static class PlayerHeadProportionBuilder
    {
        public const string SourcePath = "Assets/Prefabs/Player/Characters/Male.prefab";
        public const string PrefabPath = "Assets/Prefabs/Player/Characters/MaleHeadMatched.prefab";
        private const string Folder = "Assets/Art/Player/LittleMung/Male/HeadMatched";

        [MenuItem("Tools/A305/Player/Match Male Head To Female")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            var copy = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath)
                );
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Player/Characters/Female.prefab"
                )
            );
            try
            {
                var bones = copy.GetComponentsInChildren<Transform>();
                var poses = bones
                    .Select(b => (b.localPosition, b.localRotation, b.localScale))
                    .ToArray();
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Art/Player/LittleMung/Idle.anim"
                );
                idle.SampleAnimation(copy.GetComponentInChildren<Animator>().gameObject, 0);
                idle.SampleAnimation(reference.GetComponentInChildren<Animator>().gameObject, 0);
                Vector3 scale = CharacterHeadProportions.MatchDimensions(copy, reference);
                Debug.Log(
                    $"남성 머리 치수 보정: {scale:F4}, 기준 {CharacterHeadProportions.HeadBounds(reference)}, 결과 {CharacterHeadProportions.HeadBounds(copy)}"
                );
                BuilderTools.EnsureFolder(Folder);
                var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < skins.Length; i++)
                {
                    Mesh generated = skins[i].sharedMesh;
                    generated.name = "LittleMung_Male_HeadMatched";
                    string path = Folder + "/Surface" + i + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null)
                        AssetDatabase.CreateAsset(generated, path);
                    else
                    {
                        EditorUtility.CopySerialized(generated, saved);
                        Object.DestroyImmediate(generated);
                        skins[i].sharedMesh = saved;
                        EditorUtility.SetDirty(saved);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(skins[i]);
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = poses[i].localPosition;
                    bones[i].localRotation = poses[i].localRotation;
                    bones[i].localScale = poses[i].localScale;
                }
                copy.name = "MaleHeadMatched";
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                    "Assets/Data/Player/Characters/M.asset"
                );
                BuilderTools.SetReference(definition, "visualPrefab", prefab);
                PlayerCharacterFitBuilder.Fit(definition);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(reference);
            }
        }
    }
}
