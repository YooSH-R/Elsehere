using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public enum TripoAssetKind
    {
        Character,
        Hair,
        Clothing,
        Accessory,
    }

    public enum TripoAttachmentKind
    {
        Rigid,
        Skinned,
        FaceSurface,
    }

    public sealed class TripoAuditReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> ManualChecks = new List<string>();
        public bool Passed => Errors.Count == 0;
    }

    public static class TripoAssetAudit
    {
        public static TripoAuditReport Inspect(
            GameObject model,
            TripoAssetKind kind,
            TripoAttachmentKind attachment,
            TripoValidationProfile profile
        )
        {
            var report = new TripoAuditReport();
            bool hasMesh = false;
            var meshes = new HashSet<Mesh>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                hasMesh |= filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0;
                if (filter.sharedMesh != null)
                    meshes.Add(filter.sharedMesh);
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                hasMesh |= skin.sharedMesh != null && skin.sharedMesh.vertexCount > 0;
                if (skin.sharedMesh != null)
                    meshes.Add(skin.sharedMesh);
            }
            if (
                attachment == TripoAttachmentKind.Skinned
                && kind != TripoAssetKind.Character
                && model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0
            )
                report.Errors.Add(
                    "스킨 부품에는 본과 가중치를 갖춘 SkinnedMeshRenderer가 필요합니다."
                );
            if (!hasMesh)
                report.Errors.Add("표시할 메시가 없습니다.");
            foreach (var mesh in meshes)
            {
                InspectBoundaries(mesh, report);
                TripoUvAudit.Inspect(mesh, HasTexture(model, mesh), report);
            }
            bool hasFaceShape = false;
            foreach (var mesh in meshes)
                hasFaceShape |= mesh.blendShapeCount > 0;
            if (kind == TripoAssetKind.Character && !hasFaceShape)
                report.ManualChecks.Add(
                    "얼굴 Shape Key 없음: 표정·립싱크 변형은 준비되지 않았습니다. 별도 제작과 검수가 필요합니다."
                );
            report.ManualChecks.Add(
                "동작 검사 미실행: 현재 모델의 구조와 지정 접합 샘플을 검사했습니다. 걷기·뛰기·인사 중 변형 품질은 별도 검수해야 합니다."
            );
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterials.Length == 0)
                    report.Errors.Add(renderer.name + ": 재질이 없습니다.");
                foreach (var material in renderer.sharedMaterials)
                    if (material == null)
                        report.Errors.Add(renderer.name + ": 연결되지 않은 재질이 있습니다.");
            }
            report.ManualChecks.Add("대화 거리와 얼굴 확대에서 틈·관통·외형을 확인하세요.");
            report.ManualChecks.Add("대표 포즈, 표정, 전체 크기 0.85/1/1.15에서 확인하세요.");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null)
                {
                    report.Errors.Add(skin.name + ": 스킨 메시가 없습니다.");
                    continue;
                }
                Transform[] bones = skin.bones;
                foreach (Transform bone in bones)
                    if (bone == null)
                        report.Errors.Add(skin.name + ": 연결되지 않은 본이 있습니다.");
                if (!mesh.isReadable)
                {
                    report.Errors.Add(
                        skin.name + ": 가중치를 검사하려면 Read/Write를 활성화하세요."
                    );
                    continue;
                }
                using var counts = mesh.GetBonesPerVertex();
                using var weights = mesh.GetAllBoneWeights();
                if (counts.Length != mesh.vertexCount || weights.Length == 0)
                    report.Errors.Add(skin.name + ": 정점 가중치가 없습니다.");
                int offset = 0;
                for (int vertex = 0; vertex < counts.Length; vertex++)
                {
                    float sum = 0f;
                    for (int influence = 0; influence < counts[vertex]; influence++)
                        sum += weights[offset++].weight;
                    if (
                        float.IsNaN(sum)
                        || float.IsInfinity(sum)
                        || Mathf.Abs(sum - 1f) > profile.WeightTolerance
                    )
                    {
                        report.Errors.Add(
                            skin.name + ": 정점 " + vertex + "의 가중치 합계가 올바르지 않습니다."
                        );
                        break;
                    }
                }
            }
            return report;
        }

        private static bool HasTexture(GameObject model, Mesh mesh)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh renderedMesh =
                    skin != null ? skin.sharedMesh
                    : filter != null ? filter.sharedMesh
                    : null;
                if (renderedMesh != mesh)
                    continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    foreach (string property in material.GetTexturePropertyNames())
                        if (material.GetTexture(property) != null)
                            return true;
                }
            }
            return false;
        }

        private static void InspectBoundaries(Mesh mesh, TripoAuditReport report)
        {
            if (!mesh.isReadable)
            {
                report.ManualChecks.Add(
                    mesh.name
                        + ": Read/Write가 꺼져 경계를 검사하지 못했습니다. 활성화 후 재검사하세요."
                );
                return;
            }
            // UV·법선 분리로 같은 위치에 복제된 정점은 검사 중에만 같은 정점으로 본다.
            // 위치가 가까울 뿐인 정점은 합치지 않아 실제 작은 틈을 숨기지 않는다.
            Vector3[] vertices = mesh.vertices;
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
            var edges = new Dictionary<(int, int), int>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    continue;
                int[] triangles = mesh.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = ids[triangles[i + edge]];
                    int b = ids[triangles[i + (edge + 1) % 3]];
                    if (a == b)
                        continue;
                    var key = a < b ? (a, b) : (b, a);
                    edges.TryGetValue(key, out int count);
                    edges[key] = count + 1;
                }
            }
            int open = 0;
            int shared = 0;
            foreach (int count in edges.Values)
            {
                if (count == 1)
                    open++;
                else if (count > 2)
                    shared++;
            }
            if (open > 0)
                report.ManualChecks.Add(
                    mesh.name
                        + $": 열린 경계 {open}개. 옷 입구 등 의도된 경계인지 얼굴 균열인지 확인하세요. 자동 불합격으로 판정하지 않습니다."
                );
            if (shared > 0)
                report.ManualChecks.Add(
                    mesh.name
                        + $": 3개 이상 면이 공유하는 경계 {shared}개. 중복 면·접합 상태를 확인하세요."
                );
        }
    }
}
