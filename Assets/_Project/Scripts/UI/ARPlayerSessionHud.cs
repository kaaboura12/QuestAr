using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VisageIDAuth.Models;

/// <summary>
/// Displays the authenticated player's identity alongside the AR health bar
/// and provides a Bloodlines UI styled Logout button to return to VisageID_Auth_Scene.
/// </summary>
[DefaultExecutionOrder(210)]
public class ARPlayerSessionHud : MonoBehaviour
{
    private const string AuthSceneName = "VisageID_Auth_Scene";
    private const string MedievalFontPath =
        "Assets/Alebardium/Bloodlines UI/Fonts/MedievalSharp SDF.asset";

    [Header("Configuration")]
    [SerializeField] private string loginSceneName = AuthSceneName;
    [SerializeField] private TMP_FontAsset medievalFont;
    [SerializeField] private Sprite buttonNormalSprite;
    [SerializeField] private Sprite buttonHoverSprite;
    [SerializeField] private Sprite buttonPressedSprite;

    private GameObject hudRoot;
    private RectTransform plateRect;
    private RectTransform logoutButtonRect;
    private TextMeshProUGUI playerLabel;
    private Button logoutButton;

    private static Sprite whiteSprite;

    private void Awake()
    {
        ResolveAssets();
    }

    private void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        BuildHud();
        UpdatePlayerDisplay();
    }

    private void OnEnable()
    {
        VisageAuthSession.OnUserLoggedIn += HandleUserLoggedIn;
        VisageAuthSession.OnUserLoggedOut += HandleUserLoggedOut;

        if (logoutButtonRect != null)
            ARMobileCombatControls.RegisterHitRect(logoutButtonRect);
    }

    private void OnDisable()
    {
        VisageAuthSession.OnUserLoggedIn -= HandleUserLoggedIn;
        VisageAuthSession.OnUserLoggedOut -= HandleUserLoggedOut;

        if (logoutButtonRect != null)
            ARMobileCombatControls.UnregisterHitRect(logoutButtonRect);
    }

    private void OnDestroy()
    {
        if (logoutButtonRect != null)
            ARMobileCombatControls.UnregisterHitRect(logoutButtonRect);

        if (hudRoot != null)
            Destroy(hudRoot);
    }

    private void ResolveAssets()
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null)
        {
            if (medievalFont == null && registry.medievalSharpFont != null)
                medievalFont = registry.medievalSharpFont;

            if (buttonNormalSprite == null && registry.settingsButton != null)
                buttonNormalSprite = registry.settingsButton;

            if (buttonHoverSprite == null && registry.settingsButtonHover != null)
                buttonHoverSprite = registry.settingsButtonHover;

            if (buttonPressedSprite == null && registry.settingsButtonPressed != null)
                buttonPressedSprite = registry.settingsButtonPressed;
        }

#if UNITY_EDITOR
        if (medievalFont == null)
        {
            medievalFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MedievalFontPath);
        }
#endif
    }

    private void BuildHud()
    {
        if (hudRoot != null)
            return;

        hudRoot = new GameObject("AR Player Session HUD");
        var canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220; // Above health bar (180), below touch buttons (260)

        var scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        hudRoot.AddComponent<GraphicRaycaster>();

        // Container panel placed right beside the health plate (Health is at X=32, width=508 -> right edge=540)
        plateRect = CreateRect("Player_Plate", hudRoot.transform);
        plateRect.anchorMin = new Vector2(0f, 1f);
        plateRect.anchorMax = new Vector2(0f, 1f);
        plateRect.pivot = new Vector2(0f, 1f);
        plateRect.anchoredPosition = new Vector2(552f, -32f);
        plateRect.sizeDelta = new Vector2(520f, 70f);

        // Dark gothic background (matches Bloodlines UI theme)
        var background = plateRect.gameObject.AddComponent<Image>();
        background.sprite = GetWhiteSprite();
        background.color = new Color(0.04f, 0.03f, 0.03f, 0.92f);
        background.raycastTarget = false;

        // Crimson red vertical accent stripe on left edge
        var accent = CreateRect("Accent_Stripe", plateRect);
        StretchVertical(accent, 0f, 5f);
        var accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.sprite = GetWhiteSprite();
        accentImage.color = new Color(0.78f, 0.10f, 0.08f, 1f);
        accentImage.raycastTarget = false;

        // Bottom border line
        var bottomBorder = CreateRect("Bottom_Border", plateRect);
        bottomBorder.anchorMin = new Vector2(0f, 0f);
        bottomBorder.anchorMax = new Vector2(1f, 0f);
        bottomBorder.pivot = new Vector2(0.5f, 0f);
        bottomBorder.anchoredPosition = Vector2.zero;
        bottomBorder.sizeDelta = new Vector2(0f, 2f);
        var borderImage = bottomBorder.gameObject.AddComponent<Image>();
        borderImage.sprite = GetWhiteSprite();
        borderImage.color = new Color(0.38f, 0.08f, 0.07f, 0.75f);
        borderImage.raycastTarget = false;

        // Player Name text label
        var textObj = new GameObject("Player_Name_Label");
        textObj.transform.SetParent(plateRect, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.offsetMin = new Vector2(20f, 4f);
        textRect.offsetMax = new Vector2(-168f, -4f); // Leaves space for Logout button

        playerLabel = textObj.AddComponent<TextMeshProUGUI>();
        playerLabel.font = medievalFont;
        playerLabel.fontSize = 22f;
        playerLabel.enableAutoSizing = true;
        playerLabel.fontSizeMin = 14f;
        playerLabel.fontSizeMax = 22f;
        playerLabel.characterSpacing = 1.5f;
        playerLabel.alignment = TextAlignmentOptions.MidlineLeft;
        playerLabel.color = new Color(0.94f, 0.91f, 0.88f, 1f);
        playerLabel.raycastTarget = false;

        // Logout Button
        var btnObj = new GameObject("Btn_Logout");
        btnObj.transform.SetParent(plateRect, false);
        logoutButtonRect = btnObj.AddComponent<RectTransform>();
        logoutButtonRect.anchorMin = new Vector2(1f, 0.5f);
        logoutButtonRect.anchorMax = new Vector2(1f, 0.5f);
        logoutButtonRect.pivot = new Vector2(1f, 0.5f);
        logoutButtonRect.anchoredPosition = new Vector2(-14f, 0f);
        logoutButtonRect.sizeDelta = new Vector2(146f, 44f);

        var btnImage = btnObj.AddComponent<Image>();
        if (buttonNormalSprite != null)
        {
            btnImage.sprite = buttonNormalSprite;
            btnImage.type = Image.Type.Sliced;
        }
        else
        {
            btnImage.sprite = GetWhiteSprite();
            btnImage.color = new Color(0.55f, 0.09f, 0.08f, 0.95f);
        }

        logoutButton = btnObj.AddComponent<Button>();
        logoutButton.targetGraphic = btnImage;

        if (buttonHoverSprite != null || buttonPressedSprite != null)
        {
            logoutButton.transition = Selectable.Transition.SpriteSwap;
            var states = logoutButton.spriteState;
            states.highlightedSprite = buttonHoverSprite != null ? buttonHoverSprite : buttonNormalSprite;
            states.pressedSprite = buttonPressedSprite != null ? buttonPressedSprite : buttonNormalSprite;
            states.selectedSprite = buttonHoverSprite != null ? buttonHoverSprite : buttonNormalSprite;
            logoutButton.spriteState = states;
        }
        else
        {
            logoutButton.transition = Selectable.Transition.ColorTint;
            var colors = logoutButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            logoutButton.colors = colors;
        }

        logoutButton.onClick.AddListener(OnLogoutClicked);

        // Register hit rect so clicking logout does not shoot in background
        ARMobileCombatControls.RegisterHitRect(logoutButtonRect);

        // Logout Button Label
        var btnTextObj = new GameObject("Btn_Label");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        var btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        var btnLabel = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnLabel.font = medievalFont;
        btnLabel.text = "DÉCONNEXION";
        btnLabel.fontSize = 15f;
        btnLabel.fontStyle = FontStyles.Bold;
        btnLabel.characterSpacing = 1.2f;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = new Color(0.96f, 0.93f, 0.90f, 1f);
        btnLabel.raycastTarget = false;
    }

    private void UpdatePlayerDisplay()
    {
        if (playerLabel == null)
            return;

        string username = "Invité";
        if (VisageAuthSession.CurrentUser != null &&
            !string.IsNullOrWhiteSpace(VisageAuthSession.CurrentUser.username))
        {
            username = VisageAuthSession.CurrentUser.username;
        }

        playerLabel.text = $"JOUEUR : <color=#FF382E>{username}</color>";
    }

    private void HandleUserLoggedIn(MongoUserDocument user, string authMethod)
    {
        UpdatePlayerDisplay();
    }

    private void HandleUserLoggedOut()
    {
        UpdatePlayerDisplay();
    }

    public void OnLogoutClicked()
    {
        Debug.Log("[ARPlayerSessionHud] Déconnexion demandée par le joueur. Nettoyage de la session...");
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;

        VisageAuthSession.ClearSession();

        string sceneToLoad = !string.IsNullOrWhiteSpace(loginSceneName) ? loginSceneName : AuthSceneName;
        SceneManager.LoadScene(sceneToLoad);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static void StretchVertical(RectTransform rect, float leftOffset, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(leftOffset, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
    }

    private static Sprite GetWhiteSprite()
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
