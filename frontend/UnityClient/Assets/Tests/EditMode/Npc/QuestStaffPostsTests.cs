using System.IO;
using System.Linq;
using A305.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 이야기 퀘스트 직원은 가게에 24시간 상주하며 카운터를 맡고, 같은 가게의 교대 직원은 카운터에서 떨어진 자기 자리를 지킨다.
    /// 퀘스트가 퇴근한 직원을 다시 불러내 근무 중인 교대 직원과 같은 자리에 겹쳐 세우지 않게 한다.
    /// </summary>
    public class QuestStaffPostsTests
    {
        private const int Hour = 60;

        private static readonly string[] QuestStaff =
        {
            "DINER_STAFF",
            "LAUNDROMAT_STAFF",
            "BANK_STAFF",
            "BARBER_STAFF",
            "FLOWER_SHOP_STAFF",
            "BAKERY_STAFF",
            "HOME_STORE_STAFF",
            "ARCADE_STAFF",
            "POST_OFFICE_STAFF",
        };

        // 가게, 상주하는 퀘스트 직원, 같은 가게의 교대 직원
        private static readonly object[] ShiftStaff =
        {
            new object[] { "MapleDiner", "DINER_STAFF", "FIRE_CAPTAIN" },
            new object[] { "SudsLaundromat", "LAUNDROMAT_STAFF", "FIRE_FIGHTER" },
            new object[] { "TownBank", "BANK_STAFF", "TOWN_EVENING_WALKER" },
            new object[] { "TownBank", "BANK_STAFF", "TOWN_SHOPPER" },
            new object[] { "MainStreetBarber", "BARBER_STAFF", "FIRE_ROOKIE" },
            new object[] { "BloomFlowers", "FLOWER_SHOP_STAFF", "HOTEL_CONCIERGE" },
            new object[] { "MapleBakery", "BAKERY_STAFF", "POLICE_PATROL_OFFICER" },
            new object[] { "HomeLiving", "HOME_STORE_STAFF", "TOWN_HANDYMAN" },
            new object[] { "LittleArcade", "ARCADE_STAFF", "TOWN_STUDENT" },
            new object[] { "PostOffice", "POST_OFFICE_STAFF", "TOWN_RETIREE" },
        };

        [OneTimeTearDown]
        public void CloseScenes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void QuestStaff_WorkAtTheirShopAllDay(
            [ValueSource(nameof(QuestStaff))] string npcCode
        )
        {
            NpcLifeData data = NpcLifeData.Parse(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json"))
            );
            NpcLifeEntry staff = data.FindNpc(npcCode);

            foreach (int hour in new[] { 0, 6, 12, 18, 21 })
            {
                Assert.That(
                    staff.ActivityAt(hour * Hour),
                    Is.EqualTo("WORK"),
                    $"{npcCode}는 {hour}시에도 가게에서 일해야 합니다."
                );
            }
        }

        [TestCaseSource(nameof(ShiftStaff))]
        public void ShiftStaff_KeepOwnSpotAwayFromTheCounter(
            string building,
            string questStaffCode,
            string shiftStaffCode
        )
        {
            Scene shop = EditorSceneManager.OpenScene(
                $"Assets/Scenes/ShopInteriors/{building}Interior.unity",
                OpenSceneMode.Single
            );
            Transform questStaff = Npc(shop, questStaffCode);
            Transform shiftStaff = Npc(shop, shiftStaffCode);

            var routine = new SerializedObject(shiftStaff.GetComponent<NpcRoutine>());
            Assert.That(
                routine.FindProperty("keepsOwnPost").boolValue,
                Is.True,
                "교대 직원은 근무 중에 자기 자리를 지켜야 합니다."
            );
            Assert.That(
                HorizontalDistance(shiftStaff.position, questStaff.position),
                Is.GreaterThan(1f),
                "교대 직원이 상주 직원과 겹쳐 서지 않아야 합니다."
            );
            foreach (Bounds counter in Counters(shop))
            {
                Vector3 nearest = counter.ClosestPoint(
                    new Vector3(shiftStaff.position.x, counter.center.y, shiftStaff.position.z)
                );
                Assert.That(
                    HorizontalDistance(shiftStaff.position, nearest),
                    Is.GreaterThan(1.5f),
                    "교대 직원은 카운터를 맡지 않도록 카운터에서 떨어져 서야 합니다."
                );
            }
        }

        private static Transform Npc(Scene scene, string code)
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Single(npc => npc.Code == code)
                .transform;
        }

        // 손님을 맞는 카운터(NpcRoutine이 맡는 사물)의 보이는 영역
        private static Bounds[] Counters(Scene scene)
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HoverableObject>(true))
                .Where(placed =>
                    placed.UnityKey == "counter" || placed.UnityKey == "reception_desk"
                )
                .Select(placed =>
                {
                    Renderer[] renderers = placed.GetComponentsInChildren<Renderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);
                    return bounds;
                })
                .ToArray();
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;
        }
    }
}
