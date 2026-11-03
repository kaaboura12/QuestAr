using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// 7 reliques (4 vraies + 3 leurres) autour du joueur, chacune avec un chiffre qui flotte au-dessus.
// Le joueur "lit" une relique en la regardant au centre de l'écran pendant 1 seconde.
// À la résolution : tout disparaît et une grande caisse (récompense) apparaît devant le panneau.
// Ajouté automatiquement par HoloPuzzlePanel quand le panneau est posé.
public class IndicesFlottants : MonoBehaviour
{
    const float DistanceMin = 1.5f;   // distance au joueur (mètres)
    const float DistanceMax = 2.5f;
    // Graine de placement : 0 = différente à chaque partie ; mets un nombre (ex. 42) pour rejouer le même placement en test
    public static int graineTest = 0;
    const float LargeurCaisse = 0.25f;
    const float LargeurRecompense = 0.4f;
    const float DureeRegard = 0.8f;   // secondes à regarder pour lire une relique
    const float AngleToucher = 7f;    // tolérance du toucher direct (degrés)
    const float AngleMax = 8f;        // tolérance du "centre de l'écran" (degrés)
    // Thème vampire : rouge sang, lueur rouge vif, blanc os, noir
    static readonly Color Sang = new Color(0.690f, 0f, 0.125f);        // #B00020
    static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);       // #FF1030
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);      // #EDE6DA
    static readonly Color RougeSombre = new Color(0.35f, 0f, 0.06f);   // chiffres des leurres
    static readonly Color Noir = new Color(0.039f, 0.039f, 0.039f, 0.85f); // #0A0A0A

    class Indice
    {
        public Transform racine;      // le chiffre + son numéro d'ordre
        public GameObject caisse;
        public TextMeshPro chiffre;
        public Vector3 depart;
        public float temps;
        public bool trouve;
        public bool leurre;           // vrai pour les caisses pièges
        public RelicData data;        // la relique (texte, rang...)
        public Vector3 caissePos;     // position de la caisse (pour le tremblement)
        public float tremble;         // temps de tremblement restant
    }

    Indice[] indices;                   // 7 reliques (4 vraies + 3 leurres)
    CodePuzzle puzzle;                  // clavier + logique (EnigmeLogic)
    VisionVampirique vision;            // bouton VISION, runes, traces, lignes
    float progressionVisee;             // 0 à 1 : anneau de visée autour du réticule

    // --- Accès pour la Vision (lecture seule) ---
    public event System.Action<int> ReliqueLue;                       // index de la relique qui vient d'être lue
    public float SolY { get { return panneau != null ? panneau.position.y : 0f; } }
    public float ProgressionVisee { get { return progressionVisee; } }
    public int NbReliques { get { return indices == null ? 0 : indices.Length; } }
    public bool EstLue(int i) { return indices[i].trouve; }
    public RelicData Data(int i) { return indices[i].data; }
    public Transform Racine(int i) { return indices[i].racine; }
    TextMeshProUGUI compteur;
    GameObject canvasGo;
    Camera cam;
    Transform panneau;
    GameObject caissePrefab;
    int nbTrouves = 0;
    bool fini = false;
    GameObject recompense, boutonCanvas;   // récompense affichée après la résolution
    Transform anneau1, anneau2, plan;

    // Appelé par le panneau : l'énigme (avec ses 7 reliques), le panneau (sa hauteur = le sol détecté), le prefab Crate
    public void Init(PuzzleData enigme, Transform panneauPose, GameObject prefabCaisse)
    {
        cam = Camera.main;
        panneau = panneauPose;
        caissePrefab = prefabCaisse;
        puzzle = FindAnyObjectByType<CodePuzzle>();
        if (cam == null || enigme == null || enigme.reliques == null || enigme.reliques.Length == 0) return;

        RelicData[] liste = enigme.reliques;
        int n = liste.Length;
        indices = new Indice[n];
        float sol = panneau.position.y; // le panneau est posé sur le sol détecté

        // Les reliques sont connues de la logique (essais, contradictions)
        if (puzzle != null)
        {
            foreach (RelicData r in liste)
                puzzle.Logic.AjouterRelique(new InfoRelique { id = r.id, chiffre = r.chiffre, rang = r.rang, authentique = r.authentique, rangsContredits = r.rangsContredits });
            puzzle.Etat.Aller(EtatEnigme.Exploration);
        }

        // Placement mélangé sur 360° (graine par session, reproductible avec graineTest)
        System.Random hasard = new System.Random(graineTest != 0 ? graineTest : System.Environment.TickCount);
        int[] ordre = new int[n];
        for (int essai = 0; essai < 50; essai++)
        {
            for (int i = 0; i < n; i++) ordre[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = hasard.Next(i + 1);
                int tmp = ordre[i]; ordre[i] = ordre[j]; ordre[j] = tmp;
            }
            if (!OrdreChronologique(liste, ordre)) break;
        }

        Vector3 avant = cam.transform.forward; avant.y = 0f; avant.Normalize();
        float pas = 360f / n;
        for (int i = 0; i < n; i++)
        {
            RelicData r = liste[ordre[i]];
            float angle = i * pas + (float)(hasard.NextDouble() - 0.5) * pas * 0.4f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * avant;
            float dist = DistanceMin + (float)hasard.NextDouble() * (DistanceMax - DistanceMin);
            // Toutes les reliques ont la MÊME apparence : un leurre ne se reconnaît qu'à son texte
            indices[i] = CreerIndice("Relique_" + r.id, dir, dist, sol, r.chiffre.ToString(), Sang, r);
        }

        CreerCompteur();
        vision = gameObject.AddComponent<VisionVampirique>();
        vision.Init(this, puzzle, cam);
    }

    // Vrai si les reliques authentiques se suivent dans l'ordre du temps autour du joueur (à éviter)
    static bool OrdreChronologique(RelicData[] liste, int[] ordre)
    {
        int montees = 0, descentes = 0, nb = 0, premier = 0, prec = 0;
        foreach (int k in ordre)
        {
            if (!liste[k].authentique) continue;
            int rang = liste[k].rang;
            if (nb == 0) premier = rang;
            else if (rang > prec) montees++; else descentes++;
            prec = rang; nb++;
        }
        if (nb > 1) { if (premier > prec) descentes++; else montees++; }   // on referme le cercle
        return nb > 1 && (montees <= 1 || descentes <= 1);
    }

    // Crée une caisse + son chiffre flottant dans la direction donnée
    Indice CreerIndice(string nom, Vector3 dir, float distance, float sol, string chiffre, Color couleur, RelicData data)
    {
        Indice ind = new Indice();
        ind.data = data;
        ind.leurre = !data.authentique;
        Vector3 posSol = cam.transform.position + dir * distance;
        posSol.y = sol;

        // Caisse posée au sol
        float hauteur = 0.2f;
        if (caissePrefab != null)
        {
            ind.caisse = CreerCaisse(posSol, LargeurCaisse, Quaternion.identity, out hauteur);
            ind.caisse.name = nom;
            ind.caissePos = ind.caisse.transform.position;
        }

        // Chiffre holographique au-dessus de la caisse
        GameObject go = new GameObject("Indice_" + nom);
        go.transform.SetParent(transform, false);
        ind.racine = go.transform;
        ind.depart = posSol + Vector3.up * (hauteur + 0.2f);
        go.transform.position = ind.depart;

        ind.chiffre = CreerTexte("Chiffre", go.transform, chiffre, 0.35f, Vector3.zero, couleur);
        return ind;
    }

    // Crée une caisse de la largeur voulue, posée sur le sol à "posSol". Renvoie aussi sa hauteur.
    GameObject CreerCaisse(Vector3 posSol, float largeur, Quaternion rot, out float hauteur)
    {
        GameObject c = Instantiate(caissePrefab, posSol, rot);
        Renderer[] rends = c.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(c.transform.position, Vector3.zero);
        foreach (Renderer r in rends) b.Encapsulate(r.bounds);

        // Mise à l'échelle pour obtenir la largeur voulue
        float actuelle = Mathf.Max(b.size.x, b.size.z);
        if (actuelle > 0.0001f) c.transform.localScale *= largeur / actuelle;

        // Pose le bas de la caisse exactement sur le sol
        b = new Bounds(c.transform.position, Vector3.zero);
        foreach (Renderer r in rends) b.Encapsulate(r.bounds);
        c.transform.position += Vector3.up * (posSol.y - b.min.y);
        hauteur = b.size.y;

        // Teinte rouge sombre sur une COPIE du matériau (le matériau du pack n'est pas modifié)
        Color teinte = new Color(0.45f, 0.08f, 0.1f);
        foreach (Renderer r in rends)
        {
            foreach (Material m in r.materials)   // .materials = copies
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", teinte);
                if (m.HasProperty("_Color")) m.SetColor("_Color", teinte);
            }
        }
        return c;
    }

    TextMeshPro CreerTexte(string nom, Transform parent, string texte, float taille, Vector3 pos, Color couleur)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texte;
        t.fontSize = taille * 10f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = couleur;
        t.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        return t;
    }

    // Texte "Chroniques lues : X/7" en haut de l'écran
    void CreerCompteur()
    {
        canvasGo = new GameObject("CanvasIndices");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject go = new GameObject("Compteur");
        go.transform.SetParent(canvasGo.transform, false);
        compteur = go.AddComponent<TextMeshProUGUI>();
        compteur.fontSize = 48;
        compteur.alignment = TextAlignmentOptions.Center;
        compteur.color = Os;
        compteur.raycastTarget = false; // ne bloque pas les clics
        RectTransform rt = compteur.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta = new Vector2(0f, 80f);
        MettreAJourCompteur();
    }

    void MettreAJourCompteur()
    {
        if (compteur != null) { compteur.text = TextesEnigme.Compteur(nbTrouves); compteur.fontSize = 48f * ProgressManager.FacteurTexte; }
    }

    void OnEnable()
    {
        if (puzzle == null) puzzle = FindAnyObjectByType<CodePuzzle>();
        if (puzzle != null) puzzle.OnPuzzleSolved.AddListener(Resolu);
    }

    void OnDisable()
    {
        if (puzzle != null) puzzle.OnPuzzleSolved.RemoveListener(Resolu);
    }

    // Énigme résolue : caisses et chiffres disparaissent, puis la récompense apparaît
    void Resolu()
    {
        if (fini) return;
        fini = true;
        BandeauHistoire.Afficher(TextesEnigme.Fin);
        if (canvasGo != null) Destroy(canvasGo);
        if (vision != null) vision.Terminer();
        if (indices != null) foreach (Indice ind in indices)
        {
            if (ind == null) continue;
            Destroy(ind.racine.gameObject);
            if (ind.caisse != null) Destroy(ind.caisse);
        }
        if (caissePrefab != null && panneau != null) StartCoroutine(ApparitionRecompense());
    }

    IEnumerator ApparitionRecompense()
    {
        // 1 m devant le panneau (côté joueur), posée au sol
        Vector3 pos = panneau.position - panneau.forward * 1f;
        pos.y = panneau.position.y;
        GameObject caisse = CreerCaisse(pos, LargeurRecompense, panneau.rotation, out float hauteur);
        caisse.name = "CaisseRecompense";

        // Lumière rouge autour
        GameObject lumiere = new GameObject("LumiereRouge");
        lumiere.transform.SetParent(caisse.transform, true);
        lumiere.transform.position = pos + Vector3.up * (hauteur * 0.5f);
        Light l = lumiere.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = Lueur;
        l.range = 1.5f;
        l.intensity = 2f;

        // Tout est rangé sous un objet "Recompense" (pour tout supprimer d'un coup)
        recompense = new GameObject("Recompense");
        recompense.transform.position = pos;
        caisse.transform.SetParent(recompense.transform, true);
        lumiere.transform.SetParent(recompense.transform, true);
        float milieu = hauteur * 0.5f;

        // Faisceau de lumière vertical (cylindre cyan transparent qui monte du sol)
        GameObject faisceau = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        faisceau.name = "Faisceau";
        Destroy(faisceau.GetComponent<Collider>());
        faisceau.transform.SetParent(recompense.transform, false);
        faisceau.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f); // 3 m de haut
        faisceau.transform.localPosition = Vector3.up * 1.5f;
        faisceau.GetComponent<Renderer>().material = MatCyan(0.18f);

        // Deux anneaux holographiques qui tournent autour de la caisse
        anneau1 = CreerAnneau("Anneau1", 0.45f, new Vector3(0f, milieu, 0f), Vector3.zero);
        anneau2 = CreerAnneau("Anneau2", 0.55f, new Vector3(0f, milieu, 0f), new Vector3(70f, 0f, 0f));

        // Petites particules cyan qui montent
        GameObject pgo = new GameObject("ParticulesMontantes");
        pgo.transform.SetParent(recompense.transform, false);
        ParticleSystem ps = pgo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startColor = Lueur;
        main.startLifetime = 2f;
        main.startSpeed = 0.4f;
        main.startSize = 0.025f;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 20f;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.3f;
        sh.rotation = new Vector3(-90f, 0f, 0f); // vers le haut
        pgo.GetComponent<ParticleSystemRenderer>().material = MatCyan(1f);
        ps.Play();

        // Hologramme "PLAN - MUSÉE" qui flotte et tourne au-dessus de la caisse
        plan = new GameObject("HologrammePlan").transform;
        plan.SetParent(recompense.transform, false);
        plan.localPosition = Vector3.up * (hauteur + 0.35f);
        GameObject fond = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fond.GetComponent<Collider>());
        fond.transform.SetParent(plan, false);
        fond.transform.localScale = new Vector3(0.5f, 0.2f, 1f);
        fond.GetComponent<Renderer>().material = MatCyan(0.35f);
        CreerTextePlan(plan, -0.01f, 0f);   // face avant
        CreerTextePlan(plan, 0.01f, 180f);  // face arrière (lisible des deux côtés)

        // Le bouton RÉCUPÉRER apparaît en bas de l'écran
        CreerBouton();

        // La caisse grandit de 0 à sa taille (1 seconde)
        Vector3 tailleFinale = caisse.transform.localScale;
        Vector3 basePos = caisse.transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t);
            caisse.transform.localScale = tailleFinale * k;
            yield return null;
        }
        caisse.transform.localScale = tailleFinale;
        caisse.transform.position = basePos;
    }

    // Matériau cyan transparent (Sprites/Default = pas de lumière, toujours lumineux)
    Material MatCyan(float alpha)
    {
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.color = new Color(Lueur.r, Lueur.g, Lueur.b, alpha);
        return m;
    }

    // Anneau = cercle tracé avec un LineRenderer, dans un pivot qu'on fait tourner
    Transform CreerAnneau(string nom, float rayon, Vector3 posLocale, Vector3 inclinaison)
    {
        GameObject pivot = new GameObject(nom);
        pivot.transform.SetParent(recompense.transform, false);
        pivot.transform.localPosition = posLocale;
        pivot.transform.localRotation = Quaternion.Euler(inclinaison);

        LineRenderer lr = pivot.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.015f;
        lr.material = MatCyan(0.9f);
        int n = 48;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rayon, 0f, Mathf.Sin(a) * rayon));
        }
        return pivot.transform;
    }

    void CreerTextePlan(Transform parent, float z, float angleY)
    {
        TextMeshPro t = CreerTexte("Texte", parent, TextesEnigme.PlanHologramme, 0.3f, new Vector3(0f, 0f, z), Os);
        t.rectTransform.sizeDelta = new Vector2(0.5f, 0.2f);
        t.transform.localRotation = Quaternion.Euler(0f, angleY, 0f);
    }

    // Bouton UI "RÉCUPÉRER" en bas de l'écran
    void CreerBouton()
    {
        boutonCanvas = new GameObject("CanvasRecuperer");
        Canvas canvas = boutonCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        boutonCanvas.AddComponent<GraphicRaycaster>();

        GameObject go = new GameObject("BoutonRecuperer");
        go.transform.SetParent(boutonCanvas.transform, false);
        Image fond = go.AddComponent<Image>();
        fond.color = Noir;
        Outline contour = go.AddComponent<Outline>();
        contour.effectColor = Lueur;
        Button bouton = go.AddComponent<Button>();
        bouton.onClick.AddListener(Recuperer);
        RectTransform rt = fond.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(500f, 140f);

        GameObject txt = new GameObject("Texte");
        txt.transform.SetParent(go.transform, false);
        TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
        t.text = "RÉCUPÉRER";
        t.fontSize = 56;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Os;
        t.raycastTarget = false;
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
    }

    // Bouton touché : explosion de particules, la caisse disparaît, puis la carte s'affiche
    void Recuperer()
    {
        if (recompense == null) return;
        Destroy(boutonCanvas);

        GameObject fx = new GameObject("EffetDisparition");
        fx.transform.position = recompense.transform.position + Vector3.up * 0.2f;
        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startColor = Lueur;
        main.startLifetime = 1f;
        main.startSpeed = 1f;
        main.startSize = 0.04f;
        main.loop = false;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.2f;
        fx.GetComponent<ParticleSystemRenderer>().material = MatCyan(1f);
        ps.Play();
        Destroy(fx, 2f);

        Destroy(recompense);   // caisse, faisceau, anneaux, hologramme
        recompense = null;

        CarteIndice carte = FindAnyObjectByType<CarteIndice>();
        if (carte != null) carte.Afficher();
    }

    // Relique lue (vraie ou fausse) : elle reste marquée et ne coûte AUCUN essai
    void Lire(int i)
    {
        Indice ind = indices[i];
        if (ind.trouve) return;
        ind.trouve = true;
        ind.chiffre.color = Os;   // blanc os = lue
        if (puzzle != null) puzzle.Logic.MarquerLue(ind.data.id);
        ProgressManager.MarquerLue(ind.data.id);   // sauvegardé
        nbTrouves++;
        MettreAJourCompteur();
        if (ind.leurre)
        {
            BandeauHistoire.Afficher(ind.data.fragment + "\n" + TextesEnigme.Leurre);
            if (!ProgressManager.Data.effetsReduits) ind.tremble = 0.6f;
        }
        else BandeauHistoire.Afficher(ind.data.fragment);
        AudioManager.Jouer(ind.leurre ? "leurre" : "relique_ok");
        AudioManager.Vibrer(40);
        AudioManager.ProgressionScellage(nbTrouves / (float)indices.Length);
        if (ReliqueLue != null) ReliqueLue(i);
    }

    // Au lancement : remet comme "lues" les reliques déjà lues (sauvegarde). Pas de message, pas de son.
    public void RestaurerLues()
    {
        if (indices == null) return;
        for (int i = 0; i < indices.Length; i++)
        {
            Indice ind = indices[i];
            if (ind.trouve || !ProgressManager.EstLue(ind.data.id)) continue;
            ind.trouve = true;
            ind.chiffre.color = Os;
            if (puzzle != null) puzzle.Logic.MarquerLue(ind.data.id);
            nbTrouves++;
            if (ReliqueLue != null) ReliqueLue(i);
        }
        MettreAJourCompteur();
    }

    // Toucher direct : renvoie la relique non lue la plus proche du doigt (null si aucune / si on touche l'interface)
    Indice ReliqueTouchee()
    {
        Vector2 position;
        int idDoigt = -1;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            idDoigt = Touchscreen.current.primaryTouch.touchId.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            position = Mouse.current.position.ReadValue();
        else return null;

        if (EventSystem.current != null && (idDoigt >= 0 ? EventSystem.current.IsPointerOverGameObject(idDoigt) : EventSystem.current.IsPointerOverGameObject()))
            return null;

        Vector3 rayon = cam.ScreenPointToRay(position).direction;
        Indice meilleur = null;
        float min = AngleToucher;
        foreach (Indice ind in indices)
        {
            if (ind == null || ind.trouve) continue;
            Vector3 versRelique = ind.racine.position - Vector3.up * 0.2f - cam.transform.position;
            float a = Vector3.Angle(rayon, versRelique);
            if (a < min) { min = a; meilleur = ind; }
        }
        return meilleur;
    }

    void Update()
    {
        // Récompense affichée : anneaux et hologramme tournent doucement
        if (fini)
        {
            if (recompense != null)
            {
                anneau1.Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self);
                anneau2.Rotate(0f, -30f * Time.deltaTime, 0f, Space.Self);
                plan.Rotate(0f, 30f * Time.deltaTime, 0f, Space.Self);
            }
            return;
        }
        if (cam == null || indices == null) return;

        progressionVisee = 0f;
        Indice touche = ReliqueTouchee();   // toucher direct (alternative au regard)

        foreach (Indice ind in indices)
        {
            if (ind == null) continue;

            // Flotte doucement + se tourne vers la caméra
            Vector3 p = ind.depart;
            p.y += Mathf.Sin(Time.time * 1.5f + ind.depart.x) * 0.03f;
            ind.racine.position = p;
            Vector3 dir = ind.racine.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) ind.racine.rotation = Quaternion.LookRotation(dir);

            // Leurre qui tremble : la caisse bouge au hasard quelques instants
            if (ind.tremble > 0f && ind.caisse != null)
            {
                ind.tremble -= Time.deltaTime;
                ind.caisse.transform.position = ind.caissePos + Random.insideUnitSphere * 0.015f;
                if (ind.tremble <= 0f) ind.caisse.transform.position = ind.caissePos;
            }

            if (ind.trouve) continue;

            // Regarde-t-on cette relique (centre de l'écran) ? Ou l'a-t-on touchée ?
            float angle = Vector3.Angle(cam.transform.forward, dir);
            if (angle < AngleMax) ind.temps += Time.deltaTime;
            else ind.temps = 0f;
            progressionVisee = Mathf.Max(progressionVisee, Mathf.Clamp01(ind.temps / DureeRegard));

            if (ind.temps >= DureeRegard || ind == touche) Lire(System.Array.IndexOf(indices, ind));
        }
    }
}
