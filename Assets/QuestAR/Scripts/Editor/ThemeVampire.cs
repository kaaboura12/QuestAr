using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;

// Outil éditeur : applique le thème vampire (rouge/noir) à la scène ouverte (menu QuestAR > Thème Vampire)
public static class ThemeVampire
{
    static readonly Color Sang = new Color(0.690f, 0f, 0.125f);          // #B00020
    static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);         // #FF1030
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);        // #EDE6DA
    static readonly Color Noir = new Color(0.039f, 0.039f, 0.039f);      // #0A0A0A

    static void ColorerMateriau(string chemin, Color c)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (m == null) { Debug.LogWarning("Matériau introuvable : " + chemin); return; }
        m.color = c;
        EditorUtility.SetDirty(m);
    }

    // Recolore tous les éléments UI sous une racine
    static void ColorerUI(GameObject racine)
    {
        foreach (TMP_Text t in racine.GetComponentsInChildren<TMP_Text>(true))
            t.color = new Color(Os.r, Os.g, Os.b, t.color.a);
        foreach (Image img in racine.GetComponentsInChildren<Image>(true))
        {
            Color a = img.color;
            bool sombre = Mathf.Max(a.r, a.g, a.b) < 0.3f || a.a > 0.8f;
            Color n = sombre ? Noir : Sang;
            img.color = new Color(n.r, n.g, n.b, a.a);
        }
        foreach (Shadow s in racine.GetComponentsInChildren<Shadow>(true))
            s.effectColor = Lueur;
    }

    [MenuItem("QuestAR/Thème Vampire")]
    public static void Appliquer()
    {
        // 1) Matériaux du panneau et des plans AR (à nous, pas ceux du pack)
        ColorerMateriau("Assets/QuestAR/Prefabs/HoloPanneau.mat", new Color(Sang.r, Sang.g, Sang.b, 0.3f));
        ColorerMateriau("Assets/QuestAR/Prefabs/HoloContour.mat", Lueur);
        ColorerMateriau("Assets/QuestAR/Prefabs/HoloCadenas.mat", new Color(Sang.r, Sang.g, Sang.b, 0.95f));
        ColorerMateriau("Assets/QuestAR/Materials/PlaneCyan.mat", new Color(Sang.r, Sang.g, Sang.b, 0.25f));

        // 2) Prefab du panneau : titre
        string pp = "Assets/QuestAR/Prefabs/HoloPuzzlePanel.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(pp);
        foreach (TMP_Text t in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            t.color = Os;
            if (t.name == "Titre") t.text = "Le sceau du vampire";
        }
        PrefabUtility.SaveAsPrefabAsset(prefab, pp);
        PrefabUtility.UnloadPrefabContents(prefab);

        // 3) Lumière directionnelle sombre et rougeâtre
        Light soleil = Object.FindFirstObjectByType<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) soleil = l;
        if (soleil != null) { soleil.color = new Color(0.85f, 0.2f, 0.18f); soleil.intensity = 0.35f; }

        // 4) UI : clavier, carte, canvas principal
        foreach (string nom in new[] { "CodeKeypadUI", "CarteIndiceUI", "Canvas" })
        {
            GameObject go = GameObject.Find(nom);
            if (go == null) { Debug.LogWarning("Introuvable : " + nom); continue; }
            ColorerUI(go);
        }

        // 5) Vignette noire sur les bords
        CreerVignette();

        // 6) Liste des textes de la scène (pour repérer le texte d'aide)
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Debug.Log("[Thème] " + t.transform.root.name + "/" + t.name + " = " + t.text);
            if (t.name == "TexteAide") t.text = "Cherche le sceau dans l'obscurité";
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Thème vampire appliqué");
    }

    static void CreerVignette()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null || canvas.transform.Find("Vignette") != null) return;

        // Texture radiale : transparente au centre, noire sur les bords
        int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y) / (n - 1), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01((d - 0.55f) / 0.6f) * 0.7f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        Directory.CreateDirectory("Assets/QuestAR/Textures");
        string chemin = "Assets/QuestAR/Textures/Vignette.png";
        File.WriteAllBytes(chemin, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(chemin);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(chemin);
        ti.textureType = TextureImporterType.Sprite;
        ti.SaveAndReimport();

        GameObject v = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        v.transform.SetParent(canvas.transform, false);
        v.transform.SetAsFirstSibling();
        Image img = v.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(chemin);
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
