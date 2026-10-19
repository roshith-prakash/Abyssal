using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.Gameplay;

namespace Abyssal.UI
{
    /// <summary>
    /// Comprehensive HUD and UI manager for ABYSSAL.
    /// Renders the scanner readout, health bar, interaction prompts,
    /// dynamic objectives, and Win/Loss screens.
    /// Uses high-performance, robust OnGUI with modern styling so it works
    /// instantly with zero external Canvas dependencies, while exposing
    /// events for Canvas components if preferred.
    /// </summary>
    public class GameHUDManager : MonoBehaviour
    {
        [Header("State References")]
        [SerializeField] private DiverHealth diverHealth;
        [SerializeField] private ScannerHUD scanner;
        [SerializeField] private CrystalInteractable crystal;

        [Header("Objective Display")]
        [SerializeField] private string currentObjective = "Descend into the abyss and locate the anomalous signal.";

        private bool isGameOver = false;
        private string endScreenMessage = "";

        private Texture2D barBackgroundTex;
        private Texture2D barHealthTex;
        private Texture2D darkBoxTex;
        private Texture2D dangerBoxTex;

        private void Awake()
        {
            CreateTextures();
        }

        private void Start()
        {
            ResolveReferences();

            if (diverHealth != null)
            {
                diverHealth.OnDiverDied += HandleDiverDied;
            }

            if (LeviathanEncounter.Instance != null)
            {
                LeviathanEncounter.Instance.OnChaseStarted += () =>
                {
                    currentObjective = "CRITICAL ALERT: LEVIATHAN TERRITORY DETECTED!";
                };
            }
        }

        private void Update()
        {
            if (diverHealth == null || scanner == null)
            {
                ResolveReferences();
            }

            // Quick restart on game over
            if (isGameOver)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    RestartGame();
                }
            }
        }

        private void ResolveReferences()
        {
            if (diverHealth == null) diverHealth = FindAnyObjectByType<DiverHealth>();
            if (scanner == null) scanner = FindAnyObjectByType<ScannerHUD>();
            if (crystal == null) crystal = FindAnyObjectByType<CrystalInteractable>();
        }

        private void CreateTextures()
        {
            barBackgroundTex = MakeTex(2, 2, new Color(0.08f, 0.12f, 0.18f, 0.85f));
            barHealthTex = MakeTex(2, 2, new Color(0.15f, 0.75f, 0.95f, 0.95f));
            darkBoxTex = MakeTex(2, 2, new Color(0.03f, 0.06f, 0.10f, 0.82f));
            dangerBoxTex = MakeTex(2, 2, new Color(0.6f, 0.08f, 0.08f, 0.85f));
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void HandleDiverDied()
        {
            ShowGameOverScreen("CRITICAL INTEGRITY FAILURE — DIVER LOST TO THE ABYSS");
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
            // Set base styling
            GUI.skin.label.fontSize = 14;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. GAME OVER OVERLAY (Cinematic scene: only ends if diver is lost/consumed)
            if (isGameOver)
            {
                DrawEndScreen(screenW, screenH);
                return;
            }

            // 2. TOP BANNER: Title & Current Objective
            Rect topBox = new Rect(screenW * 0.5f - 240f, 15f, 480f, 52f);
            GUI.DrawTexture(topBox, darkBoxTex);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.8f, 1.0f) }
            };
            GUI.Label(new Rect(topBox.x, topBox.y + 4f, topBox.width, 18f), "ABYSSAL — DEEP SEA RESEARCH PROTOCOL", titleStyle);

            GUIStyle objStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(topBox.x, topBox.y + 24f, topBox.width, 22f), currentObjective, objStyle);

            // 3. SCANNER DISPLAY (Top Center / Under Objective)
            if (scanner != null)
            {
                Rect scannerBox = new Rect(screenW * 0.5f - 210f, 74f, 420f, 62f);
                GUI.DrawTexture(scannerBox, darkBoxTex);

                // Ping pulse indicator
                Color pulseCol = Color.Lerp(new Color(0.2f, 0.9f, 1.0f), Color.white, scanner.PingProgress);
                GUIStyle pulseStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = pulseCol }
                };
                GUI.Label(new Rect(scannerBox.x + 15f, scannerBox.y + 8f, 240f, 22f), $"SCANNER: {scanner.DirectionIndicator}", pulseStyle);

                GUIStyle distStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.4f, 0.95f, 0.6f) }
                };
                GUI.Label(new Rect(scannerBox.x + 230f, scannerBox.y + 8f, 175f, 22f), $"DIST: {scanner.CurrentDistance:F0}m", distStyle);

                GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.75f, 0.85f, 0.95f) }
                };
                GUI.Label(new Rect(scannerBox.x, scannerBox.y + 34f, scannerBox.width, 20f), scanner.StatusMessage, statusStyle);
            }

            // 4. HEALTH BAR (Bottom Left)
            if (diverHealth != null)
            {
                Rect hpContainer = new Rect(25f, screenH - 65f, 240f, 40f);
                GUI.DrawTexture(hpContainer, darkBoxTex);

                GUIStyle hpLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.7f, 0.9f, 1.0f) }
                };
                GUI.Label(new Rect(hpContainer.x + 10f, hpContainer.y + 3f, 200f, 16f), $"SUIT INTEGRITY: {diverHealth.CurrentHealth:F0}/{diverHealth.MaxHealth:F0} HP", hpLabelStyle);

                // Bar
                Rect hpBarBg = new Rect(hpContainer.x + 10f, hpContainer.y + 21f, 220f, 12f);
                GUI.DrawTexture(hpBarBg, barBackgroundTex);

                float hpPercent = Mathf.Clamp01(diverHealth.CurrentHealth / diverHealth.MaxHealth);
                Rect hpBarFill = new Rect(hpBarBg.x, hpBarBg.y, hpBarBg.width * hpPercent, hpBarBg.height);
                GUI.DrawTexture(hpBarFill, barHealthTex);
            }

            // 5. INTERACTION PROMPT (Center Screen)
            if (crystal != null && crystal.IsPlayerInRange)
            {
                Rect promptBox = new Rect(screenW * 0.5f - 160f, screenH * 0.5f + 40f, 320f, 36f);
                GUI.DrawTexture(promptBox, darkBoxTex);

                GUIStyle pStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.3f, 1.0f, 0.9f) }
                };
                GUI.Label(promptBox, crystal.PromptText, pStyle);
            }

            // 6. CONTROLS HELPER (Bottom Right)
            Rect controlsBox = new Rect(screenW - 240f, screenH - 65f, 215f, 40f);
            GUI.DrawTexture(controlsBox, darkBoxTex);
            GUIStyle ctrlStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.7f, 0.8f) }
            };
            GUI.Label(controlsBox, "WASD: Swim | Space/C: Ascend/Descend\nShift: Sprint | F: Flashlight | E: Interact", ctrlStyle);
        }

        private void DrawEndScreen(float screenW, float screenH)
        {
            // Dimmed background
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), dangerBoxTex);

            Rect centerBox = new Rect(screenW * 0.5f - 300f, screenH * 0.5f - 110f, 600f, 220f);
            GUI.DrawTexture(centerBox, darkBoxTex);

            GUIStyle mainTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.25f, 0.25f) }
            };
            GUI.Label(new Rect(centerBox.x, centerBox.y + 20f, centerBox.width, 36f), "SIGNAL TERMINATED", mainTitleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(centerBox.x + 20f, centerBox.y + 65f, centerBox.width - 40f, 40f), endScreenMessage, subStyle);

            if (GUI.Button(new Rect(centerBox.x + 200f, centerBox.y + 130f, 200f, 45f), "RETRY EXPEDITION [R]"))
            {
                RestartGame();
            }
        }
    }
}
