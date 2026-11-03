using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Outbreak settings card. Bloodlines frames, slider, and toggle, wired to the options the game uses.
/// </summary>
public sealed class MainMenuSettingsPanel : MonoBehaviour
{
    const string FramePath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_main_menu_red.png";
    const string WellPath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_red.png";
    const string GearPath = "Assets/Alebardium/Bloodlines UI/Textures/Icon/Icons_settins_frame.png";
    const string ClosePath = "Assets/Alebardium/Bloodlines UI/Textures/Input Field/Cross.png";
    const string TrackPath = "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_empty.png";
    const string FillPath = "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_full_v1.png";
    const string HandlePath = "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_toggler.png";
    const string TogglePath = "Assets/Alebardium/Bloodlines UI/Textures/Toggle/Icons-13.png";
    const string MarkPath = "Assets/Alebardium/Bloodlines UI/Textures/Toggle/Icon Checkmark 1 (Round).png";
    const string ButtonPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Red_Default.png";
    const string ButtonHoverPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Red_Hover.png";
    const string ButtonPressedPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Pressed.png";
    const string FontPath = "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold SDF.asset";

    static readonly Color Paper = new Color(0.94f, 0.91f, 0.88f, 1f);
    static readonly Color Accent = new Color(0.86f, 0.12f, 0.09f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.56f, 0.52f, 1f);

    TMP_FontAsset font;
    GameObject[] covered;

    public static GameObject Build(Transform parent, GameObject[] hideWhileOpen)
    {
        var root = new GameObject("Settings Overlay");
        root.transform.SetParent(parent, false);
        var rect = root.AddComponent<RectTransform>();
        Stretch(rect);
        var overlay = root.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 80;
        root.AddComponent<GraphicRaycaster>();
        var panel = root.AddComponent<MainMenuSettingsPanel>();
        panel.covered = hideWhileOpen;
        panel.Construct();
        root.SetActive(false);
        return root;
    }

    void OnEnable()
    {
        SetCovered(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void OnDisable()
    {
        SetCovered(true);
        PlayerPrefs.Save();
    }

    void SetCovered(bool visible)
    {
        if (covered == null)
            return;

        for (int i = 0; i < covered.Length; i++)
        {
            if (covered[i] != null)
                covered[i].SetActive(visible);
        }
    }

    void Construct()
    {
        font = LoadFont();

        var dimmer = CreateRect("Dimmer", transform);
        Stretch(dimmer);
        var dimmerImage = dimmer.gameObject.AddComponent<Image>();
        dimmerImage.sprite = White();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.82f);
        dimmerImage.raycastTarget = true;

        var panel = CreateRect("Panel", transform);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(1020f, 780f), Vector2.zero);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = LoadSprite(FramePath);
        panelImage.type = Image.Type.Sliced;
        panelImage.pixelsPerUnitMultiplier = 0.35f;
        panelImage.raycastTarget = true;

        var backing = CreateRect("Backing", panel);
        Stretch(backing);
        backing.offsetMin = new Vector2(22f, 22f);
        backing.offsetMax = new Vector2(-22f, -22f);
        var backingImage = backing.gameObject.AddComponent<Image>();
        backingImage.sprite = White();
        backingImage.color = new Color(0.012f, 0.01f, 0.01f, 0.96f);
        backingImage.raycastTarget = false;

        var gear = CreateImage("Gear", panel, LoadSprite(GearPath), new Vector2(72f, 72f));
        Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(72f, 72f), new Vector2(-250f, 292f));
        gear.preserveAspect = true;

        CreateLabel(panel, "SYSTEM SETTINGS", 34f, Paper, FontStyles.Bold, 6f,
            new Vector2(40f, 292f), new Vector2(460f, 52f));

        var close = CreateButton(panel, LoadSprite(ClosePath), null, null, Close);
        Place(close, new Vector2(0.5f, 0.5f), new Vector2(52f, 52f), new Vector2(452f, 328f));
        close.GetComponent<Image>().preserveAspect = true;

        var well = CreateRect("Well", panel);
        Place(well, new Vector2(0.5f, 0.5f), new Vector2(900f, 470f), new Vector2(0f, 8f));
        var wellImage = well.gameObject.AddComponent<Image>();
        wellImage.sprite = LoadSprite(WellPath);
        wellImage.type = Image.Type.Sliced;
        wellImage.pixelsPerUnitMultiplier = 0.42f;
        wellImage.raycastTarget = false;

        var plate = CreateRect("Plate", well);
        Stretch(plate);
        plate.offsetMin = new Vector2(16f, 16f);
        plate.offsetMax = new Vector2(-16f, -16f);
        var plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = White();
        plateImage.color = new Color(0.015f, 0.012f, 0.012f, 0.96f);
        plateImage.raycastTarget = false;

        CreateLabel(well, "AUDIO", 16f, Accent, FontStyles.Bold, 5f,
            new Vector2(-300f, 180f), new Vector2(240f, 28f), TextAlignmentOptions.Left);
        BuildSlider(well, "MASTER VOLUME", new Vector2(0f, 108f), 0f, 1f, GameSettings.MasterVolume, true, value =>
        {
            PlayerPrefs.SetFloat(GameSettings.VolumeKey, value);
            AudioListener.volume = value;
        });

        CreateLabel(well, "MOUSE LOOK", 16f, Accent, FontStyles.Bold, 5f,
            new Vector2(-300f, 28f), new Vector2(240f, 28f), TextAlignmentOptions.Left);
        BuildSlider(well, "LOOK SENSITIVITY", new Vector2(0f, -44f), 0.5f, 1.5f, GameSettings.LookScale, false, value =>
        {
            PlayerPrefs.SetFloat(GameSettings.LookKey, value);
        });
        BuildInvert(well, new Vector2(0f, -148f));

        CreateLabel(panel, "The headset follows your head. These change the desktop view.",
            15f, Muted, FontStyles.Normal, 0.5f, new Vector2(0f, -228f), new Vector2(760f, 28f));

        var logout = CreateButton(
            panel,
            LoadSprite(ButtonPath),
            LoadSprite(ButtonHoverPath),
            LoadSprite(ButtonPressedPath),
            () =>
            {
                Screen.orientation = ScreenOrientation.Portrait;
                Screen.autorotateToPortrait = true;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = false;
                Screen.autorotateToLandscapeRight = false;

                VisageIDAuth.Models.VisageAuthSession.ClearSession();
                UnityEngine.SceneManagement.SceneManager.LoadScene("VisageID_Auth_Scene");
            });
        Place(logout, new Vector2(0.5f, 0.5f), new Vector2(280f, 58f), new Vector2(-155f, -286f));
        CreateLabel(logout, "DÉCONNEXION", 20f, Paper, FontStyles.Bold, 3f, Vector2.zero, new Vector2(280f, 58f));

        var back = CreateButton(
            panel,
            LoadSprite(ButtonPath),
            LoadSprite(ButtonHoverPath),
            LoadSprite(ButtonPressedPath),
            Close);
        Place(back, new Vector2(0.5f, 0.5f), new Vector2(280f, 58f), new Vector2(155f, -286f));
        CreateLabel(back, "RETURN", 20f, Paper, FontStyles.Bold, 3f, Vector2.zero, new Vector2(280f, 58f));
    }

    void BuildSlider(
        Transform parent,
        string caption,
        Vector2 position,
        float min,
        float max,
        float value,
        bool percent,
        UnityEngine.Events.UnityAction<float> changed)
    {
        var row = CreateRect(caption, parent);
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(820f, 72f), position);

        CreateLabel(row, caption, 20f, Paper, FontStyles.Bold, 1.2f,
            new Vector2(-250f, 0f), new Vector2(280f, 40f), TextAlignmentOptions.Left);

        var sliderRoot = CreateRect("Slider", row);
        Place(sliderRoot, new Vector2(0.5f, 0.5f), new Vector2(360f, 36f), new Vector2(70f, 0f));

        var track = CreateImage("Track", sliderRoot, LoadSprite(TrackPath), new Vector2(360f, 28f));
        Stretch(track.rectTransform);
        track.raycastTarget = false;

        var fillArea = CreateRect("Fill Area", sliderRoot);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(8f, 7f);
        fillArea.offsetMax = new Vector2(-8f, -7f);

        var fill = CreateRect("Fill", fillArea);
        Stretch(fill);
        fill.gameObject.AddComponent<RectMask2D>();

        var teeth = CreateImage("Teeth", fill, LoadSprite(FillPath), new Vector2(344f, 22f));
        teeth.rectTransform.anchorMin = new Vector2(0f, 0f);
        teeth.rectTransform.anchorMax = new Vector2(0f, 1f);
        teeth.rectTransform.pivot = new Vector2(0f, 0.5f);
        teeth.rectTransform.anchoredPosition = Vector2.zero;
        teeth.rectTransform.sizeDelta = new Vector2(344f, 0f);
        teeth.raycastTarget = false;

        var handleArea = CreateRect("Handle Slide Area", sliderRoot);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(12f, -16f);
        handleArea.offsetMax = new Vector2(-12f, 16f);

        var handle = CreateImage("Handle", handleArea, LoadSprite(HandlePath), new Vector2(28f, 64f));
        handle.preserveAspect = true;
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(28f, 64f);

        var readout = CreateLabel(row, string.Empty, 18f, Accent, FontStyles.Bold, 1f,
            new Vector2(350f, 0f), new Vector2(90f, 36f));

        var slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.ColorTint;
        var colors = slider.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.78f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        slider.colors = colors;

        void Paint(float current)
        {
            readout.text = percent
                ? Mathf.RoundToInt(current * 100f) + "%"
                : current.ToString("0.0") + "x";
        }

        slider.onValueChanged.AddListener(current =>
        {
            Paint(current);
            changed.Invoke(current);
        });
        slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
        Paint(slider.value);
    }

    void BuildInvert(Transform parent, Vector2 position)
    {
        var row = CreateRect("Invert Look", parent);
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(820f, 72f), position);

        CreateLabel(row, "INVERT LOOK", 20f, Paper, FontStyles.Bold, 1.2f,
            new Vector2(-250f, 0f), new Vector2(280f, 40f), TextAlignmentOptions.Left);

        var state = CreateLabel(row, GameSettings.InvertLook ? "ON" : "OFF", 18f, Accent, FontStyles.Bold, 1.5f,
            new Vector2(250f, 0f), new Vector2(80f, 36f));

        var toggleRoot = CreateRect("Toggle", row);
        Place(toggleRoot, new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(150f, 0f));
        var background = toggleRoot.gameObject.AddComponent<Image>();
        background.sprite = LoadSprite(TogglePath);
        background.preserveAspect = true;

        var mark = CreateImage("Checkmark", toggleRoot, LoadSprite(MarkPath), new Vector2(36f, 36f));
        Stretch(mark.rectTransform);
        mark.rectTransform.offsetMin = new Vector2(14f, 14f);
        mark.rectTransform.offsetMax = new Vector2(-14f, -14f);
        mark.preserveAspect = true;
        mark.raycastTarget = false;

        var toggle = toggleRoot.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = mark;
        toggle.isOn = GameSettings.InvertLook;
        background.color = toggle.isOn ? Color.white : new Color(0.42f, 0.42f, 0.42f, 1f);
        toggle.onValueChanged.AddListener(on =>
        {
            background.color = on ? Color.white : new Color(0.42f, 0.42f, 0.42f, 1f);
            state.text = on ? "ON" : "OFF";
            PlayerPrefs.SetInt(GameSettings.InvertKey, on ? 1 : 0);
        });
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    RectTransform CreateButton(Transform parent, Sprite normal, Sprite hover, Sprite pressed, UnityEngine.Events.UnityAction action)
    {
        var rect = CreateRect("Button", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = normal;
        image.type = Image.Type.Simple;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (hover != null || pressed != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = hover;
            state.pressedSprite = pressed;
            state.selectedSprite = hover;
            button.spriteState = state;
        }

        button.onClick.AddListener(action);
        return rect;
    }

    TextMeshProUGUI CreateLabel(
        Transform parent,
        string text,
        float size,
        Color color,
        FontStyles style,
        float spacing,
        Vector2 position,
        Vector2 box,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = CreateRect(string.IsNullOrEmpty(text) ? "Value" : text, parent);
        Place(rect, new Vector2(0.5f, 0.5f), box, position);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.characterSpacing = spacing;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        var rect = CreateRect(name, parent);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = true;
        return image;
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

    static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static Sprite LoadSprite(string path)
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null)
        {
            Sprite regSprite = registry.GetSpriteByAssetPath(path);
            if (regSprite != null)
                return regSprite;
        }
#if UNITY_EDITOR
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }
#else
        _ = path;
#endif
        return null;
    }

    static TMP_FontAsset LoadFont()
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null && registry.robotoBoldFont != null)
            return registry.robotoBoldFont;
#if UNITY_EDITOR
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null)
            return font;
#endif
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
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
