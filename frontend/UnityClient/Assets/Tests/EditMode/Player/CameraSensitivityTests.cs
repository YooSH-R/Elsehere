using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    public sealed class CameraSensitivityTests
    {
        private const string SharedSettingsPath = "Assets/Data/Player/PlayerCameraSettings.asset";

        [Test]
        public void EverySpaceCameraTurnsAsFastAsTheSharedCamera()
        {
            float shared = Sensitivity(SharedSettingsPath);
            string[] different = AssetDatabase
                .FindAssets("t:PlayerCameraSettings", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !Mathf.Approximately(Sensitivity(path), shared))
                .Select(path => $"{path}: {Sensitivity(path)}")
                .ToArray();

            Assert.That(
                different,
                Is.Empty,
                $"Mouse sensitivity must match the shared {shared} deg/px."
            );
        }

        private static float Sensitivity(string path)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(path);
            return new SerializedObject(settings).FindProperty("rotateDegreesPerPixel").floatValue;
        }
    }
}
