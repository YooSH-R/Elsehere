using System;
using UnityEditor;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoMasterBindingStamp
    {
        public const int CurrentVersion = 5;
        public int version;
        public string sourceHash;
        public string masterHash;
        public string preparedHash;

        public static TripoMasterBindingStamp Capture(TripoAssetRecord record) =>
            new TripoMasterBindingStamp
            {
                version = CurrentVersion,
                sourceHash = Hash(AssetDatabase.GetAssetPath(record.sourceModel)),
                masterHash = Hash(AssetDatabase.GetAssetPath(record.commonBody)),
                preparedHash = Hash(record.masterPreparedPath),
            };

        public static TripoAuditReport Inspect(TripoAssetRecord record)
        {
            var report = new TripoAuditReport();
            var stamp = record.masterBinding;
            if (
                stamp == null
                || stamp.version != CurrentVersion
                || string.IsNullOrEmpty(record.masterPreparedPath)
            )
            {
                report.Errors.Add(
                    "마스터 메시 재연결 정보가 없거나 이전 버전입니다. 원본을 재생성하지 말고 서비스 마스터를 다시 적용하세요."
                );
                return report;
            }
            if (stamp.sourceHash != Hash(AssetDatabase.GetAssetPath(record.sourceModel)))
                report.Errors.Add("생성 원본이 변경되어 마스터 메시 재연결이 필요합니다.");
            if (stamp.masterHash != Hash(AssetDatabase.GetAssetPath(record.commonBody)))
                report.Errors.Add("마스터가 변경되어 메시 재연결이 필요합니다.");
            if (stamp.preparedHash != Hash(record.masterPreparedPath))
                report.Errors.Add(
                    "마스터 기준 메시가 변경되어 재연결 결과를 확인해야 합니다. 외형 편집본은 별도 후보로 가져오세요."
                );
            return report;
        }

        private static string Hash(string path) =>
            string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null
                ? "missing"
                : AssetDatabase.GetAssetDependencyHash(path).ToString();
    }
}
