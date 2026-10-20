using A305.Player;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tests.Player
{
    public class CameraOrbitTests
    {
        private const float Tolerance = 0.001f;

        private static readonly CameraOrbitLimits Limits = new CameraOrbitLimits(
            minDistanceMeters: 2f,
            maxDistanceMeters: 8f,
            zoomStepMeters: 1f,
            thirdPersonMinPitchDegrees: 5f,
            thirdPersonMaxPitchDegrees: 70f,
            firstPersonMinPitchDegrees: -60f,
            firstPersonMaxPitchDegrees: 70f,
            rotateDegreesPerPixel: 0.2f
        );

        private CameraOrbit orbit;

        [SetUp]
        public void CreateOrbit()
        {
            orbit = new CameraOrbit(Limits, startDistanceMeters: 5f, startPitchDegrees: 30f);
        }

        [Test]
        public void NewOrbit_StartsInThirdPerson()
        {
            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
            Assert.That(orbit.DistanceMeters, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(orbit.PitchDegrees, Is.EqualTo(30f).Within(Tolerance));
        }

        [Test]
        public void Rotate_HorizontalDrag_TurnsYawBySensitivity()
        {
            orbit.Rotate(new Vector2(100f, 0f));

            Assert.That(orbit.YawDegrees, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void Rotate_DragUp_LooksUpOnlyToThirdPersonMinimumPitch()
        {
            orbit.Rotate(new Vector2(0f, 1000f));

            Assert.That(orbit.PitchDegrees, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void Rotate_DragDown_LooksDownOnlyToMaximumPitch()
        {
            orbit.Rotate(new Vector2(0f, -1000f));

            Assert.That(orbit.PitchDegrees, Is.EqualTo(70f).Within(Tolerance));
        }

        [Test]
        public void Zoom_In_StopsAtMinimumDistance()
        {
            orbit.Zoom(10f);

            Assert.That(orbit.DistanceMeters, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Zoom_Out_StopsAtMaximumDistance()
        {
            orbit.Zoom(-10f);

            Assert.That(orbit.DistanceMeters, Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void ToggleFirstPerson_SwitchesBetweenThirdAndFirstPerson()
        {
            orbit.ToggleFirstPerson();
            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.FirstPerson));

            orbit.ToggleFirstPerson();
            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        [Test]
        public void ToggleFirstPerson_InFirstPerson_CanLookUpBeyondThirdPersonLimit()
        {
            orbit.ToggleFirstPerson();

            orbit.Rotate(new Vector2(0f, 1000f));

            Assert.That(orbit.PitchDegrees, Is.EqualTo(-60f).Within(Tolerance));
        }

        [Test]
        public void ToggleFirstPerson_BackToThirdPerson_KeepsDistanceAndClampsPitch()
        {
            orbit.Zoom(-1f);
            orbit.ToggleFirstPerson();
            orbit.Rotate(new Vector2(0f, 1000f));

            orbit.ToggleFirstPerson();

            Assert.That(orbit.DistanceMeters, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(orbit.PitchDegrees, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void Zoom_InFirstPerson_KeepsDistance()
        {
            orbit.ToggleFirstPerson();

            orbit.Zoom(1f);

            Assert.That(orbit.DistanceMeters, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void EnterDialogueView_IgnoresRotateZoomAndToggle()
        {
            orbit.EnterDialogueView();

            orbit.Rotate(new Vector2(100f, 100f));
            orbit.Zoom(1f);
            orbit.ToggleFirstPerson();

            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.Dialogue));
            Assert.That(orbit.YawDegrees, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(orbit.PitchDegrees, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(orbit.DistanceMeters, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void ExitDialogueView_RestoresViewModeBeforeDialogue()
        {
            orbit.ToggleFirstPerson();
            orbit.Rotate(new Vector2(225f, -50f));

            orbit.EnterDialogueView();
            orbit.ExitDialogueView();

            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.FirstPerson));
            Assert.That(orbit.YawDegrees, Is.EqualTo(45f).Within(Tolerance));
            Assert.That(orbit.PitchDegrees, Is.EqualTo(40f).Within(Tolerance));
        }

        [Test]
        public void EnterDialogueView_Twice_ExitStillRestoresViewBeforeDialogue()
        {
            orbit.EnterDialogueView();
            orbit.EnterDialogueView();

            orbit.ExitDialogueView();

            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        [Test]
        public void ExitDialogueView_WhenNotInDialogue_KeepsCurrentView()
        {
            orbit.ToggleFirstPerson();

            orbit.ExitDialogueView();

            Assert.That(orbit.ViewMode, Is.EqualTo(CameraViewMode.FirstPerson));
        }

        [Test]
        public void GetThirdPersonPose_PlacesCameraBehindAndAbovePivotLookingAtIt()
        {
            Vector3 pivot = new Vector3(1f, 2f, 3f);

            Pose pose = orbit.GetThirdPersonPose(pivot);

            Vector3 expectedOffset = new Vector3(
                0f,
                5f * Mathf.Sin(30f * Mathf.Deg2Rad),
                -5f * Mathf.Cos(30f * Mathf.Deg2Rad)
            );
            AssertVector(pivot + expectedOffset, pose.position);
            AssertVector((pivot - pose.position).normalized, pose.forward);
        }

        [Test]
        public void GetFirstPersonPose_StaysAtEyeAndUsesYawAndPitch()
        {
            orbit.ToggleFirstPerson();
            orbit.Rotate(new Vector2(450f, 0f));
            Vector3 eye = new Vector3(0f, 1.6f, 0f);

            Pose pose = orbit.GetFirstPersonPose(eye);

            AssertVector(eye, pose.position);
            AssertVector(Quaternion.Euler(30f, 90f, 0f) * Vector3.forward, pose.forward);
        }

        [Test]
        public void GetDialoguePose_StaysAtEyeAndFacesTarget()
        {
            Vector3 eye = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(2f, 1.6f, 2f);

            Pose pose = CameraOrbit.GetDialoguePose(eye, target);

            AssertVector(eye, pose.position);
            AssertVector((target - eye).normalized, pose.forward);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), "z");
        }
    }
}
