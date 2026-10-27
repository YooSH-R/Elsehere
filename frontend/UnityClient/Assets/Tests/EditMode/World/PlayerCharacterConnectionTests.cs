using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Player;
using A305.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    public sealed class PlayerCharacterConnectionTests
    {
        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        public void ReconnectingCharacterKeepsPlacementAndExistingFeatureReferences(
            string sceneName
        )
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/" + sceneName + ".unity"
            );
            try
            {
                var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                var camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                var arrival = BuilderTools.FindSingleInScene<SpaceArrival>(scene);
                var appearance = player.GetComponent<PlayerCharacterAppearance>();
                Transform visual = appearance.VisualRoot;
                Vector3 position = player.transform.position;
                Quaternion rotation = player.transform.rotation;
                var unrelated = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(item => !item.IsChildOf(player.transform))
                    .Select(item => new
                    {
                        Transform = item,
                        item.localPosition,
                        item.localRotation,
                        item.localScale,
                        item.gameObject.activeSelf,
                    })
                    .ToArray();
                var playerReferences = new SerializedObject(player);
                Object move = playerReferences.FindProperty("moveAction").objectReferenceValue;
                Object jump = playerReferences.FindProperty("jumpAction").objectReferenceValue;
                PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, camera);
                PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, camera);

                Assert.That(
                    appearance.VisualRoot,
                    Is.SameAs(visual),
                    "Repeat setup must retain the authored visual."
                );
                Assert.That(player.transform.position, Is.EqualTo(position));
                Assert.That(player.transform.rotation, Is.EqualTo(rotation));
                Assert.That(player.CameraRig, Is.SameAs(camera));
                Assert.That(
                    new SerializedObject(camera).FindProperty("followTarget").objectReferenceValue,
                    Is.EqualTo(player.transform)
                );
                Assert.That(
                    new SerializedObject(arrival).FindProperty("player").objectReferenceValue,
                    Is.EqualTo(player)
                );
                Assert.That(
                    new SerializedObject(arrival).FindProperty("cameraRig").objectReferenceValue,
                    Is.EqualTo(camera)
                );
                playerReferences.Update();
                Assert.That(
                    playerReferences.FindProperty("moveAction").objectReferenceValue,
                    Is.EqualTo(move)
                );
                Assert.That(
                    playerReferences.FindProperty("jumpAction").objectReferenceValue,
                    Is.EqualTo(jump)
                );
                Assert.That(
                    player.GetComponentsInChildren<PlayerCharacterAppearance>(true),
                    Has.Length.EqualTo(1)
                );
                Assert.That(
                    player.GetComponents<PlayerCharacterVisibility>(),
                    Has.Length.EqualTo(1)
                );
                Assert.That(
                    player.GetComponentsInChildren<BlockoutPlayerVisibility>(true),
                    Is.Empty
                );
                Assert.That(visual.gameObject.activeSelf, Is.True);
                Assert.That(visual.GetComponentsInChildren<Animator>(true), Has.Length.EqualTo(1));
                foreach (var before in unrelated)
                {
                    Assert.That(before.Transform, Is.Not.Null);
                    Assert.That(before.Transform.localPosition, Is.EqualTo(before.localPosition));
                    Assert.That(before.Transform.localRotation, Is.EqualTo(before.localRotation));
                    Assert.That(before.Transform.localScale, Is.EqualTo(before.localScale));
                    Assert.That(
                        before.Transform.gameObject.activeSelf,
                        Is.EqualTo(before.activeSelf)
                    );
                }
                Assert.That(
                    scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Sum(item =>
                            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                                item.gameObject
                            )
                        ),
                    Is.Zero
                );
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
