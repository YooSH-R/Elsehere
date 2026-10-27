using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoUploadProbeTests
    {
        [TestCase(1)]
        [TestCase(4)]
        public async Task RunAsync_UploadsOnlySelectedImagesAndReportsCompletion(int count)
        {
            var paths = new string[count];
            for (int i = 0; i < count; i++)
                paths[i] = "image-" + i;
            var uploads = new List<string>();
            var completed = new List<int>();
            await TripoUploadProbe.RunAsync(
                paths,
                (path, token) =>
                {
                    uploads.Add(path);
                    return Task.FromResult("uploaded-token");
                },
                completed.Add,
                CancellationToken.None
            );
            Assert.That(uploads, Is.EqualTo(paths));
            Assert.That(completed.Count, Is.EqualTo(count));
            Assert.That(completed[completed.Count - 1], Is.EqualTo(count));
        }

        [Test]
        public void RunAsync_WhenUploadFails_StopsBeforeRemainingImages()
        {
            int uploads = 0;
            int completed = 0;
            var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
                TripoUploadProbe.RunAsync(
                    new[] { "front", "left", "back", "right" },
                    (path, token) =>
                    {
                        uploads++;
                        if (uploads == 2)
                            throw new InvalidOperationException("upload rejected");
                        return Task.FromResult("token");
                    },
                    value => completed = value,
                    CancellationToken.None
                )
            );
            Assert.That(error.Message, Is.EqualTo("upload rejected"));
            Assert.That(uploads, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void RunAsync_WhenCancelled_DoesNotUploadNextImage()
        {
            using var cancellation = new CancellationTokenSource();
            int uploads = 0;
            Assert.CatchAsync<OperationCanceledException>(() =>
                TripoUploadProbe.RunAsync(
                    new[] { "front", "left" },
                    (path, token) =>
                    {
                        uploads++;
                        cancellation.Cancel();
                        return Task.FromResult("token");
                    },
                    value => { },
                    cancellation.Token
                )
            );
            Assert.That(uploads, Is.EqualTo(1));
        }
    }
}
