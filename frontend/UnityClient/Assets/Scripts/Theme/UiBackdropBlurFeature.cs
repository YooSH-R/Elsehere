using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace A305.Theme
{
    /// <summary>
    /// 유리 창(A305/UI/Glass)이 비춰 볼 뒤 화면을 만든다. 게임 카메라가 반투명 물체를 그리기 전 화면을 줄여 복사하고
    /// 가로·세로로 두 번 흐리게 해 전역 텍스처 _A305UiBackdrop에 둔다. 화면 위 UI는 카메라가 다 그린 뒤에 그려지므로
    /// 같은 프레임의 흐린 화면을 쓴다. UI 자신은 흐림에 섞이지 않는다.
    /// </summary>
    public sealed class UiBackdropBlurFeature : ScriptableRendererFeature
    {
        private static readonly int BackdropId = Shader.PropertyToID("_A305UiBackdrop");
        private static readonly int ReadyId = Shader.PropertyToID("_A305UiBackdropReady");
        private static readonly int SpreadId = Shader.PropertyToID("_BlurSpread");

        [SerializeField]
        private Shader shader;

        [SerializeField]
        [Range(2, 8)]
        [Tooltip("화면을 몇 분의 1로 줄여 흐릴지. 클수록 가볍고 더 흐리다.")]
        private int downsample = 4;

        [SerializeField]
        [Range(0.5f, 4f)]
        [Tooltip("흐림 한 번에 퍼지는 거리(줄인 화면의 화소)")]
        private float spread = 1.6f;

        private Material material;
        private BlurPass pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/A305/UiBackdropBlur");
            if (shader == null)
                return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new BlurPass(material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents,
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData
        )
        {
            if (pass == null || renderingData.cameraData.camera.cameraType != CameraType.Game)
                return;
            material.SetFloat(SpreadId, spread);
            pass.Downsample = downsample;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Release();
            CoreUtils.Destroy(material);
            Shader.SetGlobalFloat(ReadyId, 0f);
        }

        private sealed class BlurPass : ScriptableRenderPass
        {
            private readonly Material material;
            private RTHandle blurred;
            private RTHandle scratch;

            public BlurPass(Material material)
            {
                this.material = material;
                // 카메라가 화면에 바로 그리면 읽을 수 없으므로 중간 화면을 쓰게 한다.
                requiresIntermediateTexture = true;
            }

            public int Downsample { get; set; } = 4;

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData
            )
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer)
                    return;

                RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
                descriptor.width = Mathf.Max(1, descriptor.width / Downsample);
                descriptor.height = Mathf.Max(1, descriptor.height / Downsample);
                descriptor.depthStencilFormat = UnityEngine
                    .Experimental
                    .Rendering
                    .GraphicsFormat
                    .None;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref blurred,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_A305UiBackdrop"
                );
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref scratch,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_A305UiBackdropScratch"
                );

                TextureHandle result = renderGraph.ImportTexture(blurred);
                TextureHandle temporary = renderGraph.ImportTexture(scratch);
                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(
                        resources.activeColorTexture,
                        result,
                        material,
                        0
                    ),
                    "A305 UI Backdrop Downsample"
                );
                for (int round = 0; round < 2; round++)
                {
                    renderGraph.AddBlitPass(
                        new RenderGraphUtils.BlitMaterialParameters(result, temporary, material, 1),
                        "A305 UI Backdrop Blur H"
                    );
                    renderGraph.AddBlitPass(
                        new RenderGraphUtils.BlitMaterialParameters(temporary, result, material, 2),
                        "A305 UI Backdrop Blur V"
                    );
                }

                Shader.SetGlobalTexture(BackdropId, blurred);
                Shader.SetGlobalFloat(ReadyId, 1f);
            }

            public void Release()
            {
                blurred?.Release();
                scratch?.Release();
            }
        }
    }
}
