using System;
using System.IO;
using System.Threading;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed partial class TripoWindow : EditorWindow
    {
        private const string StatePath = "UserSettings/Tripo/current-job.json";
        private static readonly string[] ViewNames = { "정면", "좌측면", "우측면", "후면" };
        private static readonly string[] GenerationModels = { TripoJob.H31Model, TripoJob.P2Model };
        private static readonly string[] GenerationModelNames =
        {
            "H v3.1",
            "Smart Mesh / P2 Preview",
        };
        private static readonly string[] StageNames =
        {
            "이미지 업로드",
            "3D 생성",
            "리깅 가능 여부 검사",
            "자동 리깅",
            "FBX 다운로드",
            "로컬 가져오기·마스터 적용·자동 검사",
            "자동 검사 통과 · 외관 확인 대기",
            "원격 작업 실패",
            "FBX 변환",
        };

        [SerializeField]
        private string assetName = "Character";

        [SerializeField]
        private string[] paths = new string[4];

        [SerializeField]
        private int faceLimit = 15000;

        [SerializeField]
        private int generationModelIndex;

        [SerializeField]
        private float heightMeters = 1.7f;

        // API 키는 Editor 레이아웃, 프로젝트 파일, 작업 JSON에 직렬화하지 않는다.
        [NonSerialized]
        private string apiKey = "";
        private readonly Texture2D[] previews = new Texture2D[4];
        private TripoJobStore store;
        private TripoJob job;
        private TripoPipeline pipeline;
        private CancellationTokenSource cancellation;
        private bool running;
        private bool inFlight;
        private bool testingUploads;
        private int testedImageCount;
        private int testImageTotal;
        private double nextPoll;
        private double startedAt;
        private string message = "";
        private bool messageIsError;
        private double nextStatusRepaint;
        private string recoveryTaskId = "";
        private Vector2 scroll;

        [SerializeField]
        private int creationSource;
        private bool showGenerationHelp;
        private bool showGenerationOptions;
        private bool showJobDetails;

        [SerializeField]
        private string modelOutputFolder = "Assets/Art/Characters/Tripo";

        [SerializeField]
        private string prefabOutputFolder = "Assets/Prefabs/Characters/Tripo";

        [MenuItem("Tools/A305/Assets/Tripo Workbench")]
        public static void Open()
        {
            GetWindow<TripoWindow>("Tripo Workbench");
        }

        private void OnEnable()
        {
            minSize = new Vector2(320f, 360f);
            store = new TripoJobStore(StatePath);
            try
            {
                job = store.Load();
                if (record == null && job != null && !string.IsNullOrEmpty(job.reviewRecordPath))
                    record = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(job.reviewRecordPath);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
            EditorApplication.update += Tick;
            EditorApplication.update += UpdateSpeech;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            Stop();
            ResetPreview();
            blenderCancellation?.Cancel();
            StopSpeech();
            EditorApplication.update -= UpdateSpeech;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            foreach (Texture2D preview in previews)
                if (preview != null)
                    DestroyImmediate(preview);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Stop();
                ResetPreview();
                blenderCancellation?.Cancel();
            }
        }

        private void DrawGeneration()
        {
            if (GUILayout.Button("공통 바디로 캐릭터 조합 · API 사용 없음"))
                TripoCharacterWindow.Open();
            EditorGUILayout.HelpBox(
                "이미지 → 생성·리깅 각 1회 → 로컬 마스터 보정·동작 검사 → 마지막 외관 확인 → 최종 등록. 검사 실패 시 등록을 차단하며 자동 재생성하지 않습니다.",
                MessageType.Info
            );
            creationSource = GUILayout.SelectionGrid(
                creationSource,
                new[] { "API 생성", "웹·파일 가져오기" },
                CompactView ? 1 : 2
            );
            EditorGUILayout.Space();
            if (creationSource == 0)
                DrawApiGeneration();
            else
                DrawWebImport();
            DrawGenerationHelp();
        }

        private void DrawWebImport()
        {
            EditorGUILayout.LabelField("웹에서 생성한 모델 가져오기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Studio에서 생성·리깅한 모델을 Bridge로 보내거나 FBX 파일로 가져오세요. 이 가져오기에는 API 키가 필요 없습니다.",
                MessageType.Info
            );
            if (GUILayout.Button("Tripo Studio 열기"))
                Application.OpenURL("https://studio.tripo3d.ai/");
            if (GUILayout.Button("공식 DCC Bridge 열기"))
            {
                if (!EditorApplication.ExecuteMenuItem("Tools/Tripo Bridge"))
                    message = "Bridge가 설치되지 않았습니다. 도움말의 설치 안내를 확인하세요.";
            }
            EditorGUILayout.LabelField(
                "웹: DCC Bridge → Unity 연결 후 Export → Send to Unity. 받은 모델은 아래에서 선택하세요.",
                EditorStyles.wordWrappedMiniLabel
            );
            DrawModelImport();
        }

        private void DrawGenerationHelp()
        {
            EditorGUILayout.Space();
            showGenerationHelp = EditorGUILayout.Foldout(
                showGenerationHelp,
                "도움말 · 이미지 준비 / 계정 / 연결",
                true
            );
            if (!showGenerationHelp)
                return;
            DrawAssetPreparationGuide();
            EditorGUILayout.LabelField(
                "가져올 때 Blender를 백그라운드로 실행해 텍스처를 준비합니다. 생성 전에 Blender가 설치되어 있어야 합니다.",
                EditorStyles.wordWrappedLabel
            );
            if (GUILayout.Button("텍스처 자동 준비 · Blender 실행 파일 선택"))
            {
                string selected = EditorUtility.OpenFilePanel(
                    "Blender 실행 파일",
                    "",
                    Application.platform == RuntimePlatform.WindowsEditor ? "exe" : ""
                );
                if (!string.IsNullOrEmpty(selected))
                {
                    blenderExecutable = selected;
                    EditorPrefs.SetString(BlenderPreference, selected);
                }
            }
            EditorGUILayout.LabelField(
                "Studio 구독 크레딧과 API 크레딧은 별도입니다. Pro/Max 보너스의 자격·지급 여부는 공식 안내와 계정에서 확인하세요.",
                EditorStyles.wordWrappedLabel
            );
            if (GUILayout.Button("Pro/Max API 보너스 안내"))
                Application.OpenURL("https://studio.tripo3d.ai/cli");
            if (GUILayout.Button("API 요금표"))
                Application.OpenURL("https://developers.tripo3d.ai/en/pricing");
            if (GUILayout.Button("P2 모델 안내"))
                Application.OpenURL("https://developers.tripo3d.ai/en/models/p2");
            if (creationSource == 1)
            {
                EditorGUILayout.LabelField(
                    "Bridge 설치: Unity를 닫고 저장소의 infra/scripts/install-tripo-bridge.ps1을 실행하세요. Chrome/Edge에서 연결하고 Unity Play는 꺼 두세요.",
                    EditorStyles.wordWrappedLabel
                );
                if (GUILayout.Button("공식 Bridge 연결 안내"))
                    Application.OpenURL("https://www.tripo3d.ai/blog/tripo-dcc-bridge-for-unity");
            }
        }

        private void DrawAssetPreparationGuide()
        {
            string guide;
            switch (generationKind)
            {
                case TripoAssetKind.Character:
                    guide =
                        "MVP 권장: 리틀 멍 얼굴·대두 비율을 유지한 머리·복장 포함 완성형 캐릭터를 준비하세요. 모든 뷰의 외형과 중립 표정·A/T 포즈를 맞추고 팔과 몸을 떨어뜨립니다. 공통 몸체·교체 부품을 선택할 때만 머리카락 없는 단색 기본복의 LittleMungBase를 준비합니다. 완성형 생성도 얼굴·의상 결함 검수를 생략할 수 없습니다.";
                    break;
                case TripoAssetKind.Hair:
                    guide =
                        "헤어는 머리카락 부품만 생성하고 기준 캐릭터의 머리 크기·헤어라인에 맞춥니다. 짧은 머리는 머리 소켓, 변형이 필요한 머리는 Blender에서 공통 골격의 가중치를 준비합니다.";
                    break;
                case TripoAssetKind.Clothing:
                    guide =
                        "복장은 몸·얼굴 없이 의상 부품으로 준비합니다. 공통 체형·포즈에 맞춰 Blender에서 내부 몸체를 정리하고 공통 골격의 가중치를 연결합니다. 독립 자동 리깅만으로 착용 호환성이 생기지는 않습니다.";
                    break;
                default:
                    guide =
                        "장신구는 개별 부품으로 생성하고 착용할 소켓에 연결합니다. 좌우 귀걸이는 각각 지정해 한쪽 또는 양쪽에 착용할 수 있습니다.";
                    break;
            }
            EditorGUILayout.HelpBox(guide, MessageType.None);
        }

        private void DrawApiGeneration()
        {
            using (new EditorGUI.DisabledScope(running || inFlight))
            {
                EditorGUILayout.LabelField("1. 이미지", EditorStyles.boldLabel);
                singleImage = EditorGUILayout.Toggle(FieldLabel("단일 이미지 사용"), singleImage);
                EditorGUILayout.LabelField(
                    singleImage
                        ? "전신 정면 이미지 1장. 손발이 잘리지 않고 팔과 몸이 떨어진 자세를 사용하세요."
                        : "같은 외형·포즈의 전신 4뷰. 좌우는 캐릭터 기준입니다.",
                    EditorStyles.wordWrappedMiniLabel
                );
                int imageCount = singleImage ? 1 : 4;
                int imageColumns =
                    position.width >= 760f ? 4
                    : position.width >= 440f ? 2
                    : 1;
                imageColumns = Mathf.Min(imageColumns, imageCount);
                float imageWidth = (ContentWidth - 8f * imageColumns) / imageColumns;
                for (int row = 0; row < imageCount; row += imageColumns)
                {
                    using (new EditorGUILayout.HorizontalScope())
                        for (
                            int column = 0;
                            column < imageColumns && row + column < imageCount;
                            column++
                        )
                            DrawImage(row + column, imageWidth);
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("2. 생성 설정", EditorStyles.boldLabel);
                assetName = EditorGUILayout.TextField(FieldLabel("에셋 이름"), assetName);
                generationKind = (TripoAssetKind)
                    EditorGUILayout.EnumPopup(FieldLabel("생성 카테고리"), generationKind);
                generationModelIndex = EditorGUILayout.Popup(
                    FieldLabel("생성 모델"),
                    generationModelIndex,
                    GenerationModelNames
                );
                if (generationKind == TripoAssetKind.Character)
                {
                    autoRig = true;
                    EditorGUILayout.LabelField("Tripo 자동 리깅", "1회 포함 · 중복 제출 차단");
                    EditorGUILayout.HelpBox(
                        "서비스 공통 마스터: LittleMungAdjusted / Female 부모 프리팹. 남녀 모두 같은 골격·동작을 사용하고 외형은 생성 모델을 유지합니다. 다운로드 후 마스터 보정과 동작 검사를 자동 실행하고 마지막 외관 확인만 기다립니다.",
                        MessageType.Info
                    );
                }
                else
                    autoRig = false;
                EditorGUILayout.LabelField(
                    generationKind == TripoAssetKind.Character && autoRig
                        ? $"표준 텍스처·PBR·삼각형 / 목표 {faceLimit:N0} / 서비스 마스터 비율·배율 사용"
                        : $"표준 텍스처·PBR·삼각형 / 목표 {faceLimit:N0} / 가져오기 높이 {heightMeters:0.##}m",
                    EditorStyles.wordWrappedMiniLabel
                );
                showGenerationOptions = EditorGUILayout.Foldout(
                    showGenerationOptions,
                    "세부 설정 · 면 수 / 높이",
                    true
                );
                if (showGenerationOptions)
                {
                    faceLimit = EditorGUILayout.IntSlider(
                        FieldLabel("목표 삼각형 수"),
                        faceLimit,
                        500,
                        50000
                    );
                    heightMeters = EditorGUILayout.Slider(
                        FieldLabel("가져오기 높이 (m)"),
                        heightMeters,
                        0.1f,
                        10f
                    );
                    EditorGUILayout.LabelField(
                        "면 수는 요청값이며 결과가 달라질 수 있습니다. 높이는 전체 배율로 적용됩니다.",
                        EditorStyles.wordWrappedMiniLabel
                    );
                }
                EditorGUILayout.Space();
                DrawOutputFolders();
                EditorGUILayout.LabelField("3. 비용 확인 후 생성", EditorStyles.boldLabel);
                apiKey = EditorGUILayout.PasswordField(FieldLabel("Tripo API 키"), apiKey);
                EditorGUILayout.LabelField(
                    "키를 비우면 TRIPO_API_KEY 환경변수를 사용합니다.",
                    EditorStyles.wordWrappedMiniLabel
                );
                if (GUILayout.Button("이미지 업로드만 테스트 · 생성 안 함", GUILayout.Height(30f)))
                    TestImageUploads();
                EditorGUILayout.LabelField(
                    "선택한 이미지만 업로드하고 종료합니다. 생성·리깅 요청이나 기존 작업 재개는 하지 않습니다.",
                    EditorStyles.wordWrappedMiniLabel
                );
                if (GUILayout.Button("API 콘솔 · 잔액 확인"))
                    Application.OpenURL("https://developers.tripo3d.ai/");
                TripoCreditEstimate estimate = TripoCreditEstimate.ForNewAsset(
                    GenerationModels[generationModelIndex],
                    autoRig
                );
                EditorGUILayout.HelpBox(DescribeEstimate(estimate), MessageType.Info);
                EditorGUILayout.LabelField(
                    $"Studio 구독과 별도인 API 크레딧 사용 · {TripoCreditEstimate.PricingDate} 요금표 기준 예상액이며 잔액이 아닙니다.",
                    EditorStyles.wordWrappedMiniLabel
                );
                if (generationKind == TripoAssetKind.Character)
                    EditorGUILayout.LabelField(
                        "자동 리깅은 얼굴 표정·립싱크를 만들지 않습니다.",
                        EditorStyles.wordWrappedMiniLabel
                    );
                if (generationModelIndex == 1)
                    EditorGUILayout.LabelField(
                        "P2는 Preview 모델입니다.",
                        EditorStyles.wordWrappedMiniLabel
                    );
                if (GUILayout.Button("새 에셋 생성…", GUILayout.Height(30f)))
                    StartNewJob();
            }
        }

        private static string DescribeEstimate(TripoCreditEstimate estimate)
        {
            string processing =
                estimate.Rigging > 0
                    ? $"자동 리깅 {estimate.Rigging} (검사 무료·FBX 포함)"
                    : $"FBX 변환 {estimate.Conversion} (PNG 텍스처)";
            return $"새 생성 1회 예상: {estimate.Total} API 크레딧\n생성 {estimate.Generation} + {processing}";
        }

        private void DrawImage(int index, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            EditorGUILayout.LabelField(ViewNames[index], GUILayout.Width(width));
            Rect rect = GUILayoutUtility.GetRect(width, 120f, GUILayout.ExpandWidth(false));
            if (previews[index] != null)
                EditorGUI.DrawPreviewTexture(rect, previews[index], null, ScaleMode.ScaleToFit);
            else
                GUI.Box(rect, "이미지 선택");
            EditorGUILayout.LabelField(
                Path.GetFileName(paths[index] ?? ""),
                EditorStyles.wordWrappedMiniLabel,
                GUILayout.Width(width)
            );
            if (GUILayout.Button("선택"))
            {
                string path = EditorUtility.OpenFilePanel(
                    ViewNames[index] + " 전신 이미지",
                    "",
                    ""
                );
                if (!string.IsNullOrEmpty(path))
                {
                    paths[index] = path;
                    LoadPreview(index);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void LoadPreview(int index)
        {
            if (previews[index] != null)
                DestroyImmediate(previews[index]);
            previews[index] = null;
            try
            {
                string path = paths[index];
                if (!File.Exists(path) || new FileInfo(path).Length > 20 * 1024 * 1024)
                    return;
                var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (texture.LoadImage(File.ReadAllBytes(path), true))
                    previews[index] = texture;
                else
                    DestroyImmediate(texture);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void DrawJob()
        {
            if (job == null)
                return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                job.assetName + " · " + StageNames[(int)job.stage],
                EditorStyles.boldLabel
            );
            EditorGUILayout.LabelField(
                "확인된 사용량",
                (job.generationCredits + job.rigCredits + job.conversionCredits) + " credits"
            );
            if (
                !running
                && !inFlight
                && !job.autoRig
                && job.stage == TripoStage.Failed
                && !job.submissionUncertain
                && !string.IsNullOrEmpty(job.generationTaskId)
            )
                if (GUILayout.Button("성공한 원본 생성에서 FBX 초안 복구"))
                    RecoverRawDraft();
            showJobDetails = EditorGUILayout.Foldout(
                showJobDetails,
                "작업 상세 · 모델 / 작업 ID",
                true
            );
            if (showJobDetails)
            {
                EditorGUILayout.LabelField(
                    "저장된 생성 모델: " + job.model,
                    EditorStyles.wordWrappedMiniLabel
                );
                DrawTaskId("생성 작업", job.generationTaskId);
                DrawTaskId("검사 작업", job.rigCheckTaskId);
                DrawTaskId("리깅 작업", job.rigTaskId);
                DrawTaskId("변환 작업", job.conversionTaskId);
                if (job.stage != TripoStage.Completed)
                {
                    DrawSavedPath("Unity 모델 저장 예정 위치", job.modelAssetPath);
                    DrawSavedPath("프리팹 저장 예정 위치", job.prefabPath);
                }
            }
            if (running)
            {
                if (GUILayout.Button("일시정지"))
                    Stop();
                EditorGUILayout.LabelField(
                    "일시정지는 로컬 처리를 멈춥니다. 이미 제출한 원격 작업은 계속 실행될 수 있습니다.",
                    EditorStyles.wordWrappedMiniLabel
                );
            }
            else if (
                !inFlight
                && job.stage != TripoStage.Completed
                && job.stage != TripoStage.Failed
            )
            {
                if (job.submissionUncertain)
                {
                    EditorGUILayout.HelpBox(
                        "제출 결과가 불명확해 자동 재제출을 막았습니다. Tripo 콘솔에서 이 캐릭터·현재 단계의 작업 ID를 확인해 연결하세요. 다른 모델의 ID를 넣지 마세요.",
                        MessageType.Warning
                    );
                    recoveryTaskId = EditorGUILayout.TextField(
                        FieldLabel("확인한 작업 ID"),
                        recoveryTaskId
                    );
                    if (GUILayout.Button("기존 작업 ID 연결"))
                        Reconcile();
                }
                else if (
                    GUILayout.Button(
                        job.stage == TripoStage.ReadyToImport
                            ? "자동 보정·검증 이어서 실행 (API 사용 없음)"
                            : "저장된 작업 재개"
                    )
                )
                    Resume();
            }
            if (job.stage == TripoStage.Completed)
            {
                EditorGUILayout.HelpBox(
                    "자동 보정·검사 완료 · 외관 확인 전에는 최종 프리팹을 등록하지 않습니다.",
                    MessageType.Info
                );
                DrawSavedPath("원본 다운로드", job.modelPath);
                DrawSavedPath("Unity 모델", job.modelAssetPath);
                string reviewPrefabPath =
                    record != null
                    && AssetDatabase.GetAssetPath(record.sourceModel) == job.modelAssetPath
                    && Path.GetExtension(record.ModelPath) == ".prefab"
                        ? record.ModelPath
                    : record == null ? ""
                    : record.ModelPath;
                DrawSavedPath("중간 검수 후보 · 최종 등록 전", reviewPrefabPath);
                if (GUILayout.Button("마지막 외관 확인하기"))
                    RunEditorAction(() =>
                    {
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(job.modelAssetPath);
                        record = TripoLocalWorkflow.StartReview(
                            model,
                            job.kind,
                            job.kind == TripoAssetKind.Accessory || job.kind == TripoAssetKind.Hair
                                ? TripoAttachmentKind.Rigid
                                : TripoAttachmentKind.Skinned
                        );
                        ResetPreview();
                        selectedTab = 5;
                    });
                if (!string.IsNullOrEmpty(job.modelAssetPath) && File.Exists(job.modelAssetPath))
                {
                    if (GUILayout.Button("생성 결과 FBX 선택"))
                    {
                        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(
                            job.modelAssetPath
                        );
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }
                    if (GUILayout.Button("생성 결과 폴더 열기"))
                        EditorUtility.RevealInFinder(Path.GetFullPath(job.modelAssetPath));
                }
                if (!string.IsNullOrEmpty(reviewPrefabPath) && File.Exists(reviewPrefabPath))
                {
                    if (GUILayout.Button("생성 결과 프리팹 열기"))
                        PrefabStageUtility.OpenPrefab(reviewPrefabPath);
                    DrawReview();
                }
            }
        }

        private void DrawActivity()
        {
            if (testingUploads)
            {
                EditorGUILayout.LabelField(
                    "이미지 업로드만 테스트 중 · 생성 차단",
                    EditorStyles.boldLabel
                );
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    testedImageCount / (float)Math.Max(1, testImageTotal),
                    $"{testedImageCount}/{testImageTotal}장 업로드 완료"
                );
                if (GUILayout.Button("업로드 테스트 중지"))
                    cancellation?.Cancel();
            }
            else if (job != null)
            {
                string state =
                    running ? "진행 중"
                    : inFlight ? "처리 중"
                    : job.stage == TripoStage.Completed
                        ? (
                            record != null && !string.IsNullOrEmpty(record.registeredPrefabPath)
                                ? "최종 프리팹 등록 완료"
                                : "자동 검사 통과 · 외관 확인 대기"
                        )
                    : job.stage == TripoStage.Failed || messageIsError ? "오류로 중단"
                    : "중지됨 · 저장된 작업 재개 가능";
                EditorGUILayout.LabelField(
                    job.assetName + " · " + state,
                    EditorStyles.wordWrappedLabel
                );
                string stage =
                    job.stage == TripoStage.Completed
                    && record != null
                    && !string.IsNullOrEmpty(record.automaticStatus)
                        ? record.automaticStatus
                        : StageNames[(int)job.stage];
                if (job.stage == TripoStage.Uploading)
                {
                    int completed = Array
                        .FindAll(job.imageTokens, token => !string.IsNullOrEmpty(token))
                        .Length;
                    EditorGUI.ProgressBar(
                        EditorGUILayout.GetControlRect(false, 20),
                        completed / (float)Math.Max(1, job.imageTokens.Length),
                        $"{stage} · {completed}/{job.imageTokens.Length}장 완료"
                    );
                }
                else if (job.TryGetRemoteProgress(out int progress))
                    EditorGUI.ProgressBar(
                        EditorGUILayout.GetControlRect(false, 20),
                        progress / 100f,
                        $"{stage} · 마지막 서버 응답 {progress}%"
                    );
                else
                    EditorGUILayout.LabelField(
                        stage
                            + (
                                running
                                && job.progressStage == job.stage
                                && job.remoteStatus == "queued"
                                    ? " · 서버 대기열"
                                    : ""
                            )
                    );
                if (running || inFlight)
                {
                    string dots = new string(
                        '.',
                        (int)(EditorApplication.timeSinceStartup * 2) % 4
                    );
                    string elapsed = running
                        ? TimeSpan
                            .FromSeconds(EditorApplication.timeSinceStartup - startedAt)
                            .ToString(@"mm\:ss")
                        : "";
                    EditorGUILayout.LabelField(
                        "처리 중"
                            + dots
                            + "  이번 실행 "
                            + elapsed
                            + " · 전체 완료율/남은 시간은 미제공",
                        EditorStyles.wordWrappedMiniLabel
                    );
                }
            }
            if (blenderBusy)
                EditorGUILayout.LabelField(
                    "Blender 처리 중 · 원본은 유지됩니다.",
                    EditorStyles.wordWrappedLabel
                );
            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(
                    message,
                    messageIsError ? MessageType.Error : MessageType.Info
                );
                if (messageIsError && GUILayout.Button("오류 내용 복사"))
                    EditorGUIUtility.systemCopyBuffer = message;
            }
        }

        private static void DrawSavedPath(string label, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                Path.GetFullPath(path),
                EditorStyles.wordWrappedLabel,
                GUILayout.Height(42)
            );
        }

        private static void DrawTaskId(string label, string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                EditorGUILayout.LabelField(label);
                EditorGUILayout.SelectableLabel(id, GUILayout.Height(18f));
            }
        }

        private void DrawReview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                record == null ? job.modelAssetPath : record.ModelPath
            );
            if (prefab == null)
                return;
            long triangles = 0;
            int shapes = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                triangles += CountTriangles(filter.sharedMesh);
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                triangles += CountTriangles(renderer.sharedMesh);
                if (renderer.sharedMesh != null)
                    shapes += renderer.sharedMesh.blendShapeCount;
            }
            EditorGUILayout.LabelField($"삼각형 {triangles:N0} · 블렌드셰이프 {shapes}");
            EditorGUILayout.HelpBox(
                "현재는 중간 검수 후보입니다. 자동 검사 결과와 외관을 확인한 뒤 최종 등록하세요. 모델·설정 변경 시 이전 승인은 무효화됩니다.",
                MessageType.Info
            );
            if (shapes == 0)
                EditorGUILayout.HelpBox(
                    "얼굴 블렌드셰이프가 없습니다. 이 모델은 아직 표정·립싱크용으로 준비되지 않았습니다.",
                    MessageType.Warning
                );
        }

        private static long CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    count += (long)mesh.GetIndexCount(i) / 3;
            return count;
        }

        private void DrawOutputFolders()
        {
            EditorGUILayout.LabelField("저장 위치 · 프로젝트 Assets 하위", EditorStyles.boldLabel);
            modelOutputFolder = EditorGUILayout.TextField(
                FieldLabel("모델 폴더"),
                modelOutputFolder
            );
            if (GUILayout.Button("모델 폴더 선택"))
                RunEditorAction(() => modelOutputFolder = ChooseOutputFolder(modelOutputFolder));
            prefabOutputFolder = EditorGUILayout.TextField(
                FieldLabel("프리팹 폴더"),
                prefabOutputFolder
            );
            if (GUILayout.Button("프리팹 폴더 선택"))
                RunEditorAction(() => prefabOutputFolder = ChooseOutputFolder(prefabOutputFolder));
            EditorGUILayout.LabelField(
                "새 생성에 적용됩니다. 다운로드 원본은 UserSettings/Tripo/Downloads에 별도 보존합니다.",
                EditorStyles.wordWrappedMiniLabel
            );
            if (
                job != null
                && job.stage == TripoStage.Uploading
                && !job.submissionUncertain
                && string.IsNullOrEmpty(job.generationTaskId)
                && GUILayout.Button("저장된 미생성 작업에도 이 경로 적용")
            )
                RunEditorAction(() =>
                {
                    job.SetOutputFolders(modelOutputFolder, prefabOutputFolder);
                    store.Save(job);
                    message = "저장된 작업의 출력 폴더를 변경했습니다. 생성은 시작하지 않았습니다.";
                    messageIsError = false;
                });
        }

        private static string ChooseOutputFolder(string current)
        {
            string selected = EditorUtility.OpenFolderPanel(
                "프로젝트 Assets 하위 폴더",
                Application.dataPath,
                ""
            );
            return string.IsNullOrEmpty(selected)
                ? current
                : TripoJob.NormalizeOutputFolder(
                    Path.GetRelativePath(Directory.GetCurrentDirectory(), selected)
                );
        }

        private async void TestImageUploads()
        {
            if (running || inFlight)
                return;
            testedImageCount = 0;
            testImageTotal = singleImage ? 1 : 4;
            try
            {
                BuilderTools.ThrowIfPlaying();
                var images = new TripoImages(
                    paths[0],
                    singleImage ? paths[0] : paths[1],
                    singleImage ? paths[0] : paths[2],
                    singleImage ? paths[0] : paths[3]
                );
                images.Validate();
                string[] selectedPaths = singleImage
                    ? new[] { paths[0] }
                    : images.GetOrderedPaths();
                var api = new TripoHttpApi(GetApiKey());
                cancellation = new CancellationTokenSource();
                message = "";
                messageIsError = false;
                testingUploads = true;
                inFlight = true;
                // 생성 파이프라인 및 저장된 job에 연결하지 않는다.
                await TripoUploadProbe.RunAsync(
                    selectedPaths,
                    api.UploadAsync,
                    count => testedImageCount = count,
                    cancellation.Token
                );
                message =
                    $"업로드 테스트 성공: {testedImageCount}/{testImageTotal}장. 생성·리깅 요청 없이 종료했습니다. 기존 작업 기록은 변경하지 않았습니다.";
            }
            catch (OperationCanceledException)
            {
                messageIsError = false;
                message = "업로드 테스트를 중지했습니다. 생성·리깅 요청은 하지 않았습니다.";
            }
            catch (Exception exception)
            {
                ShowError(exception);
                message =
                    $"이미지 업로드 테스트 실패 ({testedImageCount}/{testImageTotal}장 완료)\n"
                    + message;
            }
            finally
            {
                Stop();
                testingUploads = false;
                inFlight = false;
                if (this != null)
                    Repaint();
            }
        }

        private void StartNewJob()
        {
            try
            {
                BuilderTools.ThrowIfPlaying();
                if (string.IsNullOrWhiteSpace(GetApiKey()))
                    throw new InvalidOperationException("Tripo API 키가 필요합니다.");
                TripoMaterialImport.FindBlender();
                if (generationKind == TripoAssetKind.Character)
                {
                    autoRig = true;
                    TripoMasterWorkflow.RequireMaster();
                }
                TripoJob created = TripoJob.Create(
                    assetName,
                    singleImage
                        ? new TripoImages(paths[0], paths[0], paths[0], paths[0])
                        : new TripoImages(paths[0], paths[1], paths[2], paths[3]),
                    faceLimit,
                    heightMeters,
                    GenerationModels[generationModelIndex]
                );
                created.SetOutputFolders(modelOutputFolder, prefabOutputFolder);
                created.autoRig = autoRig;
                created.kind = generationKind;
                if (singleImage)
                {
                    created.imagePaths = new[] { paths[0] };
                    created.imageTokens = new string[1];
                }
                TripoCreditEstimate estimate = TripoCreditEstimate.ForNewAsset(
                    created.model,
                    created.autoRig
                );
                if (
                    !EditorUtility.DisplayDialog(
                        "Tripo 생성 시작",
                        (singleImage ? "이미지 1장" : "이미지 4장")
                            + "을 Tripo로 업로드해 3D 모델을 생성합니다. "
                            + (
                                autoRig
                                    ? "자동 리깅을 추가합니다."
                                    : "리깅 없이 FBX 초안을 받습니다."
                            )
                            + "\n\n모델: "
                            + created.model
                            + "\n"
                            + DescribeEstimate(estimate)
                            + "\n모델 저장: "
                            + created.modelAssetPath
                            + "\n프리팹 저장: "
                            + created.prefabPath
                            + "\n예상액은 요금표 기준이며 API 잔액 조회값이 아닙니다."
                            + "\n\n한 작업에서 생성과 리깅은 각각 최대 1회 제출합니다. 실패·재개 시 중복 제출하지 않습니다. Studio Pro와 별도인 API 크레딧을 사용합니다. 이전 원격 작업은 취소되지 않습니다.",
                        "생성 시작",
                        "돌아가기"
                    )
                )
                    return;
                if (File.Exists(StatePath))
                {
                    Directory.CreateDirectory("UserSettings/Tripo/History");
                    File.Copy(
                        StatePath,
                        "UserSettings/Tripo/History/" + Guid.NewGuid().ToString("N") + ".json"
                    );
                }
                store.Save(created);
                job = created;
                Resume();
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void Resume()
        {
            try
            {
                BuilderTools.ThrowIfPlaying();
                if (job.submissionUncertain)
                    throw new InvalidOperationException("먼저 기존 작업 ID를 확인해 연결하세요.");
                pipeline =
                    job.stage == TripoStage.ReadyToImport
                        ? null
                        : new TripoPipeline(new TripoHttpApi(GetApiKey()), store.Save);
                cancellation = new CancellationTokenSource();
                startedAt = EditorApplication.timeSinceStartup;
                nextPoll = 0;
                running = true;
                message = "";
                messageIsError = false;
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private async void Tick()
        {
            UpdateBodyMotion();
            if (
                (running || inFlight || blenderBusy)
                && EditorApplication.timeSinceStartup >= nextStatusRepaint
            )
            {
                nextStatusRepaint = EditorApplication.timeSinceStartup + 0.25;
                Repaint();
            }
            if (!running || inFlight || EditorApplication.timeSinceStartup < nextPoll)
                return;
            if (EditorApplication.timeSinceStartup - startedAt > 1800)
            {
                Stop();
                message =
                    "30분 동안 완료되지 않아 로컬 처리를 멈췄습니다. 저장된 작업 ID로 재개할 수 있습니다.";
                Repaint();
                return;
            }
            inFlight = true;
            try
            {
                if (job.stage == TripoStage.ReadyToImport)
                {
                    try
                    {
                        record = TripoLocalImport.Complete(job);
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                            record.ModelPath
                        );
                        ResetPreview();
                        selectedTab = 5;
                        Selection.activeObject = prefab;
                        EditorGUIUtility.PingObject(prefab);
                        messageIsError = !record.revision.validationPassed;
                        message = messageIsError
                            ? "다운로드 완료 · 자동 검사 실패. 재생성하지 말고 아래 문제를 확인하세요. 로컬 재검사에는 API 키·크레딧이 필요하지 않습니다.\n"
                                + string.Join("\n", record.validationErrors)
                            : "마스터 보정·자동 동작 검사 통과. 마지막 외관을 확인하면 최종 프리팹을 등록할 수 있습니다.";
                    }
                    finally
                    {
                        store.Save(job);
                    }
                    Stop();
                }
                else
                {
                    await pipeline.AdvanceAsync(job, cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Stop();
            }
            catch (Exception exception)
            {
                Stop();
                ShowError(exception);
            }
            finally
            {
                inFlight = false;
                nextPoll = EditorApplication.timeSinceStartup + 3;
                if (this != null)
                    Repaint();
            }
        }

        private async void RecoverRawDraft()
        {
            inFlight = true;
            try
            {
                var recovery = new TripoPipeline(new TripoHttpApi(GetApiKey()), store.Save);
                await recovery.RecoverDraftAsync(job, CancellationToken.None);
                message =
                    "생성 원본을 재사용합니다. 재개하면 FBX 변환을 제출합니다. 새 3D 생성은 하지 않으며 변환 요금은 서버 정책을 따릅니다.";
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
            finally
            {
                inFlight = false;
                if (this != null)
                    Repaint();
            }
        }

        private async void Reconcile()
        {
            inFlight = true;
            try
            {
                var recovery = new TripoPipeline(new TripoHttpApi(GetApiKey()), store.Save);
                await recovery.ReconcileSubmissionAsync(
                    job,
                    recoveryTaskId,
                    CancellationToken.None
                );
                message = "기존 작업을 연결했습니다. 재개 버튼으로 이어서 진행하세요.";
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
            finally
            {
                inFlight = false;
                if (this != null)
                    Repaint();
            }
        }

        private void Stop()
        {
            running = false;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        private string GetApiKey()
        {
            return string.IsNullOrWhiteSpace(apiKey)
                ? Environment.GetEnvironmentVariable("TRIPO_API_KEY")
                : apiKey;
        }

        private void ShowError(Exception exception)
        {
            messageIsError = true;
            string key = GetApiKey();
            message = string.IsNullOrEmpty(key)
                ? exception.Message
                : exception.Message.Replace(key, "[숨김]");
        }
    }
}
