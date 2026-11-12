using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Ambience.PlayModeTests
{
    public class ServiceAmbiencePlaybackTests
    {
        [UnityTest]
        public IEnumerator MutingAndUnmutingKeepsTheCurrentLoopAvailable()
        {
            bool enabled = BackgroundSoundPreference.Enabled;
            Scene original = SceneManager.GetActiveScene();
            Scene room = SceneManager.CreateScene("HomeScene");
            var root = new GameObject("Background toggle check");
            try
            {
                BackgroundSoundPreference.Enabled = true;
                SceneManager.SetActiveScene(room);
                var catalog = Resources.Load<ServiceAmbienceCatalog>(
                    ServiceAmbienceCatalog.ResourcePath
                );
                root.AddComponent<ServiceAmbiencePlayer>().Configure(catalog);
                yield return new WaitForSecondsRealtime(.2f);
                var loop = root.GetComponentsInChildren<AudioSource>()
                    .Single(source => source.loop && source.clip != null);
                BackgroundSoundPreference.Enabled = false;
                yield return null;
                Assert.That(loop.mute, Is.True);
                Assert.That(loop.clip, Is.EqualTo(catalog.Bed(AmbienceBed.Room)));
                BackgroundSoundPreference.Enabled = true;
                yield return null;
                Assert.That(loop.mute, Is.False);
                Assert.That(loop.isPlaying, Is.True);
            }
            finally
            {
                BackgroundSoundPreference.Enabled = enabled;
                Object.Destroy(root);
                SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(room);
            }
        }

        [UnityTest]
        public IEnumerator HeldQuiet_KeepsThePlaceLoopAsLowAsDuringAConversation()
        {
            // 튜토리얼이 대화 뒤 안내를 마칠 때까지 낮춰 두라고 하면 대화하지 않아도 대화 중 크기로 둔다.
            bool enabled = BackgroundSoundPreference.Enabled;
            Scene original = SceneManager.GetActiveScene();
            Scene room = SceneManager.CreateScene("HomeScene");
            var root = new GameObject("Held quiet check");
            try
            {
                BackgroundSoundPreference.Enabled = true;
                AmbienceQuiet.IsHeld = true;
                SceneManager.SetActiveScene(room);
                var catalog = Resources.Load<ServiceAmbienceCatalog>(
                    ServiceAmbienceCatalog.ResourcePath
                );
                root.AddComponent<ServiceAmbiencePlayer>().Configure(catalog);
                yield return new WaitForSecondsRealtime(.6f);
                var loop = root.GetComponentsInChildren<AudioSource>()
                    .Single(source => source.loop && source.clip != null);

                Assert.That(
                    loop.volume,
                    Is.LessThanOrEqualTo(AmbiencePlan.Volume(.6f, true) + .001f)
                );
            }
            finally
            {
                AmbienceQuiet.IsHeld = false;
                BackgroundSoundPreference.Enabled = enabled;
                Object.Destroy(root);
                SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(room);
            }
        }

        [UnityTest]
        public IEnumerator ActualSources_ChangeFromRoomToStreetAndStopAtEntry()
        {
            Scene original = SceneManager.GetActiveScene();
            Scene room = SceneManager.CreateScene("HomeScene");
            Scene street = SceneManager.CreateScene("TownScene");
            Scene entry = SceneManager.CreateScene("EntryScene");
            var root = new GameObject("Ambience smoke check");
            try
            {
                var catalog = Resources.Load<ServiceAmbienceCatalog>(
                    ServiceAmbienceCatalog.ResourcePath
                );
                Assert.That(catalog, Is.Not.Null);
                SceneManager.SetActiveScene(room);
                root.AddComponent<ServiceAmbiencePlayer>().Configure(catalog);
                yield return null;
                AudioSource[] sources = root.GetComponentsInChildren<AudioSource>();
                Assert.That(
                    sources.Any(source =>
                        source.loop && source.clip == catalog.Bed(AmbienceBed.Room)
                    ),
                    Is.True
                );
                SceneManager.SetActiveScene(street);
                yield return null;
                Assert.That(
                    sources.Any(source =>
                        source.loop && source.clip == catalog.Bed(AmbienceBed.Street)
                    ),
                    Is.True
                );
                SceneManager.SetActiveScene(entry);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(
                    sources
                        .Where(source => source.loop)
                        .All(source => source.volume == 0 && !source.isPlaying),
                    Is.True
                );
            }
            finally
            {
                Object.Destroy(root);
                SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(room);
                SceneManager.UnloadSceneAsync(street);
                SceneManager.UnloadSceneAsync(entry);
            }
        }
    }
}
