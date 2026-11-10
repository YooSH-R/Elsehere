using A305.EditorTools.Characters;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    public class EyelidFrameTrimmerTests
    {
        private Mesh mesh;

        [SetUp]
        public void CreateMesh()
        {
            mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
        }

        [TearDown]
        public void DestroyMesh()
        {
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Trim_KeepsOnlyTheHalfAndFullFramesOfAnInBetweenChannel()
        {
            // 웹 빌드에서 눈꺼풀 하나의 블렌드셰이프 데이터 80%가 깜빡임·찡그림의 25·50·75·90·100 단계다.
            AddFrames("eyeBlinkLeft", 25, 50, 75, 90, 100);

            bool trimmed = EyelidFrameTrimmer.Trim(mesh);

            Assert.That(trimmed, Is.True);
            int blink = mesh.GetBlendShapeIndex("eyeBlinkLeft");
            Assert.That(mesh.GetBlendShapeFrameCount(blink), Is.EqualTo(2));
            Assert.That(mesh.GetBlendShapeFrameWeight(blink, 0), Is.EqualTo(50));
            Assert.That(mesh.GetBlendShapeFrameWeight(blink, 1), Is.EqualTo(100));
            Assert.That(FrameDelta(blink, 0), Is.EqualTo(Delta(50)), "남긴 단계의 모양은 그대로다");
            Assert.That(FrameDelta(blink, 1), Is.EqualTo(Delta(100)));
        }

        [Test]
        public void Trim_KeepsSingleFrameChannelsAndChannelOrder()
        {
            AddFrames("browDownLeft", 100);
            AddFrames("eyeSquintLeft", 25, 50, 75, 90, 100);
            AddFrames("cheekRaiseLeft", 100);

            EyelidFrameTrimmer.Trim(mesh);

            Assert.That(mesh.blendShapeCount, Is.EqualTo(3));
            Assert.That(mesh.GetBlendShapeName(0), Is.EqualTo("browDownLeft"));
            Assert.That(mesh.GetBlendShapeName(1), Is.EqualTo("eyeSquintLeft"));
            Assert.That(mesh.GetBlendShapeName(2), Is.EqualTo("cheekRaiseLeft"));
            Assert.That(mesh.GetBlendShapeFrameCount(0), Is.EqualTo(1));
            Assert.That(mesh.GetBlendShapeFrameCount(1), Is.EqualTo(2));
            Assert.That(mesh.GetBlendShapeFrameCount(2), Is.EqualTo(1));
            Assert.That(FrameDelta(2, 0), Is.EqualTo(Delta(100)));
        }

        [Test]
        public void Trim_KeepsTheNormalsOfKeptFrames()
        {
            foreach (float weight in new[] { 25f, 50f, 75f, 90f, 100f })
            {
                mesh.AddBlendShapeFrame(
                    "eyeBlinkLeft",
                    weight,
                    Filled(Delta(weight)),
                    Filled(Vector3.forward * weight),
                    null
                );
            }

            EyelidFrameTrimmer.Trim(mesh);

            var normals = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(0, 0, new Vector3[mesh.vertexCount], normals, null);
            Assert.That(normals[0], Is.EqualTo(Vector3.forward * 50));
        }

        [Test]
        public void Trim_LeavesAChannelUnchangedWhenItHasNoHalfFrame()
        {
            // 50·100 단계가 없는 채널은 어떤 단계를 남길지 알 수 없으니 그대로 둔다.
            AddFrames("eyeSquintLeft", 30, 60, 100);

            bool trimmed = EyelidFrameTrimmer.Trim(mesh);

            Assert.That(trimmed, Is.False);
            Assert.That(mesh.GetBlendShapeFrameCount(0), Is.EqualTo(3));
        }

        private void AddFrames(string channel, params float[] weights)
        {
            foreach (float weight in weights)
            {
                mesh.AddBlendShapeFrame(channel, weight, Filled(Delta(weight)), null, null);
            }
        }

        private Vector3 FrameDelta(int channel, int frame)
        {
            var deltas = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(
                channel,
                frame,
                deltas,
                new Vector3[mesh.vertexCount],
                new Vector3[mesh.vertexCount]
            );
            return deltas[0];
        }

        private static Vector3 Delta(float weight) => new Vector3(0, -weight / 1000f, 0);

        private Vector3[] Filled(Vector3 value)
        {
            var values = new Vector3[mesh.vertexCount];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = value;
            }
            return values;
        }
    }
}
