using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen outbreak briefing. The transition owns timing; this only draws it.
/// </summary>
public sealed class LoadingScreenView : MonoBehaviour
{
    const string FontPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold SDF.asset";

    static readonly Color Accent = new Color(0.86f, 0.12f, 0.09f, 1f);
    static readonly Color Paper = new Color(0.94f, 0.91f, 0.88f, 1f);

    CanvasGroup rootGroup;
    CanvasGroup lineGroup;
    TextMeshProUGUI lineLabel;
    TextMeshProUGUI previousLabel;
    TextMeshProUGUI phaseLabel;
    RectTransform fill;
    float barWidth;
    string shownLine = string.Empty;
    Coroutine crossfade;
    readonly List<RectTransform> ash = new List<RectTransform>();
    readonly List<float> ashSpeed = new List<float>();
    readonly List<float> ashPhase = new List<float>();

    public CanvasGroup RootGroup => rootGroup;

    void Awake()
    {
        Build();
    }

    void Update()
    {
        float time = Time.unscaledTime;
        float step = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = 0; i < ash.Count; i++)
        {
            RectTransform mote = ash[i];
            Vector2 position = mote.anchoredPosition;
            position.y += ashSpeed[i] * step;
            position.x += Mathf.Sin(time * 0.7f + ashPhase[i]) * 8f * step;
            if (position.y > 620f)
            {
                position.y = -620f;
                position.x = Random.Range(-860f, 860f);
            }

            mote.anchoredPosition = position;
        }
    }

    public void ShowLine(string line)
    {
        if (line == shownLine)
            return;

        if (crossfade != null)
            StopCoroutine(crossfade);

        crossfade = StartCoroutine(Crossfade(line));
    }

    public void SetPhase(string label)
    {
        if (phaseLabel != null)
            phaseLabel.text = label;
    }

    public void SetProgress(float normalized)
    {
        if (fill == null)
            return;

        float width = barWidth * Mathf.Clamp01(normalized);
        fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    IEnumerator Crossfade(string line)
    {
        if (!string.IsNullOrEmpty(shownLine) && previousLabel != null)
            previousLabel.text = shownLine;

        if (string.IsNullOrEmpty(shownLine))
        {
            shownLine = line;
            lineLabel.text = line;
            lineGroup.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        const float outDuration = 0.16f;
        while (elapsed < outDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            lineGroup.alpha = 1f - Mathf.Clamp01(elapsed / outDuration);
            yield return null;
        }

        shownLine = line;
        lineLabel.text = line;
        elapsed = 0f;
        const float inDuration = 0.28f;
        while (elapsed < inDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            lineGroup.alpha = Mathf.Clamp01(elapsed / inDuration);
            yield return null;
        }

        lineGroup.alpha = 1f;
    }

    void Build()
    {
        var canvasObject = new GameObject("Loading Canvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = canvasObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = true;

        var background = CreateRect("Background", canvasObject.transform);
        Stretch(background);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = White();
        backgroundImage.color = new Color(0.015f, 0.012f, 0.012f, 1f);
        backgroundImage.raycastTarget = true;

        BuildAsh(canvasObject.transform);

        var rule = CreateRect("Rule", canvasObject.transform);
        rule.anchorMin = new Vector2(0f, 0f);
        rule.anchorMax = new Vector2(0f, 1f);
        rule.pivot = new Vector2(0f, 0.5f);
        rule.sizeDelta = new Vector2(6f, 0f);
        var ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.sprite = White();
        ruleImage.color = Accent;
        ruleImage.raycastTarget = false;

        TMP_FontAsset font = LoadFont();

        var kicker = CreateText("Kicker", canvasObject.transform, "OUTBREAK IN PROGRESS", 22f, font, Accent);
        Place(kicker.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(900f, 36f));
        kicker.characterSpacing = 7f;

        var lineRoot = CreateRect("Line", canvasObject.transform);
        Place(lineRoot, new Vector2(0.5f, 0.5f), new Vector2(1400f, 120f));
        lineGroup = lineRoot.gameObject.AddComponent<CanvasGroup>();
        lineGroup.blocksRaycasts = false;
        lineLabel = CreateText("Current", lineRoot, string.Empty, 42f, font, Paper);
        Stretch(lineLabel.rectTransform);
        lineLabel.characterSpacing = 1.5f;

        previousLabel = CreateText("Previous", canvasObject.transform, string.Empty, 22f, font, new Color(0.55f, 0.5f, 0.48f, 1f));
        Place(previousLabel.rectTransform, new Vector2(0.5f, 0.40f), new Vector2(1100f, 36f));

        barWidth = 560f;
        var track = CreateRect("Track", canvasObject.transform);
        Place(track, new Vector2(0.5f, 0.30f), new Vector2(barWidth, 6f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = White();
        trackImage.color = new Color(1f, 1f, 1f, 0.08f);
        trackImage.raycastTarget = false;

        fill = CreateRect("Fill", track);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = new Vector2(0f, 0f);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = White();
        fillImage.color = Accent;
        fillImage.raycastTarget = false;

        phaseLabel = CreateText("Phase", canvasObject.transform, "LISTENING", 18f, font, new Color(0.7f, 0.66f, 0.62f, 1f));
        Place(phaseLabel.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(700f, 30f));
        phaseLabel.characterSpacing = 4f;
    }

    void BuildAsh(Transform parent)
    {
        for (int i = 0; i < 22; i++)
        {
            var mote = CreateRect("Ash " + i, parent);
            mote.anchorMin = mote.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(2f, 5f);
            mote.sizeDelta = new Vector2(size, size);
            mote.anchoredPosition = new Vector2(Random.Range(-860f, 860f), Random.Range(-540f, 540f));
            var image = mote.gameObject.AddComponent<Image>();
            image.sprite = White();
            image.color = new Color(0.85f, 0.2f, 0.1f, Random.Range(0.15f, 0.45f));
            image.raycastTarget = false;
            ash.Add(mote);
            ashSpeed.Add(Random.Range(18f, 46f));
            ashPhase.Add(Random.Range(0f, 6.28f));
        }
    }

    static TMP_FontAsset LoadFont()
    {
#if UNITY_EDITOR
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null)
            return font;
#endif
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TMP_FontAsset font, Color color)
    {
        var rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name);
        rectObject.transform.SetParent(parent, false);
        return rectObject.AddComponent<RectTransform>();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    static Sprite white;

    static Sprite White()
    {
        if (white != null)
            return white;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return white;
    }
}
