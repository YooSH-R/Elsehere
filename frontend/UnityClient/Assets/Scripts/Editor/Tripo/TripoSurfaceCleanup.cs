using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    /// <summary>닫힌 바탕 면에 붙은 극소 삼각형만 제거한다. 구멍을 메우거나 의도된 열린 면을 제거하지 않는다.</summary>
    public static class TripoSurfaceCleanup
    {
        public static int RemoveAttachedSlivers(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var positions = new Dictionary<Vector3, int>();
            var ids = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!positions.TryGetValue(vertices[i], out int id))
                {
                    id = positions.Count;
                    positions.Add(vertices[i], id);
                }
                ids[i] = id;
            }
            var faces = new List<Face>();
            var counts = new Dictionary<(int, int), int>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    return 0;
                var triangles = mesh.GetTriangles(submesh);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    int a = triangles[index],
                        b = triangles[index + 1],
                        c = triangles[index + 2];
                    var edges = new[]
                    {
                        Edge(ids[a], ids[b]),
                        Edge(ids[b], ids[c]),
                        Edge(ids[c], ids[a]),
                    }
                        .Where(e => e.Item1 != e.Item2)
                        .ToArray();
                    faces.Add(
                        new Face
                        {
                            Submesh = submesh,
                            Index = index,
                            Edges = edges,
                            Area =
                                Vector3
                                    .Cross(vertices[b] - vertices[a], vertices[c] - vertices[a])
                                    .magnitude * .5f,
                        }
                    );
                    foreach (var edge in edges)
                    {
                        counts.TryGetValue(edge, out int count);
                        counts[edge] = count + 1;
                    }
                }
            }
            float limit = mesh.bounds.size.sqrMagnitude * 1e-7f;
            var removed = faces
                .Where(f =>
                    f.Area <= limit
                    && f.Edges.Any(e => counts[e] == 1)
                    && f.Edges.Any(e => counts[e] > 2)
                )
                .ToArray();
            if (removed.Length == 0 || removed.Length > 32)
                return 0;
            var remaining = new Dictionary<(int, int), int>(counts);
            var affected = new HashSet<(int, int)>();
            foreach (var face in removed)
            foreach (var edge in face.Edges)
            {
                remaining[edge]--;
                affected.Add(edge);
            }
            // 변경한 경계는 사라지거나 정확히 두 면에 연결되어야 한다. 새로운 열린 경계는 허용하지 않는다.
            if (affected.Any(e => remaining[e] != 0 && remaining[e] != 2))
                return 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = removed
                    .Where(f => f.Submesh == submesh)
                    .Select(f => f.Index / 3)
                    .ToHashSet();
                if (indices.Count > 0)
                    mesh.SetTriangles(
                        mesh.GetTriangles(submesh)
                            .Where((v, i) => !indices.Contains(i / 3))
                            .ToArray(),
                        submesh
                    );
            }
            return removed.Length;
        }

        private static (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);

        private sealed class Face
        {
            public int Submesh,
                Index;
            public float Area;
            public (int, int)[] Edges;
        }
    }
}
