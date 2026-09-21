using UnityEngine;

namespace TideAndTill
{
    /// <summary>
    /// Creates the playable vertical slice without relying on scene-authored references.
    /// This keeps the prototype easy to open: load SampleScene and press Play.
    /// </summary>
    public static class TideAndTillBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            if (Object.FindFirstObjectByType<IslandGame>() != null)
                return;

            var root = new GameObject("TIDE & TILL • Game");
            root.AddComponent<IslandGame>();
        }
    }

    public sealed class IslandGame : MonoBehaviour
    {
        public static IslandGame Instance { get; private set; }

        public GameState State { get; private set; }
        public GameInput Input { get; private set; }
        public FarmSystem Farm { get; private set; }
        public PlayerController Player { get; private set; }
        public DayNightSystem DayNight { get; private set; }
        public GameHUD HUD { get; private set; }
        public Transform WorldRoot { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;

            State = new GameState();
            Input = gameObject.AddComponent<GameInput>();

            WorldRoot = new GameObject("Sunpetal Island").transform;
            WorldRoot.SetParent(transform);
            WorldBuilder.Build(WorldRoot);

            var farmObject = new GameObject("Seabreeze Farm");
            farmObject.transform.SetParent(WorldRoot);
            Farm = farmObject.AddComponent<FarmSystem>();
            Farm.Initialize(State);

            Player = PlayerController.Create(transform, State, Input, Farm);
            SetupCamera(Player.transform);

            DayNight = gameObject.AddComponent<DayNightSystem>();
            DayNight.Initialize(Farm, Player.transform);

            HUD = gameObject.AddComponent<GameHUD>();
            HUD.Initialize(State, DayNight, Player);

            State.ShowToast("Welcome to Sunpetal Island! Till a plot and plant your first moonmelon.");
        }

        public void BeginNextDay()
        {
            DayNight.BeginNextDay();
            State.RestoreForNewDay();
            Player.Teleport(new Vector3(6f, WorldBuilder.SampleHeight(6f, 5f) + 0.15f, 5f));
            State.ShowToast(DayNight.IsRaining
                ? "A soft island rain waters every crop."
                : "A bright new morning begins on the farm.");
        }

        private void SetupCamera(Transform follow)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 240f;
            camera.allowHDR = true;
            camera.backgroundColor = new Color(0.43f, 0.76f, 0.91f);

            var rig = camera.GetComponent<IslandCamera>();
            if (rig == null)
                rig = camera.gameObject.AddComponent<IslandCamera>();
            rig.Initialize(follow, Input);
        }

        private void OnApplicationQuit()
        {
            if (State != null)
                PrototypeSave.Save(State, DayNight, Farm, Player);
        }
    }
}
