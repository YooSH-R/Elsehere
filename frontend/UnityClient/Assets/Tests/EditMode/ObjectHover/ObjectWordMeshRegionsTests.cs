using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordMeshRegionsTests
    {
        [Test]
        public void Register_SeparateProductsSharingOneMesh_LeavesTheGapHoverable()
        {
            var display = new GameObject("SeparatedProducts");
            display.transform.position = Vector3.up * 1000;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var combined = new Mesh();
            try
            {
                Mesh shape = cube.GetComponent<MeshFilter>().sharedMesh;
                combined.CombineMeshes(
                    new[]
                    {
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.left * 2),
                        },
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.right * 2),
                        },
                    }
                );
                Object.DestroyImmediate(cube);
                display.AddComponent<MeshFilter>().sharedMesh = combined;
                display.AddComponent<MeshRenderer>();
                var target = display.AddComponent<HoverableObject>();
                var fields = new SerializedObject(target);
                fields.FindProperty("word").objectReferenceValue = ObjectWordAuthoring.LoadWord(
                    "cup"
                );
                fields.ApplyModifiedPropertiesWithoutUndo();
                ObjectWordRegistration.ApplyToPrefab(
                    display,
                    "Assets/Prefabs/World/SeparatedProducts.prefab"
                );
                Physics.SyncTransforms();
                var raycast = new ObjectHoverRaycast();
                Assert.That(
                    raycast.Find(
                        new Ray(
                            display.transform.TransformPoint(new Vector3(0, 0, -3)),
                            Vector3.forward
                        ),
                        6,
                        ~0,
                        null,
                        out _
                    ),
                    Is.Null
                );
                Assert.That(
                    raycast.Find(
                        new Ray(
                            display.transform.TransformPoint(new Vector3(-2, 0, -3)),
                            Vector3.forward
                        ),
                        6,
                        ~0,
                        null,
                        out _
                    ),
                    Is.SameAs(target)
                );
            }
            finally
            {
                Object.DestroyImmediate(display);
                if (cube != null)
                    Object.DestroyImmediate(cube);
                Object.DestroyImmediate(combined);
            }
        }

        [Test]
        public void Split_CombinedSeparateShapes_DoesNotCoverEmptySpaceBetweenThem()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var combined = new Mesh();
            try
            {
                Mesh shape = cube.GetComponent<MeshFilter>().sharedMesh;
                combined.CombineMeshes(
                    new[]
                    {
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.left * 2f),
                        },
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.right * 2f),
                        },
                    }
                );
                Bounds[] regions = ObjectWordMeshRegions.Split(combined);
                Assert.That(regions, Has.Length.EqualTo(2));
                foreach (Bounds region in regions)
                {
                    Assert.That(region.Contains(Vector3.zero), Is.False);
                    Assert.That(region.size, Is.EqualTo(Vector3.one));
                }
                Assert.That(
                    ObjectWordMeshRegions.Split(shape),
                    Has.Length.EqualTo(1),
                    "면마다 복제된 정점은 같은 모양으로 묶어야 한다."
                );
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(combined);
            }
        }
    }
}
