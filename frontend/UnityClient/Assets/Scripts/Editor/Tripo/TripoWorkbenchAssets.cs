using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoWorkbenchAssets
    {
        public const string Folder = "Assets/Data/Tripo";

        public static TripoAssetRecord CreateRecord(
            GameObject model,
            TripoAssetKind kind,
            TripoAttachmentKind attachment,
            string folder = Folder,
            string assetName = null
        )
        {
            string modelPath = AssetDatabase.GetAssetPath(model);
            string guid = AssetDatabase.AssetPathToGUID(modelPath);
            if (model == null || string.IsNullOrEmpty(guid))
                throw new ArgumentException("프로젝트에 저장된 모델을 선택하세요.", nameof(model));
            EnsureFolder(folder);
            string recordPath = folder + "/" + guid + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(recordPath);
            if (existing != null && existing.sourceModel == model)
                return existing;
            foreach (string key in AssetDatabase.FindAssets("t:TripoAssetRecord", new[] { folder }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(
                    AssetDatabase.GUIDToAssetPath(key)
                );
                if (candidate != null && candidate.sourceModel == model)
                    return candidate;
            }
            assetName = string.IsNullOrEmpty(assetName) ? ModelName(model) : assetName;
            recordPath = AssetDatabase.GenerateUniqueAssetPath(
                folder + "/" + assetName + "_Review.asset"
            );
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            record.sourceModel = model;
            record.assetName = assetName;
            record.kind = kind;
            record.attachment = attachment;
            record.requireHumanoid = kind == TripoAssetKind.Character;
            record.revision.activePath = modelPath;
            record.profile = EnsureProfile(folder);
            if (kind == TripoAssetKind.Character)
                record.commonBody = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TripoMasterRig.MasterPath
                );
            AssetDatabase.CreateAsset(record, recordPath);
            AssetDatabase.SaveAssets();
            return record;
        }

        public static string ModelName(GameObject model)
        {
            if (model == null)
                return "Character";
            string path = AssetDatabase.GetAssetPath(model);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == "Source" || name == "Model" || name == "Visual")
                name = Path.GetFileName(Path.GetDirectoryName(path));
            // 이전 생성 방식의 8자리 작업 식별자는 표시 이름에서 제외한다.
            name = System.Text.RegularExpressions.Regex.Replace(name, "_[a-fA-F0-9]{8}$", "");
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(name) ? "Character" : name;
        }

        public static TripoValidationProfile EnsureProfile(string folder = Folder)
        {
            EnsureFolder(folder);
            string path = folder + "/DefaultValidation.asset";
            var profile = AssetDatabase.LoadAssetAtPath<TripoValidationProfile>(path);
            if (profile != null)
                return profile;
            profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        [MenuItem("Tools/A305/Development/Tripo/Create Workbench Defaults")]
        public static void BuildDefaults()
        {
            EnsureProfile();
            string blender = Path.GetFullPath(
                "Library/TripoTools/blender-4.5.14-windows-x64/blender.exe"
            );
            if (File.Exists(blender) && !EditorPrefs.HasKey("A305.Tripo.BlenderExecutable"))
                EditorPrefs.SetString("A305.Tripo.BlenderExecutable", blender);
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string folder)
        {
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
                throw new ArgumentException("Assets 하위 폴더가 필요합니다.", nameof(folder));
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
