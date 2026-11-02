using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Voice
{
    /// <summary>
    /// 녹음 설정 에셋과 녹음 프리팹, 마이크 녹음 확인용 Sandbox 씬을 만든다. 브라우저 마이크는 Editor에서 확인되지 않으므로
    /// Sandbox 씬만 담은 Web 빌드도 만든다.
    /// </summary>
    public static class VoiceBuilder
    {
        public const string MenuPath = "Tools/A305/Voice/Build";

        private const string SettingsPath = "Assets/Data/Voice/VoiceRecorderSettings.asset";
        private const string RecorderPrefabPath = "Assets/Prefabs/Voice/VoiceRecorder.prefab";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox/VoiceSandbox.unity";
        private const string WebBuildPath = "Builds/VoiceCheck";

        private const float PanelWidth = 820f;

        private static readonly Color BackgroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);
        private static readonly Color LevelBackgroundColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        private static readonly Color LevelFillColor = new Color(0.35f, 0.85f, 0.55f, 1f);
        private static readonly Color StatusColor = new Color(0.8f, 0.82f, 0.86f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            BuildSandbox();
            AssetDatabase.SaveAssets();
            Debug.Log($"마이크 녹음 확인 씬을 준비했습니다: {SandboxScenePath}");
        }

        [MenuItem("Tools/A305/Voice/Build Voice Check Web")]
        public static void BuildVoiceCheckWeb()
        {
            Build();
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { SandboxScenePath },
                WebBuildPath,
                BuildTarget.WebGL,
                BuildOptions.None
            );
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException(
                    $"마이크 녹음 확인 빌드 실패: {report.summary.result}"
                );
            }
        }

        /// <summary>녹음 설정(없을 때만)과 녹음 프리팹의 기능 구성을 맞춘다. 대화 화면도 이 프리팹을 쓴다.</summary>
        public static void PrepareAssets()
        {
            var settings = BuilderTools.LoadOrCreateAsset<VoiceRecorderSettings>(SettingsPath);
            BuilderTools.UpdateOrCreatePrefab(
                RecorderPrefabPath,
                "VoiceRecorder",
                root =>
                {
                    var recorder = BuilderTools.GetOrAddComponent<VoiceRecorder>(root);
                    BuilderTools.SetReference(recorder, "settings", settings);
                    BuilderTools.RequireReferences(recorder);
                }
            );
        }

        // Sandbox 씬은 확인용이라 다시 만들 때 덮어쓴다.
        private static void BuildSandbox()
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            BuilderTools.EnsureEventSystem(groups);

            var camera = BuilderTools
                .CreateEmpty("SandboxCamera", groups.Environment, Vector3.zero, Quaternion.identity)
                .gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.gameObject.AddComponent<AudioListener>();

            var recorder = BuilderTools
                .InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(RecorderPrefabPath, MenuPath),
                    groups.Systems,
                    Vector3.zero,
                    Quaternion.identity
                )
                .GetComponent<VoiceRecorder>();
            var audioSource = BuilderTools
                .CreateEmpty("PlaybackAudio", groups.Systems, Vector3.zero, Quaternion.identity)
                .gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var canvas = new GameObject("VoiceSandboxCanvas");
            canvas.transform.SetParent(groups.UI, false);
            UiBuilderTools.EnsureOverlayCanvas(canvas, 0, receivesClicks: true);
            var view = canvas.AddComponent<VoiceSandboxView>();

            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                canvas.transform,
                "Panel",
                new Vector2(0.5f, 0.5f),
                PanelWidth + 80f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "마이크 녹음 확인"
            );
            Button access = UiBuilderTools.FindOrCreateButton(
                panel,
                "AccessButton",
                "마이크 권한 요청",
                regular,
                PanelWidth
            );
            Button talk = UiBuilderTools.FindOrCreateButton(
                panel,
                "TalkButton",
                "말하기",
                bold,
                PanelWidth
            );
            Image levelFill = CreateLevelBar(panel);
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                panel,
                "Status",
                regular,
                22f,
                StatusColor
            );
            Button play = UiBuilderTools.FindOrCreateButton(
                panel,
                "PlayButton",
                "다시 듣기",
                regular,
                PanelWidth
            );

            BuilderTools.SetReference(view, "recorder", recorder);
            BuilderTools.SetReference(view, "accessButton", access);
            BuilderTools.SetReference(view, "talkButton", talk);
            BuilderTools.SetReference(
                view,
                "talkCaption",
                talk.transform.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(view, "levelFill", levelFill);
            BuilderTools.SetReference(view, "statusText", status);
            BuilderTools.SetReference(view, "playButton", play);
            BuilderTools.SetReference(view, "audioSource", audioSource);
            BuilderTools.RequireReferences(view);

            BuilderTools.SaveScene(groups.Scene, SandboxScenePath);
        }

        // 음량 막대. 채움 비율은 Image의 Filled 방식으로 바꾸므로 기본 UI 스프라이트를 쓴다.
        private static Image CreateLevelBar(Transform panel)
        {
            var bar = new GameObject("LevelBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel, false);
            bar.GetComponent<Image>().color = LevelBackgroundColor;
            var size = bar.AddComponent<LayoutElement>();
            size.preferredHeight = 24f;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(bar.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var image = fill.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.color = LevelFillColor;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = 0f;
            return image;
        }
    }
}
