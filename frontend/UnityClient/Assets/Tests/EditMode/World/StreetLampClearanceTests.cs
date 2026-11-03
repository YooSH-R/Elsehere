using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 가로등 머리(높이 약 3.5m)가 가로수 잎(폭 약 3m, 높이 약 4.5m) 속에 파묻혀 보이지 않게, 가로등은 가로수와 3m 넘게 떨어뜨린다.
    /// </summary>
    public class StreetLampClearanceTests
    {
        private const float Clearance = 3f;

        [Test]
        public void TownStreetLamps_StandClearOfStreetTrees()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            Transform[] all = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            Vector3[] lamps = all.Where(item => item.name == "StreetLampV2")
                .Select(item => item.position)
                .ToArray();
            Vector3[] trees = all.Where(item => item.name == "GardenTreeV2")
                .Select(item => item.position)
                .ToArray();

            Assert.That(lamps, Is.Not.Empty);
            string[] buried = (
                from lamp in lamps
                from tree in trees
                where Flat(lamp, tree) < Clearance
                select $"가로등 ({lamp.x:F2}, {lamp.z:F2}) - 가로수 ({tree.x:F2}, {tree.z:F2})"
            ).ToArray();
            Assert.That(buried, Is.Empty);
        }

        private static float Flat(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
