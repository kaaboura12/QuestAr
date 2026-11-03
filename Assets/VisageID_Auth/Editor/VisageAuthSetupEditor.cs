#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VisageIDAuth.Biometrics;
using VisageIDAuth.Database;
using VisageIDAuth.Models;
using VisageIDAuth.UI;

namespace VisageIDAuth.Editor
{
    public class VisageAuthSetupEditor : EditorWindow
    {
        private Vector2 _scrollPos;

        // Chemins des assets du thème de ton ami (Alebardium / Bloodlines UI - QUEST AR)
        private const string FontPath = "Assets/Alebardium/Bloodlines UI/Fonts/MedievalSharp SDF.asset";
        private const string FrameBgPath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_background.png";
        private const string FrameMainRedPath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_main_menu_red.png";
        private const string FrameOutlineRedPath = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_red.png";
        private const string FrameOutlineV2Path = "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_v2.png";
        private const string Btn1RedDefaultPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button1/Status_Red_Default.png";
        private const string Btn1RedHoverPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button1/Status_Red_Hover.png";
        private const string Btn1PressedPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button1/Status_Pressed.png";
        private const string Btn1GreyDefaultPath = "Assets/Alebardium/Bloodlines UI/Textures/Button/Button1/Status_Grey_Default.png";
        private const string InputRedPath = "Assets/Alebardium/Bloodlines UI/Textures/Input Field/Frame_input_red.png";
        private const string NoticeV1Path = "Assets/Alebardium/Bloodlines UI/Textures/Notice/Frame_notice_v1.png";
        private const string NoticeV2Path = "Assets/Alebardium/Bloodlines UI/Textures/Notice/Frame_notice_v2.png";

        [MenuItem("Tools/VisageID Auth/1. Build VisageID UI & Prefab in Scene")]
        public static void BuildVisageAuthInActiveScene()
        {
            // Désactiver l'ancien UI AR Template et AR Session au départ pour qu'aucune caméra AR ne tourne en arrière-plan
            GameObject existingTemplateUI = GameObject.Find("UI");
            if (existingTemplateUI != null && existingTemplateUI.GetComponent<VisageAuthUIController>() == null)
            {
                existingTemplateUI.SetActive(false);
            }

            GameObject existingArSession = GameObject.Find("AR Session");
            if (existingArSession != null)
            {
                existingArSession.SetActive(false);
            }

            // Supprimer une éventuelle ancienne instance de VisageAuth_System avant reconstruction propre
            GameObject oldSystem = GameObject.Find("VisageAuth_System");
            if (oldSystem != null)
            {
                DestroyImmediate(oldSystem);
            }

            // Assurer la présence d'un EventSystem
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // Charger les sprites et la police Bloodlines UI (QUEST AR)
            TMP_FontAsset medievalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Sprite frameBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameBgPath);
            Sprite frameMainRedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameMainRedPath);
            Sprite frameOutlineRedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameOutlineRedPath);
            Sprite frameOutlineV2Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameOutlineV2Path);
            Sprite btnRedDefault = AssetDatabase.LoadAssetAtPath<Sprite>(Btn1RedDefaultPath);
            Sprite btnRedHover = AssetDatabase.LoadAssetAtPath<Sprite>(Btn1RedHoverPath);
            Sprite btnPressed = AssetDatabase.LoadAssetAtPath<Sprite>(Btn1PressedPath);
            Sprite btnGreyDefault = AssetDatabase.LoadAssetAtPath<Sprite>(Btn1GreyDefaultPath);
            Sprite inputRedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(InputRedPath);
            Sprite noticeV1Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NoticeV1Path);
            Sprite noticeV2Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NoticeV2Path);

            // Couleurs du thème QUEST AR / Bloodlines UI
            Color questOrangeTitle = new Color(0.92f, 0.40f, 0.08f, 1f);
            Color parchmentWhite = new Color(0.96f, 0.92f, 0.86f, 1f);
            Color warmGold = new Color(0.95f, 0.72f, 0.32f, 1f);

            // 1. Objet Racine : VisageAuth_System (Canvas + Services + Contrôleur)
            GameObject root = new GameObject("VisageAuth_System");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            MongoAtlasService mongoService = root.AddComponent<MongoAtlasService>();
            VisageCameraScanner cameraScanner = root.AddComponent<VisageCameraScanner>();
            VisageAuthUIController uiController = root.AddComponent<VisageAuthUIController>();

            // 2. Fond principal sombre (Style QUEST AR Bloodlines)
            GameObject bgObj = CreateUIObject("Background", root.transform);
            StretchFull(bgObj.GetComponent<RectTransform>());
            Image bgImage = bgObj.AddComponent<Image>();
            if (frameBgSprite != null)
            {
                bgImage.sprite = frameBgSprite;
                bgImage.color = new Color(0.65f, 0.60f, 0.58f, 1f);
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.06f, 0.05f, 1.0f);
            }

            // 3. Carte Centrale Responsive (MainAuthCard avec Frame_background + bordure Frame_main_menu_red)
            GameObject cardObj = CreateUIObject("MainAuthCard", bgObj.transform);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(960f, 1740f);
            Image cardBg = cardObj.AddComponent<Image>();
            if (frameBgSprite != null)
            {
                cardBg.sprite = frameBgSprite;
                cardBg.color = new Color(0.92f, 0.88f, 0.86f, 0.98f);
            }
            else
            {
                cardBg.color = new Color(0.12f, 0.09f, 0.08f, 0.98f);
            }

            // Cadre décoratif rouge sang / rouille (Frame_main_menu_red)
            GameObject cardBorderObj = CreateUIObject("CardBloodlinesBorder", cardObj.transform);
            StretchFull(cardBorderObj.GetComponent<RectTransform>());
            Image cardBorderImg = cardBorderObj.AddComponent<Image>();
            if (frameMainRedSprite != null)
            {
                cardBorderImg.sprite = frameMainRedSprite;
                cardBorderImg.color = Color.white;
            }
            else
            {
                cardBorderImg.color = new Color(0.75f, 0.22f, 0.10f, 0.35f);
            }
            cardBorderImg.raycastTarget = false;

            // 4. En-tête QUEST AR + Sous-titre Visage ID & Statut MongoDB Atlas
            TMP_Text headerTitle = CreateText(
                "HeaderTitle",
                cardObj.transform,
                "QUEST AR",
                64,
                FontStyles.Bold,
                questOrangeTitle,
                new Vector2(0f, 760f),
                new Vector2(860f, 75f),
                medievalFont
            );

            CreateText(
                "HeaderSubtitle",
                cardObj.transform,
                "PORTAIL D'AUTHENTIFICATION VISAGE ID",
                26,
                FontStyles.Bold,
                parchmentWhite,
                new Vector2(0f, 702f),
                new Vector2(860f, 38f),
                medievalFont
            );

            TMP_Text mongoStatusText = CreateText(
                "MongoStatusBadge",
                cardObj.transform,
                "MongoDB Atlas : visage_auth_db (users • login_logs)",
                20,
                FontStyles.Normal,
                warmGold,
                new Vector2(0f, 664f),
                new Vector2(860f, 32f),
                medievalFont
            );

            // 5. Barre d'Onglets (CONNEXION | CRÉER UN COMPTE) avec les boutons Bloodlines UI
            GameObject tabsBar = CreateUIObject("TabsContainer", cardObj.transform);
            RectTransform tabsRect = tabsBar.GetComponent<RectTransform>();
            tabsRect.anchoredPosition = new Vector2(0f, 575f);
            tabsRect.sizeDelta = new Vector2(860f, 96f);

            Button tabLoginBtn = CreateBloodlinesButton(
                "Tab_Login_Button",
                tabsBar.transform,
                "CONNEXION",
                28,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(-215f, 0f),
                new Vector2(415f, 92f),
                medievalFont,
                out Image tabLoginBg
            );

            Button tabRegisterBtn = CreateBloodlinesButton(
                "Tab_Register_Button",
                tabsBar.transform,
                "INSCRIPTION",
                28,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                new Color(0.55f, 0.48f, 0.45f, 0.88f),
                parchmentWhite,
                new Vector2(215f, 0f),
                new Vector2(415f, 92f),
                medievalFont,
                out Image tabRegisterBg
            );

            // 5b. Bannière Héro décorative QUEST AR (affichée quand la caméra Selfie est fermée)
            GameObject heroBanner = CreateUIObject("QuestArHeroBanner", cardObj.transform);
            RectTransform heroRect = heroBanner.GetComponent<RectTransform>();
            heroRect.anchoredPosition = new Vector2(0f, 295f);
            heroRect.sizeDelta = new Vector2(820f, 410f);
            Image heroBg = heroBanner.AddComponent<Image>();
            if (noticeV2Sprite != null)
            {
                heroBg.sprite = noticeV2Sprite;
                heroBg.color = new Color(1f, 1f, 1f, 0.92f);
            }
            else
            {
                heroBg.color = new Color(0.14f, 0.09f, 0.08f, 0.90f);
            }

            if (frameOutlineV2Sprite != null)
            {
                GameObject heroBorder = CreateUIObject("HeroOutline", heroBanner.transform);
                StretchFull(heroBorder.GetComponent<RectTransform>());
                Image hbImg = heroBorder.AddComponent<Image>();
                hbImg.sprite = frameOutlineV2Sprite;
                hbImg.color = new Color(0.90f, 0.38f, 0.12f, 0.85f);
                hbImg.raycastTarget = false;
            }

            CreateText(
                "HeroBannerTitle",
                heroBanner.transform,
                "SYSTÈME BIOMÉTRIQUE\nVISAGE ID",
                38,
                FontStyles.Bold,
                questOrangeTitle,
                new Vector2(0f, 55f),
                new Vector2(740f, 120f),
                medievalFont
            );

            CreateText(
                "HeroBannerSub",
                heroBanner.transform,
                "Appuyez sur le bouton Visage ID ci-dessous pour ouvrir\nla caméra Selfie et scanner votre visage en temps réel.",
                23,
                FontStyles.Normal,
                parchmentWhite,
                new Vector2(0f, -65f),
                new Vector2(740f, 95f),
                medievalFont
            );

            // 6. Zone Caméra Visage ID (Cachée par défaut, s'ouvre par-dessus la bannière Héro au clic !)
            GameObject camSection = CreateUIObject("CameraScannerSection", cardObj.transform);
            RectTransform camSectionRect = camSection.GetComponent<RectTransform>();
            camSectionRect.anchoredPosition = new Vector2(0f, 295f);
            camSectionRect.sizeDelta = new Vector2(820f, 430f);

            Image camFrameBorder = camSection.AddComponent<Image>();
            if (frameOutlineRedSprite != null)
            {
                camFrameBorder.sprite = frameOutlineRedSprite;
                camFrameBorder.color = Color.white;
            }
            else
            {
                camFrameBorder.color = new Color(0.88f, 0.35f, 0.12f, 0.95f);
            }

            GameObject camInner = CreateUIObject("CameraViewport", camSection.transform);
            RectTransform camInnerRect = camInner.GetComponent<RectTransform>();
            camInnerRect.anchorMin = Vector2.zero;
            camInnerRect.anchorMax = Vector2.one;
            camInnerRect.offsetMin = new Vector2(18f, 18f);
            camInnerRect.offsetMax = new Vector2(-18f, -18f);
            Image camInnerBg = camInner.AddComponent<Image>();
            camInnerBg.color = new Color(0.05f, 0.04f, 0.04f, 1f);
            Mask camMask = camInner.AddComponent<Mask>();
            camMask.showMaskGraphic = true;

            GameObject rawImgObj = CreateUIObject("CameraFeedRawImage", camInner.transform);
            StretchFull(rawImgObj.GetComponent<RectTransform>());
            RawImage cameraFeedRawImage = rawImgObj.AddComponent<RawImage>();
            cameraFeedRawImage.color = Color.white;

            // Guide visuel de cadrage du visage (Coeur interne sans arrière-plan)
            GameObject faceGuideBox = CreateUIObject("FaceTargetGuide", camInner.transform);
            RectTransform guideRect = faceGuideBox.GetComponent<RectTransform>();
            guideRect.anchoredPosition = new Vector2(0f, 10f);
            guideRect.sizeDelta = new Vector2(270f, 320f);
            Image guideImg = faceGuideBox.AddComponent<Image>();
            if (frameOutlineRedSprite != null)
            {
                guideImg.sprite = frameOutlineRedSprite;
                guideImg.color = new Color(0.95f, 0.45f, 0.15f, 0.45f);
            }
            else
            {
                guideImg.color = new Color(0.95f, 0.45f, 0.15f, 0.18f);
            }
            guideImg.raycastTarget = false;

            TMP_Text cameraGuidanceText = CreateText(
                "CameraGuidanceText",
                camSection.transform,
                "Placez votre visage au centre du cadre",
                22,
                FontStyles.Bold,
                warmGold,
                new Vector2(0f, -180f),
                new Vector2(760f, 38f),
                medievalFont
            );

            // =================================================================
            // 7. PANNEAU CONNEXION (CHOIX 1 : VISAGE ID | CHOIX 2 : MOT DE PASSE)
            // =================================================================
            GameObject loginPanel = CreateUIObject("LoginPanel", cardObj.transform);
            RectTransform loginRect = loginPanel.GetComponent<RectTransform>();
            loginRect.anchoredPosition = new Vector2(0f, -245f);
            loginRect.sizeDelta = new Vector2(840f, 570f);

            CreateText(
                "Choice1_Label",
                loginPanel.transform,
                "CHOIX 1 — CONNEXION BIOMÉTRIQUE VISAGE ID",
                23,
                FontStyles.Bold,
                questOrangeTitle,
                new Vector2(0f, 240f),
                new Vector2(820f, 36f),
                medievalFont
            );

            Button loginFaceIdBtn = CreateBloodlinesButton(
                "Btn_Login_FaceID",
                loginPanel.transform,
                "OUVRIR LA CAMÉRA SELFIE (VISAGE ID)",
                25,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(0f, 162f),
                new Vector2(820f, 102f),
                medievalFont,
                out _
            );

            CreateText(
                "Divider_Label",
                loginPanel.transform,
                "— OU CHOIX 2 : EMAIL & MOT DE PASSE —",
                22,
                FontStyles.Bold,
                warmGold,
                new Vector2(0f, 76f),
                new Vector2(820f, 34f),
                medievalFont
            );

            TMP_InputField loginEmailInput = CreateBloodlinesInputField(
                "Input_Login_Email",
                loginPanel.transform,
                "Email ou Nom d'utilisateur...",
                inputRedSprite,
                medievalFont,
                new Vector2(0f, 2f),
                new Vector2(820f, 86f),
                TMP_InputField.ContentType.Standard
            );

            TMP_InputField loginPasswordInput = CreateBloodlinesInputField(
                "Input_Login_Password",
                loginPanel.transform,
                "Mot de passe...",
                inputRedSprite,
                medievalFont,
                new Vector2(0f, -98f),
                new Vector2(820f, 86f),
                TMP_InputField.ContentType.Password
            );

            Button loginPasswordBtn = CreateBloodlinesButton(
                "Btn_Login_Password",
                loginPanel.transform,
                "SE CONNECTER (MOT DE PASSE)",
                26,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(0f, -210f),
                new Vector2(820f, 100f),
                medievalFont,
                out _
            );

            // =================================================================
            // 8. PANNEAU INSCRIPTION (CRÉER COMPTE + ENREGISTRER VISAGE ID)
            // =================================================================
            GameObject registerPanel = CreateUIObject("RegisterPanel", cardObj.transform);
            RectTransform regRect = registerPanel.GetComponent<RectTransform>();
            regRect.anchoredPosition = new Vector2(0f, -245f);
            regRect.sizeDelta = new Vector2(840f, 570f);

            TMP_InputField regUsernameInput = CreateBloodlinesInputField(
                "Input_Register_Username",
                registerPanel.transform,
                "Votre Nom complet ou Pseudo...",
                inputRedSprite,
                medievalFont,
                new Vector2(0f, 220f),
                new Vector2(820f, 80f),
                TMP_InputField.ContentType.Standard
            );

            TMP_InputField regEmailInput = CreateBloodlinesInputField(
                "Input_Register_Email",
                registerPanel.transform,
                "Votre Adresse Email...",
                inputRedSprite,
                medievalFont,
                new Vector2(0f, 126f),
                new Vector2(820f, 80f),
                TMP_InputField.ContentType.EmailAddress
            );

            TMP_InputField regPasswordInput = CreateBloodlinesInputField(
                "Input_Register_Password",
                registerPanel.transform,
                "Choisir un Mot de passe (min 4 caractères)...",
                inputRedSprite,
                medievalFont,
                new Vector2(0f, 32f),
                new Vector2(820f, 80f),
                TMP_InputField.ContentType.Password
            );

            Button captureFaceRegBtn = CreateBloodlinesButton(
                "Btn_Capture_Face_Register",
                registerPanel.transform,
                "1. OUVRIR LA CAMÉRA SELFIE",
                24,
                btnGreyDefault != null ? btnGreyDefault : btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                warmGold,
                new Vector2(0f, -68f),
                new Vector2(820f, 90f),
                medievalFont,
                out _
            );

            TMP_Text captureStatusBadge = CreateText(
                "CaptureFaceStatusBadge",
                registerPanel.transform,
                "Empreinte faciale : Non capturée",
                20,
                FontStyles.Normal,
                warmGold,
                new Vector2(0f, -132f),
                new Vector2(820f, 32f),
                medievalFont
            );

            Button submitRegisterBtn = CreateBloodlinesButton(
                "Btn_Submit_Register",
                registerPanel.transform,
                "2. CRÉER LE COMPTE DANS MONGODB",
                26,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(0f, -210f),
                new Vector2(820f, 98f),
                medievalFont,
                out _
            );

            // =================================================================
            // 9. PANNEAU PROFIL / SUCCÈS DE CONNEXION
            // =================================================================
            GameObject profilePanel = CreateUIObject("ProfileSuccessPanel", cardObj.transform);
            RectTransform profRect = profilePanel.GetComponent<RectTransform>();
            profRect.anchoredPosition = new Vector2(0f, -245f);
            profRect.sizeDelta = new Vector2(840f, 570f);

            Image profCardBg = profilePanel.AddComponent<Image>();
            if (noticeV2Sprite != null)
            {
                profCardBg.sprite = noticeV2Sprite;
                profCardBg.color = Color.white;
            }
            else
            {
                profCardBg.color = new Color(0.12f, 0.08f, 0.07f, 0.96f);
            }

            TMP_Text profileWelcomeTitle = CreateText(
                "ProfileWelcomeTitle",
                profilePanel.transform,
                "Bienvenue !",
                38,
                FontStyles.Bold,
                questOrangeTitle,
                new Vector2(0f, 205f),
                new Vector2(780f, 54f),
                medievalFont
            );

            TMP_Text profileDetails = CreateText(
                "ProfileDetailsText",
                profilePanel.transform,
                "Détails de la session MongoDB...",
                23,
                FontStyles.Normal,
                parchmentWhite,
                new Vector2(0f, 40f),
                new Vector2(740f, 220f),
                medievalFont
            );
            profileDetails.alignment = TextAlignmentOptions.Left;

            Button continueBtn = CreateBloodlinesButton(
                "Btn_Continue_App",
                profilePanel.transform,
                "ENTRER DANS QUEST AR",
                26,
                btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(0f, -120f),
                new Vector2(760f, 92f),
                medievalFont,
                out _
            );

            Button logoutBtn = CreateBloodlinesButton(
                "Btn_Logout",
                profilePanel.transform,
                "SE DÉCONNECTER",
                23,
                btnGreyDefault != null ? btnGreyDefault : btnRedDefault,
                btnRedHover,
                btnPressed,
                Color.white,
                parchmentWhite,
                new Vector2(0f, -215f),
                new Vector2(760f, 82f),
                medievalFont,
                out _
            );

            // =================================================================
            // 10. BANNIÈRE DE FEEDBACK EN BAS DE LA CARTE (Notice Bloodlines)
            // =================================================================
            GameObject feedbackBanner = CreateUIObject("FeedbackBanner", cardObj.transform);
            RectTransform fbRect = feedbackBanner.GetComponent<RectTransform>();
            fbRect.anchoredPosition = new Vector2(0f, -755f);
            fbRect.sizeDelta = new Vector2(860f, 100f);
            Image feedbackBannerBg = feedbackBanner.AddComponent<Image>();
            if (noticeV1Sprite != null)
            {
                feedbackBannerBg.sprite = noticeV1Sprite;
                feedbackBannerBg.color = new Color(0.85f, 0.38f, 0.14f, 0.95f);
            }
            else
            {
                feedbackBannerBg.color = new Color(0.55f, 0.20f, 0.10f, 0.95f);
            }

            TMP_Text feedbackMessageText = CreateText(
                "FeedbackMessageText",
                feedbackBanner.transform,
                "Prêt : Connectez-vous par Visage ID ou Mot de passe.",
                21,
                FontStyles.Bold,
                parchmentWhite,
                Vector2.zero,
                new Vector2(800f, 82f),
                medievalFont
            );

            // Masquer Register, Profile et le carré de la caméra par défaut
            registerPanel.SetActive(false);
            profilePanel.SetActive(false);
            camSection.SetActive(false);

            // =================================================================
            // 11. LIAISON AUTOMATIQUE DES CHAMPS SERIALIZEFIELD (INSPECTOR)
            // =================================================================
            SerializedObject soScanner = new SerializedObject(cameraScanner);
            soScanner.FindProperty("cameraContainerBox").objectReferenceValue = camSection;
            soScanner.FindProperty("cameraPreviewImage").objectReferenceValue = cameraFeedRawImage;
            soScanner.FindProperty("scanFrameBorderImage").objectReferenceValue = camFrameBorder;
            soScanner.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject soUI = new SerializedObject(uiController);
            soUI.FindProperty("mongoService").objectReferenceValue = mongoService;
            soUI.FindProperty("cameraScanner").objectReferenceValue = cameraScanner;
            soUI.FindProperty("loginPanel").objectReferenceValue = loginPanel;
            soUI.FindProperty("registerPanel").objectReferenceValue = registerPanel;
            soUI.FindProperty("profileSuccessPanel").objectReferenceValue = profilePanel;
            soUI.FindProperty("tabLoginButton").objectReferenceValue = tabLoginBtn;
            soUI.FindProperty("tabRegisterButton").objectReferenceValue = tabRegisterBtn;
            soUI.FindProperty("tabLoginBg").objectReferenceValue = tabLoginBg;
            soUI.FindProperty("tabRegisterBg").objectReferenceValue = tabRegisterBg;
            soUI.FindProperty("cameraFeedRawImage").objectReferenceValue = cameraFeedRawImage;
            soUI.FindProperty("cameraFrameBorder").objectReferenceValue = camFrameBorder;
            soUI.FindProperty("cameraGuidanceText").objectReferenceValue = cameraGuidanceText;
            soUI.FindProperty("loginEmailInput").objectReferenceValue = loginEmailInput;
            soUI.FindProperty("loginPasswordInput").objectReferenceValue = loginPasswordInput;
            soUI.FindProperty("loginWithFaceIdButton").objectReferenceValue = loginFaceIdBtn;
            soUI.FindProperty("loginWithPasswordButton").objectReferenceValue = loginPasswordBtn;
            soUI.FindProperty("registerUsernameInput").objectReferenceValue = regUsernameInput;
            soUI.FindProperty("registerEmailInput").objectReferenceValue = regEmailInput;
            soUI.FindProperty("registerPasswordInput").objectReferenceValue = regPasswordInput;
            soUI.FindProperty("captureFaceForRegisterButton").objectReferenceValue = captureFaceRegBtn;
            soUI.FindProperty("captureFaceStatusBadgeText").objectReferenceValue = captureStatusBadge;
            soUI.FindProperty("submitRegisterButton").objectReferenceValue = submitRegisterBtn;
            soUI.FindProperty("profileWelcomeTitleText").objectReferenceValue = profileWelcomeTitle;
            soUI.FindProperty("profileDetailsText").objectReferenceValue = profileDetails;
            soUI.FindProperty("continueToAppButton").objectReferenceValue = continueBtn;
            soUI.FindProperty("logoutButton").objectReferenceValue = logoutBtn;
            soUI.FindProperty("mongoStatusText").objectReferenceValue = mongoStatusText;
            soUI.FindProperty("feedbackMessageText").objectReferenceValue = feedbackMessageText;
            soUI.FindProperty("feedbackBannerBg").objectReferenceValue = feedbackBannerBg;
            soUI.ApplyModifiedPropertiesWithoutUndo();

            // 12. Sauvegarder en Prefab prêt à l'emploi pour l'ami
            string prefabFolder = "Assets/VisageID_Auth/Prefabs";
            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);
            }
            string prefabPath = $"{prefabFolder}/VisageAuth_System.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Selection.activeGameObject = root;
            Debug.Log($"[VisageID Auth] Système créé avec le design QUEST AR (Bloodlines UI) et sauvegardé en Prefab : {prefabPath}");
        }

        [MenuItem("Tools/VisageID Auth/2. Export Package (.unitypackage) for Friend")]
        public static void ExportPackageForFriend()
        {
            string exportPath = Path.Combine(Directory.GetCurrentDirectory(), "VisageID_Auth_For_Friend.unitypackage");
#pragma warning disable CS0618
            string[] foldersToExport = Directory.Exists("Assets/Alebardium/Bloodlines UI")
                ? new[] { "Assets/VisageID_Auth", "Assets/Alebardium/Bloodlines UI" }
                : new[] { "Assets/VisageID_Auth" };

            AssetDatabase.ExportPackage(
                foldersToExport,
                exportPath,
                ExportPackageOptions.Recurse
            );
#pragma warning restore CS0618
            Debug.Log($"[VisageID Auth] Package exporté avec succès pour ton ami (avec thème QUEST AR / Bloodlines UI) : {exportPath}");
        }

        [MenuItem("Tools/VisageID Auth/3. Open MongoDB Database Viewer")]
        public static void OpenViewer()
        {
            var win = GetWindow<VisageAuthSetupEditor>("MongoDB VisageID");
            win.minSize = new Vector2(460, 520);
            win.Show();
        }

        [MenuItem("Tools/VisageID Auth/4. Clear Old Accounts on MongoDB Atlas")]
        public static async void ClearOldAccountsOnMongoAtlas()
        {
            string[] shards = new[]
            {
                "ac-1xnvtj8-shard-00-02.shnhcn3.mongodb.net",
                "ac-1xnvtj8-shard-00-00.shnhcn3.mongodb.net",
                "ac-1xnvtj8-shard-00-01.shnhcn3.mongodb.net"
            };
            var client = new MongoAtlasWireClient(shards, 27017, "negzaouioussama15_db_user", "yM3tcpyKQZGOxZX6", "visage_auth_db");
            var res = await client.InitializeAndFetchUsersAsync("users", "login_logs");
            Debug.Log($"[VisageID Auth] Nettoyage MongoDB Atlas terminé : {res.message} (Comptes v2 restants : {res.users?.Count ?? 0})");
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Base de données MongoDB : visage_auth_db", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "URI configuré : mongodb+srv://negzaouioussama15_db_user:***@stage.shnhcn3.mongodb.net/visage_auth_db\n" +
                "Collections actives : 'users' et 'login_logs'\n" +
                "Thème UI : QUEST AR (Alebardium / Bloodlines UI)",
                MessageType.Info
            );

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reconstruire UI + Prefab (QUEST AR)", GUILayout.Height(30)))
            {
                BuildVisageAuthInActiveScene();
            }
            if (GUILayout.Button("Exporter .unitypackage (Pour ton ami)", GUILayout.Height(30)))
            {
                ExportPackageForFriend();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            string dbFile = Path.Combine(Application.dataPath, "VisageID_Auth", "Database", "visage_auth_db_collections.json");
            if (File.Exists(dbFile))
            {
                string json = File.ReadAllText(dbFile);
                var snapshot = JsonUtility.FromJson<LocalDatabaseSnapshot>(json);
                if (snapshot != null)
                {
                    EditorGUILayout.LabelField($"Collection 'users' ({snapshot.users.Count} compte(s))", EditorStyles.boldLabel);
                    _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                    foreach (var u in snapshot.users)
                    {
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.LabelField($"_id : {u._id}");
                        EditorGUILayout.LabelField($"Nom : {u.username} | Email : {u.email}");
                        EditorGUILayout.LabelField($"Visage ID : {(u.hasFaceId ? $"Oui ({u.faceEmbedding?.Length ?? 0} floats)" : "Non")}");
                        EditorGUILayout.LabelField($"Dernier Login : {u.lastLoginAt} ({u.lastAuthMethod})");
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        // =====================================================================
        // HELPERS DE CONSTRUCTION UI (THÈME BLOODLINES UI / QUEST AR)
        // =====================================================================

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            FontStyles style,
            Color color,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            TMP_FontAsset customFont = null)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (customFont != null)
            {
                tmp.font = customFont;
            }
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static Button CreateBloodlinesButton(
            string name,
            Transform parent,
            string label,
            float fontSize,
            Sprite defaultSprite,
            Sprite hoverSprite,
            Sprite pressedSprite,
            Color imageTint,
            Color textColor,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            TMP_FontAsset customFont,
            out Image bgImage)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            bgImage = go.AddComponent<Image>();
            if (defaultSprite != null)
            {
                bgImage.sprite = defaultSprite;
                bgImage.color = imageTint;
            }
            else
            {
                bgImage.color = new Color(0.55f, 0.18f, 0.10f, 1f);
            }

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bgImage;

            if (defaultSprite != null && (hoverSprite != null || pressedSprite != null))
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState st = new SpriteState
                {
                    highlightedSprite = hoverSprite != null ? hoverSprite : defaultSprite,
                    pressedSprite = pressedSprite != null ? pressedSprite : defaultSprite,
                    selectedSprite = hoverSprite != null ? hoverSprite : defaultSprite
                };
                btn.spriteState = st;
            }

            CreateText(
                "Label",
                go.transform,
                label,
                fontSize,
                FontStyles.Bold,
                textColor,
                Vector2.zero,
                sizeDelta - new Vector2(48f, 16f),
                customFont
            );
            return btn;
        }

        private static TMP_InputField CreateBloodlinesInputField(
            string name,
            Transform parent,
            string placeholderText,
            Sprite inputFrameSprite,
            TMP_FontAsset customFont,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            TMP_InputField.ContentType contentType)
        {
            GameObject root = CreateUIObject(name, parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchoredPosition = anchoredPos;
            rootRect.sizeDelta = sizeDelta;

            Image bg = root.AddComponent<Image>();
            if (inputFrameSprite != null)
            {
                bg.sprite = inputFrameSprite;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.10f, 0.07f, 0.06f, 1f);
            }

            GameObject textArea = CreateUIObject("Text Area", root.transform);
            RectTransform areaRect = textArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(36f, 12f);
            areaRect.offsetMax = new Vector2(-36f, -12f);
            textArea.AddComponent<RectMask2D>();

            GameObject placeholderGo = CreateUIObject("Placeholder", textArea.transform);
            StretchFull(placeholderGo.GetComponent<RectTransform>());
            TextMeshProUGUI placeholderTmp = placeholderGo.AddComponent<TextMeshProUGUI>();
            if (customFont != null) placeholderTmp.font = customFont;
            placeholderTmp.text = placeholderText;
            placeholderTmp.fontSize = 24f;
            placeholderTmp.fontStyle = FontStyles.Italic;
            placeholderTmp.color = new Color(0.75f, 0.65f, 0.58f, 0.72f);
            placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject textGo = CreateUIObject("Text", textArea.transform);
            StretchFull(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI textTmp = textGo.AddComponent<TextMeshProUGUI>();
            if (customFont != null) textTmp.font = customFont;
            textTmp.text = "";
            textTmp.fontSize = 25f;
            textTmp.color = new Color(0.98f, 0.94f, 0.88f, 1f);
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField inputField = root.AddComponent<TMP_InputField>();
            inputField.textViewport = areaRect;
            inputField.textComponent = textTmp;
            inputField.placeholder = placeholderTmp;
            inputField.contentType = contentType;
            inputField.pointSize = 25f;

            return inputField;
        }
    }
}
#endif
