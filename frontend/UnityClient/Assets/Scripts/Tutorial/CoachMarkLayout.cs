using System.Collections.Generic;
using UnityEngine;

namespace A305.Tutorial
{
    public static class CoachMarkLayout
    {
        public static Rect Hole(Rect target, Vector2 canvasSize, float padding) => default;

        public static Rect[] Shades(Rect hole, Vector2 canvasSize) => new Rect[0];

        public static Rect Union(IReadOnlyList<Rect> targets) => default;

        public static bool IsCaptionBelow(Rect hole, float captionHeight, float gap) => false;
    }
}
