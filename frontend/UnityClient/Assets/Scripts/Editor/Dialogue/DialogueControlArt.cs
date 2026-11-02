using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    internal static class DialogueControlArt
    {
        public static void Draw(Transform parent, string name, bool centered, bool primary)
        {
            Glyph(
                parent,
                name,
                centered ? -11 : 16,
                -11,
                22,
                primary ? Color.white : new Color32(81, 66, 54, 255)
            );
            var rect = (RectTransform)parent.Find("Glyph");
            rect.name = "LineIcon";
            rect.anchorMin = rect.anchorMax = new Vector2(centered ? .5f : 0, .5f);
        }

        private static void Glyph(
            Transform parent,
            string name,
            float x,
            float y,
            float size,
            Color color
        )
        {
            RectTransform root = Group(parent, "Glyph", x, y, size, size);
            Vector2[] points;
            switch (name)
            {
                case "mic":
                    points = new[]
                    {
                        new Vector2(.36f, .46f),
                        new Vector2(.36f, .83f),
                        new Vector2(.43f, .91f),
                        new Vector2(.57f, .91f),
                        new Vector2(.64f, .83f),
                        new Vector2(.64f, .46f),
                        new Vector2(.57f, .37f),
                        new Vector2(.43f, .37f),
                        new Vector2(.36f, .46f),
                    };
                    Polyline(root, points, size, color);
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.22f, .54f),
                            new Vector2(.22f, .38f),
                            new Vector2(.36f, .23f),
                            new Vector2(.64f, .23f),
                            new Vector2(.78f, .38f),
                            new Vector2(.78f, .54f),
                        },
                        size,
                        color
                    );
                    Segment(root, new Vector2(.5f, .23f), new Vector2(.5f, .06f), size, color);
                    Segment(root, new Vector2(.33f, .06f), new Vector2(.67f, .06f), size, color);
                    break;
                case "replay":
                    for (int i = 0; i < 14; i++)
                    {
                        float a = (55 + i * 20) * Mathf.Deg2Rad;
                        float b = (55 + (i + 1) * 20) * Mathf.Deg2Rad;
                        Segment(
                            root,
                            new Vector2(.5f, .5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .35f,
                            new Vector2(.5f, .5f) + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * .35f,
                            size,
                            color
                        );
                    }
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.62f, .96f),
                            new Vector2(.72f, .78f),
                            new Vector2(.49f, .79f),
                        },
                        size,
                        color
                    );
                    break;
                case "send":
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.08f, .55f),
                            new Vector2(.91f, .91f),
                            new Vector2(.62f, .08f),
                            new Vector2(.43f, .43f),
                            new Vector2(.08f, .55f),
                        },
                        size,
                        color
                    );
                    Segment(root, new Vector2(.43f, .43f), new Vector2(.91f, .91f), size, color);
                    break;
                case "exit":
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.44f, .9f),
                            new Vector2(.12f, .9f),
                            new Vector2(.12f, .1f),
                            new Vector2(.44f, .1f),
                        },
                        size,
                        color
                    );
                    Segment(root, new Vector2(.35f, .5f), new Vector2(.93f, .5f), size, color);
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.7f, .73f),
                            new Vector2(.93f, .5f),
                            new Vector2(.7f, .27f),
                        },
                        size,
                        color
                    );
                    break;
                case "meaning":
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.12f, .85f),
                            new Vector2(.88f, .85f),
                            new Vector2(.88f, .27f),
                            new Vector2(.43f, .27f),
                            new Vector2(.2f, .08f),
                            new Vector2(.2f, .27f),
                            new Vector2(.12f, .27f),
                            new Vector2(.12f, .85f),
                        },
                        size,
                        color
                    );
                    Segment(root, new Vector2(.29f, .62f), new Vector2(.72f, .62f), size, color);
                    Segment(root, new Vector2(.29f, .46f), new Vector2(.58f, .46f), size, color);
                    break;
                case "close":
                    Segment(root, new Vector2(.25f, .25f), new Vector2(.75f, .75f), size, color);
                    Segment(root, new Vector2(.25f, .75f), new Vector2(.75f, .25f), size, color);
                    break;
                case "insert":
                    Segment(root, new Vector2(.15f, .77f), new Vector2(.76f, .77f), size, color);
                    Polyline(
                        root,
                        new[]
                        {
                            new Vector2(.76f, .77f),
                            new Vector2(.76f, .35f),
                            new Vector2(.3f, .35f),
                            new Vector2(.48f, .53f),
                        },
                        size,
                        color
                    );
                    Segment(root, new Vector2(.3f, .35f), new Vector2(.48f, .17f), size, color);
                    break;
                default:
                    foreach (float px in new[] { .2f, .5f, .8f })
                        Box(root, "Dot", px * size, .45f * size, 3, 3, null, color);
                    break;
            }
        }

        private static void Polyline(Transform root, Vector2[] points, float size, Color color)
        {
            for (int i = 0; i < points.Length - 1; i++)
                Segment(root, points[i], points[i + 1], size, color);
        }

        private static void Segment(
            Transform root,
            Vector2 from,
            Vector2 to,
            float size,
            Color color
        )
        {
            Vector2 delta = (to - from) * size;
            RectTransform line = Box(
                root,
                "Stroke",
                from.x * size,
                from.y * size,
                delta.magnitude,
                1.8f,
                null,
                color
            );
            line.pivot = new Vector2(0, .5f);
            line.localRotation = Quaternion.Euler(
                0,
                0,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg
            );
        }

        private static RectTransform Group(
            Transform parent,
            string name,
            float x,
            float y,
            float w,
            float h
        )
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static RectTransform Box(
            Transform parent,
            string name,
            float x,
            float y,
            float w,
            float h,
            Sprite sprite,
            Color color
        )
        {
            RectTransform rect = Group(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type =
                sprite != null && sprite.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
