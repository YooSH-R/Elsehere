using System;
using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.ObjectHover;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>각 횡단보도 양끝에서 반대편을 볼 수 있는 사람 모양 신호를 추가한다.</summary>
    public static class PedestrianSignalBuilder
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string GroupName = "PedestrianSignals";

        [MenuItem("Tools/A305/World/Apply Pedestrian Signals")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var lights = BuilderTools.FindSingleInScene<TrafficLights>(scene);
            Transform parent = lights.transform.parent;
            Transform group = parent.Find(GroupName);
            if (group == null)
                group = Group(GroupName, parent, Vector3.zero);

            Corner(
                group,
                lights,
                "SouthWest",
                new Vector3(-5.7f, .1f, 6.55f),
                "MainSouthA",
                "CrossWestA",
                Vector3.right,
                Vector3.forward,
                6.85f,
                -4.1f
            );
            Corner(
                group,
                lights,
                "SouthEast",
                new Vector3(5.7f, .1f, 6.55f),
                "MainSouthB",
                "CrossEastA",
                Vector3.left,
                Vector3.forward,
                6.85f,
                4.1f
            );
            Corner(
                group,
                lights,
                "NorthWest",
                new Vector3(-5.7f, .1f, 13.45f),
                "MainNorthA",
                "CrossWestB",
                Vector3.right,
                Vector3.back,
                13.15f,
                -4.1f
            );
            Corner(
                group,
                lights,
                "NorthEast",
                new Vector3(5.7f, .1f, 13.45f),
                "MainNorthB",
                "CrossEastB",
                Vector3.left,
                Vector3.back,
                13.15f,
                4.1f
            );
            Head(
                group,
                "CafeA",
                new Vector3(0, .1f, -11),
                new Vector3(-5.7f, .1f, -13.4f),
                Vector3.right,
                true,
                ownPole: true
            );
            Head(
                group,
                "CafeB",
                new Vector3(0, .1f, -11),
                new Vector3(5.7f, .1f, -13.4f),
                Vector3.left,
                true,
                ownPole: true
            );

            ObjectWordDefinition parkingWord = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                "Assets/Data/ObjectHover/Words/parking_meter.asset"
            );
            foreach (HoverableObject prop in parent.GetComponentsInChildren<HoverableObject>(true))
                if (prop.Word == parkingWord && parkingWord != null)
                    Object.DestroyImmediate(prop.gameObject);

            var fields = new SerializedObject(lights);
            SerializedProperty heads = fields.FindProperty("pedestrianHeads");
            PedestrianSignalHead[] found = group.GetComponentsInChildren<PedestrianSignalHead>();
            heads.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++)
                heads.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            NpcLifeBuilder.RebakeWalkArea(scene);
            BuilderTools.SaveScene(scene, ScenePath);
            // 제거 요청을 받은 소품과 그 전용 메시. 다른 씬·프리팹에서 사용하지 않음을 확인한 경로다.
            AssetDatabase.DeleteAsset("Assets/Prefabs/World/MainstreetArt/ParkingMeter.prefab");
            foreach (string material in new[] { "MeterWindow", "MeterSteel", "MeterPost" })
                AssetDatabase.DeleteAsset($"{ArtFolder}/Meshes/ParkingMeter_{material}.asset");
            AssetDatabase.SaveAssets();
            Debug.Log($"보행자 신호 {found.Length}개와 보행 데이터를 갱신했습니다.");
        }

        private static void Corner(
            Transform group,
            TrafficLights lights,
            string name,
            Vector3 position,
            string mainName,
            string crossName,
            Vector3 mainFacing,
            Vector3 crossFacing,
            float mainCrossingZ,
            float crossCrossingX
        )
        {
            bool vehiclePole = false;
            foreach (TrafficSignalHead head in lights.Heads)
                if (head != null && Vector3.Distance(head.transform.position, position) < .2f)
                    vehiclePole = true;
            if (!vehiclePole && group.Find(name + "Pole") == null)
            {
                Transform pole = Group(name + "Pole", group, position);
                AddPole(pole);
                BindWord(pole.gameObject);
            }
            Head(
                group,
                mainName,
                new Vector3(0, .1f, mainCrossingZ),
                position + mainFacing * .42f,
                mainFacing,
                true,
                ownPole: false
            );
            Head(
                group,
                crossName,
                new Vector3(crossCrossingX, .1f, 10),
                position + crossFacing * .42f,
                crossFacing,
                false,
                ownPole: false
            );
        }

        private static void Head(
            Transform parent,
            string name,
            Vector3 crossing,
            Vector3 position,
            Vector3 facing,
            bool mainRoad,
            bool ownPole
        )
        {
            // 사용자가 요청한 기둥 공유 배치를 갱신하되 기존 신호판 디자인은 유지한다.
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.SetLocalPositionAndRotation(position, Quaternion.LookRotation(facing));
                if (!ownPole)
                {
                    Transform oldPole = existing.Find("Pole");
                    if (oldPole != null)
                        Object.DestroyImmediate(oldPole.gameObject);
                    var oldCollider = existing.GetComponent<CapsuleCollider>();
                    if (oldCollider != null)
                        Object.DestroyImmediate(oldCollider);
                }
                SetHoverBox(existing.gameObject);
                var existingFields = new SerializedObject(
                    existing.GetComponent<PedestrianSignalHead>()
                );
                foreach (string person in new[] { "StandingPerson", "WalkingPerson" })
                {
                    Transform figure = existing.Find(person);
                    if (figure.GetComponentsInChildren<Renderer>().Length > 1)
                        CombineParts(figure.gameObject, "Pedestrian" + person);
                    SetParts(
                        existingFields.FindProperty(
                            person == "StandingPerson" ? "standingPerson" : "walkingPerson"
                        ),
                        figure
                    );
                }
                existingFields.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            Transform root = Group(name, parent, position);
            root.rotation = Quaternion.LookRotation(facing);
            Material dark = Color("PedestrianHousing", "#202A2D");
            Material off = Color("PedestrianUnlit", "#35413E");
            Material red = Glow("PedestrianRed", "#FF4D43");
            Material green = Glow("PedestrianGreen", "#72FFAF");
            if (ownPole)
                AddPole(root);
            Block("Housing", root, new Vector3(0, 2.05f, 0), new Vector3(.58f, 1.3f, .2f), dark);
            Block(
                "StopCell",
                root,
                new Vector3(0, 2.35f, .11f),
                new Vector3(.48f, .55f, .025f),
                Color("PedestrianCell", "#0B1213")
            );
            Block(
                "WalkCell",
                root,
                new Vector3(0, 1.75f, .11f),
                new Vector3(.48f, .55f, .025f),
                Color("PedestrianCell", "#0B1213")
            );
            Transform standing = Person(root, "StandingPerson", 2.35f, false, red);
            Transform walking = Person(root, "WalkingPerson", 1.75f, true, green);
            BindWord(root.gameObject);
            SetHoverBox(root.gameObject);
            var head = root.gameObject.AddComponent<PedestrianSignalHead>();
            var fields = new SerializedObject(head);
            fields.FindProperty("crossesMainRoad").boolValue = mainRoad;
            fields.FindProperty("crossingPoint").vector3Value = crossing;
            SetParts(fields.FindProperty("standingPerson"), standing);
            SetParts(fields.FindProperty("walkingPerson"), walking);
            fields.FindProperty("redOn").objectReferenceValue = red;
            fields.FindProperty("greenOn").objectReferenceValue = green;
            fields.FindProperty("off").objectReferenceValue = off;
            fields.ApplyModifiedPropertiesWithoutUndo();
            head.Show(PedestrianLight.Stop, 0);
        }

        private static void AddPole(Transform root)
        {
            Post(
                "Pole",
                root,
                new Vector3(0, 1.3f, 0),
                .065f,
                2.6f,
                Color("PedestrianHousing", "#202A2D")
            );
            var collider = root.gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 1.3f, 0);
            collider.height = 2.6f;
            collider.radius = .065f;
        }

        private static void BindWord(GameObject root)
        {
            BuilderTools.SetReference(
                root.AddComponent<HoverableObject>(),
                "word",
                AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                    "Assets/Data/ObjectHover/Words/traffic_light.asset"
                )
            );
        }

        // 호버 범위는 물리 이동이나 경로를 막는 장애물로 사용하지 않는다.
        private static void SetHoverBox(GameObject root)
        {
            var box = BuilderTools.GetOrAddComponent<BoxCollider>(root);
            box.center = new Vector3(0, 2.05f, 0);
            box.size = new Vector3(.58f, 1.3f, .2f);
            box.isTrigger = true;
        }

        private static Transform Person(
            Transform parent,
            string name,
            float y,
            bool walking,
            Material material
        )
        {
            Transform root = Group(name, parent, new Vector3(0, y, .16f));
            Ball("Head", root, new Vector3(0, .16f, 0), Vector3.one * .095f, material);
            Segment(root, "Body", new Vector2(0, .085f), new Vector2(0, -.06f), .055f, material);
            Segment(
                root,
                "LeftArm",
                new Vector2(-.025f, .065f),
                new Vector2(-.14f, walking ? .11f : -.055f),
                .036f,
                material
            );
            Segment(
                root,
                "RightArm",
                new Vector2(.025f, .065f),
                new Vector2(.14f, walking ? -.04f : -.055f),
                .036f,
                material
            );
            Segment(
                root,
                "LeftLeg",
                new Vector2(-.015f, -.05f),
                new Vector2(walking ? -.145f : -.055f, -.21f),
                .045f,
                material
            );
            Segment(
                root,
                "RightLeg",
                new Vector2(.015f, -.05f),
                new Vector2(walking ? .145f : .055f, -.21f),
                .045f,
                material
            );
            CombineParts(root.gameObject, "Pedestrian" + name);
            return root;
        }

        private static void Segment(
            Transform parent,
            string name,
            Vector2 from,
            Vector2 to,
            float width,
            Material material
        )
        {
            Vector3 start = new Vector3(from.x, from.y, 0);
            Vector3 end = new Vector3(to.x, to.y, 0);
            Transform part = Post(
                name,
                parent,
                (start + end) / 2,
                width / 2,
                Vector3.Distance(start, end),
                material
            );
            part.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        private static void SetParts(SerializedProperty field, Transform parent)
        {
            Renderer[] parts = parent.GetComponentsInChildren<Renderer>();
            field.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
                field.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        }

        private static Material Glow(string name, string hex)
        {
            Material material = Color(name, hex);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", material.color * 1.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/A305/World/Capture Pedestrian Signals")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);
            foreach (Camera previous in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                previous.enabled = false;
            var camera = new GameObject("PedestrianReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 50;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 150;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new UnityEngine.Color(.7f, .8f, .85f);
            var cycle = new TrafficSignalCycle(12, TrafficLights.CrossGreenSeconds, 3);
            PreviewPhase(cycle, 15);
            Save(camera, "intersection", new Vector3(0, 8, -3), new Vector3(0, .7f, 10));
            Save(
                camera,
                "from-west",
                new Vector3(-4.1f, 2.2f, 6.85f),
                new Vector3(5.28f, 2.15f, 6.55f)
            );
            Save(
                camera,
                "from-east",
                new Vector3(4.1f, 2.2f, 6.85f),
                new Vector3(-5.28f, 2.15f, 6.55f)
            );
            Transform closeup = Object
                .FindFirstObjectByType<TrafficLights>()
                .transform.parent.Find(GroupName + "/MainSouthB");
            Save(
                camera,
                "walking-close",
                closeup.position + closeup.forward * 2.2f + Vector3.up * 2.05f,
                closeup.position + Vector3.up * 2.05f
            );
            PreviewPhase(cycle, 0);
            Save(
                camera,
                "from-south",
                new Vector3(4.1f, 2.2f, 6.2f),
                new Vector3(5.7f, 2.15f, 13.03f)
            );
            Save(
                camera,
                "from-north",
                new Vector3(4.1f, 2.2f, 13.8f),
                new Vector3(5.7f, 2.15f, 6.97f)
            );
            Save(camera, "stop", new Vector3(-4.1f, 2.2f, 6.85f), new Vector3(5.28f, 2.15f, 6.55f));
            Object.DestroyImmediate(camera.gameObject);
        }

        private static void PreviewPhase(TrafficSignalCycle cycle, float seconds)
        {
            foreach (
                var head in Object.FindObjectsByType<PedestrianSignalHead>(FindObjectsSortMode.None)
            )
                head.Show(cycle.PedestrianAt(seconds, head.CrossesMainRoad), seconds);
            foreach (
                var head in Object.FindObjectsByType<TrafficSignalHead>(FindObjectsSortMode.None)
            )
                head.Show(cycle.MainAt(seconds), cycle.CrossAt(seconds));
        }

        public static void ApplyAndCapture()
        {
            Apply();
            Capture();
        }

        private static void Save(Camera camera, string name, Vector3 position, Vector3 target)
        {
            string output =
                Environment.GetEnvironmentVariable("PEDESTRIAN_PREVIEW_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/pedestrian-679");
            Directory.CreateDirectory(output);
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var render = new RenderTexture(1600, 1000, 24);
            var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(render);
        }
    }
}
