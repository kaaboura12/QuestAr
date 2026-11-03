using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Panneau holographique : regarde le joueur, flotte doucement, réagit au toucher.
public class HoloPuzzlePanel : MonoBehaviour
{
    [Header("Références (déjà réglées dans le prefab)")]
    public PuzzleData enigme;          // l'énigme à afficher (Enigme_Musee)
    public TextMeshPro titreTexte;     // texte 3D au-dessus du panneau
    public Transform visuel;           // partie qui flotte (panneau + cadenas + titre)
    public GameObject caissePrefab;    // prefab Crate (indices + récompense)

    [Header("Flottement")]
    public float amplitude = 0.03f;    // 3 cm de haut en bas
    public float vitesse = 1.5f;

    CodePuzzle clavier;   // le clavier à code (trouvé automatiquement)
    CarteIndice carte;    // la carte d'indice (trouvée automatiquement)
    bool resolu = false;  // true une fois l'énigme résolue
    float departY;
    Camera cam;
    Light lumiereRouge;   // lumière rouge qui scintille

    void Start()
    {
        cam = Camera.main;
        departY = visuel.localPosition.y;
        if (titreTexte != null) titreTexte.text = TextesEnigme.TitrePanneau;

        // Texte de l'énigme sur le panneau (objet "Enigme" créé par l'outil Histoire Vampire)
        Transform enigmeObj = visuel.Find("Enigme");
        if (enigmeObj != null) enigmeObj.GetComponent<TextMeshPro>().text = TextesEnigme.Enigme;

        BandeauHistoire.Afficher(TextesEnigme.PanneauPose);

        // Fait apparaître les 7 reliques autour du joueur
        if (enigme != null)
        {
            IndicesFlottants indices = new GameObject("IndicesFlottants").AddComponent<IndicesFlottants>();
            indices.Init(enigme, transform, caissePrefab);
            // Le sceau réagit aux reliques, aux erreurs et à la victoire
            gameObject.AddComponent<SceauVisuel>().Init(visuel, indices, FindAnyObjectByType<CodePuzzle>());
            indices.RestaurerLues();   // reliques déjà lues lors d'une partie précédente
        }

        // Petite lumière rouge sur le panneau (elle scintille dans Update)
        GameObject lg = new GameObject("LumiereScintillante");
        lg.transform.SetParent(visuel, false);
        lg.transform.localPosition = new Vector3(0f, 0f, -0.3f);
        lumiereRouge = lg.AddComponent<Light>();
        lumiereRouge.type = LightType.Point;
        lumiereRouge.color = new Color(1f, 0.063f, 0.188f);
        lumiereRouge.range = 1.5f;
    }

    // Écoute l'événement "énigme résolue" du clavier (le panneau est créé en jeu,
    // donc on se branche par le code et pas dans l'Inspector)
    void OnEnable()
    {
        CodePuzzle puzzle = FindAnyObjectByType<CodePuzzle>();
        if (puzzle != null) puzzle.OnPuzzleSolved.AddListener(Deverrouiller);
    }

    void OnDisable()
    {
        CodePuzzle puzzle = FindAnyObjectByType<CodePuzzle>();
        if (puzzle != null) puzzle.OnPuzzleSolved.RemoveListener(Deverrouiller);
    }

    // Énigme résolue : le cadenas s'envole, le panneau brille, des particules apparaissent
    public void Deverrouiller()
    {
        resolu = true;
        StartCoroutine(AnimationDeverrouillage());
    }

    IEnumerator AnimationDeverrouillage()
    {
        Transform cadenas = visuel.Find("Cadenas");
        Renderer[] piecesCadenas = cadenas.GetComponentsInChildren<Renderer>();
        Vector3 depart = cadenas.localPosition;

        // Panneau plus lumineux (.material = copie, on ne touche pas au fichier d'origine)
        foreach (Renderer r in visuel.GetComponentsInChildren<Renderer>())
        {
            if (r.transform.IsChildOf(cadenas) || r is ParticleSystemRenderer) continue;
            // Texte TextMeshPro : on utilise tmp.color (pas material.color, sinon erreur "_Color")
            TextMeshPro tmp = r.GetComponent<TextMeshPro>();
            if (tmp != null) { tmp.color = new Color(0.929f, 0.902f, 0.855f, 1f); continue; }   // blanc os
            r.material.color = r.name == "Fond" ? new Color(0.69f, 0f, 0.125f, 0.55f) : new Color(1f, 0.063f, 0.188f, 1f);
        }
        LancerParticules();

        // Le cadenas monte et disparaît en 1 seconde
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            cadenas.localPosition = depart + Vector3.up * (0.3f * t);
            foreach (Renderer r in piecesCadenas)
            {
                Color c = r.material.color;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                r.material.color = c;
            }
            yield return null;
        }
        cadenas.gameObject.SetActive(false);
    }

    void LancerParticules()
    {
        GameObject go = new GameObject("Particules");
        go.transform.SetParent(visuel, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.startColor = new Color(1f, 0.063f, 0.188f);   // rouge #FF1030
        main.startLifetime = 1.5f;
        main.startSpeed = 0.15f;
        main.startSize = 0.03f;
        main.loop = true;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 25f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 0.7f, 0.05f); // la taille du panneau

        go.GetComponent<ParticleSystemRenderer>().material = new Material(Shader.Find("Sprites/Default"));
        ps.Play();
    }

    void Update()
    {
        // 1) Flottement : monte et descend en douceur
        Vector3 p = visuel.localPosition;
        p.y = departY + Mathf.Sin(Time.time * vitesse) * amplitude;
        visuel.localPosition = p;

        // Scintillement rouge léger (bruit de Perlin = variation douce et irrégulière)
        if (lumiereRouge != null)
            lumiereRouge.intensity = 0.6f + Mathf.PerlinNoise(Time.time * 8f, 0f) * 1.2f;

        if (cam == null) return;

        // 2) Tourne vers la caméra (seulement autour de l'axe vertical, le panneau reste droit)
        Vector3 dir = transform.position - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);

        // 3) Toucher : lance un rayon depuis l'écran et vérifie si on touche ce panneau
        if (ToucheOuClic(out Vector2 pos))
        {
            // Clavier ou carte déjà ouverts ? On ignore le toucher
            if (clavier == null) clavier = FindAnyObjectByType<CodePuzzle>();
            if (carte == null) carte = FindAnyObjectByType<CarteIndice>();
            if (clavier == null || clavier.EstOuvert) return;
            if (carte != null && carte.EstOuverte) return;

            Ray rayon = cam.ScreenPointToRay(pos);
            if (Physics.Raycast(rayon, out RaycastHit hit, 20f) && hit.transform.IsChildOf(transform))
            {
                // Résolu : on rouvre la carte d'indice, sinon on ouvre le clavier
                if (resolu && carte != null) carte.Afficher();
                else if (!resolu) clavier.Ouvrir();
            }
        }
    }

    // Retourne true si l'écran vient d'être touché (ou clic souris dans l'éditeur)
    bool ToucheOuClic(out Vector2 position)
    {
        position = Vector2.zero;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
        return false;
    }
}
