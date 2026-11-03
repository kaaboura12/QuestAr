using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Death sequence. Blood closes over the view, then five seconds of the end
/// clip play with the game-over text on top. The last frame holds.
/// </summary>
[DefaultExecutionOrder(195)]
public class ARGameOver : MonoBehaviour
{
    const float VideoSeconds = 5f;
    const float FloodSeconds = 0.55f;
    const float RevealSeconds = 0.7f;
    const string VideoPath =
        "Assets/_Project/vedio/EndGame/mixkit-terrifying-group-of-zombies-walking-in-a-pack-through-smoke-44986-hd-ready.mp4";
    const string FontPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold SDF.asset";

    [SerializeField] VideoClip endClip;
    [SerializeField] TMP_FontAsset font;

    VideoPlayer player;
    RenderTexture target;
    CanvasGroup videoGroup;
    CanvasGroup bloodGroup;
    CanvasGroup copyGroup;
    GameObject root;
    float phaseTime;
    bool begun;
    bool playbackStarted;
    bool held;
    Phase phase = Phase.Idle;

    enum Phase
    {
        Idle,
        Flood,
        Reveal,
        Playing,
        Held
    }

    void Awake()
    {
        if (endClip == null)
            endClip = LoadClip(VideoPath);
        if (font == null)
            font = LoadFont(FontPath);

        Build();
        PreparePlayer();
    }

    void OnDestroy()
    {
        if (player != null)
            player.prepareCompleted -= OnPrepared;

        if (root != null)
            Destroy(root);

        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }

    public void Begin()
    {
        if (begun)
            return;

        begun = true;
        phase = Phase.Flood;
        phaseTime = 0f;
        root.SetActive(true);
        videoGroup.alpha = 0f;
        bloodGroup.alpha = 1f;
        copyGroup.alpha = 0f;
        bloodGroup.transform.localScale = Vector3.one * 1.22f;

        if (player != null && !player.isPrepared)
            player.Prepare();
    }

    void Update()
    {
        if (!begun || phase == Phase.Idle || phase == Phase.Held)
            return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        phaseTime += dt;

        if (phase == Phase.Flood)
        {
            float u = Mathf.Clamp01(phaseTime / FloodSeconds);
            bloodGroup.alpha = 1f;
            bloodGroup.transform.localScale = Vector3.one * Mathf.Lerp(1.22f, 1f, u);
            videoGroup.alpha = 0f;
            copyGroup.alpha = 0f;
            if (u >= 1f)
            {
                phase = Phase.Reveal;
                phaseTime = 0f;
                if (player != null && player.isPrepared && !playbackStarted)
                    StartPlayback();
            }

            return;
        }

        if (phase == Phase.Reveal)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / RevealSeconds));
            videoGroup.alpha = u;
            copyGroup.alpha = u;
            bloodGroup.alpha = Mathf.Lerp(1f, 0.22f, u);
            if (u >= 1f)
            {
                phase = Phase.Playing;
                phaseTime = 0f;
            }
        }

        if (!playbackStarted || player == null)
            return;

        double limit = VideoSeconds;
        if (player.length > 0.1)
            limit = System.Math.Min(VideoSeconds, player.length);

        if (player.time >= limit)
        {
            player.Pause();
            phase = Phase.Held;
            videoGroup.alpha = 1f;
            copyGroup.alpha = 1f;
            bloodGroup.alpha = 0.22f;
        }
    }

    void StartPlayback()
    {
        if (player == null || playbackStarted || endClip == null)
            return;

        playbackStarted = true;
        player.time = 0d;
        player.Play();
    }

    void OnPrepared(VideoPlayer source)
    {
        if (source.audioTrackCount > 0)
            source.SetDirectAudioVolume(0, 0.85f);

        if (begun && !playbackStarted && phase != Phase.Flood)
            StartPlayback();
    }

    void PreparePlayer()
    {
        target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        target.Create();

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = target;
        player.aspectRatio = VideoAspectRatio.FitOutside;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.clip = endClip;
        player.prepareCompleted += OnPrepared;

        var image = root.transform.Find("Video").GetComponent<RawImage>();
        image.texture = target;

        if (endClip != null)
            player.Prepare();
    }

    void Build()
    {
        root = new GameObject("AR Game Over");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var video = CreateRect("Video", root.transform);
        Stretch(video);
        var raw = video.gameObject.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        videoGroup = video.gameObject.AddComponent<CanvasGroup>();
        videoGroup.alpha = 0f;
        videoGroup.blocksRaycasts = false;

        var blood = CreateRect("Blood", root.transform);
        Stretch(blood);
        bloodGroup = blood.gameObject.AddComponent<CanvasGroup>();
        bloodGroup.alpha = 0f;
        bloodGroup.blocksRaycasts = false;
        BuildBlood(blood);

        var scrim = CreateRect("Scrim", root.transform);
        scrim.anchorMin = new Vector2(0f, 0f);
        scrim.anchorMax = new Vector2(1f, 0.42f);
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        var scrimImage = scrim.gameObject.AddComponent<Image>();
        scrimImage.sprite = BottomScrim();
        scrimImage.color = new Color(0f, 0f, 0f, 0.82f);
        scrimImage.raycastTarget = false;

        var copy = CreateRect("Copy", root.transform);
        Stretch(copy);
        copyGroup = copy.gameObject.AddComponent<CanvasGroup>();
        copyGroup.alpha = 0f;
        copyGroup.blocksRaycasts = false;
        scrim.SetParent(copy, false);

        TMP_FontAsset used = font != null
            ? font
            : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        var died = CreateLabel(copy, "Died", "YOU DIED", 28f, used);
        var diedRect = died.rectTransform;
        diedRect.anchorMin = diedRect.anchorMax = new Vector2(0.5f, 0.22f);
        diedRect.sizeDelta = new Vector2(800f, 48f);
        died.color = new Color(0.78f, 0.12f, 0.1f);
        died.characterSpacing = 8f;

        var rule = CreateRect("Rule", copy);
        rule.anchorMin = rule.anchorMax = new Vector2(0.5f, 0.175f);
        rule.sizeDelta = new Vector2(220f, 2f);
        var ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.sprite = WhiteSprite();
        ruleImage.color = new Color(0.72f, 0.1f, 0.08f, 0.95f);
        ruleImage.raycastTarget = false;

        var over = CreateLabel(copy, "Over", "GAME OVER", 78f, used);
        var overRect = over.rectTransform;
        overRect.anchorMin = overRect.anchorMax = new Vector2(0.5f, 0.1f);
        overRect.sizeDelta = new Vector2(1100f, 110f);
        over.color = new Color(0.94f, 0.93f, 0.9f);
        over.characterSpacing = 4f;
    }

    void BuildBlood(RectTransform parent)
    {
        var wash = CreateRect("Wash", parent);
        Stretch(wash);
        var washImage = wash.gameObject.AddComponent<Image>();
        washImage.sprite = WhiteSprite();
        washImage.color = new Color(0.32f, 0.01f, 0.02f, 0.94f);
        washImage.raycastTarget = false;

        Sprite splatter = Splatter();
        Vector2[] spots =
        {
            new Vector2(-520f, 180f),
            new Vector2(540f, -80f),
            new Vector2(-40f, 260f),
            new Vector2(80f, -300f),
            new Vector2(-300f, -160f),
            new Vector2(280f, 140f),
            new Vector2(0f, 20f),
            new Vector2(-180f, 40f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var drop = CreateRect("Drop " + i, parent);
            drop.anchorMin = drop.anchorMax = new Vector2(0.5f, 0.5f);
            drop.pivot = new Vector2(0.5f, 0.5f);
            drop.anchoredPosition = spots[i];
            float width = i < 4 ? 640f : 280f;
            drop.sizeDelta = new Vector2(width, width * 1.15f);
            drop.localRotation = Quaternion.Euler(0f, 0f, i * 37f);
            var image = drop.gameObject.AddComponent<Image>();
            image.sprite = splatter;
            image.color = new Color(0.4f, 0.01f, 0.02f, 0.95f);
            image.raycastTarget = false;
        }
    }

    static VideoClip LoadClip(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(path);
#else
        _ = path;
        return null;
#endif
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

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, TMP_FontAsset used)
    {
        var rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = used;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static Sprite whiteSprite;
    static Sprite splatterSprite;
    static Sprite scrimSprite;

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

    static Sprite BottomScrim()
    {
        if (scrimSprite != null)
            return scrimSprite;

        const int height = 64;
        var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[4 * height];
        for (int y = 0; y < height; y++)
        {
            byte alpha = (byte)(Mathf.Lerp(255f, 0f, y / (float)(height - 1)));
            for (int x = 0; x < 4; x++)
                pixels[y * 4 + x] = new Color32(255, 255, 255, alpha);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        scrimSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return scrimSprite;
    }

    static Sprite Splatter()
    {
        if (splatterSprite != null)
            return splatterSprite;

        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        SoftBlob(pixels, size, 64f, 78f, 40f, 32f, 245);
        SoftBlob(pixels, size, 78f, 70f, 24f, 20f, 230);
        SoftBlob(pixels, size, 48f, 66f, 18f, 22f, 220);
        float y = 58f;
        for (int i = 0; i < 6; i++)
        {
            y -= 9f;
            float radius = Mathf.Lerp(10f, 3f, i / 5f);
            SoftBlob(pixels, size, 66f, y, radius * 0.7f, radius, (byte)(210 - i * 18));
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        splatterSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        return splatterSprite;
    }

    static void SoftBlob(Color32[] pixels, int size, float cx, float cy, float rx, float ry, byte alpha)
    {
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

                byte coverage = (byte)(alpha * (1f - Mathf.SmoothStep(0.4f, 1f, Mathf.Sqrt(distance))));
                int index = y * size + x;
                if (pixels[index].a < coverage)
                    pixels[index] = new Color32(255, 255, 255, coverage);
            }
        }
    }
}
