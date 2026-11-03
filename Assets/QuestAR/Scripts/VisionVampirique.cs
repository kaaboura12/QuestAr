using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// "Vision vampirique" : bouton VISION maintenu (énergie 6 s, recharge 8 s), réticule avec anneau de visée,
// runes sur les reliques, traces spectrales + chevron vers la relique non lue la plus proche,
// lignes entre reliques lues (dorée continue = cohérentes, rouge pointillée + croix = contradiction), voile rouge.
// Tout est créé par le code (ajouté par IndicesFlottants). Aucun post-traitement, pas d'allocation par image.
public class VisionVampirique : MonoBehaviour
{
    const float EnergieSecondes = 6f;      // durée max d'utilisation
    const float RechargeSecondes = 8f;     // temps pour tout recharger
    const float RayonRunes = 3f;           // les runes apparaissent dans ce rayon
    const int NbTraces = 6;                // fantômes au sol

    static readonly Color Sang = new Color(0.690f, 0f, 0.125f);
    static readonly Color Lueur = new Color(1f, 0.063f, 0.188f);
    static readonly Color Os = new Color(0.929f, 0.902f, 0.855f);
    static readonly Color Noir = new Color(0.039f, 0.039f, 0.039f, 0.85f);
    static readonly Color Or = new Color(1f, 0.82f, 0.25f);

    IndicesFlottants reliques;
    CodePuzzle puzzle;
    Camera cam;
    int n;

    // Interface
    GameObject canvasGo;
    Image voile, anneauVisee, arcEnergie, chevron;
    RectTransform reticule, chevronRt;
    CanvasGroup boutonGroupe;
    TextMeshProUGUI texteBouton;
    int pourcentAffiche = -1;
    bool boutonTenu;
    bool attenteRelache;     // énergie vide : il faut relâcher avant de réessayer
    float energie = 1f;      // 0 à 1
    float intensite;         // 0 à 1 (fondu de la vision)
    bool visionActive;
    static bool chuchotementsAffiches;   // sous-titre affiché une seule fois par partie
    bool boutonDisponible = true;

    // Monde
    Transform conteneur;
    Transform[] runes;
    Material[] matRunes;
    Transform[] traces;
    Material[] matTraces;
    // une ligne par paire de reliques (n*(n-1)/2)
    LineRenderer[] lignes;
    Transform[] croix;
    int[] ligneA, ligneB;
    bool[] ligneVisible;
    Material matOr, matPointille;

    public void Init(IndicesFlottants source, CodePuzzle clavier, Camera camera)
    {
        reliques = source; puzzle = clavier; cam = camera;
        n = reliques.NbReliques;
        conteneur = new GameObject("VisionMonde").transform;
        conteneur.SetParent(transform, false);
        CreerInterface();
        CreerRunes();
        CreerTraces();
        CreerLignes();
        reliques.ReliqueLue += OnReliqueLue;
    }

    // Énigme résolue : tout disparaît
    public void Terminer()
    {
        if (reliques != null) reliques.ReliqueLue -= OnReliqueLue;
        if (canvasGo != null) Destroy(canvasGo);
        if (conteneur != null) Destroy(conteneur.gameObject);
        Destroy(this);
    }

    // ---------- Création ----------

    void CreerInterface()
    {
        canvasGo = new GameObject("CanvasVision");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5;   // sous les autres interfaces (clavier, bandeau, carte)
        CanvasScaler cs = canvasGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        Sprite disque = CercleSprite(128, 0f, 1f);
        Sprite anneau = CercleSprite(128, 0.86f, 1f);

        // Voile rouge sur les bords (image plein écran, pas de post-traitement)
        voile = NouvelleImage("Voile", canvasGo.transform, VoileSprite(), new Color(0.45f, 0f, 0.08f, 0f));
        Etirer(voile.rectTransform);

        // Réticule + anneau de progression de visée
        reticule = NouvelleImage("Reticule", canvasGo.transform, anneau, new Color(Os.r, Os.g, Os.b, 0.7f)).rectTransform;
        reticule.sizeDelta = new Vector2(56f, 56f);
        anneauVisee = NouvelleImage("AnneauVisee", reticule, anneau, Lueur);
        anneauVisee.rectTransform.sizeDelta = new Vector2(96f, 96f);
        anneauVisee.type = Image.Type.Filled;
        anneauVisee.fillMethod = Image.FillMethod.Radial360;
        anneauVisee.fillOrigin = (int)Image.Origin360.Top;
        anneauVisee.fillAmount = 0f;

        // Bouton VISION (rond, 160 px) en bas à droite
        GameObject bouton = new GameObject("BoutonVision");
        bouton.transform.SetParent(UIOutils.ZoneSure(canvasGo.transform), false);   // reste dans la zone sûre de l'écran
        boutonGroupe = bouton.AddComponent<CanvasGroup>();
        Image fond = bouton.AddComponent<Image>();   // cet Image reçoit les touchers
        fond.sprite = disque;
        fond.color = Noir;
        RectTransform rt = fond.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-130f, 260f);
        rt.sizeDelta = new Vector2(160f, 160f);

        EventTrigger et = bouton.AddComponent<EventTrigger>();
        AjouterEvenement(et, EventTriggerType.PointerDown, () => boutonTenu = true);
        AjouterEvenement(et, EventTriggerType.PointerUp, () => boutonTenu = false);

        // Arc d'énergie autour du bouton (en plus du pourcentage : pas de couleur seule)
        arcEnergie = NouvelleImage("ArcEnergie", bouton.transform, anneau, Lueur);
        arcEnergie.rectTransform.sizeDelta = new Vector2(190f, 190f);
        arcEnergie.type = Image.Type.Filled;
        arcEnergie.fillMethod = Image.FillMethod.Radial360;
        arcEnergie.fillOrigin = (int)Image.Origin360.Top;
        arcEnergie.fillAmount = 1f;

        GameObject txt = new GameObject("Texte");
        txt.transform.SetParent(bouton.transform, false);
        texteBouton = txt.AddComponent<TextMeshProUGUI>();
        texteBouton.fontSize = 32;
        texteBouton.fontStyle = FontStyles.Bold;
        texteBouton.alignment = TextAlignmentOptions.Center;
        texteBouton.color = Os;
        texteBouton.raycastTarget = false;
        Etirer(texteBouton.rectTransform);

        // Chevron : indique la direction de la relique hors écran
        chevron = NouvelleImage("Chevron", canvasGo.transform, TriangleSprite(), Lueur);
        chevronRt = chevron.rectTransform;
        chevronRt.sizeDelta = new Vector2(80f, 80f);
        chevron.gameObject.SetActive(false);

        MettreAJourBouton(true);
    }

    Image NouvelleImage(string nom, Transform parent, Sprite sprite, Color couleur)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        Image im = go.AddComponent<Image>();
        im.sprite = sprite;
        im.color = couleur;
        im.raycastTarget = false;   // ne bloque pas les clics
        return im;
    }

    static void Etirer(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void AjouterEvenement(EventTrigger et, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => action());
        et.triggers.Add(e);
    }

    // Une rune par relique : MÊME style pour les vraies et les leurres (dessin tiré de l'id seulement)
    void CreerRunes()
    {
        runes = new Transform[n];
        matRunes = new Material[n];
        for (int i = 0; i < n; i++)
        {
            RelicData d = reliques.Data(i);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Rune_" + d.id;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(reliques.Racine(i), false);
            go.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            go.transform.localScale = Vector3.one * 0.16f;
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = d.runeSprite != null ? d.runeSprite.texture : RuneTexture(d.id);
            m.color = new Color(Lueur.r, Lueur.g, Lueur.b, 0f);
            go.GetComponent<Renderer>().material = m;
            go.SetActive(false);
            runes[i] = go.transform;
            matRunes[i] = m;
        }
    }

    // Fantômes lumineux posés au sol (pool de petits disques plats)
    void CreerTraces()
    {
        traces = new Transform[NbTraces];
        matTraces = new Material[NbTraces];
        Texture2D disque = CercleSprite(64, 0f, 1f).texture;
        for (int i = 0; i < NbTraces; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Trace_" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(conteneur, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // à plat
            go.transform.localScale = new Vector3(0.1f, 0.16f, 1f);
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = disque;
            m.color = new Color(Lueur.r, Lueur.g, Lueur.b, 0f);
            go.GetComponent<Renderer>().material = m;
            go.SetActive(false);
            traces[i] = go.transform;
            matTraces[i] = m;
        }
    }

    // Une ligne (et sa croix) pour chaque paire de reliques, créées une fois
    void CreerLignes()
    {
        int nb = n * (n - 1) / 2;
        lignes = new LineRenderer[nb];
        croix = new Transform[nb];
        ligneA = new int[nb]; ligneB = new int[nb];
        ligneVisible = new bool[nb];

        matOr = new Material(Shader.Find("Sprites/Default")) { color = Or };
        // Pointillé : texture 16x1 (moitié opaque, moitié transparente) répétée le long de la ligne
        Texture2D tex = new Texture2D(16, 1, TextureFormat.RGBA32, false);
        for (int x = 0; x < 16; x++) tex.SetPixel(x, 0, x < 8 ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Point; tex.Apply();
        matPointille = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = Lueur };

        int k = 0;
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++, k++)
            {
                ligneA[k] = a; ligneB[k] = b;
                GameObject go = new GameObject("Lien_" + a + "_" + b);
                go.transform.SetParent(conteneur, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.012f;
                lr.textureMode = LineTextureMode.Tile;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                go.SetActive(false);
                lignes[k] = lr;

                // Croix ✕ en forme (2 barres) au milieu de la ligne
                Transform c = new GameObject("Croix").transform;
                c.SetParent(go.transform, false);
                for (int s = 0; s < 2; s++)
                {
                    GameObject barre = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(barre.GetComponent<Collider>());
                    barre.transform.SetParent(c, false);
                    barre.transform.localRotation = Quaternion.Euler(0f, 0f, s == 0 ? 45f : -45f);
                    barre.transform.localScale = new Vector3(0.14f, 0.025f, 1f);
                    barre.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = Lueur };
                }
                croix[k] = c;
            }
    }

    // Une relique vient d'être lue : on réévalue les lignes (visibles seulement entre reliques lues)
    void OnReliqueLue(int i)
    {
        int nb = lignes.Length;
        for (int k = 0; k < nb; k++)
        {
            bool visible = reliques.EstLue(ligneA[k]) && reliques.EstLue(ligneB[k]);
            ligneVisible[k] = visible;
            if (!visible) continue;
            bool contradiction = puzzle != null && puzzle.Logic.Contradit(reliques.Data(ligneA[k]).id, reliques.Data(ligneB[k]).id);
            lignes[k].sharedMaterial = contradiction ? matPointille : matOr;
            croix[k].gameObject.SetActive(contradiction);
        }
    }

    // ---------- Mise à jour ----------

    void Update()
    {
        if (cam == null) return;
        float dt = Time.deltaTime;

        // Anneau de visée (autour du réticule)
        anneauVisee.fillAmount = reliques.ProgressionVisee;

        // Vision autorisée seulement en exploration (pas pendant la saisie du code, ni verrouillée, ni résolue)
        bool autorise = puzzle == null || puzzle.Etat.Etat == EtatEnigme.Exploration;
        bool demande = boutonTenu || (Keyboard.current != null && Keyboard.current.vKey.isPressed);   // touche V pour tester
        if (!demande) attenteRelache = false;

        bool active = demande && autorise && !attenteRelache && energie > 0f;
        if (active)
        {
            energie -= dt / EnergieSecondes;
            if (energie <= 0f) { energie = 0f; attenteRelache = true; active = false; }
        }
        else energie = Mathf.Min(1f, energie + dt / RechargeSecondes);

        if (active != visionActive)
        {
            AudioManager.Jouer(active ? "vision_on" : "vision_off");
            if (active && !chuchotementsAffiches) { chuchotementsAffiches = true; BandeauHistoire.Afficher(TextesEnigme.SousTitreChuchotements); }
        }
        visionActive = active;
        if (puzzle != null) puzzle.Etat.VisionActive = active;
        intensite = Mathf.MoveTowards(intensite, active ? 1f : 0f, dt * 5f);

        MettreAJourBouton(autorise != boutonDisponible);
        boutonDisponible = autorise;

        // Voile rouge + légère distorsion du réticule
        bool reduit = ProgressManager.Data.effetsReduits;
        float pulsation = reduit ? 0f : 0.08f * Mathf.Sin(Time.time * 2f);   // pulsation du voile (2 par seconde au plus)
        voile.color = new Color(0.45f, 0f, 0.08f, (0.85f + pulsation) * intensite);
        reticule.localScale = Vector3.one * (1f + (reduit ? 0f : 0.12f * intensite * Mathf.Sin(Time.time * 9f)));

        MettreAJourRunes();
        MettreAJourTraces();
        MettreAJourLignes();
    }

    void MettreAJourBouton(bool forcer)
    {
        int pct = Mathf.RoundToInt(energie * 100f);
        if (forcer || pct != pourcentAffiche)
        {
            pourcentAffiche = pct;
            texteBouton.text = "VISION\n" + pct + "%";
            arcEnergie.fillAmount = energie;
        }
        else arcEnergie.fillAmount = energie;
        boutonGroupe.alpha = (puzzle == null || puzzle.Etat.Etat == EtatEnigme.Exploration) ? 1f : 0.35f;
    }

    void MettreAJourRunes()
    {
        Vector3 pc = cam.transform.position;
        for (int i = 0; i < n; i++)
        {
            bool proche = intensite > 0.01f && (reliques.Racine(i).position - pc).sqrMagnitude < RayonRunes * RayonRunes;
            if (runes[i].gameObject.activeSelf != proche) runes[i].gameObject.SetActive(proche);
            if (!proche) continue;
            // rune pleine si la relique est lue, plus pâle sinon
            float a = (reliques.EstLue(i) ? 1f : 0.55f) * intensite;
            matRunes[i].color = new Color(Lueur.r, Lueur.g, Lueur.b, a);
        }
    }

    // Plus proche relique non lue
    int PlusProcheNonLue()
    {
        int meilleur = -1;
        float min = float.MaxValue;
        Vector3 pc = cam.transform.position;
        for (int i = 0; i < n; i++)
        {
            if (reliques.EstLue(i)) continue;
            float d = (reliques.Racine(i).position - pc).sqrMagnitude;
            if (d < min) { min = d; meilleur = i; }
        }
        return meilleur;
    }

    void MettreAJourTraces()
    {
        int cible = intensite > 0.01f ? PlusProcheNonLue() : -1;
        bool montrerTraces = false, montrerChevron = false;

        if (cible >= 0)
        {
            Vector3 pos = reliques.Racine(cible).position;
            Vector3 vp = cam.WorldToViewportPoint(pos);
            bool surEcran = vp.z > 0f && vp.x > 0.08f && vp.x < 0.92f && vp.y > 0.12f && vp.y < 0.88f;

            // Traces : de la caméra (au sol) vers la relique
            Vector3 debut = cam.transform.position; debut.y = reliques.SolY + 0.01f;
            Vector3 fin = pos; fin.y = debut.y;
            Vector3 trajet = fin - debut;
            float longueur = trajet.magnitude;
            if (longueur > 0.9f)
            {
                montrerTraces = true;
                Vector3 dir = trajet / longueur;
                Vector3 cote = Vector3.Cross(Vector3.up, dir);
                float t0 = Time.time * 0.35f;
                for (int i = 0; i < NbTraces; i++)
                {
                    float f = Mathf.Repeat(t0 + (float)i / NbTraces, 1f);          // avance le long du trajet
                    float dist = 0.5f + f * (longueur - 0.7f);
                    Vector3 p = debut + dir * dist + cote * ((i % 2 == 0) ? 0.06f : -0.06f);   // pas gauche / droit
                    traces[i].position = p;
                    traces[i].rotation = Quaternion.LookRotation(Vector3.down, dir);          // à plat, pointe vers la relique
                    float a = Mathf.Sin(f * Mathf.PI) * 0.8f * intensite;
                    matTraces[i].color = new Color(Lueur.r, Lueur.g, Lueur.b, a);
                    if (!traces[i].gameObject.activeSelf) traces[i].gameObject.SetActive(true);
                }
            }

            // Chevron au bord de l'écran si la relique n'est pas visible
            if (!surEcran)
            {
                montrerChevron = true;
                float x = vp.x - 0.5f, y = vp.y - 0.5f;
                if (vp.z < 0f) { x = -x; y = -y; }   // derrière nous : on inverse
                Vector2 d2 = new Vector2(x, y);
                if (d2.sqrMagnitude < 0.0001f) d2 = Vector2.down;
                d2.Normalize();
                chevronRt.anchorMin = chevronRt.anchorMax = new Vector2(0.5f, 0.5f);
                chevronRt.anchoredPosition = new Vector2(d2.x * 440f, d2.y * 780f);
                chevronRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d2.y, d2.x) * Mathf.Rad2Deg - 90f);
                chevron.color = new Color(Lueur.r, Lueur.g, Lueur.b, 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
            }
        }

        if (!montrerTraces)
            for (int i = 0; i < NbTraces; i++)
                if (traces[i].gameObject.activeSelf) traces[i].gameObject.SetActive(false);
        if (chevron.gameObject.activeSelf != montrerChevron) chevron.gameObject.SetActive(montrerChevron);
    }

    void MettreAJourLignes()
    {
        bool voir = intensite > 0.01f;
        for (int k = 0; k < lignes.Length; k++)
        {
            bool afficher = voir && ligneVisible[k];
            GameObject go = lignes[k].gameObject;
            if (go.activeSelf != afficher) go.SetActive(afficher);
            if (!afficher) continue;

            Vector3 a = reliques.Racine(ligneA[k]).position;
            Vector3 b = reliques.Racine(ligneB[k]).position;
            lignes[k].SetPosition(0, a);
            lignes[k].SetPosition(1, b);
            // pointillé : on adapte la répétition de la texture à la longueur
            if (lignes[k].sharedMaterial == matPointille) lignes[k].textureScale = new Vector2(1f / 0.08f, 1f);

            if (croix[k].gameObject.activeSelf)
            {
                croix[k].position = (a + b) * 0.5f;
                croix[k].rotation = cam.transform.rotation;   // la croix fait face à la caméra
            }
        }
    }

    // ---------- Textures créées par le code ----------

    // Disque ou anneau (rayons relatifs 0..1), bords adoucis
    public static Sprite CercleSprite(int taille, float rayonInterne, float rayonExterne)
    {
        Texture2D t = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        float c = (taille - 1) * 0.5f;
        Color32[] px = new Color32[taille * taille];
        for (int y = 0; y < taille; y++)
            for (int x = 0; x < taille; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((rayonExterne - r) * c) * Mathf.Clamp01((r - rayonInterne) * c + (rayonInterne <= 0f ? 99f : 0f));
                px[y * taille + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f), 100f);
    }

    // Voile : transparent au centre, opaque sur les bords
    public static Sprite VoileSprite()
    {
        const int taille = 64;
        Texture2D t = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[taille * taille];
        float c = (taille - 1) * 0.5f;
        for (int y = 0; y < taille; y++)
            for (int x = 0; x < taille; x++)
            {
                float r = Mathf.Max(Mathf.Abs(x - c), Mathf.Abs(y - c)) / c;   // 0 centre, 1 bord
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, r));
                px[y * taille + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f), 100f);
    }

    // Triangle pointant vers le haut (chevron)
    static Sprite TriangleSprite()
    {
        const int taille = 64;
        Texture2D t = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[taille * taille];
        for (int y = 0; y < taille; y++)
            for (int x = 0; x < taille; x++)
            {
                float largeur = (1f - (float)y / (taille - 1)) * 0.5f * taille;   // large en bas, pointe en haut
                float a = Mathf.Clamp01(largeur - Mathf.Abs(x - (taille - 1) * 0.5f));
                px[y * taille + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f), 100f);
    }

    // Rune : cadre rond + 4 traits tirés de l'id (jamais de l'authenticité)
    public static Texture2D RuneTexture(int id)
    {
        const int T = 64;
        Texture2D t = new Texture2D(T, T, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[T * T];
        float c = (T - 1) * 0.5f;
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                bool cadre = r > 0.88f && r < 0.98f;
                px[y * T + x] = new Color32(255, 255, 255, (byte)(cadre ? 255 : 0));
            }
        System.Random h = new System.Random(id * 7919 + 13);
        for (int s = 0; s < 4; s++)
        {
            int x0 = 14 + h.Next(5) * 9, y0 = 14 + h.Next(5) * 9, x1 = 14 + h.Next(5) * 9, y1 = 14 + h.Next(5) * 9;
            Trait(px, T, x0, y0, x1, y1);
        }
        t.SetPixels32(px); t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static void Trait(Color32[] px, int T, int x0, int y0, int x1, int y1)
    {
        int pas = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
        for (int i = 0; i <= pas; i++)
        {
            int x = x0 + (x1 - x0) * i / pas, y = y0 + (y1 - y0) * i / pas;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < T && yy >= 0 && yy < T) px[yy * T + xx] = new Color32(255, 255, 255, 255);
                }
        }
    }
}
