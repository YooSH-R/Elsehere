using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    public static class JohnParkRapBuilder
    {
        public const string DefinitionPath = "Assets/Data/Npc/JohnPark/OrderRap.asset";
        public const string VoicePath = "Assets/Audio/Characters/JohnParkDialogue/OrderRap.bytes";
        public const string GuideVoicePath =
            "Assets/Audio/Characters/JohnParkDialogue/ReplayGuide.bytes";

        [Serializable]
        private sealed class GuideText
        {
            public string english;
            public string korean;
        }

        [MenuItem("Tools/A305/Npc/John Park/Connect Recorded Order Rap")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            AssetDatabase.Refresh();
            var voice = AssetDatabase.LoadAssetAtPath<TextAsset>(VoicePath);
            var decoded = voice != null ? DialogueSpeechWav.Read(voice.bytes) : null;
            if (decoded?.Timeline == null || decoded.Emotions == null)
                throw new InvalidOperationException(
                    "발음·감정 시간표가 든 주문 랩 음성이 필요합니다."
                );
            var definition = AssetDatabase.LoadAssetAtPath<RecordedPerformanceDefinition>(
                DefinitionPath
            );
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<RecordedPerformanceDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            definition.Voice = voice;
            var guide = AssetDatabase.LoadAssetAtPath<TextAsset>(GuideVoicePath);
            var guideDecoded = guide != null ? DialogueSpeechWav.Read(guide.bytes) : null;
            if (guideDecoded?.Timeline == null || guideDecoded.Emotions == null)
                throw new InvalidOperationException("안내 음성의 발음·감정 시간표가 필요합니다.");
            definition.GuideVoice = guide;
            var guideText = JsonUtility.FromJson<GuideText>(
                File.ReadAllText(
                    "../ArtSources/Characters/ServiceNpcs/LittleMung_JohnPark_Male/ReplayGuide/Guide.json"
                )
            );
            if (
                string.IsNullOrWhiteSpace(guideText.english)
                || string.IsNullOrWhiteSpace(guideText.korean)
            )
                throw new InvalidOperationException(
                    "안내 음성과 함께 저장된 원문·번역이 필요합니다."
                );
            definition.GuideEnglish = guideText.english;
            definition.GuideKorean = guideText.korean;
            definition.English =
                "Cheeseburger, fries, lemonade on ice!\nAdd an apple pie, yeah, that sounds nice!\nMake that two, one for me, one for you.\nThat's my order, now tell me yours!";
            definition.Korean =
                "치즈버거, 감자튀김, 얼음 넣은 레모네이드!\n애플파이도 하나, 그래 딱 좋아!\n두 개로 해줘, 하나는 내 것, 하나는 네 것.\n내 주문은 이거야, 네 것도 말해줘!";
            definition.BeatsPerMinute = 121;
            // 원본 4초 동작에는 팔 강세가 두 번 있다. 한 마디의 스네어 2·4박에 맞춘다.
            definition.BeatsPerAnimationLoop = 4;
            definition.AnimationState = JohnParkRhythmBuilder.StateName;
            EditorUtility.SetDirty(definition);
            var root = PrefabUtility.LoadPrefabContents(JohnParkRhythmBuilder.PrefabPath);
            try
            {
                var performer = root.GetComponent<RecordedNpcPerformance>();
                if (performer == null)
                    performer = root.AddComponent<RecordedNpcPerformance>();
                BuilderTools.SetReference(performer, "definition", definition);
                BuilderTools.SetReference(
                    performer,
                    "speech",
                    root.GetComponent<CharacterSpeechPlayer>()
                );
                BuilderTools.SetReference(performer, "source", root.GetComponent<AudioSource>());
                BuilderTools.SetReference(
                    performer,
                    "animator",
                    root.GetComponentInChildren<Animator>()
                );
                PrefabUtility.SaveAsPrefabAsset(root, JohnParkRhythmBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("존박 저장 주문 랩·발음·리듬·재요청 연결 완료");
        }

        public static void BuildAndCapture()
        {
            Build();
            JohnParkRapValidation.Capture();
            VerifyRestaurantConnection();
        }

        private static void VerifyRestaurantConnection()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    JohnParkDialogueBuilder.RestaurantScenePath
                );
                var npc = BuilderTools
                    .FindAllInScene<NpcIdentity>(scene)
                    .Single(item => item.Code == JohnParkEmotionBuilder.NpcCode);
                var performer = npc.GetComponent<RecordedNpcPerformance>();
                if (
                    performer == null
                    || performer.Definition
                        != AssetDatabase.LoadAssetAtPath<RecordedPerformanceDefinition>(
                            DefinitionPath
                        )
                )
                    throw new InvalidOperationException("식당 존박에 저장 공연 연결이 없습니다.");
                var shop = BuilderTools
                    .FindAllInScene<Transform>(scene)
                    .Single(item => item.name == "MapleDiner" && item.Find("Interior") != null);
                if (!JohnParkDialogueBuilder.IsInsideRestaurant(shop, npc.transform))
                    throw new InvalidOperationException("존박이 식당 실내에 없습니다.");
                Debug.Log("식당 존박의 프리팹 공연 상속·실내 연결 확인 완료");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
