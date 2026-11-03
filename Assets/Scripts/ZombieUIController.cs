using UnityEngine;

namespace ZombieAR
{
    public class ZombieUIController : MonoBehaviour
    {
        [Header("UI Controls")]
        [Tooltip("Activer/Désactiver le panneau de debug de droite (désactivé par défaut)")]
        public bool showDebugWindow = false;

        [Header("HUD AR Settings")]
        public bool showCrosshair = true;
        public bool showFireButton = true;

        private GUIStyle fireBtnStyle;
        private GUIStyle bannerStyle;
        private bool stylesInitialized = false;

        private void InitStyles(float scale)
        {
            if (stylesInitialized) return;

            fireBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(22 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            fireBtnStyle.normal.textColor = Color.white;

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bannerStyle.normal.textColor = Color.yellow;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            float scale = Mathf.Clamp(Screen.width / 800f, 1.0f, 2.2f);
            InitStyles(scale);

            // 1. Réticule de visée propre et discret au centre de l'écran (Crosshair)
            if (showCrosshair)
            {
                DrawCrosshair();
            }

            // 2. Bouton tactile de TIR en bas à droite pour mobile
            if (showFireButton)
            {
                float btnSize = 85f * scale;
                float margin = 25f * scale;
                Rect fireRect = new Rect(Screen.width - btnSize - margin, Screen.height - btnSize - margin, btnSize, btnSize);

                GUI.backgroundColor = new Color(0.85f, 0.15f, 0.15f, 0.85f);
                if (GUI.Button(fireRect, "💥\nTIR", fireBtnStyle))
                {
                    if (PlayerWeaponController.Instance != null)
                    {
                        PlayerWeaponController.Instance.Shoot();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            // 3. Bannière d'alerte rouge discrète pendant les 5s (au centre haut)
            var anyZombie = FindAnyObjectByType<ZombieChaseController>();
            if (anyZombie != null && anyZombie.CurrentState == ZombieChaseController.State.AlertHolding)
            {
                float bannerWidth = Mathf.Min(Screen.width * 0.85f, 440f * scale);
                float bannerHeight = 70f * scale;
                Rect centerRect = new Rect((Screen.width - bannerWidth) * 0.5f, 40f * scale, bannerWidth, bannerHeight);

                GUI.backgroundColor = new Color(0.7f, 0.05f, 0.05f, 0.88f);
                GUI.Box(centerRect, "");
                GUI.backgroundColor = Color.white;

                GUI.Label(centerRect, $"⚠️ BRUIT DÉTECTÉ !\nAttaque dans : {anyZombie.CountdownRemaining:F1}s", bannerStyle);
            }

            // Si vous souhaitez réactiver la fenêtre de debug pour des tests plus tard
            if (showDebugWindow)
            {
                DrawDebugPanel(scale);
            }
        }

        private void DrawCrosshair()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float size = 8f;
            float thickness = 2f;

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            // Ligne horizontale
            GUI.DrawTexture(new Rect(cx - size, cy - thickness * 0.5f, size * 2f, thickness), Texture2D.whiteTexture);
            // Ligne verticale
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - size, thickness, size * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawDebugPanel(float scale)
        {
            float panelWidth = 320f * scale;
            float panelHeight = 160f * scale;
            Rect panelRect = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, panelHeight);

            GUI.Box(panelRect, "Panneau Debug");
            GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 20, panelWidth - 20, panelHeight - 30));

            var mic = MicLoudnessDetector.Instance;
            if (mic != null)
            {
                GUILayout.Label($"Micro dB: {mic.CurrentDb:F1} (Seuil: {mic.thresholdDb:F0})");
            }

            if (GUILayout.Button("Réinitialiser Zombies"))
            {
                var zombies = FindObjectsByType<ZombieChaseController>(FindObjectsSortMode.None);
                foreach (var z in zombies) z.ResetToIdle();
            }

            GUILayout.EndArea();
        }
    }
}
