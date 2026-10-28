using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoResponseTests
    {
        [Test]
        public void Read_WhenTaskSucceeded_PreservesOutputAndCreditUsage()
        {
            const string json =
                "{\"code\":0,\"data\":{\"task_id\":\"rig\",\"status\":\"success\",\"credits_consumed\":25,\"output\":{\"model_url\":\"https://example.com/model.fbx\"}}}";
            TripoTask task = TripoResponse.Read<TripoTask>(json);
            Assert.That(task, Is.Not.Null);
            Assert.That(task.task_id, Is.EqualTo("rig"));
            Assert.That(task.output.model_url, Is.EqualTo("https://example.com/model.fbx"));
            Assert.That(task.credits_consumed, Is.EqualTo(25f));
        }

        [TestCase("{\"code\":2010,\"status\":\"error\",\"message\":\"secret text\"}", "2010")]
        [TestCase("{\"code\":1000,\"status\":\"error\"}", "1000")]
        [TestCase("{}", "응답")]
        [TestCase("not json", "응답")]
        public void Read_WhenApiRejectsOrReturnsMalformedBody_ReportsSafeError(
            string json,
            string expected
        )
        {
            Exception error = Assert.Catch(() => TripoResponse.Read<TripoTask>(json));
            StringAssert.Contains(expected, error.Message);
            StringAssert.DoesNotContain("secret text", error.Message);
        }
    }
}
