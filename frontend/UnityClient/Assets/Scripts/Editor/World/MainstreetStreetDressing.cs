using A305.World;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    internal static class MainstreetStreetDressing
    {
        internal static void Apply(Transform environment, TownBlockoutSettings settings)
        {
            foreach (Transform child in environment.Find("BuildingFootprints"))
                if (child.name == "BackgroundShell")
                    child.gameObject.SetActive(false);
            environment.Find("FutureBuildingSlots").gameObject.SetActive(false);
            foreach (
                Renderer renderer in environment
                    .Find("MovableSandboxBoundary")
                    .GetComponentsInChildren<Renderer>()
            )
                renderer.enabled = false;
            Material road = MainstreetReferenceKit.Surface("RoadBlueV2", "#667FA7", false);
            foreach (Transform part in environment.Find("StreetModules"))
                if (part.name.StartsWith("Road_") || part.name == "CrossRoadWing")
                    part.GetComponent<Renderer>().sharedMaterial = road;

            Transform dressing = Group("StreetDepthAndGardens", environment, Vector3.zero);
            Block(
                "DistantGround",
                dressing,
                new Vector3(0, -.22f, 30),
                new Vector3(180, .05f, 180),
                Color("DistantGrassV2", "#9DBB6C")
            );
            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < 5; index++)
                {
                    float z = 17f + index * 8.5f;
                    if (z + 6f > 30f + settings.NorthExtensionMeters)
                        break;
                    int style = (index + (side > 0 ? 1 : 0)) % 3;
                    Place(
                        "DistantShopV2_" + style,
                        dressing,
                        new Vector3(side * 7f, .1f, z + 3),
                        () => DistantShop(style),
                        side < 0 ? 90 : 270
                    );
                    if (index % 2 == 0)
                        Place(
                            "GardenTreeV2",
                            dressing,
                            new Vector3(side * 14f, .1f, z + 4),
                            MainstreetReferenceKit.GardenTree
                        );
                }
            }
            foreach (
                Vector3 position in new[]
                {
                    new Vector3(7.7f, .1f, -16),
                    new Vector3(7.7f, .1f, -14.3f),
                    new Vector3(12.8f, .1f, -10.1f),
                    new Vector3(12.8f, .1f, -8.6f),
                }
            )
                Place("GardenBedV2", dressing, position, GardenBed);
            Place(
                "GardenTreeV2",
                dressing,
                new Vector3(12.3f, .1f, -12.3f),
                MainstreetReferenceKit.GardenTree
            );
            Place(
                "GardenTreeV2",
                dressing,
                new Vector3(-6.4f, .1f, -15.5f),
                MainstreetReferenceKit.GardenTree
            );
            for (int index = 0; index < 7; index++)
            {
                Transform grove = Place(
                    "GardenTreeV2",
                    dressing,
                    new Vector3(-20 + index * 7, .1f, 76),
                    MainstreetReferenceKit.GardenTree
                );
                grove.localScale = Vector3.one * (1.7f + (index % 3) * .3f);
            }
        }

        private static GameObject DistantShop(int style)
        {
            var root = new GameObject("DistantShop");
            Transform p = root.transform;
            float height = new[] { 6.5f, 7.3f, 5.8f }[style];
            Material wall =
                style == 0 ? MainstreetReferenceKit.Brick
                : style == 1 ? Color("DistantOchre", "#E4BF77")
                : Color("DistantSage", "#A9C6B3");
            Block(
                "Walls",
                p,
                new Vector3(0, height / 2, -2.6f),
                new Vector3(6, height, 5.2f),
                wall,
                .06f
            );
            Block(
                "RoofCap",
                p,
                new Vector3(0, height, -2.6f),
                new Vector3(6.2f, .26f, 5.4f),
                Cream,
                .05f
            );
            Block("Band", p, new Vector3(0, 3.2f, .08f), new Vector3(6.1f, .18f, .26f), Cream);
            foreach (float x in new[] { -1.6f, 1.6f })
            {
                GameObject upper = MainstreetReferenceKit.TallWindow();
                upper.transform.SetParent(p, false);
                upper.transform.localPosition = new Vector3(x, height - 1.7f, .1f);
                GameObject lower = MainstreetReferenceKit.WarmShopWindow();
                lower.transform.SetParent(p, false);
                lower.transform.localPosition = new Vector3(x, 1.5f, .1f);
            }
            Block(
                "Door",
                p,
                new Vector3(0, 1.15f, .08f),
                new Vector3(1.15f, 2.3f, .18f),
                MainstreetReferenceKit.Frame
            );
            Block(
                "DoorGlass",
                p,
                new Vector3(0, 1.4f, .2f),
                new Vector3(.82f, 1.45f, .04f),
                MainstreetReferenceKit.Glow
            );
            Material awning = style == 1 ? Color("DistantAwningBlue", "#526C91") : Green;
            Transform roof = Block(
                "Awning",
                p,
                new Vector3(0, 2.9f, .55f),
                new Vector3(5.5f, .16f, 1.25f),
                awning,
                .05f
            );
            roof.localEulerAngles = new Vector3(25, 0, 0);
            Block(
                "Valance",
                p,
                new Vector3(0, 2.55f, 1.1f),
                new Vector3(5.5f, .24f, .16f),
                awning,
                .07f
            );
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, height / 2, -2.6f);
            collider.size = new Vector3(6, height, 5.2f);
            return root;
        }

        private static GameObject GardenBed()
        {
            var root = new GameObject("GardenBed");
            Transform p = root.transform;
            Block("Pot", p, new Vector3(0, .3f, 0), new Vector3(.95f, .6f, 1.5f), Cream, .05f);
            Block(
                "Soil",
                p,
                new Vector3(0, .61f, 0),
                new Vector3(.8f, .04f, 1.35f),
                Color("Soil", "#776951")
            );
            for (int index = 0; index < 7; index++)
            {
                float z = -.54f + index * .18f;
                float x = Mathf.Sin(index * 2.4f) * .2f;
                SmoothBall(
                    "Shrub",
                    p,
                    new Vector3(x, .78f, z),
                    new Vector3(.65f, .5f, .6f),
                    MainstreetReferenceKit.Foliage
                );
                Vector3 centre = new Vector3(x + .22f, .95f, z);
                for (int petal = 0; petal < 5; petal++)
                {
                    float angle = petal * Mathf.PI * 2 / 5;
                    Ball(
                        "Petal",
                        p,
                        centre + new Vector3(Mathf.Cos(angle) * .08f, Mathf.Sin(angle) * .08f, .1f),
                        Vector3.one * .12f,
                        Cream
                    );
                }
                Ball(
                    "Pollen",
                    p,
                    centre + Vector3.forward * .15f,
                    Vector3.one * .08f,
                    Color("Gold", "#E0B456")
                );
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .3f, 0);
            collider.size = new Vector3(.95f, .6f, 1.5f);
            return root;
        }
    }
}
