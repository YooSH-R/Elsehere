using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.Learning;
using A305.Mission;
using A305.Player;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Npc.PlayModeTests
{
    public class DinerMealRestTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ActualDinerBoothSitsPausesAndReceivesOnlyOnce()
        {
            SceneManager.sceneLoaded += DisableNetworkAndClock;
            try
            {
                yield return SceneManager.LoadSceneAsync("TownScene");
                yield return null;
                foreach (
                    NpcPopulation population in Object.FindObjectsByType<NpcPopulation>(
                        FindObjectsSortMode.None
                    )
                )
                    population.enabled = false;
                var shop = Object
                    .FindObjectsByType<ShopInterior>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                    .Single(item => item.PlaceCode == "DINER");
                yield return (IEnumerator)
                    typeof(ShopInterior)
                        .GetMethod("SetOccupied", Fields)
                        .Invoke(shop, new object[] { true });
                var player = Object.FindFirstObjectByType<PlayerMover>();
                var dialogue = Object.FindFirstObjectByType<DialogueController>();
                var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                var pose = player.GetComponent<PlayerSeatPose>();
                Transform booth = SceneManager
                    .GetSceneByName(shop.SceneName)
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>())
                    .First(item => item.name == "BoothSeat");
                player.Teleport(booth.position + booth.forward * .7f, Quaternion.identity);
                player.SetMovementLocked(false);
                typeof(PasserbyOfferView).GetMethod("CloseTalkPrompt", Fields).Invoke(offer, null);
                int received = 0;
                var state = new FirstDayMissionState
                {
                    Started = true,
                    IsTownStory = true,
                    Stage = 0,
                    CanRest = true,
                };
                using (var rest = new DinerMealRest(player, dialogue, offer, () => received++))
                {
                    rest.Tick(100, state, false);
                    Assert.That(received, Is.Zero, "서서 기다린 시간은 세지 않습니다.");
                    Assert.That(offer.IsTalkPromptShownBy(rest), Is.True);
                    offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                    Assert.That(
                        pose.IsSeated,
                        Is.True,
                        "실제 식당 부스와 Humanoid 앉기를 연결합니다."
                    );
                    Assert.That(player.CameraRig.IsInSeatView, Is.True);
                    rest.Tick(4, state, false);
                    yield return null;
                    rest.Tick(0, state, false);
                    Assert.That(offer.ShownButtonRect.gameObject.activeInHierarchy, Is.True);
                    Assert.That(received, Is.Zero);
                    CaptureIfRequested(
                        player.GetComponent<PlayerCameraRig>()
                            ?? Object.FindFirstObjectByType<PlayerCameraRig>()
                    );
                    offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                    Assert.That(pose.IsSeated, Is.False);
                    Assert.That(player.CameraRig.IsInSeatView, Is.False);
                    rest.Tick(100, state, false);
                    Assert.That(received, Is.Zero, "일어선 동안 준비 시간은 멈춥니다.");
                    offer.ShownButtonRect.GetComponent<Button>().onClick.Invoke();
                    rest.Tick(6, state, false);
                    Assert.That(received, Is.EqualTo(1));
                    Assert.That(pose.IsSeated, Is.False);
                    state.CanRest = false;
                    rest.Tick(100, state, false);
                    Assert.That(received, Is.EqualTo(1));
                }
                yield return (IEnumerator)
                    typeof(ShopInterior)
                        .GetMethod("SetOccupied", Fields)
                        .Invoke(shop, new object[] { false });
            }
            finally
            {
                SceneManager.sceneLoaded -= DisableNetworkAndClock;
            }
        }

        private static void DisableNetworkAndClock(Scene scene, LoadSceneMode mode)
        {
            foreach (
                MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    component is FirstDayMissionController
                    || component is LearningController
                    || component is GameClockDriver
                )
                    component.enabled = false;
                if (component is A305.Tutorial.TutorialDirector)
                    component.enabled = false;
                if (component is Passerby)
                    component.enabled = false;
                if (component is A305.Tutorial.TutorialGuideView)
                    (
                        (GameObject)
                            typeof(A305.Tutorial.TutorialGuideView)
                                .GetField("panel", Fields)
                                .GetValue(component)
                    ).SetActive(false);
                if (component is ControlsGuideView guide)
                    guide.SetBlocked(true);
                if (component is SpaceArrival)
                    typeof(SpaceArrival).GetField("apiSettings", Fields).SetValue(component, null);
            }
            var dialogue = Object.FindFirstObjectByType<DialogueController>();
            if (dialogue == null)
                return;
            typeof(DialogueController).GetField("conversationApi", Fields).SetValue(dialogue, null);
            typeof(DialogueController).GetField("followUpApi", Fields).SetValue(dialogue, null);
        }

        private static void CaptureIfRequested(PlayerCameraRig rig)
        {
            string path = System.Environment.GetEnvironmentVariable("DINER_MEAL_CAPTURE");
            if (string.IsNullOrEmpty(path))
                return;
            Camera camera = rig.GetComponent<Camera>();
            camera.nearClipPlane = .01f;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .2f;
                }
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            camera.aspect = 1.6f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(image);
            Object.Destroy(target);
        }
    }
}
