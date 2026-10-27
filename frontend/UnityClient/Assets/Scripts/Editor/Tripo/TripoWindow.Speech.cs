using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed partial class TripoWindow
    {
        [SerializeField]
        private AudioClip speechClip;

        [SerializeField]
        private TextAsset speechTimelineFile;
        private TripoSpeechTimeline speechTimeline;
        private TripoAudioPreview audioPreview;
        private bool speechPlaying;
        private bool speechUsesAudio;
        private double speechStarted;
        private double releaseStarted = -1d;
        private float releaseFrom;
        private int nextSpeechCue;
        private int missedSpeechCues;
        private readonly List<float> speechErrors = new List<float>();
        private string speechReport = "";

        private void DrawSpeechPanel()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("음성·발음 시간표 검수", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(speechPlaying))
            {
                speechClip = (AudioClip)
                    EditorGUILayout.ObjectField(
                        FieldLabel("검수 음성"),
                        speechClip,
                        typeof(AudioClip),
                        false
                    );
                speechTimelineFile = (TextAsset)
                    EditorGUILayout.ObjectField(
                        FieldLabel("발음 시간표 JSON"),
                        speechTimelineFile,
                        typeof(TextAsset),
                        false
                    );
                BeginButtonRow();
                {
                    using (
                        new EditorGUI.DisabledScope(
                            speechClip == null
                                || speechTimelineFile == null
                                || !TripoAudioPreview.IsSupported
                        )
                    )
                        if (GUILayout.Button("음성에 맞춰 재생"))
                            RunEditorAction(() => StartSpeech(true));
                    if (GUILayout.Button("15발음 무음 순서 검수"))
                        RunEditorAction(() => StartSpeech(false));
                }
                EndButtonRow();
            }
            if (speechPlaying && GUILayout.Button("발화 중단 · 입 모양 복원"))
                StopSpeech();
            if (!string.IsNullOrEmpty(speechReport))
                EditorGUILayout.HelpBox(speechReport, MessageType.Info);
            EditorGUILayout.HelpBox(
                "시간표 형식: { \"cues\": [{ \"start\": 0.1, \"end\": 0.3, \"viseme\": \"FF\", \"weight\": 1 }] }. 시간은 음성 시작 기준 초 단위입니다. 측정값은 에디터 구동 시점의 오차이며 음소 정답·기기 출력 지연은 별도 청각 검수가 필요합니다.",
                MessageType.None
            );
        }

        private void StartSpeech(bool withAudio)
        {
            StopSpeech();
            faceSweep = false;
            useFaceClip = false;
            if (record == null)
                throw new InvalidOperationException("모델 작업을 선택하세요.");
            if (withAudio)
            {
                if (
                    speechClip == null
                    || speechTimelineFile == null
                    || speechTimelineFile.text.Length > 1000000
                )
                    throw new InvalidOperationException(
                        "음성과 1MB 이하 시간표 JSON이 필요합니다."
                    );
                speechTimeline = JsonUtility.FromJson<TripoSpeechTimeline>(speechTimelineFile.text);
                if (speechTimeline == null)
                    throw new InvalidOperationException("발음 시간표를 읽을 수 없습니다.");
                speechTimeline.Validate(speechClip.length);
                audioPreview = new TripoAudioPreview();
                audioPreview.Play(speechClip);
            }
            else
            {
                var cues = new TripoSpeechCue[TripoFacePreview.Visemes.Length];
                for (int i = 0; i < cues.Length; i++)
                    cues[i] = new TripoSpeechCue
                    {
                        start = i * 0.4f,
                        end = i * 0.4f + 0.35f,
                        viseme = TripoFacePreview.Visemes[i],
                    };
                speechTimeline = new TripoSpeechTimeline { cues = cues };
                speechTimeline.Validate(cues[cues.Length - 1].end + 0.1f);
            }
            speechUsesAudio = withAudio;
            speechStarted = EditorApplication.timeSinceStartup;
            speechPlaying = true;
            releaseStarted = -1d;
            speechErrors.Clear();
            nextSpeechCue = 0;
            missedSpeechCues = 0;
            speechReport = withAudio
                ? "음성 재생 위치를 따라 발음을 구동합니다."
                : "무음 형태 검수 중 · 음성 동기화 판정에는 사용하지 않습니다.";
        }

        private void UpdateSpeech()
        {
            if (this == null)
                return;
            if (faceSweep)
            {
                int index = (int)((EditorApplication.timeSinceStartup - faceSweepStarted) / 0.8d);
                if (index >= TripoFacePreview.Emotions.Length * TripoFacePreview.Visemes.Length)
                {
                    faceSweep = false;
                    emotionIndex = 0;
                    visemeIndex = 0;
                    articulation = 0f;
                    message = "105조합 순회를 마쳤습니다. 시각 품질의 자동 합격 판정은 아닙니다.";
                }
                else
                {
                    emotionIndex = index / TripoFacePreview.Visemes.Length;
                    visemeIndex = index % TripoFacePreview.Visemes.Length;
                    articulation = visemeIndex == 0 ? 0f : 1f;
                }
                Repaint();
                return;
            }
            if (!speechPlaying)
            {
                if (releaseStarted < 0d)
                    return;
                float release =
                    record == null || record.profile == null
                        ? 150f
                        : record.profile.SpeechReleaseMilliseconds;
                articulation =
                    releaseFrom
                    * TripoFacePreview.ReleaseWeight(
                        (float)((EditorApplication.timeSinceStartup - releaseStarted) * 1000d),
                        release
                    );
                if (articulation <= 0f)
                {
                    articulation = 0f;
                    visemeIndex = 0;
                    releaseStarted = -1d;
                }
                Repaint();
                return;
            }
            try
            {
                float elapsed = (float)(EditorApplication.timeSinceStartup - speechStarted);
                if (speechUsesAudio && !audioPreview.IsPlaying && elapsed > 0.2f)
                {
                    StopSpeech();
                    return;
                }
                float cursor = speechUsesAudio ? audioPreview.Position : elapsed;
                var current = speechTimeline.Sample(cursor);
                visemeIndex =
                    current == null ? 0 : Array.IndexOf(TripoFacePreview.Visemes, current.viseme);
                articulation = current == null ? 0f : current.weight;
                if (
                    !speechUsesAudio
                    && cursor > speechTimeline.cues[speechTimeline.cues.Length - 1].end + 0.1f
                )
                    StopSpeech();
                Repaint();
            }
            catch (Exception exception)
            {
                StopSpeech();
                ShowError(exception);
            }
        }

        private void MeasureSpeechFrame()
        {
            if (!speechPlaying || modelPreview == null || Event.current.type != EventType.Repaint)
                return;
            float cursor = speechUsesAudio
                ? audioPreview.Position
                : (float)(EditorApplication.timeSinceStartup - speechStarted);
            while (
                nextSpeechCue < speechTimeline.cues.Length
                && speechTimeline.cues[nextSpeechCue].start <= cursor
            )
            {
                var cue = speechTimeline.cues[nextSpeechCue++];
                if (cursor >= cue.end)
                    missedSpeechCues++;
                speechErrors.Add((cursor - cue.start) * 1000f);
            }
        }

        private void StopSpeech()
        {
            audioPreview?.Dispose();
            audioPreview = null;
            if (!speechPlaying)
                return;
            speechPlaying = false;
            releaseFrom = articulation;
            releaseStarted = EditorApplication.timeSinceStartup;
            var timing = TripoTimingReport.Measure(
                speechErrors,
                record == null ? null : record.profile
            );
            bool faceReady =
                record != null
                && modelPreview != null
                && TripoFaceBindings.Validate(modelPreview.Model, record.faceBindings).Count == 0;
            bool complete =
                faceReady
                && speechTimeline != null
                && nextSpeechCue == speechTimeline.cues.Length
                && missedSpeechCues == 0;
            speechReport =
                (speechUsesAudio ? "음성 시계 기준 에디터 구동" : "무음 형태 검수")
                + $" · 관측 {speechErrors.Count}구간 · 놓친 구간 {missedSpeechCues}\nP95 {timing.Percentile95:F1}ms / 최대 {timing.Maximum:F1}ms"
                + (
                    complete && timing.Passed && speechUsesAudio
                        ? " · 구동 오차 기준 통과"
                        : " · 음성 동기화 합격 판정 없음"
                )
                + "\n실제 음소 정확도와 출력 지연, 표정의 자연스러움은 별도 검수합니다.";
        }
    }
}
