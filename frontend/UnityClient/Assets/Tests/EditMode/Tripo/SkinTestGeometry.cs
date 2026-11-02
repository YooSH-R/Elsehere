using System.Linq;
using UnityEngine;

namespace A305.Tripo.Tests
{
    internal static class SkinTestGeometry
    {
        internal static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var matrices = skin
                .bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
            var weights = mesh.boneWeights;
            return mesh
                .vertices.Select(
                    (v, i) =>
                    {
                        var w = weights[i];
                        return matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    }
                )
                .ToArray();
        }
    }
}
