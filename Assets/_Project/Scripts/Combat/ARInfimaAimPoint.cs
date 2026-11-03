using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-center aim point. Bullets travel along the camera forward, so the marker stays on that pixel.
/// Hip-fire keeps an open cross. Aiming closes it to a tight dot.
/// </summary>
[DefaultExecutionOrder(210)]
public class ARInfimaAimPoint : MonoBehaviour
{
    ARInfimaPlayerCharacter playerCharacter;
    RectTransform left;
    RectTransform right;
    RectTransform up;
    RectTransform down;
    RectTransform dot;
    float gap = 14f;
    float bar = 12f;

    void Start()
    {
        playerCharacter = GetComponent<ARInfimaPlayerCharacter>();
        Build();
    }

    void LateUpdate()
    {
        if (left == null)
            return;

        bool aiming = playerCharacter != null && playerCharacter.IsAiming();
        float targetGap = aiming ? 2.5f : 14f;
        float targetBar = aiming ? 5f : 12f;
        float blend = 1f - Mathf.Exp(-14f * Time.deltaTime);
        gap = Mathf.Lerp(gap, targetGap, blend);
        bar = Mathf.Lerp(bar, targetBar, blend);

        float alpha = aiming ? 0.95f : 0.8f;
        SetBar(left, new Vector2(-(gap + bar * 0.5f), 0f), new Vector2(bar, 2f), alpha);
        SetBar(right, new Vector2(gap + bar * 0.5f, 0f), new Vector2(bar, 2f), alpha);
        SetBar(up, new Vector2(0f, gap + bar * 0.5f), new Vector2(2f, bar), alpha);
        SetBar(down, new Vector2(0f, -(gap + bar * 0.5f)), new Vector2(2f, bar), alpha);

        var dotImage = dot.GetComponent<Image>();
        if (dotImage != null)
        {
            Color color = dotImage.color;
            color.a = 1f;
            dotImage.color = color;
        }

        dot.sizeDelta = aiming ? new Vector2(3f, 3f) : new Vector2(4f, 4f);
    }

    void Build()
    {
        var canvasObject = new GameObject("AR Aim Point");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var rootObject = new GameObject("Reticle");
        rootObject.transform.SetParent(canvasObject.transform, false);
        var root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(64f, 64f);
        root.anchoredPosition = Vector2.zero;

        left = CreateBar(root, "Left");
        right = CreateBar(root, "Right");
        up = CreateBar(root, "Up");
        down = CreateBar(root, "Down");
        dot = CreateBar(root, "Dot");
        dot.sizeDelta = new Vector2(4f, 4f);
    }

    static RectTransform CreateBar(RectTransform parent, string name)
    {
        var barObject = new GameObject(name);
        barObject.transform.SetParent(parent, false);
        var rect = barObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        var image = barObject.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = new Color(1f, 1f, 1f, 0.9f);
        image.raycastTarget = false;
        var outline = barObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        return rect;
    }

    static void SetBar(RectTransform rect, Vector2 position, Vector2 size, float alpha)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = rect.GetComponent<Image>();
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    static Sprite whiteSprite;

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }
}
