
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    [Header("Menu Assets")]
    public Sprite backgroundSprite;
    public Sprite logoSprite;

    [Header("Gameplay")]
    public string gameplaySceneName = "ARGameplay";

    [Header("Bloodlines UI Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Colors")]
    public Color accentColor = new Color(0.9f, 0.12f, 0.08f, 1f);
    public Color buttonColor = new Color(0.055f, 0.055f, 0.055f, 0.94f);

    [Header("Button Animation")]
    public float pulseAmount = 0.008f;
    public float pulseSpeed = 1.5f;

    private Canvas canvas;
    private GameObject settingsPanel;

    private readonly List<RectTransform> animatedButtons =
        new List<RectTransform>();

    private readonly List<Vector3> buttonScales =
        new List<Vector3>();

    private readonly List<float> buttonPhases =
        new List<float>();

    private void Start()
    {
        CreateMenu();
    }

    private void Update()
    {
        for (int i = 0; i < animatedButtons.Count; i++)
        {
            if (animatedButtons[i] == null)
                continue;

            float pulse = 1f +
                Mathf.Sin(Time.unscaledTime * pulseSpeed + buttonPhases[i])
                * pulseAmount;

            animatedButtons[i].localScale = buttonScales[i] * pulse;
        }
    }

    private void CreateMenu()
    {
        // Canvas
        canvas = GetComponentInChildren<Canvas>(true);

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("QuestAR Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvas.gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.gameObject.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Background
        Image background = CreateImage(
            "Background",
            backgroundSprite,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        if (background != null)
        {
            background.transform.SetAsFirstSibling();
            background.raycastTarget = false;

            Canvas backgroundCanvas =
                background.gameObject.AddComponent<Canvas>();

            backgroundCanvas.overrideSorting = true;
            backgroundCanvas.sortingOrder = -1;

            background.gameObject.AddComponent<BackgroundAnimator>();
        }

        // Ember particles
        GameObject emberLayer = new GameObject("Ember Layer");
        emberLayer.transform.SetParent(canvas.transform, false);

        RectTransform emberRect = emberLayer.AddComponent<RectTransform>();
        emberRect.anchorMin = Vector2.zero;
        emberRect.anchorMax = Vector2.one;
        emberRect.offsetMin = Vector2.zero;
        emberRect.offsetMax = Vector2.zero;

        emberLayer.AddComponent<UIEmberEffect>();

        // Logo
        Image logo = CreateImage(
            "QuestAR Logo",
            logoSprite,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 210f),
            new Vector2(650f, 430f)
        );

        logo.preserveAspect = true;

        settingsPanel = MainMenuSettingsPanel.Build(canvas.transform, new[]
        {
            playButton != null ? playButton.gameObject : null,
            settingsButton != null ? settingsButton.gameObject : null,
            quitButton != null ? quitButton.gameObject : null
        });
        SetupBloodlinesButtons();

        if (playButton != null)
            playButton.transform.SetAsLastSibling();

        if (settingsButton != null)
            settingsButton.transform.SetAsLastSibling();

        if (quitButton != null)
            quitButton.transform.SetAsLastSibling();

        // Fade in
        StartCoroutine(FadeIn(canvas));
    }

    private Image CreateImage(
        string objectName,
        Sprite sprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(canvas.transform, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;

        return image;
    }

    private void SetupBloodlinesButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(() =>
            {
                playButton.interactable = false;
                GameplayTransition.Begin(gameplaySceneName);
            });
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(() =>
            {
                settingsPanel.SetActive(true);
                settingsPanel.transform.SetAsLastSibling();
            });
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() =>
            {
                Application.Quit();
            });
        }
    }

    private void CreateButton(
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label + " Button");
        obj.transform.SetParent(canvas.transform, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = position;
        rect.anchorMax = position;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(420f, 82f);

        // Dark tactical panel
        Image image = obj.AddComponent<Image>();
        image.color = buttonColor;

        // Button interaction
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.55f, 0.48f, 1f);
        colors.pressedColor = new Color(0.65f, 0.25f, 0.22f, 1f);
        colors.selectedColor = new Color(1f, 0.75f, 0.7f, 1f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        button.onClick.AddListener(action);

        // Register subtle idle pulse
        animatedButtons.Add(rect);
        buttonScales.Add(rect.localScale);
        buttonPhases.Add(Random.Range(0f, 6.28f));

        // Red accent stripe
        GameObject stripe = new GameObject("Red Accent");
        stripe.transform.SetParent(obj.transform, false);

        RectTransform stripeRect = stripe.AddComponent<RectTransform>();
        stripeRect.anchorMin = Vector2.zero;
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        stripeRect.sizeDelta = new Vector2(5f, 0f);
        stripeRect.anchoredPosition = Vector2.zero;

        Image stripeImage = stripe.AddComponent<Image>();
        stripeImage.color = accentColor;
        stripeImage.raycastTarget = false;

        // Small red top line
        GameObject topLine = new GameObject("Top Accent");
        topLine.transform.SetParent(obj.transform, false);

        RectTransform topRect = topLine.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(0f, 2f);

        Image topImage = topLine.AddComponent<Image>();
        topImage.color = new Color(
            accentColor.r, accentColor.g, accentColor.b, 0.65f);
        topImage.raycastTarget = false;

        // Main label
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(obj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.25f);
        textRect.anchorMax = new Vector2(1f, 0.9f);
        textRect.offsetMin = new Vector2(15f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label == "PLAY" ? "DEPLOY" :
                    label == "QUIT" ? "EXIT GAME" : "SETTINGS";
        text.fontSize = 29f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.95f, 0.92f, 0.89f, 1f);
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 3f;
        text.raycastTarget = false;

        // Subtitle
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(obj.transform, false);

        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.35f);
        subRect.offsetMin = new Vector2(15f, 2f);
        subRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI subtitle = subObj.AddComponent<TextMeshProUGUI>();

        subtitle.text = label == "PLAY"
            ? "ENTER THE QUARANTINE ZONE"
            : label == "SETTINGS"
                ? "CONFIGURE YOUR EXPERIENCE"
                : "ABANDON THE MISSION";

        subtitle.fontSize = 10f;
        subtitle.characterSpacing = 1.5f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(0.68f, 0.63f, 0.60f, 1f);
        subtitle.raycastTarget = false;
    }

    private IEnumerator FadeIn(Canvas targetCanvas)
    {
        CanvasGroup group = targetCanvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        float duration = 1.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        group.alpha = 1f;
    }

}
