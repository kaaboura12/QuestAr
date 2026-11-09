using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Contrôle l'ambiance visuelle et le composant Vignette URP :
/// - En temps normal : intensité 0.42 (ambiance tamisée sans assombrir la scène AR ni les zombies).
/// - Pendant les 5 secondes d'AlertHolding : fait pulser la vignette en rouge sang (effet battement de cœur).
/// - Préserve l'éclairage de la scène pour que les modèles 3D et textures restent parfaitement visibles.
/// - Inclut un fallback Canvas ScreenSpace pour garantir l'effet même sans Render Pipeline URP actif.
/// </summary>
[DefaultExecutionOrder(180)]
public class ARAlertVignetteController : MonoBehaviour
{
    public static ARAlertVignetteController Instance { get; private set; }

    [Header("Vignette Settings")]
    [SerializeField] private float normalIntensity = 0.42f;
    [SerializeField] private float alertPeakIntensity = 0.78f;
    [SerializeField] private Color normalColor = new Color(0.1f, 0.1f, 0.12f, 1f);
    [SerializeField] private Color bloodRedColor = new Color(0.85f, 0.02f, 0.03f, 1f);
    [SerializeField] private float heartbeatFrequency = 4.8f;
    [SerializeField] private float alertDuration = 5.0f;

    private Volume volume;
    private VolumeProfile profile;
    private Vignette vignette;

    // Fallback Canvas Overlay
    private GameObject fallbackCanvas;
    private Image fallbackVignetteImage;
    private CanvasGroup fallbackGroup;

    private float alertTimer = 0f;
    private bool isAlertActive = false;
    private float currentPulseIntensity = 0.42f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureController()
    {
        if (Instance == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ARGameplay")
        {
            var go = new GameObject("ARAlertVignetteController");
            go.AddComponent<ARAlertVignetteController>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUrpVolume();
        BuildFallbackOverlay();
    }

    private void OnEnable()
    {
        ARMicDetectionManager.OnLoudSound += HandleLoudSound;
    }

    private void OnDisable()
    {
        ARMicDetectionManager.OnLoudSound -= HandleLoudSound;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (profile != null)
            Destroy(profile);

        if (fallbackCanvas != null)
            Destroy(fallbackCanvas);
    }

    private void BuildUrpVolume()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 85f;
        volume.weight = 1f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;

        vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.value = normalColor;
        vignette.intensity.value = normalIntensity;
        vignette.smoothness.value = 0.65f;
        vignette.rounded.value = false;

        volume.sharedProfile = profile;
    }

    private void BuildFallbackOverlay()
    {
        fallbackCanvas = new GameObject("AR_Alert_Vignette_Fallback");
        var c = fallbackCanvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 35; // Derrière le HUD mais devant la scène AR
        var scaler = fallbackCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        fallbackGroup = fallbackCanvas.AddComponent<CanvasGroup>();
        fallbackGroup.alpha = normalIntensity * 0.4f;
        fallbackGroup.blocksRaycasts = false;
        fallbackGroup.interactable = false;

        var imgObj = new GameObject("VignetteImage", typeof(RectTransform));
        imgObj.transform.SetParent(fallbackCanvas.transform, false);
        var rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fallbackVignetteImage = imgObj.AddComponent<Image>();
        fallbackVignetteImage.sprite = CreateVignetteSprite();
        fallbackVignetteImage.color = bloodRedColor;
        fallbackVignetteImage.raycastTarget = false;
    }

    private Sprite CreateVignetteSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        float center = size * 0.5f;
        float maxDist = center * 1.35f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = Mathf.Clamp01(d / maxDist);
                byte alpha = (byte)(Mathf.SmoothStep(0.2f, 1f, norm) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void HandleLoudSound()
    {
        isAlertActive = true;
        alertTimer = alertDuration;
        Debug.Log("[ARAlertVignetteController] Alerte 5 secondes activée : pulsation rouge sang de la vignette !");
    }

    private void Update()
    {
        if (isAlertActive)
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer <= 0f)
            {
                isAlertActive = false;
                alertTimer = 0f;
            }

            // Battement de cœur : impulsion sinusoïdale asymétrique
            float pulseWave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * heartbeatFrequency)), 2.5f);
            currentPulseIntensity = Mathf.Lerp(normalIntensity, alertPeakIntensity, pulseWave);

            Color activeColor = Color.Lerp(normalColor, bloodRedColor, 0.85f + 0.15f * pulseWave);

            if (vignette != null)
            {
                vignette.intensity.value = currentPulseIntensity;
                vignette.color.value = activeColor;
            }

            if (fallbackGroup != null && fallbackVignetteImage != null)
            {
                fallbackGroup.alpha = currentPulseIntensity * 0.8f;
                fallbackVignetteImage.color = activeColor;
            }
        }
        else
        {
            // Retour doux vers l'intensité normale 0.42
            currentPulseIntensity = Mathf.MoveTowards(currentPulseIntensity, normalIntensity, 0.8f * Time.deltaTime);

            if (vignette != null)
            {
                vignette.intensity.value = currentPulseIntensity;
                vignette.color.value = Color.Lerp(vignette.color.value, normalColor, 2.0f * Time.deltaTime);
            }

            if (fallbackGroup != null && fallbackVignetteImage != null)
            {
                fallbackGroup.alpha = Mathf.MoveTowards(fallbackGroup.alpha, normalIntensity * 0.35f, 0.8f * Time.deltaTime);
                fallbackVignetteImage.color = Color.Lerp(fallbackVignetteImage.color, normalColor, 2.0f * Time.deltaTime);
            }
        }
    }
}
