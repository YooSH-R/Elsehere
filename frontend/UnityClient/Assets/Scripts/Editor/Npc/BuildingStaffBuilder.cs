using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 마을의 들어갈 수 있는 건물 9곳(1층 매장 6곳·북쪽 블록 3곳)에 직원 NPC를 한 명씩 두고, 생활하며 자기 건물 1층 안에서만 지내게 한다.
    /// 외형은 리틀 멍 서비스 NPC 외형을 돌려 쓰고, 머리·옷·눈동자 색만 NPC마다 바꾼 재질 사본을 입힌다.
    /// 같은 NPC가 이미 있으면 위치는 그대로 두고 연결·색·구역만 맞춘다. 색 텍스처는 없을 때만 만들고, 재질 사본은 실행할 때마다 외형의 원래 재질 설정(그림자 받기 등)에 맞춘다.
    /// </summary>
    public static class BuildingStaffBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Build Building Staff";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string SettingsPath = "Assets/Data/Npc/BuildingStaffSettings.asset";
        private const string ColorFolder = "Assets/Art/Characters/BuildingStaff";
        private const string LookFolder = "Assets/Prefabs/Npc/Review/LittleMung_";

        // 몸 색 사본은 원본(2048)의 절반으로 저장한다. 화면에서 작게 보이는 NPC라 차이가 없고 저장소를 덜 차지한다.
        private const int BodyTextureSize = 1024;

        // 가구와 겹치지 않는 자리를 찾을 때 쓰는 NPC 몸(반지름·키)과 한 번에 옮겨 보는 거리
        private const float BodyRadiusMeters = .32f;
        private const float BodyHeightMeters = 1.6f;
        private const float SearchStepMeters = .3f;
        private const int SearchRings = 6;

        // 건물 기준 좌표: 앞면(문) z=0, 뒷벽 z=-7.8, 벽 x=±3.8, 1층 바닥 y=0, 2층 바닥 y=4. 1층 실내만 감싼다.
        private static readonly Bounds GroundFloor = new Bounds(
            new Vector3(0, 1.75f, -3.9f),
            new Vector3(7.6f, 3.3f, 7.6f)
        );

        // ---------- 외형별로 바꿀 수 있는 색 범위(색상·채도·명도) ----------
        // 중년 여성·중년 남성은 머리와 피부가 같은 색 범위라 머리는 그대로 두고 옷만 바꾼다.

        private static readonly ColorRange FemaleClothes = new ColorRange(
            (.55f, .72f),
            (.2f, 1f),
            (.15f, 1f)
        );
        private static readonly ColorRange GrandmotherGrey = new ColorRange(
            (0f, 1f),
            (0f, .22f),
            (.3f, .9f)
        );
        private static readonly ColorRange GrandmotherPlum = new ColorRange(
            (.88f, .02f),
            (.25f, .7f),
            (.3f, .75f)
        );
        private static readonly ColorRange MaleShirt = new ColorRange(
            (.45f, .6f),
            (.2f, 1f),
            (.2f, 1f)
        );
        private static readonly ColorRange MaleJacket = new ColorRange(
            (0f, .08f),
            (.35f, .65f),
            (.15f, .4f)
        );
        private static readonly ColorRange BusinessBrown = new ColorRange(
            (0f, .09f),
            (.3f, .7f),
            (.1f, .42f)
        );
        private static readonly ColorRange BusinessNavy = new ColorRange(
            (.58f, .7f),
            (.25f, .7f),
            (.15f, .5f)
        );
        private static readonly ColorRange YoungHair = new ColorRange(
            (0f, .1f),
            (.25f, .8f),
            (.04f, .45f)
        );
        private static readonly ColorRange YoungBlue = new ColorRange(
            (.58f, .7f),
            (.35f, 1f),
            (.25f, 1f)
        );
        private static readonly ColorRange YoungYellow = new ColorRange(
            (.08f, .15f),
            (.6f, 1f),
            (.6f, 1f)
        );

        // 눈 텍스처의 눈동자. 흰자(채도 낮고 밝음)와 동공(아주 어두움)은 남기고, 어두운 눈동자는 조금 밝혀 색이 보이게 한다.
        private static readonly ColorRange IrisColor = new ColorRange(
            (0f, 1f),
            (.12f, 1f),
            (.12f, .95f)
        );
        private static readonly ColorRange IrisDark = new ColorRange(
            (0f, 1f),
            (0f, .12f),
            (.08f, .35f)
        );

        private static readonly Staff[] Staffs =
        {
            new Staff(
                "POST_OFFICE_STAFF",
                "PostOfficeStaff",
                "PostOffice",
                new Vector3(-1.5f, 0, -4.5f),
                "MiddleAge_Female",
                (.07f, .7f),
                FemaleClothes.To(.97f, .55f)
            ),
            new Staff(
                "DINER_STAFF",
                "DinerStaff",
                "MapleDiner",
                new Vector3(.4f, 0, -6.4f),
                "MiddleAge_Female",
                (.33f, .55f),
                FemaleClothes.To(0f, .7f)
            ),
            new Staff(
                "BANK_STAFF",
                "BankStaff",
                "TownBank",
                new Vector3(2.5f, 0, -4.6f),
                "Business_Male",
                (.58f, .3f),
                BusinessBrown.To(.6f, .05f, 1.4f),
                BusinessNavy.To(.62f, .2f)
            ),
            new Staff(
                "LAUNDROMAT_STAFF",
                "LaundromatStaff",
                "SudsLaundromat",
                new Vector3(.2f, 0, -5f),
                "MiddleAge_Male",
                (.08f, .6f),
                MaleShirt.To(.12f, .55f),
                MaleJacket.To(.2f, .45f, 1.2f)
            ),
            new Staff(
                "FLOWER_SHOP_STAFF",
                "FlowerShopStaff",
                "BloomFlowers",
                new Vector3(.9f, 0, -6.4f),
                "Grandmother",
                (.48f, .5f),
                GrandmotherGrey.To(.78f, .18f, 1.05f),
                GrandmotherPlum.To(.33f, .45f)
            ),
            new Staff(
                "BARBER_STAFF",
                "BarberStaff",
                "MainStreetBarber",
                new Vector3(-1.6f, 0, -4.1f),
                "MiddleAge_Male",
                (.06f, .6f),
                MaleShirt.To(0f, .03f, 1.9f),
                MaleJacket.To(0f, 0f, .6f)
            ),
            new Staff(
                "BAKERY_STAFF",
                "BakeryStaff",
                "MapleBakery",
                new Vector3(-1.6f, 0, -4.4f),
                "Grandmother",
                (.6f, .55f),
                GrandmotherGrey.To(.1f, .05f, 1.25f),
                GrandmotherPlum.To(.02f, .45f)
            ),
            new Staff(
                "ARCADE_STAFF",
                "ArcadeStaff",
                "LittleArcade",
                new Vector3(-1.4f, 0, -4.6f),
                "BlackYoung_Male",
                (.11f, .8f),
                YoungHair.To(.01f, .6f, 1.3f),
                YoungBlue.To(.78f, .55f),
                YoungYellow.To(.25f, .65f)
            ),
            new Staff(
                "HOME_STORE_STAFF",
                "HomeStoreStaff",
                "HomeLiving",
                new Vector3(-.3f, 0, -6.6f),
                "MiddleAge_Female",
                (.1f, .6f),
                FemaleClothes.To(.28f, .35f)
            ),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PlaceStaff();
            // 생활 데이터가 있는 NPC에 생활을 붙이고 바닥을 다시 굽는다. 그 뒤 직원마다 구역을 정한다.
            NpcLifeBuilder.Build();
            LimitStaffToBuildings();
            Debug.Log("마을 건물 9곳에 직원 NPC를 두고 건물 안에서만 지내게 했습니다.");
        }

        /// <summary>직원을 건물 안에 두고 외형·색·대화를 잇는다. 생활(구역)은 <see cref="Build"/>가 이어서 맞춘다.</summary>
        public static void PlaceStaff()
        {
            BuilderTools.ThrowIfPlaying();
            NpcBuilder.PrepareMockLines();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            foreach (Staff staff in Staffs)
            {
                Place(town, staff);
            }
            NpcBuilder.ConnectToScene(
                BuilderTools.EnsureGroups(town),
                BuilderTools.FindSingleInScene<PlayerMover>(town),
                BuilderTools.FindSingleInScene<DialogueController>(town)
            );
            BuilderTools.SaveScene(town, TownScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>생활이 붙은 직원마다 자기 건물 1층 구역 안에서만 사물·자리를 고르게 한다.</summary>
        public static void LimitStaffToBuildings()
        {
            BuilderTools.ThrowIfPlaying();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            foreach (Staff staff in Staffs)
            {
                LimitToBuilding(town, staff);
            }
            BuilderTools.SaveScene(town, TownScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void Place(Scene scene, Staff staff)
        {
            Transform building = Building(scene, staff.Building);
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(candidate => candidate.Code == staff.Code);
            if (npc == null)
            {
                GameObject placed = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        NpcBuilder.PasserbyPrefabPath,
                        MenuPath
                    ),
                    BuilderTools.EnsureGroups(scene).Characters,
                    FreeSpot(building, staff.Stand),
                    building.rotation
                );
                placed.name = staff.ObjectName;
                npc = placed.GetComponent<NpcIdentity>();
                BuilderTools.SetText(npc, "code", staff.Code);
            }

            Passerby passerby = npc.GetComponent<Passerby>();
            BuilderTools.SetReference(passerby, "settings", Settings());
            BuilderTools.SetReference(
                passerby,
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(
                    NpcBuilder.MockLinesPath,
                    MenuPath
                )
            );
            NpcVisualBuilder.Connect(npc, staff.LookPrefab, true);
            Dress(npc, staff);
        }

        // 카운터·가구와 겹치지 않는 가장 가까운 자리. 정한 자리에서 링 모양으로 넓혀 가며 1층 실내 안에서만 찾는다.
        private static Vector3 FreeSpot(Transform building, Vector3 stand)
        {
            Physics.SyncTransforms();
            for (int ring = 0; ring <= SearchRings; ring++)
            {
                int steps = ring == 0 ? 1 : ring * 8;
                for (int step = 0; step < steps; step++)
                {
                    float angle = step * Mathf.PI * 2f / steps;
                    Vector3 local =
                        stand
                        + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle))
                            * (ring * SearchStepMeters);
                    if (!GroundFloor.Contains(local + Vector3.up))
                    {
                        continue;
                    }

                    Vector3 world = building.TransformPoint(local);
                    bool blocked = Physics.CheckCapsule(
                        world + Vector3.up * (BodyRadiusMeters + .1f),
                        world + Vector3.up * (BodyHeightMeters - BodyRadiusMeters),
                        BodyRadiusMeters,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    );
                    if (!blocked)
                    {
                        return world + Vector3.up * .05f;
                    }
                }
            }
            throw new System.InvalidOperationException(
                "건물 안에 직원이 설 빈자리가 없습니다: " + building.name
            );
        }

        private static void LimitToBuilding(Scene scene, Staff staff)
        {
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(candidate => candidate.Code == staff.Code);
            var routine = npc.GetComponent<NpcRoutine>();
            if (routine == null)
            {
                throw new System.InvalidOperationException(
                    staff.Code
                        + "에 생활(NpcRoutine)이 없습니다. ai/tools/npc_life_export.py로 생활 데이터(Data/Npc/NpcLife.json)를 먼저 내보내세요."
                );
            }

            var fields = new SerializedObject(routine);
            fields.FindProperty("stayInArea").boolValue = true;
            fields.FindProperty("area").boundsValue = WorldBounds(
                Building(scene, staff.Building),
                GroundFloor
            );
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 건물은 90도 단위로 돌아 있어 모서리 8개를 감싼 축 정렬 상자가 실내와 같다.
        private static Bounds WorldBounds(Transform building, Bounds local)
        {
            var world = new Bounds(building.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                world.Encapsulate(
                    building.TransformPoint(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))
                    )
                );
            }
            return world;
        }

        // 카페 직원처럼 손님에게 걸어가지 않고, 가까이 오면 대화를 제안한다.
        private static PasserbySettings Settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PasserbySettings>(SettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<PasserbySettings>();
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = 3.5f;
            fields.FindProperty("offerDistanceMeters").floatValue = 2.5f;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
            fields.FindProperty("restSeconds").floatValue = 20;
            fields.FindProperty("canDecline").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        // ---------- 색 ----------

        private static void Dress(NpcIdentity npc, Staff staff)
        {
            foreach (Renderer renderer in npc.GetComponentsInChildren<Renderer>(true))
            {
                // 이미 사본을 입혔어도 외형 프리팹의 원래 재질에서 다시 맞춰, 원래 재질의 설정이 바뀌면 사본도 따라가게 한다.
                Renderer original = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] sources = (original != null ? original : renderer).sharedMaterials;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length && index < sources.Length; index++)
                {
                    Material variant = Variant(staff, sources[index]);
                    if (variant != null && variant != materials[index])
                    {
                        materials[index] = variant;
                        changed = true;
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
        }

        // 몸 색 텍스처와 눈 텍스처를 쓰는 재질만 NPC 색 사본으로 바꾼다. 입·이·눈꺼풀 재질은 그대로 둔다.
        private static Material Variant(Staff staff, Material source)
        {
            if (source == null)
                return null;
            string folder = ColorFolder + "/" + staff.Code;
            if (AssetDatabase.GetAssetPath(source).StartsWith(folder + "/"))
                return source;
            Texture texture = source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap")
                : source.mainTexture;
            if (texture == null)
                return null;

            bool body = texture.name == "Material_0_color" || texture.name == "CharacterColor";
            bool eye = texture.name.EndsWith("EyeColor");
            if (!body && !eye)
                return null;

            string name = (body ? "Body_" : "Eye_") + texture.name;
            string materialPath = folder + "/" + name + ".mat";
            BuilderTools.EnsureFolder(folder);
            string texturePath = folder + "/" + name + ".png";
            if (!File.Exists(texturePath))
            {
                WriteRecolored(
                    AssetDatabase.GetAssetPath(texture),
                    texturePath,
                    body ? staff.Body : staff.Eye,
                    body ? BodyTextureSize : 0
                );
            }

            // 텍스처만 색 사본이고 셰이더·그림자 받기 같은 설정은 원래 재질을 따른다. 이미 있는 사본도 다시 맞춘다.
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            var recolored = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", recolored);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", recolored);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteRecolored(
            string sourcePath,
            string targetPath,
            IReadOnlyList<NpcColorSwap> swaps,
            int size
        )
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(sourcePath));
            if (size > 0 && texture.width > size)
            {
                Texture2D smaller = Downscale(texture, size);
                Object.DestroyImmediate(texture);
                texture = smaller;
            }
            texture.SetPixels32(NpcColorSwap.Recolor(texture.GetPixels32(), swaps));
            File.WriteAllBytes(targetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(targetPath);
        }

        private static Texture2D Downscale(Texture2D source, int size)
        {
            RenderTexture previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var smaller = new Texture2D(size, size, TextureFormat.RGBA32, false);
            smaller.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            smaller.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return smaller;
        }

        private static Transform Building(Scene scene, string name)
        {
            Transform found = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == name);
            if (found == null)
            {
                throw new System.InvalidOperationException("마을 씬에 건물이 없습니다: " + name);
            }
            return found;
        }

        /// <summary>바꿀 색 범위. <see cref="To"/>로 바꿀 색을 정해 <see cref="NpcColorSwap"/>을 만든다.</summary>
        private readonly struct ColorRange
        {
            private readonly Vector2 hue;
            private readonly Vector2 saturation;
            private readonly Vector2 value;

            public ColorRange(
                (float Min, float Max) hue,
                (float Min, float Max) saturation,
                (float Min, float Max) value
            )
            {
                this.hue = new Vector2(hue.Min, hue.Max);
                this.saturation = new Vector2(saturation.Min, saturation.Max);
                this.value = new Vector2(value.Min, value.Max);
            }

            public NpcColorSwap To(float targetHue, float targetSaturation, float valueScale = 1f)
            {
                return new NpcColorSwap(
                    hue,
                    saturation,
                    value,
                    targetHue,
                    targetSaturation,
                    valueScale
                );
            }
        }

        private sealed class Staff
        {
            public Staff(
                string code,
                string objectName,
                string building,
                Vector3 stand,
                string look,
                (float Hue, float Saturation) eye,
                params NpcColorSwap[] body
            )
            {
                Code = code;
                ObjectName = objectName;
                Building = building;
                Stand = stand;
                LookPrefab = LookFolder + look + ".prefab";
                Body = body;
                Eye = new[]
                {
                    IrisColor.To(eye.Hue, eye.Saturation),
                    IrisDark.To(eye.Hue, eye.Saturation, 1.6f),
                };
            }

            public string Code { get; }
            public string ObjectName { get; }
            public string Building { get; }

            /// <summary>건물 기준 서 있을 자리. 가구와 겹치면 가까운 빈자리로 옮긴다.</summary>
            public Vector3 Stand { get; }

            public string LookPrefab { get; }
            public IReadOnlyList<NpcColorSwap> Body { get; }
            public IReadOnlyList<NpcColorSwap> Eye { get; }
        }
    }
}
