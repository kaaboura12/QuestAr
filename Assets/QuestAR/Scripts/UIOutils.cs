using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Petits outils pour construire l'interface par le code (même style rouge sang / noir / blanc os).
public static class UIOutils
{
    public static readonly Color Sang = new Color(0.690f, 0f, 0.125f);
    public static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);
    public static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);
    public static readonly Color Noir = new Color(0.039f, 0.039f, 0.039f, 0.92f);

    // Textes créés ici : leur taille suit le réglage "Taille du texte"
    static readonly List<TMP_Text> textes = new List<TMP_Text>();
    static readonly List<float> tailles = new List<float>();

    public static Canvas NouveauCanvas(string nom, int ordre)
    {
        GameObject go = new GameObject(nom);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = ordre;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = MatchSelonOrientation();
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // Paysage : on s'adapte � la hauteur (rien n'est coup� en haut/bas). Portrait : � la largeur.
    public static float MatchSelonOrientation() { return Screen.width >= Screen.height ? 1f : 0f; }

    // Configure un Canvas existant (ex: KeypadCanvas, CarteCanvas) pour qu'il s'adapte au mode Paysage et reçoive les clics
    public static void ConfigurerCanvasOverlay(GameObject canvasGo, int sortingOrder)
    {
        if (canvasGo == null) return;

        Canvas c = canvasGo.GetComponent<Canvas>();
        if (c != null)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
        }

        CanvasScaler cs = canvasGo.GetComponent<CanvasScaler>();
        if (cs == null) cs = canvasGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = MatchSelonOrientation();

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            canvasGo.AddComponent<GraphicRaycaster>();

        // Supprime TrackedDeviceGraphicRaycaster s'il existe car il peut bloquer les touchers 2D sur mobile
        foreach (MonoBehaviour mb in canvasGo.GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().Name.Contains("TrackedDeviceGraphicRaycaster"))
                Object.Destroy(mb);
        }

        // Les textes enfants des boutons ne doivent pas intercepter le raycast à la place de l'Image du bouton
        foreach (TMP_Text t in canvasGo.GetComponentsInChildren<TMP_Text>(true))
            t.raycastTarget = false;
    }

    // Corrige les matériaux roses/magenta (shader incompatible avec le pipeline de rendu actif)
    public static void CorrigerMateriauxURP(GameObject racine, Color teinte)
    {
        if (racine == null) return;
        bool urpActif = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader fallbackLit = null;
        if (urpActif)
        {
            fallbackLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (fallbackLit == null) fallbackLit = Shader.Find("Universal Render Pipeline/Lit");
        }
        else
        {
            // Built-in Render Pipeline : un shader URP s'affiche en ROSE -> on passe en Standard
            fallbackLit = Shader.Find("Standard");
            if (fallbackLit == null) fallbackLit = Shader.Find("Legacy Shaders/Diffuse");
        }
        if (fallbackLit == null) fallbackLit = Shader.Find("Sprites/Default");

        foreach (Renderer r in racine.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r.GetComponent<TextMeshPro>() != null) continue;
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                Texture mainTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                if (mainTex == null && m.HasProperty("_MainTex")) mainTex = m.GetTexture("_MainTex");
                string nomShader = m.shader != null ? m.shader.name : "";
                bool broken = m.shader == null || !m.shader.isSupported ||
                              nomShader.Contains("InternalError") ||
                              (urpActif && nomShader == "Standard") ||
                              (!urpActif && nomShader.StartsWith("Universal Render Pipeline"));
                if (broken && fallbackLit != null)
                    m.shader = fallbackLit;
                m.shaderKeywords = System.Array.Empty<string>();
                if (mainTex != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mainTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", mainTex);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", teinte);
                if (m.HasProperty("_Color")) m.SetColor("_Color", teinte);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
            r.materials = mats;
        }
    }

    // Conteneur qui suit la zone sûre de l'écran (encoche, coins arrondis)
    public static RectTransform ZoneSure(Transform parent)
    {
        GameObject go = new GameObject("ZoneSure", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<ZoneSureAuto>();
        return (RectTransform)go.transform;
    }

    public static Image Rect(string nom, Transform parent, Vector2 pos, Vector2 taille, Color couleur, bool interactif = false)
    {
        GameObject go = new GameObject(nom, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image im = go.AddComponent<Image>();
        im.color = couleur;
        im.raycastTarget = interactif;   // seuls les éléments cliquables reçoivent les touchers
        RectTransform rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = taille;
        return im;
    }

    public static TextMeshProUGUI Texte(Transform parent, string texte, float taille, Vector2 pos, Vector2 zone,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject("Texte", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = texte;
        t.fontSize = taille * ProgressManager.FacteurTexte;
        t.alignment = align;
        t.color = Os;
        t.raycastTarget = false;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = zone;
        textes.Add(t); tailles.Add(taille);
        return t;
    }

    // Bouton rectangulaire avec contour rouge : fond noir + libellé
    public static Button Bouton(Transform parent, string libelle, Vector2 pos, Vector2 taille, UnityAction action, float tailleTexte = 40f)
    {
        Image fond = Rect("Bouton_" + libelle, parent, pos, taille, Noir, true);
        Outline o = fond.gameObject.AddComponent<Outline>();
        o.effectColor = Lueur;
        o.effectDistance = new Vector2(3f, -3f);
        Button b = fond.gameObject.AddComponent<Button>();
        b.targetGraphic = fond;
        b.onClick.AddListener(action);
        TextMeshProUGUI t = Texte(fond.transform, libelle, tailleTexte, Vector2.zero, taille);
        t.fontStyle = FontStyles.Bold;
        return b;
    }

    // Réapplique la taille du texte (réglage "Normale / Grande")
    public static void AppliquerTailleTexte()
    {
        float f = ProgressManager.FacteurTexte;
        for (int i = textes.Count - 1; i >= 0; i--)
        {
            if (textes[i] == null) { textes.RemoveAt(i); tailles.RemoveAt(i); continue; }
            textes[i].fontSize = tailles[i] * f;
        }
    }

    // Les textes ne doivent pas bloquer les touchers (ce sont les Images des boutons qui reçoivent le clic)
    public static void DesactiverRaycastTextes()
    {
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            if (t != null && t.raycastTarget) t.raycastTarget = false;
    }

    // Garantit que l'EventSystem possède un module d'entrée fonctionnel avec le New Input System
    public static void AssurerEventSystemValide()
    {
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null)
            es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        var inputSysModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputSysModule == null)
        {
            // Si un XRUIInputModule mal configuré est présent sans actions, on le remplace par InputSystemUIInputModule
            foreach (MonoBehaviour mb in es.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.Contains("XRUIInputModule"))
                    Object.Destroy(mb);
            }
            inputSysModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            inputSysModule.AssignDefaultActions();
        }
        else if (inputSysModule.point == null || inputSysModule.leftClick == null)
        {
            inputSysModule.AssignDefaultActions();
        }
    }
}

// Ajuste un RectTransform sur Screen.safeArea
public class ZoneSureAuto : MonoBehaviour
{
    Rect dernier;
    void OnEnable() { Appliquer(); }
    void Update() { if (Screen.safeArea != dernier) Appliquer(); }

    void Appliquer()
    {
        dernier = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(dernier.xMin / Screen.width, dernier.yMin / Screen.height);
        rt.anchorMax = new Vector2(dernier.xMax / Screen.width, dernier.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
