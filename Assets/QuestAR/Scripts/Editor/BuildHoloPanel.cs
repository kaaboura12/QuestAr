using UnityEngine;
using UnityEditor;
using TMPro;

// Outil éditeur : construit le prefab HoloPuzzlePanel (menu QuestAR > Construire HoloPuzzlePanel)
public static class BuildHoloPanel
{
    static Shader sh;

    static Material Mat(string nom, Color c)
    {
        Material m = new Material(sh);
        m.color = c;
        AssetDatabase.CreateAsset(m, "Assets/QuestAR/Prefabs/" + nom + ".mat");
        return m;
    }

    static GameObject Cube(string nom, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nom;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<BoxCollider>());
        return g;
    }

    [MenuItem("QuestAR/Construire HoloPuzzlePanel")]
    public static void Construire()
    {
        sh = Shader.Find("Sprites/Default"); // simple, transparent, fonctionne sur Android
        Color cyan = new Color(0f, 0.784f, 1f);
        Material matPanneau = Mat("HoloPanneau", new Color(0f, 0.784f, 1f, 0.25f));
        Material matContour = Mat("HoloContour", new Color(0.4f, 0.95f, 1f, 1f));
        Material matCadenas = Mat("HoloCadenas", new Color(0f, 0.784f, 1f, 0.95f));

        GameObject root = new GameObject("HoloPuzzlePanel");
        GameObject visuel = new GameObject("Visuel");
        visuel.transform.SetParent(root.transform, false);
        visuel.transform.localPosition = new Vector3(0f, 0.6f, 0f); // 60 cm au-dessus du sol

        float L = 0.5f, H = 0.7f, e = 0.012f;
        Transform v = visuel.transform;
        Cube("Fond", v, Vector3.zero, new Vector3(L, H, 0.01f), matPanneau, true);
        Cube("ContourHaut", v, new Vector3(0f, H / 2f, 0f), new Vector3(L + e, e, 0.015f), matContour, false);
        Cube("ContourBas", v, new Vector3(0f, -H / 2f, 0f), new Vector3(L + e, e, 0.015f), matContour, false);
        Cube("ContourGauche", v, new Vector3(-L / 2f, 0f, 0f), new Vector3(e, H + e, 0.015f), matContour, false);
        Cube("ContourDroit", v, new Vector3(L / 2f, 0f, 0f), new Vector3(e, H + e, 0.015f), matContour, false);

        // Cadenas : corps + anse en U (vue depuis -Z, côté caméra)
        GameObject cad = new GameObject("Cadenas");
        cad.transform.SetParent(v, false);
        cad.transform.localPosition = new Vector3(0f, -0.05f, 0.02f);
        Cube("Corps", cad.transform, Vector3.zero, new Vector3(0.2f, 0.16f, 0.04f), matCadenas, false);
        Cube("AnseGauche", cad.transform, new Vector3(-0.06f, 0.14f, 0f), new Vector3(0.03f, 0.12f, 0.03f), matCadenas, false);
        Cube("AnseDroite", cad.transform, new Vector3(0.06f, 0.14f, 0f), new Vector3(0.03f, 0.12f, 0.03f), matCadenas, false);
        Cube("AnseHaut", cad.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.15f, 0.03f, 0.03f), matCadenas, false);
        Cube("Trou", cad.transform, new Vector3(0f, 0f, -0.025f), new Vector3(0.03f, 0.05f, 0.01f), matContour, false);

        // Titre au-dessus du panneau
        GameObject t = new GameObject("Titre");
        t.transform.SetParent(v, false);
        t.transform.localPosition = new Vector3(0f, H / 2f + 0.08f, 0f);
        TextMeshPro tmp = t.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = cyan;
        tmp.rectTransform.sizeDelta = new Vector2(0.9f, 0.15f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.2f;
        tmp.fontSizeMax = 0.8f;
        tmp.text = "Le cadenas du musée";

        HoloPuzzlePanel script = root.AddComponent<HoloPuzzlePanel>();
        script.enigme = AssetDatabase.LoadAssetAtPath<PuzzleData>("Assets/QuestAR/Data/Enigme_Musee.asset");
        script.titreTexte = tmp;
        script.visuel = v;

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/QuestAR/Prefabs/HoloPuzzlePanel.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("HoloPuzzlePanel construit");
    }
}
