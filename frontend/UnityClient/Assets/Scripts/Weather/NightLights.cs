using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Weather
{
    /// <summary>
    /// 밤에 밝히는 등과 발광 재질, 밤에만 켜는 가로등과 그 전구, 밤하늘로 칠하는 창밖 유리, 안에서 불을 켠 것처럼 빛나는 입점 건물 창.
    /// 재질은 사본을 칠해 원본 에셋을 바꾸지 않고, 다 쓰면 렌더러에 원본 재질을 돌려준다.
    /// 값은 늘 씬 디자인 값에서 계산하므로 여러 번 칠해도 쌓이지 않는다.
    /// </summary>
    internal sealed class NightLights
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int CoreStrengthId = Shader.PropertyToID("_CoreStrength");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");

        private readonly Light[] lamps;
        private readonly float[] lampIntensities;
        private readonly Light[] streetLamps;
        private readonly float[] streetLampIntensities;
        private readonly Light[] shopLamps;
        private readonly float[] shopLampIntensities;
        private readonly List<Glow> glows = new List<Glow>();
        private readonly List<Glow> streetLampGlows = new List<Glow>();
        private readonly List<Window> windows = new List<Window>();
        private readonly List<LitWindow> litWindows = new List<LitWindow>();
        private readonly Dictionary<Renderer, Material[]> designedMaterials =
            new Dictionary<Renderer, Material[]>();
        private readonly List<Material> copies = new List<Material>();

        /// <param name="renderers">발광 재질·창밖 유리를 찾을 렌더러. 보통 장소 씬 전체다.</param>
        /// <param name="litBuildingRenderers">입점한 상점·숙소 건물의 렌더러. 이 안의 창 유리만 밤에 불 켠 창이 된다.</param>
        /// <param name="streetLamps">밤에만 켜는 가로등 빛. 씬 디자인 세기는 밤에 켰을 때의 세기다.</param>
        /// <param name="shopLamps">마을 입점 건물 안의 실내등. 밤에는 가로등보다 큰 배율로 방을 밝힌다.</param>
        public NightLights(
            DaylightSettings settings,
            IEnumerable<Light> lamps,
            IEnumerable<Renderer> renderers,
            IEnumerable<Renderer> litBuildingRenderers = null,
            IEnumerable<Light> streetLamps = null,
            IEnumerable<Light> shopLamps = null
        )
        {
            this.lamps = lamps.Where(lamp => lamp != null).ToArray();
            lampIntensities = this.lamps.Select(lamp => lamp.intensity).ToArray();
            this.streetLamps = (streetLamps ?? Enumerable.Empty<Light>())
                .Where(lamp => lamp != null)
                .ToArray();
            streetLampIntensities = this.streetLamps.Select(lamp => lamp.intensity).ToArray();
            this.shopLamps = (shopLamps ?? Enumerable.Empty<Light>())
                .Where(lamp => lamp != null)
                .ToArray();
            shopLampIntensities = this.shopLamps.Select(lamp => lamp.intensity).ToArray();

            var glowMaterials = new HashSet<Material>(
                settings.GlowMaterials.Where(item => item != null)
            );
            var streetLampGlowMaterials = new HashSet<Material>(
                settings.NightOnlyGlowMaterials.Where(item => item != null)
            );
            var windowMaterials = new HashSet<Material>(
                settings.WindowMaterials.Where(item => item != null)
            );
            var copyOf = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
                SwapMaterials(
                    renderer,
                    designed =>
                    {
                        bool isGlow = glowMaterials.Contains(designed);
                        bool isStreetLampGlow = streetLampGlowMaterials.Contains(designed);
                        if (!isGlow && !isStreetLampGlow && !windowMaterials.Contains(designed))
                            return null;
                        return CopyOnce(
                            designed,
                            copyOf,
                            copy =>
                            {
                                if (isGlow)
                                    glows.Add(new Glow(copy));
                                else if (isStreetLampGlow)
                                    streetLampGlows.Add(new Glow(copy));
                                else
                                    windows.Add(new Window(copy));
                            }
                        );
                    }
                );

            // 같은 창 유리를 주택·빈 건물도 쓰므로 입점 건물에는 따로 사본을 둔다.
            var litWindowMaterials = new HashSet<Material>(
                settings.LitWindowMaterials.Where(item => item != null)
            );
            var litCopyOf = new Dictionary<Material, Material>();
            foreach (Renderer renderer in litBuildingRenderers ?? Enumerable.Empty<Renderer>())
                SwapMaterials(
                    renderer,
                    designed =>
                        litWindowMaterials.Contains(designed)
                            ? CopyOnce(
                                designed,
                                litCopyOf,
                                copy => litWindows.Add(new LitWindow(copy))
                            )
                            : null
                );
        }

        /// <param name="night">밤인 정도. 0이면 가로등은 꺼지고 나머지는 씬 디자인 그대로다.</param>
        public void Apply(DaylightSettings settings, float night)
        {
            float lampScale = Mathf.Lerp(1f, settings.NightLampScale, night);
            for (int index = 0; index < lamps.Length; index++)
                lamps[index].intensity = lampIntensities[index] * lampScale;
            float shopLampScale = Mathf.Lerp(1f, settings.NightShopLampScale, night);
            for (int index = 0; index < shopLamps.Length; index++)
                shopLamps[index].intensity = shopLampIntensities[index] * shopLampScale;
            float glowScale = Mathf.Lerp(1f, settings.NightGlowScale, night);
            foreach (Glow glow in glows)
                glow.Apply(glowScale);
            // 가로등은 해가 지면서 켜져 한밤에 다른 등과 같은 배율로 밝다. 낮에 꺼진 빛은 그리지 않는다.
            float streetLampScale = settings.NightLampScale * night;
            for (int index = 0; index < streetLamps.Length; index++)
            {
                streetLamps[index].intensity = streetLampIntensities[index] * streetLampScale;
                streetLamps[index].enabled = streetLampScale > 0f;
            }
            foreach (Glow glow in streetLampGlows)
                glow.Apply(settings.NightGlowScale * night);
            float windowEmission = Mathf.Lerp(1f, settings.NightWindowEmissionScale, night);
            foreach (Window window in windows)
                window.Apply(settings.NightWindowColor, night, windowEmission);
            foreach (LitWindow window in litWindows)
                window.Apply(settings.LitWindowColor, settings.LitWindowEmission, night);
        }

        public void Restore()
        {
            for (int index = 0; index < streetLamps.Length; index++)
            {
                if (streetLamps[index] == null)
                    continue;
                streetLamps[index].intensity = streetLampIntensities[index];
                streetLamps[index].enabled = true;
            }
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

        /// <summary>렌더러의 재질 가운데 copyFor가 사본을 주는 것만 바꾸고, 처음 바꿀 때 원본 재질 목록을 기억한다.</summary>
        private void SwapMaterials(Renderer renderer, Func<Material, Material> copyFor)
        {
            Material[] materials = renderer.sharedMaterials;
            bool swapped = false;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] == null)
                    continue;
                Material copy = copyFor(materials[index]);
                if (copy == null)
                    continue;
                materials[index] = copy;
                swapped = true;
            }
            if (!swapped)
                return;
            if (!designedMaterials.ContainsKey(renderer))
                designedMaterials.Add(renderer, renderer.sharedMaterials);
            renderer.sharedMaterials = materials;
        }

        private Material CopyOnce(
            Material designed,
            Dictionary<Material, Material> copyOf,
            Action<Material> created
        )
        {
            if (copyOf.TryGetValue(designed, out Material copy))
                return copy;
            copy = new Material(designed);
            copyOf.Add(designed, copy);
            copies.Add(copy);
            created(copy);
            return copy;
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

        /// <summary>입점한 상점·숙소의 창 유리. 밤에는 안에서 실내등을 켠 것처럼 따뜻한 빛을 낸다.</summary>
        private sealed class LitWindow
        {
            private readonly Material material;
            private readonly Color color;
            private readonly Color emission;

            public LitWindow(Material material)
            {
                this.material = material;
                color = material.color;
                if (!material.HasProperty(EmissionId))
                    return;
                emission = material.GetColor(EmissionId);
                material.EnableKeyword("_EMISSION");
            }

            public void Apply(Color litColor, Color litEmission, float night)
            {
                Color painted = Color.Lerp(color, litColor, night);
                painted.a = color.a;
                material.color = painted;
                if (material.HasProperty(EmissionId))
                    material.SetColor(EmissionId, Color.Lerp(emission, litEmission, night));
            }
        }
    }
}
