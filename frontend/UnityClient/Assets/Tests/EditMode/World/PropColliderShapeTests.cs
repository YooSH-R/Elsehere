using System.Linq;
using A305.EditorTools.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 가구·소품 이동 콜라이더의 윗면이 겉모양보다 떠서, 그 위에 올라선 캐릭터가 공중에 떠 보이지 않는지 검사한다.
    /// </summary>
    public sealed class PropColliderShapeTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FindFloatingBoxes_FindsBoxTallerThanItsMesh()
        {
            root = Prop("Chair", meshHeight: .45f, boxHeight: .8f);

            Assert.That(
                PropColliderShape.FindFloatingBoxes(new[] { root }),
                Is.EqualTo(new[] { root.GetComponent<BoxCollider>() })
            );
        }

        [Test]
        public void FindFloatingBoxes_IgnoresBoxThatMatchesItsMesh()
        {
            root = Prop("Table", meshHeight: .6f, boxHeight: .6f);

            Assert.That(PropColliderShape.FindFloatingBoxes(new[] { root }), Is.Empty);
        }

        [Test]
        public void FindFloatingBoxes_IgnoresWideFloorsAndThinTallWalls()
        {
            root = new GameObject("Room");
            GameObject floor = Prop("Floor", meshHeight: .05f, boxHeight: .3f, width: 8f);
            floor.transform.SetParent(root.transform);
            GameObject wall = Prop("Wall", meshHeight: 1.5f, boxHeight: 2.4f, width: .2f);
            wall.transform.SetParent(root.transform);

            Assert.That(PropColliderShape.FindFloatingBoxes(new[] { root }), Is.Empty);
        }

        [Test]
        public void FitToShape_ReplacesBoxWithMeshCollidersOnItsMeshes()
        {
            root = Prop("Chair", meshHeight: .45f, boxHeight: .8f);

            PropColliderShape.FitToShape(root.GetComponent<BoxCollider>());

            Assert.That(root.GetComponentsInChildren<BoxCollider>(), Is.Empty);
            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>();
            Assert.That(
                colliders.Select(collider => collider.sharedMesh),
                Is.EqualTo(root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh))
            );
            Assert.That(colliders.All(collider => !collider.convex && !collider.isTrigger));
        }

        [Test]
        public void FitToShape_LeavesItemsWithTheirOwnCollidersAlone()
        {
            root = Prop("Table", meshHeight: .6f, boxHeight: 1f);
            GameObject mug = Prop("Mug", meshHeight: .1f, boxHeight: .1f);
            mug.transform.SetParent(root.transform);
            mug.transform.localPosition = new Vector3(0f, .6f, 0f);

            PropColliderShape.FitToShape(root.GetComponent<BoxCollider>());

            Assert.That(mug.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(mug.GetComponentInChildren<MeshCollider>(), Is.Null);
        }

        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionScene_HasNoPropColliderFloatingAboveItsShape(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");

            string[] floating = PropColliderShape
                .FindFloatingBoxes(scene.GetRootGameObjects())
                .Select(PropColliderShape.Describe)
                .ToArray();

            Assert.That(
                floating,
                Is.Empty,
                "Tools/A305/World/Fit Prop Colliders To Shape를 실행하세요.\n"
                    + string.Join("\n", floating)
            );
        }

        /// <summary>meshHeight 높이의 상자 메시 위에 boxHeight 높이의 이동 콜라이더를 둔 사물</summary>
        private static GameObject Prop(
            string name,
            float meshHeight,
            float boxHeight,
            float width = .5f
        )
        {
            var prop = new GameObject(name);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(prop.transform);
            visual.transform.localScale = new Vector3(width, meshHeight, width);
            visual.transform.localPosition = new Vector3(0f, meshHeight / 2f, 0f);

            var box = prop.AddComponent<BoxCollider>();
            box.size = new Vector3(width, boxHeight, width);
            box.center = new Vector3(0f, boxHeight / 2f, 0f);
            return prop;
        }
    }
}
