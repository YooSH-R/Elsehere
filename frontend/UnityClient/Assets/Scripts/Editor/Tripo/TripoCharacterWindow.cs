using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoCharacterWindow : EditorWindow
    {
        [SerializeField]
        private TripoCharacterLibrary library;

        [SerializeField]
        private TripoCharacterPart head;

        [SerializeField]
        private TripoCharacterPart body;

        [SerializeField]
        private TripoCharacterComposition composition;

        [SerializeField]
        private string assetName = "LittleMung_Casual_Variant";

        [SerializeField]
        private string outputFolder = "Assets/Art/Characters/Variants";

        [SerializeField]
        private GameObject preparedSource;

        [SerializeField]
        private TripoCharacterRegion newPartRegion;
        private TripoModelPreview preview;
        private TripoMotionPreview motion;
        private TripoAuditReport report;
        private TripoReviewMotion selectedMotion = TripoReviewMotion.Idle;
        private Vector2 scroll;
        private bool busy,
            playing,
            reviewed,
            showDetails,
            showPreparation;
        private float phase;
        private double previousTime;
        private string status = "부품 선택 → 후보 만들기 → 동작·목 경계 확인 → 승인·등록";

        [MenuItem("Tools/A305/Assets/Common Character Builder")]
        public static void Open() => GetWindow<TripoCharacterWindow>("공통 캐릭터 조합");

        private void OnEnable()
        {
            minSize = new Vector2(360, 400);
            if (library == null)
                library = AssetDatabase.LoadAssetAtPath<TripoCharacterLibrary>(
                    TripoCharacterBootstrap.LibraryPath
                );
            if (library != null)
            {
                if (head == null)
                    head = library.head;
                if (body == null)
                    body = library.dressedBody;
            }
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            ResetPreview();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is TripoCharacterComposition selected)
            {
                composition = selected;
                library = selected.library;
                head = selected.head;
                body = selected.dressedBody;
                reviewed = false;
                report = null;
                ResetPreview();
                Repaint();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("공통 바디로 캐릭터 만들기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 작업은 로컬 부품 조합입니다. API 키·생성·자동 리깅 비용이 없습니다. 같은 착의 바디는 메시와 가중치를 그대로 재사용합니다.",
                MessageType.Info
            );
            using (new EditorGUI.DisabledScope(busy))
            {
                EditorGUI.BeginChangeCheck();
                library = Field("공통 기준", library);
                head = Field("머리·얼굴 부품", head);
                body = Field("착의 바디 부품", body);
                if (EditorGUI.EndChangeCheck())
                {
                    composition = null;
                    report = null;
                    reviewed = false;
                    ResetPreview();
                }
                if (library != null)
                {
                    if (GUILayout.Button("기본 머리·바디 선택"))
                    {
                        head = library.head;
                        body = library.dressedBody;
                        composition = null;
                        report = null;
                        reviewed = false;
                        ResetPreview();
                    }
                    EditorGUILayout.LabelField(
                        "기준 상태",
                        library.IsCurrent ? "고정된 기준 버전" : "기준 변경됨 · 검토 필요"
                    );
                }
                if (head != null)
                    EditorGUILayout.LabelField(
                        "머리 검수",
                        head.IsApproved ? "현재 버전 승인됨" : "첫 검수 또는 재검수 필요"
                    );
                if (body != null)
                    EditorGUILayout.LabelField(
                        "바디 검수",
                        body.IsApproved ? "현재 버전 승인됨" : "첫 검수 또는 재검수 필요"
                    );
                assetName = EditorGUILayout.TextField("캐릭터 이름", assetName);
                outputFolder = EditorGUILayout.TextField("후보 저장 폴더", outputFolder);
                using (new EditorGUI.DisabledScope(library == null || head == null || body == null))
                    if (GUILayout.Button("새 조합 후보 만들기 · API 사용 없음"))
                        Run(() =>
                        {
                            RequireName();
                            string folder = AssetDatabase.GenerateUniqueAssetPath(
                                TripoJob.NormalizeOutputFolder(outputFolder) + "/" + assetName
                            );
                            composition = TripoCharacterAssembly.Build(library, head, body, folder);
                            report = TripoCharacterAssembly.Validate(composition);
                            reviewed = false;
                            ResetPreview();
                            Selection.activeObject = composition;
                            status =
                                "후보 저장: " + AssetDatabase.GetAssetPath(composition.preview);
                        });
                DrawPreparation();
            }
            EditorGUILayout.HelpBox(busy ? "로컬 처리·검사 중…" : status, MessageType.None);
            if (composition != null && composition.preview != null)
                DrawCandidate();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCandidate()
        {
            EditorGUILayout.LabelField(
                "현재 후보",
                AssetDatabase.GetAssetPath(composition.preview),
                EditorStyles.wordWrappedLabel
            );
            if (preview == null)
            {
                preview = new TripoModelPreview(Instantiate(composition.preview));
                motion = new TripoMotionPreview(preview.Model);
            }
            EditorGUILayout.BeginHorizontal();
            string[] views = { "정면", "좌측", "우측", "후면" };
            for (int i = 0; i < views.Length; i++)
                if (GUILayout.Button(views[i]))
                    preview.SetView(i);
            EditorGUILayout.EndHorizontal();
            selectedMotion = (TripoReviewMotion)
                EditorGUILayout.EnumPopup("검수 동작", selectedMotion);
            phase = EditorGUILayout.Slider("동작 위치", phase, 0f, 1f);
            if (GUILayout.Button(playing ? "재생 멈추기" : "동작 재생"))
            {
                playing = !playing;
                previousTime = EditorApplication.timeSinceStartup;
            }
            motion.Apply(selectedMotion, phase);
            preview.Draw(
                GUILayoutUtility.GetRect(
                    100,
                    Mathf.Clamp(position.width * .8f, 260f, 580f),
                    GUILayout.ExpandWidth(true)
                )
            );
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("현재 조합 다시 검사"))
                    Run(() =>
                    {
                        report = TripoCharacterAssembly.Validate(composition);
                        status = report.Passed ? "자동 검사 통과 · 시각 검수 필요" : "검사 실패";
                    });
                if (GUILayout.Button("실제 이동 검수 씬 만들기"))
                    Run(() =>
                    {
                        string scene = TripoCharacterBootstrap.BuildScene(composition);
                        status = "검수 씬 저장: " + scene;
                    });
                reviewed = EditorGUILayout.ToggleLeft(
                    "목 경계·옷·손·발·대표 동작과 아래 수동 항목을 확인했습니다",
                    reviewed
                );
                using (new EditorGUI.DisabledScope(!reviewed))
                    if (GUILayout.Button("확인한 부품·조합 승인"))
                        Run(() =>
                        {
                            TripoCharacterAssembly.Approve(composition, reviewed);
                            report = TripoCharacterAssembly.Validate(composition, true);
                            status = "현재 부품과 조합 승인 완료";
                        });
                using (
                    new EditorGUI.DisabledScope(
                        composition.visuallyApprovedFingerprint != composition.CurrentFingerprint
                            || !head.IsApproved
                            || !body.IsApproved
                    )
                )
                    if (GUILayout.Button("최종 프리팹 등록"))
                        Run(() =>
                        {
                            RequireName();
                            string path = AssetDatabase.GenerateUniqueAssetPath(
                                "Assets/Prefabs/Characters/Common/" + assetName + ".prefab"
                            );
                            status =
                                "등록 완료: " + TripoCharacterAssembly.Register(composition, path);
                        });
            }
            if (report != null)
            {
                foreach (string error in report.Errors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                showDetails = EditorGUILayout.Foldout(
                    showDetails,
                    "수동 검수 항목 · 얼굴·경계·의상",
                    true
                );
                if (showDetails)
                    foreach (string check in report.ManualChecks)
                        EditorGUILayout.HelpBox(check, MessageType.Warning);
            }
        }

        private void DrawPreparation()
        {
            showPreparation = EditorGUILayout.Foldout(
                showPreparation,
                "새 외형 부품을 처음 준비할 때",
                true
            );
            if (!showPreparation)
                return;
            EditorGUILayout.HelpBox(
                "기존 작업대에서 마스터를 적용한 모델을 선택하세요. 머리에는 얼굴·머리카락, 착의 바디에는 옷·손·다리·신발이 포함됩니다. 새 부품은 미승인으로 저장되며 조합 검수를 통과한 뒤 재사용합니다. 얼굴 Blend Shape가 없는 모델에 표정·립싱크가 자동으로 생기지는 않습니다.",
                MessageType.Info
            );
            preparedSource = Field("마스터 적용 모델", preparedSource);
            newPartRegion = (TripoCharacterRegion)
                EditorGUILayout.EnumPopup("분리할 영역", newPartRegion);
            using (new EditorGUI.DisabledScope(library == null || preparedSource == null))
                if (GUILayout.Button("새 부품 사본 준비"))
                    Run(() =>
                    {
                        RequireName();
                        string root = Path.GetDirectoryName(AssetDatabase.GetAssetPath(library))
                            .Replace('\\', '/');
                        var part = TripoCharacterLibraryBuilder.PreparePart(
                            library,
                            preparedSource,
                            newPartRegion,
                            AssetDatabase.GenerateUniqueAssetPath(root + "/Parts/" + assetName)
                        );
                        if (newPartRegion == TripoCharacterRegion.Head)
                            head = part;
                        else
                            body = part;
                        composition = null;
                        report = null;
                        reviewed = false;
                        ResetPreview();
                        Selection.activeObject = part;
                        status = "부품 준비 완료 · 조합 후보에서 처음 한 번 검수하세요.";
                    });
        }

        private static T Field<T>(string label, T value)
            where T : UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(label, value, typeof(T), false);

        private void RequireName()
        {
            if (!Regex.IsMatch(assetName ?? "", "^[A-Za-z][A-Za-z0-9_]*$"))
                throw new InvalidOperationException(
                    "이름은 영문으로 시작하고 영문·숫자·밑줄을 사용하세요. 버전은 폴더에서 자동 구분합니다."
                );
        }

        private void Run(Action action)
        {
            busy = true;
            Repaint();
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    status = exception.Message;
                    Debug.LogException(exception);
                }
                finally
                {
                    busy = false;
                    Repaint();
                }
            };
        }

        private void Tick()
        {
            if (!playing || motion == null || busy)
                return;
            double now = EditorApplication.timeSinceStartup;
            phase = Mathf.Repeat(phase + (float)(now - previousTime) / motion.Duration, 1f);
            previousTime = now;
            Repaint();
        }

        private void ResetPreview()
        {
            playing = false;
            motion?.Dispose();
            motion = null;
            preview?.Dispose();
            preview = null;
        }
    }
}
