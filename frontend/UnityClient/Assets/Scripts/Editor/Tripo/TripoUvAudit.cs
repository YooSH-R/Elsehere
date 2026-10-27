using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoUvAudit
    {
        private const int MaximumPairChecks = 1000000;
        private const int MaximumSweepChecks = 10000000;
        private const float CoordinateTolerance = 0.000001f;
        private const double MinimumDoubleArea = 0.0000000001;

        private struct UvTriangle
        {
            public Vector2 a,
                b,
                c;
            public int index;
            public float minX,
                maxX,
                minY,
                maxY;
        }

        public static void Inspect(Mesh mesh, bool textured, TripoAuditReport report)
        {
            if (!mesh.isReadable)
            {
                report.Errors.Add(
                    mesh.name + ": UV0 검사 불가. Read/Write를 활성화하고 재검사하세요."
                );
                return;
            }
            Vector2[] uv = mesh.uv;
            if (uv.Length != mesh.vertexCount || uv.Length == 0)
            {
                string message =
                    mesh.name + ": UV0 누락. 텍스처 배치를 위한 UV를 Blender에서 확인하세요.";
                if (textured)
                    report.Errors.Add(message);
                else
                    report.ManualChecks.Add(message + " 단색 재질이면 의도된 상태일 수 있습니다.");
                return;
            }
            int outside = 0;
            int firstOutside = -1;
            for (int vertex = 0; vertex < uv.Length; vertex++)
            {
                Vector2 value = uv[vertex];
                if (!Finite(value.x) || !Finite(value.y))
                {
                    report.Errors.Add(
                        mesh.name
                            + $": UV0 비정상 값: 정점 {vertex}. NaN/Infinity는 사용할 수 없습니다."
                    );
                    return;
                }
                if (
                    value.x < -CoordinateTolerance
                    || value.x > 1f + CoordinateTolerance
                    || value.y < -CoordinateTolerance
                    || value.y > 1f + CoordinateTolerance
                )
                {
                    if (outside == 0)
                        firstOutside = vertex;
                    outside++;
                }
            }
            if (outside > 0)
                report.ManualChecks.Add(
                    mesh.name
                        + $": UV0 범위 0~1 이탈 정점 {outside}개 (첫 정점 {firstOutside}). 반복 텍스처/UDIM 의도와 재질 지원 여부를 확인하세요."
                );
            int pairChecks = 0;
            int sweepChecks = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    report.ManualChecks.Add(
                        mesh.name + $": 서브메시 {submesh} UV0 삼각형 외 면 검사 미실행."
                    );
                    continue;
                }
                int[] indices = mesh.GetTriangles(submesh);
                var triangles = new List<UvTriangle>(indices.Length / 3);
                int degenerate = 0;
                int firstDegenerate = -1;
                for (int offset = 0; offset < indices.Length; offset += 3)
                {
                    Vector2 a = uv[indices[offset]],
                        b = uv[indices[offset + 1]],
                        c = uv[indices[offset + 2]];
                    double area =
                        ((double)b.x - a.x) * ((double)c.y - a.y)
                        - ((double)b.y - a.y) * ((double)c.x - a.x);
                    if (Math.Abs(area) <= MinimumDoubleArea)
                    {
                        if (degenerate == 0)
                            firstDegenerate = offset / 3;
                        degenerate++;
                        continue;
                    }
                    triangles.Add(
                        new UvTriangle
                        {
                            a = a,
                            b = b,
                            c = c,
                            index = offset / 3,
                            minX = Mathf.Min(a.x, b.x, c.x),
                            maxX = Mathf.Max(a.x, b.x, c.x),
                            minY = Mathf.Min(a.y, b.y, c.y),
                            maxY = Mathf.Max(a.y, b.y, c.y),
                        }
                    );
                }
                if (degenerate > 0)
                    report.ManualChecks.Add(
                        mesh.name
                            + $": 서브메시 {submesh} UV0 퇴화 면 {degenerate}개 (첫 삼각형 {firstDegenerate}). UV 면적이 거의 없어 무늬가 늘어나거나 한 점을 샘플링할 수 있습니다."
                    );
                triangles.Sort((first, second) => first.minX.CompareTo(second.minX));
                int overlaps = 0;
                var samples = new List<string>();
                for (int i = 0; i < triangles.Count; i++)
                for (int j = i + 1; j < triangles.Count; j++)
                {
                    UvTriangle first = triangles[i],
                        second = triangles[j];
                    if (second.minX >= first.maxX)
                        break;
                    bool boxesOverlap = second.minY < first.maxY && first.minY < second.maxY;
                    bool exhausted = ++sweepChecks > MaximumSweepChecks;
                    if (boxesOverlap)
                        exhausted |= ++pairChecks > MaximumPairChecks;
                    if (exhausted)
                    {
                        AddOverlaps(mesh.name, submesh, overlaps, samples, report);
                        report.ManualChecks.Add(
                            mesh.name
                                + ": UV0 겹침 검사 미완료: 비교 상한에 도달했습니다. 이후 면의 퇴화·겹침은 검사하지 않았으므로 Blender에서 확인하세요."
                        );
                        return;
                    }
                    if (!boxesOverlap)
                        continue;
                    if (!Overlaps(first, second))
                        continue;
                    overlaps++;
                    if (samples.Count < 5)
                        samples.Add(first.index + "/" + second.index);
                }
                AddOverlaps(mesh.name, submesh, overlaps, samples, report);
            }
            if (mesh.subMeshCount > 1)
                report.ManualChecks.Add(
                    mesh.name
                        + ": UV0 검사 범위: 같은 서브메시 내부의 겹침만 검사했습니다. 서로 다른 서브메시/메시가 같은 텍스처를 공유하면 교차 겹침을 별도 확인하세요."
                );
        }

        private static void AddOverlaps(
            string mesh,
            int submesh,
            int count,
            List<string> samples,
            TripoAuditReport report
        )
        {
            if (count > 0)
                report.ManualChecks.Add(
                    mesh
                        + $": 서브메시 {submesh} UV0 겹침 {count}쌍 (삼각형 쌍 {string.Join(", ", samples)}). 대칭/스택 UV 의도인지 확인하세요. 자동 불합격은 아닙니다."
                );
        }

        // Separating axes reject shared edges and vertices: only positive-area intersections count.
        private static bool Overlaps(UvTriangle first, UvTriangle second) =>
            !Separated(first, second, first.b - first.a)
            && !Separated(first, second, first.c - first.b)
            && !Separated(first, second, first.a - first.c)
            && !Separated(first, second, second.b - second.a)
            && !Separated(first, second, second.c - second.b)
            && !Separated(first, second, second.a - second.c);

        private static bool Separated(UvTriangle first, UvTriangle second, Vector2 edge)
        {
            double x = -edge.y,
                y = edge.x;
            double fa = x * first.a.x + y * first.a.y,
                fb = x * first.b.x + y * first.b.y,
                fc = x * first.c.x + y * first.c.y;
            double sa = x * second.a.x + y * second.a.y,
                sb = x * second.b.x + y * second.b.y,
                sc = x * second.c.x + y * second.c.y;
            double firstMin = Math.Min(fa, Math.Min(fb, fc)),
                firstMax = Math.Max(fa, Math.Max(fb, fc));
            double secondMin = Math.Min(sa, Math.Min(sb, sc)),
                secondMax = Math.Max(sa, Math.Max(sb, sc));
            double tolerance = 0.00000001 * Math.Sqrt(x * x + y * y);
            return Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin) <= tolerance;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
