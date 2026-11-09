using NUnit.Framework;
using UnityEngine;

namespace A305.Ambience.Tests
{
    public sealed class FootstepCatalogTests
    {
        [Test]
        public void RuntimeCatalogContainsSixShortIndividualSteps()
        {
            var catalog = Resources.Load<FootstepCatalog>(FootstepCatalog.ResourcePath);
            Assert.That(catalog, Is.Not.Null, "Run FootstepAudioBuilder.Build first.");
            Assert.That(catalog.Count, Is.EqualTo(6));
            for (int index = 0; index < catalog.Count; index++)
            {
                AudioClip step = catalog.Step(index);
                Assert.That(step, Is.Not.Null);
                Assert.That(step.channels, Is.EqualTo(1));
                Assert.That(step.length, Is.InRange(.05f, 1f));
            }
        }
    }
}
