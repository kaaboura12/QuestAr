using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VisageIDAuth.Models;

/// <summary>
/// Gère la progression des 4 niveaux de QuestAR entre le combat (ARGameplay) et l'énigme de Souhaila (Mission5_Enigme) :
/// - Niveau 1 : 1 seule arme (Pistolet / Fard), pas de Flash, pas de Micro -> Tuer 3 zombies -> Énigme 2 chiffres -> Débloque 2ème Arme.
/// - Niveau 2 : 2 armes (SWAP actif), pas de Flash, pas de Micro -> Tuer 5 zombies -> Énigme 3 chiffres -> Débloque Flash (Capteur Lumière).
/// - Niveau 3 : 2 armes + Flash -> Tuer 7 zombies -> Énigme 4 chiffres -> Débloque Micro.
/// - Niveau 4 : 2 armes + Flash + Micro -> Tuer 10 zombies -> VICTOIRE FINALE !
/// </summary>
[DefaultExecutionOrder(-80)]
public class QuestARLevelManager : MonoBehaviour
{
    public static QuestARLevelManager Instance { get; private set; }

    public static int CurrentLevel { get; private set; } = 1;
    public static int KillsInCurrentLevel { get; private set; } = 0;
    public static bool IsTransitioning { get; private set; } = false;
    public static bool IsGameWon { get; private set; } = false;

    public static bool IsSecondWeaponUnlocked => CurrentLevel >= 2;
    public static bool IsFlashUnlocked => CurrentLevel >= 3;
    public static bool IsMicUnlocked => CurrentLevel >= 4;

    public static int TargetKillsForCurrentLevel => GetTargetKillsForLevel(CurrentLevel);

    public static int GetTargetKillsForLevel(int level)
    {
        switch (level)
        {
            case 1: return 3;
            case 2: return 5;
            case 3: return 7;
            case 4: return 10;
            default: return 10;
        }
    }

    public static int GetPuzzleDigitsForCurrentLevel()
    {
        switch (CurrentLevel)
        {
            case 1: return 2; // Niveau 1 -> 2 chiffres
            case 2: return 3; // Niveau 2 -> 3 chiffres
            default: return 4; // Niveau 3 -> 4 chiffres
        }
    }

    public static string GetPuzzleRewardTitle()
    {
        switch (CurrentLevel)
        {
            case 1: return "ARME DÉBLOQUÉE : FUSIL D'ASSAUT !";
            case 2: return "FLASH + CAPTEUR LUMIÈRE DÉBLOQUÉ !";
            default: return "MICROPHONE SONORE DÉBLOQUÉ !";
        }
    }

    public static string GetPuzzleRewardDescription()
    {
        switch (CurrentLevel)
        {
            case 1:
                return "Bravo ! Le sceau à 2 chiffres est brisé.\nVous débloquez le FUSIL D'ASSAUT et le bouton SWAP pour le Niveau 2 (5 zombies) !";
            case 2:
                return "Bravo ! Le sceau à 3 chiffres est brisé.\nVous débloquez le FLASH intelligent (relié au capteur de lumière du téléphone) pour le Niveau 3 (7 zombies) !";
            default:
                return "Bravo ! Le sceau à 4 chiffres est brisé.\nLe MICROPHONE est activé pour le Niveau 4 Final (10 zombies) : si vous parlez fort, les zombies courront vers vous !";
        }
    }

    public static string GetNextMissionButtonText()
    {
        int nextLvl = Mathf.Min(4, CurrentLevel + 1);
        return $"PASSER AU NIVEAU {nextLvl}";
    }

    public static void ResetToLevelOne()
    {
        CurrentLevel = 1;
        KillsInCurrentLevel = 0;
        IsTransitioning = false;
        IsGameWon = false;
        ProgressManager.Reinitialiser();
        Debug.Log("[QuestARLevelManager] Progression réinitialisée au Niveau 1.");
    }

    public static void ResetCurrentLevelKills()
    {
        KillsInCurrentLevel = 0;
        IsTransitioning = false;
        IsGameWon = false;
    }

    public static void CompleteEnigmeAndLoadNextLevel()
    {
        CurrentLevel = Mathf.Clamp(CurrentLevel + 1, 1, 4);
        KillsInCurrentLevel = 0;
        IsTransitioning = false;
        IsGameWon = false;
        ProgressManager.Reinitialiser();

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        Debug.Log($"[QuestARLevelManager] Énigme résolue ! Chargement du Niveau {CurrentLevel} dans ARGameplay...");
        SceneManager.LoadScene("ARGameplay");
    }

    private GameObject hudCanvasObj;
    private TextMeshProUGUI levelHeaderText;
    private TextMeshProUGUI levelSubText;
    private GameObject transitionOverlayObj;
    private TextMeshProUGUI transitionTitleText;
    private TextMeshProUGUI transitionDescText;
    private GameObject victoryOverlayObj;
    private CanvasGroup victoryCanvasGroup;
    private RectTransform victoryCardRect;
    private RectTransform replayBtnRect;
    private RectTransform logoutBtnRect;
    private int victoryActionFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        KillsInCurrentLevel = 0;
        IsTransitioning = false;
        IsGameWon = false;
    }

    private void Start()
    {
        BuildLevelHud();
        UpdateHudDisplay();
    }

    private void Update()
    {
        // Si le joueur est mort (0% Health), annuler immédiatement toute transition vers l'énigme pour laisser l'écran GAME OVER !
        if (IsPlayerDead() && IsTransitioning)
        {
            StopAllCoroutines();
            IsTransitioning = false;
            if (transitionOverlayObj != null)
                transitionOverlayObj.SetActive(false);
        }

        // Secours tactile direct pour les boutons de l'écran de Victoire Totale
        if (IsGameWon && victoryOverlayObj != null && victoryOverlayObj.activeSelf)
        {
            Vector2 touchPos;
            bool pressed = false;
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                touchPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                pressed = true;
            }
            else if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                touchPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                pressed = true;
            }
            else
            {
                return;
            }

            if (pressed && Time.frameCount != victoryActionFrame)
            {
                if (replayBtnRect != null && RectTransformUtility.RectangleContainsScreenPoint(replayBtnRect, touchPos, null))
                {
                    victoryActionFrame = Time.frameCount;
                    OnVictoryReplayClicked();
                }
                else if (logoutBtnRect != null && RectTransformUtility.RectangleContainsScreenPoint(logoutBtnRect, touchPos, null))
                {
                    victoryActionFrame = Time.frameCount;
                    OnVictoryLogoutClicked();
                }
            }
        }
    }

    public static bool IsPlayerDead()
    {
        var vitals = Object.FindAnyObjectByType<ARZombieVitals>();
        return vitals != null && vitals.IsDead;
    }

    private void OnDestroy()
    {
        if (replayBtnRect != null)
            ARMobileCombatControls.UnregisterHitRect(replayBtnRect);
        if (logoutBtnRect != null)
            ARMobileCombatControls.UnregisterHitRect(logoutBtnRect);
        if (hudCanvasObj != null)
            Destroy(hudCanvasObj);
        if (Instance == this)
            Instance = null;
    }

    public static void NotifyZombieKilled()
    {
        if (IsTransitioning || IsGameWon || IsPlayerDead())
            return;

        KillsInCurrentLevel++;
        Debug.Log($"[QuestARLevelManager] Zombie éliminé ! ({KillsInCurrentLevel}/{TargetKillsForCurrentLevel}) au Niveau {CurrentLevel}.");

        if (Instance != null)
        {
            Instance.UpdateHudDisplay();
            if (KillsInCurrentLevel >= TargetKillsForCurrentLevel && !IsPlayerDead())
            {
                Instance.TriggerLevelObjectiveReached();
            }
        }
    }

    public void UpdateHudDisplay()
    {
        if (levelHeaderText == null)
            return;

        int target = TargetKillsForCurrentLevel;
        levelHeaderText.text = $"NIVEAU {CurrentLevel} / 4   |   ZOMBIES : <color=#FF4444>{KillsInCurrentLevel} / {target}</color>";

        if (levelSubText != null)
        {
            string equip;
            if (CurrentLevel == 1)
                equip = "Arme : Pistolet seul  |  Flash : Verrouillé  |  Micro : Verrouillé";
            else if (CurrentLevel == 2)
                equip = "Armes : Pistolet + Fusil (SWAP)  |  Flash : Verrouillé  |  Micro : Verrouillé";
            else if (CurrentLevel == 3)
                equip = "Armes : 2 (SWAP)  |  Flash : <color=#FFE066>ACTIF (Capteur Lumière)</color>  |  Micro : Verrouillé";
            else
                equip = "Armes : 2 (SWAP)  |  Flash : <color=#FFE066>ACTIF</color>  |  Micro : <color=#FF5555>ACTIF (Silence requis !)</color>";

            levelSubText.text = equip;
        }
    }

    private void TriggerLevelObjectiveReached()
    {
        if (IsTransitioning || IsGameWon || IsPlayerDead())
            return;

        if (CurrentLevel < 4)
        {
            IsTransitioning = true;
            StartCoroutine(TransitionToEnigmeRoutine());
        }
        else
        {
            IsGameWon = true;
            StartCoroutine(ShowFinalVictoryRoutine());
        }
    }

    private IEnumerator TransitionToEnigmeRoutine()
    {
        if (IsPlayerDead())
        {
            IsTransitioning = false;
            yield break;
        }

        if (transitionOverlayObj != null)
        {
            transitionOverlayObj.SetActive(true);
            int digits = GetPuzzleDigitsForCurrentLevel();
            string nextUnlock = CurrentLevel == 1 ? "le 2ème Fusil (SWAP)" : (CurrentLevel == 2 ? "le Flash (Capteur Lumière)" : "le Microphone");
            if (transitionTitleText != null)
                transitionTitleText.text = $"NIVEAU {CurrentLevel} TERMINÉ !";
            if (transitionDescText != null)
                transitionDescText.text = $"Tous les zombies sont éliminés !\nOuverture de l'Énigme de Souhaila (Code à {digits} chiffres)\npour débloquer {nextUnlock}...";
        }

        float elapsed = 0f;
        while (elapsed < 2.4f)
        {
            if (IsPlayerDead())
            {
                IsTransitioning = false;
                if (transitionOverlayObj != null)
                    transitionOverlayObj.SetActive(false);
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (IsPlayerDead())
        {
            IsTransitioning = false;
            yield break;
        }

        ProgressManager.Reinitialiser();
        SceneManager.LoadScene("Mission5_Enigme");
    }

    private IEnumerator ShowFinalVictoryRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryOverlayObj != null)
        {
            victoryOverlayObj.SetActive(true);
            if (victoryCanvasGroup != null)
                victoryCanvasGroup.alpha = 0f;
            if (victoryCardRect != null)
                victoryCardRect.localScale = Vector3.one * 0.92f;

            // Effet sonore de victoire
            AudioManager.Jouer("relique_ok");

            float t = 0f;
            float duration = 0.85f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                if (victoryCanvasGroup != null)
                    victoryCanvasGroup.alpha = progress;
                if (victoryCardRect != null)
                    victoryCardRect.localScale = Vector3.Lerp(Vector3.one * 0.92f, Vector3.one, progress);
                yield return null;
            }

            if (victoryCanvasGroup != null)
                victoryCanvasGroup.alpha = 1f;
            if (victoryCardRect != null)
                victoryCardRect.localScale = Vector3.one;
        }
    }

    private void BuildLevelHud()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        hudCanvasObj = new GameObject("QuestAR_LevelHUD_Canvas");
        var canvas = hudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 410;

        var scaler = hudCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        hudCanvasObj.AddComponent<GraphicRaycaster>();

        // 1. Top-Right / Center-Right Level Banner
        var banner = CreateRect("LevelBanner", hudCanvasObj.transform);
        banner.anchorMin = banner.anchorMax = new Vector2(1f, 1f);
        banner.pivot = new Vector2(1f, 1f);
        banner.anchoredPosition = new Vector2(-28f, -22f);
        banner.sizeDelta = new Vector2(680f, 82f);

        var bannerBg = banner.gameObject.AddComponent<Image>();
        bannerBg.color = new Color(0.05f, 0.05f, 0.07f, 0.88f);
        bannerBg.raycastTarget = false;

        var outline = banner.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.18f, 0.15f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        levelHeaderText = CreateLabel(banner, "Header", "NIVEAU 1 / 4 | ZOMBIES : 0 / 3", 24f, font);
        levelHeaderText.alignment = TextAlignmentOptions.Center;
        levelHeaderText.fontStyle = FontStyles.Bold;
        levelHeaderText.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        levelHeaderText.rectTransform.anchorMax = new Vector2(1f, 1f);
        levelHeaderText.rectTransform.offsetMin = new Vector2(12f, 0f);
        levelHeaderText.rectTransform.offsetMax = new Vector2(-12f, -4f);

        levelSubText = CreateLabel(banner, "Sub", "", 16f, font);
        levelSubText.alignment = TextAlignmentOptions.Center;
        levelSubText.color = new Color(0.82f, 0.84f, 0.88f, 1f);
        levelSubText.rectTransform.anchorMin = new Vector2(0f, 0f);
        levelSubText.rectTransform.anchorMax = new Vector2(1f, 0.48f);
        levelSubText.rectTransform.offsetMin = new Vector2(10f, 4f);
        levelSubText.rectTransform.offsetMax = new Vector2(-10f, 0f);

        // 2. Transition Overlay (when wave cleared -> going to Enigme)
        var transRect = CreateRect("TransitionOverlay", hudCanvasObj.transform);
        Stretch(transRect);
        transitionOverlayObj = transRect.gameObject;
        var transBg = transitionOverlayObj.AddComponent<Image>();
        transBg.color = new Color(0.03f, 0.01f, 0.02f, 0.92f);

        transitionTitleText = CreateLabel(transRect, "TransTitle", "NIVEAU TERMINÉ !", 58f, font);
        transitionTitleText.fontStyle = FontStyles.Bold;
        transitionTitleText.color = new Color(1f, 0.82f, 0.25f, 1f);
        transitionTitleText.rectTransform.anchorMin = transitionTitleText.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
        transitionTitleText.rectTransform.sizeDelta = new Vector2(1200f, 90f);

        transitionDescText = CreateLabel(transRect, "TransDesc", "", 32f, font);
        transitionDescText.color = Color.white;
        transitionDescText.rectTransform.anchorMin = transitionDescText.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
        transitionDescText.rectTransform.sizeDelta = new Vector2(1300f, 220f);

        transitionOverlayObj.SetActive(false);

        // 3. Final Victory Overlay (Écran de Victoire Totale Stylisé & Animé)
        var vicRect = CreateRect("VictoryOverlay", hudCanvasObj.transform);
        Stretch(vicRect);
        victoryOverlayObj = vicRect.gameObject;
        victoryCanvasGroup = victoryOverlayObj.AddComponent<CanvasGroup>();

        var vicBg = victoryOverlayObj.AddComponent<Image>();
        vicBg.color = new Color(0.015f, 0.025f, 0.035f, 0.97f);
        vicBg.raycastTarget = true;

        // Carte centrale avec cadre doré
        victoryCardRect = CreateRect("VictoryCard", vicRect);
        victoryCardRect.anchorMin = victoryCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        victoryCardRect.pivot = new Vector2(0.5f, 0.5f);
        victoryCardRect.sizeDelta = new Vector2(1360f, 740f);

        var cardBg = victoryCardRect.gameObject.AddComponent<Image>();
        cardBg.color = new Color(0.045f, 0.06f, 0.08f, 0.92f);
        cardBg.raycastTarget = false;

        var cardOutline = victoryCardRect.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(1f, 0.82f, 0.25f, 0.85f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // Étoiles et bandeau haut
        var vicStars = CreateLabel(victoryCardRect, "VicStars", "★  ★  ★   MISSION ACCOMPLIE - MONDE SAUVÉ   ★  ★  ★", 23f, font);
        vicStars.color = new Color(1f, 0.85f, 0.25f, 1f);
        vicStars.fontStyle = FontStyles.Bold;
        vicStars.characterSpacing = 6f;
        vicStars.rectTransform.anchorMin = vicStars.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        vicStars.rectTransform.anchoredPosition = new Vector2(0f, 290f);
        vicStars.rectTransform.sizeDelta = new Vector2(1200f, 40f);

        // Grand Titre en Or Brillant
        var vicTitle = CreateLabel(victoryCardRect, "VicTitle", "VICTOIRE TOTALE !", 74f, font);
        vicTitle.color = new Color(1f, 0.92f, 0.38f, 1f);
        vicTitle.fontStyle = FontStyles.Bold;
        vicTitle.characterSpacing = 4f;
        vicTitle.rectTransform.anchorMin = vicTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        vicTitle.rectTransform.anchoredPosition = new Vector2(0f, 225f);
        vicTitle.rectTransform.sizeDelta = new Vector2(1200f, 95f);

        // Ligne dorée sous le titre
        var goldRule = CreateRect("GoldRule", victoryCardRect);
        goldRule.anchorMin = goldRule.anchorMax = new Vector2(0.5f, 0.5f);
        goldRule.anchoredPosition = new Vector2(0f, 168f);
        goldRule.sizeDelta = new Vector2(480f, 3f);
        var ruleImg = goldRule.gameObject.AddComponent<Image>();
        ruleImg.color = new Color(1f, 0.85f, 0.25f, 0.95f);
        ruleImg.raycastTarget = false;

        // Sous-titre
        var vicSub = CreateLabel(victoryCardRect, "VicSub", "Toutes les vagues de zombies ont été purgées et les énigmes résolues !", 22f, font);
        vicSub.color = new Color(0.85f, 0.90f, 0.95f, 1f);
        vicSub.rectTransform.anchorMin = vicSub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        vicSub.rectTransform.anchoredPosition = new Vector2(0f, 130f);
        vicSub.rectTransform.sizeDelta = new Vector2(1200f, 38f);

        // Grille de 4 Badges de Statistiques (Style RPG AAA)
        string nomJoueur = "Chasseur AR";
        if (VisageAuthSession.CurrentUser != null && !string.IsNullOrWhiteSpace(VisageAuthSession.CurrentUser.username))
            nomJoueur = VisageAuthSession.CurrentUser.username;

        CreateStatBadge(victoryCardRect, "BadgeJoueur", "CHASSEUR", nomJoueur, new Vector2(-310f, 25f), font);
        CreateStatBadge(victoryCardRect, "BadgeKills", "ZOMBIES ÉLIMINÉS", "15 / 15 (4 NIVEAUX)", new Vector2(310f, 25f), font);
        CreateStatBadge(victoryCardRect, "BadgeEnigmes", "ÉNIGMES RÉSOLUES", "3 / 3 SCEAUX BRISÉS", new Vector2(-310f, -85f), font);
        CreateStatBadge(victoryCardRect, "BadgeRang", "RANG DE MISSION", "RANG S+ (LÉGENDAIRE)", new Vector2(310f, -85f), font, new Color(1f, 0.85f, 0.25f, 1f));

        // Boutons d'Action (Rejouer / Déconnexion)
        replayBtnRect = CreateVictoryButton(
            victoryCardRect,
            "Btn_VictoryReplay",
            "REJOUER (NIVEAU 1)",
            new Vector2(-230f, -245f),
            new Color(0.12f, 0.65f, 0.32f, 1f),
            new Color(0.35f, 1f, 0.55f, 1f),
            font,
            OnVictoryReplayClicked);

        logoutBtnRect = CreateVictoryButton(
            victoryCardRect,
            "Btn_VictoryLogout",
            "DÉCONNEXION",
            new Vector2(230f, -245f),
            new Color(0.72f, 0.16f, 0.16f, 1f),
            new Color(1f, 0.4f, 0.4f, 0.85f),
            font,
            OnVictoryLogoutClicked);

        victoryOverlayObj.SetActive(false);
    }

    private void CreateStatBadge(Transform parent, string name, string label, string val, Vector2 pos, TMP_FontAsset font, Color? valColor = null)
    {
        var rt = CreateRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(580f, 90f);

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
        bg.raycastTarget = false;

        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(1f, 0.82f, 0.25f, 0.35f);
        o.effectDistance = new Vector2(1.5f, -1.5f);

        var lbl = CreateLabel(rt, "BadgeLabel", label, 18f, font);
        lbl.color = new Color(0.65f, 0.72f, 0.82f, 1f);
        lbl.fontStyle = FontStyles.Bold;
        lbl.characterSpacing = 3f;
        lbl.rectTransform.anchorMin = new Vector2(0f, 0.52f);
        lbl.rectTransform.anchorMax = new Vector2(1f, 0.95f);
        lbl.rectTransform.offsetMin = new Vector2(12f, 0f);
        lbl.rectTransform.offsetMax = new Vector2(-12f, 0f);

        var v = CreateLabel(rt, "BadgeVal", val, 26f, font);
        v.color = valColor.HasValue ? valColor.Value : Color.white;
        v.fontStyle = FontStyles.Bold;
        v.rectTransform.anchorMin = new Vector2(0f, 0.05f);
        v.rectTransform.anchorMax = new Vector2(1f, 0.52f);
        v.rectTransform.offsetMin = new Vector2(12f, 0f);
        v.rectTransform.offsetMax = new Vector2(-12f, 0f);
    }

    private RectTransform CreateVictoryButton(
        Transform parent,
        string name,
        string caption,
        Vector2 anchoredPos,
        Color bgColor,
        Color outlineColor,
        TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClick)
    {
        var rt = CreateRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(400f, 74f);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = outlineColor;
        o.effectDistance = new Vector2(2f, -2f);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        ARMobileCombatControls.RegisterHitRect(rt);

        var lbl = CreateLabel(rt, "Label", caption, 26f, font);
        lbl.fontStyle = FontStyles.Bold;
        Stretch(lbl.rectTransform);

        return rt;
    }

    private void OnVictoryReplayClicked()
    {
        Time.timeScale = 1f;
        ResetToLevelOne();
        SceneManager.LoadScene("ARGameplay");
    }

    private void OnVictoryLogoutClicked()
    {
        Time.timeScale = 1f;
        ResetToLevelOne();
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;

        VisageAuthSession.ClearSession();
        SceneManager.LoadScene("VisageID_Auth_Scene");
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, TMP_FontAsset font)
    {
        var rt = CreateRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }
}
