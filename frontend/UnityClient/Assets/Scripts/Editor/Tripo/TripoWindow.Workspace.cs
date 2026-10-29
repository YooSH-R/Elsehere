using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed partial class TripoWindow
    {
        private static readonly string[] Tabs =
        {
            "생성·가져오기",
            "모델·골격",
            "Blender 수정",
            "표정·립싱크",
            "부품·소켓",
            "결과·외관 확인",
        };
        private static readonly int[] BasicTabIndices = { 0, 5 };
        private static readonly string[] BasicTabs = { "생성·가져오기", "결과·외관 확인" };
        private const string BlenderPreference = "A305.Tripo.BlenderExecutable";

        [SerializeField]
        private TripoAssetRecord record;

        [SerializeField]
        private int selectedTab;

        [SerializeField]
        private bool showAdvancedTools;

        [SerializeField]
        private TripoAssetKind generationKind;

        [SerializeField]
        private bool autoRig;

        [SerializeField]
        private bool singleImage;
        private GameObject modelToOpen;
        private bool blenderBusy;
        private CancellationTokenSource blenderCancellation;
        private string blenderExecutable;
        private int emotionIndex;
        private int visemeIndex;
        private float articulation;
        private float blink;
        private TripoModelPreview modelPreview;
        private TripoMotionPreview bodyMotion;
        private TripoReviewMotion bodyMotionKind;
        private TripoAuditReport previewRigReport;
        private bool showReviewBones;
        private bool bodyMotionPlaying;
        private float bodyMotionPhase;
        private double bodyMotionUpdated;
        private float bodyMotionSpeed = 1f;
        private string previewFingerprint;
        private bool showBindings;
        private bool showRecordPicker;
        private bool showModelOptions;
        private bool showBlenderOptions;
        private bool showValidationOptions;
        private bool showValidationHelp;

        private float ContentWidth => Mathf.Max(200f, position.width - 24f);
        private bool CompactView => position.width < 540f;
        private int NavigationColumns => Mathf.Clamp(Mathf.FloorToInt(ContentWidth / 150f), 1, 3);

        private string FieldLabel(string label)
        {
            if (!CompactView)
                return label;
            EditorGUILayout.LabelField(label, EditorStyles.wordWrappedLabel);
            return string.Empty;
        }

        private void BeginButtonRow()
        {
            if (CompactView)
                EditorGUILayout.BeginVertical();
            else
                EditorGUILayout.BeginHorizontal();
        }

        private void EndButtonRow()
        {
            if (CompactView)
                EditorGUILayout.EndVertical();
            else
                EditorGUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            bool previousWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.labelWidth = CompactView ? 0f : 180f;
            EditorGUIUtility.wideMode = !CompactView;
            try
            {
                DrawWorkbench();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUIUtility.wideMode = previousWideMode;
            }
        }

        private void DrawWorkbench()
        {
            DrawActivity();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginVertical(GUILayout.Width(ContentWidth));
            EditorGUILayout.LabelField("Tripo Asset Workbench", EditorStyles.boldLabel);
            if (record != null)
                EditorGUILayout.LabelField(
                    "현재 작업: " + record.DisplayName,
                    EditorStyles.wordWrappedLabel
                );
            if (record == null || job == null || job.stage != TripoStage.Completed)
                DrawJob();
            showRecordPicker = EditorGUILayout.Foldout(
                showRecordPicker,
                "기존 작업 불러오기",
                true
            );
            if (showRecordPicker)
            {
                EditorGUI.BeginChangeCheck();
                record = (TripoAssetRecord)
                    EditorGUILayout.ObjectField(
                        FieldLabel("작업 기록"),
                        record,
                        typeof(TripoAssetRecord),
                        false
                    );
                if (EditorGUI.EndChangeCheck())
                    ResetPreview();
            }
            if (record != null)
            {
                showAdvancedTools = EditorGUILayout.ToggleLeft(
                    "고급 작업 · 모델 / Blender / 표정 / 부품",
                    showAdvancedTools
                );
                if (showAdvancedTools)
                    selectedTab = GUILayout.SelectionGrid(selectedTab, Tabs, NavigationColumns);
                else
                {
                    int basicTab = Array.IndexOf(BasicTabIndices, selectedTab);
                    selectedTab = BasicTabIndices[
                        GUILayout.SelectionGrid(
                            Mathf.Max(0, basicTab),
                            BasicTabs,
                            NavigationColumns
                        )
                    ];
                }
            }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(blenderBusy))
            {
                if (record == null || selectedTab == 0)
                    DrawGeneration();
                else
                {
                    switch (selectedTab)
                    {
                        case 1:
                            DrawModelPanel();
                            break;
                        case 2:
                            DrawBlenderPanel();
                            break;
                        case 3:
                            DrawFacePanel();
                            break;
                        case 4:
                            DrawPartsPanel();
                            break;
                        case 5:
                            DrawValidationPanel();
                            break;
                    }
                }
            }
            if (record != null && job != null && job.stage == TripoStage.Completed)
                DrawJob();
            if (blenderBusy)
            {
                EditorGUILayout.HelpBox(
                    "Blender 처리 중입니다. 원본과 현재 적용 모델은 유지됩니다.",
                    MessageType.Info
                );
                if (GUILayout.Button("처리 중지"))
                    blenderCancellation?.Cancel();
            }
            if (record != null && selectedTab != 0 && selectedTab != 5)
                DrawModelPreview();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawModelImport()
        {
            generationKind = (TripoAssetKind)
                EditorGUILayout.EnumPopup(FieldLabel("모델 카테고리"), generationKind);
            BeginButtonRow();
            {
                modelToOpen = (GameObject)
                    EditorGUILayout.ObjectField(
                        FieldLabel("가져온 모델"),
                        modelToOpen,
                        typeof(GameObject),
                        false
                    );
                using (new EditorGUI.DisabledScope(modelToOpen == null || blenderBusy))
                    if (GUILayout.Button("작업 시작"))
                        RunEditorAction(() =>
                        {
                            record = TripoLocalWorkflow.StartReview(
                                modelToOpen,
                                generationKind,
                                (
                                    generationKind == TripoAssetKind.Accessory
                                    || generationKind == TripoAssetKind.Hair
                                )
                                    ? TripoAttachmentKind.Rigid
                                    : TripoAttachmentKind.Skinned
                            );
                            ResetPreview();
                            selectedTab = 5;
                        });
            }
            EndButtonRow();
            using (new EditorGUI.DisabledScope(blenderBusy))
                if (GUILayout.Button("로컬 FBX 복사해서 가져오기 · 자동 검사"))
                    RunEditorAction(() =>
                    {
                        string path = EditorUtility.OpenFilePanel(
                            "다운로드한 캐릭터 FBX",
                            "",
                            "fbx"
                        );
                        if (string.IsNullOrEmpty(path))
                            return;
                        record = TripoLocalWorkflow.ImportLocalFbx(
                            path,
                            generationKind,
                            generationKind == TripoAssetKind.Accessory
                            || generationKind == TripoAssetKind.Hair
                                ? TripoAttachmentKind.Rigid
                                : TripoAttachmentKind.Skinned
                        );
                        TripoLocalImport.Review(record);
                        selectedTab = 5;
                        ResetPreview();
                    });
        }

        private void DrawModelPanel()
        {
            if (
                showAdvancedTools
                && record.kind == TripoAssetKind.Character
                && record.requireHumanoid
                && GUILayout.Button("서비스 마스터 적용 · 새 복사본 만들기 (API 사용 없음)")
            )
                RunEditorAction(() =>
                {
                    TripoLocalImport.Review(record);
                    ResetPreview();
                });
            EditorGUI.BeginChangeCheck();
            record.uniformScale = EditorGUILayout.Slider(
                FieldLabel("전체 확대·축소"),
                record.uniformScale,
                record.profile == null || !record.profile.IsValid
                    ? 0.85f
                    : record.profile.MinimumScale,
                record.profile == null || !record.profile.IsValid
                    ? 1.15f
                    : record.profile.MaximumScale
            );
            if (EditorGUI.EndChangeCheck())
                SaveRecord();
            if (GUILayout.Button("Scene에서 자동 자세 핸들 조작"))
                RunEditorAction(() => TripoPoseStage.Open(record));
            EditorGUILayout.LabelField(
                "자세와 외형을 확인하세요. 본 기본 위치·메시·가중치는 Blender에서 수정합니다.",
                EditorStyles.wordWrappedMiniLabel
            );
            showModelOptions = EditorGUILayout.Foldout(
                showModelOptions,
                "고급 설정 · 기준 모델 / 본 매핑",
                true
            );
            if (!showModelOptions)
                return;
            EditorGUILayout.LabelField(
                "현재 적용 모델",
                record.revision.activePath ?? "",
                EditorStyles.wordWrappedMiniLabel
            );
            if (!string.IsNullOrEmpty(record.revision.candidatePath))
                EditorGUILayout.LabelField(
                    "수정 후보",
                    record.revision.candidatePath,
                    EditorStyles.wordWrappedMiniLabel
                );
            EditorGUI.BeginChangeCheck();
            record.kind = (TripoAssetKind)
                EditorGUILayout.EnumPopup(FieldLabel("카테고리"), record.kind);
            record.attachment = (TripoAttachmentKind)
                EditorGUILayout.EnumPopup(FieldLabel("연결 방식"), record.attachment);
            record.commonBody = (GameObject)
                EditorGUILayout.ObjectField(
                    FieldLabel("공통 기준 모델"),
                    record.commonBody,
                    typeof(GameObject),
                    false
                );
            record.referenceHeightMeters = EditorGUILayout.FloatField(
                FieldLabel("기준 높이 (m, 0=원본)"),
                record.referenceHeightMeters
            );
            if (EditorGUI.EndChangeCheck())
                SaveRecord();
            if (GUILayout.Button("FBX Inspector에서 본 매핑 설정"))
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(record.ModelPath);
            EditorGUILayout.LabelField(
                "FBX Inspector > Model의 Read/Write·Blend Shapes를 켜고 Apply하세요. Humanoid 본 매핑은 기준 캐릭터로 검수한 뒤 설정합니다.",
                EditorStyles.wordWrappedLabel
            );
        }

        private void DrawBlenderPanel()
        {
            if (string.IsNullOrEmpty(blenderExecutable))
            {
                try
                {
                    blenderExecutable = TripoMaterialImport.FindBlender();
                }
                catch (FileNotFoundException)
                {
                    blenderExecutable = "";
                }
            }
            EditorGUILayout.LabelField(
                "열기 → Blender에서 저장 → 수정본 가져오기",
                EditorStyles.boldLabel
            );
            if (GUILayout.Button("Blender에서 열기 · 최초 원본 준비"))
                RunBlender(false);
            if (!string.IsNullOrEmpty(record.blenderSourcePath))
            {
                if (GUILayout.Button("저장된 수정본 가져오기"))
                    RunBlender(true);
            }
            EditorGUILayout.HelpBox(
                "Blender에서 Ctrl+S로 저장하세요. 가져오면 원본을 유지한 수정 후보를 만들고 자동 재검사합니다. 몸 애니메이션·제약·드라이버·메시 Modifier 결과는 내보내지 않습니다.",
                MessageType.Info
            );
            showBlenderOptions = EditorGUILayout.Foldout(
                showBlenderOptions,
                "연결 설정 · Blender 경로 / 기존 수정 파일",
                true
            );
            if (showBlenderOptions || !File.Exists(blenderExecutable))
            {
                EditorGUILayout.LabelField(
                    "Blender 실행 파일",
                    blenderExecutable,
                    EditorStyles.wordWrappedMiniLabel
                );
                if (GUILayout.Button("Blender 실행 파일 선택"))
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
                    "편집 원본 (.blend)",
                    record.blenderSourcePath ?? "",
                    EditorStyles.wordWrappedLabel
                );
                if (GUILayout.Button("기존 .blend 연결"))
                {
                    string selected = EditorUtility.OpenFilePanel("편집 원본", "", "blend");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        record.blenderSourcePath = Path.GetRelativePath(
                            Directory.GetCurrentDirectory(),
                            selected
                        );
                        SaveRecord();
                    }
                }
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(
                    record.revision.candidatePath ?? ""
                );
                EditorGUI.BeginChangeCheck();
                candidate = (GameObject)
                    EditorGUILayout.ObjectField(
                        FieldLabel("직접 만든 수정 FBX"),
                        candidate,
                        typeof(GameObject),
                        false
                    );
                if (EditorGUI.EndChangeCheck())
                {
                    RunEditorAction(() =>
                    {
                        if (candidate == null)
                            TripoLocalWorkflow.DiscardCandidate(record);
                        else
                            TripoLocalWorkflow.ReviewCandidate(record, candidate);
                        selectedTab = 5;
                        ResetPreview();
                    });
                }
            }
            DrawRevisionButtons();
        }

        private async void RunBlender(bool export)
        {
            TripoAssetRecord target = record;
            blenderBusy = true;
            blenderCancellation = new CancellationTokenSource();
            try
            {
                var bridge = new TripoBlenderBridge(blenderExecutable);
                if (string.IsNullOrEmpty(target.blenderSourcePath))
                {
                    string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
                    target.blenderSourcePath = "../ArtSources/Tripo/" + id + "/Editable.blend";
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }
                string blend = Path.GetFullPath(target.blenderSourcePath);
                if (export)
                {
                    var report = await TripoLocalWorkflow.ImportBlenderCandidateAsync(
                        target,
                        blenderExecutable,
                        blenderCancellation.Token
                    );
                    message =
                        $"수정 후보 재검사 완료: 오류 {report.Errors.Count}개, 수동 검수 {report.ManualChecks.Count}개. 현재 적용 모델은 유지됩니다.";
                    if (record == target)
                        selectedTab = 5;
                }
                else
                {
                    if (
                        Path.GetExtension(target.ModelPath)
                            .Equals(".prefab", StringComparison.OrdinalIgnoreCase)
                    )
                        throw new InvalidOperationException(
                            "현재 마스터 적용본은 Unity 자식 프리팹입니다. 이 프리팹을 FBX 원본으로 오인해 Blender에서 열 수 없습니다. 원본 FBX 작업 기록에서 외형을 편집한 뒤 서비스 마스터를 다시 적용하세요."
                        );
                    await bridge.PrepareAsync(
                        Path.GetFullPath(target.ModelPath),
                        blend,
                        blenderCancellation.Token
                    );
                    bridge.OpenEditor(blend);
                }
                ResetPreview();
            }
            catch (OperationCanceledException)
            {
                message = "Blender 처리를 중지했습니다. 기존 모델은 유지됩니다.";
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
            finally
            {
                blenderBusy = false;
                blenderCancellation?.Dispose();
                blenderCancellation = null;
                if (this != null)
                    Repaint();
            }
        }

        private AnimationClip faceClip;
        private bool useFaceClip;
        private float faceClipTime;
        private bool faceSweep;
        private double faceSweepStarted;

        private void DrawFaceSequenceControls()
        {
            if (record.faceBindings.Count == 0)
                EditorGUILayout.HelpBox(
                    "얼굴 매핑이 아직 없습니다. Blender에서 Shape Key를 만든 뒤 이름 연결을 실행하세요.",
                    MessageType.Warning
                );
            if (GUILayout.Button(faceSweep ? "105조합 순회 중지" : "7감정 × 15발음 자동 순회"))
            {
                bool start = !faceSweep;
                StopSpeech();
                useFaceClip = false;
                faceSweep = start;
                faceSweepStarted = EditorApplication.timeSinceStartup;
            }
            faceClip = (AnimationClip)
                EditorGUILayout.ObjectField(
                    FieldLabel("기존 얼굴 애니메이션"),
                    faceClip,
                    typeof(AnimationClip),
                    false
                );
            using (new EditorGUI.DisabledScope(faceClip == null || speechPlaying || faceSweep))
                useFaceClip = EditorGUILayout.Toggle(
                    FieldLabel("클립 곡선으로 미리보기"),
                    useFaceClip
                );
            if (faceClip != null && useFaceClip)
                faceClipTime = EditorGUILayout.Slider(
                    FieldLabel("클립 시간"),
                    faceClipTime,
                    0f,
                    Mathf.Max(faceClip.length, 0.001f)
                );
            EditorGUILayout.LabelField(
                "기존 클립은 호환되는 얼굴 곡선만 연결합니다. 자동 순회는 육안 검수를 돕는 재생이며 품질 합격 판정이 아닙니다.",
                EditorStyles.wordWrappedMiniLabel
            );
        }

        private System.Collections.Generic.Dictionary<string, float> PreviewFaceWeights()
        {
            if (useFaceClip && faceClip != null && !speechPlaying && !faceSweep)
                return TripoFaceClip.Evaluate(faceClip, faceClipTime, record.faceBindings);
            return TripoFacePreview.Mix(
                TripoFacePreview.Emotions[emotionIndex],
                TripoFacePreview.Visemes[visemeIndex],
                articulation,
                blink
            );
        }

        private void DrawFacePanel()
        {
            EditorGUILayout.HelpBox(
                "29개 논리 컨트롤 = 감정용 15 + 발음용 14. 중립/SIL은 변형 0입니다. 눈·코·입의 기본 생김새는 Blender의 기준 얼굴에 유지합니다. 이 화면은 기존 Shape Key를 연결하며 없는 표정을 생성하지 않습니다.",
                MessageType.Info
            );
            if (GUILayout.Button("이름으로 얼굴 변형 연결 초안 만들기"))
                RunEditorAction(() =>
                {
                    if (
                        record.faceBindings.Count > 0
                        && !EditorUtility.DisplayDialog(
                            "매핑 다시 찾기",
                            "현재 얼굴 매핑을 이름 기반 검색 결과로 바꿉니다.",
                            "다시 찾기",
                            "유지"
                        )
                    )
                        return;
                    record.faceBindings = TripoFaceBindings.Discover(
                        AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath)
                    );
                    SaveRecord();
                });
            emotionIndex = EditorGUILayout.Popup(
                FieldLabel("감정"),
                emotionIndex,
                TripoFacePreview.Emotions
            );
            visemeIndex = EditorGUILayout.Popup(
                FieldLabel("발음"),
                visemeIndex,
                TripoFacePreview.Visemes
            );
            articulation = EditorGUILayout.Slider(FieldLabel("발음 강도"), articulation, 0f, 1f);
            blink = EditorGUILayout.Slider(FieldLabel("눈 감기"), blink, 0f, 1f);
            int look = GUILayout.SelectionGrid(
                -1,
                new[] { "중립", "미소", "눈 감기", "입 벌리기", "F/V", "TH" },
                NavigationColumns
            );
            if (look >= 0)
            {
                emotionIndex = look == 1 ? 1 : 0;
                blink = look == 2 ? 1f : 0f;
                visemeIndex =
                    look == 3 ? 10
                    : look == 4 ? 2
                    : look == 5 ? 3
                    : 0;
                articulation = look >= 3 ? 1f : 0f;
            }
            DrawFaceSequenceControls();
            DrawSpeechPanel();
            showBindings = EditorGUILayout.Foldout(
                showBindings,
                "얼굴 메시 경로·Shape Key 연결",
                true
            );
            if (showBindings)
                DrawRecordProperty("faceBindings");
            EditorGUILayout.HelpBox(
                "발음 중 입꼬리 감정은 억제하고 눈·눈썹 감정은 유지합니다. 턱 본을 별도로 중복 구동하지 않습니다. 실제 대화 음성 연결은 런타임 연동 단계에서 검증합니다.",
                MessageType.None
            );
        }

        private void DrawPartsPanel()
        {
            EditorGUILayout.HelpBox(
                "소켓은 캐릭터 루트 기준 본 경로를 사용합니다. 왼쪽·오른쪽 귀걸이는 독립 슬롯입니다. 스킨 옷·헤어는 기준 모델과 같은 본 경로 및 바인드 포즈가 필요합니다.",
                MessageType.Info
            );
            BeginButtonRow();
            {
                if (GUILayout.Button("왼쪽 귀걸이"))
                    AddSlot("LeftEar", TripoAssetKind.Accessory);
                if (GUILayout.Button("오른쪽 귀걸이"))
                    AddSlot("RightEar", TripoAssetKind.Accessory);
                if (GUILayout.Button("헤어"))
                    AddSlot("Hair", TripoAssetKind.Hair);
                if (GUILayout.Button("의상"))
                    AddSlot("Clothing", TripoAssetKind.Clothing);
                if (GUILayout.Button("악세서리"))
                    AddSlot("Accessory", TripoAssetKind.Accessory);
            }
            EndButtonRow();
            DrawRecordProperty("parts");
            for (int i = 0; i < record.parts.Count; i++)
            {
                var slot = record.parts[i];
                if (GUILayout.Button(slot.name + " 부품 가져오기 안내"))
                {
                    generationKind = slot.category;
                    singleImage = true;
                    autoRig = false;
                    selectedTab = 0;
                }
            }
            if (GUILayout.Button("조립 미리보기 새로고침"))
                ResetPreview();
        }

        private void AddSlot(string name, TripoAssetKind category)
        {
            string path = "";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
            var animator = model == null ? null : model.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && category != TripoAssetKind.Clothing)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                    path = AnimationUtility.CalculateTransformPath(head, model.transform);
            }
            record.parts.Add(
                new TripoPartSlot
                {
                    name = name,
                    category = category,
                    bonePath = path,
                    attachment =
                        category == TripoAssetKind.Clothing
                            ? TripoAttachmentKind.Skinned
                            : TripoAttachmentKind.Rigid,
                }
            );
            SaveRecord();
        }

        private void DrawValidationPanel()
        {
            EditorGUILayout.LabelField(
                "결과 확인 · 마우스로 드래그하여 회전 / 휠로 확대",
                EditorStyles.wordWrappedLabel
            );
            DrawModelPreview();
            DrawSavedPath("현재 검사 모델", record.ModelPath);
            if (File.Exists(record.ModelPath) && GUILayout.Button("현재 모델 저장 폴더 열기"))
                EditorUtility.RevealInFinder(Path.GetFullPath(record.ModelPath));
            DrawSavedPath("작업 기록", AssetDatabase.GetAssetPath(record));
            if (!string.IsNullOrEmpty(record.registeredPrefabPath))
                DrawSavedPath("등록한 검수 프리팹", record.registeredPrefabPath);

            EditorGUILayout.LabelField(
                $"검사 기준: {(record.profile == null ? "미지정" : record.profile.name)} · Humanoid {(record.requireHumanoid ? "필수" : "선택")} · 얼굴 {(record.requireFace ? "필수" : "선택")}",
                EditorStyles.wordWrappedMiniLabel
            );
            showValidationOptions = EditorGUILayout.Foldout(
                showValidationOptions,
                "검사 설정 · 기준값 / 접합 샘플",
                true
            );
            if (showValidationOptions)
            {
                EditorGUI.BeginChangeCheck();
                record.profile = (TripoValidationProfile)
                    EditorGUILayout.ObjectField(
                        FieldLabel("검증 기준"),
                        record.profile,
                        typeof(TripoValidationProfile),
                        false
                    );
                record.requireHumanoid = EditorGUILayout.Toggle(
                    FieldLabel("Humanoid 준비 검사 필수"),
                    record.requireHumanoid
                );
                record.requireFace = EditorGUILayout.Toggle(
                    FieldLabel("대화용 29 얼굴 컨트롤 필수"),
                    record.requireFace
                );
                if (EditorGUI.EndChangeCheck())
                    SaveRecord();
                if (showAdvancedTools || record.contacts.Count > 0)
                {
                    DrawRecordProperty("contacts");
                    if (
                        record.contacts.Count > 0
                        && GUILayout.Button("접합 샘플 경로·정점 번호 재확인 완료")
                    )
                        RunEditorAction(() =>
                        {
                            var assembled = TripoWorkbench.CreateAssembly(record);
                            try
                            {
                                record.contactTopologyFingerprint =
                                    TripoContactAudit.GetTopologyFingerprint(assembled);
                            }
                            finally
                            {
                                DestroyImmediate(assembled);
                            }
                            SaveRecord();
                        });
                    EditorGUILayout.HelpBox(
                        "고급 접합 샘플: 현재 Unity 모델의 메시 경로와 정점 번호를 지정합니다. Seam은 닫혀야 하는 정점 쌍, Penetration은 관통하면 안 되는 지점입니다. 의도한 숨은 겹침은 금지 관통 샘플에 넣지 말고 수동 검수 기록에 남기세요. 토폴로지 변경 후에는 샘플을 다시 지정하세요.",
                        MessageType.Info
                    );
                }
                if (GUILayout.Button("검증값 Inspector에서 조정"))
                    Selection.activeObject = record.profile;
            }
            EditorGUILayout.HelpBox(
                record.automaticStatus ?? "자동 보정·검증이 필요합니다.",
                MessageType.Info
            );
            DrawSavedPath("최종 등록 예정 위치", record.finalPrefabPath);
            if (!string.IsNullOrEmpty(record.reviewScenePath))
            {
                DrawSavedPath("자동 준비한 동작 검수 씬", record.reviewScenePath);
                if (GUILayout.Button("동작 검수 씬 열기"))
                    UnityEditor.AssetDatabase.OpenAsset(
                        AssetDatabase.LoadAssetAtPath<SceneAsset>(record.reviewScenePath)
                    );
            }
            if (GUILayout.Button("자동 보정·검증 다시 실행 · API 사용 없음"))
                RunEditorAction(() =>
                {
                    TripoLocalImport.Review(record);
                    ResetPreview();
                });
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(record.revision.candidatePath)
                    ? "검사 대상: 현재 모델"
                    : "검사 대상: 수정 후보 · 아직 미적용",
                EditorStyles.boldLabel
            );
            EditorGUILayout.LabelField(record.ModelPath ?? "", EditorStyles.wordWrappedMiniLabel);
            bool current = TripoLocalImport.ReadyForAppearance(record);
            EditorGUILayout.HelpBox(
                !current ? "검증 전 또는 변경 후 재검증 필요"
                    : record.revision.validationPassed
                        ? "자동 보정·동작 검사 통과 · 마지막 외관 확인"
                    : "자동 검사 오류 수정 필요",
                current && record.revision.validationPassed ? MessageType.Info : MessageType.Warning
            );
            if (record.validationErrors.Count > 0 || record.manualChecks.Count > 0)
            {
                if (!current)
                    EditorGUILayout.LabelField(
                        "이전 검사 결과 · 현재 모델은 재검사 필요",
                        EditorStyles.boldLabel
                    );
                EditorGUILayout.LabelField(
                    $"자동 검사 오류 {record.validationErrors.Count}개",
                    EditorStyles.boldLabel
                );
                foreach (string error in record.validationErrors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                EditorGUILayout.LabelField(
                    $"사람이 확인할 항목 {record.manualChecks.Count}개",
                    EditorStyles.boldLabel
                );
                foreach (string manual in record.manualChecks)
                    EditorGUILayout.LabelField(
                        "수동 검수: " + manual,
                        EditorStyles.wordWrappedLabel
                    );
            }
            EditorGUILayout.HelpBox(
                "자동 검사와 별도로 외형·동작을 확인하세요. 미확보 모션과 아직 보지 않은 수동 항목은 검수 완료로 처리하지 마세요.",
                MessageType.Info
            );
            EditorGUILayout.LabelField(
                "검사·Blender 왕복은 API 크레딧을 쓰지 않습니다. API 재생성은 별도 과금됩니다.",
                EditorStyles.wordWrappedMiniLabel
            );
            showValidationHelp = EditorGUILayout.Foldout(
                showValidationHelp,
                "외형·동작 검수 방법",
                true
            );
            if (showValidationHelp)
            {
                EditorGUILayout.LabelField(
                    "정면·측면·후면과 얼굴 확대에서 눈썹·입술 경계, 텍스처 누락, 옷 관통을 확인하세요. 최소·기준·최대 크기에서 확보한 모션으로 검수하세요. UV 항목의 메시/서브메시와 정점·삼각형 번호로 위치를 찾을 수 있습니다.",
                    EditorStyles.wordWrappedLabel
                );
                if (record.requireFace || record.faceBindings.Count > 0 || showAdvancedTools)
                    EditorGUILayout.LabelField(
                        "표정이 있으면 눈·입 닫힘과 7감정×15발음 조합을 검수하세요. 립싱크 오차는 승인된 음성 타임라인으로 측정합니다.",
                        EditorStyles.wordWrappedLabel
                    );
            }
            using (new EditorGUI.DisabledScope(!current || !record.revision.validationPassed))
            {
                bool approved = EditorGUILayout.ToggleLeft(
                    "외관과 표시된 확인 항목을 확인했고, 이 결과를 사용하겠습니다",
                    record.revision.visuallyApproved
                );
                if (approved != record.revision.visuallyApproved)
                {
                    record.revision.visuallyApproved = approved;
                    SaveRecord(false);
                }
                using (new EditorGUI.DisabledScope(!record.revision.visuallyApproved))
                    if (GUILayout.Button("외관 승인한 최종 프리팹 등록"))
                        RunEditorAction(() =>
                        {
                            string path = record.finalPrefabPath;
                            if (string.IsNullOrEmpty(path))
                                throw new InvalidOperationException(
                                    "자동 보정·검증을 먼저 실행해 저장 위치를 준비하세요."
                                );
                            if (!string.IsNullOrEmpty(path))
                                Selection.activeObject = TripoWorkbench.Register(record, path);
                        });
            }
            DrawRevisionButtons();
            if (GUILayout.Button("현재 검사 모델 Inspector에서 확인"))
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(record.ModelPath);
            if (GUILayout.Button("문제 수정 · Blender 단계로 이동"))
                selectedTab = 2;
            if (
                !string.IsNullOrEmpty(record.registeredPrefabPath)
                && GUILayout.Button("등록 프리팹 열기")
            )
                PrefabStageUtility.OpenPrefab(record.registeredPrefabPath);
        }

        private void DrawRevisionButtons()
        {
            if (!string.IsNullOrEmpty(record.revision.candidatePath))
            {
                BeginButtonRow();
                using (
                    new EditorGUI.DisabledScope(
                        !record.revision.validationPassed
                            || !record.revision.visuallyApproved
                            || record.revision.validatedFingerprint != record.GetFingerprint()
                    )
                )
                    if (GUILayout.Button("검증한 수정본 적용"))
                        RunEditorAction(() =>
                        {
                            record.revision.Apply(record.GetFingerprint());
                            SaveRecord(false);
                        });
                if (GUILayout.Button("후보 제외 · 현재 모델 재검사"))
                    RunEditorAction(() =>
                    {
                        TripoLocalWorkflow.DiscardCandidate(record);
                        ResetPreview();
                    });
                EndButtonRow();
            }
            if (!string.IsNullOrEmpty(record.revision.previousPath))
                if (GUILayout.Button("이전 모델 버전 복원"))
                    RunEditorAction(() =>
                    {
                        record.revision.Restore();
                        TripoWorkbench.Validate(record);
                        ResetPreview();
                    });
        }

        private void DrawRecordProperty(string name)
        {
            var serialized = new SerializedObject(record);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
            if (serialized.ApplyModifiedProperties())
                SaveRecord();
        }

        private void SaveRecord(bool invalidate = true)
        {
            if (invalidate)
                record.revision.Invalidate();
            EditorUtility.SetDirty(record);
            AssetDatabase.SaveAssets();
            ResetPreview();
        }

        private void RunEditorAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void ResetPreview()
        {
            StopSpeech();
            faceSweep = false;
            useFaceClip = false;
            releaseStarted = -1d;
            articulation = 0f;
            speechReport = string.Empty;
            bodyMotionPlaying = false;
            bodyMotionPhase = 0f;
            bodyMotionKind = TripoReviewMotion.Rest;
            bodyMotion?.Dispose();
            bodyMotion = null;
            modelPreview?.Dispose();
            modelPreview = null;
            previewFingerprint = null;
            previewRigReport = null;
        }

        private void UpdateBodyMotion()
        {
            if (!bodyMotionPlaying)
                return;
            double now = EditorApplication.timeSinceStartup;
            if (selectedTab != 1 && selectedTab != 5)
            {
                bodyMotionPlaying = false;
                return;
            }
            bodyMotionPhase = Mathf.Repeat(
                bodyMotionPhase
                    + (float)(now - bodyMotionUpdated)
                        * bodyMotionSpeed
                        / Mathf.Max(bodyMotion?.Duration ?? 4f, 0.01f),
                1f
            );
            bodyMotionUpdated = now;
            Repaint();
        }

        private void DrawBodyMotionControls()
        {
            BeginButtonRow();
            string[] views = { "정면", "캐릭터 좌측", "캐릭터 우측", "후면" };
            for (int i = 0; i < views.Length; i++)
                if (GUILayout.Button(views[i]))
                    modelPreview.SetView(i);
            EndButtonRow();
            EditorGUILayout.LabelField(
                "좌우는 캐릭터 본인 기준입니다. Humanoid는 골격, 그 외 모델은 루트 축을 기준으로 봅니다.",
                EditorStyles.wordWrappedMiniLabel
            );
            previewRigReport ??= TripoModelReview.InspectRig(modelPreview.Model);
            foreach (string error in previewRigReport.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string check in previewRigReport.ManualChecks)
                EditorGUILayout.HelpBox(check, MessageType.Warning);
            if (!previewRigReport.Passed)
                return;
            EditorGUILayout.LabelField(
                "골격·스킨 연결 확인 · 동작 외형은 검수 필요",
                EditorStyles.wordWrappedMiniLabel
            );
            showReviewBones = EditorGUILayout.ToggleLeft("골격 연결 표시", showReviewBones);
            modelPreview.ShowSkeleton = showReviewBones;
            var selected = (TripoReviewMotion)
                EditorGUILayout.Popup(
                    FieldLabel("검수 동작"),
                    (int)bodyMotionKind,
                    new[]
                    {
                        "기준 자세",
                        "팔 올리기",
                        "팔꿈치 굽히기",
                        "무릎 굽히기",
                        "플레이어 걷기",
                        "플레이어 달리기",
                        "점프 · 상승",
                        "점프 · 하강",
                        "점프 · 착지",
                        "플레이어 정지 · Idle",
                    }
                );
            if (selected != bodyMotionKind)
            {
                bodyMotion?.Dispose();
                bodyMotion = null;
                bodyMotionKind = selected;
                bodyMotionPhase = 0f;
                bodyMotionPlaying = false;
            }
            if (bodyMotionKind == TripoReviewMotion.Rest)
            {
                bodyMotion?.Dispose();
                bodyMotion = null;
            }
            else
            {
                if (bodyMotion == null)
                    bodyMotion = new TripoMotionPreview(modelPreview.Model);
                EditorGUI.BeginChangeCheck();
                bodyMotionPhase = EditorGUILayout.Slider(
                    FieldLabel("동작 구간"),
                    bodyMotionPhase,
                    0f,
                    1f
                );
                if (EditorGUI.EndChangeCheck())
                    bodyMotionPlaying = false;
                bodyMotionSpeed = EditorGUILayout.Slider(
                    FieldLabel("재생 속도"),
                    bodyMotionSpeed,
                    0.25f,
                    1f
                );
                BeginButtonRow();
                if (GUILayout.Button(bodyMotionPlaying ? "일시정지" : "반복 재생"))
                {
                    bodyMotionPlaying = !bodyMotionPlaying;
                    bodyMotionUpdated = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("기준 자세로 복원"))
                {
                    bodyMotionPlaying = false;
                    bodyMotionKind = TripoReviewMotion.Rest;
                    bodyMotion.Dispose();
                    bodyMotion = null;
                    bodyMotionPhase = 0f;
                }
                EndButtonRow();
                bodyMotion?.Apply(bodyMotionKind, bodyMotionPhase);
                if (bodyMotionKind >= TripoReviewMotion.Walk)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(
                            FieldLabel("재사용 모션"),
                            bodyMotion.ActiveClip,
                            typeof(AnimationClip),
                            false
                        );
                    EditorGUILayout.LabelField(
                        $"{bodyMotionPhase * bodyMotion.Duration:F2} / {bodyMotion.Duration:F2}초 · 모델 자체 Avatar 사용",
                        EditorStyles.wordWrappedMiniLabel
                    );
                }
            }
            EditorGUILayout.HelpBox(
                "로컬 복사본에 실제 플레이어 모션을 적용합니다. 발 뒤집힘·옷 틈·손/몸 관통을 각 방향에서 확인하세요. 평면은 기준 자세 발밑 가이드입니다. 마스터 적용본은 전용 Idle과 런타임 팔 보정을 함께 확인합니다. 실제 이동·점프 높이·접지 IK는 별도 플레이 검수 씬에서 확인하며, 재생 성공만으로 외형을 승인하지 않습니다. API 크레딧은 사용하지 않습니다.",
                MessageType.None
            );
        }

        private void DrawModelPreview()
        {
            try
            {
                string fingerprint = record.GetFingerprint();
                if (modelPreview == null || fingerprint != previewFingerprint)
                {
                    bodyMotionPlaying = false;
                    bodyMotion?.Dispose();
                    bodyMotion = null;
                    modelPreview?.Dispose();
                    modelPreview = new TripoModelPreview(TripoWorkbench.CreateAssembly(record));
                    previewFingerprint = fingerprint;
                    previewRigReport = null;
                }
                if (selectedTab == 5 || selectedTab == 1)
                    DrawBodyMotionControls();
                TripoFaceBindings.Apply(
                    modelPreview.Model,
                    record.faceBindings,
                    PreviewFaceWeights()
                );
                if (StageUtility.GetCurrentStage() is TripoPoseStage poseStage)
                    poseStage.ApplyFace(record, PreviewFaceWeights());
                MeasureSpeechFrame();
                modelPreview.Draw(
                    GUILayoutUtility.GetRect(
                        ContentWidth,
                        Mathf.Min(ContentWidth, 360f),
                        GUILayout.ExpandWidth(false)
                    )
                );
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Warning);
            }
        }
    }
}
