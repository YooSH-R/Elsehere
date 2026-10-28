using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    internal static class MainstreetAtmosphere
    {
        private const string ProfilePath = "Assets/Data/World/MainstreetBlockout/ArtVolume.asset";

        internal static void ConfigureReferenceColor()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
                return;
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(0f);
                color.contrast.Override(10f);
                color.saturation.Override(8f);
                EditorUtility.SetDirty(color);
            }
            if (profile.TryGet(out Tonemapping tone))
            {
                tone.mode.Override(TonemappingMode.None);
                EditorUtility.SetDirty(tone);
            }
        }

        internal static void Apply(Transform environment, Transform lighting, Camera camera)
        {
            Light sun = lighting.GetComponentInChildren<Light>();
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new UnityEngine.Color(1f, 0.93f, 0.81f);
            sun.intensity = 1.7f;
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.08f;
            sun.shadowNormalBias = 0.3f;
            sun.shadowResolution = LightShadowResolution.VeryHigh;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new UnityEngine.Color(0.55f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new UnityEngine.Color(0.7f, 0.64f, 0.53f);
            RenderSettings.ambientGroundColor = new UnityEngine.Color(0.34f, 0.31f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 125f;
            RenderSettings.fogColor = new UnityEngine.Color(0.65f, 0.82f, 0.87f);

            const string skyPath = ArtFolder + "/SummerSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Elsehere/SummerSky"));
                sky.SetColor("_Zenith", new UnityEngine.Color(0.23f, 0.65f, 0.95f));
                sky.SetColor("_Horizon", new UnityEngine.Color(0.72f, 0.88f, 0.94f));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            var volume = Group("MainstreetColor", lighting, Vector3.zero)
                .gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0f);
                color.contrast.Override(10f);
                color.saturation.Override(8f);
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.15f);
                bloom.threshold.Override(1.1f);
                bloom.scatter.Override(0.6f);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.None);
                foreach (VolumeComponent component in profile.components)
                    AssetDatabase.AddObjectToAsset(component, profile);
            }
            volume.sharedProfile = profile;
            BuildClouds(environment);
        }

        private static void BuildClouds(Transform environment)
        {
            Transform clouds = Group("SummerClouds", environment, Vector3.zero);
            const string cloudPath = ArtFolder + "/CloudWhiteV2.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(cloudPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new UnityEngine.Color(1f, 0.98f, 0.95f));
                AssetDatabase.CreateAsset(material, cloudPath);
            }
            Vector3[] positions =
            {
                new Vector3(-17, 19, 48),
                new Vector3(13, 19, 60),
                new Vector3(-8, 20, 90),
            };
            foreach (Vector3 position in positions)
            {
                Transform cloud = Group("Cloud", clouds, position);
                SmoothBall("CloudBase", cloud, Vector3.zero, new Vector3(11, 2.8f, 3.5f), material);
                SmoothBall(
                    "CloudLeft",
                    cloud,
                    new Vector3(-2.8f, 1.1f, 0),
                    new Vector3(4.8f, 4.4f, 4.5f),
                    material
                );
                SmoothBall(
                    "CloudCentre",
                    cloud,
                    new Vector3(.5f, 2, 0),
                    new Vector3(5.2f, 5.5f, 4.8f),
                    material
                );
                SmoothBall(
                    "CloudRight",
                    cloud,
                    new Vector3(3.4f, .8f, 0),
                    new Vector3(4.2f, 3.8f, 3.8f),
                    material
                );
            }
            foreach (Renderer renderer in clouds.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
