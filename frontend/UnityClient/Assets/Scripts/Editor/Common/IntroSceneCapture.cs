using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Tripo;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Common
{
    // 웹 로딩 화면의 소개 장면 이미지를 실제 씬에서 찍어 웹 템플릿에 넣는다. 씬 배치가 바뀌면 다시 찍는다.
    public static class IntroSceneCapture
    {
        private const string OutputFolder = "Assets/WebGLTemplates/A305Responsive/intro";
        private const string ScenesFolder = "Assets/Scenes/Production/";
        private const int Width = 1600;
        private const int Height = 900;
        private const int JpgQuality = 80;

        private static Camera camera;
        private static Transform player;

        [MenuItem("Tools/A305/Web/Capture Intro Scenes")]
        public static void Capture()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string previousScene = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutputFolder);

            InScene(
                "TownScene",
                () =>
                {
                    // 행인 NPC는 아직 임시 모형이라 소개 장면에서 뺀다.
                    Hide("TOWN_PASSERBY");
                    Shot(
                        "town",
                        player.position + new Vector3(-1f, 2.4f, -4.2f),
                        player.position + new Vector3(-2f, 1f, 10f)
                    );
                    Shot(
                        "town-overview",
                        player.position + new Vector3(0f, 9f, -6f),
                        player.position + new Vector3(0f, 0f, 16f)
                    );

                    NpcIdentity driver = Npc("TAXI_DRIVER");
                    Vector3 forward = Flat(driver.transform.forward);
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    Vector3 head = Head(driver);
                    Shot(
                        "taxi",
                        head + forward * 4.5f - right * 3f + Vector3.up * 0.3f,
                        head + forward * 0.5f + Vector3.down * 0.5f
                    );
                }
            );
            InScene("CafeScene", () => Conversation("cafe", Npc("CAFE_STAFF")));
            InScene("HotelScene", () => Conversation("hotel", Npc("HOTEL_STAFF")));
            InScene(
                "HomeScene",
                () =>
                {
                    // 플레이어 머리가 화면을 가리지 않도록 눈높이에서 방 안쪽을 비스듬히 본다.
                    Vector3 forward = Quaternion.Euler(0f, 45f, 0f) * Flat(player.forward);
                    Vector3 eye = player.position + Vector3.up * 1.6f + forward * 0.4f;
                    Shot("home", eye, eye + forward * 5f + Vector3.down * 0.9f, 70f);
                }
            );

            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        // 씬을 열어 찍기만 하고 저장하지 않는다. 찍으려고 옮긴 플레이어·숨긴 NPC는 다음 씬을 열 때 버려진다.
        private static void InScene(string sceneName, Action shoot)
        {
            EditorSceneManager.OpenScene(ScenesFolder + sceneName + ".unity", OpenSceneMode.Single);
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            camera =
                cameras.FirstOrDefault(item => item.CompareTag("MainCamera")) ?? cameras.First();
            player = Object
                .FindObjectsByType<PlayerMover>(FindObjectsSortMode.None)
                .First()
                .transform;
            shoot();
        }

        // 카운터 직원 앞에 플레이어를 세우고 옆에서 둘이 마주 보는 모습을 담는다.
        private static void Conversation(string name, NpcIdentity npc)
        {
            Vector3 towardCustomer = Flat(player.position - npc.transform.position);
            Vector3 standing = npc.transform.position + towardCustomer * 1.7f;
            standing.y = player.position.y;
            player.SetPositionAndRotation(standing, Quaternion.LookRotation(-towardCustomer));

            Vector3 side = Vector3.Cross(Vector3.up, towardCustomer);
            Vector3 middle = (Head(npc) + player.position + Vector3.up * 1.3f) / 2f;
            Shot(
                name,
                middle + side * 3.2f + towardCustomer * 1.2f + Vector3.up * 0.5f,
                middle + Vector3.down * 0.2f
            );
        }

        private static void Shot(
            string name,
            Vector3 position,
            Vector3 lookAt,
            float fieldOfView = 60f
        )
        {
            List<TripoMotionPreview> poses = PoseCharactersIdle();
            try
            {
                camera.fieldOfView = fieldOfView;
                camera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(lookAt - position)
                );
                File.WriteAllBytes(Path.Combine(OutputFolder, name + ".jpg"), Render());
            }
            finally
            {
                foreach (TripoMotionPreview pose in poses)
                {
                    pose.Dispose();
                }
            }
        }

        private static NpcIdentity Npc(string code)
        {
            return Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .First(npc => npc.Code == code);
        }

        private static void Hide(string code)
        {
            foreach (
                NpcIdentity npc in Object.FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
            )
            {
                if (npc.Code == code)
                {
                    npc.gameObject.SetActive(false);
                }
            }
        }

        private static Vector3 Head(NpcIdentity npc)
        {
            return npc.LookTarget != null
                ? npc.LookTarget.position
                : npc.transform.position + Vector3.up * 1.4f;
        }

        // 편집 상태의 캐릭터는 기본 자세라 서 있는 동작으로 맞춘다.
        private static List<TripoMotionPreview> PoseCharactersIdle()
        {
            var poses = new List<TripoMotionPreview>();
            foreach (
                Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            )
            {
                if (animator.avatar == null || !animator.avatar.isHuman)
                {
                    continue;
                }

                try
                {
                    var pose = new TripoMotionPreview(animator.gameObject);
                    pose.Apply(TripoReviewMotion.Idle, 0.3f);
                    poses.Add(pose);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"{animator.name} 자세를 맞추지 못해 기본 자세로 찍습니다: {exception.Message}"
                    );
                }
            }

            return poses;
        }

        private static byte[] Render()
        {
            var target = new RenderTexture(Width, Height, 24);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image.EncodeToJPG(JpgQuality);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }
    }
}
