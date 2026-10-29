using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>재질별로 합쳐진 메시에서도 서로 떨어진 모양마다 호버 영역을 만든다.</summary>
    public static class ObjectWordMeshRegions
    {
        public static Bounds[] Split(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] parents = Enumerable.Range(0, vertices.Length).ToArray();
            var positions = new Dictionary<Vector3Int, int>();
            for (int index = 0; index < vertices.Length; index++)
            {
                // 노멀·UV 때문에 복제된 정점도 같은 위치면 같은 모양이다.
                Vector3Int position = Vector3Int.RoundToInt(vertices[index] * 100000f);
                if (positions.TryGetValue(position, out int previous))
                    Join(parents, index, previous);
                else
                    positions.Add(position, index);
            }
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Join(parents, triangles[index], triangles[index + 1]);
                Join(parents, triangles[index], triangles[index + 2]);
            }
            var groups = new Dictionary<int, Bounds>();
            foreach (int vertex in triangles)
            {
                int root = Root(parents, vertex);
                if (!groups.TryGetValue(root, out Bounds bounds))
                    bounds = new Bounds(vertices[vertex], Vector3.zero);
                bounds.Encapsulate(vertices[vertex]);
                groups[root] = bounds;
            }
            return groups
                .Values.OrderBy(bounds => bounds.center.x)
                .ThenBy(bounds => bounds.center.y)
                .ThenBy(bounds => bounds.center.z)
                .ToArray();
        }

        private static int Root(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }
            return index;
        }

        private static void Join(int[] parents, int left, int right)
        {
            parents[Root(parents, right)] = Root(parents, left);
        }
    }
}
