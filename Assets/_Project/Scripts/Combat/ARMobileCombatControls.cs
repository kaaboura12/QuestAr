using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Thumb buttons for a phone. AIM and FIRE stay under the thumbs and are held.
/// RELOAD sits above fire, SWAP above aim. Those two are a tap: reload, or holster
/// the current gun and draw the other one.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ARMobileCombatControls : MonoBehaviour
{
    const float ButtonReach = 188f;
    const float ButtonHit = 268f;
    const float ButtonVisual = 210f;
    const float ActionLift = 436f;
    const float ActionHit = 176f;
    const float ActionVisual = 140f;

    static readonly List<RectTransform> hitRects = new List<RectTransform>();
    static Sprite discSprite;
    static Sprite ringSprite;

    ARInfimaShooterInput shooter;
    ARCombatTouchButton aimButton;
    ARCombatTouchButton fireButton;
    ARCombatTouchButton reloadButton;
    ARCombatTouchButton swapButton;
    ARCombatTouchButton flashButton;
    GameObject canvasRoot;
    RectTransform safeArea;
    CanvasGroup canvasGroup;

    public static bool ContainsScreenPoint(Vector2 screenPoint)
    {
        for (int i = hitRects.Count - 1; i >= 0; i--)
        {
            RectTransform rect = hitRects[i];
            if (rect == null)
            {
                hitRects.RemoveAt(i);
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null))
                return true;
        }

        return false;
    }

    internal static void RegisterHitRect(RectTransform rect)
    {
        if (rect != null && !hitRects.Contains(rect))
            hitRects.Add(rect);
    }

    internal static void UnregisterHitRect(RectTransform rect)
    {
        hitRects.Remove(rect);
    }

    void Awake()
    {
        shooter = GetComponent<ARInfimaShooterInput>();
        if (Application.isMobilePlatform)
            Input.simulateMouseWithTouches = false;

        EnsureEventSystem();
        Build();
    }

    void Update()
    {
        if (shooter == null)
            shooter = GetComponent<ARInfimaShooterInput>();

        if (safeArea != null)
            ApplySafeArea(safeArea);

        var vitals = GetComponent<ARZombieVitals>();
        bool dead = (vitals != null && vitals.IsDead) || QuestARLevelManager.IsGameWon || QuestARLevelManager.IsTransitioning;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = dead ? 0f : 1f;
            canvasGroup.interactable = !dead;
            canvasGroup.blocksRaycasts = !dead;
        }

        // Déblocage progressif des boutons par Niveau :
        // Niveau 1 : pas de SWAP, pas de FLASH
        // Niveau 2 : SWAP débloqué, pas de FLASH
        // Niveau 3+ : SWAP + FLASH débloqués
        if (swapButton != null && swapButton.gameObject.activeSelf != QuestARLevelManager.IsSecondWeaponUnlocked)
            swapButton.gameObject.SetActive(QuestARLevelManager.IsSecondWeaponUnlocked);

        if (flashButton != null && flashButton.gameObject.activeSelf != QuestARLevelManager.IsFlashUnlocked)
            flashButton.gameObject.SetActive(QuestARLevelManager.IsFlashUnlocked);

        if (dead)
            ReleaseAll();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            ReleaseAll();
    }

    void OnDestroy()
    {
        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    void ReleaseAll()
    {
        aimButton?.ForceRelease();
        fireButton?.ForceRelease();
        reloadButton?.ForceRelease();
        swapButton?.ForceRelease();
        flashButton?.ForceRelease();
    }

    void Build()
    {
        canvasRoot = new GameObject("AR Combat Controls");
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;
        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasRoot.AddComponent<CanvasGroup>();

        var safeObject = CreateRect("Safe Area", canvasRoot.transform);
        safeArea = safeObject;
        Stretch(safeArea);
        ApplySafeArea(safeArea);

        TMP_FontAsset font = LoadFont();
        aimButton = CreateButton(
            safeArea,
            "Aim",
            "AIM",
            ARCombatTouchButton.Kind.Aim,
            new Vector2(0f, 0f),
            new Vector2(ButtonReach, ButtonReach),
            ButtonHit,
            ButtonVisual,
            34f,
            font);
        fireButton = CreateButton(
            safeArea,
            "Fire",
            "FIRE",
            ARCombatTouchButton.Kind.Fire,
            new Vector2(1f, 0f),
            new Vector2(-ButtonReach, ButtonReach),
            ButtonHit,
            ButtonVisual,
            34f,
            font);
        swapButton = CreateButton(
            safeArea,
            "Swap",
            "SWAP",
            ARCombatTouchButton.Kind.Swap,
            new Vector2(0f, 0f),
            new Vector2(ButtonReach, ActionLift),
            ActionHit,
            ActionVisual,
            26f,
            font);
        reloadButton = CreateButton(
            safeArea,
            "Reload",
            "RELOAD",
            ARCombatTouchButton.Kind.Reload,
            new Vector2(1f, 0f),
            new Vector2(-ButtonReach, ActionLift),
            ActionHit,
            ActionVisual,
            22f,
            font);
        flashButton = CreateButton(
            safeArea,
            "Flash",
            "FLASH",
            ARCombatTouchButton.Kind.Flash,
            new Vector2(1f, 0f),
            new Vector2(-ButtonReach, ActionLift + 175f),
            ActionHit,
            ActionVisual,
            24f,
            font);

        aimButton.Bind(shooter);
        fireButton.Bind(shooter);
        swapButton.Bind(shooter);
        reloadButton.Bind(shooter);
        flashButton.Bind(shooter);
    }

    ARCombatTouchButton CreateButton(
        Transform parent,
        string name,
        string caption,
        ARCombatTouchButton.Kind kind,
        Vector2 anchor,
        Vector2 position,
        float hitSize,
        float visualSize,
        float fontSize,
        TMP_FontAsset font)
    {
        var root = CreateRect(name, parent);
        root.anchorMin = root.anchorMax = anchor;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(hitSize, hitSize);
        root.anchoredPosition = position;

        var hit = root.gameObject.AddComponent<Image>();
        hit.sprite = Disc();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        var visual = CreateRect("Visual", root);
        visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
        visual.pivot = new Vector2(0.5f, 0.5f);
        visual.sizeDelta = new Vector2(visualSize, visualSize);
        visual.anchoredPosition = Vector2.zero;

        var fillRect = CreateRect("Fill", visual);
        Stretch(fillRect);
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = Disc();
        fill.raycastTarget = false;

        var ringRect = CreateRect("Ring", visual);
        Stretch(ringRect);
        var ring = ringRect.gameObject.AddComponent<Image>();
        ring.sprite = Ring();
        ring.raycastTarget = false;

        CreateCaption(visual, caption, font, fontSize);

        var button = root.gameObject.AddComponent<ARCombatTouchButton>();
        button.Configure(kind, visual, fill, ring);
        return button;
    }

    static void CreateCaption(Transform parent, string text, TMP_FontAsset font, float fontSize)
    {
        var rect = CreateRect("Label", parent);
        Stretch(rect);

        if (font != null)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 3f;
            label.color = new Color(0.96f, 0.94f, 0.9f, 1f);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return;
        }

        var fallback = rect.gameObject.AddComponent<Text>();
        fallback.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fallback.text = text;
        fallback.fontSize = Mathf.RoundToInt(fontSize);
        fallback.fontStyle = FontStyle.Bold;
        fallback.alignment = TextAnchor.MiddleCenter;
        fallback.color = new Color(0.96f, 0.94f, 0.9f, 1f);
        fallback.raycastTarget = false;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null ||
            FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        var eventObject = new GameObject("EventSystem");
        eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<InputSystemUIInputModule>();
    }

    static void ApplySafeArea(RectTransform rect)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect area = Screen.safeArea;
        var min = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        var max = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        if (rect.anchorMin == min && rect.anchorMax == max)
            return;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static TMP_FontAsset LoadFont()
    {
        var registry = ARCombatAssetRegistry.Instance;
        if (registry != null && registry.robotoBoldFont != null)
            return registry.robotoBoldFont;

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
#if UNITY_EDITOR
        if (font == null)
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold SDF.asset");
        }
#endif
        return font;
    }

    static Sprite Disc()
    {
        if (discSprite == null)
            discSprite = BuildCircle(false);
        return discSprite;
    }

    static Sprite Ring()
    {
        if (ringSprite == null)
            ringSprite = BuildCircle(true);
        return ringSprite;
    }

    static Sprite BuildCircle(bool ring)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float outer = center - 1f;
        float inner = outer - 8f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = ring
                    ? Mathf.Min(
                        Mathf.Clamp01(outer - distance + 0.75f),
                        Mathf.Clamp01(distance - inner + 0.75f))
                    : Mathf.Clamp01(outer - distance + 0.75f);
                byte channel = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, channel);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}

/// <summary>
/// Hold target for one combat button. Tracks its own finger so aim and fire work together.
/// </summary>
public class ARCombatTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    static readonly Color IdleFill = new Color(0.07f, 0.06f, 0.06f, 0.5f);
    static readonly Color IdleAimRing = new Color(0.92f, 0.9f, 0.86f, 0.9f);
    static readonly Color IdleFireRing = new Color(0.9f, 0.16f, 0.1f, 0.95f);
    static readonly Color HeldFill = new Color(0.72f, 0.1f, 0.07f, 0.88f);
    static readonly Color HeldRing = new Color(1f, 0.32f, 0.22f, 1f);
    static readonly Color IdleFlashRing = new Color(1f, 0.82f, 0.2f, 0.95f);
    static readonly Color ActiveFlashRing = new Color(1f, 0.95f, 0.5f, 1f);
    static readonly Color ActiveFlashFill = new Color(0.9f, 0.72f, 0.1f, 0.55f);

    static readonly List<ARCombatTouchButton> activeButtons = new List<ARCombatTouchButton>();

    public enum Kind
    {
        Aim,
        Fire,
        Reload,
        Swap,
        Flash
    }

    ARInfimaShooterInput shooter;
    RectTransform hitRect;
    RectTransform visual;
    Image fill;
    Image ring;
    TextMeshProUGUI labelTmp;
    Text labelText;
    Kind kind;
    bool held;
    bool pointerHeld;
    int pointerId = int.MinValue;
    int trackedTouchId = int.MinValue;
    float lastTapActionTime = -10f;

    public void Configure(Kind buttonKind, RectTransform visualRect, Image fillImage, Image ringImage)
    {
        kind = buttonKind;
        hitRect = transform as RectTransform;
        visual = visualRect;
        fill = fillImage;
        ring = ringImage;
        labelTmp = GetComponentInChildren<TextMeshProUGUI>();
        labelText = GetComponentInChildren<Text>();
        ApplyVisual(false, 1f);
    }

    public void Bind(ARInfimaShooterInput shooterInput)
    {
        shooter = shooterInput;
    }

    void OnEnable()
    {
        if (hitRect == null)
            hitRect = transform as RectTransform;
        ARMobileCombatControls.RegisterHitRect(hitRect);
        if (!activeButtons.Contains(this))
            activeButtons.Add(this);
    }

    void OnDisable()
    {
        ARMobileCombatControls.UnregisterHitRect(hitRect);
        activeButtons.Remove(this);
        ForceRelease();
    }

    void Update()
    {
        PollDirectTouches();
        ReleasePointerIfAllInputGone();

        bool shouldHold = pointerHeld || trackedTouchId != int.MinValue;
        if (shouldHold != held || (shouldHold && (kind == Kind.Fire || kind == Kind.Aim)))
        {
            held = shouldHold;
            PushHeld(held);
        }

        bool engaged = held;
        if (kind == Kind.Fire)
            engaged = (shooter != null && shooter.TouchFireHeld) || held;
        else if (kind == Kind.Aim)
            engaged = (shooter != null && shooter.TouchAimHeld) || held;

        if (kind == Kind.Flash)
        {
            bool isStunActive = HamaFlashStunManager.Instance != null && HamaFlashStunManager.Instance.IsFlashActive;
            bool isInCooldown = HamaFlashStunManager.Instance != null && HamaFlashStunManager.Instance.IsInCooldown;
            if (isStunActive)
            {
                string stunText = $"FLASH\n{HamaFlashStunManager.Instance.RemainingStunTime:F0}s";
                if (labelTmp != null) labelTmp.text = stunText;
                if (labelText != null) labelText.text = stunText;
            }
            else if (isInCooldown)
            {
                string coolText = $"PAUSE\n{HamaFlashStunManager.Instance.RemainingCooldownTime:F0}s";
                if (labelTmp != null) labelTmp.text = coolText;
                if (labelText != null) labelText.text = coolText;
            }
            else
            {
                if (labelTmp != null && labelTmp.text != "FLASH") labelTmp.text = "FLASH";
                if (labelText != null && labelText.text != "FLASH") labelText.text = "FLASH";
            }
        }

        float targetScale = engaged ? 0.94f : 1f;
        if (visual != null)
        {
            visual.localScale = Vector3.Lerp(
                visual.localScale,
                Vector3.one * targetScale,
                1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        }

        ApplyVisual(engaged, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        pointerId = eventData.pointerId;
        pointerHeld = true;
        held = true;
        if (kind == Kind.Reload || kind == Kind.Swap || kind == Kind.Flash)
            TriggerTapAction();
        else
            PushHeld(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId && pointerId != int.MinValue)
            return;

        pointerHeld = false;
        pointerId = int.MinValue;
        if (trackedTouchId == int.MinValue)
        {
            held = false;
            PushHeld(false);
        }
    }

    public void ForceRelease()
    {
        held = false;
        pointerHeld = false;
        pointerId = int.MinValue;
        trackedTouchId = int.MinValue;
        if (kind == Kind.Fire)
            shooter?.SetTouchFireHeld(false);
        else if (kind == Kind.Aim)
            shooter?.SetTouchAimHeld(false);
    }

    void TriggerTapAction()
    {
        if (Time.unscaledTime - lastTapActionTime < 0.12f)
            return;

        lastTapActionTime = Time.unscaledTime;
        if (kind == Kind.Reload)
            shooter?.RequestReload();
        else if (kind == Kind.Swap && QuestARLevelManager.IsSecondWeaponUnlocked)
            shooter?.GetComponent<ARInfimaWeaponSwitcher>()?.TrySwap();
        else if (kind == Kind.Flash && QuestARLevelManager.IsFlashUnlocked)
        {
            if (HamaFlashStunManager.Instance != null && HamaFlashStunManager.Instance.CanTriggerFlash)
                HamaFlashStunManager.Instance.TriggerFlashStun();
        }
    }

    void PollDirectTouches()
    {
        if (hitRect == null)
            return;

        Touchscreen screen = Touchscreen.current;
        if (screen == null)
        {
            trackedTouchId = int.MinValue;
            return;
        }

        if (trackedTouchId != int.MinValue)
        {
            bool stillActive = false;
            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.press.isPressed && touch.touchId.ReadValue() == trackedTouchId)
                {
                    stillActive = true;
                    break;
                }
            }

            if (!stillActive)
                trackedTouchId = int.MinValue;
        }

        if (trackedTouchId == int.MinValue)
        {
            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!touch.press.isPressed)
                    continue;

                int id = touch.touchId.ReadValue();
                if (IsTouchClaimedByOtherButton(id))
                    continue;

                Vector2 pos = touch.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(hitRect, pos, null))
                    continue;

                if (kind == Kind.Reload || kind == Kind.Swap || kind == Kind.Flash)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        trackedTouchId = id;
                        TriggerTapAction();
                        break;
                    }
                }
                else
                {
                    trackedTouchId = id;
                    break;
                }
            }
        }
    }

    bool IsTouchClaimedByOtherButton(int touchId)
    {
        for (int i = 0; i < activeButtons.Count; i++)
        {
            var other = activeButtons[i];
            if (other != null && other != this && other.trackedTouchId == touchId)
                return true;
        }

        return false;
    }

    void ReleasePointerIfAllInputGone()
    {
        if (!pointerHeld)
            return;

        bool anyTouchDown = false;
        Touchscreen screen = Touchscreen.current;
        if (screen != null)
        {
            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed)
                {
                    anyTouchDown = true;
                    break;
                }
            }
        }

        if (!anyTouchDown && Input.touchCount > 0)
            anyTouchDown = true;

        bool mouseDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (anyTouchDown || mouseDown)
            return;

        pointerHeld = false;
        pointerId = int.MinValue;
    }

    void PushHeld(bool value)
    {
        if (shooter == null)
            return;

        if (kind == Kind.Fire)
            shooter.SetTouchFireHeld(value);
        else if (kind == Kind.Aim)
            shooter.SetTouchAimHeld(value);
    }

    void ApplyVisual(bool engaged, float blend)
    {
        bool isFlashStunActive = (kind == Kind.Flash) &&
            (HamaFlashStunManager.Instance != null && HamaFlashStunManager.Instance.IsFlashActive);
        bool isFlashInCooldown = (kind == Kind.Flash) &&
            (HamaFlashStunManager.Instance != null && HamaFlashStunManager.Instance.IsInCooldown);

        if (fill != null)
        {
            Color target;
            if (isFlashInCooldown)
                target = new Color(0.18f, 0.18f, 0.22f, 0.75f);
            else if (engaged || isFlashStunActive)
                target = (kind == Kind.Flash ? ActiveFlashFill : HeldFill);
            else
                target = IdleFill;

            fill.color = Color.Lerp(fill.color, target, blend);
        }

        if (ring == null)
            return;

        Color ringTarget;
        if (kind == Kind.Flash)
        {
            if (isFlashInCooldown)
                ringTarget = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            else if (engaged || isFlashStunActive)
                ringTarget = ActiveFlashRing;
            else
                ringTarget = IdleFlashRing;
        }
        else if (kind == Kind.Fire)
            ringTarget = engaged ? HeldRing : IdleFireRing;
        else
            ringTarget = engaged ? HeldRing : IdleAimRing;

        ring.color = Color.Lerp(ring.color, ringTarget, blend);
    }
}
