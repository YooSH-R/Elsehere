using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 튜토리얼 Ben 길 안내 대본의 첫 말과 대답에 Ben 목소리로 미리 합성한 음성을 잇는다. 길 안내는 서버 대화가 아니라 서버 음성을 받지 않는다.
    /// 음성은 ai/tools/synthesize_script_lines.py로 <see cref="VoiceFolder"/>에 Opening·Reply1… 이름으로 만든다.
    /// 서버가 음성에 붙이는 표정 시간표도 받지 않으므로, 대사에 맞는 표정을 서버와 같은 형식(e305 청크)으로 음성 파일에 저장한다.
    /// </summary>
    public static class TutorialVoiceBuilder
    {
        public const string MenuPath = "Tools/A305/Dialogue/Apply Tutorial Ben Voice";

        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";
        private const string VoiceFolder = "Assets/Audio/Dialogue/TutorialBen";
        private const string TutorialNpcCode = "TUTORIAL_PASSERBY";
        private const string EmotionChunk = "e305";

        // 길을 헤매다 처음 말을 걸며 궁금한 얼굴로 묻는다.
        private static readonly VoiceLine Opening = new VoiceLine(
            "Opening",
            CharacterEmotion.Curious
        );

        // 대답마다 이을 음성과 표정. 둘째 대사(길 안내)에서 대화를 마치므로 마지막 고마움 인사에는 처음 셋째 대답으로 만든
        // Reply3("Thank you so much. You're a lifesaver!")을 쓴다. Reply2("Got it. Is it far from here?")는 쓰지 않는다.
        private static readonly VoiceLine[] Replies =
        {
            // 인사를 받고 반갑게 웃으며 카페 가는 길을 묻는다.
            new VoiceLine("Reply1", CharacterEmotion.WarmSmile),
            // 길을 알게 되어 안도하며 고마워한다.
            new VoiceLine("Reply3", CharacterEmotion.Relieved),
        };

        private readonly struct VoiceLine
        {
            public readonly string Name;
            public readonly CharacterEmotion Emotion;

            public VoiceLine(string name, CharacterEmotion emotion)
            {
                Name = name;
                Emotion = emotion;
            }
        }

        [MenuItem("Tools/A305/Dialogue/Apply Tutorial Ben Greeting")]
        public static void ApplyGreeting()
        {
            var scripts = BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(
                ScriptsPath,
                DialogueBuilder.MenuPath
            );
            var defaults = ScriptableObject.CreateInstance<MockDialogueScriptSource>();
            var target = new SerializedObject(scripts);
            var source = new SerializedObject(defaults);
            SerializedProperty entry = FindEntry(target.FindProperty("entries"));
            SerializedProperty initial = FindEntry(source.FindProperty("entries"));
            if (entry.propertyPath != initial.propertyPath)
                throw new InvalidOperationException("튜토리얼 대본의 배열 위치가 변경됐습니다.");
            target.CopyFromSerializedProperty(initial);
            target.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(defaults);
            Apply();
            A305.EditorTools.Tutorial.TutorialBuilder.AlignServiceGuideTexts();
        }

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            var scripts = BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(
                ScriptsPath,
                DialogueBuilder.MenuPath
            );
            var serialized = new SerializedObject(scripts);
            SerializedProperty entry = FindEntry(serialized.FindProperty("entries"));
            SetVoice(entry.FindPropertyRelative("opening"), Opening);
            SerializedProperty turns = entry.FindPropertyRelative("guidedTurns");
            if (turns.arraySize != Replies.Length)
            {
                throw new InvalidOperationException(
                    $"튜토리얼 대본 대답 {turns.arraySize}개와 음성 {Replies.Length}개의 수가 다릅니다."
                );
            }

            for (int i = 0; i < turns.arraySize; i++)
            {
                SetVoice(turns.GetArrayElementAtIndex(i).FindPropertyRelative("reply"), Replies[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scripts);
            AssetDatabase.SaveAssets();
        }

        private static SerializedProperty FindEntry(SerializedProperty entries)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("npcCode").stringValue == TutorialNpcCode)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException(
                $"{ScriptsPath}에 {TutorialNpcCode} 대본이 없습니다."
            );
        }

        private static void SetVoice(SerializedProperty line, VoiceLine voiceLine)
        {
            string path = $"{VoiceFolder}/{voiceLine.Name}.bytes";
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"{path}가 없습니다. ai/tools/synthesize_script_lines.py로 먼저 만듭니다."
                );
            }

            byte[] original = File.ReadAllBytes(path);
            byte[] withEmotion = WithEmotion(original, voiceLine.Emotion);
            if (!withEmotion.AsSpan().SequenceEqual(original))
            {
                File.WriteAllBytes(path, withEmotion);
                AssetDatabase.ImportAsset(path);
            }

            line.FindPropertyRelative("voice").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        /// <summary>
        /// 음성 전체에 표정 하나를 붙인 WAV. 서버(ai/worker/app/speech_emotion.py의 attach_emotion)와 같은 형식이며,
        /// 이미 있던 표정 청크는 바꾸고 소리와 입 모양 청크는 그대로 둔다.
        /// </summary>
        private static byte[] WithEmotion(byte[] wav, CharacterEmotion emotion)
        {
            var chunks = new MemoryStream();
            byte[] pcm = null;
            int channels = 0;
            int frequency = 0;
            int offset = 12;
            while (offset + 8 <= wav.Length)
            {
                string tag = Encoding.ASCII.GetString(wav, offset, 4);
                int size = BitConverter.ToInt32(wav, offset + 4);
                int chunkLength = 8 + size + size % 2;
                if (tag == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, offset + 10);
                    frequency = BitConverter.ToInt32(wav, offset + 12);
                }
                else if (tag == "data")
                {
                    pcm = new byte[size];
                    Array.Copy(wav, offset + 8, pcm, 0, size);
                }

                if (tag != EmotionChunk)
                {
                    chunks.Write(wav, offset, chunkLength);
                }

                offset += chunkLength;
            }

            if (pcm == null || channels == 0 || frequency == 0)
            {
                throw new InvalidOperationException("튜토리얼 음성에 PCM이 없습니다.");
            }

            // 재생할 때 표정 시간표와 견주는 길이와 같은 계산으로 구한다(DialogueSpeechWav).
            float duration = (float)(pcm.Length / 2) / channels / frequency;
            string seconds = duration.ToString("R", CultureInfo.InvariantCulture);
            string payload =
                $"{{\"version\":1,\"duration\":{seconds},\"pcmSha256\":\"{Sha256(pcm)}\","
                + $"\"cues\":[{{\"start\":0,\"end\":{seconds},\"emotion\":\"{SnakeCase(emotion)}\",\"intensity\":1}}]}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            chunks.Write(Encoding.ASCII.GetBytes(EmotionChunk), 0, 4);
            chunks.Write(BitConverter.GetBytes(payloadBytes.Length), 0, 4);
            chunks.Write(payloadBytes, 0, payloadBytes.Length);
            if (payloadBytes.Length % 2 == 1)
            {
                chunks.WriteByte(0);
            }

            var result = new MemoryStream();
            result.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
            result.Write(BitConverter.GetBytes((int)chunks.Length + 4), 0, 4);
            result.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
            chunks.WriteTo(result);
            return result.ToArray();
        }

        private static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter
                .ToString(sha.ComputeHash(bytes))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        // 서버와 같은 이름(warm_smile)으로 적는다.
        private static string SnakeCase(CharacterEmotion emotion)
        {
            return Regex.Replace(emotion.ToString(), "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
        }
    }
}
