using System;
using System.IO;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoJobStoreTests
    {
        [Test]
        public void SaveThenReload_PreservesLatestTaskAndSubmissionGuard()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "a305-tripo-store-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "job.json");
            try
            {
                var store = new TripoJobStore(path);
                var job = new TripoJob
                {
                    assetName = "Character",
                    generationTaskId = "generation",
                    stage = TripoStage.Rigging,
                };
                store.Save(job);
                job.submissionUncertain = true;
                store.Save(job);

                TripoJob restored = new TripoJobStore(path).Load();
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.generationTaskId, Is.EqualTo("generation"));
                Assert.That(restored.stage, Is.EqualTo(TripoStage.Rigging));
                Assert.That(restored.submissionUncertain, Is.True);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}
