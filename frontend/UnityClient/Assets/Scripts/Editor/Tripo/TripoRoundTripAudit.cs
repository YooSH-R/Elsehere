using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoRoundTripAudit
    {
        public static TripoAuditReport Compare(
            GameObject before,
            GameObject after,
            IReadOnlyList<TripoPartSlot> slots,
            TripoValidationProfile profile,
            bool requireRestPose = false
        )
        {
            var report = new TripoAuditReport();
            float height = A305.EditorTools.Common.BuilderTools.GetModelHeight(before);
            foreach (var original in before.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(
                    original.transform,
                    before.transform
                );
                Transform replacement = Find(after.transform, path);
                var changed =
                    replacement == null ? null : replacement.GetComponent<SkinnedMeshRenderer>();
                if (changed == null || changed.sharedMesh == null || original.sharedMesh == null)
                {
                    report.Errors.Add("재가져오기 스킨 메시 연결 누락: " + path);
                    continue;
                }
                for (int i = 0; i < original.sharedMesh.blendShapeCount; i++)
                {
                    string key = original.sharedMesh.GetBlendShapeName(i);
                    if (changed.sharedMesh.GetBlendShapeIndex(key) < 0)
                        report.Errors.Add("재가져오기 얼굴 변형 누락: " + key);
                }
                if (original.sharedMesh.vertexCount != changed.sharedMesh.vertexCount)
                    report.ManualChecks.Add(
                        "토폴로지가 변경되어 접합 정점 번호를 다시 지정해야 합니다: " + path
                    );
                foreach (Transform bone in original.bones)
                {
                    if (bone == null)
                        continue;
                    string bonePath = AnimationUtility.CalculateTransformPath(
                        bone,
                        before.transform
                    );
                    Transform mapped = Find(after.transform, bonePath);
                    if (mapped == null)
                    {
                        report.Errors.Add("재가져오기 본 계층 누락: " + bonePath);
                        continue;
                    }
                    bool moved =
                        Vector3.Distance(bone.localPosition, mapped.localPosition)
                            > height * profile.SocketPositionHeightRatio
                        || Quaternion.Angle(bone.localRotation, mapped.localRotation)
                            > profile.SocketAngleDegrees
                        || Vector3.Distance(bone.localScale, mapped.localScale) > 0.0001f;
                    if (moved)
                    {
                        string detail = "본 기준 자세 변경: " + bonePath;
                        if (requireRestPose)
                            report.Errors.Add(detail);
                        else
                            report.ManualChecks.Add(detail);
                    }
                }
            }
            foreach (var slot in slots)
            {
                if (slot.attachment != TripoAttachmentKind.Rigid)
                    continue;
                Transform original = Find(before.transform, slot.bonePath);
                Transform changed = Find(after.transform, slot.bonePath);
                if (original == null || changed == null)
                {
                    report.Errors.Add(slot.name + ": 재가져오기 소켓 본 누락.");
                    continue;
                }
                Vector3 first = original.TransformPoint(slot.position);
                Vector3 second = changed.TransformPoint(slot.position);
                float distance = Vector3.Distance(
                    before.transform.InverseTransformPoint(first),
                    after.transform.InverseTransformPoint(second)
                );
                Quaternion firstRotation =
                    Quaternion.Inverse(before.transform.rotation)
                    * original.rotation
                    * Quaternion.Euler(slot.rotation);
                Quaternion secondRotation =
                    Quaternion.Inverse(after.transform.rotation)
                    * changed.rotation
                    * Quaternion.Euler(slot.rotation);
                Matrix4x4 slotTransform = Matrix4x4.TRS(
                    slot.position,
                    Quaternion.Euler(slot.rotation),
                    Vector3.one * slot.scale
                );
                Vector3 firstScale = (
                    before.transform.worldToLocalMatrix
                    * original.localToWorldMatrix
                    * slotTransform
                ).lossyScale;
                Vector3 secondScale = (
                    after.transform.worldToLocalMatrix * changed.localToWorldMatrix * slotTransform
                ).lossyScale;
                if (
                    distance > height * profile.SocketPositionHeightRatio
                    || Quaternion.Angle(firstRotation, secondRotation) > profile.SocketAngleDegrees
                    || Vector3.Distance(firstScale, secondScale) > 0.0001f
                )
                    report.Errors.Add(
                        slot.name
                            + ": 변경하지 않은 소켓의 위치·각도·크기가 허용 범위를 벗어났습니다."
                    );
            }
            return report;
        }

        private static Transform Find(Transform root, string path) =>
            string.IsNullOrEmpty(path) ? root : root.Find(path);
    }
}
