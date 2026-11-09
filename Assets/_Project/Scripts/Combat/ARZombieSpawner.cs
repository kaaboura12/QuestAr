using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// 1) Désactive l'ancien ObjectSpawner du template AR (qui faisait apparaître des formes roses au toucher).
/// 2) Demande au joueur de scanner le vrai sol horizontal (PlaneAlignment.HorizontalUp uniquement, sous la caméra)
///    pour verrouiller la hauteur exacte du sol (DetectedFloorY) afin que les zombies marchent sur le vrai sol sans flotter.
/// 3) Fait apparaître les zombies à une distance intérieure réaliste (5.2m à 7.8m) selon le quota du Niveau actuel (3 -> 5 -> 7 -> 10).
/// </summary>
[DefaultExecutionOrder(100)]
public class ARZombieSpawner : MonoBehaviour
{
    const string ZombiePrefabFallbackPath = "Assets/Zombie/Prefabs/Zombie3.prefab";
    const float DefaultCameraToFloorHeight = 1.45f;

    public static float DetectedFloorY { get; private set; } = -1.45f;
    public static bool IsFloorCalibrated { get; private set; } = false;

    [Header("Zombie Settings")]
    [SerializeField] GameObject zombiePrefab;
    [SerializeField] int maxSimultaneousZombies = 3;
    [SerializeField] float spawnDistanceMin = 5.2f;
    [SerializeField] float spawnDistanceMax = 7.8f;
    [SerializeField] float respawnInterval = 3.8f;

    private readonly List<HamaZombieAI> activeZombies = new List<HamaZombieAI>();
    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    private float lastRespawnTime = 0f;
    private Transform playerCam;
    private int totalSpawnedInLevel = 0;
    private bool spawningStarted = false;
    private bool lockedRealArFloorPlane = false;
    private float calibratedFloorOffsetFromCam = -DefaultCameraToFloorHeight;

    private ARPlaneManager planeManager;
    private ARRaycastManager raycastManager;
    private GameObject scanOverlayCanvas;
    private TextMeshProUGUI scanStatusText;
    private Image scanProgressFill;
    private RectTransform validateFloorBtnRect;
    private bool manualFloorConfirmRequested = false;
    private Material safeScanPlaneMaterial;

    public int ActiveZombieCount => CleanAndCountZombies();

    private void Awake()
    {
        DisableTemplateObjectSpawners();

        if (zombiePrefab == null)
        {
            var registry = ARCombatAssetRegistry.Instance;
            if (registry != null && registry.zombiePrefab != null)
            {
                zombiePrefab = registry.zombiePrefab;
            }
        }

#if UNITY_EDITOR
        if (zombiePrefab == null)
        {
            zombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabFallbackPath);
        }
#endif
    }

    private void Start()
    {
        totalSpawnedInLevel = 0;
        spawningStarted = false;
        IsFloorCalibrated = false;
        lockedRealArFloorPlane = false;

        planeManager = Object.FindAnyObjectByType<ARPlaneManager>();
        raycastManager = Object.FindAnyObjectByType<ARRaycastManager>();

        FixPinkPlanesInScene();
        StartCoroutine(FloorScanThenSpawnRoutine());
    }

    private void OnDestroy()
    {
        if (validateFloorBtnRect != null)
            ARMobileCombatControls.UnregisterHitRect(validateFloorBtnRect);
        if (scanOverlayCanvas != null)
            Destroy(scanOverlayCanvas);
    }

    /// <summary>
    /// Supprime / désactive l'Object Spawner du template AR (qui faisait apparaître des cubes/pyramides roses au toucher).
    /// </summary>
    private void DisableTemplateObjectSpawners()
    {
        GameObject objSpawner = GameObject.Find("Object Spawner");
        if (objSpawner != null)
        {
            objSpawner.SetActive(false);
        }

        MonoBehaviour[] allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (MonoBehaviour mb in allBehaviours)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (typeName == "ObjectSpawner" || typeName == "ARInteractorSpawnTrigger" || typeName == "ARContactSpawnTrigger")
            {
                mb.enabled = false;
                if (mb.gameObject.name.Contains("Spawner"))
                    mb.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Corrige les matériaux roses (Built-in shader dans URP) sur les plans AR détectés.
    /// </summary>
    private void FixPinkPlanesInScene()
    {
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null && safeScanPlaneMaterial == null)
        {
            safeScanPlaneMaterial = new Material(spriteShader);
            safeScanPlaneMaterial.color = new Color(0.15f, 0.85f, 1f, 0.18f);
        }

        if (planeManager == null) return;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null) continue;
            ApplySafePlaneVisual(plane, !IsFloorCalibrated);
        }
    }

    private void ApplySafePlaneVisual(ARPlane plane, bool showScanVisual)
    {
        if (plane == null) return;

        // Cacher les plans verticaux (murs/portes) ou cacher tous les maillages une fois le sol calibré
        bool isFloorPlane = plane.alignment == PlaneAlignment.HorizontalUp;
        bool visible = showScanVisual && isFloorPlane;

        foreach (Renderer r in plane.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            r.enabled = visible;
            if (visible && safeScanPlaneMaterial != null)
            {
                r.sharedMaterial = safeScanPlaneMaterial;
            }
        }

        LineRenderer lr = plane.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.enabled = visible;
            if (visible && safeScanPlaneMaterial != null)
                lr.sharedMaterial = safeScanPlaneMaterial;
        }
    }

    private bool TryFindLowestHorizontalFloorY(out float floorY)
    {
        FindPlayerCamera();
        float camY = playerCam != null ? playerCam.position.y : 0f;
        floorY = camY - DefaultCameraToFloorHeight;
        bool foundFloor = false;

        // 1. Tester ARRaycastManager vers le bas de l'écran uniquement sur des plans horizontaux (HorizontalUp)
        if (raycastManager != null)
        {
            Vector2 screenPt = new Vector2(Screen.width * 0.5f, Screen.height * 0.30f);
            if (raycastManager.Raycast(screenPt, arHits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
            {
                for (int i = 0; i < arHits.Count; i++)
                {
                    ARPlane hitPlane = planeManager != null ? planeManager.GetPlane(arHits[i].trackableId) : null;
                    if (hitPlane != null && hitPlane.alignment != PlaneAlignment.HorizontalUp)
                        continue;

                    float hitY = arHits[i].pose.position.y;
                    // Ignorer les tables / chaises trop hautes : le vrai sol est au moins 0.70m sous le téléphone
                    if (hitY <= camY - 0.70f)
                    {
                        floorY = hitY;
                        foundFloor = true;
                        break;
                    }
                }
            }
        }

        // 2. Parcourir les plans AR horizontaux (PlaneAlignment.HorizontalUp) pour prendre le plus bas (le vrai sol de la pièce)
        if (planeManager != null && planeManager.trackables.count > 0)
        {
            float lowestY = float.MaxValue;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null || !plane.gameObject.activeInHierarchy)
                    continue;

                ApplySafePlaneVisual(plane, !IsFloorCalibrated);

                if (plane.alignment != PlaneAlignment.HorizontalUp)
                    continue;

                float py = plane.transform.position.y;
                if (py <= camY - 0.70f && py < lowestY)
                {
                    lowestY = py;
                }
            }

            if (lowestY < float.MaxValue)
            {
                floorY = foundFloor ? Mathf.Min(floorY, lowestY) : lowestY;
                foundFloor = true;
            }
        }

        // Sécurité : maintenir le sol dans une plage réaliste sous la caméra (jamais à hauteur des yeux !)
        floorY = Mathf.Clamp(floorY, camY - 1.80f, camY - 1.15f);
        return foundFloor;
    }

    private IEnumerator FloorScanThenSpawnRoutine()
    {
        yield return new WaitForSeconds(0.35f);
        FindPlayerCamera();

        BuildFloorScanOverlay();

        float scanProgress = 0f;
        float candidateFloorY = (playerCam != null ? playerCam.position.y : 0f) - DefaultCameraToFloorHeight;
        bool foundRealPlane = false;

        float requiredScanTime = Application.isEditor ? 1.0f : 1.8f;

        while (scanProgress < 1f && !manualFloorConfirmRequested)
        {
            FindPlayerCamera();

            if (TryFindLowestHorizontalFloorY(out float detectedY))
            {
                candidateFloorY = detectedY;
                foundRealPlane = true;
                scanProgress += Time.deltaTime / requiredScanTime;

                if (scanStatusText != null)
                {
                    scanStatusText.text =
                        $"<color=#55FF77>SOL HORIZONTAL DÉTECTÉ !</color>\nVerrouillage du terrain ({Mathf.Clamp01(scanProgress) * 100f:F0}%)...";
                }
            }
            else if (Application.isEditor)
            {
                scanProgress += Time.deltaTime / requiredScanTime;
                if (scanStatusText != null)
                {
                    scanStatusText.text = $"Calibrage du sol ({Mathf.Clamp01(scanProgress) * 100f:F0}%)...";
                }
            }
            else
            {
                // Tant qu'un vrai sol horizontal n'est pas détecté, on plafonne la barre à 75%
                // pour laisser le joueur pointer le sol ou cliquer sur "SOL PRÊT - LANCER LA VAGUE"
                scanProgress = Mathf.Min(0.75f, scanProgress + Time.deltaTime * 0.15f);
                if (scanStatusText != null)
                {
                    scanStatusText.text =
                        "Inclinez légèrement le téléphone vers le SOL vide devant vous\n(ou appuyez sur le bouton vert ci-dessous pour commencer)";
                }
            }

            if (scanProgressFill != null)
            {
                scanProgressFill.fillAmount = Mathf.Clamp01(scanProgress);
            }

            yield return null;
        }

        FindPlayerCamera();
        float camYNow = playerCam != null ? playerCam.position.y : 0f;
        if (TryFindLowestHorizontalFloorY(out float finalFloorY))
        {
            candidateFloorY = finalFloorY;
            foundRealPlane = true;
        }
        else
        {
            candidateFloorY = camYNow - DefaultCameraToFloorHeight;
        }

        DetectedFloorY = Mathf.Clamp(candidateFloorY, camYNow - 1.80f, camYNow - 1.15f);
        calibratedFloorOffsetFromCam = DetectedFloorY - camYNow;
        lockedRealArFloorPlane = foundRealPlane;
        IsFloorCalibrated = true;

        // Cacher tous les maillages de plans AR une fois le sol verrouillé (aucun polygone rose/cyan à l'écran)
        if (planeManager != null)
        {
            foreach (ARPlane plane in planeManager.trackables)
            {
                ApplySafePlaneVisual(plane, false);
            }
        }

        if (scanOverlayCanvas != null)
        {
            if (validateFloorBtnRect != null)
                ARMobileCombatControls.UnregisterHitRect(validateFloorBtnRect);
            Destroy(scanOverlayCanvas);
            scanOverlayCanvas = null;
        }

        Debug.Log($"[ARZombieSpawner] Sol calibré à Y = {DetectedFloorY:F2}m (CamY = {camYNow:F2}m). Lancement du Niveau {QuestARLevelManager.CurrentLevel} !");

        spawningStarted = true;
        int targetTotal = QuestARLevelManager.TargetKillsForCurrentLevel;
        int initialBatch = Mathf.Min(maxSimultaneousZombies, targetTotal);

        float[] angleOffsets = { -22f, 0f, 22f, -34f };
        for (int i = 0; i < initialBatch; i++)
        {
            if (QuestARLevelManager.IsTransitioning || QuestARLevelManager.IsGameWon || QuestARLevelManager.IsPlayerDead())
                yield break;

            float angle = i < angleOffsets.Length ? angleOffsets[i] : Random.Range(-28f, 28f);
            float dist = Mathf.Lerp(spawnDistanceMin, spawnDistanceMax, (float)i / Mathf.Max(1, initialBatch - 1));
            SpawnSingleZombie(angle, dist);
            yield return new WaitForSeconds(1.35f);
        }

        lastRespawnTime = Time.time;
    }

    private void Update()
    {
        if (planeManager != null && IsFloorCalibrated)
        {
            foreach (ARPlane plane in planeManager.trackables)
            {
                ApplySafePlaneVisual(plane, false);
            }
        }

        // Maintenir DetectedFloorY synchronisé avec le vrai sol sous la caméra
        UpdateContinuousFloorLevel();

        if (!spawningStarted || QuestARLevelManager.IsTransitioning || QuestARLevelManager.IsGameWon || QuestARLevelManager.IsPlayerDead())
            return;

        CleanAndCountZombies();

        int targetTotal = QuestARLevelManager.TargetKillsForCurrentLevel;

        if (totalSpawnedInLevel < targetTotal &&
            activeZombies.Count < maxSimultaneousZombies &&
            Time.time - lastRespawnTime >= respawnInterval)
        {
            lastRespawnTime = Time.time;
            float randomAngle = Random.Range(-28f, 28f);
            float randomDist = Random.Range(spawnDistanceMin, spawnDistanceMax);
            SpawnSingleZombie(randomAngle, randomDist);
        }
    }

    private void UpdateContinuousFloorLevel()
    {
        if (!IsFloorCalibrated) return;

        FindPlayerCamera();
        if (playerCam == null) return;

        if (TryFindLowestHorizontalFloorY(out float trackedFloorY))
        {
            DetectedFloorY = Mathf.Lerp(DetectedFloorY, trackedFloorY, Time.deltaTime * 4f);
            calibratedFloorOffsetFromCam = Mathf.Clamp(DetectedFloorY - playerCam.position.y, -1.80f, -1.15f);
            lockedRealArFloorPlane = true;
        }
        else if (!lockedRealArFloorPlane)
        {
            // Si ARCore n'a pas encore fourni de plan, garder le sol exactement à 1.45m sous la caméra
            DetectedFloorY = playerCam.position.y + calibratedFloorOffsetFromCam;
        }
        else
        {
            // Empêcher le sol de jamais passer à hauteur des yeux si la caméra AR bouge
            DetectedFloorY = Mathf.Clamp(DetectedFloorY, playerCam.position.y - 1.85f, playerCam.position.y - 1.15f);
        }
    }

    public GameObject SpawnSingleZombie(float angleOffsetDeg, float distance)
    {
        if (zombiePrefab == null)
        {
            var registry = ARCombatAssetRegistry.Instance;
            if (registry != null) zombiePrefab = registry.zombiePrefab;
        }

        if (zombiePrefab == null)
        {
            Debug.LogWarning("[ARZombieSpawner] No zombie prefab found!");
            return null;
        }

        FindPlayerCamera();
        if (playerCam == null) return null;

        distance = Mathf.Clamp(distance, spawnDistanceMin, spawnDistanceMax);

        Vector3 camForward = playerCam.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.001f)
            camForward = Vector3.forward;
        else
            camForward.Normalize();

        Vector3 spawnDir = Quaternion.Euler(0f, angleOffsetDeg, 0f) * camForward;
        Vector3 spawnPos = playerCam.position + spawnDir * distance;

        // Poser strictement sur le sol calibré sous la caméra (jamais en l'air)
        float floorY = IsFloorCalibrated
            ? Mathf.Clamp(DetectedFloorY, playerCam.position.y - 1.85f, playerCam.position.y - 1.15f)
            : (playerCam.position.y - DefaultCameraToFloorHeight);
        spawnPos.y = floorY;

        Vector3 lookDir = playerCam.position - spawnPos;
        lookDir.y = 0f;
        Quaternion lookAtPlayer = lookDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
            : Quaternion.identity;

        totalSpawnedInLevel++;
        GameObject instance = Instantiate(zombiePrefab, spawnPos, lookAtPlayer);
        instance.name = $"AR_Zombie_L{QuestARLevelManager.CurrentLevel}_{totalSpawnedInLevel}";
        instance.transform.localScale = Vector3.one * 0.92f;

        HamaZombieAI ai = instance.GetComponent<HamaZombieAI>();
        if (ai == null)
        {
            ai = instance.AddComponent<HamaZombieAI>();
        }

        ai.walkSpeed = Random.Range(0.78f, 0.96f);
        ai.runSpeed = Random.Range(3.8f, 4.4f);
        ai.rotationSpeed = 5.0f;
        ai.attackRange = 1.45f;
        ai.attackDamage = 15.0f;
        ai.attackInterval = 1.35f;
        ai.minSeparationFromOtherZombies = 1.35f;

        activeZombies.Add(ai);
        return instance;
    }

    private void BuildFloorScanOverlay()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        scanOverlayCanvas = new GameObject("AR_FloorScanner_Overlay");
        var canvas = scanOverlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;

        var scaler = scanOverlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        scanOverlayCanvas.AddComponent<GraphicRaycaster>();

        var card = new GameObject("ScanCard", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(scanOverlayCanvas.transform, false);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, 20f);
        card.sizeDelta = new Vector2(880f, 320f);

        var cardBg = card.gameObject.AddComponent<Image>();
        cardBg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        var outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.85f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Titre
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(card, false);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.text = $"NIVEAU {QuestARLevelManager.CurrentLevel} - DÉTECTION DU SOL AR";
        titleTmp.fontSize = 30f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.35f, 0.92f, 1f, 1f);
        titleTmp.rectTransform.anchorMin = new Vector2(0f, 0.72f);
        titleTmp.rectTransform.anchorMax = new Vector2(1f, 0.96f);
        titleTmp.rectTransform.offsetMin = Vector2.zero;
        titleTmp.rectTransform.offsetMax = Vector2.zero;

        // Texte d'instruction
        var statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(card, false);
        scanStatusText = statusGo.AddComponent<TextMeshProUGUI>();
        scanStatusText.font = font;
        scanStatusText.text = "Pointez le téléphone vers un espace vide au sol\npour calibrer le terrain...";
        scanStatusText.fontSize = 22f;
        scanStatusText.alignment = TextAlignmentOptions.Center;
        scanStatusText.color = Color.white;
        scanStatusText.rectTransform.anchorMin = new Vector2(0.05f, 0.38f);
        scanStatusText.rectTransform.anchorMax = new Vector2(0.95f, 0.72f);
        scanStatusText.rectTransform.offsetMin = Vector2.zero;
        scanStatusText.rectTransform.offsetMax = Vector2.zero;

        // Barre de progression
        var barBgGo = new GameObject("BarBg", typeof(RectTransform));
        barBgGo.transform.SetParent(card, false);
        var barBgRt = barBgGo.GetComponent<RectTransform>();
        barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0.5f, 0.30f);
        barBgRt.sizeDelta = new Vector2(680f, 22f);
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.color = new Color(0.18f, 0.2f, 0.25f, 1f);

        var barFillGo = new GameObject("BarFill", typeof(RectTransform));
        barFillGo.transform.SetParent(barBgRt, false);
        var barFillRt = barFillGo.GetComponent<RectTransform>();
        barFillRt.anchorMin = Vector2.zero;
        barFillRt.anchorMax = Vector2.one;
        barFillRt.offsetMin = Vector2.zero;
        barFillRt.offsetMax = Vector2.zero;
        scanProgressFill = barFillGo.AddComponent<Image>();
        scanProgressFill.color = new Color(0.2f, 0.9f, 0.45f, 1f);
        scanProgressFill.type = Image.Type.Filled;
        scanProgressFill.fillMethod = Image.FillMethod.Horizontal;
        scanProgressFill.fillAmount = 0f;

        // Bouton de validation directe du sol
        var btnGo = new GameObject("BtnValidateFloor", typeof(RectTransform));
        btnGo.transform.SetParent(card, false);
        validateFloorBtnRect = btnGo.GetComponent<RectTransform>();
        validateFloorBtnRect.anchorMin = validateFloorBtnRect.anchorMax = new Vector2(0.5f, 0.12f);
        validateFloorBtnRect.sizeDelta = new Vector2(460f, 54f);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.62f, 0.32f, 1f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(() => { manualFloorConfirmRequested = true; });
        ARMobileCombatControls.RegisterHitRect(validateFloorBtnRect);

        var btnTxtGo = new GameObject("Label", typeof(RectTransform));
        btnTxtGo.transform.SetParent(validateFloorBtnRect, false);
        var btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
        btnTxt.font = font;
        btnTxt.text = "SOL PRÊT - LANCER LA VAGUE";
        btnTxt.fontSize = 21f;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;
        btnTxt.raycastTarget = false;
        btnTxt.rectTransform.anchorMin = Vector2.zero;
        btnTxt.rectTransform.anchorMax = Vector2.one;
        btnTxt.rectTransform.offsetMin = Vector2.zero;
        btnTxt.rectTransform.offsetMax = Vector2.zero;
    }

    private int CleanAndCountZombies()
    {
        for (int i = activeZombies.Count - 1; i >= 0; i--)
        {
            if (activeZombies[i] == null || activeZombies[i].isDead)
            {
                activeZombies.RemoveAt(i);
            }
        }
        return activeZombies.Count;
    }

    private void FindPlayerCamera()
    {
        if (playerCam != null) return;

        if (Camera.main != null)
        {
            playerCam = Camera.main.transform;
        }
        else
        {
            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null) playerCam = cam.transform;
        }
    }
}
