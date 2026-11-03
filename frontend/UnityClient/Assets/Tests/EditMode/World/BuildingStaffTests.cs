using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 마을의 들어갈 수 있는 건물마다 직원 NPC가 한 명 있고, 생활하며 그 건물 1층 구역 밖으로 나가지 않는다.
    /// 같은 외형을 돌려 쓰더라도 NPC마다 자기 색 재질(머리·옷·눈동자)을 쓴다.
    /// </summary>
    public sealed class BuildingStaffTests
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const string ColorFolder = "Assets/Art/Characters/BuildingStaff/";

        [TestCase("PostOffice", "POST_OFFICE_STAFF")]
        [TestCase("MapleDiner", "DINER_STAFF")]
        [TestCase("TownBank", "BANK_STAFF")]
        [TestCase("SudsLaundromat", "LAUNDROMAT_STAFF")]
        [TestCase("BloomFlowers", "FLOWER_SHOP_STAFF")]
        [TestCase("MainStreetBarber", "BARBER_STAFF")]
        [TestCase("MapleBakery", "BAKERY_STAFF")]
        [TestCase("LittleArcade", "ARCADE_STAFF")]
        [TestCase("HomeLiving", "HOME_STORE_STAFF")]
        public void BuildingHasItsStaffInsideWhoStaysThere(string building, string code)
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            Transform root = Building(scene, building);
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(candidate => candidate.Code == code);

            Assert.That(npc, Is.Not.Null, building + "에 직원 NPC가 없습니다.");
            var routine = npc.GetComponent<NpcRoutine>();
            Assert.That(routine, Is.Not.Null, code + ": 생활(NpcRoutine)이 없습니다.");
            var fields = new SerializedObject(routine);
            Assert.That(fields.FindProperty("stayInArea").boolValue, Is.True, code);
            Bounds area = fields.FindProperty("area").boundsValue;
            Assert.That(
                area.Contains(npc.transform.position + Vector3.up * .1f),
                Is.True,
                code + ": 지낼 구역 밖에 서 있습니다."
            );
            Assert.That(
                area.Contains(root.TransformPoint(new Vector3(0, .5f, 1.5f))),
                Is.False,
                code + ": 구역이 문 밖 보도까지 넓습니다."
            );
            Assert.That(
                area.Contains(root.TransformPoint(new Vector3(0, 4.5f, -4f))),
                Is.False,
                code + ": 구역이 2층까지 넓습니다."
            );
        }

        [TestCase("POST_OFFICE_STAFF")]
        [TestCase("DINER_STAFF")]
        [TestCase("BANK_STAFF")]
        [TestCase("LAUNDROMAT_STAFF")]
        [TestCase("FLOWER_SHOP_STAFF")]
        [TestCase("BARBER_STAFF")]
        [TestCase("BAKERY_STAFF")]
        [TestCase("ARCADE_STAFF")]
        [TestCase("HOME_STORE_STAFF")]
        public void StaffWearsTheirOwnColors(string code)
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(candidate => candidate.Code == code);
            Assert.That(npc, Is.Not.Null, code);

            string[] own = npc.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => path.StartsWith(ColorFolder + code + "/"))
                .Distinct()
                .ToArray();

            Assert.That(own.Any(path => path.Contains("/Body")), Is.True, code + ": 몸 색 재질");
            Assert.That(own.Any(path => path.Contains("/Eye")), Is.True, code + ": 눈동자 색 재질");
        }

        // 색 사본은 텍스처만 다르고 그림자 받기 같은 설정은 외형의 원래 재질을 따른다.
        // 원본 눈이 얼굴과 같이 그림자를 받게 바뀌면(S15P31A305-217) 사본도 따라가야 눈 둘레만 밝게 뜨지 않는다.
        [TestCase("POST_OFFICE_STAFF")]
        [TestCase("DINER_STAFF")]
        [TestCase("BANK_STAFF")]
        [TestCase("LAUNDROMAT_STAFF")]
        [TestCase("FLOWER_SHOP_STAFF")]
        [TestCase("BARBER_STAFF")]
        [TestCase("BAKERY_STAFF")]
        [TestCase("ARCADE_STAFF")]
        [TestCase("HOME_STORE_STAFF")]
        public void StaffColorsKeepTheLookMaterialSettings(string code)
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(candidate => candidate.Code == code);
            Assert.That(npc, Is.Not.Null, code);

            int compared = 0;
            foreach (Renderer renderer in npc.GetComponentsInChildren<Renderer>(true))
            {
                Renderer original = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if (original == null)
                {
                    continue;
                }

                Material[] copies = renderer.sharedMaterials;
                Material[] sources = original.sharedMaterials;
                for (int index = 0; index < copies.Length && index < sources.Length; index++)
                {
                    if (!AssetDatabase.GetAssetPath(copies[index]).StartsWith(ColorFolder + code))
                    {
                        continue;
                    }

                    string name = renderer.name + " " + copies[index].name;
                    Assert.That(copies[index].shader, Is.EqualTo(sources[index].shader), name);
                    Assert.That(
                        copies[index].GetFloat("_ReceiveShadows"),
                        Is.EqualTo(sources[index].GetFloat("_ReceiveShadows")),
                        name
                    );
                    Assert.That(
                        copies[index].IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"),
                        Is.EqualTo(sources[index].IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")),
                        name
                    );
                    compared++;
                }
            }

            Assert.That(compared, Is.GreaterThan(0), code + ": 비교할 색 사본이 없습니다.");
        }

        private static Transform Building(Scene scene, string name)
        {
            Transform found = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == name);
            Assert.That(found, Is.Not.Null, "마을 씬에 건물이 없습니다: " + name);
            return found;
        }
    }
}
