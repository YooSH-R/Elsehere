using System.Linq;
using A305.EditorTools.Common;
using A305.Mission;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트에서 만나야 하는 행인이 기다릴 자리를 마을에 둔다. 비 오는 거리 단계의 Ben은 카페 출입문 밖 도착 지점(-5.95, -11)에서
    /// 큰길 쪽 보도로 2m쯤 떨어져 문을 보고 기다린다. 이미 있으면 자리와 연결만 맞춘다.
    /// </summary>
    public static class QuestNpcSpotBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest NPC Spots";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string BenCode = "TOWN_PASSERBY";
        internal const string BenSpotName = "BenRainWaitSpot";
        private static readonly Vector3 BenSpot = new Vector3(-3.8f, .12f, -10.3f);

        [MenuItem(MenuPath)]
        public static void ApplyToTown()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Apply(BuilderTools.EnsureGroups(scene));
            BuilderTools.SaveScene(scene, ScenePath);
        }

        internal static void Apply(SceneGroups groups)
        {
            Passerby ben = BuilderTools
                .FindAllInScene<Passerby>(groups.Scene)
                .Single(passerby => passerby.GetComponent<NpcIdentity>().Code == BenCode);
            Transform spot = groups.Systems.Find(BenSpotName);
            if (spot == null)
            {
                spot = new GameObject(BenSpotName).transform;
                spot.SetParent(groups.Systems, false);
            }
            spot.SetPositionAndRotation(BenSpot, Quaternion.Euler(0f, 270f, 0f));
            var wait = BuilderTools.GetOrAddComponent<QuestNpcWaitSpot>(spot.gameObject);
            BuilderTools.SetReference(wait, "npc", ben);
        }
    }
}
