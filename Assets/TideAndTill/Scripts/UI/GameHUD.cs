using System;
using UnityEngine;
using UnityEngine.UI;

namespace TideAndTill
{
    public sealed class GameHUD : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.14f, 0.15f, 1f);
        private static readonly Color DeepTeal = new Color(0.035f, 0.22f, 0.23f, 0.96f);
        private static readonly Color Cream = new Color(1f, 0.94f, 0.78f, 1f);
        private static readonly Color Coral = new Color(0.97f, 0.34f, 0.26f, 1f);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.19f, 1f);
        private static readonly Color Muted = new Color(0.71f, 0.82f, 0.72f, 1f);

        private readonly string[] toolNames = { "HOE", "WATER", "SEEDS", "HARVEST", "AXE" };
        private readonly string[] toolMarks = { "HO", "WA", "SE", "HA", "AX" };

        private GameState state;
        private DayNightSystem time;
        private PlayerController player;
        private Font font;
        private Sprite roundedSprite;
        private Canvas canvas;
        private Text dayText;
        private Text timeText;
        private Text weatherText;
        private Text coinsText;
        private Text questText;
        private Text staminaText;
        private Text waterText;
        private Image staminaFill;
        private Image waterFill;
        private readonly Image[] slots = new Image[5];
        private readonly Text[] slotLabels = new Text[5];
        private GameObject promptPanel;
        private Text promptText;
        private GameObject toastPanel;
        private Text toastText;
        private CanvasGroup toastGroup;
        private float toastUntil;

        public void Initialize(GameState gameState, DayNightSystem clock, PlayerController playerController)
        {
            state = gameState;
            time = clock;
            player = playerController;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "Helvetica" }, 18);
            roundedSprite = BuildRoundedSprite();
            BuildCanvas();
            state.Changed += Refresh;
            state.ToastRequested += ShowToast;
            Refresh();
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Tide & Till HUD");
            canvasObject.transform.SetParent(transform);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            BuildQuestCard(canvas.transform);
            BuildClockCard(canvas.transform);
            BuildVitals(canvas.transform);
            BuildToolbar(canvas.transform);
            BuildPrompt(canvas.transform);
            BuildToast(canvas.transform);
            BuildControls(canvas.transform);
        }

        private void BuildQuestCard(Transform parent)
        {
            GameObject panel = CreatePanel("Quest Card", parent, DeepTeal, new Vector2(32f, -30f), new Vector2(575f, 148f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            CreateText("Brand", panel.transform, "TIDE & TILL", 31, FontStyle.Bold, Cream, TextAnchor.UpperLeft,
                new Vector2(22f, -16f), new Vector2(250f, 45f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            CreateText("Location", panel.transform, "SUNPETAL ISLAND  /  SEABREEZE FARM", 15, FontStyle.Bold, Gold, TextAnchor.UpperRight,
                new Vector2(-20f, -24f), new Vector2(310f, 30f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            questText = CreateText("Quest", panel.transform, string.Empty, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft,
                new Vector2(22f, -75f), new Vector2(525f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            CreateText("Quest Caption", panel.transform, "JOURNAL", 13, FontStyle.Bold, Muted, TextAnchor.MiddleLeft,
                new Vector2(22f, -112f), new Vector2(100f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private void BuildClockCard(Transform parent)
        {
            GameObject panel = CreatePanel("Clock Card", parent, DeepTeal, new Vector2(-32f, -30f), new Vector2(360f, 172f),
                new Vector2(1f, 1f), new Vector2(1f, 1f));
            dayText = CreateText("Day", panel.transform, string.Empty, 18, FontStyle.Bold, Gold, TextAnchor.MiddleLeft,
                new Vector2(20f, -15f), new Vector2(220f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            weatherText = CreateText("Weather", panel.transform, string.Empty, 14, FontStyle.Bold, Muted, TextAnchor.MiddleRight,
                new Vector2(-18f, -16f), new Vector2(170f, 32f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            timeText = CreateText("Time", panel.transform, string.Empty, 39, FontStyle.Bold, Cream, TextAnchor.MiddleLeft,
                new Vector2(20f, -52f), new Vector2(250f, 55f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            coinsText = CreateText("Coins", panel.transform, string.Empty, 21, FontStyle.Bold, Color.white, TextAnchor.MiddleRight,
                new Vector2(-20f, -114f), new Vector2(320f, 38f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        }

        private void BuildVitals(Transform parent)
        {
            GameObject panel = CreatePanel("Vitals", parent, new Color(0.035f, 0.16f, 0.17f, 0.93f), new Vector2(32f, 30f), new Vector2(370f, 132f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            staminaText = CreateText("Stamina Label", panel.transform, "ENERGY", 14, FontStyle.Bold, Muted, TextAnchor.MiddleLeft,
                new Vector2(18f, -15f), new Vector2(110f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            staminaFill = BuildBar(panel.transform, new Vector2(18f, -47f), new Vector2(334f, 18f), Gold);
            waterText = CreateText("Water Label", panel.transform, "WATERING CAN", 12, FontStyle.Bold, Muted, TextAnchor.MiddleLeft,
                new Vector2(18f, -75f), new Vector2(334f, 25f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            waterFill = BuildBar(panel.transform, new Vector2(18f, -105f), new Vector2(334f, 14f), new Color(0.18f, 0.69f, 0.88f));
        }

        private Image BuildBar(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject back = CreatePanel("Bar Background", parent, new Color(0f, 0f, 0f, 0.42f), position, size,
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            GameObject fillObject = CreatePanel("Fill", back.transform, color, Vector2.zero, Vector2.zero,
                new Vector2(0f, 0f), new Vector2(1f, 1f));
            RectTransform rect = fillObject.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            Image image = fillObject.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            return image;
        }

        private void BuildToolbar(Transform parent)
        {
            GameObject panel = CreatePanel("Toolbelt", parent, new Color(0.025f, 0.14f, 0.15f, 0.96f), new Vector2(0f, 28f), new Vector2(790f, 118f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            float start = -292f;
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject slot = CreatePanel($"Tool {i + 1}", panel.transform, new Color(0.13f, 0.30f, 0.29f, 1f),
                    new Vector2(start + i * 146f, 12f), new Vector2(134f, 94f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                slots[i] = slot.GetComponent<Image>();
                CreateText("Key", slot.transform, (i + 1).ToString(), 13, FontStyle.Bold, Muted, TextAnchor.UpperLeft,
                    new Vector2(10f, -7f), new Vector2(24f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                CreateText("Mark", slot.transform, toolMarks[i], 25, FontStyle.Bold, Cream, TextAnchor.MiddleCenter,
                    new Vector2(0f, -7f), new Vector2(70f, 45f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                slotLabels[i] = CreateText("Label", slot.transform, toolNames[i], 14, FontStyle.Bold, Color.white, TextAnchor.LowerCenter,
                    new Vector2(0f, 9f), new Vector2(125f, 30f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            }
        }

        private void BuildPrompt(Transform parent)
        {
            promptPanel = CreatePanel("Interaction Prompt", parent, new Color(0.02f, 0.12f, 0.13f, 0.94f), new Vector2(0f, 173f), new Vector2(470f, 52f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            promptText = CreateText("Prompt", promptPanel.transform, string.Empty, 18, FontStyle.Bold, Cream, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            Stretch(promptText.rectTransform, 8f);
            promptPanel.SetActive(false);
        }

        private void BuildToast(Transform parent)
        {
            toastPanel = CreatePanel("Toast", parent, new Color(0.96f, 0.88f, 0.65f, 0.98f), new Vector2(0f, -210f), new Vector2(720f, 72f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            toastText = CreateText("Toast Message", toastPanel.transform, string.Empty, 20, FontStyle.Bold, Ink, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            Stretch(toastText.rectTransform, 15f);
            toastGroup = toastPanel.AddComponent<CanvasGroup>();
            toastPanel.SetActive(false);
        }

        private void BuildControls(Transform parent)
        {
            Text controls = CreateText("Controls", parent,
                "WASD  MOVE    SHIFT  SPRINT    SPACE / CLICK  USE TOOL    E  INTERACT    1–5 / WHEEL  TOOLS    RMB  ORBIT",
                13, FontStyle.Bold, new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleRight,
                new Vector2(-30f, -218f), new Vector2(1040f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            var outline = controls.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.62f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void Update()
        {
            if (time == null) return;
            dayText.text = time.DayLabel;
            timeText.text = time.TimeLabel;
            weatherText.text = time.WeatherLabel;

            string prompt = player == null ? string.Empty : player.InteractionPrompt;
            bool showPrompt = !string.IsNullOrEmpty(prompt);
            if (promptPanel.activeSelf != showPrompt) promptPanel.SetActive(showPrompt);
            if (showPrompt) promptText.text = prompt;

            if (toastPanel.activeSelf)
            {
                float remaining = toastUntil - Time.unscaledTime;
                if (remaining <= 0f)
                {
                    toastPanel.SetActive(false);
                }
                else
                {
                    toastGroup.alpha = Mathf.Clamp01(remaining * 2.5f);
                }
            }
        }

        private void Refresh()
        {
            if (state == null) return;
            coinsText.text = $"◆  {state.Coins:N0} SHELLS";
            questText.text = state.QuestText;
            staminaText.text = $"ENERGY   {Mathf.CeilToInt(state.Stamina)} / {state.MaxStamina}";
            waterText.text = $"WATER {state.Water}/{state.MaxWater}     SEEDS {state.Seeds}     PRODUCE {state.Produce}";
            staminaFill.fillAmount = state.Stamina / state.MaxStamina;
            waterFill.fillAmount = state.Water / (float)state.MaxWater;

            for (int i = 0; i < slots.Length; i++)
            {
                bool selected = i == (int)state.SelectedTool;
                slots[i].color = selected ? Coral : new Color(0.13f, 0.30f, 0.29f, 1f);
                slotLabels[i].color = selected ? Cream : Color.white;
            }
        }

        private void ShowToast(string message)
        {
            if (toastPanel == null) return;
            toastText.text = message;
            toastUntil = Time.unscaledTime + 4.2f;
            toastGroup.alpha = 1f;
            toastPanel.SetActive(true);
        }

        private GameObject CreatePanel(string name, Transform parent, Color color, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMax.x, anchorMax.y);
            if (anchorMin == anchorMax && anchorMin.x == 0.5f) rect.pivot = new Vector2(0.5f, anchorMin.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = item.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return item;
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment,
            Vector2 position, Vector2 dimensions, Vector2 anchorMin, Vector2 anchorMax)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMax.x, anchorMax.y);
            if (anchorMin != anchorMax) rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 64;
            const float radius = 15f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Rounded UI",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - ((size - 1) * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - ((size - 1) * 0.5f - radius), 0f);
                    float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
        }

        private void OnDestroy()
        {
            if (state == null) return;
            state.Changed -= Refresh;
            state.ToastRequested -= ShowToast;
        }
    }
}
