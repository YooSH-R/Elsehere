using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 시안 스타일로 그린 마을 지도 그림(2000×800, 위가 0) 위에 내 위치와 목적지를 찍는 변환.
    /// 그림은 실제 마을 배치를 1m = 20단위로 그려, 큰길이 가로로 놓이고 북쪽이 오른쪽·서쪽이 위다.
    /// 미니맵 창은 내 위치를 가운데에 두되 지도 밖으로 나가지 않는다.
    /// </summary>
    public class TownMapArtTests
    {
        [Test]
        public void PlaceName_UsesBurgerShopForDiner()
        {
            Assert.That(TownMapArt.PlaceName("DINER_STAFF"), Is.EqualTo("햄버거 가게"));
        }

        [Test]
        public void ToArt_LaysTheMainStreetAcrossWithNorthToTheRight()
        {
            Assert.That(
                TownMapArt.ToArt(new Vector3(-20, 0, -32)),
                Is.EqualTo(Vector2.zero),
                "남서쪽 끝"
            );
            Assert.That(
                TownMapArt.ToArt(new Vector3(20, 0, 68)),
                Is.EqualTo(TownMapArt.Size),
                "북동쪽 끝"
            );
            Assert.That(
                TownMapArt.ToArt(new Vector3(0, 0, 18)),
                Is.EqualTo(new Vector2(1000, 400)),
                "큰길 가운데"
            );
        }

        [Test]
        public void Window_CentersOnThePlayerInsideTheMap()
        {
            var window = new Vector2(860, 580);

            Assert.That(
                TownMapArt.Window(new Vector2(1000, 400), window),
                Is.EqualTo(new Vector2(570, 110))
            );
            Assert.That(TownMapArt.Window(new Vector2(0, 0), window), Is.EqualTo(Vector2.zero));
            Assert.That(
                TownMapArt.Window(TownMapArt.Size, window),
                Is.EqualTo(TownMapArt.Size - window)
            );
        }

        // 화살표 그림은 위를 가리킨다. 지도는 북쪽이 오른쪽이라 북쪽을 보면 오른쪽, 동쪽을 보면 아래를 가리킨다.
        [TestCase(0f, -90f)]
        [TestCase(90f, -180f)]
        [TestCase(180f, -270f)]
        public void ArrowRotation_PointsTheUpwardArrowAlongTheHeading(float yaw, float expected)
        {
            Assert.That(TownMapArt.ArrowRotation(yaw), Is.EqualTo(expected).Within(.01f));
        }

        // 마을의 실제 건물 가운데(x, z)가 그 장소 칸 안에 있다.
        [TestCase("HOME", 13f, -17f)]
        [TestCase("HOTEL", 12f, -2f)]
        [TestCase("CAFE", -11f, -9f)]
        [TestCase("SHOP", -11f, -1f)]
        public void Tile_FramesEachMissionBuildingWhereItStands(string code, float x, float z)
        {
            Rect? tile = TownMapArt.Tile(code);

            Assert.That(tile.HasValue, Is.True);
            Assert.That(tile.Value.Contains(TownMapArt.ToArt(new Vector3(x, 0, z))), Is.True);
        }

        [Test]
        public void Tile_IsNullForTheStreet()
        {
            Assert.That(TownMapArt.Tile("TOWN"), Is.Null);
            Assert.That(TownMapArt.Door("TOWN"), Is.Null);
        }

        // 건물 안에 있을 때 화살표를 둘 출입문은 장소 칸 가장자리 바로 바깥(길 쪽)에 있다.
        [TestCase("HOME")]
        [TestCase("HOTEL")]
        [TestCase("CAFE")]
        [TestCase("SHOP")]
        public void Door_SitsJustOutsideItsTile(string code)
        {
            Rect tile = TownMapArt.Tile(code).Value;
            Vector2 door = TownMapArt.Door(code).Value;
            Rect near = new Rect(tile.position - Vector2.one * 30, tile.size + Vector2.one * 60);

            Assert.That(tile.Contains(door), Is.False);
            Assert.That(near.Contains(door), Is.True);
        }

        // [이웃]에서 찾는 NPC가 일터에 있으면 생활 데이터의 근무지 코드로 그 건물 칸을 찾는다.
        [TestCase("BAKERY", "BAKERY_STAFF")]
        [TestCase("BARBER", "BARBER_STAFF")]
        [TestCase("HOTEL", "HOTEL")]
        [TestCase("CAFE", "CAFE")]
        [TestCase("SHOP", "SHOP")]
        public void WorkplaceTile_FindsTheBuildingByWorkplaceCode(string workplace, string tileCode)
        {
            Assert.That(TownMapArt.WorkplaceTile(workplace), Is.EqualTo(TownMapArt.Tile(tileCode)));
        }

        [Test]
        public void WorkplaceTile_PoliceStationUsesTheLabelledBuildingOnTheMap()
        {
            Assert.That(
                TownMapArt.WorkplaceTile("POLICE_STATION"),
                Is.EqualTo(TownMapArt.PoliceTile)
            );
        }

        [TestCase("TOWN")]
        [TestCase("TAXI")]
        [TestCase(null)]
        public void WorkplaceTile_IsNullWithoutABuilding(string workplace)
        {
            Assert.That(TownMapArt.WorkplaceTile(workplace), Is.Null);
        }

        [Test]
        public void NeighborPoint_FollowsTheBodyInTown()
        {
            var body = new Vector3(0, 0, 18);

            Assert.That(
                TownMapArt.NeighborPoint(A305.Npc.NpcLocation.AtBody(body)),
                Is.EqualTo(TownMapArt.ToArt(body))
            );
        }

        // 핀 아래 이름표가 건물 아래 이름표(장소 이름)를 가리지 않게 건물 칸 위쪽 가장자리 가운데에 꽂는다.
        [Test]
        public void NeighborPoint_SitsOnTopOfTheWorkplace()
        {
            Rect bakery = TownMapArt.Tile("BAKERY_STAFF").Value;

            Assert.That(
                TownMapArt.NeighborPoint(A305.Npc.NpcLocation.AtWorkplace("BAKERY")),
                Is.EqualTo(new Vector2(bakery.center.x, bakery.yMin))
            );
        }

        [Test]
        public void NeighborPoint_IsNullWhenTheSpotIsNotOnTheMap()
        {
            Assert.That(TownMapArt.NeighborPoint(A305.Npc.NpcLocation.Resting), Is.Null);
            Assert.That(TownMapArt.NeighborPoint(A305.Npc.NpcLocation.Unknown), Is.Null);
            Assert.That(
                TownMapArt.NeighborPoint(A305.Npc.NpcLocation.AtWorkplace("TAXI")),
                Is.Null
            );
        }
    }
}
