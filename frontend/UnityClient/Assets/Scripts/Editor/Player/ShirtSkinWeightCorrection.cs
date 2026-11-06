using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Player
{
    /// <summary>
    /// 리틀 멍 F·M 메시에서 셔츠 아랫단이 허벅지·팔 뼈를 따라가지 않도록 가중치를 고친다.
    /// 판단은 <see cref="ShirtSkinWeights"/>가 하고, 여기서는 메시의 색·높이·뼈 역할을 모아 넘긴다.
    /// </summary>
    public static class ShirtSkinWeightCorrection
    {
        public const string FemaleTexturePath = "Assets/Art/Player/LittleMung/BaseColor.png";
        public const string MaleMeshPath =
            "Assets/Art/Player/LittleMung/Male/LittleMungMaleSurface.asset";
        public const string MaleTexturePath =
            "Assets/Art/Player/LittleMung/Male/LittleMungMaleColor.png";
        private const string MaleVisualPath = "Assets/Prefabs/Player/Characters/Male.prefab";

        private static readonly string[] TorsoBones = { "Hips", "Spine", "Chest", "UpperChest" };
        private static readonly string[] LegParts = { "UpperLeg", "LowerLeg", "Foot", "Toes" };
        private static readonly string[] ArmParts =
        {
            "Shoulder",
            "UpperArm",
            "LowerArm",
            "Hand",
            "Thumb",
            "Index",
            "Middle",
            "Ring",
            "Pinky",
        };

        /// <summary>
        /// F는 발바닥 스킨을 다시 만들며 함께 고치고(Refresh Terrain Sole Skin),
        /// M은 메시를 직접 고친다.
        /// </summary>
        [MenuItem("Tools/A305/Player/Correct Shirt Skin Weights")]
        public static void CorrectAll()
        {
            if (!BuilderTools.CanBuild())
                return;
            LittleMungTerrainBuilder.RefreshSoleSkins();
            var maleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MaleMeshPath);
            var maleSkin = AssetDatabase
                .LoadAssetAtPath<GameObject>(MaleVisualPath)
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(skin => skin.sharedMesh == maleMesh);
            Apply(maleMesh, maleSkin.bones, MaleTexturePath);
            EditorUtility.SetDirty(maleMesh);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Mesh mesh, Transform[] bones, string texturePath)
        {
            string[] names = bones.Select(bone => bone.name).ToArray();
            int hips = IndexOf(names, "Hips");
            Vector3 hipsPosition = BindPosition(mesh, hips);
            Vector3 up = (BindPosition(mesh, IndexOf(names, "Head")) - hipsPosition).normalized;
            float Height(Vector3 point) => Vector3.Dot(point - hipsPosition, up);
            float elbow =
                (
                    Height(BindPosition(mesh, IndexOf(names, "Left_LowerArm")))
                    + Height(BindPosition(mesh, IndexOf(names, "Right_LowerArm")))
                ) / 2f;
            float[] heights = mesh.vertices.Select(Height).ToArray();
            mesh.boneWeights = ShirtSkinWeights.Correct(
                mesh.boneWeights,
                Colors(mesh, texturePath),
                heights,
                names.Select(RoleOf).ToArray(),
                hips,
                elbow
            );
        }

        private static Color32[] Colors(Mesh mesh, string texturePath)
        {
            // 가져오기 설정을 바꾸지 않도록 원본 파일을 따로 읽는다.
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(texturePath));
                return mesh
                    .uv.Select(uv =>
                        (Color32)
                            texture.GetPixel(
                                Mathf.FloorToInt(Mathf.Repeat(uv.x, 1f) * texture.width),
                                Mathf.FloorToInt(Mathf.Repeat(uv.y, 1f) * texture.height)
                            )
                    )
                    .ToArray();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static SkinBoneRole RoleOf(string name)
        {
            if (TorsoBones.Contains(name))
                return SkinBoneRole.Torso;
            if (LegParts.Any(part => name.Contains(part)))
                return SkinBoneRole.Leg;
            if (ArmParts.Any(part => name.Contains(part)))
                return SkinBoneRole.Arm;
            return SkinBoneRole.Other;
        }

        private static int IndexOf(string[] names, string name)
        {
            int index = System.Array.IndexOf(names, name);
            if (index < 0)
                throw new System.InvalidOperationException("Missing bone: " + name);
            return index;
        }

        private static Vector3 BindPosition(Mesh mesh, int bone)
        {
            return mesh.bindposes[bone].inverse.GetColumn(3);
        }
    }
}
