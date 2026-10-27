using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public enum TripoContactKind
    {
        Seam,
        Penetration,
    }

    [Serializable]
    public sealed class TripoContactSample
    {
        public string label = "Contact";
        public string bodyMeshPath;
        public int bodyVertex;
        public string partMeshPath;
        public int partVertex;
        public TripoContactKind kind;
    }

    public static class TripoContactAudit
    {
        public static string GetTopologyFingerprint(GameObject model)
        {
            var text = new System.Text.StringBuilder();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                AppendTopology(text, filter.transform, model.transform, filter.sharedMesh);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AppendTopology(text, skin.transform, model.transform, skin.sharedMesh);
            return Hash128.Compute(text.ToString()).ToString();
        }

        private static void AppendTopology(
            System.Text.StringBuilder text,
            Transform target,
            Transform root,
            Mesh mesh
        )
        {
            if (mesh == null || !mesh.isReadable)
                throw new InvalidOperationException(
                    "접합 샘플을 확인할 메시와 Read/Write 설정이 필요합니다."
                );
            text.Append(UnityEditor.AnimationUtility.CalculateTransformPath(target, root))
                .Append(':')
                .Append(mesh.vertexCount)
                .Append(';');
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                text.Append(mesh.GetTopology(submesh)).Append(':');
                foreach (int index in mesh.GetIndices(submesh))
                    text.Append(index).Append(',');
                text.Append(';');
            }
        }

        public static TripoAuditReport Inspect(
            GameObject model,
            IReadOnlyList<TripoContactSample> samples,
            float height,
            TripoValidationProfile profile
        )
        {
            var report = new TripoAuditReport();
            if (height <= 0f || float.IsNaN(height) || float.IsInfinity(height) || profile == null)
            {
                report.Errors.Add("접합 검사에 유효한 캐릭터 높이와 프로파일이 필요합니다.");
                return report;
            }
            if (samples.Count == 0)
            {
                report.ManualChecks.Add(
                    "접합 샘플 없음: 틈·관통 거리를 측정하지 않았습니다. 필요한 샘플을 지정하고 재검사하세요."
                );
                return report;
            }
            foreach (var sample in samples)
            {
                try
                {
                    ReadVertex(
                        model,
                        sample.bodyMeshPath,
                        sample.bodyVertex,
                        out Vector3 body,
                        out Vector3 normal
                    );
                    ReadVertex(
                        model,
                        sample.partMeshPath,
                        sample.partVertex,
                        out Vector3 part,
                        out _
                    );
                    if (sample.kind == TripoContactKind.Seam)
                    {
                        float ratio = Vector3.Distance(body, part) / height;
                        if (ratio > profile.SeamHeightRatio)
                            report.Errors.Add(
                                sample.label
                                    + $": 지정 접합 간격 {ratio:P3} H, 허용 {profile.SeamHeightRatio:P3} H 초과."
                            );
                    }
                    else
                    {
                        float depth = -Vector3.Dot(part - body, normal) / height;
                        if (depth > profile.PenetrationHeightRatio)
                            report.Errors.Add(
                                sample.label
                                    + $": 금지 영역의 지정 관통 깊이 {depth:P3} H, 재검수 필요."
                            );
                    }
                }
                catch (Exception exception)
                {
                    report.Errors.Add(sample.label + ": " + exception.Message);
                }
            }
            report.ManualChecks.Add(
                "지정 정점 쌍의 거리·법선 방향 깊이만 측정했습니다. 샘플 밖 전체 표면의 틈·관통은 별도 검수하세요."
            );
            return report;
        }

        private static void ReadVertex(
            GameObject root,
            string path,
            int index,
            out Vector3 position,
            out Vector3 normal
        )
        {
            Transform target = string.IsNullOrEmpty(path)
                ? root.transform
                : root.transform.Find(path);
            if (target == null)
                throw new InvalidOperationException("메시 경로 누락: " + path);
            var filter = target.GetComponent<MeshFilter>();
            Mesh mesh = filter == null ? null : filter.sharedMesh;
            Mesh baked = null;
            try
            {
                var skin = target.GetComponent<SkinnedMeshRenderer>();
                if (skin != null)
                {
                    baked = new Mesh();
                    skin.BakeMesh(baked);
                    mesh = baked;
                }
                if (mesh == null || !mesh.isReadable || index < 0 || index >= mesh.vertexCount)
                    throw new InvalidOperationException(
                        "메시/정점 인덱스 또는 Read/Write 설정을 확인하세요: " + path
                    );
                Vector3[] normals = mesh.normals;
                if (normals.Length != mesh.vertexCount || normals[index].sqrMagnitude < 0.000001f)
                    throw new InvalidOperationException("측정 정점에 법선이 없습니다: " + path);
                position = target.TransformPoint(mesh.vertices[index]);
                normal = target
                    .localToWorldMatrix.inverse.transpose.MultiplyVector(normals[index])
                    .normalized;
                if (!Finite(position) || !Finite(normal))
                    throw new InvalidOperationException(
                        "측정 정점에 유한하지 않은 좌표가 있습니다."
                    );
            }
            finally
            {
                if (baked != null)
                    UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }
}
