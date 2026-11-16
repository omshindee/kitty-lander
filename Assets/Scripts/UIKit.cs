using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Sprites and sounds shared by the in-game HUD and the main menu.</summary>
[Serializable]
public class UISkin
{
    public Sprite panel;
    public Sprite button;
    public Sprite star;
    public Sprite starEmpty;
    public Sprite paw;
    public Sprite fishIcon;
    public Sprite fuelIcon;
    public AudioClip click;
}

/// <summary>Small helpers for building uGUI layouts from code.</summary>
public static class UIKit
{
    public static readonly Color Ink = new Color(0.17f, 0.11f, 0.08f, 1f);
    public static readonly Color Cream = new Color(1f, 0.97f, 0.91f, 1f);
    public static readonly Color Orange = new Color(0.96f, 0.55f, 0.25f, 1f);
    public static readonly Color Green = new Color(0.36f, 0.72f, 0.33f, 1f);
    public static readonly Color Blue = new Color(0.33f, 0.6f, 0.9f, 1f);
    public static readonly Color Grey = new Color(0.6f, 0.6f, 0.65f, 1f);

    private static Font font;
    public static AudioClip ClickClip;

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas CreateCanvas(string name, int sortOrder = 10)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        return canvas;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(Transform parent, string name)
    {
        var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Image Image(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
        img.preserveAspect = sprite != null && sprite.border == Vector4.zero;
        return img;
    }

    public static Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor align,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 rectSize, bool outline = false)
    {
        var rt = Rect(parent, name, anchor, pivot, pos, rectSize);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(3, -3);
        }
        return t;
    }

    public static Button Button(Transform parent, UISkin skin, string label, Color color, Vector2 anchor, Vector2 pos, Vector2 size,
        Action onClick, int fontSize = 40)
    {
        var rt = Rect(parent, "Button " + label, anchor, new Vector2(0.5f, 0.5f), pos, size);
        var img = Image(rt, skin != null ? skin.button : null, color);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.selectedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            PlayClick();
            onClick?.Invoke();
        });
        Text(rt, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
        return btn;
    }

    public static void PlayClick()
    {
        if (ClickClip == null) return;
        var cam = Camera.main;
        AudioSource.PlayClipAtPoint(ClickClip, cam != null ? cam.transform.position : Vector3.zero, 0.7f);
    }
}
