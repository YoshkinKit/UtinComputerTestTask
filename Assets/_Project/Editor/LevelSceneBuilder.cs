using System.Collections.Generic;
using System.IO;
using Game.Cameras;
using Game.Config;
using Game.Core;
using Game.Inputs;
using Game.Level;
using Game.Obstacles;
using Game.Player;
using Game.Shooting;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Полностью пересобирает игровую сцену из кода: конфиги, материалы, префабы, иерархия,
    /// связи между компонентами и сгенерированный уровень. Ручной расстановки в сцене нет
    /// вообще — любое изменение баланса или формы уровня применяется повторным запуском
    /// «Game → Build Level Scene», а результат воспроизводим по сиду из <see cref="LevelConfig"/>.
    /// </summary>
    public static class LevelSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LegacyScenePath = "Assets/_Project/Scenes/SampleScene.unity";
        internal const string ConfigFolder = "Assets/_Project/Settings/Configs";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        private const string GameplayRootName = "Gameplay";
        private const string GroundName = "Ground";
        private const string UiRootName = "UI";
        private const string EventSystemName = "EventSystem";
        private const string InputActionsPath = "Assets/_Project/InputSystem_Actions.inputactions";

        [MenuItem("Game/Build Level Scene", priority = 0)]
        public static void BuildLevelScene()
        {
            EnsureFolders();

            PlayerConfig playerConfig = LoadOrCreate<PlayerConfig>("PlayerConfig");
            ShotConfig shotConfig = LoadOrCreate<ShotConfig>("ShotConfig");
            InfectionConfig infectionConfig = LoadOrCreate<InfectionConfig>("InfectionConfig");
            LevelConfig levelConfig = LoadOrCreate<LevelConfig>("LevelConfig");

            Palette palette = LoadOrCreatePalette();
            GameObject obstaclePrefab = BuildObstaclePrefab(palette);
            GameObject shotPrefab = BuildShotPrefab(palette);

            Scene scene = OpenGameScene();
            ClearGeneratedRoots();

            LevelLayout layout = LevelBuilder.Build(levelConfig, infectionConfig.MaxNeighborGap);

            var root = new GameObject(GameplayRootName);
            CreateGround(layout, palette);

            PlayerBall player = CreatePlayer(root.transform, playerConfig, palette);
            LevelPath path = CreatePath(root.transform, layout);
            ObstacleField field = CreateObstacles(root.transform, layout, obstaclePrefab, infectionConfig, palette);
            DoorController door = CreateDoor(root.transform, layout, player.transform, palette);
            Transform shotPool = new GameObject("Shot Pool").transform;
            shotPool.SetParent(root.transform, false);

            CameraFollow follow = SetUpCamera(layout, player.transform);

            Systems systems = CreateSystems(root.transform, layout, playerConfig, shotConfig, infectionConfig,
                player, path, field, door, shotPool, shotPrefab, follow);

            CreateUi(playerConfig, player, systems);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings();

            AssetDatabase.SaveAssets();
            LogSummary(layout, levelConfig, playerConfig, infectionConfig);
        }

        // ------------------------------------------------------------------ сцена

        private static Scene OpenGameScene()
        {
            if (!File.Exists(ScenePath) && File.Exists(LegacyScenePath))
            {
                EditorSceneManager.SaveOpenScenes();
                string error = AssetDatabase.RenameAsset(LegacyScenePath, "Game");
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"Не удалось переименовать сцену: {error}");
                }

                AssetDatabase.Refresh();
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                return active;
            }

            return File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        private static void ClearGeneratedRoots()
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == GameplayRootName || go.name == GroundName ||
                    go.name == UiRootName || go.name == EventSystemName)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void RegisterSceneInBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------------ объекты сцены

        private static void CreateGround(LevelLayout layout, Palette palette)
        {
            Bounds bounds = ComputeBounds(layout);
            GameObject ground = CreatePrimitive(PrimitiveType.Plane, GroundName, palette.Ground);

            // Примитив Plane при масштабе 1 занимает 10x10 метров.
            ground.transform.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
            ground.transform.localScale = new Vector3(bounds.size.x / 10f, 1f, bounds.size.z / 10f);
        }

        private static PlayerBall CreatePlayer(Transform parent, PlayerConfig playerConfig, Palette palette)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(parent, false);

            GameObject visual = CreatePrimitive(PrimitiveType.Sphere, "Visual", palette.Player);
            visual.transform.SetParent(root.transform, false);

            PlayerBall ball = root.AddComponent<PlayerBall>();
            PlayerBallView view = root.AddComponent<PlayerBallView>();

            Wire(view, ("visualRoot", visual.transform));
            Wire(ball, ("view", view));

            view.SetRadius(playerConfig.StartRadius);
            root.transform.position = new Vector3(0f, playerConfig.StartRadius, 0f);

            return ball;
        }

        private static LevelPath CreatePath(Transform parent, LevelLayout layout)
        {
            var root = new GameObject("Path");
            root.transform.SetParent(parent, false);

            LevelPath path = root.AddComponent<LevelPath>();

            for (int i = 0; i < layout.PathPoints.Length; i++)
            {
                var point = new GameObject($"Point {i:00}");
                point.transform.SetParent(root.transform, false);
                point.transform.position = layout.PathPoints[i];
            }

            path.SetCorridorWidths(layout.CorridorWidths);
            Wire(path, ("fallbackCorridorWidth", layout.CorridorWidths[layout.CorridorWidths.Length - 1]));
            EditorUtility.SetDirty(path);

            return path;
        }

        private static ObstacleField CreateObstacles(Transform parent, LevelLayout layout, GameObject prefab,
            InfectionConfig infectionConfig, Palette palette)
        {
            var root = new GameObject("Obstacles");
            root.transform.SetParent(parent, false);

            ObstacleField field = root.AddComponent<ObstacleField>();
            Wire(field, ("infectionConfig", infectionConfig));

            for (int i = 0; i < layout.Obstacles.Length; i++)
            {
                ObstacleSpec spec = layout.Obstacles[i];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = $"{spec.Role} {i:000}";
                instance.transform.position = spec.Position;

                Obstacle obstacle = instance.GetComponent<Obstacle>();
                obstacle.Configure(spec.Radius);

                Material material = spec.Role == ObstacleRole.CorridorWall ? palette.Wall : palette.Blocker;
                instance.GetComponentInChildren<Renderer>().sharedMaterial = material;

                EditorUtility.SetDirty(obstacle);
            }

            return field;
        }

        private static DoorController CreateDoor(Transform parent, LevelLayout layout, Transform player, Palette palette)
        {
            var root = new GameObject("Door");
            root.transform.SetParent(parent, false);
            root.transform.position = layout.DoorPosition;
            root.transform.rotation = Quaternion.LookRotation(layout.DoorForward, Vector3.up);

            float opening = layout.CorridorWidths[layout.DoorPointIndex];
            float panelWidth = opening * 0.5f;
            const float height = 2.4f;
            const float thickness = 0.25f;
            const float postWidth = 0.3f;

            Transform left = CreateDoorPanel(root.transform, "Panel Left", palette.DoorPanel,
                new Vector3(-panelWidth * 0.5f, height * 0.5f, 0f), new Vector3(panelWidth, height, thickness));
            Transform right = CreateDoorPanel(root.transform, "Panel Right", palette.DoorPanel,
                new Vector3(panelWidth * 0.5f, height * 0.5f, 0f), new Vector3(panelWidth, height, thickness));

            CreateDoorPanel(root.transform, "Post Left", palette.DoorFrame,
                new Vector3(-(opening + postWidth) * 0.5f, height * 0.5f, 0f),
                new Vector3(postWidth, height, thickness * 1.6f));
            CreateDoorPanel(root.transform, "Post Right", palette.DoorFrame,
                new Vector3((opening + postWidth) * 0.5f, height * 0.5f, 0f),
                new Vector3(postWidth, height, thickness * 1.6f));
            CreateDoorPanel(root.transform, "Lintel", palette.DoorFrame,
                new Vector3(0f, height + postWidth * 0.5f, 0f),
                new Vector3(opening + postWidth * 2f, postWidth, thickness * 1.6f));

            DoorView view = root.AddComponent<DoorView>();
            DoorController controller = root.AddComponent<DoorController>();

            Wire(view,
                ("leftPanel", left),
                ("rightPanel", right),
                ("leftOpenLocalOffset", new Vector3(-panelWidth, 0f, 0f)),
                ("rightOpenLocalOffset", new Vector3(panelWidth, 0f, 0f)));

            Wire(controller,
                ("player", player),
                ("view", view),
                ("openDistance", 5f),
                ("openDuration", 0.6f));

            return controller;
        }

        private static Transform CreateDoorPanel(Transform parent, string name, Material material,
            Vector3 localPosition, Vector3 size)
        {
            GameObject panel = CreatePrimitive(PrimitiveType.Cube, name, material);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localScale = size;
            return panel.transform;
        }

        /// <summary>Компоненты-системы, которые нужны сборщику UI после создания сцены.</summary>
        private readonly struct Systems
        {
            public readonly GameController Controller;
            public readonly ChargeController Charge;

            public Systems(GameController controller, ChargeController charge)
            {
                Controller = controller;
                Charge = charge;
            }
        }

        private static Systems CreateSystems(Transform parent, LevelLayout layout,
            PlayerConfig playerConfig, ShotConfig shotConfig, InfectionConfig infectionConfig,
            PlayerBall player, LevelPath path, ObstacleField field, DoorController door,
            Transform shotPool, GameObject shotPrefab, CameraFollow follow)
        {
            var root = new GameObject("Systems");
            root.transform.SetParent(parent, false);

            root.AddComponent<ApplicationSettings>();

            InputReader input = root.AddComponent<InputReader>();
            ShotLauncher launcher = root.AddComponent<ShotLauncher>();
            PlayerMover mover = root.AddComponent<PlayerMover>();
            ChargeController charge = root.AddComponent<ChargeController>();
            GameController controller = root.AddComponent<GameController>();
            GameFeedback feedback = root.AddComponent<GameFeedback>();

            Wire(launcher,
                ("projectilePrefab", shotPrefab.GetComponent<ShotProjectile>()),
                ("poolParent", shotPool),
                ("shotConfig", shotConfig),
                ("infectionConfig", infectionConfig),
                ("player", player),
                ("obstacleField", field),
                ("target", door.transform));

            Wire(mover,
                ("player", player),
                ("playerConfig", playerConfig),
                ("levelPath", path),
                ("obstacleField", field));

            Wire(charge,
                ("inputReader", input),
                ("gameController", controller),
                ("player", player),
                ("playerView", player.GetComponent<PlayerBallView>()),
                ("playerConfig", playerConfig),
                ("shotConfig", shotConfig),
                ("shotLauncher", launcher),
                ("target", door.transform));

            Wire(feedback,
                ("shotLauncher", launcher),
                ("cameraFollow", follow));

            Wire(controller,
                ("playerConfig", playerConfig),
                ("infectionConfig", infectionConfig),
                ("player", player),
                ("chargeController", charge),
                ("mover", mover),
                ("shotLauncher", launcher),
                ("obstacleField", field),
                ("levelPath", path),
                ("door", door));

            player.transform.position = new Vector3(layout.PlayerStart.x, playerConfig.StartRadius, layout.PlayerStart.z);

            return new Systems(controller, charge);
        }

        private static CameraFollow SetUpCamera(LevelLayout layout, Transform player)
        {
            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }

            Vector3 axis = layout.PathPoints[layout.DoorPointIndex] - layout.PlayerStart;
            axis.y = 0f;
            Vector3 forward = axis.sqrMagnitude < 1e-6f ? Vector3.forward : axis.normalized;

            CameraFollow follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<CameraFollow>();
            }

            Wire(follow,
                ("target", player),
                ("offset", -forward * 10f + Vector3.up * 8f),
                ("lookOffset", forward * 5f),
                ("damping", 5f));

            follow.SnapToTarget();
            camera.farClipPlane = 200f;

            return follow;
        }

        // ------------------------------------------------------------------ UI

        /// <summary>
        /// Собирает Canvas с HUD и экраном итога. Все графики HUD помечаются
        /// <c>raycastTarget = false</c>: иначе полоски перехватывали бы тапы и
        /// <see cref="InputReader"/> (он игнорирует нажатия над UI) не давал бы стрелять
        /// по половине экрана.
        /// </summary>
        private static void CreateUi(PlayerConfig playerConfig, PlayerBall player, Systems systems)
        {
            var canvasGo = new GameObject(UiRootName, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Скин UI лежит не в builtin-ресурсах, а в builtin extra: через Resources он не
            // грузится. Если его нет — панели просто станут прямоугольниками без скруглений.
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            CreateHud(canvasGo.transform, font, sprite, playerConfig, player, systems);
            CreateResultScreen(canvasGo.transform, font, sprite, systems);
            CreateEventSystem();
        }

        private static void CreateHud(Transform parent, Font font, Sprite sprite,
            PlayerConfig playerConfig, PlayerBall player, Systems systems)
        {
            RectTransform hud = CreateStretch("HUD", parent);
            HudView view = hud.gameObject.AddComponent<HudView>();

            var barBackground = new Color(1f, 1f, 1f, 0.14f);

            RectTransform massRect = CreateRect("Mass Bar", hud, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(560f, 46f));
            Slider massBar = CreateBar(massRect, sprite, barBackground, Color.white, 1f, out Image massFill);

            RectTransform massLabelRect = CreateRect("Mass Label", hud, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -96f), new Vector2(560f, 44f));
            Text massLabel = AddText(massLabelRect, font, "Размер", 32, TextAnchor.MiddleLeft,
                new Color(1f, 1f, 1f, 0.85f));

            RectTransform charge = CreateRect("Charge", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 250f), new Vector2(680f, 110f));
            CanvasGroup chargeGroup = charge.gameObject.AddComponent<CanvasGroup>();
            chargeGroup.alpha = 0f;

            RectTransform chargeCaption = CreateRect("Caption", charge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(680f, 40f));
            AddText(chargeCaption, font, "Заряд выстрела", 30, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.8f));

            RectTransform chargeRect = CreateRect("Bar", charge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(680f, 44f));
            Slider chargeBar = CreateBar(chargeRect, sprite, barBackground,
                new Color(1f, 0.62f, 0.20f), 0f, out _);

            RectTransform hint = CreateRect("Hint", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(920f, 90f));
            CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            AddText(Inset(CreateStretch("Label", hint), 0f), font,
                "Удерживай палец — шар перекачивается в выстрел.\nОтпусти — выстрел летит к двери.",
                30, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));

            Wire(view,
                ("player", player),
                ("playerConfig", playerConfig),
                ("chargeController", systems.Charge),
                ("gameController", systems.Controller),
                ("massBar", massBar),
                ("massFill", massFill),
                ("massLabel", massLabel),
                ("chargeGroup", chargeGroup),
                ("chargeBar", chargeBar),
                ("hintGroup", hintGroup));
        }

        private static void CreateResultScreen(Transform parent, Font font, Sprite sprite, Systems systems)
        {
            RectTransform root = CreateStretch("Result", parent);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ResultView view = root.gameObject.AddComponent<ResultView>();

            // Затемнение перехватывает тапы: пока висит итог, стрелять уже нельзя.
            AddImage(CreateStretch("Dim", root), null, new Color(0f, 0f, 0f, 0.72f)).raycastTarget = true;

            RectTransform panel = CreateRect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(880f, 540f));
            AddImage(panel, sprite, new Color(0.12f, 0.13f, 0.16f, 0.98f)).raycastTarget = true;

            RectTransform titleRect = CreateRect("Title", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 160f), new Vector2(820f, 110f));
            Text title = AddText(titleRect, font, "Победа", 72, TextAnchor.MiddleCenter, Color.white);

            RectTransform subtitleRect = CreateRect("Subtitle", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f), new Vector2(780f, 180f));
            Text subtitle = AddText(subtitleRect, font, "", 34, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.85f));

            RectTransform buttonRect = CreateRect("Restart", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -160f), new Vector2(440f, 110f));
            Image buttonImage = AddImage(buttonRect, sprite, new Color(0.25f, 0.55f, 0.95f));
            buttonImage.raycastTarget = true;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            AddText(Inset(CreateStretch("Label", buttonRect), 0f), font, "Заново", 42,
                TextAnchor.MiddleCenter, Color.white);

            Wire(view,
                ("gameController", systems.Controller),
                ("group", group),
                ("title", title),
                ("subtitle", subtitle),
                ("restartButton", button),
                ("panel", panel));
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject(EventSystemName, typeof(EventSystem), typeof(InputSystemUIInputModule));

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions != null)
            {
                go.GetComponent<InputSystemUIInputModule>().actionsAsset = actions;
            }
        }

        /// <summary>
        /// Полоса-индикатор на Slider без ручки.
        /// <para/>
        /// Именно Slider, а не Image с <c>Type.Filled</c>: Filled и Sliced взаимоисключающи, и при
        /// заливке через <c>fillAmount</c> 9-слайс отключается — скруглённые торцы спрайта
        /// растягиваются на всю ширину полосы, а потом обрезаются посередине. Slider же меняет
        /// размер самого прямоугольника заливки, поэтому спрайт остаётся нарезанным и торцы
        /// выглядят одинаково при любом заполнении.
        /// <para/>
        /// Слайдер выключен и не интерактивен, все графики — не raycast target: это индикатор,
        /// а не элемент управления, и он не должен перехватывать тапы у геймплея.
        /// </summary>
        private static Slider CreateBar(RectTransform root, Sprite sprite, Color backgroundColor,
            Color fillColor, float initialValue, out Image fill)
        {
            AddImage(root, sprite, backgroundColor);

            RectTransform fillArea = Inset(CreateStretch("Fill Area", root), 4f);
            RectTransform fillRect = CreateStretch("Fill", fillArea);
            fill = AddImage(fillRect, sprite, fillColor);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.value = initialValue;

            return slider;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform CreateStretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Inset(RectTransform rect, float padding)
        {
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(RectTransform rect, Font font, string content, int fontSize,
            TextAnchor alignment, Color color)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ------------------------------------------------------------------ префабы и материалы

        private static GameObject BuildObstaclePrefab(Palette palette)
        {
            var root = new GameObject("Obstacle");
            GameObject visual = CreatePrimitive(PrimitiveType.Sphere, "Visual", palette.Wall);
            visual.transform.SetParent(root.transform, false);

            ObstacleView view = visual.AddComponent<ObstacleView>();
            Obstacle obstacle = root.AddComponent<Obstacle>();

            Wire(view, ("targetRenderer", visual.GetComponent<MeshRenderer>()));
            Wire(obstacle, ("view", view));

            return SaveAsPrefab(root, $"{PrefabFolder}/Obstacle.prefab");
        }

        private static GameObject BuildShotPrefab(Palette palette)
        {
            var root = new GameObject("Shot");
            GameObject visual = CreatePrimitive(PrimitiveType.Sphere, "Visual", palette.Shot);
            visual.transform.SetParent(root.transform, false);

            ShotProjectile projectile = root.AddComponent<ShotProjectile>();
            Wire(projectile, ("visualRoot", visual.transform));

            return SaveAsPrefab(root, $"{PrefabFolder}/Shot.prefab");
        }

        private static GameObject SaveAsPrefab(GameObject instance, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            // Физики в проекте нет вообще — вся геометрия аналитическая, коллайдеры только мешают.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            return go;
        }

        /// <summary>Материалы уровня. Цвета подобраны так, чтобы завалы читались отдельно от стен коридора.</summary>
        private readonly struct Palette
        {
            public readonly Material Ground;
            public readonly Material Player;
            public readonly Material Shot;
            public readonly Material Wall;
            public readonly Material Blocker;
            public readonly Material DoorFrame;
            public readonly Material DoorPanel;

            public Palette(Material ground, Material player, Material shot, Material wall,
                Material blocker, Material doorFrame, Material doorPanel)
            {
                Ground = ground;
                Player = player;
                Shot = shot;
                Wall = wall;
                Blocker = blocker;
                DoorFrame = doorFrame;
                DoorPanel = doorPanel;
            }
        }

        private static Palette LoadOrCreatePalette()
        {
            return new Palette(
                LoadOrCreateMaterial("Ground", new Color(0.15f, 0.17f, 0.21f), 0.1f),
                LoadOrCreateMaterial("Player", new Color(0.25f, 0.80f, 1.00f), 0.6f),
                LoadOrCreateMaterial("Shot", new Color(1.00f, 0.62f, 0.20f), 0.7f),
                LoadOrCreateMaterial("ObstacleWall", new Color(0.42f, 0.45f, 0.48f), 0.2f),
                LoadOrCreateMaterial("ObstacleBlocker", new Color(0.72f, 0.45f, 0.30f), 0.3f),
                LoadOrCreateMaterial("DoorFrame", new Color(0.30f, 0.32f, 0.36f), 0.4f),
                LoadOrCreateMaterial("DoorPanel", new Color(0.30f, 0.85f, 0.45f), 0.5f));
        }

        private static Material LoadOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = $"{ConfigFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Settings", "Configs");
            EnsureFolder("Assets/_Project", "Art");
            EnsureFolder("Assets/_Project/Art", "Materials");
            EnsureFolder("Assets/_Project", "Prefabs");
            EnsureFolder("Assets/_Project", "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        // ------------------------------------------------------------------ вспомогательное

        /// <summary>
        /// Проставляет приватные сериализованные поля компонента. Все связи сцены строятся
        /// только так — руками в инспекторе ничего не назначается, поэтому пересборка сцены
        /// никогда не теряет ссылки.
        /// </summary>
        private static void Wire(Object target, params (string Field, object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach ((string field, object value) in fields)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"{target.GetType().Name}: поля '{field}' не существует — связь не установлена.");
                    continue;
                }

                switch (value)
                {
                    case Object reference:
                        property.objectReferenceValue = reference;
                        break;
                    case float number:
                        property.floatValue = number;
                        break;
                    case int number:
                        property.intValue = number;
                        break;
                    case Vector3 vector:
                        property.vector3Value = vector;
                        break;
                    default:
                        Debug.LogError($"{target.GetType().Name}.{field}: тип значения не поддерживается.");
                        break;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds ComputeBounds(LevelLayout layout)
        {
            var bounds = new Bounds(layout.PathPoints[0], Vector3.zero);

            foreach (Vector3 point in layout.PathPoints)
            {
                bounds.Encapsulate(point);
            }

            foreach (ObstacleSpec spec in layout.Obstacles)
            {
                bounds.Encapsulate(spec.Position);
            }

            // Запас такой, чтобы край земли не попадал в кадр игровой камеры.
            bounds.Expand(new Vector3(40f, 0f, 40f));
            return bounds;
        }

        private static void LogSummary(LevelLayout layout, LevelConfig levelConfig,
            PlayerConfig playerConfig, InfectionConfig infectionConfig)
        {
            var counts = new Dictionary<ObstacleRole, int>();
            foreach (ObstacleSpec spec in layout.Obstacles)
            {
                counts.TryGetValue(spec.Role, out int current);
                counts[spec.Role] = current + 1;
            }

            LevelBudget budget = LevelBudgetValidator.Evaluate(layout, playerConfig, infectionConfig);

            string outcome = budget.Completable
                ? $"пройден за {budget.ShotCount} выстрел(ов), радиус на финише {budget.FinalRadius:F2} м " +
                  $"(критический {playerConfig.CriticalRadius:F2} м), запас массы {budget.Reserve * 100f:F0}% " +
                  $"при требуемых {LevelBudgetValidator.RequiredReserve * 100f:F0}%"
                : $"НЕ ПРОЙДЕН: {budget.Failure}";

            Debug.Log(
                $"Сцена собрана: {ScenePath}\n" +
                $"Маршрут: {layout.PathPoints.Length} точек, амплитуда изгиба {layout.CurveAmplitude:F2} м " +
                $"(из {levelConfig.CurveAmplitude:F2} м после ограничения {levelConfig.MaxPathDeviationDegrees:F0}°)\n" +
                $"Коридор: {layout.CorridorWidths[0]:F2} м → {layout.CorridorWidths[layout.DoorPointIndex]:F2} м\n" +
                $"Препятствий: {layout.Obstacles.Length} " +
                $"(стены {Count(counts, ObstacleRole.CorridorWall)}, " +
                $"кластеры {Count(counts, ObstacleRole.BlockerCluster)}, " +
                $"одиночные {Count(counts, ObstacleRole.SingleBlocker)}), завалов {layout.Blockers.Length}\n" +
                $"Прогон идеальным игроком: {outcome}");
        }

        private static int Count(Dictionary<ObstacleRole, int> counts, ObstacleRole role)
        {
            return counts.TryGetValue(role, out int value) ? value : 0;
        }
    }
}
