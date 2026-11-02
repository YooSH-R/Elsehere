using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Characters.Tests
{
    public sealed class CharacterSkinToneTests
    {
        private GameObject first;
        private GameObject second;
        private Material shared;
        private CharacterSkinTone tone;
        private Renderer[] targets;

        [SetUp]
        public void SetUp()
        {
            shared = new Material(Shader.Find("Elsehere/CharacterSkin"));
            first = new GameObject("First NPC");
            second = new GameObject("Second NPC");
            targets = new Renderer[3];
            string[] names = { "Body", "Eyelid", "SpeakingLips" };
            for (int i = 0; i < targets.Length; i++)
            {
                var child = new GameObject(names[i]);
                child.transform.SetParent(first.transform);
                targets[i] = child.AddComponent<MeshRenderer>();
                targets[i].sharedMaterial = shared;
            }
            var other = second.AddComponent<MeshRenderer>();
            other.sharedMaterial = shared;
            var otherTone = second.AddComponent<CharacterSkinTone>();
            otherTone.Configure(new[] { other });
            otherTone.SetSkinColor(CharacterSkinTone.ReferenceColor);
            tone = first.AddComponent<CharacterSkinTone>();
            tone.Configure(targets);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(shared);
        }

        private static Vector4 Tint(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            return block.GetVector(CharacterSkinTone.TintProperty);
        }

        [Test]
        public void ColorReachesBodyLidsAndLipsWithoutChangingAnotherNpcOrSharedMaterial()
        {
            Vector4 original = shared.GetVector(CharacterSkinTone.TintProperty);
            tone.SetSkinColor(new Color(.62f, .40f, .27f));
            var value = Tint(targets[0]);
            Assert.That(value.x, Is.InRange(.01f, .99f));
            foreach (var target in targets)
                Assert.That(Tint(target), Is.EqualTo(value));
            Assert.That(Tint(second.GetComponent<Renderer>()), Is.EqualTo(Vector4.one));
            Assert.That(shared.GetVector(CharacterSkinTone.TintProperty), Is.EqualTo(original));
        }

        [Test]
        public void ReferenceColorRestoresTextureAndPreservesOtherRendererOverrides()
        {
            int property = Shader.PropertyToID("_OtherOverride");
            var block = new MaterialPropertyBlock();
            block.SetFloat(property, .37f);
            targets[0].SetPropertyBlock(block, 0);
            tone.SetSkinColor(Color.black);
            tone.SetSkinColor(CharacterSkinTone.ReferenceColor);
            foreach (var target in targets)
                Assert.That(Tint(target), Is.EqualTo(Vector4.one));
            targets[0].GetPropertyBlock(block, 0);
            Assert.That(block.GetFloat(property), Is.EqualTo(.37f));
        }

        [Test]
        public void DisableRestoresOriginalTintAndEnableReappliesChosenColor()
        {
            tone.SetSkinColor(new Color(.62f, .40f, .27f));
            var chosen = Tint(targets[0]);
            tone.enabled = false;
            Assert.That(Tint(targets[0]), Is.EqualTo(Vector4.one));
            tone.enabled = true;
            Assert.That(Tint(targets[0]), Is.EqualTo(chosen));
        }
    }
}
