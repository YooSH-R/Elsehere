using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoRevisionTests
    {
        [Test]
        public void Restore_AfterApply_ReturnsToPreviousVersionAndInvalidatesChecks()
        {
            var revision = new TripoRevision
            {
                activePath = "Assets/Current.fbx",
                previousPath = "Assets/Previous.fbx",
                candidatePath = "Assets/Pending.fbx",
                validationPassed = true,
                visuallyApproved = true,
                validatedFingerprint = "old",
            };
            revision.Restore();
            Assert.That(
                revision.candidatePath,
                Is.Empty,
                "복원 후 거부한 후보가 미리보기에 남으면 안 됩니다."
            );
            Assert.That(revision.activePath, Is.EqualTo("Assets/Previous.fbx"));
            Assert.That(revision.previousPath, Is.EqualTo("Assets/Current.fbx"));
            Assert.That(revision.validationPassed, Is.False);
            Assert.That(revision.visuallyApproved, Is.False);
        }

        [Test]
        public void Apply_WithoutCandidate_RejectsEvenWithMatchingValidation()
        {
            var revision = new TripoRevision
            {
                candidatePath = "",
                activePath = "Assets/Approved.fbx",
                validatedFingerprint = "same",
                validationPassed = true,
                visuallyApproved = true,
            };
            Assert.Throws<InvalidOperationException>(() => revision.Apply("same"));
            Assert.That(revision.activePath, Is.EqualTo("Assets/Approved.fbx"));
        }

        [Test]
        public void Apply_ValidatedCandidate_SwitchesActiveAndPreservesPrevious()
        {
            var revision = new TripoRevision
            {
                candidatePath = "Assets/Candidate.fbx",
                activePath = "Assets/Approved.fbx",
                validatedFingerprint = "same",
                validationPassed = true,
                visuallyApproved = true,
            };
            revision.Apply("same");
            Assert.That(revision.activePath, Is.EqualTo("Assets/Candidate.fbx"));
            Assert.That(revision.previousPath, Is.EqualTo("Assets/Approved.fbx"));
            Assert.That(revision.candidatePath, Is.Empty);
            Assert.That(revision.validationPassed, Is.False);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Apply_WithoutPassingChecksAndVisualApproval_LeavesActiveModel(
            bool passed,
            bool approved
        )
        {
            var revision = new TripoRevision
            {
                candidatePath = "Assets/Candidate.fbx",
                activePath = "Assets/Approved.fbx",
                validatedFingerprint = "same",
                validationPassed = passed,
                visuallyApproved = approved,
            };
            Assert.Throws<InvalidOperationException>(() => revision.Apply("same"));
            Assert.That(revision.activePath, Is.EqualTo("Assets/Approved.fbx"));
        }

        [Test]
        public void Apply_AfterCandidateChanged_RejectsOldValidationAndKeepsActiveModel()
        {
            var revision = new TripoRevision
            {
                candidatePath = "Assets/Candidate.fbx",
                activePath = "Assets/Approved.fbx",
                validatedFingerprint = "original-model-and-settings",
                validationPassed = true,
                visuallyApproved = true,
            };

            Assert.Throws<InvalidOperationException>(() =>
                revision.Apply("changed-model-or-settings")
            );
            Assert.That(revision.activePath, Is.EqualTo("Assets/Approved.fbx"));
        }
    }
}
