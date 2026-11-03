using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Infima HUD (crosshair, ammo) after arms install; strips demo-only widgets.
/// </summary>
[DefaultExecutionOrder(200)]
public class ARInfimaCombatHud : MonoBehaviour
{
    const string UiCanvasPrefabPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Prefabs/Interface/P_LPSP_UI_Canvas.prefab";

    static bool hudSpawned;

    [SerializeField] GameObject uiCanvasPrefab;

    void Awake()
    {
        if (uiCanvasPrefab == null)
        {
            var registry = ARCombatAssetRegistry.Instance;
            if (registry != null && registry.uiCanvasPrefab != null)
                uiCanvasPrefab = registry.uiCanvasPrefab;
        }

#if UNITY_EDITOR
        if (uiCanvasPrefab == null)
        {
            uiCanvasPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                UiCanvasPrefabPath);
        }
#endif
    }

    void Start()
    {
        StartCoroutine(EnsureHudWhenReady());
    }

    IEnumerator EnsureHudWhenReady()
    {
        yield return null;
        yield return null;
        yield return null;

        List<GameObject> canvases = ARInfimaUiCleanup.FindAllInfimaUiCanvases();

        if (canvases.Count == 0 && uiCanvasPrefab != null)
        {
            GameObject hud = Instantiate(uiCanvasPrefab);
            hud.name = "P_LPSP_UI_Canvas";
            hudSpawned = true;
        }

        ARInfimaUiCleanup.DestroyClonedInfimaCanvases();
        ARInfimaUiCleanup.KeepSingleInfimaHud();
        ARInfimaUiCleanup.ApplyToAllInfimaCanvasesInScene();
    }
}
