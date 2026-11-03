using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.Theme
{
    /// <summary>
    /// 사용자가 연 창을 맨 위에 그린다. 창을 열면 그 창의 캔버스를 지금 떠 있는 창들보다 한 칸 위로 올려, 가장 최근에 연 창이 맨 위에 온다.
    /// 그 캔버스의 창을 모두 닫으면 원래 순서로 내린다. 순서는 고정 순서 캔버스(HUD·미션·대화·피드백·단어장, 135 이하) 위인 1000부터 센다.
    /// </summary>
    public static class UiLayer
    {
        public const int First = 1000;

        private static readonly Dictionary<Canvas, Raised> raised =
            new Dictionary<Canvas, Raised>();

        /// <param name="window">캔버스 안에서 연 창. 같은 캔버스의 창이 하나라도 열려 있으면 캔버스를 올려 둔다.</param>
        public static void Open(Canvas canvas, Object window)
        {
            ForgetDestroyed();
            if (!raised.TryGetValue(canvas, out Raised entry))
                entry = new Raised(canvas.sortingOrder);
            entry.Windows.Add(window);
            raised.Remove(canvas);
            int top = raised
                .Keys.Select(other => other.sortingOrder)
                .DefaultIfEmpty(First - 1)
                .Max();
            canvas.sortingOrder = Mathf.Max(top, First - 1) + 1;
            raised.Add(canvas, entry);
        }

        /// <summary>창이 놓인 가장 바깥 캔버스를 올린다.</summary>
        public static void Open(Component window)
        {
            Canvas root = RootOf(window);
            if (root != null)
                Open(root, window);
        }

        public static void Close(Component window) => Close(RootOf(window), window);

        public static void Close(Canvas canvas, Object window)
        {
            if (canvas == null || !raised.TryGetValue(canvas, out Raised entry))
                return;
            entry.Windows.Remove(window);
            if (entry.Windows.Count > 0)
                return;
            canvas.sortingOrder = entry.ClosedOrder;
            raised.Remove(canvas);
        }

        private static Canvas RootOf(Component window) =>
            window.GetComponentsInParent<Canvas>(true).LastOrDefault();

        // 장소를 옮겨 없어진 캔버스는 잊는다.
        private static void ForgetDestroyed()
        {
            foreach (Canvas gone in raised.Keys.Where(canvas => canvas == null).ToArray())
                raised.Remove(gone);
        }

        private sealed class Raised
        {
            public Raised(int closedOrder)
            {
                ClosedOrder = closedOrder;
            }

            public int ClosedOrder { get; }
            public HashSet<Object> Windows { get; } = new HashSet<Object>();
        }
    }
}
