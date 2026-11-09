using System.IO;
using System.Linq;
using A305.EditorTools.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Npc.Tests
{
    public class NpcCastDresserTests
    {
        private const string Code = "TEXTURE_RECOLOR_TEST";
        private const string Folder = NpcCastDresser.ColorFolder + "/" + Code;
        private GameObject copy;
        private NpcCastMember member;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            var source = NpcCast.Find("POLICE_PATROL_OFFICER");
            member = new NpcCastMember(
                Code,
                source.Look,
                source.Hair,
                source.Clothes,
                source.SecondClothes,
                source.Shoes,
                null
            );
            copy = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(member.Look.PrefabPath)
                );
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(copy);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Dress_PreservesTextureDetailsWithoutAGraphicsDevice()
        {
            NpcCastDresser.Dress(copy, member);
            AssertHasDetails();
        }

        [Test]
        public void Dress_RebuildsAFlatCachedTextureEvenWhenTheRecipeMatches()
        {
            NpcCastDresser.Dress(copy, member);
            var flat = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            flat.SetPixels32(
                Enumerable.Repeat(new Color32(205, 205, 205, 255), 1024 * 1024).ToArray()
            );
            File.WriteAllBytes(Folder + "/Body.png", flat.EncodeToPNG());
            Object.DestroyImmediate(flat);
            AssetDatabase.ImportAsset(Folder + "/Body.png", ImportAssetOptions.ForceUpdate);

            NpcCastDresser.Dress(copy, member);

            AssertHasDetails();
        }

        private static void AssertHasDetails()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(Folder + "/Body.png")), Is.True);
                Assert.That(texture.width, Is.EqualTo(1024));
                Assert.That(
                    texture.GetPixels32().Distinct().Take(65).Count(),
                    Is.GreaterThan(64),
                    "피부·머리·옷의 색이 단색으로 사라지면 안 됩니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
