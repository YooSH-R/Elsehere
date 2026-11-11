using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>
    /// ESC로 닫을 수 있는 창들의 목록. 창이 열릴 때 올리고 닫힐 때 내린다. ESC를 한 번 누르면 가장 나중에 연 창 하나만 닫는다.
    /// 창마다 ESC를 따로 받으면 한 번에 여러 창이 닫히거나, 대화 중 단어 카드 대신 대화가 끝나므로 ESC는 여기서만 받는다.
    /// </summary>
    public static class UiEscape
    {
        private sealed class Entry
        {
            public object Owner;
            public Action Close;
            public Func<bool> IsShowing;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        /// <param name="owner">창. 같은 창을 다시 올리면 맨 위로 옮긴다.</param>
        /// <param name="close">ESC를 눌렀을 때 할 일. 창을 닫거나 한 단계 뒤로 간다.</param>
        /// <param name="isShowing">창이 다른 길로 사라졌는지 확인한다. 보이지 않는 창은 ESC로 닫지 않고 건너뛴다.</param>
        public static void Opened(object owner, Action close, Func<bool> isShowing = null)
        {
            Remove(owner);
            entries.Add(
                new Entry
                {
                    Owner = owner,
                    Close = close,
                    IsShowing = isShowing,
                }
            );
        }

        public static void Closed(object owner) => Remove(owner);

        public static bool IsListed(object owner) => entries.Exists(entry => entry.Owner == owner);

        /// <summary>가장 나중에 연 창 하나를 닫는다. 닫은 창이 있으면 true.</summary>
        public static bool CloseTop()
        {
            while (entries.Count > 0)
            {
                Entry top = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (IsGone(top))
                {
                    continue;
                }

                top.Close();
                return true;
            }

            return false;
        }

        public static void Clear() => entries.Clear();

        private static bool IsGone(Entry entry)
        {
            // 장면이 바뀌어 사라진 창은 닫기를 부르면 오류가 나므로 건너뛴다.
            if (entry.Owner is UnityEngine.Object unityObject && unityObject == null)
            {
                return true;
            }

            try
            {
                return entry.IsShowing != null && !entry.IsShowing();
            }
            catch (MissingReferenceException)
            {
                // 창이 보이는지 확인하다 지워진 창의 오브젝트를 건드렸다.
                return true;
            }
        }

        private static void Remove(object owner) =>
            entries.RemoveAll(entry => entry.Owner == owner);

        // ESC 키를 받는 곳. 장면이 바뀌어도 남도록 처음 한 번 만든다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInput()
        {
            var root = new GameObject(nameof(UiEscapeInput));
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<UiEscapeInput>();
        }

        // 창들이 Update·LateUpdate에서 목록에 오른 뒤에 ESC를 받도록 그 프레임의 맨 끝에 돈다.
        // 창이 뜬 바로 그 프레임에 누른 ESC도 그 창을 닫는다.
        [DefaultExecutionOrder(10000)]
        private sealed class UiEscapeInput : MonoBehaviour
        {
            private void LateUpdate()
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseTop();
                }
            }
        }
    }
}
