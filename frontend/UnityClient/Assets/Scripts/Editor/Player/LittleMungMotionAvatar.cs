using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Player
{
    /// <summary>FBX의 실제 휴식 자세로 만든 기준 Avatar를 같은 골격의 모션에 연결한다.</summary>
    public static class LittleMungMotionAvatar
    {
        public const string Folder = "Assets/Art/Player/LittleMung/Motions";

        public static void Prepare()
        {
            string referencePath = Folder + "/Reference.fbx";
            if (!File.Exists(referencePath))
                throw new InvalidOperationException(
                    "휴식 자세 기준 FBX가 없습니다: " + referencePath
                );
            var importer = (ModelImporter)AssetImporter.GetAtPath(referencePath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(referencePath).OfType<Avatar>().Single();
            if (!avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("휴식 자세 기준 Avatar가 유효하지 않습니다.");
            foreach (string name in new[] { "Walk", "Run", "Jump" })
            {
                string path = Folder + "/" + name + ".fbx";
                ValidateSkeleton(referencePath, path);
                var target = (ModelImporter)AssetImporter.GetAtPath(path);
                target.animationType = ModelImporterAnimationType.Human;
                target.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                target.sourceAvatar = avatar;
                target.SaveAndReimport();
            }
        }

        private static void ValidateSkeleton(string referencePath, string candidatePath)
        {
            var reference = AssetDatabase
                .LoadAssetAtPath<GameObject>(referencePath)
                .GetComponentsInChildren<Transform>()
                .ToDictionary(t => t.name);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
            if (asset == null)
                throw new InvalidOperationException("변환된 모션 FBX가 없습니다: " + candidatePath);
            var candidate = asset.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            foreach (var pair in reference)
            {
                if (!pair.Key.StartsWith("mixamorig:") || pair.Key == "mixamorig:Hips")
                    continue;
                if (
                    !candidate.TryGetValue(pair.Key, out var bone)
                    || bone.parent.name != pair.Value.parent.name
                    || Vector3.Distance(bone.localPosition, pair.Value.localPosition) > 0.001f
                )
                    throw new InvalidOperationException(
                        "공유 Avatar와 골격 규격이 다릅니다: " + candidatePath + " / " + pair.Key
                    );
            }
        }
    }
}
