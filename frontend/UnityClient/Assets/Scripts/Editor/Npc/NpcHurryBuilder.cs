using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Npc;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>출퇴근 NPC의 실제 보행·달리기 속도에 맞는 전용 이동 컨트롤러를 생성한다.</summary>
    public static class NpcHurryBuilder
    {
        private const string ControllerPath = "Assets/Resources/NpcCommuter.controller";

        [MenuItem("Tools/A305/Npc/Apply Hurry Movement")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                return;
            var player = (AnimatorController)LittleMungAnimationBuilder.Build();
            var source = (BlendTree)
                player
                    .layers[0]
                    .stateMachine.states.First(state => state.state.name == "Locomotion")
                    .state.motion;
            ChildMotion idle = source.children.First(child => child.threshold == 0);
            ChildMotion walk = source.children.First(child => child.motion.name == "Walk");
            ChildMotion run = source.children.First(child => child.motion.name == "Run");
            walk.threshold = NpcHurry.WalkMetersPerSecond;
            walk.timeScale = 2;
            run.threshold = NpcHurry.RunMetersPerSecond;
            run.timeScale = 2;
            BuilderTools.EnsureFolder("Assets/Resources");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Action", AnimatorControllerParameterType.Int);
            var parameters = controller.parameters;
            parameters[1].defaultBool = true;
            controller.parameters = parameters;
            var state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.children = new[] { idle, walk, run };
            controller.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
            Debug.Log("출퇴근 NPC 전용 걷기·달리기 컨트롤러를 만들었습니다.");
        }

        [MenuItem("Tools/A305/Npc/Capture Hurry Event")]
        public static void Capture()
        {
            Apply();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            // 일과를 실행하지 않는 검수 화면에서는 저장된 직원 대신 예시 NPC 한 명만 보여 준다.
            foreach (NpcIdentity identity in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                identity.gameObject.SetActive(false);
            foreach (Camera previous in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                previous.enabled = false;
            var camera = new GameObject("HurryReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 50;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 150;
            camera.transform.position = new Vector3(1, 2.4f, 19.8f);
            camera.transform.LookAt(new Vector3(4.2f, .8f, 24));
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .2f;
                }
            var offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(scene);
            offer.ShowEvent(
                typeof(NpcHurryBuilder),
                PasserbyOfferText.HurryHint("Mia"),
                PasserbyOfferText.HurryGreeting
            );
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Commuters/CAFE_STAFF.prefab"
            );
            GameObject npc = Object.Instantiate(prefab);
            npc.SetActive(true);
            npc.transform.SetPositionAndRotation(
                new Vector3(4.2f, .1f, 24),
                Quaternion.LookRotation(new Vector3(-1, 0, -1))
            );
            Animator model = npc.GetComponentsInChildren<Animator>(true)
                .First(animator => animator.avatar != null && animator.avatar.isHuman);
            model.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath
            );
            model.applyRootMotion = false;
            model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.Rebind();
            model.Update(0);
            for (int frame = 0; frame < 20; frame++)
            {
                model.SetFloat("Speed", NpcHurry.RunMetersPerSecond);
                model.Update(1f / 60f);
            }
            foreach (AnimatorClipInfo clip in model.GetCurrentAnimatorClipInfo(0))
                if (clip.weight > .01f)
                    Debug.Log($"서두르기 검수 동작: {clip.clip.name}, 비중 {clip.weight:0.00}");
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1600, 1000, 24);
            var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                pixels.Apply();
                string output =
                    Environment.GetEnvironmentVariable("NPC_HURRY_PREVIEW_OUTPUT")
                    ?? Path.GetFullPath("../../artifacts/npc-680");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "hurry-event.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(npc);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
