using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StartupClicker.Game
{
    /// <summary>
    /// Helpers for building the interface from code, plus the UI colour theme.
    /// Every element is a RectTransform: anchors say which part of the parent it sticks to,
    /// and the pivot is the point of the element that position refers to.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Navy = new Color(15 / 255f, 25 / 255f, 35 / 255f);
        public static readonly Color Navy2 = new Color(27 / 255f, 40 / 255f, 54 / 255f);
        public static readonly Color Slate = new Color(58 / 255f, 76 / 255f, 96 / 255f);
        public static readonly Color Dim = new Color(140 / 255f, 156 / 255f, 172 / 255f);
        public static readonly Color Cream = WorldView.Cream;
        public static readonly Color Red = WorldView.Red;
        public static readonly Color Teal = WorldView.Teal;
        public static readonly Color Gold = WorldView.Gold;

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fixed-size element pinned to one point of its parent (anchor) at an offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fill the parent, inset by the given margins.</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Img(string name, Transform parent, string sprite, Color color, float sliceBorder = 0f)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                if (sliceBorder > 0f)
                {
                    img.sprite = SpriteBank.Sliced(sprite, sliceBorder);
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else
                {
                    img.sprite = sprite.StartsWith("b_") ? SpriteBank.Grounded(sprite) : SpriteBank.Centered(sprite);
                    img.preserveAspect = true;
                }
            }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string content, int size, Color color,
            TextAnchor align = TextAnchor.UpperLeft, bool bold = true, bool wrap = false)
        {
            var t = Node(name, parent).gameObject.AddComponent<Text>();
            t.font = SpriteBank.Font(bold);
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = content;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>A clickable cut-corner button. Returns the Button; its Image is the tintable background.</summary>
        public static Button Btn(string name, Transform parent, string sprite, float border, Color color, Action onClick)
        {
            var img = Img(name, parent, sprite, color, border);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // ButtonJuice handles the feedback instead
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<ButtonJuice>();
            return btn;
        }
    }

    /// <summary>Makes a button grow slightly on hover and dip when pressed.</summary>
    public sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float HoverScale = 1.03f;
        bool hover, down;
        float current = 1f;

        public void OnPointerEnter(PointerEventData e) { hover = true; }
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void OnDisable() { hover = down = false; current = 1f; transform.localScale = Vector3.one; }

        void Update()
        {
            float target = down ? 0.96f : (hover ? HoverScale : 1f);
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(current, current, 1f);
        }
    }
}
