using System;
using System.IO;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoCharacterHeadFitBuilder
    {
        public const string Folder = TripoCharacterBootstrap.Folder + "/FittedHead";

        public static TripoCharacterPart Prepare(
            TripoCharacterLibrary library,
            TripoCharacterPart source,
            string folder
        )
        {
            BuilderTools.ThrowIfPlaying();
            TripoJob.NormalizeOutputFolder(folder);
            if (Directory.Exists(folder))
                throw new InvalidOperationException("기존 수정 사본을 덮어쓰지 않습니다.");
            if (
                library == null
                || !library.IsCurrent
                || source == null
                || source.region != TripoCharacterRegion.Head
                || source.referenceFingerprint != library.referenceFingerprint
            )
                throw new InvalidOperationException(
                    "같은 공통 기준으로 준비한 머리 부품이 필요합니다."
                );
            BuilderTools.EnsureFolder(folder);
            var instance = Object.Instantiate(source.model);
            GameObject prefab;
            try
            {
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var references = library.head.model.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length != 1 || references.Length != 1)
                    throw new InvalidOperationException(
                        "단일 스킨 머리만 자동 연결할 수 있습니다."
                    );
                var skin = skins[0];
                var mesh = TripoCharacterHeadFit.Fit(skin, references[0]);
                AssetDatabase.CreateAsset(mesh, folder + "/Surface.asset");
                skin.sharedMesh = mesh;
                skin.localBounds = mesh.bounds;
                prefab = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Part.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            var part = ScriptableObject.CreateInstance<TripoCharacterPart>();
            part.model = prefab;
            part.region = TripoCharacterRegion.Head;
            part.referenceFingerprint = library.referenceFingerprint;
            AssetDatabase.CreateAsset(part, folder + "/Part.asset");
            AssetDatabase.SaveAssets();
            return part;
        }

        public static void BuildFemaleCandidate()
        {
            if (Directory.Exists(Folder))
                throw new InvalidOperationException("기존 수정 사본을 덮어쓰지 않습니다.");
            var library = AssetDatabase.LoadAssetAtPath<TripoCharacterLibrary>(
                TripoCharacterBootstrap.LibraryPath
            );
            var source = AssetDatabase.LoadAssetAtPath<TripoCharacterPart>(
                TripoCharacterBootstrap.Folder + "/Heads/Female/Part.asset"
            );
            var part = Prepare(library, source, Folder + "/Head");
            var recipe = TripoCharacterAssembly.Build(
                library,
                part,
                library.dressedBody,
                Folder + "/FemaleCasualFitted"
            );
            var report = TripoCharacterAssembly.Validate(recipe);
            if (!report.Passed)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
            string path = AssetDatabase.GetAssetPath(recipe);
            string scene = TripoCharacterBootstrap.BuildScene(recipe);
            File.WriteAllText(
                Environment.GetEnvironmentVariable("TRIPO_HEAD_FIT_REPORT"),
                JsonUtility.ToJson(new Result { composition = path, scene = scene }, true)
            );
        }

        [Serializable]
        class Result
        {
            public string composition,
                scene;
        }
    }
}
