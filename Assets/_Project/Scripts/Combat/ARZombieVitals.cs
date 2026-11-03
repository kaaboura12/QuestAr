using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Zombie-game health plate for the AR gameplay scene.
/// H removes 25% of max health. The bar drains, the hurt voice plays, and a red
/// vignette with blood obscures the view for 1.5 seconds.
/// </summary>
[DefaultExecutionOrder(190)]
public class ARZombieVitals : MonoBehaviour
{
    const float MaxHealth = 100f;
    const float DamageStep = 25f;
    const float HurtDuration = 1.5f;
    static readonly Vector2 PlateRest = new Vector2(32f, -32f);

    const string FillSpritePath =
        "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_full_v1.png";
    const string TrackSpritePath =
        "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_empty_v1.png";
    const string TitleFontPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold SDF.asset";

    [SerializeField] Sprite healthFill;
    [SerializeField] Sprite healthTrack;
    [SerializeField] TMP_FontAsset titleFont;

    public float Current => current;
    public float Normalized => current / MaxHealth;
    public bool IsDead { get; private set; }

    float current = MaxHealth;
    float displayed = 1f;
    float trail = 1f;
    float hurtTime = -1f;
    float shake;
    float flash;
    bool visualsReady;
    bool postProcessingReady;

    RectTransform plate;
    RectTransform fillMask;
    RectTransform trailMask;
    Image fillImage;
    TextMeshProUGUI titleLabel;
    TextMeshProUGUI valueLabel;
    float barWidth;

    GameObject hudRoot;
    GameObject bloodRoot;
    CanvasGroup bloodGroup;
    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    readonly List<RectTransform> bloodDrops = new List<RectTransform>();

    void Awake()
    {
        LoadArt();
    }

    void Start()
    {
        BuildHud();
        BuildHurtVolume();
        BuildBlood();
        EnablePostProcessing();
        visualsReady = true;
        ApplyBar(1f, 1f);
        RefreshReadout(1f);
    }

    void Update()
    {
        if (!postProcessingReady)
            postProcessingReady = EnablePostProcessing();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
            ApplyDamage(DamageStep);

        if (!visualsReady)
            return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        AnimateBar(dt);
        AnimateHurt(dt);
        AnimatePlate(dt);
    }

    void OnDestroy()
    {
        if (hudRoot != null)
            Destroy(hudRoot);
        if (bloodRoot != null)
            Destroy(bloodRoot);
        if (volume != null)
            Destroy(volume.gameObject);
        if (profile != null)
            Destroy(profile);
    }

    public bool ApplyDamage(float amount)
    {
        if (amount <= 0f || current <= 0f)
            return false;

        current = Mathf.Max(0f, current - amount);
        shake = 11f;
        flash = 1f;
        hurtTime = HurtDuration;
        PlayHurtVoice();
        if (visualsReady)
        {
            ScatterBlood();
            ApplyHurtPresence(1f);
        }

        if (current <= 0f)
            Die();

        return true;
    }

    void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        ARGameOver gameOver = GetComponent<ARGameOver>();
        if (gameOver == null)
            gameOver = FindAnyObjectByType<ARGameOver>();

        gameOver?.Begin();
    }

    void PlayHurtVoice()
    {
        ARGameplayAudio audio = GetComponent<ARGameplayAudio>();
        if (audio == null)
            audio = FindAnyObjectByType<ARGameplayAudio>();

        audio?.PlayHurt();
    }

    void AnimateBar(float dt)
    {
        float target = Normalized;
        displayed = Mathf.Lerp(displayed, target, 1f - Mathf.Exp(-14f * dt));
        if (Mathf.Abs(displayed - target) < 0.002f)
            displayed = target;

        if (trail > displayed)
            trail = Mathf.Lerp(trail, displayed, 1f - Mathf.Exp(-3.4f * dt));
        else
            trail = displayed;

        ApplyBar(displayed, trail);
        RefreshReadout(displayed);
    }

    void AnimateHurt(float dt)
    {
        if (hurtTime < 0f)
            return;

        hurtTime -= dt;
        float elapsed = HurtDuration - Mathf.Max(hurtTime, 0f);
        ApplyHurtPresence(HurtPresence(elapsed / HurtDuration));

        if (hurtTime <= 0f)
        {
            hurtTime = -1f;
            ApplyHurtPresence(0f);
        }
    }

    void ApplyHurtPresence(float presence)
    {
        presence = Mathf.Clamp01(presence);
        if (vignette != null)
        {
            vignette.intensity.value = presence * 0.68f;
            vignette.smoothness.value = Mathf.Lerp(0.42f, 0.78f, presence);
        }

        if (volume != null)
            volume.weight = presence > 0.001f ? 1f : 0f;

        if (bloodGroup != null)
            bloodGroup.alpha = presence;
    }

    static float HurtPresence(float u)
    {
        if (u < 0.7f)
            return 1f;
        return 1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f);
    }

    void AnimatePlate(float dt)
    {
        if (plate == null)
            return;

        shake = Mathf.Lerp(shake, 0f, 1f - Mathf.Exp(-9f * dt));
        flash = Mathf.Lerp(flash, 0f, 1f - Mathf.Exp(-8f * dt));
        plate.anchoredPosition = PlateRest + Random.insideUnitCircle * shake;

        float pulse = Normalized <= 0.25f ? 0.5f + Mathf.Sin(Time.unscaledTime * 7f) * 0.5f : 0f;
        if (fillImage != null)
        {
            Color hot = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), 0.35f);
            fillImage.color = Color.Lerp(Color.white, hot, Mathf.Max(flash, pulse * 0.55f));
        }
    }

    void ApplyBar(float fill, float damageTrail)
    {
        if (fillMask == null)
            return;

        fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barWidth * Mathf.Clamp01(fill));
        trailMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barWidth * Mathf.Clamp01(damageTrail));
    }

    void RefreshReadout(float shown)
    {
        if (valueLabel == null)
            return;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(shown) * 100f);
        valueLabel.text = percent + "%";

        bool critical = Normalized <= 0.25f;
        Color blood = critical ? new Color(1f, 0.28f, 0.22f) : new Color(0.93f, 0.9f, 0.86f);
        valueLabel.color = Color.Lerp(blood, Color.white, flash);
        if (titleLabel != null)
            titleLabel.color = critical ? new Color(0.86f, 0.16f, 0.12f) : new Color(0.72f, 0.12f, 0.1f);
    }

    void BuildHud()
    {
        hudRoot = new GameObject("AR Zombie Health");
        var canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 180;
        var scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        plate = CreateRect("Plate", hudRoot.transform);
        plate.anchorMin = plate.anchorMax = new Vector2(0f, 1f);
        plate.pivot = new Vector2(0f, 1f);
        plate.anchoredPosition = PlateRest;
        plate.sizeDelta = new Vector2(508f, 142f);

        var backing = plate.gameObject.AddComponent<Image>();
        backing.sprite = WhiteSprite();
        backing.color = new Color(0.04f, 0.03f, 0.03f, 0.9f);
        backing.raycastTarget = false;

        var accent = CreateRect("Accent", plate);
        Stretch(accent, 0f, 0f, 0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.sizeDelta = new Vector2(5f, 0f);
        var accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.sprite = WhiteSprite();
        accentImage.color = new Color(0.72f, 0.08f, 0.07f, 1f);
        accentImage.raycastTarget = false;

        titleLabel = CreateLabel("Title", plate, "HEALTH", 28f, TextAlignmentOptions.MidlineLeft);
        var titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0.62f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(22f, -12f);
        titleRect.sizeDelta = new Vector2(0f, 42f);
        titleLabel.color = new Color(0.82f, 0.14f, 0.11f);
        titleLabel.font = ResolveFont();
        titleLabel.characterSpacing = 3.5f;

        valueLabel = CreateLabel("Value", plate, "100%", 30f, TextAlignmentOptions.MidlineRight);
        var valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = new Vector2(-18f, -12f);
        valueRect.sizeDelta = new Vector2(0f, 42f);
        valueLabel.font = ResolveFont();

        var bar = CreateRect("Bar", plate);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 18f);
        bar.sizeDelta = new Vector2(-36f, 52f);
        barWidth = 508f - 36f;

        var track = CreateRect("Track", bar);
        Stretch(track, 0f, 0f, 1f, 1f);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = healthTrack != null ? healthTrack : WhiteSprite();
        trackImage.color = healthTrack != null ? Color.white : new Color(0.18f, 0.16f, 0.16f, 1f);
        trackImage.raycastTarget = false;

        trailMask = CreateMask("Trail Mask", bar);
        var trailImage = CreateFillImage("Trail", trailMask, new Color(1f, 0.55f, 0.48f, 1f));
        fillMask = CreateMask("Fill Mask", bar);
        fillImage = CreateFillImage("Fill", fillMask, Color.white);

        for (int i = 1; i <= 3; i++)
        {
            var tick = CreateRect("Tick " + i, bar);
            tick.anchorMin = tick.anchorMax = new Vector2(i * 0.25f, 0.5f);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.sizeDelta = new Vector2(2f, 52f);
            var tickImage = tick.gameObject.AddComponent<Image>();
            tickImage.sprite = WhiteSprite();
            tickImage.color = new Color(0f, 0f, 0f, 0.55f);
            tickImage.raycastTarget = false;
        }

        trailImage.raycastTarget = false;
    }

    RectTransform CreateMask(string name, RectTransform parent)
    {
        var mask = CreateRect(name, parent);
        mask.anchorMin = new Vector2(0f, 0f);
        mask.anchorMax = new Vector2(0f, 1f);
        mask.pivot = new Vector2(0f, 0.5f);
        mask.anchoredPosition = Vector2.zero;
        mask.sizeDelta = new Vector2(barWidth, 0f);
        mask.gameObject.AddComponent<RectMask2D>();
        return mask;
    }

    Image CreateFillImage(string name, RectTransform mask, Color color)
    {
        var rect = CreateRect(name, mask);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(barWidth, 0f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = healthFill != null ? healthFill : WhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    void BuildHurtVolume()
    {
        var volumeObject = new GameObject("AR Damage Global Volume");
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 80f;
        volume.weight = 0f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        vignette = profile.Add<Vignette>(true);
        vignette.color.value = new Color(0.62f, 0.015f, 0.02f, 1f);
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.intensity.value = 0f;
        vignette.smoothness.value = 0.55f;
        vignette.rounded.value = false;
        volume.sharedProfile = profile;
    }

    void BuildBlood()
    {
        bloodRoot = new GameObject("AR Damage Blood");
        var canvas = bloodRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = bloodRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        bloodGroup = bloodRoot.AddComponent<CanvasGroup>();
        bloodGroup.alpha = 0f;
        bloodGroup.blocksRaycasts = false;
        bloodGroup.interactable = false;

        var wash = CreateRect("Wash", bloodRoot.transform);
        Stretch(wash, 0f, 0f, 1f, 1f);
        var washImage = wash.gameObject.AddComponent<Image>();
        washImage.sprite = RadialSprite();
        washImage.color = new Color(0.45f, 0.02f, 0.03f, 0.92f);
        washImage.raycastTarget = false;

        Sprite[] splatters = { SplatterSprite(3), SplatterSprite(11), SplatterSprite(19), SplatterSprite(29) };
        int count = 12;
        for (int i = 0; i < count; i++)
        {
            var drop = CreateRect("Drop " + i, bloodRoot.transform);
            drop.anchorMin = drop.anchorMax = new Vector2(0.5f, 0.5f);
            drop.pivot = new Vector2(0.5f, 0.5f);
            var image = drop.gameObject.AddComponent<Image>();
            image.sprite = splatters[i % splatters.Length];
            image.color = new Color(0.42f, 0.01f, 0.02f, 0.96f);
            image.raycastTarget = false;
            bloodDrops.Add(drop);
        }

        ScatterBlood();
    }

    void ScatterBlood()
    {
        Vector2[] anchors =
        {
            new Vector2(-560f, 160f),
            new Vector2(580f, -120f),
            new Vector2(-80f, 300f),
            new Vector2(60f, -320f),
            new Vector2(-360f, -180f),
            new Vector2(340f, 220f),
            new Vector2(-160f, 40f),
            new Vector2(220f, -30f),
            new Vector2(120f, 140f),
            new Vector2(-480f, -20f),
            new Vector2(40f, 80f),
            new Vector2(-220f, -260f),
        };

        for (int i = 0; i < bloodDrops.Count; i++)
        {
            RectTransform drop = bloodDrops[i];
            Vector2 spot = i < anchors.Length ? anchors[i] : Vector2.zero;
            spot += Random.insideUnitCircle * (i < 2 ? 30f : 70f);
            drop.anchoredPosition = spot;
            float width = i < 2 ? Random.Range(460f, 680f) : i < 6 ? Random.Range(220f, 380f) : Random.Range(36f, 110f);
            float height = width * Random.Range(0.85f, 1.45f);
            drop.sizeDelta = new Vector2(width, height);
            drop.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            drop.localScale = new Vector3(Random.value > 0.5f ? -1f : 1f, 1f, 1f);
        }
    }

    bool EnablePostProcessing()
    {
        Camera[] cameras = Camera.allCameras;
        bool enabledOne = false;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
                continue;

            UniversalAdditionalCameraData data = cameras[i].GetUniversalAdditionalCameraData();
            if (data == null)
                continue;

            data.renderPostProcessing = true;
            data.volumeLayerMask = -1;
            enabledOne = true;
        }

        return enabledOne;
    }

    void LoadArt()
    {
        if (healthFill == null)
            healthFill = LoadSprite(FillSpritePath);
        if (healthTrack == null)
            healthTrack = LoadSprite(TrackSpritePath);
        if (titleFont == null)
            titleFont = LoadFont(TitleFontPath);
    }

    TMP_FontAsset ResolveFont()
    {
        if (titleFont != null)
            return titleFont;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    static Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
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

    static TMP_FontAsset LoadFont(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
#else
        _ = path;
        return null;
#endif
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name);
        rectObject.transform.SetParent(parent, false);
        return rectObject.AddComponent<RectTransform>();
    }

    static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static Sprite whiteSprite;
    static Sprite radialSprite;
    static readonly Dictionary<int, Sprite> splatters = new Dictionary<int, Sprite>();

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

    static Sprite RadialSprite()
    {
        if (radialSprite != null)
            return radialSprite;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = ((y + 0.5f) / size - 0.5f) * 0.82f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / 0.62f;
                byte alpha = (byte)(Mathf.Clamp01(Mathf.SmoothStep(0.15f, 1f, distance)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        radialSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        return radialSprite;
    }

    static Sprite SplatterSprite(int seed)
    {
        if (splatters.TryGetValue(seed, out Sprite cached))
            return cached;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        var random = new System.Random(seed);

        float cx = 128f + Range(random, -16f, 16f);
        float cy = 156f + Range(random, -8f, 14f);
        SoftBlob(pixels, size, cx, cy, 68f, 50f, 250);
        SoftBlob(pixels, size, cx + Range(random, -28f, 24f), cy + Range(random, -18f, 16f), 44f, 34f, 240);
        SoftBlob(pixels, size, cx + Range(random, -36f, 12f), cy + Range(random, -8f, 22f), 30f, 38f, 230);

        float dripX = cx + Range(random, -6f, 6f);
        float dripY = cy - 36f;
        int drips = 5 + random.Next(3);
        for (int i = 0; i < drips; i++)
        {
            dripY -= Range(random, 9f, 16f);
            float radius = Mathf.Lerp(15f, 3f, i / (float)(drips - 1));
            SoftBlob(pixels, size, dripX + Mathf.Sin(i * 1.4f) * 3f, dripY, radius * 0.72f, radius * 1.25f, (byte)(230 - i * 16));
        }

        int satellites = 6 + random.Next(4);
        for (int i = 0; i < satellites; i++)
        {
            float angle = Range(random, 0f, Mathf.PI * 2f);
            float dist = Range(random, 36f, 108f);
            float radius = Range(random, 2.5f, 11f);
            SoftBlob(
                pixels,
                size,
                cx + Mathf.Cos(angle) * dist,
                cy + Mathf.Sin(angle) * dist * 0.75f,
                radius,
                radius * Range(random, 0.65f, 1.35f),
                210);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        splatters.Add(seed, sprite);
        return sprite;
    }

    static float Range(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    static void SoftBlob(Color32[] pixels, int size, float cx, float cy, float rx, float ry, byte alpha)
    {
        if (rx < 0.5f || ry < 0.5f)
            return;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + rx));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + ry));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float nx = (x - cx) / rx;
                float ny = (y - cy) / ry;
                float distance = nx * nx + ny * ny;
                if (distance > 1f)
                    continue;

                float falloff = 1f - Mathf.SmoothStep(0.45f, 1f, Mathf.Sqrt(distance));
                byte coverage = (byte)(alpha * falloff);
                int index = y * size + x;
                if (pixels[index].a >= coverage)
                    continue;

                pixels[index] = new Color32(255, 255, 255, coverage);
            }
        }
    }
}
