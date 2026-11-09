using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fenêtre interactive à l'écran pour tester et calibrer le son / microphone :
/// - Affiche la puissance sonore (VU-mètre en direct et décibels temps réel).
/// - Permet d'ajuster le seuil de détection (-75 dB à -30 dB) et le gain d'amplification.
/// - Fournit un bouton direct "SIMULER VOIX / TEST 5S" pour déclencher l'alerte à tout moment.
/// - Indique le compte à rebours de 5 secondes pendant l'immobilisation des zombies puis le sprint.
/// - Réductible en un mini-badge discret.
/// </summary>
[DefaultExecutionOrder(190)]
public class ARMicTestWindow : MonoBehaviour
{
    public static ARMicTestWindow Instance { get; private set; }

    private GameObject rootCanvas;
    private RectTransform panelRect;
    private RectTransform miniBadgeRect;

    private Image vuMeterFill;
    private RectTransform thresholdMarker;
    private TextMeshProUGUI dbText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI thresholdBtnText;
    private TextMeshProUGUI gainBtnText;
    private TextMeshProUGUI miniBadgeText;

    private bool isMinimized = false;
    private float smoothNormalizedLevel = 0f;

    private static Sprite whiteSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildWindow();
    }

    private void Start()
    {
        // Enregistrer la fenêtre pour bloquer les tirs AR quand on clique dessus
        if (panelRect != null)
            ARMobileCombatControls.RegisterHitRect(panelRect);
        if (miniBadgeRect != null)
            ARMobileCombatControls.RegisterHitRect(miniBadgeRect);
    }

    private void OnDestroy()
    {
        if (panelRect != null)
            ARMobileCombatControls.UnregisterHitRect(panelRect);
        if (miniBadgeRect != null)
            ARMobileCombatControls.UnregisterHitRect(miniBadgeRect);

        if (rootCanvas != null)
            Destroy(rootCanvas);

        if (Instance == this)
            Instance = null;
    }

    private void BuildWindow()
    {
        rootCanvas = new GameObject("AR_Mic_Test_Window");
        var canvas = rootCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320; // Au-dessus du HUD de combat, sous Game Over

        var scaler = rootCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootCanvas.AddComponent<GraphicRaycaster>();

        TMP_FontAsset font = null;
        var reg = ARCombatAssetRegistry.Instance;
        if (reg != null && reg.robotoBoldFont != null)
            font = reg.robotoBoldFont;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // 1. Panneau Principal (Déplié)
        panelRect = CreateRect("Panel", rootCanvas.transform);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.98f);
        panelRect.pivot = new Vector2(0.5f, 1.0f);
        panelRect.anchoredPosition = new Vector2(0f, -10f);
        panelRect.sizeDelta = new Vector2(560f, 225f);

        var bgImage = panelRect.gameObject.AddComponent<Image>();
        bgImage.sprite = GetWhiteSprite();
        bgImage.color = new Color(0.06f, 0.08f, 0.11f, 0.93f);
        bgImage.raycastTarget = true;

        // Bordure rouge tactical
        var border = CreateRect("Border", panelRect);
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.sizeDelta = new Vector2(4f, 4f);
        var borderImage = border.gameObject.AddComponent<Image>();
        borderImage.sprite = GetWhiteSprite();
        borderImage.color = new Color(0.85f, 0.18f, 0.15f, 0.75f);
        border.SetAsFirstSibling();
        borderImage.raycastTarget = false;

        // Barre de titre
        var title = CreateLabel(panelRect, "Title", "RADAR MICRO & TEST ZOMBIE", 18f, font);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.88f);
        title.rectTransform.sizeDelta = new Vector2(420f, 28f);
        title.color = new Color(0.95f, 0.92f, 0.88f, 1f);
        title.fontStyle = FontStyles.Bold;

        // Bouton Réduire [-]
        var minBtn = CreateButton(panelRect, "Btn_Min", "-", new Vector2(245f, -16f), new Vector2(36f, 26f), font, 14f, ToggleMinimize);

        // 2. VU-Mètre (Barre Audio)
        var meterTrack = CreateRect("VU_Track", panelRect);
        meterTrack.anchorMin = meterTrack.anchorMax = new Vector2(0.5f, 0.68f);
        meterTrack.sizeDelta = new Vector2(500f, 20f);
        var trackImg = meterTrack.gameObject.AddComponent<Image>();
        trackImg.sprite = GetWhiteSprite();
        trackImg.color = new Color(0.14f, 0.16f, 0.20f, 1f);

        var meterFill = CreateRect("VU_Fill", meterTrack);
        meterFill.anchorMin = new Vector2(0f, 0f);
        meterFill.anchorMax = new Vector2(0f, 1f);
        meterFill.pivot = new Vector2(0f, 0.5f);
        meterFill.anchoredPosition = Vector2.zero;
        meterFill.sizeDelta = new Vector2(0f, 0f);
        vuMeterFill = meterFill.gameObject.AddComponent<Image>();
        vuMeterFill.sprite = GetWhiteSprite();
        vuMeterFill.color = new Color(0.2f, 0.85f, 0.3f, 1f);

        // Repère vertical du Seuil
        var marker = CreateRect("ThresholdMarker", meterTrack);
        marker.anchorMin = marker.anchorMax = new Vector2(0.35f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(3f, 26f);
        thresholdMarker = marker;
        var markerImg = marker.gameObject.AddComponent<Image>();
        markerImg.sprite = GetWhiteSprite();
        markerImg.color = new Color(1f, 0.2f, 0.2f, 1f);

        // Texte Décibels en direct
        dbText = CreateLabel(panelRect, "DbText", "Niveau: -100 dB | Seuil: -60 dB", 14f, font);
        dbText.rectTransform.anchorMin = dbText.rectTransform.anchorMax = new Vector2(0.5f, 0.54f);
        dbText.rectTransform.sizeDelta = new Vector2(500f, 22f);
        dbText.color = new Color(0.85f, 0.88f, 0.92f, 1f);

        // Texte État Actuel (Calme / Alerte / Sprint)
        statusText = CreateLabel(panelRect, "StatusText", "[OK] CALME (Parlez fort ou criez dans le micro)", 14f, font);
        statusText.rectTransform.anchorMin = statusText.rectTransform.anchorMax = new Vector2(0.5f, 0.41f);
        statusText.rectTransform.sizeDelta = new Vector2(500f, 22f);
        statusText.color = new Color(0.3f, 0.95f, 0.4f, 1f);
        statusText.fontStyle = FontStyles.Bold;

        // Boutons réglage Seuil et Gain
        thresholdBtnText = CreateButton(panelRect, "Btn_ThreshMinus", "Seuil -5 dB (Plus sensible)", new Vector2(-155f, -152f), new Vector2(175f, 30f), font, 11f, OnThresholdMinus);
        CreateButton(panelRect, "Btn_ThreshPlus", "Seuil +5 dB", new Vector2(25f, -152f), new Vector2(140f, 30f), font, 11f, OnThresholdPlus);
        gainBtnText = CreateButton(panelRect, "Btn_Gain", "Gain x3", new Vector2(185f, -152f), new Vector2(120f, 30f), font, 11f, OnToggleGain);

        // Gros Bouton de Simulation / Test
        var simBtn = CreateButton(panelRect, "Btn_Simulate", "SIMULER VOIX / TEST 5 SECONDES", new Vector2(0f, -195f), new Vector2(500f, 38f), font, 15f, OnSimulateClicked);
        simBtn.transform.parent.GetComponent<Image>().color = new Color(0.78f, 0.12f, 0.10f, 0.95f);
        simBtn.fontStyle = FontStyles.Bold;

        // 3. Mini-Badge Réduit (Discret)
        miniBadgeRect = CreateRect("MiniBadge", rootCanvas.transform);
        miniBadgeRect.anchorMin = miniBadgeRect.anchorMax = new Vector2(0.5f, 0.98f);
        miniBadgeRect.pivot = new Vector2(0.5f, 1.0f);
        miniBadgeRect.anchoredPosition = new Vector2(0f, -10f);
        miniBadgeRect.sizeDelta = new Vector2(290f, 44f);
        var miniImg = miniBadgeRect.gameObject.AddComponent<Image>();
        miniImg.sprite = GetWhiteSprite();
        miniImg.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        miniImg.raycastTarget = true;

        var miniBtn = miniBadgeRect.gameObject.AddComponent<Button>();
        miniBtn.onClick.AddListener(ToggleMinimize);

        miniBadgeText = CreateLabel(miniBadgeRect, "MiniText", "TEST MICRO : -60 dB", 14f, font);
        miniBadgeText.rectTransform.anchorMin = Vector2.zero;
        miniBadgeText.rectTransform.anchorMax = Vector2.one;
        miniBadgeText.rectTransform.sizeDelta = Vector2.zero;
        miniBadgeText.color = Color.white;
        miniBadgeText.fontStyle = FontStyles.Bold;

        miniBadgeRect.gameObject.SetActive(false);
    }

    private void ToggleMinimize()
    {
        isMinimized = !isMinimized;
        panelRect.gameObject.SetActive(!isMinimized);
        miniBadgeRect.gameObject.SetActive(isMinimized);
    }

    private void OnSimulateClicked()
    {
        Debug.Log("[ARMicTestWindow] Bouton SIMULER VOIX cliqué ! Déclenchement alerte 5 secondes...");
        ARMicDetectionManager.TriggerSoundManually();
    }

    private void OnThresholdMinus()
    {
        var mgr = ARMicDetectionManager.Instance;
        if (mgr != null)
        {
            mgr.SetThreshold(mgr.ThresholdDb - 5f);
        }
    }

    private void OnThresholdPlus()
    {
        var mgr = ARMicDetectionManager.Instance;
        if (mgr != null)
        {
            mgr.SetThreshold(mgr.ThresholdDb + 5f);
        }
    }

    private void OnToggleGain()
    {
        var mgr = ARMicDetectionManager.Instance;
        if (mgr != null)
        {
            float current = mgr.AmplificationGain;
            float next = current <= 1.5f ? 3.0f : (current <= 3.5f ? 6.0f : 1.0f);
            mgr.SetGain(next);
        }
    }

    private void Update()
    {
        bool shouldShow = QuestARLevelManager.IsMicUnlocked && !QuestARLevelManager.IsGameWon && !QuestARLevelManager.IsTransitioning;
        if (rootCanvas != null && rootCanvas.activeSelf != shouldShow)
            rootCanvas.SetActive(shouldShow);

        if (!shouldShow)
            return;

        var mgr = ARMicDetectionManager.Instance;
        if (mgr == null)
            return;

        float db = mgr.CurrentDecibels;
        float thresh = mgr.ThresholdDb;

        // Normaliser dB entre -80 et -10 pour la barre (0.0 à 1.0)
        float targetNorm = Mathf.Clamp01((db - (-80f)) / 70f);
        smoothNormalizedLevel = Mathf.Lerp(smoothNormalizedLevel, targetNorm, 25f * Time.deltaTime);

        float threshNorm = Mathf.Clamp01((thresh - (-80f)) / 70f);

        // Mise à jour de la jauge VU
        if (vuMeterFill != null)
        {
            float totalWidth = 500f;
            vuMeterFill.rectTransform.sizeDelta = new Vector2(totalWidth * smoothNormalizedLevel, 0f);

            if (db >= thresh)
            {
                vuMeterFill.color = new Color(1f, 0.15f, 0.15f, 1f); // Rouge alerte
            }
            else if (db >= thresh - 10f)
            {
                vuMeterFill.color = new Color(1f, 0.85f, 0.1f, 1f); // Jaune proche
            }
            else
            {
                vuMeterFill.color = new Color(0.2f, 0.85f, 0.35f, 1f); // Vert calme
            }
        }

        // Marqueur de seuil
        if (thresholdMarker != null)
        {
            thresholdMarker.anchorMin = thresholdMarker.anchorMax = new Vector2(threshNorm, 0.5f);
        }

        // Textes
        if (dbText != null)
        {
            string dbColor = db >= thresh ? "#FF4444" : "#DDDDDD";
            dbText.text = $"Niveau : <color={dbColor}>{db:F1} dB</color>  |  Seuil : <color=#FFCC00>{thresh:F1} dB</color>";
        }

        if (gainBtnText != null)
        {
            gainBtnText.text = $"Gain : x{mgr.AmplificationGain:F0}";
        }

        // État de l'alerte
        float timer = ARMicDetectionManager.AlertTimeRemaining;
        if (timer > 0f)
        {
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            string col = pulse > 0.5f ? "#FF2222" : "#FF7777";
            if (statusText != null)
                statusText.text = $"<color={col}>[!] ALERTE BRUIT ({timer:F1}s) : SPRINT IMMEDIAT DES ZOMBIES !</color>";
            if (miniBadgeText != null)
                miniBadgeText.text = $"<color={col}>[!] SPRINT EN COURS ({timer:F1}s)</color>";
        }
        else
        {
            // Vérifier si des zombies sont en sprint
            bool anyRunning = false;
            HamaZombieAI[] zombies = Object.FindObjectsByType<HamaZombieAI>(FindObjectsInactive.Exclude);
            foreach (var z in zombies)
            {
                if (z != null && z.currentState == HamaZombieAI.ZombieState.Running)
                {
                    anyRunning = true;
                    break;
                }
            }

            if (anyRunning)
            {
                if (statusText != null)
                    statusText.text = "<color=#FF6600>[SPRINT] LES ZOMBIES FONCENT SUR LA CAMERA (4.2 m/s) !</color>";
                if (miniBadgeText != null)
                    miniBadgeText.text = "<color=#FF6600>[SPRINT] ZOMBIES !</color>";
            }
            else
            {
                if (statusText != null)
                    statusText.text = "[MARCHE] Zombies en approche lente (0.95 m/s) - Faites du bruit pour les faire courir !";
                if (miniBadgeText != null)
                    miniBadgeText.text = $"TEST MICRO : {db:F0} dB (Seuil: {thresh:F0} dB)";
            }
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, TMP_FontAsset font)
    {
        var rt = CreateRect(name, parent);
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.font = font;
        lbl.fontSize = size;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.raycastTarget = false;
        return lbl;
    }

    private static TextMeshProUGUI CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, TMP_FontAsset font, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var rt = CreateRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1.0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        img.raycastTarget = true;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lbl = CreateLabel(rt, "Text", text, fontSize, font);
        lbl.rectTransform.anchorMin = Vector2.zero;
        lbl.rectTransform.anchorMax = Vector2.one;
        lbl.rectTransform.sizeDelta = Vector2.zero;
        lbl.color = Color.white;

        return lbl;
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
