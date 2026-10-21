using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.Gameplay;

namespace Abyssal.UI
{
    /// <summary>
    /// Intuitive, high-visibility Subnautica-inspired HUD for ABYSSAL.
    /// Features:
    /// - Iconic bottom-left circular biometric gauge cluster (Oxygen O2, Suit Integrity HP, Flashlight Power)
    /// - High-contrast, enlarged legible dials (easily visible on all resolutions)
    /// - Top-center Depth Meter (meters below surface) & Directional Compass/Sonar Bearing
    /// - Center circular targeting reticle & contextual interaction prompt
    /// - Optional diving helmet / visor frame vignette overlay for deep immersion
    /// </summary>
    public class GameHUDManager : MonoBehaviour
    {
        [Header("State References")]
        [SerializeField] private DiverHealth diverHealth;
        [SerializeField] private ScannerHUD scanner;
        [SerializeField] private CrystalInteractable crystal;
        [SerializeField] private FirstPersonCamera firstPersonCamera;
        [SerializeField] private Transform diverTransform;

        [Header("HUD Customization")]
        [Tooltip("Overall scaling factor for the HUD elements.")]
        [Range(0.8f, 1.6f)]
        [SerializeField] private float hudScale = 1.05f;

        [Tooltip("Renders the authentic Subnautica-style diving mask / visor vignette around screen edges.")]
        [SerializeField] private bool showHelmetVisor = true;

        [Tooltip("Surface water elevation used to calculate depth.")]
        [SerializeField] private float surfaceWaterElevation = 9.5f;

        [Header("Oxygen System")]
        [SerializeField] private bool enableOxygen = true;
        [SerializeField] private float maxOxygen = 135f; // ~2.25 minutes of deep sea dive reserve
        [SerializeField] private float currentOxygen = 135f;
        [SerializeField] private float oxygenDepletionRate = 1.0f;
        [SerializeField] private float oxygenRefillRate = 35.0f;

        [Header("Objective Display")]
        [SerializeField] private string currentObjective = "Descend into the canyon and recover the anomalous crystal.";

        // Textures & Materials
        private Texture2D circleBackTex;
        private Texture2D reticleTex;
        private Texture2D heartIconTex;
        private Texture2D oxygenIconTex;
        private Texture2D flashlightIconTex;
        private Texture2D compassBracketTex;
        private Texture2D visorFrameTex;

        private Texture2D solidDarkTex;
        private Texture2D solidPillTex;
        private Material radialRingMaterial;

        private bool isGameOver = false;
        private string endScreenMessage = "";

        public float CurrentOxygen => currentOxygen;
        public float MaxOxygen => maxOxygen;

        private void Awake()
        {
            LoadHUDAssets();
            CreateProceduralFallbacks();
        }

        private void Start()
        {
            currentOxygen = maxOxygen;
            ResolveReferences();

            if (diverHealth != null)
            {
                diverHealth.OnDiverDied += HandleDiverDied;
            }

            if (LeviathanEncounter.Instance != null)
            {
                LeviathanEncounter.Instance.OnChaseStarted += () =>
                {
                    currentObjective = "CRITICAL ALERT: PREDATOR LEVIATHAN TERRITORY!";
                };
            }
        }

        private void Update()
        {
            if (diverHealth == null || scanner == null || firstPersonCamera == null || diverTransform == null)
            {
                ResolveReferences();
            }

            // Oxygen depletion & surface replenishment
            if (enableOxygen && !isGameOver && diverHealth != null && !diverHealth.IsDead)
            {
                float diverY = diverTransform != null ? diverTransform.position.y : 0f;
                bool isSurfaced = diverY >= (surfaceWaterElevation - 1.5f);

                if (isSurfaced)
                {
                    currentOxygen = Mathf.Min(maxOxygen, currentOxygen + oxygenRefillRate * Time.deltaTime);
                }
                else
                {
                    currentOxygen = Mathf.Max(0f, currentOxygen - oxygenDepletionRate * Time.deltaTime);
                    if (currentOxygen <= 0f)
                    {
                        diverHealth.TakeDamage(6f * Time.deltaTime, "Oxygen Depletion");
                    }
                }
            }

            // Game over restart shortcut
            if (isGameOver && Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
        }

        private void ResolveReferences()
        {
            if (diverHealth == null) diverHealth = FindAnyObjectByType<DiverHealth>();
            if (scanner == null) scanner = FindAnyObjectByType<ScannerHUD>();
            if (crystal == null) crystal = FindAnyObjectByType<CrystalInteractable>();
            if (firstPersonCamera == null) firstPersonCamera = FindAnyObjectByType<FirstPersonCamera>();
            if (diverTransform == null)
            {
                GameObject diver = GameObject.Find("Diver");
                if (diver != null) diverTransform = diver.transform;
                else if (diverHealth != null) diverTransform = diverHealth.transform;
            }
        }

        private void LoadHUDAssets()
        {
            circleBackTex = Resources.Load<Texture2D>("UI/HUD_CircleBack");
            reticleTex = Resources.Load<Texture2D>("UI/HUD_Reticle");
            heartIconTex = Resources.Load<Texture2D>("UI/HUD_HeartIcon");
            oxygenIconTex = Resources.Load<Texture2D>("UI/HUD_OxygenIcon");
            flashlightIconTex = Resources.Load<Texture2D>("UI/HUD_FlashlightIcon");
            compassBracketTex = Resources.Load<Texture2D>("UI/HUD_CompassBracket");
            visorFrameTex = Resources.Load<Texture2D>("UI/DiverVisor_Frame");

            Shader ringShader = Shader.Find("Abyssal/HUD_RadialRing");
            if (ringShader != null)
            {
                radialRingMaterial = new Material(ringShader);
            }
        }

        private void CreateProceduralFallbacks()
        {
            solidDarkTex = MakeTex(2, 2, new Color(0.03f, 0.08f, 0.14f, 0.85f));
            solidPillTex = MakeTex(2, 2, new Color(0.05f, 0.15f, 0.25f, 0.90f));
            if (circleBackTex == null) circleBackTex = solidDarkTex;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void HandleDiverDied()
        {
            if (FinalCutsceneController.Instance != null && FinalCutsceneController.Instance.IsCutsceneActive)
            {
                return;
            }
            ShowGameOverScreen("SUIT COMPROMISED — DIVER LOST TO THE ABYSS");
        }

        public void ShowGameOverScreen(string message)
        {
            isGameOver = true;
            endScreenMessage = message;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void RestartGame()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void OnGUI()
        {
            // Suppress HUD during scripted cutscenes for cinematic immersion
            if (FinalCutsceneController.Instance != null && FinalCutsceneController.Instance.IsCutsceneActive)
            {
                return;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;

            if (isGameOver)
            {
                DrawGameOverScreen(screenW, screenH);
                return;
            }

            // 1. DIVING VISOR OVERLAY (Authentic Subnautica helmet border)
            if (showHelmetVisor && visorFrameTex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.92f);
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), visorFrameTex);
                GUI.color = Color.white;
            }

            // 2. RETICLE & INTERACTION PROMPT (Center)
            DrawCenterReticle(screenW, screenH);

            // 3. TOP CENTER: SUBNAUTICA DEPTH & DIRECTIONAL SONAR HEADING
            DrawTopDepthAndSonar(screenW, screenH);

            // 4. TOP LEFT: MISSION OBJECTIVE BADGE
            DrawObjectivePill();

            // 5. BOTTOM LEFT: THE ICONIC CIRCULAR BIOMETRIC CLUSTER (O2, HP, FLASHLIGHT)
            DrawSubnauticaBiometricCluster(screenH);

            // 6. BOTTOM RIGHT: CONTROLS GUIDE
            DrawControlsBadge(screenW, screenH);
        }

        // ==========================================
        // SUBNAUTICA BIOMETRIC CLUSTER (Bottom Left)
        // ==========================================
        private void DrawSubnauticaBiometricCluster(float screenH)
        {
            float scale = hudScale;
            float clusterBaseX = 40f * scale;
            float clusterBaseY = screenH - (195f * scale);

            // --- A. MAIN DIAL: OXYGEN (O2) ---
            float o2DialSize = 145f * scale;
            Rect o2Rect = new Rect(clusterBaseX + (35f * scale), clusterBaseY - (10f * scale), o2DialSize, o2DialSize);

            // Translucent glass circle back
            GUI.DrawTexture(o2Rect, circleBackTex);

            float o2Percent = Mathf.Clamp01(currentOxygen / maxOxygen);
            bool isLowO2 = currentOxygen < 30f;
            Color o2FillColor = isLowO2
                ? Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.7f, 0.1f), Mathf.PingPong(Time.time * 4f, 1f))
                : new Color(0.0f, 0.88f, 1.0f, 1.0f); // Electric Cyan

            DrawRadialRing(o2Rect, o2Percent, o2FillColor, 0.38f, 0.48f);

            // Center icon & readout
            if (oxygenIconTex != null)
            {
                float iconSize = 34f * scale;
                GUI.color = o2FillColor;
                GUI.DrawTexture(new Rect(o2Rect.center.x - iconSize * 0.5f, o2Rect.y + (20f * scale), iconSize, iconSize), oxygenIconTex);
                GUI.color = Color.white;
            }

            GUIStyle o2LabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.9f, 1.0f, 0.85f) }
            };
            GUI.Label(new Rect(o2Rect.x, o2Rect.y + (54f * scale), o2Rect.width, 18f * scale), "O₂", o2LabelStyle);

            GUIStyle o2NumberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isLowO2 ? Color.red : Color.white }
            };
            GUI.Label(new Rect(o2Rect.x, o2Rect.y + (70f * scale), o2Rect.width, 36f * scale), $"{Mathf.CeilToInt(currentOxygen)}", o2NumberStyle);

            // --- B. SATELLITE DIAL 1: SUIT INTEGRITY / HEALTH (Top-Left of O2) ---
            float hpDialSize = 85f * scale;
            Rect hpRect = new Rect(clusterBaseX - (15f * scale), clusterBaseY - (65f * scale), hpDialSize, hpDialSize);

            GUI.DrawTexture(hpRect, circleBackTex);

            float currentHp = diverHealth != null ? diverHealth.CurrentHealth : 100f;
            float maxHp = diverHealth != null ? diverHealth.MaxHealth : 100f;
            float hpPercent = Mathf.Clamp01(currentHp / maxHp);

            Color hpColor;
            if (hpPercent > 0.6f) hpColor = new Color(0.0f, 0.92f, 0.45f); // Vibrant Emerald Green
            else if (hpPercent > 0.3f) hpColor = new Color(1.0f, 0.85f, 0.1f); // Warning Yellow
            else hpColor = new Color(1.0f, 0.15f, 0.15f); // Critical Red

            DrawRadialRing(hpRect, hpPercent, hpColor, 0.37f, 0.47f);

            if (heartIconTex != null)
            {
                float iconSize = 24f * scale;
                GUI.color = hpColor;
                GUI.DrawTexture(new Rect(hpRect.center.x - iconSize * 0.5f, hpRect.y + (14f * scale), iconSize, iconSize), heartIconTex);
                GUI.color = Color.white;
            }

            GUIStyle hpNumberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(hpRect.x, hpRect.y + (38f * scale), hpRect.width, 24f * scale), $"{Mathf.RoundToInt(hpPercent * 100f)}%", hpNumberStyle);

            // --- C. SATELLITE DIAL 2: FLASHLIGHT / POWER (Bottom-Left of O2) ---
            float lightDialSize = 75f * scale;
            Rect lightRect = new Rect(clusterBaseX - (20f * scale), clusterBaseY + (45f * scale), lightDialSize, lightDialSize);

            GUI.DrawTexture(lightRect, circleBackTex);

            bool isLightOn = firstPersonCamera != null && firstPersonCamera.IsFlashlightOn;
            Color lightColor = isLightOn
                ? new Color(1.0f, 0.88f, 0.35f, 1.0f) // Warm Golden Glow
                : new Color(0.35f, 0.45f, 0.55f, 0.6f); // Dim Idle

            DrawRadialRing(lightRect, isLightOn ? 1.0f : 0.20f, lightColor, 0.36f, 0.46f);

            if (flashlightIconTex != null)
            {
                float iconSize = 24f * scale;
                GUI.color = lightColor;
                GUI.DrawTexture(new Rect(lightRect.center.x - iconSize * 0.5f, lightRect.y + (12f * scale), iconSize, iconSize), flashlightIconTex);
                GUI.color = Color.white;
            }

            GUIStyle lightTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = lightColor }
            };
            GUI.Label(new Rect(lightRect.x, lightRect.y + (36f * scale), lightRect.width, 20f * scale), isLightOn ? "[F] ON" : "[F] OFF", lightTextStyle);
        }

        // ==========================================
        // TOP CENTER: DEPTH & SONAR NAVIGATION
        // ==========================================
        private void DrawTopDepthAndSonar(float screenW, float screenH)
        {
            float scale = hudScale;
            float centerX = screenW * 0.5f;

            // 1. Calculate depth below water surface
            float diverY = diverTransform != null ? diverTransform.position.y : 0f;
            float depthMeters = Mathf.Max(0f, surfaceWaterElevation - diverY);

            // Backdrop box for high-contrast visibility
            float depthBoxW = 240f * scale;
            float depthBoxH = 50f * scale;
            Rect depthBoxRect = new Rect(centerX - depthBoxW * 0.5f, 18f * scale, depthBoxW, depthBoxH);
            GUI.DrawTexture(depthBoxRect, solidPillTex);

            GUIStyle depthStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.15f, 0.95f, 1.0f) }
            };
            GUI.Label(depthBoxRect, $"{Mathf.RoundToInt(depthMeters)} m", depthStyle);

            // 2. Subnautica curved compass bracket
            if (compassBracketTex != null)
            {
                float bracketW = 280f * scale;
                float bracketH = 46f * scale;
                Rect bracketRect = new Rect(centerX - bracketW * 0.5f, depthBoxRect.yMax - (8f * scale), bracketW, bracketH);
                GUI.DrawTexture(bracketRect, compassBracketTex);
            }

            // 3. Directional Sonar Heading & Distance to Ancient Crystal
            if (scanner != null)
            {
                float sonarBoxW = 340f * scale;
                float sonarBoxH = 30f * scale;
                Rect sonarBoxRect = new Rect(centerX - sonarBoxW * 0.5f, depthBoxRect.yMax + (32f * scale), sonarBoxW, sonarBoxH);
                GUI.DrawTexture(sonarBoxRect, solidDarkTex);

                Color pulseColor = Color.Lerp(new Color(0.2f, 0.85f, 1.0f), Color.white, scanner.PingProgress);
                GUIStyle sonarStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(14 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = pulseColor }
                };

                string beaconText = scanner.IsMalfunctioning
                    ? scanner.DirectionIndicator
                    : $"{scanner.DirectionIndicator}  |  {scanner.CurrentDistance:F0}m";

                GUI.Label(sonarBoxRect, beaconText, sonarStyle);
            }
        }

        // ==========================================
        // CENTER RETICLE & CONTEXT PROMPT
        // ==========================================
        private void DrawCenterReticle(float screenW, float screenH)
        {
            float scale = hudScale;
            float centerX = screenW * 0.5f;
            float centerY = screenH * 0.5f;

            bool isNearCrystal = crystal != null && crystal.IsPlayerInRange;

            // Reticle circle
            if (reticleTex != null)
            {
                float rSize = 34f * scale;
                Rect rRect = new Rect(centerX - rSize * 0.5f, centerY - rSize * 0.5f, rSize, rSize);

                GUI.color = isNearCrystal ? new Color(0.1f, 1f, 0.85f, 1.0f) : new Color(1f, 1f, 1f, 0.75f);
                GUI.DrawTexture(rRect, reticleTex);
                GUI.color = Color.white;
            }

            // Interactive prompt pill
            if (isNearCrystal)
            {
                float promptW = 340f * scale;
                float promptH = 40f * scale;
                Rect promptRect = new Rect(centerX - promptW * 0.5f, centerY + (35f * scale), promptW, promptH);

                GUI.DrawTexture(promptRect, solidPillTex);

                GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(16 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 1.0f, 0.9f) }
                };
                GUI.Label(promptRect, crystal.PromptText, promptStyle);
            }
        }

        // ==========================================
        // MISSION OBJECTIVE BADGE (Top Left)
        // ==========================================
        private void DrawObjectivePill()
        {
            float scale = hudScale;
            float boxW = 420f * scale;
            float boxH = 40f * scale;
            Rect objRect = new Rect(30f * scale, 25f * scale, boxW, boxH);

            GUI.DrawTexture(objRect, solidDarkTex);

            GUIStyle objHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(10 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.2f, 0.8f, 1.0f) }
            };
            GUI.Label(new Rect(objRect.x + 12f * scale, objRect.y + 3f * scale, objRect.width - 24f * scale, 14f * scale), "PRIMARY DIRECTIVE", objHeaderStyle);

            GUIStyle objTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(objRect.x + 12f * scale, objRect.y + 16f * scale, objRect.width - 24f * scale, 20f * scale), currentObjective, objTextStyle);
        }

        // ==========================================
        // CONTROLS GUIDE (Bottom Right)
        // ==========================================
        private void DrawControlsBadge(float screenW, float screenH)
        {
            float scale = hudScale;
            float boxW = 280f * scale;
            float boxH = 34f * scale;
            Rect box = new Rect(screenW - boxW - (30f * scale), screenH - boxH - (25f * scale), boxW, boxH);

            GUI.DrawTexture(box, solidDarkTex);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.75f, 0.88f) }
            };
            GUI.Label(box, "WASD: Swim  |  Space: Ascend  |  C: Descend", style);
        }

        // ==========================================
        // SHADER RADIAL GAUGE HELPER
        // ==========================================
        private void DrawRadialRing(Rect rect, float fillPercent, Color fillColor, float innerRad = 0.37f, float outerRad = 0.47f)
        {
            if (radialRingMaterial != null)
            {
                radialRingMaterial.SetColor("_Color", fillColor);
                radialRingMaterial.SetColor("_BgColor", new Color(fillColor.r * 0.15f, fillColor.g * 0.15f, fillColor.b * 0.15f, 0.28f));
                radialRingMaterial.SetFloat("_Fill", Mathf.Clamp01(fillPercent));
                radialRingMaterial.SetFloat("_InnerRadius", innerRad);
                radialRingMaterial.SetFloat("_OuterRadius", outerRad);

                Graphics.DrawTexture(rect, Texture2D.whiteTexture, radialRingMaterial);
            }
        }

        // ==========================================
        // GAME OVER SCREEN
        // ==========================================
        private void DrawGameOverScreen(float screenW, float screenH)
        {
            Texture2D redTint = MakeTex(2, 2, new Color(0.4f, 0.04f, 0.04f, 0.85f));
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), redTint);

            Rect centerBox = new Rect(screenW * 0.5f - 280f, screenH * 0.5f - 110f, 560f, 220f);
            GUI.DrawTexture(centerBox, solidDarkTex);

            GUIStyle mainTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.25f, 0.25f) }
            };
            GUI.Label(new Rect(centerBox.x, centerBox.y + 20f, centerBox.width, 36f), "SIGNAL TERMINATED", mainTitleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(centerBox.x + 20f, centerBox.y + 70f, centerBox.width - 40f, 40f), endScreenMessage, subStyle);

            if (GUI.Button(new Rect(centerBox.x + 180f, centerBox.y + 135f, 200f, 48f), "RETRY EXPEDITION [R]"))
            {
                RestartGame();
            }
        }
    }
}
