using UnityEngine;

namespace A305.Characters
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharacterSkinTone : MonoBehaviour
    {
        public static readonly Color ReferenceColor = new Color(1f, .78f, .60f, 1f);
        public static readonly int TintProperty = Shader.PropertyToID("_SkinTint");

        [SerializeField]
        private Color skinColor = ReferenceColor;

        [SerializeField]
        private Renderer[] skinRenderers = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock properties;
        public Color SkinColor => skinColor;

        public void Configure(Renderer[] targets)
        {
            ResetTint();
            skinRenderers = targets ?? System.Array.Empty<Renderer>();
            Apply();
        }

        public void SetSkinColor(Color color)
        {
            skinColor = color;
            Apply();
        }

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void OnDisable() => ResetTint();

        private void Apply()
        {
            if (!isActiveAndEnabled)
                return;
            Color target = skinColor.linear;
            Color reference = ReferenceColor.linear;
            SetTint(
                new Vector4(
                    target.r / reference.r,
                    target.g / reference.g,
                    target.b / reference.b,
                    1
                )
            );
        }

        private void ResetTint() => SetTint(Vector4.one);

        private void SetTint(Vector4 tint)
        {
            properties ??= new MaterialPropertyBlock();
            foreach (var renderer in skinRenderers)
            {
                if (renderer == null)
                    continue;
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    if (materials[slot] == null || !materials[slot].HasProperty(TintProperty))
                        continue;
                    renderer.GetPropertyBlock(properties, slot);
                    properties.SetVector(TintProperty, tint);
                    renderer.SetPropertyBlock(properties, slot);
                }
            }
        }
    }
}
