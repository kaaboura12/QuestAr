using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VisageIDAuth.Biometrics;
using VisageIDAuth.Database;
using VisageIDAuth.Models;

namespace VisageIDAuth.UI
{
    /// <summary>
    /// Contrôleur principal d'interface pour le Login (Visage ID + Mot de passe) et l'Inscription (Register).
    /// La caméra Selfie s'ouvre en direct au 1er clic (pour que l'utilisateur se voie dans le cadre)
    /// et capture l'empreinte au 2ème clic, puis se referme automatiquement.
    /// </summary>
    public class VisageAuthUIController : MonoBehaviour
    {
        [Header("=== INTÉGRATION PROJET OFFICIEL (POUR TON AMI) ===")]
        [Tooltip("Nom de la scène à charger automatiquement après un Login réussi (laisser vide pour rester sur l'écran de profil)")]
        [SerializeField] private string nextSceneOnLoginSuccess = "MainMenu";

        [Tooltip("Masquer automatiquement le Canvas de Login après connexion réussie (utile si intégré dans la même scène)")]
        [SerializeField] private bool hideCanvasOnLoginSuccess = false;

        [Tooltip("Seuil de similarité biométrique requis pour le Visage ID (0.74 = 74%)")]
        [Range(0.60f, 0.95f)]
        [SerializeField] private float faceIdSimilarityThreshold = 0.74f;

        [Header("=== AUTO-CAPTURE SANS BOUTON (STYLE SMARTPHONE / FACE ID) ===")]
        [Tooltip("Active la capture automatique du visage dès que la qualité dépasse 75% sans avoir besoin d'appuyer sur le bouton")]
        [SerializeField] private bool enableAutoCapture = true;
        [Tooltip("Seuil de qualité requis pour déclencher la capture automatique (défaut 0.75 = 75%)")]
        [Range(0.60f, 0.95f)]
        [SerializeField] private float autoCaptureQualityThreshold = 0.75f;

        private int _consecutiveHighQualityFrames = 0;
        private float _lastAutoCaptureTime = 0f;

        [Tooltip("Événement Unity déclenché dès qu'un utilisateur se connecte (passe le Username et la méthode d'auth)")]
        public UnityEvent<string, string> onLoginSuccessEvent;

        [Header("=== SERVICES (Auto-assignés si vides) ===")]
        [SerializeField] private MongoAtlasService mongoService;
        [SerializeField] private VisageCameraScanner cameraScanner;

        [Header("=== PANNEAUX DE NAVIGATION ===")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject registerPanel;
        [SerializeField] private GameObject profileSuccessPanel;

        [Header("=== BOUTONS D'ONGLETS (CONNEXION / INSCRIPTION) ===")]
        [SerializeField] private Button tabLoginButton;
        [SerializeField] private Button tabRegisterButton;
        [SerializeField] private Image tabLoginBg;
        [SerializeField] private Image tabRegisterBg;

        [Header("=== CAMÉRA & SCANNER VISAGE ID ===")]
        [SerializeField] private RawImage cameraFeedRawImage;
        [SerializeField] private Image cameraFrameBorder;
        [SerializeField] private TMP_Text cameraGuidanceText;

        [Header("=== ÉCRAN LOGIN (CHOIX 1 : VISAGE ID & CHOIX 2 : CLASSIQUE) ===")]
        [SerializeField] private TMP_InputField loginEmailInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private Button loginWithFaceIdButton;
        [SerializeField] private Button loginWithPasswordButton;

        [Header("=== ÉCRAN INSCRIPTION (CRÉATION COMPTE + VISAGE ID) ===")]
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerEmailInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private Button captureFaceForRegisterButton;
        [SerializeField] private TMP_Text captureFaceStatusBadgeText;
        [SerializeField] private Button submitRegisterButton;

        [Header("=== ÉCRAN SUCCÈS / PROFIL CONNECTÉ ===")]
        [SerializeField] private TMP_Text profileWelcomeTitleText;
        [SerializeField] private TMP_Text profileDetailsText;
        [SerializeField] private Button continueToAppButton;
        [SerializeField] private Button logoutButton;

        [Header("=== BARRE D'ÉTAT MONGODB & MESSAGES ===")]
        [SerializeField] private TMP_Text mongoStatusText;
        [SerializeField] private TMP_Text feedbackMessageText;
        [SerializeField] private Image feedbackBannerBg;

        private float[] _capturedRegisterFaceEmbedding;
        private bool _isBusy;

        private readonly Color _activeTabColor = Color.white;
        private readonly Color _inactiveTabColor = new Color(0.52f, 0.46f, 0.44f, 0.85f);
        private readonly Color _successBannerColor = new Color(0.20f, 0.62f, 0.34f, 0.95f);
        private readonly Color _errorBannerColor = new Color(0.82f, 0.18f, 0.14f, 0.95f);
        private readonly Color _infoBannerColor = new Color(0.72f, 0.32f, 0.12f, 0.92f);

        private void Awake()
        {
            ApplyPortraitOrientation();
            EnsureServicesExist();
            BindUIButtons();
        }

        public static void ApplyPortraitOrientation()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        public static void ApplyLandscapeOrientation()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
        }

        private void Start()
        {
            if (cameraScanner != null)
            {
                cameraScanner.DisableBackgroundARCameras();
                if (cameraFeedRawImage != null)
                {
                    cameraScanner.SetPreviewReferences(cameraFeedRawImage, cameraFrameBorder);
                }
                cameraScanner.OnFaceDetectionUpdated += HandleFaceDetectionUpdated;
            }

            if (mongoService != null)
            {
                mongoService.OnConnectionStatusChanged += HandleMongoStatusChanged;
                HandleMongoStatusChanged(mongoService.ConnectionStatusMessage, true);
            }

            ShowLoginTab();
            SetFeedback("Prêt : Choisissez Visage ID ou Email/Mot de passe, ou créez un compte.", _infoBannerColor);
        }

        private void OnDestroy()
        {
            if (cameraScanner != null)
                cameraScanner.OnFaceDetectionUpdated -= HandleFaceDetectionUpdated;

            if (mongoService != null)
                mongoService.OnConnectionStatusChanged -= HandleMongoStatusChanged;
        }

        private void EnsureServicesExist()
        {
            if (mongoService == null)
            {
                mongoService = FindAnyObjectByType<MongoAtlasService>();
                if (mongoService == null)
                {
                    mongoService = gameObject.AddComponent<MongoAtlasService>();
                }
            }

            if (cameraScanner == null)
            {
                cameraScanner = FindAnyObjectByType<VisageCameraScanner>();
                if (cameraScanner == null)
                {
                    cameraScanner = gameObject.AddComponent<VisageCameraScanner>();
                }
            }
        }

        private void BindUIButtons()
        {
            if (tabLoginButton != null)
            {
                tabLoginButton.onClick.RemoveAllListeners();
                tabLoginButton.onClick.AddListener(ShowLoginTab);
            }

            if (tabRegisterButton != null)
            {
                tabRegisterButton.onClick.RemoveAllListeners();
                tabRegisterButton.onClick.AddListener(ShowRegisterTab);
            }

            if (loginWithFaceIdButton != null)
            {
                loginWithFaceIdButton.onClick.RemoveAllListeners();
                loginWithFaceIdButton.onClick.AddListener(OnClickLoginWithFaceId);
            }

            if (loginWithPasswordButton != null)
            {
                loginWithPasswordButton.onClick.RemoveAllListeners();
                loginWithPasswordButton.onClick.AddListener(OnClickLoginWithPassword);
            }

            if (captureFaceForRegisterButton != null)
            {
                captureFaceForRegisterButton.onClick.RemoveAllListeners();
                captureFaceForRegisterButton.onClick.AddListener(OnClickCaptureFaceForRegister);
            }

            if (submitRegisterButton != null)
            {
                submitRegisterButton.onClick.RemoveAllListeners();
                submitRegisterButton.onClick.AddListener(OnClickSubmitRegister);
            }

            if (continueToAppButton != null)
            {
                continueToAppButton.onClick.RemoveAllListeners();
                continueToAppButton.onClick.AddListener(OnClickContinueToOfficialProject);
            }

            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveAllListeners();
                logoutButton.onClick.AddListener(OnClickLogout);
            }
        }

        // =====================================================================
        // NAVIGATION ENTRE LES ÉCRANS (ONGLETS)
        // =====================================================================

        public void ShowLoginTab()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (profileSuccessPanel != null) profileSuccessPanel.SetActive(false);

            if (tabLoginBg != null) tabLoginBg.color = _activeTabColor;
            if (tabRegisterBg != null) tabRegisterBg.color = _inactiveTabColor;

            if (cameraScanner != null && cameraScanner.IsCameraRunning)
                cameraScanner.StopCamera();

            SetButtonLabel(loginWithFaceIdButton, "OUVRIR LA CAMÉRA SELFIE (VISAGE ID)");
        }

        public void ShowRegisterTab()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(true);
            if (profileSuccessPanel != null) profileSuccessPanel.SetActive(false);

            if (tabLoginBg != null) tabLoginBg.color = _inactiveTabColor;
            if (tabRegisterBg != null) tabRegisterBg.color = _activeTabColor;

            _capturedRegisterFaceEmbedding = null;
            if (captureFaceStatusBadgeText != null)
            {
                captureFaceStatusBadgeText.text = "Empreinte faciale : Non capturée (cliquez ci-dessus pour ouvrir la caméra Selfie)";
                captureFaceStatusBadgeText.color = new Color(1f, 0.78f, 0.35f, 1f);
            }

            if (cameraScanner != null && cameraScanner.IsCameraRunning)
                cameraScanner.StopCamera();

            SetButtonLabel(captureFaceForRegisterButton, "1. OUVRIR LA CAMÉRA SELFIE");
        }

        private void ShowProfileSuccessScreen(AuthResult result)
        {
            if (cameraScanner != null && cameraScanner.IsCameraRunning)
                cameraScanner.StopCamera();

            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(false);

            onLoginSuccessEvent?.Invoke(result.User?.username ?? "", result.AuthMethod ?? "");

            if (!string.IsNullOrWhiteSpace(nextSceneOnLoginSuccess))
            {
                if (cameraScanner != null) cameraScanner.RestoreBackgroundARCameras();
                ApplyLandscapeOrientation();
                gameObject.SetActive(false);
                SceneManager.LoadScene(nextSceneOnLoginSuccess.Trim());
                return;
            }

            if (profileSuccessPanel != null) profileSuccessPanel.SetActive(true);

            if (profileWelcomeTitleText != null && result.User != null)
            {
                profileWelcomeTitleText.text = $"Bienvenue, {result.User.username} !";
            }

            if (profileDetailsText != null && result.User != null)
            {
                string methodLabel = result.AuthMethod == "FACE_ID"
                    ? $"VISAGE ID (Score biométrique : {result.SimilarityScore * 100f:F1}%)"
                    : (result.AuthMethod == "PASSWORD" ? "LOGIN CLASSIQUE (Email + Mot de passe)" : "INSCRIPTION + VISAGE ID");

                profileDetailsText.text =
                    $"<b>Utilisateur :</b> {result.User.username}\n" +
                    $"<b>Email :</b> {result.User.email}\n" +
                    $"<b>Méthode :</b> {methodLabel}\n" +
                    $"<b>MongoDB _id :</b> {result.User._id}\n" +
                    $"<b>Base & Collection :</b> {mongoService.DatabaseName}.{mongoService.UsersCollectionName}";
            }

            if (hideCanvasOnLoginSuccess)
            {
                if (cameraScanner != null) cameraScanner.RestoreBackgroundARCameras();
                ApplyLandscapeOrientation();
                gameObject.SetActive(false);
            }
        }

        // =====================================================================
        // ACTIONS BOUTONS : LOGIN (CHOIX 1 : VISAGE ID | CHOIX 2 : MOT DE PASSE)
        // =====================================================================

        public async void OnClickLoginWithFaceId()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                // ÉTAPE 1 : Si la caméra Selfie est éteinte, on l'allume pour que l'utilisateur se voie à l'écran !
                if (!cameraScanner.IsCameraRunning)
                {
                    SetFeedback("Ouverture de la caméra Selfie...", _infoBannerColor);
                    var openRes = await cameraScanner.OpenSelfieCameraAsync();
                    if (!openRes.opened)
                    {
                        SetFeedback(openRes.message, _errorBannerColor);
                        return;
                    }

                    SetButtonLabel(loginWithFaceIdButton, "VALIDER & SCANNER MON VISAGE MAINTENANT");
                    SetFeedback("Caméra Selfie active ! Regardez le cadre et appuyez à nouveau sur le bouton vert.", _infoBannerColor);
                    return;
                }

                // ÉTAPE 2 : La caméra Selfie est ouverte -> on capture le visage et on vérifie sur MongoDB Atlas
                SetFeedback("Analyse biométrique de votre visage en cours...", _infoBannerColor);
                var capture = await cameraScanner.CaptureFromActiveCameraAndCloseAsync();
                SetButtonLabel(loginWithFaceIdButton, "OUVRIR LA CAMÉRA SELFIE (VISAGE ID)");

                if (!capture.success || capture.embedding == null)
                {
                    SetFeedback(capture.message, _errorBannerColor);
                    return;
                }

                string optionalEmailHint = loginEmailInput != null ? loginEmailInput.text : "";
                var authResult = await mongoService.LoginWithFaceIdAsync(
                    capture.embedding,
                    faceIdSimilarityThreshold,
                    optionalEmailHint
                );

                if (authResult.Success)
                {
                    SetFeedback(authResult.Message, _successBannerColor);
                    ShowProfileSuccessScreen(authResult);
                }
                else
                {
                    SetFeedback(authResult.Message, _errorBannerColor);
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async void OnClickLoginWithPassword()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                if (cameraScanner != null && cameraScanner.IsCameraRunning)
                    cameraScanner.StopCamera();

                string emailOrUser = loginEmailInput != null ? loginEmailInput.text : "";
                string password = loginPasswordInput != null ? loginPasswordInput.text : "";

                SetFeedback("Vérification des identifiants dans MongoDB...", _infoBannerColor);
                var authResult = await mongoService.LoginWithPasswordAsync(emailOrUser, password);

                if (authResult.Success)
                {
                    SetFeedback(authResult.Message, _successBannerColor);
                    ShowProfileSuccessScreen(authResult);
                }
                else
                {
                    SetFeedback(authResult.Message, _errorBannerColor);
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        // =====================================================================
        // ACTIONS BOUTONS : INSCRIPTION (SCAN VISAGE + CRÉATION COMPTE MONGODB)
        // =====================================================================

        public async void OnClickCaptureFaceForRegister()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                // ÉTAPE 1 : Si la caméra Selfie est éteinte, on l'ouvre en direct !
                if (!cameraScanner.IsCameraRunning)
                {
                    SetFeedback("Ouverture de la caméra Selfie...", _infoBannerColor);
                    var openRes = await cameraScanner.OpenSelfieCameraAsync();
                    if (!openRes.opened)
                    {
                        SetFeedback(openRes.message, _errorBannerColor);
                        return;
                    }

                    SetButtonLabel(captureFaceForRegisterButton, "1. CAPTURER MON VISAGE MAINTENANT");
                    if (captureFaceStatusBadgeText != null)
                    {
                        captureFaceStatusBadgeText.text = "Caméra Selfie ouverte : cadrez votre visage puis appuyez sur Capturer !";
                        captureFaceStatusBadgeText.color = new Color(0.35f, 0.95f, 1f, 1f);
                    }
                    SetFeedback("Vous vous voyez dans le cadre ! Appuyez à nouveau sur le bouton violet pour capturer.", _infoBannerColor);
                    return;
                }

                // ÉTAPE 2 : La caméra Selfie est ouverte -> on capture le vrai visage et on éteint la caméra
                SetFeedback("Capture biométrique de votre visage...", _infoBannerColor);
                var capture = await cameraScanner.CaptureFromActiveCameraAndCloseAsync();
                SetButtonLabel(captureFaceForRegisterButton, "1. RE-SCANNER MON VISAGE (SI BESOIN)");

                if (capture.success && capture.embedding != null)
                {
                    _capturedRegisterFaceEmbedding = capture.embedding;
                    if (captureFaceStatusBadgeText != null)
                    {
                        captureFaceStatusBadgeText.text = $"Visage ID capturé avec succès ({capture.embedding.Length} dimensions biométriques)";
                        captureFaceStatusBadgeText.color = new Color(0.25f, 0.95f, 0.55f, 1f);
                    }
                    SetFeedback("Empreinte du visage capturée et caméra refermée ! Cliquez sur '2. Créer le compte'.", _successBannerColor);
                }
                else
                {
                    SetFeedback(capture.message, _errorBannerColor);
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async void OnClickSubmitRegister()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                string username = registerUsernameInput != null ? registerUsernameInput.text : "";
                string email = registerEmailInput != null ? registerEmailInput.text : "";
                string password = registerPasswordInput != null ? registerPasswordInput.text : "";

                // Si la caméra Selfie est actuellement ouverte mais qu'il n'avait pas encore validé la capture, on capture maintenant
                if (_capturedRegisterFaceEmbedding == null && cameraScanner != null && cameraScanner.IsCameraRunning)
                {
                    var liveCap = await cameraScanner.CaptureFromActiveCameraAndCloseAsync();
                    if (liveCap.success && liveCap.embedding != null)
                    {
                        _capturedRegisterFaceEmbedding = liveCap.embedding;
                    }
                }

                if (_capturedRegisterFaceEmbedding == null)
                {
                    SetFeedback("Veuillez d'abord cliquer sur '1. Ouvrir la caméra Selfie' pour scanner votre visage !", _errorBannerColor);
                    return;
                }

                SetFeedback("Enregistrement du compte et du Visage ID dans MongoDB Atlas...", _infoBannerColor);
                var result = await mongoService.RegisterUserAsync(username, email, password, _capturedRegisterFaceEmbedding);

                if (result.Success)
                {
                    if (registerUsernameInput != null) registerUsernameInput.text = "";
                    if (registerEmailInput != null) registerEmailInput.text = "";
                    if (registerPasswordInput != null) registerPasswordInput.text = "";
                    _capturedRegisterFaceEmbedding = null;

                    SetFeedback(result.Message, _successBannerColor);
                    ShowProfileSuccessScreen(result);
                }
                else
                {
                    SetFeedback(result.Message, _errorBannerColor);
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        public void OnClickContinueToOfficialProject()
        {
            if (cameraScanner != null)
                cameraScanner.RestoreBackgroundARCameras();

            if (!string.IsNullOrWhiteSpace(nextSceneOnLoginSuccess))
            {
                ApplyLandscapeOrientation();
                SceneManager.LoadScene(nextSceneOnLoginSuccess.Trim());
            }
            else if (hideCanvasOnLoginSuccess)
            {
                ApplyLandscapeOrientation();
                gameObject.SetActive(false);
            }
            else
            {
                SetFeedback("Session active ! Ton ami peut renseigner 'Next Scene On Login Success' dans l'Inspector.", _successBannerColor);
            }
        }

        public void OnClickLogout()
        {
            ApplyPortraitOrientation();
            VisageAuthSession.ClearSession();
            if (loginPasswordInput != null) loginPasswordInput.text = "";
            ShowLoginTab();
            SetFeedback("Déconnecté. Vous pouvez tester avec un autre compte ou un autre visage.", _infoBannerColor);
        }

        // =====================================================================
        // CALLBACKS & AFFICHAGE D'ÉTAT
        // =====================================================================

        private void HandleFaceDetectionUpdated(bool detected, float quality, string advice)
        {
            if (cameraGuidanceText != null)
            {
                if (enableAutoCapture && detected && quality >= autoCaptureQualityThreshold && cameraScanner != null && cameraScanner.IsCameraRunning)
                {
                    cameraGuidanceText.text = $"Visage validé ({quality * 100f:F0}%) — Capture automatique...";
                    cameraGuidanceText.color = new Color(0.20f, 1f, 0.50f, 1f);
                }
                else
                {
                    cameraGuidanceText.text = advice;
                    cameraGuidanceText.color = detected
                        ? new Color(0.35f, 0.96f, 0.60f, 1f)
                        : new Color(0.95f, 0.78f, 0.35f, 1f);
                }
            }

            // AUTO-CAPTURE SANS BOUTON (Dès que la qualité est stable >= 75%) :
            if (enableAutoCapture && !_isBusy && cameraScanner != null && cameraScanner.IsCameraRunning && detected && quality >= autoCaptureQualityThreshold)
            {
                if (Time.unscaledTime - _lastAutoCaptureTime > 1.8f)
                {
                    _consecutiveHighQualityFrames++;
                    if (_consecutiveHighQualityFrames >= 4) // 4 frames consécutives stables (~0.5s sans flou de mouvement)
                    {
                        _consecutiveHighQualityFrames = 0;
                        _lastAutoCaptureTime = Time.unscaledTime;
                        TriggerAutoCapture();
                    }
                }
            }
            else
            {
                _consecutiveHighQualityFrames = 0;
            }
        }

        private void TriggerAutoCapture()
        {
            if (loginPanel != null && loginPanel.activeSelf)
            {
                SetFeedback("Visage détecté et stabilisé (≥ 75%) ! Connexion Visage ID automatique...", _infoBannerColor);
                OnClickLoginWithFaceId();
            }
            else if (registerPanel != null && registerPanel.activeSelf)
            {
                SetFeedback("Visage détecté et stabilisé (≥ 75%) ! Capture faciale automatique...", _infoBannerColor);
                OnClickCaptureFaceForRegister();
            }
        }

        private void HandleMongoStatusChanged(string status, bool ready)
        {
            if (mongoStatusText != null)
            {
                mongoStatusText.text = $"MongoDB : {status}";
            }
        }

        private void SetFeedback(string message, Color bannerColor)
        {
            if (feedbackMessageText != null)
            {
                feedbackMessageText.text = message;
            }
            if (feedbackBannerBg != null)
            {
                feedbackBannerBg.color = bannerColor;
            }
        }

        private static void SetButtonLabel(Button btn, string text)
        {
            if (btn == null) return;
            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
        }
    }
}
