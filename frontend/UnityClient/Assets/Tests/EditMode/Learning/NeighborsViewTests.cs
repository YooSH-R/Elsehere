using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Api;
using A305.Npc;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 휴대폰 [복습장] 옆 [이웃] 버튼으로 여는 창. 열린 이웃은 이름·역할, 장소·대화 수·아는 정도, 호감도를 한 줄에 보이고,
    /// 잠긴 NPC는 역할과 장소만 보인다. 열린 이웃을 누르면 정보 카드(지금 위치 포함)를 보인다.
    /// </summary>
    public class NeighborsViewTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string LifeDataPath = "Assets/Data/Npc/NpcLife.json";

        private GameObject hud;
        private NeighborsView view;
        private FakeNeighborsApi api;

        [SetUp]
        public void CreateHud()
        {
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            view = hud.GetComponent<NeighborsView>();
            typeof(NeighborsView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            api = new FakeNeighborsApi();
        }

        [TearDown]
        public void DestroyHud()
        {
            Object.DestroyImmediate(hud);
            NpcTracking.Shared.Clear();
        }

        private static Neighbor Max() =>
            new Neighbor
            {
                NpcCode = "ARCADE_STAFF",
                Unlocked = true,
                Name = "Max",
                RoleLabel = "오락실 직원",
                SceneName = "마을",
                Occupation = "attendant at Little Arcade on the main street",
                Interests = new List<string> { "video games" },
                Affinity = 12,
                Recognition = 30,
                TalkCount = 3,
            };

        private static Neighbor LockedCafeStaff() =>
            new Neighbor
            {
                NpcCode = "CAFE_BARISTA",
                Unlocked = false,
                RoleLabel = "카페 직원",
                SceneName = "모퉁이 카페",
            };

        [Test]
        public async Task Show_FillsNeighborAndLockedRows()
        {
            api.Neighbors.Add(Max());
            api.Neighbors.Add(LockedCafeStaff());

            await ShowLoaded();

            Assert.That(Shown("Neighbors"), Is.True);
            Assert.That(Text("Neighbors/Count"), Is.EqualTo("1 / 2명"));
            Assert.That(Text("Neighbors/Rows/Row0/Name"), Is.EqualTo(NeighborsText.Name(Max())));
            Assert.That(
                Text("Neighbors/Rows/Row0/Detail"),
                Is.EqualTo("마을 · 대화 3번 · 아는 정도 30")
            );
            Assert.That(
                Text("Neighbors/Rows/Row0/Affinity"),
                Is.EqualTo("<color=#FF6B81>♥</color> 12")
            );
            Assert.That(Text("Neighbors/Rows/Row0/Avatar/Letter"), Is.EqualTo("M"));
            Assert.That(Find("Neighbors/Rows/Row0").GetComponent<Button>().interactable, Is.True);
            Assert.That(
                Text("Neighbors/Rows/Row1/Name"),
                Is.EqualTo("??? <size=80%>카페 직원</size>")
            );
            Assert.That(Text("Neighbors/Rows/Row1/Affinity"), Is.EqualTo("잠김"));
            Assert.That(Find("Neighbors/Rows/Row1").GetComponent<Button>().interactable, Is.False);
            Assert.That(Shown("Neighbors/Rows/Row2"), Is.False);
            Assert.That(Shown("Neighbors/Card"), Is.False);
            Assert.That(Text("Neighbors/Empty"), Is.Empty);
            Assert.That(Text("Neighbors/Pager/PageNumber"), Is.EqualTo("1 / 1"));
            Assert.That(Shown("Neighbors/RetryButton"), Is.False);
        }

        [Test]
        public async Task NeighborRow_OpensTheCardWithWhereabouts_AndBackReturnsToTheList()
        {
            api.Neighbors.Add(Max());
            await ShowLoaded();

            Find("Neighbors/Rows/Row0").GetComponent<Button>().onClick.Invoke();

            NpcLifeEntry life = NpcLifeData
                .Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(LifeDataPath).text)
                .Npcs.Single(entry => entry.NpcId == "ARCADE_STAFF");
            string whereabouts = NpcWhereabouts.Describe(
                life,
                GameClock.Shared.Minutes,
                NpcDuty.RequiresPresence("ARCADE_STAFF")
            );
            Assert.That(Shown("Neighbors/Card"), Is.True);
            Assert.That(Shown("Neighbors/Rows"), Is.False);
            Assert.That(Shown("Neighbors/Pager"), Is.False);
            Assert.That(Text("Neighbors/Card/Name"), Is.EqualTo(NeighborsText.Name(Max())));
            Assert.That(
                Text("Neighbors/Card/Facts"),
                Is.EqualTo(NeighborsText.Facts(Max(), whereabouts))
            );

            Find("Neighbors/Card/BackButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(Shown("Neighbors/Card"), Is.False);
            Assert.That(Shown("Neighbors/Rows"), Is.True);
            Assert.That(Shown("Neighbors/Pager"), Is.True);
        }

        // 08시(시계를 처음 상태로 돌린 시각)에 오락실에서 일하는 Max는 지도 오락실 칸에 찍을 수 있다.
        [Test]
        public async Task MapButton_TracksTheNeighbor_ClosesTheWindow_AndOpensTheMap()
        {
            ResetClock();
            api.Neighbors.Add(Max());
            await ShowLoaded();
            int mapRequests = 0;
            void CountMapRequest() => mapRequests++;
            JourneyStatus.Shared.MapRequested += CountMapRequest;
            try
            {
                Find("Neighbors/Rows/Row0").GetComponent<Button>().onClick.Invoke();

                Assert.That(Shown("Neighbors/Card/MapButton"), Is.True);
                Assert.That(Shown("Neighbors/Card/MapHint"), Is.False);
                Assert.That(
                    Find("Neighbors/Card/MapButton/Label").GetComponent<TMP_Text>().text,
                    Is.EqualTo(NeighborsText.ShowOnMap)
                );

                Find("Neighbors/Card/MapButton").GetComponent<Button>().onClick.Invoke();

                Assert.That(NpcTracking.Shared.IsTracking("ARCADE_STAFF"), Is.True);
                Assert.That(NpcTracking.Shared.Name, Is.EqualTo("Max"));
                Assert.That(NpcTracking.Shared.Life.NpcId, Is.EqualTo("ARCADE_STAFF"));
                Assert.That(Shown("Neighbors"), Is.False, "지도를 가리지 않게 창을 닫습니다.");
                Assert.That(mapRequests, Is.EqualTo(1), "큰 지도를 엽니다.");
            }
            finally
            {
                JourneyStatus.Shared.MapRequested -= CountMapRequest;
            }
        }

        // 14~22시에 일하는 경찰관은 08시에 숙소에서 쉬어 지도에 찍지 않고 이유만 보인다.
        [Test]
        public async Task RestingNeighbor_ShowsWhyInsteadOfTheMapButton()
        {
            ResetClock();
            api.Neighbors.Add(
                new Neighbor
                {
                    NpcCode = "POLICE_ROOKIE",
                    Unlocked = true,
                    Name = "Leo",
                    TalkCount = 1,
                }
            );
            await ShowLoaded();

            Find("Neighbors/Rows/Row0").GetComponent<Button>().onClick.Invoke();

            Assert.That(Shown("Neighbors/Card/MapButton"), Is.False);
            Assert.That(Shown("Neighbors/Card/MapHint"), Is.True);
            Assert.That(
                Text("Neighbors/Card/MapHint"),
                Is.EqualTo(NeighborsText.MapHint(NpcSpot.Resting, playerInTown: false))
            );
            Assert.That(NpcTracking.Shared.Code, Is.Null);
        }

        [Test]
        public async Task Show_NobodyOpenYet_SaysHowToOpenUnderTheRows()
        {
            api.Neighbors.Add(LockedCafeStaff());

            await ShowLoaded();

            Assert.That(Shown("Neighbors/Rows/Row0"), Is.True);
            Assert.That(Text("Neighbors/Status"), Is.EqualTo(NeighborsText.NoneOpenYet));
        }

        [Test]
        public async Task Show_Failed_OffersRetry()
        {
            api.Failure = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했습니다."
            );

            await ShowLoaded();

            Assert.That(Text("Neighbors/Empty"), Is.EqualTo(NeighborsText.Failed));
            Assert.That(Shown("Neighbors/RetryButton"), Is.True);
        }

        [Test]
        public async Task NextButton_ShowsTheNextPage()
        {
            for (int i = 1; i <= 5; i++)
                api.Neighbors.Add(
                    new Neighbor
                    {
                        Unlocked = true,
                        Name = "NPC" + i,
                        TalkCount = 1,
                    }
                );
            await ShowLoaded();

            Find("Neighbors/Pager/NextButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(Text("Neighbors/Rows/Row0/Name"), Is.EqualTo("NPC5"));
            Assert.That(Shown("Neighbors/Rows/Row1"), Is.False);
            Assert.That(Text("Neighbors/Pager/PageNumber"), Is.EqualTo("2 / 2"));
        }

        [Test]
        public async Task CloseButton_HidesTheWindow()
        {
            await ShowLoaded();

            Find("Neighbors/CloseButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(Shown("Neighbors"), Is.False);
            Assert.That(Shown("NeighborsShade"), Is.False);
        }

        [Test]
        public void PhoneButton_HasNeighborsLabelIconAndViewConnection()
        {
            Transform phone = Find("Panel/Menu");
            Transform neighbors = phone.Find("NeighborsButton");

            Assert.That(neighbors, Is.Not.Null);
            Assert.That(phone.Find("ControlsButton"), Is.Not.Null);
            Assert.That(phone.Find("ReviewButton"), Is.Not.Null);
            Assert.That(
                neighbors.Find("Label").GetComponent<TMP_Text>().text,
                Is.EqualTo(NeighborsText.AppLabel)
            );
            Assert.That(neighbors.Find("Icon").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(Reference<Button>(view, "openButton").transform, Is.SameAs(neighbors));
        }

        // 서버 시각을 받기 전처럼 1일차 08시로 돌린다.
        private static void ResetClock() =>
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);

        private async Task ShowLoaded()
        {
            var book = new NeighborsBook(api);
            await book.LoadAsync();
            view.Show(book);
        }

        private Transform Find(string path)
        {
            Transform found = hud.transform.Find(path);
            Assert.That(found, Is.Not.Null, path);
            return found;
        }

        private bool Shown(string path) => Find(path).gameObject.activeSelf;

        private string Text(string path) => Find(path).GetComponent<TMP_Text>().text;

        private static T Reference<T>(Object owner, string field)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;

        private sealed class FakeNeighborsApi : INeighborsApi
        {
            public readonly List<Neighbor> Neighbors = new List<Neighbor>();
            public ApiException Failure;

            public Task<NeighborList> GetNeighborsAsync() =>
                Failure != null
                    ? Task.FromException<NeighborList>(Failure)
                    : Task.FromResult(
                        new NeighborList
                        {
                            NeighborCount = Neighbors.Count(neighbor => neighbor.Unlocked),
                            TotalCount = Neighbors.Count,
                            Npcs = Neighbors.ToList(),
                        }
                    );
        }
    }
}
