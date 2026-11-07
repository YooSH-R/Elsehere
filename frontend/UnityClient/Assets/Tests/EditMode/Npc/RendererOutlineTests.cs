using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>커서를 올린 NPC의 모든 몸 그림에 테두리 재질을 덧붙였다가, 커서가 떠나면 원래 재질로 되돌리는지 본다.</summary>
    public class RendererOutlineTests
    {
        private readonly List<Object> created = new List<Object>();
        private Material outline;
        private Material skin;
        private Material cloth;

        [SetUp]
        public void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            outline = Track(new Material(shader) { name = "Outline" });
            skin = Track(new Material(shader) { name = "Skin" });
            cloth = Track(new Material(shader) { name = "Cloth" });
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object target in created)
            {
                Object.DestroyImmediate(target);
            }

            created.Clear();
        }

        [Test]
        public void Show_AddsOutlineAfterEveryRenderersOwnMaterials()
        {
            GameObject npc = CreateNpc(out Renderer body, out Renderer hair);
            var highlight = new RendererOutline(outline);

            highlight.Show(npc);

            Assert.That(highlight.Target, Is.SameAs(npc));
            Assert.That(body.sharedMaterials, Is.EqualTo(new[] { skin, outline }));
            Assert.That(hair.sharedMaterials, Is.EqualTo(new[] { cloth, outline }));
        }

        [Test]
        public void Hide_RestoresOriginalMaterials()
        {
            GameObject npc = CreateNpc(out Renderer body, out Renderer hair);
            var highlight = new RendererOutline(outline);
            highlight.Show(npc);

            highlight.Hide();

            Assert.That(highlight.Target, Is.Null);
            Assert.That(body.sharedMaterials, Is.EqualTo(new[] { skin }));
            Assert.That(hair.sharedMaterials, Is.EqualTo(new[] { cloth }));
        }

        [Test]
        public void Show_SameNpcAgain_AddsOutlineOnce()
        {
            GameObject npc = CreateNpc(out Renderer body, out _);
            var highlight = new RendererOutline(outline);

            highlight.Show(npc);
            highlight.Show(npc);

            Assert.That(body.sharedMaterials, Is.EqualTo(new[] { skin, outline }));
        }

        [Test]
        public void Show_AnotherNpc_RestoresPreviousNpc()
        {
            GameObject first = CreateNpc(out Renderer firstBody, out _);
            GameObject second = CreateNpc(out Renderer secondBody, out _);
            var highlight = new RendererOutline(outline);
            highlight.Show(first);

            highlight.Show(second);

            Assert.That(highlight.Target, Is.SameAs(second));
            Assert.That(firstBody.sharedMaterials, Is.EqualTo(new[] { skin }));
            Assert.That(secondBody.sharedMaterials, Is.EqualTo(new[] { skin, outline }));
        }

        [Test]
        public void Hide_AfterNpcWasDestroyed_DoesNotFail()
        {
            // 장소를 옮기면 커서를 올린 NPC가 먼저 사라질 수 있다.
            GameObject npc = CreateNpc(out _, out _);
            var highlight = new RendererOutline(outline);
            highlight.Show(npc);
            Object.DestroyImmediate(npc);

            Assert.DoesNotThrow(highlight.Hide);
            Assert.That(highlight.Target, Is.Null);
        }

        private GameObject CreateNpc(out Renderer body, out Renderer hair)
        {
            var npc = Track(new GameObject("Npc"));
            body = CreatePart("Body", npc.transform, skin);
            hair = CreatePart("Hair", npc.transform, cloth);
            return npc;
        }

        private Renderer CreatePart(string name, Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private T Track<T>(T target)
            where T : Object
        {
            created.Add(target);
            return target;
        }
    }
}
