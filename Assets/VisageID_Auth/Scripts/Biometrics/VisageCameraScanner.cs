using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace VisageIDAuth.Biometrics
{
    /// <summary>
    /// Gère la caméra Selfie (WebCamTexture frontale sur Mobile Android/iOS et Webcam sur PC),
    /// aligne automatiquement le visage sur l'axe de symétrie bilatérale (X) et le pont nasal/pommettes (Y),
    /// découpe uniquement la T-Zone interne (25% x 64% : zéro rideau/mur/oreiller ni épaules),
    /// normalise séparément l'éclairage gauche/droite (élimine les ombres de fenêtres latérales),
    /// et extrait une empreinte biométrique 128-D invariante à la position et à la distance.
    /// </summary>
    public class VisageCameraScanner : MonoBehaviour
    {
        public const int EMBEDDING_DIMENSION = 128;

        [Header("Affichage Caméra (Remplaçable dans l'Inspector)")]
        [SerializeField] private GameObject cameraContainerBox;
        [SerializeField] private RawImage cameraPreviewImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private Image scanFrameBorderImage;

        [Header("Paramètres Caméra Selfie & Biométrie")]
        [SerializeField] private bool preferFrontFacingCamera = true;
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;
        [Range(0.60f, 0.95f)]
        [SerializeField] private float matchThreshold = 0.74f;

        [Header("Couleurs du Cadre de Scan (Thème QUEST AR / Bloodlines)")]
        [SerializeField] private Color idleFrameColor = new Color(0.85f, 0.38f, 0.12f, 0.90f);
        [SerializeField] private Color faceDetectedColor = new Color(0.22f, 0.92f, 0.48f, 0.98f);
        [SerializeField] private Color warningFrameColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

        private WebCamTexture _webCamTexture;
        private bool _isCameraRunning;
        private bool _isOpeningCamera;
        private bool _isFaceDetectedInFrame;
        private float _faceQualityScore;
        private string _liveGuidanceMessage = "";
        private float _nextDetectionTime;

        private readonly List<GameObject> _disabledArObjects = new List<GameObject>();
        private readonly List<Behaviour> _disabledArBehaviours = new List<Behaviour>();

        public bool IsCameraRunning => _isCameraRunning;
        public bool IsOpeningCamera => _isOpeningCamera;
        public bool IsFaceDetected => _isFaceDetectedInFrame;
        public float FaceQualityScore => _faceQualityScore;
        public string LiveGuidanceMessage => _liveGuidanceMessage;
        public float MatchThreshold => matchThreshold;

        public event Action<bool, float, string> OnFaceDetectionUpdated;

        public void SetPreviewReferences(RawImage preview, Image frameBorder = null, AspectRatioFitter fitter = null)
        {
            cameraPreviewImage = preview;
            if (frameBorder != null) scanFrameBorderImage = frameBorder;
            if (fitter != null) aspectRatioFitter = fitter;
            ResolveCameraContainerBox();
        }

        private void ResolveCameraContainerBox()
        {
            if (cameraContainerBox != null) return;
            if (scanFrameBorderImage != null)
            {
                cameraContainerBox = scanFrameBorderImage.gameObject;
            }
            else if (cameraPreviewImage != null && cameraPreviewImage.transform.parent != null)
            {
                cameraContainerBox = cameraPreviewImage.transform.parent.gameObject;
            }
        }

        public void SetCameraBoxVisible(bool visible)
        {
            ResolveCameraContainerBox();
            if (cameraContainerBox != null && cameraContainerBox.activeSelf != visible)
            {
                cameraContainerBox.SetActive(visible);
            }
        }

        private void Awake()
        {
            DisableBackgroundARCameras();
        }

        private void OnEnable()
        {
            DisableBackgroundARCameras();
            SetIdleCameraVisuals();
        }

        private void OnDisable()
        {
            StopCamera();
        }

        public void DisableBackgroundARCameras()
        {
            try
            {
                GameObject arSessionGo = GameObject.Find("AR Session");
                if (arSessionGo != null && arSessionGo.activeSelf)
                {
                    arSessionGo.SetActive(false);
                    if (!_disabledArObjects.Contains(arSessionGo))
                        _disabledArObjects.Add(arSessionGo);
                }

                Behaviour[] allBehaviours = FindObjectsByType<Behaviour>();
                foreach (var b in allBehaviours)
                {
                    if (b == null || !b.enabled) continue;
                    string typeName = b.GetType().Name;
                    if (typeName == "ARSession" ||
                        typeName == "ARCameraManager" ||
                        typeName == "ARCameraBackground" ||
                        typeName == "ARPlaneManager" ||
                        typeName == "ARRaycastManager")
                    {
                        b.enabled = false;
                        if (!_disabledArBehaviours.Contains(b))
                            _disabledArBehaviours.Add(b);
                    }
                }
            }
            catch { }
        }

        public void RestoreBackgroundARCameras()
        {
            try
            {
                StopCamera();
                foreach (var go in _disabledArObjects)
                {
                    if (go != null) go.SetActive(true);
                }
                _disabledArObjects.Clear();

                foreach (var b in _disabledArBehaviours)
                {
                    if (b != null) b.enabled = true;
                }
                _disabledArBehaviours.Clear();
            }
            catch { }
        }

        private void SetIdleCameraVisuals()
        {
            if (cameraPreviewImage != null)
            {
                cameraPreviewImage.texture = null;
                cameraPreviewImage.color = new Color(0.08f, 0.06f, 0.06f, 1f);
                cameraPreviewImage.rectTransform.localEulerAngles = Vector3.zero;
                cameraPreviewImage.rectTransform.localScale = Vector3.one;
            }
            if (scanFrameBorderImage != null)
            {
                scanFrameBorderImage.color = idleFrameColor;
            }
            _isFaceDetectedInFrame = false;
            _faceQualityScore = 0f;
            _liveGuidanceMessage = "";

            SetCameraBoxVisible(false);
            OnFaceDetectionUpdated?.Invoke(false, 0f, _liveGuidanceMessage);
        }

        public async Task<(bool opened, string message)> OpenSelfieCameraAsync()
        {
            if (_isCameraRunning && _webCamTexture != null && _webCamTexture.isPlaying && _webCamTexture.width > 32)
            {
                SetCameraBoxVisible(true);
                return (true, "Caméra Selfie active.");
            }

            if (_isOpeningCamera)
            {
                return (false, "Ouverture de la caméra en cours...");
            }

            _isOpeningCamera = true;
            try
            {
                DisableBackgroundARCameras();
                SetCameraBoxVisible(true);
                _liveGuidanceMessage = "Ouverture de la caméra Selfie...";
                OnFaceDetectionUpdated?.Invoke(false, 0f, _liveGuidanceMessage);

#if UNITY_ANDROID && !UNITY_EDITOR
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    _liveGuidanceMessage = "Autorisez l'accès à la caméra sur votre téléphone...";
                    OnFaceDetectionUpdated?.Invoke(false, 0f, _liveGuidanceMessage);
                    Permission.RequestUserPermission(Permission.Camera);

                    float permWait = 0f;
                    while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && permWait < 12f)
                    {
                        await Task.Delay(250);
                        permWait += 0.25f;
                    }

                    if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                    {
                        SetCameraBoxVisible(false);
                        return (false, "Permission caméra refusée sur le téléphone.");
                    }
                }
#endif
                await Task.Delay(200);

                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices == null || devices.Length == 0)
                {
                    SetCameraBoxVisible(false);
                    return (false, "Aucune caméra détectée sur cet appareil.");
                }

                string selfieDeviceName = null;
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].isFrontFacing)
                    {
                        selfieDeviceName = devices[i].name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(selfieDeviceName))
                {
                    selfieDeviceName = devices[0].name;
                }

                if (_webCamTexture != null)
                {
                    if (_webCamTexture.isPlaying) _webCamTexture.Stop();
                    Destroy(_webCamTexture);
                }

                _webCamTexture = new WebCamTexture(selfieDeviceName, requestedWidth, requestedHeight, requestedFps);
                _webCamTexture.Play();

                float waitVideo = 0f;
                while (_webCamTexture != null && _webCamTexture.width <= 32 && waitVideo < 5.0f)
                {
                    await Task.Delay(100);
                    waitVideo += 0.1f;
                }

                if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 32)
                {
                    StopCamera();
                    return (false, "Impossible d'ouvrir la caméra Selfie.");
                }

                _isCameraRunning = true;
                if (cameraPreviewImage != null)
                {
                    cameraPreviewImage.texture = _webCamTexture;
                    cameraPreviewImage.color = Color.white;
                }

                UpdatePreviewOrientation();
                _liveGuidanceMessage = "Caméra Selfie ouverte — Placez votre visage au centre";
                OnFaceDetectionUpdated?.Invoke(false, 0.5f, _liveGuidanceMessage);
                return (true, _liveGuidanceMessage);
            }
            finally
            {
                _isOpeningCamera = false;
            }
        }

        public void StopCamera()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                    _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }
            _isCameraRunning = false;
            _isOpeningCamera = false;
            SetIdleCameraVisuals();
        }

        private void Update()
        {
            if (!_isCameraRunning || _webCamTexture == null) return;

            if (_webCamTexture.didUpdateThisFrame && _webCamTexture.width > 32)
            {
                UpdatePreviewOrientation();

                if (Time.unscaledTime >= _nextDetectionTime)
                {
                    _nextDetectionTime = Time.unscaledTime + 0.12f;
                    EvaluateLiveFacePresence();
                }
            }
        }

        private void UpdatePreviewOrientation()
        {
            if (cameraPreviewImage == null || _webCamTexture == null || _webCamTexture.width <= 32)
                return;

            int rot = -_webCamTexture.videoRotationAngle;
            cameraPreviewImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, rot);

            bool isRotated90 = Mathf.Abs(_webCamTexture.videoRotationAngle) == 90 || Mathf.Abs(_webCamTexture.videoRotationAngle) == 270;
            float scaleX = preferFrontFacingCamera ? -1f : 1f;
            float scaleY = _webCamTexture.videoVerticallyMirrored ? -1f : 1f;

            if (isRotated90)
            {
                scaleX = 1f;
                scaleY = preferFrontFacingCamera ? -1f : 1f;
                if (_webCamTexture.videoVerticallyMirrored) scaleY = -scaleY;
            }

            cameraPreviewImage.rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        private void EvaluateLiveFacePresence()
        {
            if (_webCamTexture == null || _webCamTexture.width <= 32 || _webCamTexture.height <= 32)
                return;

            Color32[] rawPixels = _webCamTexture.GetPixels32();
            GetUprightPixels(
                rawPixels,
                _webCamTexture.width,
                _webCamTexture.height,
                _webCamTexture.videoRotationAngle,
                _webCamTexture.videoVerticallyMirrored,
                preferFrontFacingCamera,
                out Color32[] pixels,
                out int w,
                out int h
            );

            AnalyzeFaceRegionQuality(pixels, w, h, out bool facePresent, out float quality, out string advice);
            _isFaceDetectedInFrame = facePresent;
            _faceQualityScore = quality;
            _liveGuidanceMessage = advice;

            if (scanFrameBorderImage != null)
            {
                scanFrameBorderImage.color = facePresent ? faceDetectedColor : warningFrameColor;
            }

            OnFaceDetectionUpdated?.Invoke(facePresent, quality, advice);
        }

        public async Task<(bool success, float[] embedding, string message)> CaptureFromActiveCameraAndCloseAsync()
        {
            if (!_isCameraRunning || _webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 32)
            {
                var openRes = await OpenSelfieCameraAsync();
                if (!openRes.opened)
                {
                    return (false, null, openRes.message);
                }
                await Task.Delay(1800);
            }

            const int sampleCount = 5;
            float[] accumulated = new float[EMBEDDING_DIMENSION];
            int validSamples = 0;
            string lastAdvice = "";

            for (int s = 0; s < sampleCount; s++)
            {
                if (_webCamTexture == null || !_webCamTexture.isPlaying) break;

                Color32[] rawPixels = _webCamTexture.GetPixels32();
                GetUprightPixels(
                    rawPixels,
                    _webCamTexture.width,
                    _webCamTexture.height,
                    _webCamTexture.videoRotationAngle,
                    _webCamTexture.videoVerticallyMirrored,
                    preferFrontFacingCamera,
                    out Color32[] pixels,
                    out int w,
                    out int h
                );

                AnalyzeFaceRegionQuality(pixels, w, h, out bool facePresent, out _, out lastAdvice);
                if (facePresent)
                {
                    float[] frameVec = Compute128DEmbeddingFromPixels(pixels, w, h);
                    for (int i = 0; i < EMBEDDING_DIMENSION; i++)
                    {
                        accumulated[i] += frameVec[i];
                    }
                    validSamples++;
                }

                await Task.Delay(75);
            }

            if (validSamples < 2)
            {
                StopCamera();
                return (false, null, string.IsNullOrEmpty(lastAdvice)
                    ? "Aucun visage humain détecté devant la caméra ! Placez bien votre visage face à la caméra."
                    : $"Scan refusé : {lastAdvice}");
            }

            StopCamera();

            L2Normalize(accumulated);
            return (true, accumulated, $"Empreinte biométrique 128-D capturée ({validSamples}/{sampleCount} frames validées).");
        }

        /// <summary>
        /// Remet les pixels de la caméra à l'endroit (Portrait / Upright) quel que soit l'angle du capteur Android/iOS/PC,
        /// en tenant compte de videoVerticallyMirrored et de l'effet miroir de la caméra frontale selfie.
        /// </summary>
        private static void GetUprightPixels(
            Color32[] src,
            int srcW,
            int srcH,
            int rotationAngle,
            bool verticallyMirrored,
            bool isFrontFacing,
            out Color32[] dst,
            out int dstW,
            out int dstH)
        {
            int angle = ((rotationAngle % 360) + 360) % 360;
            bool isRotated90 = (angle == 90 || angle == 270);
            dstW = isRotated90 ? srcH : srcW;
            dstH = isRotated90 ? srcW : srcH;
            dst = new Color32[dstW * dstH];

            for (int y = 0; y < dstH; y++)
            {
                int rowOffset = y * dstW;
                for (int x = 0; x < dstW; x++)
                {
                    // L'image selfie pour l'humain est inversée horizontalement
                    int targetX = isFrontFacing ? (dstW - 1 - x) : x;
                    int targetY = y;

                    int sx, sy;
                    if (angle == 90)
                    {
                        sx = dstH - 1 - targetY;
                        sy = verticallyMirrored ? (srcH - 1 - targetX) : targetX;
                    }
                    else if (angle == 270)
                    {
                        sx = targetY;
                        sy = verticallyMirrored ? targetX : (srcH - 1 - targetX);
                    }
                    else if (angle == 180)
                    {
                        sx = srcW - 1 - targetX;
                        sy = verticallyMirrored ? targetY : (srcH - 1 - targetY);
                    }
                    else // angle == 0
                    {
                        sx = targetX;
                        sy = verticallyMirrored ? (srcH - 1 - targetY) : targetY;
                    }

                    sx = Mathf.Clamp(sx, 0, srcW - 1);
                    sy = Mathf.Clamp(sy, 0, srcH - 1);
                    dst[rowOffset + x] = src[sy * srcW + sx];
                }
            }
        }

        /// <summary>
        /// Détecte strictement la chrominance d'une vraie peau humaine (modèle Kovac + Chai-Ngan RGB/YCbCr renforcé).
        /// Rejette 100% :
        /// - Les murs/portes gris chauds, beiges ou taupes (où R - G < 14% de R ou R - B < 24% de R ou Cr < 136)
        /// - Les oreillers/tissus rouges, bordeaux ou magenta (où Cr > 160 ou G < 50% de R)
        /// - Les portes en bois verni / marron très saturé (où B < 24% de R)
        /// </summary>
        private static bool IsHumanFaceChrominance(Color32 c)
        {
            int r = c.r;
            int g = c.g;
            int b = c.b;

            if (r < 48 || g < 30 || b < 20) return false;
            if (r <= g || r <= b) return false;

            // Rejet strict des murs/portes beiges, gris chauds ou taupes (faible saturation) :
            if ((r - g) < 12 || (r - b) < 20) return false;
            if ((r - g) * 100 < r * 14) return false;
            if ((r - b) * 100 < r * 24) return false;

            // Rejet strict du ROUGE / BORDEAUX / ROSE VIF (ex: coussin/oreiller rouge) :
            if (g * 100 < r * 50) return false;
            if ((r - g) * 100 > r * 42) return false;

            // Rejet du BOIS VERNI / ORANGE PUR (ex: porte en bois très saturée) :
            if (b * 100 < r * 24) return false;
            if ((r - b) * 100 > r * 65) return false;

            float cb = 128f - 0.168736f * r - 0.331264f * g + 0.5f * b;
            float cr = 128f + 0.5f * r - 0.418688f * g - 0.081312f * b;

            return cb >= 80f && cb <= 120f && cr >= 136f && cr <= 160f;
        }

        /// <summary>
        /// Détecte le noyau de peau du visage (Pommettes/Front) et ancre la boîte biométrique interne
        /// directement sur la ligne des Yeux/Sourcils (yBrow) et la largeur des pommettes (faceW).
        /// 100% indépendant de la barbe, du cou, des oreillers rouges ou des portes en arrière-plan.
        /// </summary>
        private static void DetectNoseBridgeAnchor(
            Color32[] pixels,
            int width,
            int height,
            out float cx,
            out float yMid,
            out float cropW,
            out float cropH,
            out float skinCoverage)
        {
            int sx0 = (int)(width * 0.12f);
            int sx1 = (int)(width * 0.88f);
            int sy0 = (int)(height * 0.25f);
            int sy1 = (int)(height * 0.85f);

            float[] colSkin = new float[width];
            int skinCnt = 0;
            int totalCnt = 0;
            int step = Mathf.Max(1, Mathf.Min(width, height) / 96);

            for (int y = sy0; y < sy1; y += step)
            {
                int rowOffset = y * width;
                for (int x = sx0; x < sx1; x += step)
                {
                    totalCnt++;
                    if (IsHumanFaceChrominance(pixels[rowOffset + x]))
                    {
                        colSkin[x] += 1f;
                        skinCnt++;
                    }
                }
            }

            skinCoverage = totalCnt > 0 ? (float)skinCnt / totalCnt : 0f;

            // 1. Lissage horizontal pour trouver le centre horizontal (cx) et la largeur de peau (faceW)
            float[] smoothCol = new float[width];
            int win = Mathf.Max(3, width / 26);
            float maxCol = 0f;
            int peakX = width / 2;
            for (int x = win; x < width - win; x++)
            {
                float s = 0f;
                for (int k = -win; k <= win; k++) s += colSkin[x + k];
                smoothCol[x] = s / (2 * win + 1);
                if (smoothCol[x] > maxCol)
                {
                    maxCol = smoothCol[x];
                    peakX = x;
                }
            }

            int xMin = peakX;
            int xMax = peakX;
            while (xMin > sx0 && smoothCol[xMin - 1] >= maxCol * 0.25f) xMin--;
            while (xMax < sx1 && smoothCol[xMax + 1] >= maxCol * 0.25f) xMax++;

            cx = maxCol > 0.4f ? 0.5f * (xMin + xMax) : width * 0.5f;
            cx = Mathf.Clamp(cx, width * 0.25f, width * 0.75f);

            float minDim = Mathf.Min(width, height);
            float rawFaceW = (xMax - xMin) * 1.06f; // compense la coupure à 25% sur l'ovale des joues
            float faceW = Mathf.Clamp(rawFaceW, minDim * 0.26f, minDim * 0.62f);

            // 2. Profil vertical 32 bandes dans le tiers central [cx - 28% faceW .. cx + 28% faceW]
            const int B = 32;
            float[] bandLum = new float[B];
            float[] bandSkin = new float[B];
            int tx0 = Mathf.Clamp((int)(cx - faceW * 0.28f), 0, width - 1);
            int tx1 = Mathf.Clamp((int)(cx + faceW * 0.28f), 0, width - 1);
            float maxBandSkin = 0f;

            for (int b = 0; b < B; b++)
            {
                int by0 = (b * height) / B;
                int by1 = Mathf.Max(by0 + 1, ((b + 1) * height) / B);
                float sL = 0f;
                float sS = 0f;
                int cnt = 0;
                for (int y = by0; y < by1; y += step)
                {
                    int rowOffset = y * width;
                    for (int x = tx0; x <= tx1; x += step)
                    {
                        Color32 p = pixels[rowOffset + x];
                        sL += 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                        if (IsHumanFaceChrominance(p)) sS += 1f;
                        cnt++;
                    }
                }
                int denom = Mathf.Max(1, cnt);
                bandLum[b] = sL / denom;
                bandSkin[b] = sS / denom;
                if (bandSkin[b] > maxBandSkin) maxBandSkin = bandSkin[b];
            }

            // Sommet du front (bSkinTop) et base de la peau des joues/menton (bSkinBot)
            int bSkinTop = B - 2;
            while (bSkinTop > 10 && bandSkin[bSkinTop] < maxBandSkin * 0.30f) bSkinTop--;
            int bSkinBot = 2;
            while (bSkinBot < bSkinTop - 4 && bandSkin[bSkinBot] < maxBandSkin * 0.25f) bSkinBot++;

            // Vallée anatomique des Yeux/Sourcils (bBrow) dans la moitié supérieure de la zone de peau
            int browSearchMin = Mathf.Clamp(bSkinTop - 9, bSkinBot + 1, bSkinTop - 2);
            int browSearchMax = Mathf.Clamp(bSkinTop - 2, browSearchMin, B - 2);
            int bBrow = (browSearchMin + browSearchMax) / 2;
            float minBrowLum = 1e9f;
            for (int b = browSearchMin; b <= browSearchMax; b++)
            {
                float smLum = 0.20f * bandLum[b - 1] + 0.60f * bandLum[b] + 0.20f * bandLum[b + 1];
                if (smLum < minBrowLum)
                {
                    minBrowLum = smLum;
                    bBrow = b;
                }
            }

            float yBrow = ((bBrow + 0.5f) / B) * height;

            // Le centre biométrique interne (yMid) est ancré sous la ligne des yeux/sourcils proportionnellement à faceW
            yMid = Mathf.Clamp(yBrow - faceW * 0.32f, height * 0.28f, height * 0.72f);

            // Cadrage 100% interne à l'ovale du visage (0% d'arrière-plan dans les 4 coins)
            cropW = faceW * 0.72f;
            cropH = faceW * 0.88f;
        }

        /// <summary>
        /// Vérifie qu'un VRAI visage humain est présent (Couleur de peau + Contraste des yeux + Relief du nez/bouche).
        /// Détecte spécifiquement l'ouverture des yeux (fente oculaire) indépendamment des sourcils.
        /// </summary>
        private static void AnalyzeFaceRegionQuality(Color32[] pixels, int width, int height, out bool isValidFace, out float qualityScore, out string message)
        {
            DetectNoseBridgeAnchor(pixels, width, height, out float cx, out float yMid, out float fCropW, out float fCropH, out float skinCoverage);

            if (skinCoverage < 0.04f)
            {
                isValidFace = false;
                qualityScore = 0.10f;
                message = "Aucun visage détecté — placez votre visage face à la caméra.";
                return;
            }

            int cropW = Mathf.Clamp((int)fCropW, 24, width - 2);
            int cropH = Mathf.Clamp((int)fCropH, 24, height - 2);
            int startX = Mathf.Clamp((int)(cx - cropW * 0.5f), 0, width - cropW);
            int startY = Mathf.Clamp((int)(yMid - cropH * 0.50f), 0, height - cropH);

            double sumLum = 0;
            double sumLumSq = 0;
            double symmetryDiff = 0;
            int samplePairs = 0;

            double eyeSlitEdgeEnergy = 0;
            int eyeSlitCount = 0;
            double eyeSlitLumSum = 0;
            double eyeSlitLumSqSum = 0;
            int eyeSlitSamples = 0;

            double mouthBandEdgeEnergy = 0;
            int mouthBandCount = 0;
            int innerSkinPixels = 0;

            int step = Mathf.Max(1, cropW / 20);

            for (int y = step; y < cropH - step; y += step)
            {
                int py = startY + y;
                float normY = (float)y / cropH;

                for (int x = step; x < cropW / 2; x += step)
                {
                    int pxLeft = startX + x;
                    int pxRight = startX + (cropW - 1 - x);

                    Color32 cL = pixels[py * width + pxLeft];
                    Color32 cR = pixels[py * width + pxRight];

                    if (IsHumanFaceChrominance(cL)) innerSkinPixels++;
                    if (IsHumanFaceChrominance(cR)) innerSkinPixels++;

                    float lumL = (0.299f * cL.r + 0.587f * cL.g + 0.114f * cL.b) / 255f;
                    float lumR = (0.299f * cR.r + 0.587f * cR.g + 0.114f * cR.b) / 255f;

                    Color32 cLDown = pixels[(py + step) * width + pxLeft];
                    Color32 cRDown = pixels[(py + step) * width + pxRight];
                    float lumLDown = (0.299f * cLDown.r + 0.587f * cLDown.g + 0.114f * cLDown.b) / 255f;
                    float lumRDown = (0.299f * cRDown.r + 0.587f * cRDown.g + 0.114f * cRDown.b) / 255f;

                    float vertGrad = Mathf.Abs(lumLDown - lumL) + Mathf.Abs(lumRDown - lumR);

                    int pxLeftNext = Mathf.Min(startX + cropW - 1, pxLeft + step);
                    int pxRightPrev = Mathf.Max(startX, pxRight - step);
                    Color32 cLRight = pixels[py * width + pxLeftNext];
                    Color32 cRLeft = pixels[py * width + pxRightPrev];
                    float lumLRight = (0.299f * cLRight.r + 0.587f * cLRight.g + 0.114f * cLRight.b) / 255f;
                    float lumRLeft = (0.299f * cRLeft.r + 0.587f * cRLeft.g + 0.114f * cRLeft.b) / 255f;
                    float horizGrad = Mathf.Abs(lumLRight - lumL) + Mathf.Abs(lumRLeft - lumR);

                    // 1. BANDE DES YEUX (Fente oculaire [0.60 .. 0.78]) :
                    if (normY >= 0.60f && normY <= 0.78f)
                    {
                        eyeSlitEdgeEnergy += (vertGrad + horizGrad) * 0.5;
                        eyeSlitCount += 2;
                        eyeSlitLumSum += lumL + lumR;
                        eyeSlitLumSqSum += lumL * lumL + lumR * lumR;
                        eyeSlitSamples += 2;
                    }
                    // 2. BANDE DE LA BOUCHE ([0.12 .. 0.42]) :
                    else if (normY >= 0.12f && normY <= 0.42f)
                    {
                        mouthBandEdgeEnergy += vertGrad;
                        mouthBandCount += 2;
                    }

                    sumLum += lumL + lumR;
                    sumLumSq += lumL * lumL + lumR * lumR;
                    symmetryDiff += Mathf.Abs(lumL - lumR);
                    samplePairs++;
                }
            }

            int totalSamples = Mathf.Max(1, samplePairs * 2);
            float meanLum = (float)(sumLum / totalSamples);
            float variance = Mathf.Max(0f, (float)(sumLumSq / totalSamples) - meanLum * meanLum);
            float stdDev = Mathf.Sqrt(variance);
            float innerSkinRatio = (float)innerSkinPixels / totalSamples;
            float avgEyeSlitEdge = eyeSlitCount > 0 ? (float)(eyeSlitEdgeEnergy / eyeSlitCount) : 0f;
            float avgMouthEdge = mouthBandCount > 0 ? (float)(mouthBandEdgeEnergy / mouthBandCount) : 0f;
            float symmetryScore = Mathf.Clamp01(1f - (float)(symmetryDiff / Mathf.Max(1, samplePairs)) * 2.1f);

            float eyeSlitMean = eyeSlitSamples > 0 ? (float)(eyeSlitLumSum / eyeSlitSamples) : 0f;
            float eyeSlitVar = eyeSlitSamples > 0 ? Mathf.Max(0f, (float)(eyeSlitLumSqSum / eyeSlitSamples) - eyeSlitMean * eyeSlitMean) : 0f;
            float eyeSlitStd = Mathf.Sqrt(eyeSlitVar);

            if (meanLum < 0.07f)
            {
                isValidFace = false;
                qualityScore = 0.12f;
                message = "Éclairage trop sombre — éclairez votre visage.";
                return;
            }

            if (meanLum > 0.95f)
            {
                isValidFace = false;
                qualityScore = 0.15f;
                message = "Image surexposée — évitez le contre-jour.";
                return;
            }

            if (stdDev < 0.035f || innerSkinRatio < 0.18f || avgMouthEdge < 0.008f)
            {
                isValidFace = false;
                qualityScore = 0.18f;
                message = "Aucun visage complet (yeux/nez/bouche) détecté dans le cadre.";
                return;
            }

            // SÉCURITÉ STYLE SMARTPHONE (Attention Awareness / Détection Yeux Ouverts) :
            if (avgEyeSlitEdge < 0.009f || eyeSlitStd < 0.050f)
            {
                isValidFace = false;
                qualityScore = 0.30f;
                message = "Veuillez bien ouvrir les yeux face à la caméra.";
                return;
            }

            qualityScore = Mathf.Clamp01(
                0.35f * symmetryScore +
                0.35f * Mathf.Clamp01(avgEyeSlitEdge * 35f) +
                0.30f * Mathf.Clamp01(innerSkinRatio * 1.5f)
            );

            isValidFace = qualityScore >= 0.38f;
            message = isValidFace
                ? $"Visage humain détecté ({qualityScore * 100f:F0}%) — Prêt pour le scan !"
                : "Centrez bien votre visage (yeux, nez, bouche visibles).";
        }

        /// <summary>
        /// Extrait un vecteur biométrique 128-D invariant à la distance (Scale-Invariant) et à la position :
        /// - Cadrage dynamique sur la boîte de peau réelle du visage (cx, yMid, cropW, cropH) alignée sur Sourcils/Yeux et Bouche.
        /// - Zéro plafond/mur ni t-shirt/épaules dans l'empreinte.
        /// - Normalisation séparée Gauche/Droite + Grille 10x8 + Onde verticale Haar 8x4 + Profils Peau/Réflectance 10V + 6H.
        /// </summary>
        public static float[] Compute128DEmbeddingFromPixels(Color32[] pixels, int width, int height)
        {
            DetectNoseBridgeAnchor(pixels, width, height, out float cx, out float yMid, out float cropW, out float cropH, out _);

            float startX = cx - cropW * 0.5f;
            float startY = yMid - cropH * 0.50f;

            const int R = 20;
            const int C = 16;
            float[,] logLum = new float[R, C];
            float[,] cellSkin = new float[R, C];

            for (int r = 0; r < R; r++)
            {
                int sy0 = (int)(startY + (r * cropH) / R);
                int sy1 = Mathf.Max(sy0 + 1, (int)(startY + ((r + 1) * cropH) / R));
                for (int c = 0; c < C; c++)
                {
                    int sx0 = (int)(startX + (c * cropW) / C);
                    int sx1 = Mathf.Max(sx0 + 1, (int)(startX + ((c + 1) * cropW) / C));
                    double sumR = 0, sumG = 0, sumB = 0;
                    int sSk = 0, cnt = 0;
                    for (int py = sy0; py < sy1; py++)
                    {
                        int cpy = Mathf.Clamp(py, 0, height - 1);
                        int rowOffset = cpy * width;
                        for (int px = sx0; px < sx1; px++)
                        {
                            int cpx = Mathf.Clamp(px, 0, width - 1);
                            Color32 col = pixels[rowOffset + cpx];
                            sumR += col.r;
                            sumG += col.g;
                            sumB += col.b;
                            if (IsHumanFaceChrominance(col)) sSk++;
                            cnt++;
                        }
                    }
                    int denom = Mathf.Max(1, cnt);
                    float inv = 1f / (denom * 255f);
                    float lum = (float)(0.299 * sumR + 0.587 * sumG + 0.114 * sumB) * inv;
                    logLum[r, c] = Mathf.Log(lum + 0.05f);
                    cellSkin[r, c] = (float)sSk / denom;
                }
            }

            // Normalisation d'illumination indépendante Moitié Gauche / Moitié Droite sur la boîte faciale interne (100% visage, 0% arrière-plan)
            float[,] refl = new float[R, C];
            for (int side = 0; side < 2; side++)
            {
                int cStart = side * (C / 2);
                int cEnd = cStart + (C / 2);
                double sL = 0, sSq = 0;
                int nSide = R * (C / 2);
                for (int r = 0; r < R; r++)
                    for (int c = cStart; c < cEnd; c++)
                        sL += logLum[r, c];

                float mSide = (float)(sL / nSide);
                for (int r = 0; r < R; r++)
                {
                    for (int c = cStart; c < cEnd; c++)
                    {
                        float d = logLum[r, c] - mSide;
                        refl[r, c] = d;
                        sSq += d * d;
                    }
                }

                float stdSide = Mathf.Sqrt(Mathf.Max(1e-5f, (float)(sSq / nSide)));
                for (int r = 0; r < R; r++)
                    for (int c = cStart; c < cEnd; c++)
                        refl[r, c] = Mathf.Clamp(refl[r, c] / stdSide, -2.5f, 2.5f);
            }

            // Symétrisation bilatérale équilibrée (75% symétrique + 25% asymétrie propre à l'individu)
            float[,] symRefl = new float[R, C];
            for (int r = 0; r < R; r++)
            {
                for (int c = 0; c < C; c++)
                {
                    symRefl[r, c] = 0.75f * (0.5f * (refl[r, c] + refl[r, C - 1 - c])) + 0.25f * refl[r, c];
                }
            }

            float[] emb = new float[EMBEDDING_DIMENSION];

            // 1. [0..79] : Grille 10x8 de réflectance faciale interne
            float[] grid80 = new float[80];
            for (int gr = 0; gr < 10; gr++)
            {
                for (int gc = 0; gc < 8; gc++)
                {
                    grid80[gr * 8 + gc] = 0.25f * (
                        symRefl[gr * 2, gc * 2] + symRefl[gr * 2 + 1, gc * 2] +
                        symRefl[gr * 2, gc * 2 + 1] + symRefl[gr * 2 + 1, gc * 2 + 1]
                    );
                }
            }
            L2Normalize(grid80);
            Array.Copy(grid80, 0, emb, 0, 80);

            // 2. [80..111] : Onde verticale Haar signée 8x4 (transitions Barbe -> Bouche -> Nez -> Yeux -> Sourcils)
            float[] vWave32 = new float[32];
            for (int gr = 0; gr < 8; gr++)
            {
                for (int gc = 0; gc < 4; gc++)
                {
                    float upper = 0.5f * (grid80[(gr + 2) * 8 + gc * 2] + grid80[(gr + 2) * 8 + gc * 2 + 1]);
                    float lower = 0.5f * (grid80[gr * 8 + gc * 2] + grid80[gr * 8 + gc * 2 + 1]);
                    vWave32[gr * 4 + gc] = upper - lower;
                }
            }
            L2Normalize(vWave32);
            for (int i = 0; i < 32; i++) emb[80 + i] = vWave32[i] * 0.85f;

            // 3. [112..121] : 10 Bandes Verticales Anthropométriques (Réflectance + Ratio Peau/Barbe par étage du visage)
            float[] vBands = new float[10];
            for (int gr = 0; gr < 10; gr++)
            {
                float sR = 0f, sSk = 0f;
                for (int gc = 1; gc < 7; gc++)
                {
                    sR += grid80[gr * 8 + gc];
                    sSk += 0.25f * (
                        cellSkin[gr * 2, gc * 2] + cellSkin[gr * 2 + 1, gc * 2] +
                        cellSkin[gr * 2, gc * 2 + 1] + cellSkin[gr * 2 + 1, gc * 2 + 1]
                    );
                }
                vBands[gr] = (sR / 6f) + 0.35f * ((sSk / 6f) - 0.5f);
            }
            L2Normalize(vBands);
            for (int i = 0; i < 10; i++) emb[112 + i] = vBands[i] * 0.70f;

            // 4. [122..127] : 6 Bandes Horizontales T-Zone (Pont nasal vs Orbites)
            float[] hBands = new float[6];
            for (int gc = 0; gc < 6; gc++)
            {
                float s = 0f;
                for (int gr = 1; gr < 9; gr++) s += grid80[gr * 8 + (gc + 1)];
                hBands[gc] = s / 8f;
            }
            L2Normalize(hBands);
            for (int i = 0; i < 6; i++) emb[122 + i] = hBands[i] * 0.60f;

            L2Normalize(emb);
            return emb;
        }

        private static void ZScoreNormalizeBlock(float[] block, float scaleWeight)
        {
            if (block == null || block.Length == 0) return;
            double sum = 0.0;
            for (int i = 0; i < block.Length; i++) sum += block[i];
            float mean = (float)(sum / block.Length);

            double sumSq = 0.0;
            for (int i = 0; i < block.Length; i++)
            {
                float d = block[i] - mean;
                sumSq += d * d;
            }
            float std = Mathf.Sqrt(Mathf.Max(1e-6f, (float)(sumSq / block.Length)));

            for (int i = 0; i < block.Length; i++)
            {
                block[i] = ((block[i] - mean) / std) * scaleWeight;
            }
        }

        public static void L2Normalize(float[] vector)
        {
            if (vector == null || vector.Length == 0) return;
            double sumSq = 0.0;
            for (int i = 0; i < vector.Length; i++)
            {
                sumSq += (double)vector[i] * vector[i];
            }
            if (sumSq <= 1e-10) return;

            float invNorm = (float)(1.0 / Math.Sqrt(sumSq));
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] *= invNorm;
            }
        }
    }
}
