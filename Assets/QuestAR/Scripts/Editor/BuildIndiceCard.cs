using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

// Outil éditeur : construit la carte d'indice (menu QuestAR > Construire CarteIndice)
public static class BuildIndiceCard
{
    static readonly Color cyan = new Color(0f, 0.784f, 1f);

    static RectTransform Rect(string nom, Transform parent, Vector2 pos, Vector2 taille)
    {
        GameObject g = new GameObject(nom, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = taille;
        return r;
    }

    static TextMeshProUGUI Texte(Transform parent, string nom, string txt, float fs, Vector2 pos, Vector2 taille)
    {
        RectTransform r = Rect(nom, parent, pos, taille);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = fs;
        t.color = cyan;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    static Image Boite(string nom, Transform parent, Vector2 pos, Vector2 taille, Color couleur, bool contour)
    {
        RectTransform r = Rect(nom, parent, pos, taille);
        Image img = r.gameObject.AddComponent<Image>();
        img.color = couleur;
        if (contour)
        {
            Outline o = r.gameObject.AddComponent<Outline>();
            o.effectColor = cyan;
            o.effectDistance = new Vector2(3f, -3f);
        }
        return img;
    }

    [MenuItem("QuestAR/Construire CarteIndice")]
    public static void Construire()
    {
        GameObject racine = new GameObject("CarteIndiceUI");
        CarteIndice carte = racine.AddComponent<CarteIndice>();
        carte.enigme = AssetDatabase.LoadAssetAtPath<PuzzleData>("Assets/QuestAR/Data/Enigme_Musee.asset");

        // Canvas plein écran (même réglage que le clavier : GraphicRaycaster + TrackedDeviceGraphicRaycaster)
        GameObject cv = new GameObject("CarteCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        cv.transform.SetParent(racine.transform, false);
        Canvas canvas = cv.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler cs = cv.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = 0.5f;

        // Fond sombre plein écran
        Image fond = Boite("Fond", cv.transform, Vector2.zero, Vector2.zero, new Color(0f, 0.04f, 0.08f, 0.88f), false);
        fond.rectTransform.anchorMin = Vector2.zero;
        fond.rectTransform.anchorMax = Vector2.one;
        fond.rectTransform.offsetMin = fond.rectTransform.offsetMax = Vector2.zero;

        // Carte centrale avec contour cyan
        Transform c = Boite("Carte", cv.transform, Vector2.zero, new Vector2(900f, 800f), new Color(0f, 0.12f, 0.2f, 0.95f), true).transform;

        // Icône cadenas ouvert faite de formes UI (corps + anse décalée)
        Transform icone = Rect("IconeCadenasOuvert", c, new Vector2(-300f, 270f), new Vector2(80f, 90f));
        Boite("Corps", icone, new Vector2(0f, -15f), new Vector2(70f, 50f), cyan, false);
        Boite("AnseGauche", icone, new Vector2(-25f, 25f), new Vector2(10f, 45f), cyan, false);
        Boite("AnseHaut", icone, new Vector2(-5f, 48f), new Vector2(50f, 10f), cyan, false);

        Texte(c, "Titre", "INDICE TROUVÉ", 70f, new Vector2(40f, 270f), new Vector2(620f, 100f));
        carte.texteIndice = Texte(c, "Indice", "", 60f, new Vector2(0f, 60f), new Vector2(780f, 260f));
        carte.texteXP = Texte(c, "XP", "", 100f, new Vector2(0f, -120f), new Vector2(700f, 130f));

        // Bouton Mission suivante (gros bouton)
        Image bImg = Boite("BoutonMission", c, new Vector2(0f, -290f), new Vector2(620f, 150f), new Color(0f, 0.784f, 1f, 0.2f), true);
        Texte(bImg.transform, "Texte", "MISSION SUIVANTE", 56f, Vector2.zero, new Vector2(600f, 130f));
        Button b = bImg.gameObject.AddComponent<Button>();
        b.targetGraphic = bImg;
        UnityEventTools.AddPersistentListener(b.onClick, carte.Fermer);

        carte.canvasCarte = cv;
        cv.SetActive(false); // désactivée au départ

        // Branche la carte sur l'événement OnPuzzleSolved du clavier
        CodePuzzle puzzle = Object.FindFirstObjectByType<CodePuzzle>();
        UnityEventTools.AddPersistentListener(puzzle.OnPuzzleSolved, carte.Afficher);
        EditorUtility.SetDirty(puzzle);

        EditorSceneManager.MarkSceneDirty(racine.scene);
        Debug.Log("CarteIndice construite");
    }
}
