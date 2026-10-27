using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoImagesTests
    {
        [Test]
        public void GetOrderedPaths_MapsUiDirectionsToTripoViewOrder()
        {
            var images = new TripoImages("front.png", "left.png", "right.png", "back.png");

            Assert.That(
                images.GetOrderedPaths(),
                Is.EqualTo(new[] { "front.png", "left.png", "back.png", "right.png" })
            );
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Validate_WhenAnyRequiredViewIsMissing_RejectsInput(int missingIndex)
        {
            string[] paths = { "front.png", "left.png", "right.png", "back.png" };
            paths[missingIndex] = " ";
            var images = new TripoImages(paths[0], paths[1], paths[2], paths[3]);

            Assert.Throws<System.ArgumentException>(() => images.Validate());
        }
    }
}
