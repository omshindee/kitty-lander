using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the Kitty Lander content from data:
///  - configures import settings for the generated art (Tools/generate_art.py)
///  - creates materials and prefabs (lander, hazards, milk) if they are missing
///  - generates Assets/Scenes/MainMenu.unity and Assets/Scenes/Levels/LevelNN.unity
///    from Assets/Editor/LevelData/levels.json (Tools/level_design.py)
///  - registers the scenes in the build settings
/// Level 01 is a copy of the original hand-made SampleScene.
///
/// Menu: Kitty Lander > Rebuild Levels / Validate Levels.
/// Batch: Unity -batchmode -quit -projectPath . -executeMethod KittyLanderBuilder.BatchBuild
/// </summary>
public static class KittyLanderBuilder
{
    private const string Gen = "Assets/Art/Generated";
    private const string SampleScene = "Assets/Scenes/SampleScene.unity";
    private const string LevelsDir = "Assets/Scenes/Levels";
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string LevelJson = "Assets/Editor/LevelData/levels.json";
    private const string PrefabDir = "Assets/Prefabs";
    private const string HazardDir = "Assets/Prefabs/Hazards";
    private const string MatDir = "Assets/Art/Generated/Materials";

    private static readonly string[] Themes = { "Meadow", "Desert", "Snow", "Moon" };

    // ------------------------------------------------------------ JSON model

    [Serializable] private class LevelFile { public LevelDef[] levels; }
    [Serializable] private class PolyDef { public float[] pts; }
    [Serializable]
    private class HazardDef
    {
        public string type;
        public float x, y, rot, scale = 1f, width = 1f, mx, my, period = 4f, phase, spin;
    }
    [Serializable]
    private class LevelDef
    {
        public string name, hint, theme;
        public float ortho = 8f, fuel = 30f, gravity = 0.7f;
        public float[] bounds, start, pad, fish, food, milk;
        public PolyDef[] terrain;
        public HazardDef[] hazards;
    }

    private static LevelDef[] LoadLevels()
    {
        var json = File.ReadAllText(LevelJson);
        return JsonUtility.FromJson<LevelFile>(json).levels;
    }

    // ------------------------------------------------------------- entry points

    [MenuItem("Kitty Lander/Rebuild Levels")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigureImporters();
        CreateMaterials();
        CreatePrefabs();
        var levels = LoadLevels();
        var scenes = new List<string> { MenuScene };
        Directory.CreateDirectory(LevelsDir);

        scenes.Add(BuildLevelOne());
        for (int i = 0; i < levels.Length; i++)
        {
            scenes.Add(BuildLevel(levels[i], i + 2));
        }
        BuildMenu(levels);

        EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScene);
        Debug.Log($"[KL-BUILD] Built menu + {scenes.Count - 1} levels.");
    }

    public static void BatchBuild()
    {
        try
        {
            BuildAll();
            ValidateLevels();
        }
        catch (Exception e)
        {
            Debug.LogError("[KL-BUILD] FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    public static void BatchValidate()
    {
        try { ValidateLevels(); }
        catch (Exception e)
        {
            Debug.LogError("[KL-VALIDATE] FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    // --------------------------------------------------------------- importers

    private static void ConfigureImporters()
    {
        foreach (var t in Themes)
        {
            SetTexture($"{Gen}/Terrain/Fill_{t}.png", TextureWrapMode.Repeat, TextureWrapMode.Repeat);
            SetTexture($"{Gen}/Terrain/Edge_{t}.png", TextureWrapMode.Repeat, TextureWrapMode.Clamp);
        }
        foreach (var bg in new[] { "BG_Desert", "BG_Snow", "BG_Moon" })
            SetSprite($"{Gen}/Backgrounds/{bg}.png", 116f);
        SetSprite($"{Gen}/Hazards/Spikes.png", 128f, fullRect: true);
        foreach (var h in new[] { "YarnBall", "Cactus", "Asteroid", "Icicle" })
            SetSprite($"{Gen}/Hazards/{h}.png", 128f);
        SetSprite($"{Gen}/Pickups/Milk.png", 220f);
        SetSprite($"{Gen}/UI/Panel.png", 100f, border: new Vector4(44, 44, 44, 44));
        SetSprite($"{Gen}/UI/Button.png", 100f, border: new Vector4(36, 36, 36, 36));
        foreach (var u in new[] { "Star", "StarEmpty", "Paw" })
            SetSprite($"{Gen}/UI/{u}.png", 100f);
        AssetDatabase.Refresh();
    }

    private static void SetTexture(string path, TextureWrapMode u, TextureWrapMode v)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) throw new Exception("Missing texture " + path + " (run Tools/generate_art.py)");
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.wrapModeU = u;
        imp.wrapModeV = v;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
    }

    private static void SetSprite(string path, float ppu, bool fullRect = false, Vector4 border = default)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) throw new Exception("Missing sprite " + path + " (run Tools/generate_art.py)");
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        s.spriteBorder = border;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
    }

    // --------------------------------------------------------------- materials

    private static Shader SpriteShader()
    {
        var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return sh;
    }

    private static Material GetOrCreateMaterial(string name, Texture tex)
    {
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(SpriteShader());
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = SpriteShader();
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CreateMaterials()
    {
        foreach (var t in Themes)
        {
            GetOrCreateMaterial($"Terrain_{t}_Fill", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/Terrain/Fill_{t}.png"));
            GetOrCreateMaterial($"Terrain_{t}_Edge", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/Terrain/Edge_{t}.png"));
        }
        GetOrCreateMaterial("Terrain_Outline", null);
        AssetDatabase.SaveAssets();
    }

    // ----------------------------------------------------------------- prefabs

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (s == null) throw new Exception("No sprite at " + path);
        return s;
    }

    private static void CreatePrefabs()
    {
        Directory.CreateDirectory(HazardDir);

        // Lander + landing pad fix come from the original scene.
        var scene = EditorSceneManager.OpenScene(SampleScene, OpenSceneMode.Single);
        string landerPath = $"{PrefabDir}/Lander.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(landerPath) == null)
        {
            var lander = FindRoot(scene, "Lander");
            PrefabUtility.SaveAsPrefabAsset(lander, landerPath);
        }

        // The LandingPad prefab pointed at a deleted texture; the scene instance had the fix as overrides.
        string padPath = $"{PrefabDir}/LandingPad.prefab";
        var padContents = PrefabUtility.LoadPrefabContents(padPath);
        bool padBroken = padContents.GetComponentInChildren<SpriteRenderer>().sprite == null;
        PrefabUtility.UnloadPrefabContents(padContents);
        if (padBroken)
        {
            var padInstance = FindRoot(scene, "LandingPad");
            PrefabUtility.ApplyPrefabInstance(padInstance, InteractionMode.AutomatedAction);
        }
        // Do not keep changes to the original scene.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureBobber($"{PrefabDir}/Fish.prefab");
        EnsureBobber($"{PrefabDir}/CatFood.prefab");

        // Milk bottle: a bigger fuel pickup based on the cat food prefab.
        string milkPath = $"{PrefabDir}/Milk.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(milkPath) == null)
        {
            AssetDatabase.CopyAsset($"{PrefabDir}/CatFood.prefab", milkPath);
            var milk = PrefabUtility.LoadPrefabContents(milkPath);
            milk.name = "Milk";
            milk.GetComponentInChildren<SpriteRenderer>().sprite = LoadSprite($"{Gen}/Pickups/Milk.png");
            var box = milk.GetComponent<BoxCollider2D>();
            if (box != null) box.size = new Vector2(0.9f, 1.15f);
            var so = new SerializedObject(milk.GetComponent<CatFood>());
            so.FindProperty("fuelAmount").floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(milk, milkPath);
            PrefabUtility.UnloadPrefabContents(milk);
        }

        CreateHazardPrefab("Spikes", go =>
        {
            var sr = go.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(2f, 1f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.8f, 0.7f);
            box.offset = new Vector2(0f, -0.1f);
        });
        CreateHazardPrefab("YarnBall", go => go.AddComponent<CircleCollider2D>().radius = 0.82f);
        CreateHazardPrefab("Asteroid", go => go.AddComponent<CircleCollider2D>().radius = 0.72f);
        CreateHazardPrefab("Cactus", go => go.AddComponent<PolygonCollider2D>());
        CreateHazardPrefab("Icicle", go => go.AddComponent<PolygonCollider2D>());
        AssetDatabase.SaveAssets();
    }

    private static void EnsureBobber(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<Bobber>() == null)
        {
            root.AddComponent<Bobber>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void CreateHazardPrefab(string name, Action<GameObject> addCollider)
    {
        string path = $"{HazardDir}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite($"{Gen}/Hazards/{name}.png");
        sr.sortingOrder = 2;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        addCollider(go);
        go.AddComponent<Hazard>();
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ scenes

    private static GameObject FindRoot(Scene scene, string name)
    {
        var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go == null) throw new Exception($"'{name}' not found in {scene.path}");
        return go;
    }

    private static Scene CopyBaseScene(string path)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(SampleScene, path)) throw new Exception("Could not copy SampleScene to " + path);
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static UISkin Skin() => new UISkin
    {
        panel = LoadSprite($"{Gen}/UI/Panel.png"),
        button = LoadSprite($"{Gen}/UI/Button.png"),
        star = LoadSprite($"{Gen}/UI/Star.png"),
        starEmpty = LoadSprite($"{Gen}/UI/StarEmpty.png"),
        paw = LoadSprite($"{Gen}/UI/Paw.png"),
        fishIcon = LoadSprite("Assets/Textures and Materials/DeadFish.png"),
        fuelIcon = LoadSprite("Assets/Textures and Materials/CatFood.png"),
        click = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Gen}/Audio/Click.wav"),
    };

    private static AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Gen}/Audio/{n}.wav");

    private static void SetupManager(GameObject gmObject, string name, string hint, float fuel, float gravity, Rect bounds)
    {
        var gm = gmObject.GetComponent<GameManager>();
        gm.EditorSetup(name, hint, fuel, gravity, bounds);
        EditorUtility.SetDirty(gm);

        // (no '??' here: Unity's fake-null components in the editor would break it)
        var ui = gmObject.GetComponent<GameUI>();
        if (ui == null) ui = gmObject.AddComponent<GameUI>();
        ui.EditorSetup(Skin());
        EditorUtility.SetDirty(ui);

        var audio = gmObject.GetComponent<AudioManager>();
        if (audio == null) audio = gmObject.AddComponent<AudioManager>();
        audio.EditorSetup(Clip("Pickup"), Clip("Refuel"), Clip("Crash"), Clip("Win"), Clip("PadOpen"), Clip("Thrust"));
        EditorUtility.SetDirty(audio);
    }

    private static GameObject Place(string prefabPath, Vector2 pos, Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new Exception("Missing prefab " + prefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        return go;
    }

    private static string BuildLevelOne()
    {
        string path = $"{LevelsDir}/Level01.unity";
        var scene = CopyBaseScene(path);
        var old = FindRoot(scene, "Lander");
        var lander = Place($"{PrefabDir}/Lander.prefab", old.transform.position);
        lander.name = "Lander";
        UnityEngine.Object.DestroyImmediate(old);

        SetupManager(FindRoot(scene, "GameManager"), "First Flight",
            "Grab both fish to open the landing pad, then touch down gently.", 25f, 0.7f,
            new Rect(-11f, -12.5f, 23.5f, 15f));
        EditorSceneManager.SaveScene(scene);
        return path;
    }

    private static string BuildLevel(LevelDef L, int number)
    {
        string path = $"{LevelsDir}/Level{number:00}.unity";
        var scene = CopyBaseScene(path);
        var keep = new HashSet<string> { "Main Camera", "Global Light 2D", "GameManager" };
        foreach (var go in scene.GetRootGameObjects())
            if (!keep.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);

        var bounds = Rect.MinMaxRect(L.bounds[0], L.bounds[1], L.bounds[2], L.bounds[3]);
        Vector2 start = new Vector2(L.start[0], L.start[1]);

        // camera + background
        var camGo = FindRoot(scene, "Main Camera");
        var cam = camGo.GetComponent<Camera>();
        cam.orthographicSize = L.ortho;
        camGo.transform.position = new Vector3(start.x, start.y, -10f);
        cam.backgroundColor = ThemeSky(L.theme);
        if (camGo.GetComponent<CameraFollow>() == null) camGo.AddComponent<CameraFollow>();
        var bg = new GameObject("Background");
        bg.transform.SetParent(camGo.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 20f);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = LoadSprite(L.theme == "Meadow" ? "Assets/Textures and Materials/Background.png" : $"{Gen}/Backgrounds/BG_{L.theme}.png");
        bgSr.sortingLayerName = "minus";
        bgSr.sortingOrder = -100;
        bg.AddComponent<BackgroundFit>();

        // terrain
        var terrainRoot = new GameObject("Terrain").transform;
        var mats = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Terrain_{L.theme}_Fill.mat"),
            AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Terrain_Outline.mat"),
            AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Terrain_{L.theme}_Edge.mat"),
        };
        for (int i = 0; i < L.terrain.Length; i++)
        {
            var t = new GameObject($"Rock {i + 1}");
            t.transform.SetParent(terrainRoot, false);
            var col = t.AddComponent<PolygonCollider2D>();
            col.points = ToPoints(L.terrain[i].pts);
            t.AddComponent<MeshFilter>();
            var mr = t.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            t.AddComponent<TerrainShape>().Rebuild();
        }

        // lander, pad, pickups
        var lander = Place($"{PrefabDir}/Lander.prefab", start);
        lander.name = "Lander";
        var pad = Place($"{PrefabDir}/LandingPad.prefab", new Vector2(L.pad[0], L.pad[1]));
        pad.name = "LandingPad";
        var pickups = new GameObject("Pickups").transform;
        foreach (var p in ToPoints(L.fish)) Place($"{PrefabDir}/Fish.prefab", p, pickups).name = "Fish";
        foreach (var p in ToPoints(L.food)) Place($"{PrefabDir}/CatFood.prefab", p, pickups).name = "CatFood";
        foreach (var p in ToPoints(L.milk)) Place($"{PrefabDir}/Milk.prefab", p, pickups).name = "Milk";

        // hazards
        var hazardRoot = new GameObject("Hazards").transform;
        foreach (var h in L.hazards ?? new HazardDef[0])
        {
            var go = Place($"{HazardDir}/{h.type}.prefab", new Vector2(h.x, h.y), hazardRoot);
            go.transform.rotation = Quaternion.Euler(0f, 0f, h.rot);
            go.transform.localScale = new Vector3(h.scale, h.scale, 1f);
            if (h.type == "Spikes")
            {
                var sr = go.GetComponent<SpriteRenderer>();
                sr.size = new Vector2(2f * h.width, 1f);
                var box = go.GetComponent<BoxCollider2D>();
                box.size = new Vector2(2f * h.width - 0.2f, 0.7f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            }
            var hazard = go.GetComponent<Hazard>();
            hazard.Setup(new Vector2(h.mx, h.my), h.period, h.phase, h.spin);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hazard);
        }

        SetupManager(FindRoot(scene, "GameManager"), L.name, L.hint, L.fuel, L.gravity, bounds);
        EditorSceneManager.SaveScene(scene);
        return path;
    }

    private static void BuildMenu(LevelDef[] levels)
    {
        var scene = CopyBaseScene(MenuScene);
        var keep = new HashSet<string> { "Main Camera", "Global Light 2D" };
        foreach (var go in scene.GetRootGameObjects())
            if (!keep.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);

        var camGo = FindRoot(scene, "Main Camera");
        var cam = camGo.GetComponent<Camera>();
        cam.orthographicSize = 6f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var bg = new GameObject("Background");
        bg.transform.SetParent(camGo.transform, false);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = LoadSprite("Assets/Textures and Materials/Background.png");
        bgSr.sortingLayerName = "minus";
        bgSr.sortingOrder = -100;
        bg.AddComponent<BackgroundFit>();

        Decor("Decor Kitty", LoadSprite("Assets/Animations/happy.png"), new Vector2(-7.2f, -3.2f), 0.9f);
        Decor("Decor Fish A", LoadSprite("Assets/Textures and Materials/DeadFish.png"), new Vector2(7f, -2.5f), 1.6f);
        Decor("Decor Fish B", LoadSprite("Assets/Textures and Materials/DeadFish.png"), new Vector2(8.6f, -4.2f), 1.3f);
        Decor("Decor Food", LoadSprite("Assets/Textures and Materials/CatFood.png"), new Vector2(-8.6f, 2.6f), 1.4f);

        var ui = new GameObject("MainMenu").AddComponent<MainMenuUI>();
        var names = new List<string> { "First Flight" };
        names.AddRange(levels.Select(l => l.name));
        ui.EditorSetup(Skin(), names.ToArray());
        EditorUtility.SetDirty(ui);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Decor(string name, Sprite sprite, Vector2 pos, float scale)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        var child = new GameObject("Sprite");
        child.transform.SetParent(root.transform, false);
        child.transform.localScale = Vector3.one * scale;
        var sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;
        root.AddComponent<Bobber>();
    }

    private static Color ThemeSky(string theme)
    {
        switch (theme)
        {
            case "Desert": return new Color(1f, 0.85f, 0.65f);
            case "Snow": return new Color(0.2f, 0.28f, 0.5f);
            case "Moon": return new Color(0.1f, 0.06f, 0.2f);
            default: return new Color(0.75f, 0.88f, 0.95f);
        }
    }

    private static Vector2[] ToPoints(float[] f)
    {
        if (f == null) return new Vector2[0];
        var pts = new Vector2[f.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(f[2 * i], f[2 * i + 1]);
        return pts;
    }

    // -------------------------------------------------------------- validation

    /// <summary>
    /// Checks every level with a coarse configuration-space flood fill using the
    /// lander's real size: the start must be clear and every fish plus the pad must
    /// be reachable. Moving hazards are tested at several points of their cycle.
    /// Optionally renders a full-level preview PNG to $KL_PREVIEW_DIR.
    /// </summary>
    [MenuItem("Kitty Lander/Validate Levels")]
    public static void ValidateLevels()
    {
        string previewDir = Environment.GetEnvironmentVariable("KL_PREVIEW_DIR");
        int failures = 0;
        foreach (var s in EditorBuildSettings.scenes.Skip(1))
        {
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            failures += ValidateScene(scene) ? 0 : 1;
            if (!string.IsNullOrEmpty(previewDir)) RenderPreview(scene, previewDir);
        }
        if (failures > 0) Debug.LogError($"[KL-VALIDATE] {failures} level(s) have problems.");
        else Debug.Log("[KL-VALIDATE] All levels OK.");
    }

    private static readonly Vector2 LanderSize = new Vector2(3.15f, 2.5f);
    private static readonly Vector2 LanderOffset = new Vector2(-0.08f, -0.04f);
    private const float Step = 0.25f;

    private static bool ValidateScene(Scene scene)
    {
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var lander = UnityEngine.Object.FindAnyObjectByType<Lander>();
        var pad = UnityEngine.Object.FindAnyObjectByType<Pad>(FindObjectsInactive.Include);
        var fish = UnityEngine.Object.FindObjectsByType<Fish>(FindObjectsInactive.Exclude);
        var hazards = UnityEngine.Object.FindObjectsByType<Hazard>(FindObjectsInactive.Exclude);
        Rect bounds = gm.LevelBounds;

        var moving = new List<(Hazard h, Vector2 basePos, Vector2 offset)>();
        foreach (var h in hazards)
        {
            var so = new SerializedObject(h);
            Vector2 off = so.FindProperty("moveOffset").vector2Value;
            if (off != Vector2.zero) moving.Add((h, h.transform.position, off));
        }

        bool ok = true;
        var log = new System.Text.StringBuilder($"[KL-VALIDATE] {scene.name} '{gm.LevelName}': ");
        Physics2D.SyncTransforms();

        // Static check: moving hazards removed (they never block permanently).
        foreach (var m in moving) m.h.gameObject.SetActive(false);
        Physics2D.SyncTransforms();
        var reach = Flood(bounds, lander.transform.position, lander, pad, out bool startClear);
        if (!startClear) { ok = false; log.Append("START BLOCKED; "); }
        int fishOk = fish.Count(f => Touches(reach, bounds, f.GetComponent<Collider2D>().bounds));
        if (fishOk < fish.Length) { ok = false; log.Append($"fish reachable {fishOk}/{fish.Length} (FAIL); "); }
        else log.Append($"fish {fishOk}/{fish.Length} ok; ");
        var padCol = pad.GetComponent<Collider2D>();
        Vector2 landing = new Vector2(pad.transform.position.x, padCol.bounds.max.y + 1.3f);
        bool padOk = Near(reach, bounds, landing, 0.35f);
        if (!padOk) { ok = false; log.Append("PAD UNREACHABLE; "); }
        else log.Append("pad ok; ");
        bool padFlat = PadIsSupported(padCol);
        if (!padFlat) { ok = false; log.Append("PAD FLOATING/BURIED; "); }

        // Dynamic snapshots: all moving hazards at the same phase.
        foreach (var m in moving) m.h.gameObject.SetActive(true);
        if (moving.Count > 0)
        {
            int snaps = 0, good = 0;
            foreach (float s in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                foreach (var m in moving) m.h.transform.position = m.basePos + m.offset * s;
                Physics2D.SyncTransforms();
                var r = Flood(bounds, lander.transform.position, lander, pad, out _);
                snaps++;
                bool all = fish.All(f => Touches(r, bounds, f.GetComponent<Collider2D>().bounds))
                           && Near(r, bounds, landing, 0.35f);
                if (all) good++;
            }
            foreach (var m in moving) m.h.transform.position = m.basePos;
            log.Append($"moving-hazard snapshots fully open {good}/{snaps}; ");
        }
        Physics2D.SyncTransforms();

        if (ok) Debug.Log(log.Append("OK").ToString());
        else Debug.LogError(log.Append("PROBLEM").ToString());
        return ok;
    }

    private static bool PadIsSupported(Collider2D padCol)
    {
        // Ground should be directly under both ends of the pad.
        var b = padCol.bounds;
        foreach (float x in new[] { b.min.x + 0.2f, b.max.x - 0.2f })
        {
            var hits = Physics2D.RaycastAll(new Vector2(x, b.min.y + 0.05f), Vector2.down, 0.6f);
            if (!hits.Any(h => h.collider != padCol && !h.collider.isTrigger)) return false;
        }
        return true;
    }

    private static bool[,] Flood(Rect bounds, Vector2 start, Lander lander, Pad pad, out bool startClear)
    {
        int w = Mathf.CeilToInt(bounds.width / Step), h = Mathf.CeilToInt(bounds.height / Step);
        var free = new bool[w, h];
        var seen = new bool[w, h];
        var filter = new ContactFilter2D { useTriggers = false };
        filter.NoFilter();
        filter.useTriggers = false;
        var results = new List<Collider2D>();
        Transform lt = lander.transform;

        bool IsFree(Vector2 c)
        {
            results.Clear();
            Physics2D.OverlapBox(c + LanderOffset, LanderSize, 0f, filter, results);
            foreach (var col in results)
            {
                if (col.isTrigger) continue;
                if (col.transform.IsChildOf(lt)) continue;
                if (col.GetComponentInParent<Pad>() == pad) continue;
                return false;
            }
            return true;
        }

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                free[x, y] = IsFree(Cell(bounds, x, y));

        int sx = Mathf.Clamp(Mathf.RoundToInt((start.x - bounds.xMin) / Step), 0, w - 1);
        int sy = Mathf.Clamp(Mathf.RoundToInt((start.y - bounds.yMin) / Step), 0, h - 1);
        startClear = free[sx, sy];
        if (!startClear) return seen;

        var q = new Queue<Vector2Int>();
        q.Enqueue(new Vector2Int(sx, sy));
        seen[sx, sy] = true;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || seen[n.x, n.y] || !free[n.x, n.y]) continue;
                seen[n.x, n.y] = true;
                q.Enqueue(n);
            }
        }
        return seen;
    }

    private static Vector2 Cell(Rect b, int x, int y) => new Vector2(b.xMin + x * Step, b.yMin + y * Step);

    private static bool Touches(bool[,] reach, Rect bounds, Bounds target)
    {
        for (int x = 0; x < reach.GetLength(0); x++)
            for (int y = 0; y < reach.GetLength(1); y++)
            {
                if (!reach[x, y]) continue;
                var c = Cell(bounds, x, y) + LanderOffset;
                var box = new Bounds(new Vector3(c.x, c.y, target.center.z), new Vector3(LanderSize.x, LanderSize.y, 10f));
                if (box.Intersects(target)) return true;
            }
        return false;
    }

    private static bool Near(bool[,] reach, Rect bounds, Vector2 p, float radius)
    {
        for (int x = 0; x < reach.GetLength(0); x++)
            for (int y = 0; y < reach.GetLength(1); y++)
                if (reach[x, y] && Vector2.Distance(Cell(bounds, x, y), p) <= radius) return true;
        return false;
    }

    private static void RenderPreview(Scene scene, string dir)
    {
        Directory.CreateDirectory(dir);
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        Rect b = gm.LevelBounds;
        var main = Camera.main;
        var go = new GameObject("PreviewCam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.orthographic = true;
        int hpx = 1080;
        int wpx = Mathf.RoundToInt(hpx * b.width / b.height);
        cam.orthographicSize = b.height / 2f;
        go.transform.position = new Vector3(b.center.x, b.center.y, -10f);
        var rt = new RenderTexture(wpx, hpx, 24);
        cam.targetTexture = rt;

        // Stretch the background over the whole level for the preview.
        var bg = main.transform.Find("Background");
        if (bg != null)
        {
            var sr = bg.GetComponent<SpriteRenderer>();
            Vector2 size = sr.sprite.bounds.size;
            float s = Mathf.Max(b.width / size.x, b.height / size.y) * 1.05f;
            bg.SetParent(null, true);
            bg.position = new Vector3(b.center.x, b.center.y, 10f);
            bg.localScale = new Vector3(s, s, 1f);
        }

        // The first render after a scene load can bind stale sprite textures in batch mode; render twice.
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(wpx, hpx, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, wpx, hpx), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, scene.name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
        // The scene is reopened from disk next iteration, so the moved background is discarded.
    }
}
