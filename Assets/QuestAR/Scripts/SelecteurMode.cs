using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public enum ModeAR { A_ARComplet, B_CameraGyroscope, C_GyroscopeSeul, D_Glisser }

// Choisit au démarrage comment jouer :
//  A = AR complet (ARCore OK, ou éditeur avec XR Simulation : rien ne change)
//  B = flux caméra + gyroscope (ARCore non supporté, caméra autorisée)
//  C = fond gothique + gyroscope (caméra refusée)
//  D = regard au glisser du doigt (pas de gyroscope)
// Dans l'éditeur, ce script ne fait RIEN : le mode A reste identique.
public class SelecteurMode : MonoBehaviour
{
    public static ModeAR Mode { get; private set; }

    const float HauteurYeux = 1.4f;    // hauteur de la caméra en mode de repli
    const float HauteurSol = 0.5f;     // "sol" virtuel où sont posés le sceau et les reliques
    const float DistanceSceau = 2f;

    Camera cam;
    ARPlacementManager placement;
    WebCamTexture webcam;
    RawImage fluxCamera;
    RectTransform fluxRt;
    bool repliActif, sceauPose, utiliseGyro;
    float yaw, pitch;
    GameObject panneauErreur;
    int choix;   // 0 = attente, 1 = réessayer, 2 = continuer sans caméra
    TextMeshProUGUI etiquetteMode;

    IEnumerator Start()
    {
        cam = Camera.main;
        placement = FindAnyObjectByType<ARPlacementManager>();
        Mode = ModeAR.A_ARComplet;
        if (Application.isEditor || cam == null) yield break;   // éditeur : mode A inchangé

        yield return StartCoroutine(ARSession.CheckAvailability());

        if (ARSession.state == ARSessionState.NeedsInstall)
        {
            yield return StartCoroutine(DemanderInstallation());
            if (ARSession.state != ARSessionState.Unsupported && ARSession.state != ARSessionState.NeedsInstall)
            { EtiqueterMode(); yield break; }
        }
        else if (ARSession.state != ARSessionState.Unsupported)
        {
            EtiqueterMode();   // ARCore disponible : mode A
            yield break;
        }

        // ---- ARCore non supporté : mode de repli ----
        bool cameraOk = false;
        while (true)
        {
            yield return StartCoroutine(DemanderPermissionCamera(r => cameraOk = r));
            if (cameraOk) break;
            choix = 0;
            yield return StartCoroutine(AfficherErreurCamera());
            if (choix == 2) break;   // continuer sans caméra
        }

        if (cameraOk) yield return StartCoroutine(DemarrerFluxCamera(r => cameraOk = r));
        ActiverRepli(cameraOk);
    }

    // ---------- Installation ARCore ----------

    IEnumerator DemanderInstallation()
    {
        choix = 0;
        CreerPanneau("Les services Google Play pour la RA doivent être installés pour jouer en réalité augmentée.",
            "INSTALLER", () => choix = 1, "JOUER SANS RA", () => choix = 2);
        while (choix == 0) yield return null;
        Destroy(panneauErreur);
        if (choix == 1) yield return StartCoroutine(ARSession.Install());
        yield return StartCoroutine(ARSession.CheckAvailability());
    }

    // ---------- Permission caméra ----------

    IEnumerator DemanderPermissionCamera(System.Action<bool> fin)
    {
#if UNITY_ANDROID
        if (Permission.HasUserAuthorizedPermission(Permission.Camera)) { fin(true); yield break; }
        bool? reponse = null;
        PermissionCallbacks cb = new PermissionCallbacks();
        cb.PermissionGranted += _ => reponse = true;
        cb.PermissionDenied += _ => reponse = false;
        cb.PermissionDeniedAndDontAskAgain += _ => reponse = false;
        Permission.RequestUserPermission(Permission.Camera, cb);
        while (reponse == null) yield return null;
        fin(reponse.Value);
#else
        fin(true);
        yield break;
#endif
    }

    IEnumerator AfficherErreurCamera()
    {
        CreerPanneau("La caméra est nécessaire pour voir le monde réel. Autorise-la (dans les réglages du téléphone si besoin), ou continue sans caméra.",
            "RÉESSAYER", () => choix = 1, "CONTINUER SANS CAMÉRA", () => choix = 2);
        while (choix == 0) yield return null;
        Destroy(panneauErreur);
    }

    void CreerPanneau(string message, string bouton1, UnityEngine.Events.UnityAction a1, string bouton2, UnityEngine.Events.UnityAction a2)
    {
        Canvas c = UIOutils.NouveauCanvas("CanvasModeAR", 150);
        c.transform.SetParent(transform, false);
        panneauErreur = c.gameObject;
        UIOutils.Rect("Fond", c.transform, Vector2.zero, new Vector2(3000f, 3000f), new Color(0.03f, 0.03f, 0.03f, 0.96f), true);
        RectTransform z = UIOutils.ZoneSure(c.transform);
        TextMeshProUGUI t = UIOutils.Texte(z, message, 44f, new Vector2(0f, 250f), new Vector2(880f, 700f));
        t.color = UIOutils.Os;
        UIOutils.Bouton(z, bouton1, new Vector2(0f, -250f), new Vector2(760f, 160f), a1, 44f);
        UIOutils.Bouton(z, bouton2, new Vector2(0f, -450f), new Vector2(760f, 160f), a2, 40f);
        UIOutils.DesactiverRaycastTextes();
    }

    // ---------- Flux caméra (WebCamTexture) ----------

    IEnumerator DemarrerFluxCamera(System.Action<bool> fin)
    {
        string nom = null;
        foreach (WebCamDevice d in WebCamTexture.devices)
            if (!d.isFrontFacing) { nom = d.name; break; }
        if (nom == null && WebCamTexture.devices.Length > 0) nom = WebCamTexture.devices[0].name;
        if (nom == null) { fin(false); yield break; }

        webcam = new WebCamTexture(nom, 1280, 720, 30);
        webcam.Play();
        float attente = 0f;
        while (webcam.width < 100 && attente < 6f) { attente += Time.unscaledDeltaTime; yield return null; }
        if (webcam.width < 100) { webcam.Stop(); webcam = null; fin(false); yield break; }
        fin(true);
    }

    // ---------- Activation du repli ----------

    void ActiverRepli(bool cameraOk)
    {
        // On coupe l'AR (sinon la caméra AR reste noire et prend le contrôle de la vue)
        ARSession session = FindAnyObjectByType<ARSession>();
        if (session != null) session.enabled = false;
        foreach (ARCameraBackground bg in FindObjectsByType<ARCameraBackground>(FindObjectsInactive.Include)) bg.enabled = false;
        foreach (ARPlaneManager pm in FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include)) pm.enabled = false;
        foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name.Contains("TrackedPoseDriver")) mb.enabled = false;   // c'est nous qui orientons la caméra
        if (placement != null)
        {
            placement.enabled = false;   // le placement se fait par un simple toucher (voir Update)
            if (placement.texteAide != null) placement.texteAide.text = "Touche l'écran pour poser le sceau";
        }

        cam.transform.position = new Vector3(0f, HauteurYeux, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.01f, 0.015f);

        // Gyroscope (AttitudeSensor du nouvel Input System)
        utiliseGyro = AttitudeSensor.current != null;
        if (utiliseGyro) InputSystem.EnableDevice(AttitudeSensor.current);
        Vector3 e = cam.transform.eulerAngles;
        yaw = e.y; pitch = 0f;

        CreerFond(cameraOk);
        Mode = utiliseGyro ? (cameraOk ? ModeAR.B_CameraGyroscope : ModeAR.C_GyroscopeSeul) : ModeAR.D_Glisser;
        repliActif = true;
        EtiqueterMode();
    }

    // Fond derrière tout : flux caméra, ou voile sombre gothique si pas de caméra
    void CreerFond(bool cameraOk)
    {
        GameObject go = new GameObject("CanvasFond");
        go.transform.SetParent(transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceCamera;     // dessiné derrière les objets 3D (très loin de la caméra)
        c.worldCamera = cam;
        c.planeDistance = Mathf.Min(cam.farClipPlane * 0.9f, 50f);
        c.sortingOrder = -100;

        GameObject img = new GameObject("Fond", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);
        if (cameraOk)
        {
            fluxCamera = img.AddComponent<RawImage>();
            fluxCamera.texture = webcam;
            fluxCamera.raycastTarget = false;
            fluxRt = fluxCamera.rectTransform;
        }
        else
        {
            Image fond = img.AddComponent<Image>();
            fond.sprite = VisionVampirique.VoileSprite();
            fond.color = new Color(0.30f, 0f, 0.06f, 0.9f);   // lueur rouge sombre sur les bords
            fond.raycastTarget = false;
            Stretch(fond.rectTransform);
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void EtiqueterMode()
    {
        if (!Debug.isDebugBuild) return;   // étiquette visible seulement dans les builds de développement
        Canvas c = UIOutils.NouveauCanvas("CanvasEtiquetteMode", 90);
        c.transform.SetParent(transform, false);
        etiquetteMode = UIOutils.Texte(UIOutils.ZoneSure(c.transform), "Mode " + Mode, 28f, new Vector2(-300f, -900f), new Vector2(500f, 50f));
        etiquetteMode.color = new Color(UIOutils.Os.r, UIOutils.Os.g, UIOutils.Os.b, 0.5f);
    }

    // ---------- Mise à jour du repli ----------

    void Update()
    {
        if (!repliActif || cam == null) return;
        if (fluxRt != null && webcam != null && webcam.width > 100) AjusterFlux();

        // Orientation de la caméra
        if (utiliseGyro && AttitudeSensor.current != null)
        {
            Quaternion q = AttitudeSensor.current.attitude.ReadValue();
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
        }
        else Glisser();

        // Premier toucher : le sceau est posé à 2 m devant
        if (!sceauPose && ToucheSansUI())
            PoserSceau();
    }

    // Image caméra : tournée selon l'orientation de l'appareil et agrandie pour couvrir l'écran
    void AjusterFlux()
    {
        int angle = webcam.videoRotationAngle;
        bool pivote = angle == 90 || angle == 270;
        float w = Screen.width, h = Screen.height;
        float largeurImg = pivote ? webcam.height : webcam.width;
        float hauteurImg = pivote ? webcam.width : webcam.height;
        float s = Mathf.Max(w / largeurImg, h / hauteurImg);
        fluxRt.anchorMin = fluxRt.anchorMax = new Vector2(0.5f, 0.5f);
        fluxRt.sizeDelta = new Vector2(webcam.width * s, webcam.height * s);
        fluxRt.localEulerAngles = new Vector3(0f, 0f, -angle);
        fluxRt.localScale = new Vector3(1f, webcam.videoVerticallyMirrored ? -1f : 1f, 1f);
    }

    // Mode D : on tourne la vue en glissant le doigt (ou la souris)
    void Glisser()
    {
        Vector2 delta = Vector2.zero;
        bool appuye = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        { appuye = true; delta = Touchscreen.current.primaryTouch.delta.ReadValue(); }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        { appuye = true; delta = Mouse.current.delta.ReadValue(); }
        if (!appuye || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
        yaw += delta.x * 0.15f;
        pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -80f, 80f);
        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    bool ToucheSansUI()
    {
        bool touche = (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                   || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (!touche) return false;
        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
    }

    void PoserSceau()
    {
        if (placement == null || placement.panneauPrefab == null) return;
        sceauPose = true;
        Vector3 avant = cam.transform.forward; avant.y = 0f;
        if (avant.sqrMagnitude < 0.01f) avant = Vector3.forward;
        avant.Normalize();
        Vector3 pos = cam.transform.position + avant * DistanceSceau;
        pos.y = HauteurSol;
        Instantiate(placement.panneauPrefab, pos, Quaternion.identity);
        if (placement.texteAide != null) placement.texteAide.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (webcam != null) webcam.Stop();
    }
}
