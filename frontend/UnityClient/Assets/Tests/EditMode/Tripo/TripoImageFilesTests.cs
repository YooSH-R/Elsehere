using System;
using System.IO;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoImageFilesTests
    {
        [TestCase("missing")]
        [TestCase("empty")]
        [TestCase("oversized")]
        [TestCase("unsupported")]
        [TestCase("corrupt")]
        public void Validate_WhenImageCannotBeUploaded_RejectsInput(string kind)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "tripo-image-"
                    + Guid.NewGuid().ToString("N")
                    + (kind == "unsupported" ? ".gif" : ".png")
            );
            try
            {
                if (kind == "oversized")
                {
                    using var stream = File.Create(path);
                    stream.SetLength(20 * 1024 * 1024 + 1);
                }
                else if (kind != "missing")
                {
                    File.WriteAllBytes(
                        path,
                        kind == "empty" ? Array.Empty<byte>() : new byte[] { 1, 2, 3 }
                    );
                }
                var images = new TripoImages(path, path, path, path);
                Assert.Throws<ArgumentException>(() => images.Validate());
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void Validate_WithDecodablePng_AcceptsInput()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "tripo-image-" + Guid.NewGuid().ToString("N") + ".png"
            );
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Assert.DoesNotThrow(() => new TripoImages(path, path, path, path).Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
