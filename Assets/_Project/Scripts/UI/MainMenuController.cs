using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using VisageIDAuth.Models;

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

    private static readonly Vector2 ResponsiveButtonSize = new Vector2(380f, 92f);
    private const float PlayBottomOffset = 325f;
    private const float SettingsBottomOffset = 215f;
    private const float QuitBottomOffset = 105f;
    private const float ButtonFontSize = 34f;

    private Canvas canvas;
    private GameObject settingsPanel;
    private GameObject playerBannerRoot;
    private TextMeshProUGUI playerBannerLabel;
    private static Sprite whiteSprite;

    private readonly List<RectTransform> animatedButtons =
        new List<RectTransform>();

    private readonly List<Vector3> buttonScales =
        new List<Vector3>();

    private readonly List<float> buttonPhases =
        new List<float>();

    private void OnEnable()
    {
        VisageAuthSession.OnUserLoggedIn += HandleUserSessionChanged;
        VisageAuthSession.OnUserLoggedOut += HandleUserSessionChanged;
    }

    private void OnDisable()
    {
        VisageAuthSession.OnUserLoggedIn -= HandleUserSessionChanged;
        VisageAuthSession.OnUserLoggedOut -= HandleUserSessionChanged;
    }

    private void HandleUserSessionChanged(MongoUserDocument user, string method) => RefreshPlayerBanner();
    private void HandleUserSessionChanged() => RefreshPlayerBanner();

    private void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

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
        // Resolve the existing scene Canvas (where PLAY, SETTINGS, QUIT live) before creating a new one
        if (playButton != null)
            canvas = playButton.GetComponentInParent<Canvas>();
        if (canvas == null && settingsButton != null)
            canvas = settingsButton.GetComponentInParent<Canvas>();
        if (canvas == null && quitButton != null)
            canvas = quitButton.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
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

        CreatePlayerSessionBanner();

        settingsPanel = MainMenuSettingsPanel.Build(canvas.transform, new[]
        {
            playButton != null ? playButton.gameObject : null,
            settingsButton != null ? settingsButton.gameObject : null,
            quitButton != null ? quitButton.gameObject : null,
            playerBannerRoot
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
        ConfigureResponsiveButton(playButton, PlayBottomOffset, 0);
        ConfigureResponsiveButton(settingsButton, SettingsBottomOffset, 1);
        ConfigureResponsiveButton(quitButton, QuitBottomOffset, 2);

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

    private void ConfigureResponsiveButton(Button button, float bottomOffset, int index)
    {
        if (button == null)
            return;

        if (canvas != null && button.transform.parent != canvas.transform)
            button.transform.SetParent(canvas.transform, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ResponsiveButtonSize;
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.fontSize = ButtonFontSize;
            label.raycastTarget = false;
        }

        if (!animatedButtons.Contains(rect))
        {
            animatedButtons.Add(rect);
            buttonScales.Add(Vector3.one);
            buttonPhases.Add(index * 0.85f);
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
        CanvasGroup group = targetCanvas.gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = targetCanvas.gameObject.AddComponent<CanvasGroup>();
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

    private void CreatePlayerSessionBanner()
    {
        if (canvas == null) return;

        GameObject bannerObj = new GameObject("MainMenu Player Session Banner");
        bannerObj.transform.SetParent(canvas.transform, false);
        playerBannerRoot = bannerObj;

        RectTransform plate = bannerObj.AddComponent<RectTransform>();
        plate.anchorMin = new Vector2(0f, 1f);
        plate.anchorMax = new Vector2(0f, 1f);
        plate.pivot = new Vector2(0f, 1f);
        plate.anchoredPosition = new Vector2(36f, -36f);
        plate.sizeDelta = new Vector2(520f, 70f);

        Image bg = bannerObj.AddComponent<Image>();
        bg.sprite = GetWhiteSprite();
        bg.color = new Color(0.04f, 0.03f, 0.03f, 0.92f);
        bg.raycastTarget = false;

        GameObject stripe = new GameObject("Accent Stripe");
        stripe.transform.SetParent(plate, false);
        RectTransform stripeRect = stripe.AddComponent<RectTransform>();
        stripeRect.anchorMin = new Vector2(0f, 0f);
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        stripeRect.anchoredPosition = Vector2.zero;
        stripeRect.sizeDelta = new Vector2(5f, 0f);
        Image stripeImg = stripe.AddComponent<Image>();
        stripeImg.sprite = GetWhiteSprite();
        stripeImg.color = new Color(0.78f, 0.10f, 0.08f, 1f);
        stripeImg.raycastTarget = false;

        GameObject border = new GameObject("Bottom Border");
        border.transform.SetParent(plate, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 0f);
        borderRect.anchorMax = new Vector2(1f, 0f);
        borderRect.pivot = new Vector2(0.5f, 0f);
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(0f, 2f);
        Image borderImg = border.AddComponent<Image>();
        borderImg.sprite = GetWhiteSprite();
        borderImg.color = new Color(0.38f, 0.08f, 0.07f, 0.75f);
        borderImg.raycastTarget = false;

        TMP_FontAsset font = ResolveMedievalFont();

        GameObject textObj = new GameObject("Player Name Label");
        textObj.transform.SetParent(plate, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.offsetMin = new Vector2(20f, 4f);
        textRect.offsetMax = new Vector2(-168f, -4f);

        playerBannerLabel = textObj.AddComponent<TextMeshProUGUI>();
        playerBannerLabel.font = font;
        playerBannerLabel.fontSize = 22f;
        playerBannerLabel.enableAutoSizing = true;
        playerBannerLabel.fontSizeMin = 14f;
        playerBannerLabel.fontSizeMax = 22f;
        playerBannerLabel.characterSpacing = 1.5f;
        playerBannerLabel.alignment = TextAlignmentOptions.MidlineLeft;
        playerBannerLabel.color = new Color(0.94f, 0.91f, 0.88f, 1f);
        playerBannerLabel.raycastTarget = false;

        RefreshPlayerBanner();

        GameObject btnObj = new GameObject("Btn_Logout_Menu");
        btnObj.transform.SetParent(plate, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0.5f);
        btnRect.anchorMax = new Vector2(1f, 0.5f);
        btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.anchoredPosition = new Vector2(-14f, 0f);
        btnRect.sizeDelta = new Vector2(146f, 44f);

        Image btnImg = btnObj.AddComponent<Image>();
        Sprite btnSprite = LoadButtonSprite();
        if (btnSprite != null)
        {
            btnImg.sprite = btnSprite;
            btnImg.type = Image.Type.Sliced;
        }
        else
        {
            btnImg.sprite = GetWhiteSprite();
            btnImg.color = new Color(0.55f, 0.09f, 0.08f, 0.95f);
        }

        Button logoutBtn = btnObj.AddComponent<Button>();
        logoutBtn.targetGraphic = btnImg;
        logoutBtn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = logoutBtn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        logoutBtn.colors = colors;

        logoutBtn.onClick.AddListener(OnLogoutClicked);

        GameObject btnTextObj = new GameObject("Btn Label");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnLabel = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnLabel.font = font;
        btnLabel.text = "DÉCONNEXION";
        btnLabel.fontSize = 15f;
        btnLabel.fontStyle = FontStyles.Bold;
        btnLabel.characterSpacing = 1.2f;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = new Color(0.96f, 0.93f, 0.90f, 1f);
        btnLabel.raycastTarget = false;
    }

    private void RefreshPlayerBanner()
    {
        if (playerBannerLabel == null) return;
        string username = "Invité";
        if (VisageAuthSession.CurrentUser != null &&
            !string.IsNullOrWhiteSpace(VisageAuthSession.CurrentUser.username))
        {
            username = VisageAuthSession.CurrentUser.username;
        }
        playerBannerLabel.text = $"👤 JOUEUR : <color=#FF382E>{username}</color>";
    }

    public void OnLogoutClicked()
    {
        Debug.Log("[MainMenuController] Déconnexion demandée par le joueur. Nettoyage de la session...");
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;

        VisageAuthSession.ClearSession();
        SceneManager.LoadScene("VisageID_Auth_Scene");
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }

    private static TMP_FontAsset ResolveMedievalFont()
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null && registry.medievalSharpFont != null)
            return registry.medievalSharpFont;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Alebardium/Bloodlines UI/Fonts/MedievalSharp SDF.asset");
#else
        return null;
#endif
    }

    private static Sprite LoadButtonSprite()
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null && registry.settingsButton != null)
            return registry.settingsButton;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Red_Default.png");
#else
        return null;
#endif
    }
}
