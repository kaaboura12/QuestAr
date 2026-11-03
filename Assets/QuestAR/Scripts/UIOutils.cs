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
        c.sortingOrder = ordre;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
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

    // Les textes qui ne sont pas des boutons ne doivent pas bloquer les touchers
    public static void DesactiverRaycastTextes()
    {
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            if (t.raycastTarget && t.GetComponentInParent<Selectable>() == null) t.raycastTarget = false;
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
