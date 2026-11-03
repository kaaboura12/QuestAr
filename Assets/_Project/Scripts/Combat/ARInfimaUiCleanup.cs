using System.Collections.Generic;
using InfimaGames.LowPolyShooterPack.Interface;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hides Infima demo-only HUD widgets (timescale, cursor lock, TAB tutorial) in AR
/// and anchors the Weapon & Ammo HUD cleanly at the top-left below the health bar.
/// </summary>
public static class ARInfimaUiCleanup
{
    static readonly string[] DemoObjectNames =
    {
        "Text Timescale",
        "Text Cursor Lock",
        "Text Tutorial",
        "Text Tutorial Prompt",
        "Text Tutorial Text",
        "Text Version Text",
        "Crosshair",
    };

    // Places Weapon & Ammo at the top-left right below the ARZombieVitals health plate without overlap.
    static readonly Vector2 TopLeftWeaponAmmoPosition = new Vector2(765f, -55f);

    public static void ApplyToCanvas(GameObject canvasRoot)
    {
        if (canvasRoot == null)
            return;

        DisableInChildren<TextTimescale>(canvasRoot);
        DisableInChildren<TextTutorial>(canvasRoot);
        DisableInChildren<TextMouseLock>(canvasRoot);

        Transform root = canvasRoot.transform;
        for (int i = 0; i < DemoObjectNames.Length; i++)
            DisableByName(root, DemoObjectNames[i]);

        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        PositionWeaponAndAmmoTopLeft(root);

        foreach (Graphic graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    static void PositionWeaponAndAmmoTopLeft(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Weapon & Ammo")
                continue;

            if (child is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(100f, 100f);
                rect.anchoredPosition = TopLeftWeaponAmmoPosition;
                rect.localScale = Vector3.one;
            }
            break;
        }
    }

    public static void ApplyToAllInfimaCanvasesInScene()
    {
        foreach (GameObject canvas in FindAllInfimaUiCanvases())
            ApplyToCanvas(canvas);
    }

    /// <summary>
    /// FP character prefab spawns a duplicate via CanvasSpawner; remove (Clone) instances.
    /// </summary>
    public static void DestroyClonedInfimaCanvases()
    {
        foreach (GameObject canvas in FindAllInfimaUiCanvases())
        {
            if (canvas.name.Contains("Clone"))
                Object.Destroy(canvas);
        }
    }

    public static void KeepSingleInfimaHud()
    {
        List<GameObject> canvases = FindAllInfimaUiCanvases();
        if (canvases.Count <= 1)
            return;

        GameObject keep = canvases[0];
        for (int i = 0; i < canvases.Count; i++)
        {
            if (!canvases[i].name.Contains("Clone"))
            {
                keep = canvases[i];
                break;
            }
        }

        for (int i = 0; i < canvases.Count; i++)
        {
            if (canvases[i] != keep)
                Object.Destroy(canvases[i]);
        }
    }

    public static List<GameObject> FindAllInfimaUiCanvases()
    {
        var results = new List<GameObject>();
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.name.StartsWith("P_LPSP_UI"))
                results.Add(canvas.gameObject);
        }

        return results;
    }

    static void DisableByName(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                child.gameObject.SetActive(false);
        }
    }

    static void DisableInChildren<T>(GameObject root) where T : Behaviour
    {
        foreach (T behaviour in root.GetComponentsInChildren<T>(true))
            behaviour.gameObject.SetActive(false);
    }
}
