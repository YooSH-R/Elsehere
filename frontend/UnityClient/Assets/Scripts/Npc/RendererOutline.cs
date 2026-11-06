using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 대상 아래 모든 몸 그림(메시)에 테두리 재질을 하나 더 그려 강조하고, 지우면 원래 재질 목록으로 되돌린다.
    /// 재질 에셋은 건드리지 않고 그림마다 재질 목록만 바꾼다. 한 번에 한 대상만 강조한다.
    /// </summary>
    /// <remarks>
    /// 덧붙인 재질은 그림의 마지막 부분 메시에 그려진다. NPC의 몸·옷·머리는 그림마다 부분 메시가 하나라 전체에 그려진다.
    /// </remarks>
    public sealed class RendererOutline
    {
        private readonly Material outlineMaterial;
        private readonly List<Renderer> foundRenderers = new List<Renderer>();
        private readonly List<(Renderer Renderer, Material[] Materials)> originals =
            new List<(Renderer, Material[])>();

        public RendererOutline(Material outlineMaterial)
        {
            this.outlineMaterial = outlineMaterial;
        }

        /// <summary>테두리를 그린 대상. 없으면 null</summary>
        public GameObject Target { get; private set; }

        public void Show(GameObject target)
        {
            if (Target == target)
            {
                return;
            }

            Hide();
            Target = target;
            target.GetComponentsInChildren(foundRenderers);
            foreach (Renderer renderer in foundRenderers)
            {
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                {
                    AddOutline(renderer);
                }
            }
        }

        public void Hide()
        {
            foreach ((Renderer renderer, Material[] materials) in originals)
            {
                // 장소를 옮기면 NPC가 먼저 사라질 수 있다. 사라진 그림은 되돌릴 필요가 없다.
                if (renderer != null)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            originals.Clear();
            Target = null;
        }

        private void AddOutline(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            originals.Add((renderer, materials));
            var outlined = new Material[materials.Length + 1];
            materials.CopyTo(outlined, 0);
            outlined[materials.Length] = outlineMaterial;
            renderer.sharedMaterials = outlined;
        }
    }
}
