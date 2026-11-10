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
