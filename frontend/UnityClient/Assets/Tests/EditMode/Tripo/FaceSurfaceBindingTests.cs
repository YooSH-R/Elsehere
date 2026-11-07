using A305.EditorTools.Characters;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public sealed class FaceSurfaceBindingTests
    {
        [Test]
        public void SharedSkinBoundaryKeepsTheSameBoneInfluenceWhenTheHeadTurns()
        {
            var body = new Mesh();
            var join = new Mesh();
            try
            {
                body.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
                body.triangles = new[] { 0, 1, 2 };
                var neckAndHead = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = .96f,
                    boneIndex1 = 1,
                    weight1 = .04f,
                };
                var headOnly = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                body.boneWeights = new[] { neckAndHead, neckAndHead, headOnly };
                join.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right, Vector3.one };
                join.boneWeights = new[] { headOnly, headOnly, headOnly, headOnly };
                join.triangles = new[] { 0, 1, 3, 1, 2, 3 };

                NpcEmotionBuilder.MatchEyeJoinWeights(body, join);

                Assert.That(join.boneWeights[0], Is.EqualTo(neckAndHead));
                Assert.That(join.boneWeights[1], Is.EqualTo(neckAndHead));
                Assert.That(join.boneWeights[3], Is.EqualTo(headOnly));
                Assert.That(body.boneWeights[0], Is.EqualTo(neckAndHead));
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(join);
            }
        }
    }
}
