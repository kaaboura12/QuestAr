using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// Outil éditeur : prépare l'histoire de vampires dans la scène (menu QuestAR > Histoire Vampire)
public static class HistoireVampire
{
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);   // blanc os

    [MenuItem("QuestAR/Histoire Vampire")]
    public static void Appliquer()
    {
        // 1) Prefab du panneau : texte de l'énigme sous le titre (le cadenas descend pour lui faire de la place)
        string pp = "Assets/QuestAR/Prefabs/HoloPuzzlePanel.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(pp);
        Transform visuel = prefab.transform.Find("Visuel");
        Transform cadenas = visuel.Find("Cadenas");
        if (cadenas != null) cadenas.localPosition = new Vector3(0f, -0.2f, 0.02f);

        Transform ancien = visuel.Find("Enigme");
        if (ancien != null) Object.DestroyImmediate(ancien.gameObject);
        GameObject e = new GameObject("Enigme");
        e.transform.SetParent(visuel, false);
        e.transform.localPosition = new Vector3(0f, 0.18f, -0.01f);
        TextMeshPro tmp = e.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Os;
        tmp.rectTransform.sizeDelta = new Vector2(0.44f, 0.3f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = 0.5f;
        tmp.text = TextesEnigme.Enigme;
        PrefabUtility.SaveAsPrefabAsset(prefab, pp);
        PrefabUtility.UnloadPrefabContents(prefab);

        // 2) Clavier : texte "Essais : X/3" en bas
        CodePuzzle puzzle = Object.FindFirstObjectByType<CodePuzzle>(FindObjectsInactive.Include);
        if (puzzle != null)
        {
            Transform cv = puzzle.canvasClavier.transform;
            Transform vieux = cv.Find("Essais");
            if (vieux != null) Object.DestroyImmediate(vieux.gameObject);
            GameObject g = new GameObject("Essais", typeof(RectTransform));
            g.transform.SetParent(cv, false);
            TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
            t.text = TextesEnigme.Essais(3);
            t.fontSize = 56;
            t.color = Os;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -660f);
            rt.sizeDelta = new Vector2(900f, 100f);
            puzzle.texteEssais = t;
            EditorUtility.SetDirty(puzzle);
        }
        else Debug.LogWarning("CodePuzzle introuvable");

        // 3) Bandeau d'histoire dans la scène (affiche le message du lancement)
        if (Object.FindFirstObjectByType<BandeauHistoire>() == null)
            new GameObject("BandeauHistoire").AddComponent<BandeauHistoire>();

        // 4) Texte d'aide et carte finale (aperçu dans l'éditeur)
        foreach (TextMeshProUGUI t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "TexteAide") t.text = TextesEnigme.Aide;
            else if (t.transform.root.name == "CarteIndiceUI")
            {
                if (t.name == "Titre") t.text = TextesEnigme.CarteTitre;
                else if (t.transform.parent.name == "BoutonMission") t.text = TextesEnigme.CarteBouton;
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Histoire vampire appliquée");
    }
}
