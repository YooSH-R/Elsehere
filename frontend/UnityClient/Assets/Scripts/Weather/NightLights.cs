using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 밤에 밝히는 등과 발광 재질, 밤하늘로 칠하는 창밖 유리. 재질은 사본을 칠해 원본 에셋을 바꾸지 않고,
    /// 다 쓰면 렌더러에 원본 재질을 돌려준다. 값은 늘 씬 디자인 값에서 계산하므로 여러 번 칠해도 쌓이지 않는다.
    /// </summary>
    internal sealed class NightLights
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int CoreStrengthId = Shader.PropertyToID("_CoreStrength");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");

        private readonly Light[] lamps;
        private readonly float[] lampIntensities;
        private readonly List<Glow> glows = new List<Glow>();
        private readonly List<Window> windows = new List<Window>();
        private readonly List<KeyValuePair<Renderer, Material[]>> designedMaterials =
            new List<KeyValuePair<Renderer, Material[]>>();
        private readonly List<Material> copies = new List<Material>();

        public NightLights(
            DaylightSettings settings,
            IEnumerable<Light> lamps,
            IEnumerable<Renderer> renderers
        )
        {
            this.lamps = lamps.Where(lamp => lamp != null).ToArray();
            lampIntensities = this.lamps.Select(lamp => lamp.intensity).ToArray();
            var glowMaterials = new HashSet<Material>(
                settings.GlowMaterials.Where(item => item != null)
            );
            var windowMaterials = new HashSet<Material>(
                settings.WindowMaterials.Where(item => item != null)
            );
            var copyOf = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool swapped = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material designed = materials[index];
                    bool isGlow = designed != null && glowMaterials.Contains(designed);
                    bool isWindow = designed != null && windowMaterials.Contains(designed);
                    if (!isGlow && !isWindow)
                        continue;
                    if (!copyOf.TryGetValue(designed, out Material copy))
                    {
                        copy = new Material(designed);
                        copyOf.Add(designed, copy);
                        copies.Add(copy);
                        if (isGlow)
                            glows.Add(new Glow(copy));
                        else
                            windows.Add(new Window(copy));
                    }
                    materials[index] = copy;
                    swapped = true;
                }
                if (!swapped)
                    continue;
                designedMaterials.Add(
                    new KeyValuePair<Renderer, Material[]>(renderer, renderer.sharedMaterials)
                );
                renderer.sharedMaterials = materials;
            }
        }

        /// <param name="night">밤인 정도. 0이면 씬 디자인 그대로다.</param>
        public void Apply(DaylightSettings settings, float night)
        {
            float lampScale = Mathf.Lerp(1f, settings.NightLampScale, night);
            for (int index = 0; index < lamps.Length; index++)
                lamps[index].intensity = lampIntensities[index] * lampScale;
            float glowScale = Mathf.Lerp(1f, settings.NightGlowScale, night);
            foreach (Glow glow in glows)
                glow.Apply(glowScale);
            float windowEmission = Mathf.Lerp(1f, settings.NightWindowEmissionScale, night);
            foreach (Window window in windows)
                window.Apply(settings.NightWindowColor, night, windowEmission);
        }

        public void Restore()
        {
            foreach (KeyValuePair<Renderer, Material[]> designed in designedMaterials)
            {
                if (designed.Key != null)
                    designed.Key.sharedMaterials = designed.Value;
            }
            foreach (Material copy in copies)
            {
                if (Application.isPlaying)
                    Object.Destroy(copy);
                else
                    Object.DestroyImmediate(copy);
            }
        }

        private static Color Scale(Color color, float scale) =>
            new Color(color.r * scale, color.g * scale, color.b * scale, color.a);

        /// <summary>URP Lit의 발광색이나 Elsehere/WarmLamp 전구의 밝기를 키우는 재질.</summary>
        private sealed class Glow
        {
            private readonly Material material;
            private readonly Color emission;
            private readonly float coreStrength;
            private readonly float edgeStrength;

            public Glow(Material material)
            {
                this.material = material;
                if (material.HasProperty(EmissionId))
                    emission = material.GetColor(EmissionId);
                if (material.HasProperty(CoreStrengthId))
                    coreStrength = material.GetFloat(CoreStrengthId);
                if (material.HasProperty(EdgeStrengthId))
                    edgeStrength = material.GetFloat(EdgeStrengthId);
            }

            public void Apply(float scale)
            {
                if (material.HasProperty(EmissionId))
                    material.SetColor(EmissionId, Scale(emission, scale));
                if (material.HasProperty(CoreStrengthId))
                    material.SetFloat(CoreStrengthId, coreStrength * scale);
                if (material.HasProperty(EdgeStrengthId))
                    material.SetFloat(EdgeStrengthId, edgeStrength * scale);
            }
        }

        /// <summary>창밖 하늘을 그린 유리. 밤에는 밤하늘 색이 되고, 스스로 빛나던 유리는 어두워진다.</summary>
        private sealed class Window
        {
            private readonly Material material;
            private readonly Color color;
            private readonly Color emission;

            public Window(Material material)
            {
                this.material = material;
                color = material.color;
                if (material.HasProperty(EmissionId))
                    emission = material.GetColor(EmissionId);
            }

            public void Apply(Color nightColor, float night, float emissionScale)
            {
                Color painted = Color.Lerp(color, nightColor, night);
                painted.a = color.a;
                material.color = painted;
                if (material.HasProperty(EmissionId))
                    material.SetColor(EmissionId, Scale(emission, emissionScale));
            }
        }
    }
}
