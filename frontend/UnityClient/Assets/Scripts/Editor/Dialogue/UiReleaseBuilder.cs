using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Learning;
using A305.EditorTools.Mission;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.EditorTools.World;
using A305.Npc;
using A305.ObjectHover;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Dialogue
{
    public static class UiReleaseBuilder
    {
        [MenuItem("Tools/A305/UI/Apply Approved Dialogue Design")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            ServiceFontBuilder.Prepare();
            DialogueBuilder.PrepareAssets();
            LearningBuilder.PrepareAssets();
            FirstDayMissionBuilder.PrepareAssets();
            UiThemeBuilder.StyleFeaturePrefabs();
            UpdateCafe();
            var town = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            FirstDayShopBuilder.ConnectTownEntrance(BuilderTools.EnsureGroups(town));
            TownMapBuilder.Layout map = TownMapBuilder.Read(town);
            BuilderTools.SaveScene(town, town.path);
            BuilderTools.UpdateOrCreatePrefab(
                "Assets/Prefabs/Mission/FirstDayMission.prefab",
                "FirstDayMission",
                root => TownMapBuilder.Draw(root, map)
            );
            AssetDatabase.SaveAssets();
        }

        private static void UpdateCafe()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            var counter = BuilderTools
                .FindAllInScene<HoverableObject>(scene)
                .Single(item => item.UnityKey == "counter");
            Transform art = counter
                .GetComponentsInChildren<Transform>()
                .Single(item => item.name == "ServiceCounter");
            Renderer[] surfaces = art.GetComponentsInChildren<Renderer>();
            float oldTop = surfaces.Max(item => item.bounds.max.y);
            if (oldTop > .65f)
            {
                Vector3 scale = art.localScale;
                art.localScale = new Vector3(scale.x, scale.y * .8f, scale.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(art);
                float difference = surfaces.Max(item => item.bounds.max.y) - oldTop;
                Transform decoration = BuilderTools
                    .FindAllInScene<Transform>(scene)
                    .Single(item => item.name == "CafeDecoration");
                foreach (Transform prop in decoration)
                {
                    if (
                        Mathf.Abs(prop.position.z - 3.7f) >= .65f
                        || Mathf.Abs(prop.position.x) >= 3.65f
                        || prop.position.y <= .4f
                        || prop.position.y >= 1
                    )
                        continue;
                    prop.position += Vector3.up * difference;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
                }
            }
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(item => item.Code == "CAFE_STAFF");
            Vector3 towardCustomer = new Vector3(0, .05f, 2.1f) - npc.transform.position;
            towardCustomer.y = 0;
            float turn = Vector3.SignedAngle(
                BuilderTools.GetCharacterViewDirection(npc.gameObject, 0),
                towardCustomer,
                Vector3.up
            );
            npc.transform.rotation =
                Quaternion.AngleAxis(turn, Vector3.up) * npc.transform.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
            BuilderTools.SaveScene(scene, scene.path);
        }
    }
}
