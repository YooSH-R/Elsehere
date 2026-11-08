using A305.Npc;
using A305.Tutorial;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    /// <summary>보행 속도·게임 시각·신호의 비율을 유지해 연속 이틀을 관찰한다. 첫날은 준비, 둘째 날만 집계한다.</summary>
    [InitializeOnLoad]
    public static class NpcDailyObservation
    {
        private const string RunningKey = "A305.NpcDailyObservation";

        static NpcDailyObservation() => EditorApplication.playModeStateChanged += Changed;

        [MenuItem("Tools/A305/Npc/Observe Daily Commutes")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene
            );
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (
                state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(RunningKey, false)
            )
                return;
            SessionState.SetBool(RunningKey, false);
            var runner = new GameObject(
                "NpcDailyObservation"
            ).AddComponent<NpcDailyObservationRunner>();
            runner.PrepareScenario = () =>
            {
                TutorialState.Clear();
                TutorialState.MarkFinishedOnServer();
            };
        }
    }
}
