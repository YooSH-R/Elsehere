using System;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoCharacterLibrary : ScriptableObject
    {
        public GameObject referenceVisual;
        public string referenceFingerprint;
        public TripoCharacterPart head;
        public TripoCharacterPart dressedBody;

        public string CurrentFingerprint =>
            Hash128
                .Compute("common-character-v1:" + TripoCharacterFingerprint.Of(referenceVisual))
                .ToString();
        public bool IsCurrent =>
            referenceVisual != null && referenceFingerprint == CurrentFingerprint;
    }

    public static class TripoCharacterLibraryBuilder
    {
        public static TripoCharacterLibrary Build(GameObject source, string folder)
        {
            BuilderTools.ThrowIfPlaying();
            RequireNewFolder(folder);
            var report = TripoMasterRig.Inspect(source, TripoMasterWorkflow.RequireMaster());
            report.Errors.AddRange(TripoMasterMotionAudit.Inspect(source).Errors);
            if (!report.Passed)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
            BuilderTools.EnsureFolder(folder);
            var instance = Object.Instantiate(source);
            GameObject reference;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(
                        instance,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction
                    );
                int index = 0;
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    AssetDatabase.CreateAsset(
                        mesh,
                        folder + "/ReferenceSurface" + index++ + ".asset"
                    );
                    skin.sharedMesh = mesh;
                }
                reference = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Reference.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
            var library = ScriptableObject.CreateInstance<TripoCharacterLibrary>();
            library.referenceVisual = reference;
            library.referenceFingerprint = library.CurrentFingerprint;
            AssetDatabase.CreateAsset(library, folder + "/Library.asset");
            library.head = PreparePart(
                library,
                reference,
                TripoCharacterRegion.Head,
                folder + "/Head"
            );
            library.dressedBody = PreparePart(
                library,
                reference,
                TripoCharacterRegion.DressedBody,
                folder + "/DressedBody"
            );
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static TripoCharacterPart PreparePart(
            TripoCharacterLibrary library,
            GameObject source,
            TripoCharacterRegion region,
            string folder
        )
        {
            BuilderTools.ThrowIfPlaying();
            RequireNewFolder(folder);
            if (library == null || !library.IsCurrent)
                throw new InvalidOperationException(
                    "공통 기준이 없거나 변경되었습니다. 기준 버전을 먼저 확인하세요."
                );
            var report = TripoMasterRig.Inspect(source, TripoMasterWorkflow.RequireMaster());
            if (!report.Passed)
                throw new InvalidOperationException(
                    "마스터에 맞춰 준비한 모델이 필요합니다.\n" + string.Join("\n", report.Errors)
                );
            BuilderTools.EnsureFolder(folder);
            var instance = Object.Instantiate(source);
            GameObject model;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(
                        instance,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction
                    );
                var animator = instance.GetComponentInChildren<Animator>();
                int count = 0;
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = TripoCharacterSurface.Extract(skin, animator, region);
                    if (mesh.vertexCount == 0)
                    {
                        Object.DestroyImmediate(mesh);
                        Object.DestroyImmediate(skin);
                        continue;
                    }
                    AssetDatabase.CreateAsset(mesh, folder + "/Surface" + count++ + ".asset");
                    skin.sharedMesh = mesh;
                    skin.localBounds = mesh.bounds;
                }
                if (count == 0)
                    throw new InvalidOperationException("선택한 영역에 면이 없습니다.");
                model = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Part.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            var part = ScriptableObject.CreateInstance<TripoCharacterPart>();
            part.model = model;
            part.region = region;
            part.referenceFingerprint = library.referenceFingerprint;
            AssetDatabase.CreateAsset(part, folder + "/Part.asset");
            AssetDatabase.SaveAssets();
            return part;
        }

        private static void RequireNewFolder(string folder)
        {
            TripoJob.NormalizeOutputFolder(folder);
            if (System.IO.Directory.Exists(folder))
                throw new InvalidOperationException(
                    "기존 에셋 폴더를 덮어쓰지 않습니다: " + folder
                );
        }
    }
}
