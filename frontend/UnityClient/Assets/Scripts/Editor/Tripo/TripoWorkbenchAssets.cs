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
            string folder = Folder
        )
        {
            string modelPath = AssetDatabase.GetAssetPath(model);
            string guid = AssetDatabase.AssetPathToGUID(modelPath);
            if (model == null || string.IsNullOrEmpty(guid))
                throw new ArgumentException("프로젝트에 저장된 모델을 선택하세요.", nameof(model));
            EnsureFolder(folder);
            string recordPath = folder + "/" + guid + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(recordPath);
            if (existing != null)
                return existing;
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            record.sourceModel = model;
            record.kind = kind;
            record.attachment = attachment;
            record.requireHumanoid = kind == TripoAssetKind.Character;
            record.revision.activePath = modelPath;
            record.profile = EnsureProfile(folder);
            AssetDatabase.CreateAsset(record, recordPath);
            AssetDatabase.SaveAssets();
            return record;
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
