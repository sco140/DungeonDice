using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDice.UI
{
    /// <summary>
    /// Builds placeholder uGUI at runtime: panels, labels and buttons with TextMesh Pro text.
    /// The prototype's screens are code-built so layout lives in C# next to the logic it serves;
    /// swap these helpers for prefabs when real art arrives.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelDark = new(0.10f, 0.10f, 0.12f, 0.95f);
        public static readonly Color PanelMid = new(0.18f, 0.18f, 0.22f, 1f);
        public static readonly Color PanelLocked = new(0.14f, 0.14f, 0.14f, 1f);
        public static readonly Color TextLight = new(0.92f, 0.92f, 0.9f, 1f);
        public static readonly Color TextDim = new(0.72f, 0.72f, 0.7f, 1f);
        public static readonly Color Accent = new(0.85f, 0.65f, 0.25f, 1f);

        /// <summary>A rect that fills its parent.</summary>
        public static RectTransform Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            return stretch ? Stretch(rt) : rt;
        }

        /// <summary>An empty rect for layout groups; no image.</summary>
        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// A column that sizes its children: TMP labels take their text height, buttons and rows take
        /// their LayoutElement preferred/flexible height. With <paramref name="controlHeight"/> off the
        /// group leaves every child at its default 100 px rect and the content piles up, so keep it on.
        /// </summary>
        public static VerticalLayoutGroup Vertical(RectTransform rt, float spacing = 8f, int padding = 12, TextAnchor align = TextAnchor.UpperCenter, bool controlHeight = true)
        {
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = controlHeight;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup Horizontal(RectTransform rt, float spacing = 12f, int padding = 12, TextAnchor align = TextAnchor.MiddleCenter, bool expandChildren = true)
        {
            var group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expandChildren;
            group.childForceExpandHeight = true;
            return group;
        }

        public static TMP_Text Label(Transform parent, string name, string text, float size = 24f,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Color? color = null, float preferredHeight = -1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color ?? TextLight;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (preferredHeight > 0f)
            {
                var layout = go.AddComponent<LayoutElement>();
                layout.preferredHeight = preferredHeight;
            }
            return label;
        }

        public static Button Button(Transform parent, string name, string text, Action onClick, Color? background = null,
            float fontSize = 24f, float preferredHeight = 56f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = background ?? PanelMid;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;

            var label = Label(go.transform, "Text", text, fontSize);
            Stretch(label.rectTransform, 6f);
            // Buttons are rows' children: never let them soak up leftover height.
            layout.flexibleHeight = 0f;
            return button;
        }

        /// <summary>
        /// Pin a row to one height. flexibleHeight must be set to 0 explicitly: a layout group that
        /// force-expands its children reports itself as flexible, and an unset (-1) LayoutElement value
        /// does not override that, so the row would grab leftover space and balloon.
        /// </summary>
        public static LayoutElement FixedHeight(RectTransform rt, float height)
        {
            var layout = rt.gameObject.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;
            return layout;
        }

        public static LayoutElement Flexible(RectTransform rt, float weight = 1f)
        {
            var layout = rt.gameObject.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            layout.flexibleHeight = weight;
            layout.flexibleWidth = weight;
            return layout;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
