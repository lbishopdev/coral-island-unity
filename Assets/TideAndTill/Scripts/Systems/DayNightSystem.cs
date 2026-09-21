using System;
using UnityEngine;

namespace TideAndTill
{
    public sealed class DayNightSystem : MonoBehaviour
    {
        private FarmSystem farm;
        private Transform player;
        private Light sun;
        private ParticleSystem rain;
        private Transform rainTransform;
        private float minutes = 8f * 60f;

        public int Day { get; private set; } = 1;
        public bool IsRaining { get; private set; }
        public float Minutes => minutes;
        public string DayLabel => $"DAY {Day}  ·  {SeasonName}";
        public string SeasonName => "EARLY SUMMER";
        public string WeatherLabel => IsRaining ? "ISLAND RAIN" : "SUNNY BREEZE";
        public string TimeLabel
        {
            get
            {
                int hour24 = Mathf.FloorToInt(minutes / 60f) % 24;
                int minute = Mathf.FloorToInt(minutes % 60f / 10f) * 10;
                string suffix = hour24 >= 12 ? "PM" : "AM";
                int hour12 = hour24 % 12;
                if (hour12 == 0) hour12 = 12;
                return $"{hour12}:{minute:00} {suffix}";
            }
        }

        public void Initialize(FarmSystem farmSystem, Transform playerTransform)
        {
            farm = farmSystem;
            player = playerTransform;
            sun = RenderSettings.sun;
            BuildRain();
            UpdateLighting();
        }

        private void Update()
        {
            // One real second advances three island minutes: a full playable day is about five minutes.
            minutes += Time.deltaTime * 3f;
            if (minutes >= 24f * 60f)
            {
                minutes = 23f * 60f + 50f;
                IslandGame.Instance.BeginNextDay();
            }
            UpdateLighting();
        }

        private void LateUpdate()
        {
            if (rainTransform != null && player != null)
                rainTransform.position = player.position + Vector3.up * 11f;
        }

        public void BeginNextDay()
        {
            Day++;
            minutes = 7f * 60f + 30f;
            // A deterministic cadence ensures players see weather within the short prototype.
            IsRaining = Day % 3 == 2;
            farm.AdvanceDay(IsRaining);
            if (rain != null)
            {
                if (IsRaining && !rain.isPlaying) rain.Play();
                if (!IsRaining && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            UpdateLighting();
        }

        private void UpdateLighting()
        {
            if (sun == null) return;
            float day01 = minutes / (24f * 60f);
            float daylight = Mathf.Clamp01(Mathf.Sin((day01 - 0.25f) * Mathf.PI * 2f) * 0.92f + 0.12f);
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(minutes - 18f * 60f) / 130f);
            float sunrise = Mathf.Clamp01(1f - Mathf.Abs(minutes - 6.2f * 60f) / 100f);
            float golden = Mathf.Max(sunset, sunrise);

            sun.transform.rotation = Quaternion.Euler(day01 * 360f - 90f, -32f, 0f);
            sun.intensity = Mathf.Lerp(0.05f, IsRaining ? 0.78f : 1.42f, daylight);
            sun.color = Color.Lerp(new Color(0.55f, 0.62f, 0.86f), new Color(1f, 0.93f, 0.78f), daylight);
            sun.color = Color.Lerp(sun.color, new Color(1f, 0.49f, 0.24f), golden * 0.72f);

            Color dayFog = IsRaining ? new Color(0.38f, 0.55f, 0.59f) : new Color(0.53f, 0.78f, 0.85f);
            Color nightFog = new Color(0.055f, 0.10f, 0.18f);
            RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.06f, 0.09f, 0.18f),
                IsRaining ? new Color(0.35f, 0.46f, 0.49f) : new Color(0.57f, 0.77f, 0.82f), daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.04f, 0.065f, 0.10f), new Color(0.42f, 0.55f, 0.50f), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.025f, 0.04f, 0.055f), new Color(0.17f, 0.22f, 0.16f), daylight);

            Camera camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = Color.Lerp(new Color(0.035f, 0.07f, 0.16f), dayFog, daylight);
        }

        private void BuildRain()
        {
            var rainObject = new GameObject("Local Rain");
            rainObject.transform.SetParent(transform);
            rainTransform = rainObject.transform;
            rain = rainObject.AddComponent<ParticleSystem>();

            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.15f;
            main.startSpeed = 19f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.66f, 0.84f, 1f, 0.62f);
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = rain.emission;
            emission.rateOverTime = 520f;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 0.2f, 24f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = -1.4f;
            velocity.y = -13f;

            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 7f;
            renderer.velocityScale = 0.12f;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                var material = new Material(shader) { name = "Rain Streak Material", color = new Color(0.65f, 0.82f, 1f, 0.6f) };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color);
                renderer.sharedMaterial = material;
            }
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>Minimal local persistence hook; the world remains safe to iterate without a save-file schema.</summary>
    public static class PrototypeSave
    {
        private const string SaveKey = "TideAndTill.PrototypeSave";

        [Serializable]
        private sealed class SaveData
        {
            public int coins;
            public int seeds;
            public int produce;
            public int water;
            public float stamina;
            public int day;
            public float minutes;
            public Vector3 playerPosition;
        }

        public static void Save(GameState state, DayNightSystem time, FarmSystem farm, PlayerController player)
        {
            if (state == null || time == null || player == null) return;
            var data = new SaveData
            {
                coins = state.Coins,
                seeds = state.Seeds,
                produce = state.Produce,
                water = state.Water,
                stamina = state.Stamina,
                day = time.Day,
                minutes = time.Minutes,
                playerPosition = player.transform.position
            };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
