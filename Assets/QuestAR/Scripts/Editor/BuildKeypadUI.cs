using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

// Outil éditeur : construit CodeKeypadUI dans la scène (menu QuestAR > Construire CodeKeypadUI)
public static class BuildKeypadUI
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

    static TextMeshProUGUI Texte(Transform parent, string txt, float taille)
    {
        RectTransform r = Rect("Texte", parent, Vector2.zero, Vector2.zero);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = taille;
        t.color = cyan;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    // Case/bouton avec fond cyan transparent + contour
    static Image Boite(string nom, Transform parent, Vector2 pos, Vector2 taille, float alpha)
    {
        RectTransform r = Rect(nom, parent, pos, taille);
        Image img = r.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0.784f, 1f, alpha);
        Outline o = r.gameObject.AddComponent<Outline>();
        o.effectColor = cyan;
        o.effectDistance = new Vector2(3f, -3f);
        return img;
    }

    static Button Bouton(string nom, Transform parent, Vector2 pos, Vector2 taille, string txt, float fs)
    {
        Image img = Boite(nom, parent, pos, taille, 0.2f);
        Texte(img.transform, txt, fs);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        return b;
    }

    [MenuItem("QuestAR/Construire CodeKeypadUI")]
    public static void Construire()
    {
        // EventSystem nécessaire pour que les boutons réagissent au toucher
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameObject racine = new GameObject("CodeKeypadUI");
        CodePuzzle puzzle = racine.AddComponent<CodePuzzle>();
        puzzle.enigme = AssetDatabase.LoadAssetAtPath<PuzzleData>("Assets/QuestAR/Data/Enigme_Musee.asset");

        // Canvas Screen Space Overlay
        GameObject cv = new GameObject("KeypadCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cv.transform.SetParent(racine.transform, false);
        cv.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler cs = cv.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = 0.5f;

        // Fond sombre semi-transparent plein écran
        RectTransform fond = Rect("Fond", cv.transform, Vector2.zero, Vector2.zero);
        fond.anchorMin = Vector2.zero;
        fond.anchorMax = Vector2.one;
        fond.offsetMin = fond.offsetMax = Vector2.zero;
        fond.gameObject.AddComponent<Image>().color = new Color(0f, 0.04f, 0.08f, 0.88f);

        // Titre
        RectTransform titre = Rect("Titre", cv.transform, new Vector2(0f, 760f), new Vector2(900f, 100f));
        TextMeshProUGUI tt = titre.gameObject.AddComponent<TextMeshProUGUI>();
        tt.text = "ENTRE LE CODE";
        tt.fontSize = 64f;
        tt.color = cyan;
        tt.alignment = TextAlignmentOptions.Center;

        // 4 cases de saisie
        puzzle.fondsCases = new Image[4];
        puzzle.textesCases = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            Image c = Boite("Case" + (i + 1), cv.transform, new Vector2(-345f + i * 230f, 560f), new Vector2(200f, 240f), 0.25f);
            puzzle.fondsCases[i] = c;
            puzzle.textesCases[i] = Texte(c.transform, "", 150f);
        }

        // Clavier 0-9 (grille 3 colonnes), gros boutons 280x200
        Vector2 taille = new Vector2(280f, 200f);
        for (int n = 1; n <= 9; n++)
        {
            int col = (n - 1) % 3;
            int ligne = (n - 1) / 3;
            Button b = Bouton("Touche" + n, cv.transform, new Vector2((col - 1) * 310f, 230f - ligne * 230f), taille, n.ToString(), 110f);
            UnityEventTools.AddIntPersistentListener(b.onClick, puzzle.AjouterChiffre, n);
        }
        Button bEff = Bouton("Effacer", cv.transform, new Vector2(-310f, -460f), taille, "EFFACER", 54f);
        UnityEventTools.AddPersistentListener(bEff.onClick, puzzle.Effacer);
        Button b0 = Bouton("Touche0", cv.transform, new Vector2(0f, -460f), taille, "0", 110f);
        UnityEventTools.AddIntPersistentListener(b0.onClick, puzzle.AjouterChiffre, 0);
        Button bVal = Bouton("Valider", cv.transform, new Vector2(310f, -460f), taille, "VALIDER", 54f);
        UnityEventTools.AddPersistentListener(bVal.onClick, puzzle.Valider);

        // Bouton X pour fermer (en haut à droite)
        Button bX = Bouton("Fermer", cv.transform, new Vector2(440f, 830f), new Vector2(150f, 150f), "X", 90f);
        UnityEventTools.AddPersistentListener(bX.onClick, puzzle.Fermer);

        puzzle.canvasClavier = cv;
        cv.SetActive(false); // désactivé au départ

        EditorSceneManager.MarkSceneDirty(racine.scene);
        Debug.Log("CodeKeypadUI construit");
    }
}
