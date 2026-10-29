using System;
using System.IO;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoModelImporter
    {
        public static bool CopySource(string source, string destination)
        {
            if (File.Exists(destination))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            File.Copy(source, destination, false);
            return true;
        }

        public static GameObject Import(TripoJob job)
        {
            BuilderTools.ThrowIfPlaying();
            CopySource(job.modelPath, job.modelAssetPath);
            AssetDatabase.ImportAsset(
                job.modelAssetPath,
                ImportAssetOptions.ForceSynchronousImport
            );
            if (!job.modelConfigured)
            {
                ConfigureModel(job.modelAssetPath, job.autoRig);
                job.modelConfigured = true;
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(job.modelAssetPath);
            if (job.autoRig)
                TripoModelReview.RequireHumanoid(model);
            return model;
        }

        public static void ConfigureModel(string path, bool humanoid = true)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "다운로드한 파일을 3D 모델로 가져오지 못했습니다."
                );
            importer.animationType = humanoid
                ? ModelImporterAnimationType.Human
                : ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes = true;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();

            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            BuilderTools.EnsureFolder(folder + "/Textures");
            BuilderTools.EnsureFolder(folder + "/Materials");

            TripoMaterialImport materials = TripoMaterialImport.Extract(path, folder + "/Textures");
            int materialIndex = 0;
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(subAsset is Material source))
                    continue;
                string materialPath = folder + "/Materials/Material_" + materialIndex++ + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        throw new InvalidOperationException("URP Lit 셰이더를 찾을 수 없습니다.");
                    material = new Material(shader) { name = source.name };
                    CopyMaterial(source, material);
                    materials.Apply(source.name, material);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name),
                    material
                );
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
        }

        private static void CopyMaterial(Material source, Material target)
        {
            string color = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (source.HasProperty(color))
                target.SetColor("_BaseColor", source.GetColor(color));
            string albedo = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (source.HasProperty(albedo))
            {
                target.SetTexture("_BaseMap", source.GetTexture(albedo));
                target.SetTextureOffset("_BaseMap", source.GetTextureOffset(albedo));
                target.SetTextureScale("_BaseMap", source.GetTextureScale(albedo));
            }
            CopyTexture(source, target, "_BumpMap", "_NORMALMAP");
            CopyTexture(source, target, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP");
            if (source.HasProperty("_Metallic"))
                target.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            if (source.HasProperty("_Glossiness"))
                target.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
        }

        private static void CopyTexture(
            Material source,
            Material target,
            string property,
            string keyword
        )
        {
            if (!source.HasProperty(property) || source.GetTexture(property) == null)
                return;
            target.SetTexture(property, source.GetTexture(property));
            target.EnableKeyword(keyword);
        }
    }
}
