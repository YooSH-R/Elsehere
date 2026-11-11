using System;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.TownShopInteriorPieces;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 매장 12곳의 소품 배치. 좌표는 건물 기준(앞면 문 z=0, 뒷벽 z=-7.8, 옆벽 x=±3.8)이고 y는 그 층 바닥에서 잰다.
    /// 문 앞 통로(문 폭 + 양쪽 0.2m, 안쪽 2m), 직원 자리, 계단은 비운다.
    /// </summary>
    internal static class TownShopInteriorLayouts
    {
        /// <summary>떼어 내 옮길 수 있는 공간 하나. 건물 프리팹의 Interior 아래 그 층 높이에 놓는다.</summary>
        internal sealed class Space
        {
            public Space(string name, string buildingPrefab, float floor, Action<Transform> furnish)
            {
                Name = name;
                BuildingPrefab = buildingPrefab;
                Floor = floor;
                Furnish = furnish;
            }

            public string Name { get; }
            public string BuildingPrefab { get; }
            public float Floor { get; }
            public Action<Transform> Furnish { get; }
        }

        private const string Storefronts = TownStorefrontBuilder.Folder + "/";
        private const string NorthBlock = NorthBlockAssets.Folder + "/";

        // 실내 벽 표면. 벽에 거는 소품의 뒷면을 여기에 붙인다.
        private const float SideWall = 3.77f;
        private const float BackWall = -7.77f;
        private const float FrontWall = -.2f;

        // 매장 실내 천장 높이(1층 4m 건물의 천장판 아래)
        private const float Ceiling = 3.83f;

        internal static readonly Space[] Spaces =
        {
            new Space("PostOfficeInterior", Storefronts + "PostOffice.prefab", 0, PostOffice),
            new Space("MapleDinerInterior", Storefronts + "MapleDiner.prefab", 0, Diner),
            new Space("TownBankInterior", Storefronts + "TownBank.prefab", 0, Bank),
            new Space("LaundromatInterior", Storefronts + "SudsLaundromat.prefab", 0, Laundromat),
            new Space("FlowerShopInterior", Storefronts + "BloomFlowers.prefab", 0, FlowerShop),
            new Space("BarberShopInterior", Storefronts + "MainStreetBarber.prefab", 0, BarberShop),
            new Space("BakeryInterior", NorthBlock + "MapleBakery.prefab", 0, Bakery),
            new Space("BakeryLoungeInterior", NorthBlock + "MapleBakery.prefab", 4, BakeryLounge),
            new Space("ArcadeInterior", NorthBlock + "LittleArcade.prefab", 0, Arcade),
            new Space("BookshopInterior", NorthBlock + "LittleArcade.prefab", 4, Bookshop),
            new Space("HomeGoodsInterior", NorthBlock + "HomeLiving.prefab", 0, HomeGoods),
            new Space("TeaRoomInterior", NorthBlock + "HomeLiving.prefab", 4, TeaRoom),
        };

        // ---------- 거리 매장 ----------

        // 왼쪽 벽 이발대 둘과 이발 의자(기존) 옆에 도구, 뒤 왼쪽 샴푸 세면대, 뒤 오른쪽 제품 선반, 오른쪽 대기 소파(기존) 둘레와 앞 대기 의자
        private static void BarberShop(Transform space)
        {
            Transform stations = Zone("Stations", space);
            foreach (float z in new[] { -2.8f, -5.4f })
            {
                BarberStation(stations, new Vector3(-3.42f, 0, z), 90);
                BarbicideJar(stations, new Vector3(-3.3f, .84f, z + .38f));
                BarberTools(stations, new Vector3(-3.3f, .84f, z - .25f), 90);
            }
            ToolTrolley(stations, new Vector3(-3.3f, 0, -4.1f), 90);
            HoodDryerChair(stations, new Vector3(-3.2f, 0, -1.3f), 90);
            CoatRack(stations, new Vector3(-3.4f, 0, -.45f), 0);

            Transform wash = Zone("WashCorner", space);
            ShampooSink(wash, new Vector3(-2.85f, 0, -7.43f), 0);
            WashChair(wash, new Vector3(-2.85f, 0, -6.6f), 0);
            Broom(wash, new Vector3(-3.55f, 0, -6.45f), 90);
            Kit("SmallCabinet", wash, new Vector3(-1.6f, 0, -7.4f));
            TowelStack(wash, new Vector3(-1.6f, .7f, -7.4f), 0);
            PlainShelf(wash, new Vector3(-1.6f, 1.45f, BackWall), 0);
            DetergentBottles(wash, new Vector3(-1.6f, 1.45f, -7.63f), 0);
            TrashBin(wash, new Vector3(-.75f, 0, -7.45f));
            WallBoard(
                "BarberPriceBoard",
                wash,
                new Vector3(-2.7f, 2.0f, BackWall),
                0,
                "PRICES",
                "HAIRCUT  $18\nSHAVE  $12\nKIDS CUT  $14",
                1.3f,
                1.0f,
                "#293E57"
            );
            WallClock(wash, new Vector3(-1.6f, 2.35f, BackWall), 0);

            Transform products = Zone("ProductCorner", space);
            Kit("LowCabinet", products, new Vector3(2.6f, 0, -7.42f));
            DetergentBottles(products, new Vector3(2.6f, .7f, -7.4f), 0);
            PlainShelf(products, new Vector3(2.6f, 1.45f, BackWall), 0);
            TowelStack(products, new Vector3(2.6f, 1.45f, -7.63f), 0);
            Treat("HousePlants", "Monstera", products, new Vector3(3.3f, 0, -6.4f));
            Kit("StandingLamp", products, new Vector3(3.35f, 0, -5.3f));

            Transform waiting = Zone("Waiting", space);
            Kit("RugOvalBlue", waiting, new Vector3(2.3f, 0, -3.6f), 90);
            FramedPrint(waiting, new Vector3(SideWall, 1.85f, -3.6f), -90, "Sunset");
            Kit("CushionChair", waiting, new Vector3(3.3f, 0, -1.0f), -90);
            Kit("CushionChair", waiting, new Vector3(3.3f, 0, -1.75f), -90);
            Kit("PictureSmallA", waiting, new Vector3(SideWall, 1.9f, -1.4f), -90);
            MagazineRack(waiting, new Vector3(2.3f, 0, -.38f), 180);
        }

        // 뒷벽 우편 분류 칸·수레·소포, 손님 쪽 줄서기·양식 탁자·우체통, 벽 시계와 포스터
        private static void PostOffice(Transform space)
        {
            Transform back = Zone("BackOffice", space);
            Kit("LowCabinet", back, new Vector3(2.2f, 0, -7.42f));
            SortingCubbies(back, new Vector3(2.2f, .72f, -7.6f), 0);
            MailCart(back, new Vector3(-.4f, 0, -6.85f), 0);
            MailCart(back, new Vector3(.75f, 0, -6.75f), 20);
            ParcelStack(back, new Vector3(-2.2f, 0, -7.35f), 0);
            Kit("WoodStool", back, new Vector3(1.6f, 0, -6.4f));
            ServiceBell(back, new Vector3(.2f, 1.06f, -5.45f));

            Transform lobby = Zone("Lobby", space);
            QueueLine(lobby, new Vector3(1.5f, 0, -3.6f), 90, 2);
            FormDesk(lobby, new Vector3(2.4f, 0, -.45f), 180);
            BlueMailbox(lobby, new Vector3(-2.0f, 0, -.5f), 180);
            TrashBin(lobby, new Vector3(-2.6f, 0, -.4f));
            Kit("RugStripesBlue", lobby, new Vector3(0, 0, -1.2f));
            WallClock(lobby, new Vector3(0, 3.1f, FrontWall), 180);
            FramedPrint(lobby, new Vector3(SideWall, 2.75f, -3.4f), -90, "Sunset");
            FramedPrint(lobby, new Vector3(-SideWall, 1.75f, -.95f), 90, "Landscape");
            NoticeBoard(lobby, new Vector3(-SideWall, 1.6f, -6.2f), 90);
        }

        // 뒷벽 주방(냉장고·조리대·레인지와 후드), 카운터와 부스 위 소스·냅킨·버거, 부스 위 조명, 앞쪽 주크박스·껌 기계
        private static void Diner(Transform space)
        {
            Transform kitchen = Zone("Kitchen", space);
            Kit("KitchenFridge", kitchen, new Vector3(-1.85f, 0, -7.3f));
            Kit("KitchenCounter", kitchen, new Vector3(-.5f, 0, -7.34f));
            Kit("CuttingBoard", kitchen, new Vector3(-.5f, .88f, -7.3f));
            Kit("KitchenCounterDecorated", kitchen, new Vector3(1.1f, 0, -7.34f));
            Kit("Stove", kitchen, new Vector3(2.75f, 0, -7.34f));
            Kit("RangeHood", kitchen, new Vector3(2.75f, 0, BackWall));
            Kit("CookingPot", kitchen, new Vector3(2.4f, 1.06f, -7.3f));
            Kit("FryingPan", kitchen, new Vector3(3.1f, 1.06f, -7.25f));

            Transform counter = Zone("CounterTop", space);
            foreach (float z in new[] { -2.75f, -5.15f })
            {
                Kit("Ketchup", counter, new Vector3(-2.8f, 1.06f, z));
                Kit("Mustard", counter, new Vector3(-2.8f, 1.06f, z - .13f));
            }
            NapkinDispenser(counter, new Vector3(-2.8f, 1.06f, -3.95f), 90);
            Kit("DinerPlate", counter, new Vector3(-2.7f, 1.06f, -4.8f));
            Kit("Burger", counter, new Vector3(-2.7f, 1.09f, -4.8f));
            ServiceBell(counter, new Vector3(-2.75f, 1.06f, -6.3f));
            Kit("TableMenu", counter, new Vector3(-2.85f, 1.06f, -6.0f), 90);

            Transform booths = Zone("Booths", space);
            foreach (float z in new[] { -2.8f, -5.6f })
            {
                Kit("Ketchup", booths, new Vector3(3.4f, .77f, z + .12f));
                Kit("Mustard", booths, new Vector3(3.4f, .77f, z - .02f));
                NapkinDispenser(booths, new Vector3(3.4f, .77f, z - .22f), -90);
                Kit("Burger", booths, new Vector3(2.6f, .785f, z));
                PendantLamp(booths, new Vector3(2.9f, Ceiling, z), .55f);
            }
            Kit("PictureSmallA", booths, new Vector3(SideWall, 1.85f, -2.8f), -90);
            FramedPrint(booths, new Vector3(SideWall, 1.9f, -5.6f), -90, "Botanical");

            Transform entrance = Zone("Entrance", space);
            Jukebox(entrance, new Vector3(2.6f, 0, -.48f), 180);
            GumballMachine(entrance, new Vector3(-1.9f, 0, -.5f), 0);
            Treat("HousePlants", "Monstera", entrance, new Vector3(-3.2f, 0, -.65f));
            FramedPrint(entrance, new Vector3(-SideWall, 2.3f, -4.3f), 90, "Sunset");
            WallClock(entrance, new Vector3(0, 3.1f, FrontWall), 180);
        }

        // 창구 뒤 의자·화면·금고·서류함, 손님 쪽 대기 깔개·그림·안내 책자·정수기·안내판, 문 양쪽 화분
        private static void Bank(Transform space)
        {
            Transform tellers = Zone("Tellers", space);
            foreach (float x in new[] { -2.2f, 0, 2.2f })
            {
                Kit("CushionChair", tellers, new Vector3(x, 0, -6.45f));
                DeskMonitor(tellers, new Vector3(x + .45f, 1.16f, -5.75f), 180);
            }
            VaultDoor(tellers, new Vector3(2.3f, 0, BackWall), 0);
            Kit("DecoratedSmallCabinet", tellers, new Vector3(-2.8f, 0, -7.38f));
            Kit("SmallCabinet", tellers, new Vector3(-2.0f, 0, -7.4f));
            WallClock(tellers, new Vector3(-2.4f, 2.35f, BackWall), 0);
            Kit("CactusMedium", tellers, new Vector3(-.9f, 0, -7.4f));

            Transform lobby = Zone("Lobby", space);
            Kit("RugBlue", lobby, new Vector3(-3.0f, 0, -2.6f), 90);
            FramedPrint(lobby, new Vector3(-SideWall, 1.85f, -2.6f), 90, "Sunset");
            BrochureRack(lobby, new Vector3(3.45f, 0, -.65f), -90);
            WaterCooler(lobby, new Vector3(3.45f, 0, -3.3f), -90);
            WallBoard(
                "BankRatesBoard",
                lobby,
                new Vector3(SideWall, 1.8f, -4.2f),
                -90,
                "SAVINGS RATES",
                "1 YEAR  4.2%\n2 YEARS  4.5%",
                1.2f,
                .9f,
                "#293E57"
            );
            foreach (int side in new[] { -1, 1 })
                Treat("HousePlants", "SnakePlant", lobby, new Vector3(side * 1.75f, 0, -.45f));
            Kit("StandingLamp", lobby, new Vector3(-3.35f, 0, -.6f));
        }

        // 세탁기 위 바구니·세제, 왼쪽 벽 선반, 뒤 왼쪽 세제 자판기·수레, 뒤 오른쪽 옷걸이, 오른쪽 벽 게시판·동전 교환기
        private static void Laundromat(Transform space)
        {
            Transform washers = Zone("WasherRow", space);
            LaundryBasket(washers, new Vector3(-3.3f, .9f, -1.75f), 90);
            DetergentBottles(washers, new Vector3(-3.3f, .9f, -3.6f), 90);
            LaundryBasket(washers, new Vector3(-3.3f, .9f, -5.5f), 90);
            PlainShelf(washers, new Vector3(-SideWall, 1.6f, -2.65f), 90);
            DetergentBottles(washers, new Vector3(-3.63f, 1.6f, -2.65f), 90);
            PlainShelf(washers, new Vector3(-SideWall, 1.6f, -4.55f), 90);
            TowelStack(washers, new Vector3(-3.63f, 1.6f, -4.55f), 90);

            Transform back = Zone("BackCorner", space);
            DetergentVending(back, new Vector3(-3.3f, 0, -7.42f), 0);
            LaundryCart(back, new Vector3(-2.35f, 0, -6.55f), 20);
            ClothesRack(back, new Vector3(2.95f, 0, -7.25f), 0);
            WallBoard(
                "LaundryRulesBoard",
                back,
                new Vector3(-2.8f, 2.35f, BackWall),
                0,
                "PLEASE",
                "CLEAN THE LINT\nAFTER EACH DRY",
                1.4f,
                .8f,
                "#2F7C86"
            );

            Transform lounge = Zone("Lounge", space);
            NoticeBoard(lounge, new Vector3(SideWall, 1.75f, -2.1f), -90);
            ChangeMachine(lounge, new Vector3(SideWall, 1.3f, -4.3f), -90);
            WallClock(lounge, new Vector3(0, 3.1f, FrontWall), 180);
            TrashBin(lounge, new Vector3(3.35f, 0, -.55f));
            LaundryCart(lounge, new Vector3(.9f, 0, -2.45f), -15);
        }

        // 양쪽 벽 화분 선반, 천장에 건 화분, 뒤 꽃 냉장고, 계산대 뒤 포장지와 꽃다발, 앞쪽 꽃 통과 물뿌리개
        private static void FlowerShop(Transform space)
        {
            Transform walls = Zone("PlantShelves", space);
            foreach (int side in new[] { -1, 1 })
            {
                float yaw = side < 0 ? 90 : -90;
                PlantShelf(walls, new Vector3(side * SideWall, 1.45f, -2.5f), yaw);
                PlantShelf(walls, new Vector3(side * SideWall, 1.45f, -4.3f), yaw);
                PlantShelf(walls, new Vector3(side * SideWall, 2.05f, -3.4f), yaw);
                foreach (float z in new[] { -2.6f, -5.4f })
                    HangingPlant(walls, new Vector3(side * 1.5f, Ceiling, z));
            }

            Transform counter = Zone("WrappingCounter", space);
            FlowerCooler(counter, new Vector3(-1.25f, 0, -7.45f), 0);
            WrappingRoll(counter, new Vector3(2.4f, 1.35f, BackWall), 0);
            Bouquet(counter, new Vector3(2.0f, .9f, -6.55f), 20);
            Bouquet(counter, new Vector3(2.85f, .9f, -6.45f), -30);
            Bouquet(counter, new Vector3(-.5f, .75f, -4.25f), 90);
            WateringCan(counter, new Vector3(.55f, .75f, -4.6f), 30);

            Transform entrance = Zone("Entrance", space);
            Kit("SideTable", entrance, new Vector3(2.7f, 0, -.65f));
            Vase(entrance, new Vector3(2.55f, .7f, -.65f), "#E85D75");
            WateringCan(entrance, new Vector3(2.9f, .7f, -.6f), 30);
            FlowerPail(entrance, new Vector3(-2.4f, 0, -.5f), "#F4C430");
            FlowerPail(entrance, new Vector3(-1.85f, 0, -.75f), "#E85D75");
            FlowerPail(entrance, new Vector3(-2.9f, 0, -.85f), "#B07CC6");
            WallClock(entrance, new Vector3(0, 3.1f, FrontWall), 180);
        }

        // ---------- 북쪽 상가 ----------

        // 왼쪽 진열 카운터(기존) 앞 가격표, 뒷벽 빵 선반, 창가 케이크 탁자, 계단참 아래 오븐·쟁반 선반, 계단 아래 재료 상자
        private static void Bakery(Transform space)
        {
            Transform racks = Zone("BreadWall", space);
            BreadRack(racks, new Vector3(-2.85f, 0, -7.5f), 0);
            BreadRack(racks, new Vector3(-1.1f, 0, -7.5f), 0);
            WallBoard(
                "BreadBoard",
                racks,
                new Vector3(-1.95f, 2.5f, BackWall),
                0,
                "FRESH BREAD DAILY",
                null,
                2.6f,
                .45f,
                "#326B57"
            );
            // 카운터(기존)의 빵 쟁반·유리 진열장·케이크 받침 앞마다 가격표와 조명을 둔다.
            foreach (float z in new[] { -2.85f, -4.4f, -5.92f })
            {
                Treat("BakeryInterior", "PriceCard", racks, new Vector3(-2.45f, .9f, z), 90);
                // 2층 바닥판 아래(3.8m)에 매단 진열대 조명
                PendantLamp(racks, new Vector3(-2.9f, 3.8f, z), .9f);
            }
            Treat("BakeryInterior", "TakeawayCup", racks, new Vector3(-2.5f, .9f, -2.45f));
            WallClock(racks, new Vector3(-SideWall, 2.75f, -4.25f), 90);

            Transform window = Zone("WindowTable", space);
            // 카운터가 z=-1.7부터 시작하므로 창 쪽으로 붙인다.
            Treat("BakeryInterior", "CafeTable", window, new Vector3(-2.2f, 0, -.95f));
            Treat("BakeryInterior", "PastryStand", window, new Vector3(-2.2f, .65f, -.95f));
            Treat("BakedGoods", "ChocolateCake", window, new Vector3(-2.2f, .78f, -.95f));
            Treat("BakeryInterior", "PriceCard", window, new Vector3(-1.85f, .65f, -.65f));
            BaguetteBasket(window, new Vector3(-1.6f, 0, -.45f), 0);

            Transform kitchen = Zone("OvenUnderLanding", space);
            BakeryOven(kitchen, new Vector3(1.6f, 0, -7.38f), 0);
            BakersRack(kitchen, new Vector3(3.25f, 0, -6.95f), 0);
            FlourSack(kitchen, new Vector3(.55f, 0, -6.3f), 10);
            FlourSack(kitchen, new Vector3(.55f, .5f, -6.3f), -15);

            Transform pantry = Zone("PantryUnderStairs", space);
            Kit("BunCrate", pantry, new Vector3(3.25f, 0, -2.4f));
            Kit("BunCrate", pantry, new Vector3(3.25f, .32f, -2.4f), 15);
            Kit("WoodCrate", pantry, new Vector3(3.25f, 0, -3.3f));
            FlourSack(pantry, new Vector3(3.2f, 0, -4.2f), 80);
            FlourSack(pantry, new Vector3(3.2f, 0, -4.8f), 95);
            FlourSack(pantry, new Vector3(3.2f, .5f, -4.5f), 85);
        }

        // 빵집 2층 좌석: 탁자(기존, 앞쪽 의자·접시 포함) 아래 깔개와 뒤쪽·끝 의자, 뒷벽 장식장·그림·스탠드
        private static void BakeryLounge(Transform space)
        {
            Transform seats = Zone("Seats", space);
            Kit("RugStripesYellow", seats, new Vector3(-2.2f, 0, -3f));
            Kit("RugOvalYellow", seats, new Vector3(-2.2f, 0, -5.5f));
            foreach (float z in new[] { -3f, -5.5f })
            {
                Kit("WoodChair", seats, new Vector3(-2f, 0, z - .75f));
                Kit("WoodChair", seats, new Vector3(-.95f, 0, z), -90);
            }

            Transform back = Zone("RearCorner", space);
            Kit("DecoratedCabinet", back, new Vector3(-2.1f, 0, -7.42f));
            FramedPrint(back, new Vector3(-2.1f, 1.95f, BackWall), 0, "Landscape");
            Kit("StandingLamp", back, new Vector3(-.6f, 0, -7.3f));
            Treat("HousePlants", "Monstera", back, new Vector3(-3.35f, 0, -7.3f));
            FramedPrint(back, new Vector3(-SideWall, 1.6f, -4.25f), 90, "Botanical");
        }

        // 매장 장면에서는 계단실 칸막이 없이 건물 폭 전체를 쓴다. 문은 앞면 가운데 왼쪽(x -1.85~-0.05)이다.
        // 뒷벽과 오른쪽 벽을 따라 오락기와 의자, 앞 왼쪽 경품 진열, 오른쪽 벽 동전 교환기와 점수판, 앞 오른쪽 껌 기계, 가운데 깔개
        private static void Arcade(Transform space)
        {
            Transform games = Zone("Games", space);
            var backRow = new[]
            {
                (-2.05f, "#2F5DA8"),
                (-1.15f, "#C8463B"),
                (1.15f, "#7FAD50"),
                (2.05f, "#2F5DA8"),
            };
            foreach ((float x, string hex) in backRow)
            {
                ArcadeCabinet(games, new Vector3(x, 0, -7.4f), 0, hex);
                Kit("WoodStool", games, new Vector3(x, 0, -6.75f));
            }
            var sideRow = new[] { (-4.2f, "#C8463B"), (-5.1f, "#7FAD50"), (-6.0f, "#2F5DA8") };
            foreach ((float z, string hex) in sideRow)
            {
                ArcadeCabinet(games, new Vector3(3.38f, 0, z), -90, hex);
                Kit("WoodStool", games, new Vector3(2.75f, 0, z));
            }
            Kit("RugStripesBlue", games, new Vector3(-1.3f, 0, -3.8f), 90);
            Kit("RugStripesBlue", games, new Vector3(1.3f, 0, -3.8f), 90);
            ChangeMachine(games, new Vector3(SideWall, 1.25f, -2.9f), -90);
            TrashBin(games, new Vector3(3.45f, 0, -2.25f));
            WallClock(games, new Vector3(-1.6f, 2.6f, BackWall), 0);
            WallBoard(
                "HighScoreBoard",
                games,
                new Vector3(SideWall, 2.65f, -5.1f),
                -90,
                "HIGH SCORES",
                "ACE  9850\nMIA  8720\nLEO  7600",
                1.6f,
                .9f,
                "#293E57"
            );
            GumballMachine(games, new Vector3(1.0f, 0, -.45f), 180);
            GumballMachine(games, new Vector3(1.6f, 0, -.45f), 180);
            Treat("HousePlants", "Monstera", games, new Vector3(3.3f, 0, -.6f));
            WallBoard(
                "ArcadeBoard",
                games,
                new Vector3(-SideWall, 2.7f, -4.0f),
                90,
                "LITTLE ARCADE",
                null,
                1.8f,
                .5f,
                "#293E57"
            );

            Transform prizes = Zone("PrizeCorner", space);
            Kit("LowCabinet", prizes, new Vector3(-2.7f, 0, -.55f), 180);
            Toy("Bear", prizes, new Vector3(-3.1f, .7f, -.55f));
            Toy("Rabbit", prizes, new Vector3(-2.8f, .7f, -.5f));
            Toy("Toy", prizes, new Vector3(-2.35f, .7f, -.55f));
            PlainShelf(prizes, new Vector3(-2.7f, 1.35f, FrontWall), 180);
            PlainShelf(prizes, new Vector3(-2.7f, 1.8f, FrontWall), 180);
            for (int index = 0; index < 4; index++)
            {
                Toy(
                    index % 2 == 0 ? "Bear" : "Rabbit",
                    prizes,
                    new Vector3(-3.15f + index * .3f, 1.35f, -.34f)
                );
                Toy(
                    index % 2 == 0 ? "Rabbit" : "Bear",
                    prizes,
                    new Vector3(-3.15f + index * .3f, 1.8f, -.34f)
                );
            }
            WallBoard(
                "PrizeBoard",
                prizes,
                new Vector3(-2.7f, 2.45f, FrontWall),
                180,
                "PRIZES",
                null,
                1.6f,
                .4f,
                "#E85D75"
            );
        }

        // 오락실 2층 서점: 뒷벽 책장 셋과 안내판, 오른쪽 벽 책 선반, 앞 왼쪽 독서 자리, 탁자(기존)와 오른쪽 진열 탁자 위 책,
        // 문 옆 계산대, 뒤 오른쪽 독서 의자
        // 매장 장면에서는 계단실 칸막이 없이 건물 폭 전체를 쓴다. 문은 마을에서처럼 오른쪽 벽 앞쪽(z -1.95~-0.25)이다.
        private static void Bookshop(Transform space)
        {
            Transform shelves = Zone("Shelves", space);
            foreach (float x in new[] { -1.9f, .3f, 1.7f })
                Home("OakBookcase", shelves, new Vector3(x, 0, -7.45f), 0);
            Kit("WallShelfLongDecorated", shelves, new Vector3(SideWall, 1.3f, -3.9f), -90);
            Kit("WallShelfShortDecorated", shelves, new Vector3(SideWall, 1.95f, -5.0f), -90);
            Kit("WallShelfLongDecorated", shelves, new Vector3(SideWall, 1.3f, -6.1f), -90);
            Kit("CactusMedium", shelves, new Vector3(-3.4f, 0, -4.6f));
            FramedPrint(shelves, new Vector3(-SideWall, 1.8f, -4.6f), 90, "Landscape");
            WallBoard(
                "BookshopBoard",
                shelves,
                new Vector3(-1.9f, 2.55f, BackWall),
                0,
                "PAPER & LEAF",
                "BOOKS · MAPS · CARDS",
                2.0f,
                .55f,
                "#326B57"
            );

            Transform reading = Zone("ReadingNook", space);
            Kit("RugOvalYellow", reading, new Vector3(-2.6f, 0, -1.0f));
            Kit("Armchair", reading, new Vector3(-2.9f, 0, -.95f), 135);
            Kit("StandingLamp", reading, new Vector3(-3.4f, 0, -1.9f));
            NorthBlockAssets.Place("Nightstand", reading, new Vector3(-1.75f, 0, -.45f));
            Kit("BookSet", reading, new Vector3(-1.75f, .6f, -.45f), 20);

            Transform table = Zone("DisplayTable", space);
            Kit("BookSet", table, new Vector3(-1.1f, .75f, -4.5f), 90);
            Kit("StandingPicture", table, new Vector3(-2.15f, .75f, -4.6f));
            Kit("SquareTable", table, new Vector3(1.4f, 0, -4.4f));
            Kit("BookSet", table, new Vector3(1.25f, .7f, -4.3f), 30);
            Kit("StandingPicture", table, new Vector3(1.65f, .7f, -4.6f));

            // 문 바로 뒤 오른쪽 벽 계산대, 앞벽 화분, 뒤 오른쪽 독서 의자
            Transform counter = Zone("Counter", space);
            Kit("SmallCabinet", counter, new Vector3(3.45f, 0, -2.8f), -90);
            Treat("BakeryInterior", "CashRegister", counter, new Vector3(3.45f, .7f, -2.85f), -90);
            Treat("HousePlants", "Monstera", counter, new Vector3(.9f, 0, -.55f));

            Transform corner = Zone("ReadingCorner", space);
            Kit("RugOvalYellow", corner, new Vector3(2.6f, 0, -6.4f));
            Kit("Armchair", corner, new Vector3(3.0f, 0, -6.7f), -135);
            Kit("StandingLamp", corner, new Vector3(3.45f, 0, -7.4f));
        }

        // 생활용품점: 뒷벽 장식장·선반, 침대·소파(기존) 위 베개, 오른쪽 벽 진열장, 앞 왼쪽 진열 탁자, 깔개·스탠드·그림
        private static void HomeGoods(Transform space)
        {
            Transform back = Zone("RearDisplay", space);
            Kit("DecoratedCabinet", back, new Vector3(-1.2f, 0, -7.42f));
            Kit("WallShelfLongDecorated", back, new Vector3(-1.2f, 1.55f, BackWall));
            FramedPrint(back, new Vector3(1.6f, 1.75f, BackWall), 0, "Sunset");
            Kit("DecoratedSmallCabinet", back, new Vector3(2.9f, 0, -7.25f));
            Kit("PillowA", back, new Vector3(1.35f, .92f, -6.3f));
            Kit("PillowB", back, new Vector3(1.85f, .92f, -6.3f), 15);
            WallBoard(
                "HomeGoodsBoard",
                back,
                new Vector3(-1.2f, 2.6f, BackWall),
                0,
                "HOME & LIVING",
                "LAMPS · RUGS · DECOR",
                2.2f,
                .55f,
                "#326B57"
            );

            Transform living = Zone("LivingSet", space);
            Kit("RugOvalBlue", living, new Vector3(-1.6f, 0, -3.0f));
            Kit("PillowA", living, new Vector3(.05f, .55f, -3.7f), 10);
            Kit("PillowB", living, new Vector3(.95f, .55f, -3.7f), -10);
            Kit("StandingLamp", living, new Vector3(-3.35f, 0, -4.05f));
            FramedPrint(living, new Vector3(-SideWall, 1.7f, -4.6f), 90, "Botanical");

            Transform display = Zone("Display", space);
            Kit("LowCabinet", display, new Vector3(3.45f, 0, -3.4f), -90);
            Vase(display, new Vector3(3.45f, .7f, -3.0f), "#2F7C86");
            Kit("TableLamp", display, new Vector3(3.45f, .7f, -3.55f));
            Kit("StandingPicture", display, new Vector3(3.45f, .7f, -3.95f), -90);
            Kit("WallShelfShortDecorated", display, new Vector3(3.79f, 1.5f, -3.4f), -90);
            Kit("StandingLamp", display, new Vector3(3.35f, 0, -5.4f));
            FramedPrint(display, new Vector3(3.79f, 1.65f, -5.6f), -90, "Landscape");
            Kit("SquareTable", display, new Vector3(-1.6f, 0, -1.25f));
            Vase(display, new Vector3(-1.9f, .7f, -1.0f), "#E58B63");
            Kit("TableLamp", display, new Vector3(-1.25f, .7f, -1.5f));
            Kit("BookSet", display, new Vector3(-1.95f, .7f, -1.6f), 30);
            Kit("StandingPicture", display, new Vector3(-1.2f, .7f, -.95f));
            Treat("HousePlants", "Monstera", display, new Vector3(-3.3f, 0, -.6f));
        }

        // 생활용품점 2층 찻집: 탁자(기존) 아래 깔개와 찻주전자, 뒷벽 찻장·찻잎 단지 선반, 부스 위 조명, 벽 그림과 메뉴판
        private static void TeaRoom(Transform space)
        {
            Transform tables = Zone("Tables", space);
            foreach (float x in new[] { -2f, 1.7f })
            foreach (float z in new[] { -3f, -6f })
            {
                Kit(x < 0 ? "RugBlue" : "RugYellow", tables, new Vector3(x, 0, z));
                Teapot(tables, new Vector3(x + .35f, .75f, z), 0, x < 0 ? "#F7F3EA" : "#7FA88A");
                Treat("BakeryInterior", "Mug", tables, new Vector3(x - .4f, .75f, z + .1f));
                PendantLamp(tables, new Vector3(x, 3.08f, z), .45f);
            }

            Transform back = Zone("TeaWall", space);
            Kit("LowCabinet", back, new Vector3(-.15f, 0, -7.42f));
            Teapot(back, new Vector3(-.5f, .7f, -7.4f), 0, "#2F7C86");
            Kit("JarTall", back, new Vector3(0, .7f, -7.45f));
            Kit("JarGreen", back, new Vector3(.3f, .7f, -7.45f));
            foreach (float x in new[] { -2.4f, 2.1f })
            {
                PlainShelf(back, new Vector3(x, 1.45f, BackWall), 0);
                PlainShelf(back, new Vector3(x, 2.0f, BackWall), 0);
                string[] jars = { "JarTall", "JarRed", "JarAmber", "JarGreen" };
                for (int index = 0; index < jars.Length; index++)
                {
                    Kit(jars[index], back, new Vector3(x - .45f + index * .3f, 1.45f, -7.63f));
                    Kit(
                        jars[(index + 2) % 4],
                        back,
                        new Vector3(x - .45f + index * .3f, 2.0f, -7.63f)
                    );
                }
            }
            Treat("HousePlants", "Monstera", back, new Vector3(-3.35f, 0, -7.35f));
            Treat("HousePlants", "Monstera", back, new Vector3(3.35f, 0, -7.35f));

            Transform walls = Zone("Decor", space);
            FramedPrint(walls, new Vector3(3.79f, 1.6f, -3.0f), -90, "Botanical");
            Kit("PictureSmallB", walls, new Vector3(3.79f, 1.6f, -6.0f), -90);
            WallBoard(
                "TeaMenuBoard",
                walls,
                new Vector3(-SideWall, 1.7f, -4.5f),
                90,
                "TEA MENU",
                "EARL GREY · GREEN\nCHAMOMILE · CHAI",
                1.2f,
                .8f,
                "#326B57"
            );
        }

        // ---------- 공통 ----------

        // 공간 안의 구역 묶음. 나중에 구역째 옮기거나 끌 수 있게 한다.
        // 이름이 Wall·Stair·Roof 등으로 시작하면 단어 도구가 그 단어를 자동으로 붙이므로 피한다(ObjectWordRegistration.FindKey).
        private static Transform Zone(string name, Transform space) =>
            MainstreetArtKit.Group(name, space, Vector3.zero);

        private static Transform Kit(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        ) => TownShopInteriorProps.Place(name, parent, position, yaw);

        private static void Treat(
            string category,
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        ) =>
            Instantiate(
                TinyTreatsPropsBuilder.Prefabs + category + "/" + name + ".prefab",
                parent,
                position,
                yaw
            );

        // 경품 진열대에 올린 인형. 선반 위 소품이라 이동 충돌을 뺀다.
        private static void Toy(string name, Transform parent, Vector3 position) =>
            TownShopInteriorProps.RemoveMovementColliders(
                NorthBlockAssets.Place(name, parent, position, 180).gameObject
            );

        private static void Home(string name, Transform parent, Vector3 position, float yaw) =>
            Instantiate(
                "Assets/Prefabs/Home/CozyCottage/" + name + ".prefab",
                parent,
                position,
                yaw
            );

        // 바닥이 아닌 가구 위에 올린 재사용 프리팹(케이크 받침·컵·계산기)은 이동 충돌을 뺀다. 상자가 겉모양보다 떠 보인다.
        private static void Instantiate(string path, Transform parent, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("재사용할 프리팹이 없습니다: " + path);
            GameObject placed = BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
            if (position.y > 0)
                TownShopInteriorProps.RemoveMovementColliders(placed);
        }
    }
}
