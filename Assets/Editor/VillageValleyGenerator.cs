using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VillageValleyGenerator
{
    // Generation revision 2: stable asset GUIDs and downloaded water integration.
    private const string Root = "Assets/VillageValley";
    private const string Request = Root + "/Generate.request";
    private const string RepairRequest = Root + "/Repair.request";
    private const int HeightResolution = 513;
    private const int AlphaResolution = 512;
    private const float Size = 1000f;
    private const float MaxHeight = 300f;

    [InitializeOnLoadMethod]
    private static void GenerateWhenRequested()
    {
        bool generate = File.Exists(Request);
        bool repair = File.Exists(RepairRequest);
        if (!generate && !repair) return;
        if (generate) File.Delete(Request);
        if (repair) File.Delete(RepairRequest);
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (generate) Generate();
                else RepairExistingScene();
            }
            catch (Exception exception)
            {
                File.WriteAllText(Root + "/LastError.txt", exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    private static void RepairExistingScene()
    {
        const string scenePath = Root + "/VillageValley.unity";
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        Material houseMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/BakerHouseURP.mat");
        Material barrelMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/BarrelURP.mat");
        Material waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/RiverWater.mat");
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(Root + "/VillageValleyTerrain.asset");
        Mesh westMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/WestRiver.asset");
        Mesh eastMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/EastRiver.asset");
        if (houseMaterial == null || barrelMaterial == null || waterMaterial == null || terrainData == null || westMesh == null || eastMesh == null)
            throw new InvalidOperationException("Village repair assets are missing.");

        int houses = 0, barrels = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Terrain terrain = root.GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.terrainData = terrainData;
                TerrainCollider terrainCollider = root.GetComponent<TerrainCollider>();
                if (terrainCollider != null) terrainCollider.terrainData = terrainData;
            }
            MeshFilter rootFilter = root.GetComponent<MeshFilter>();
            if (root.name == "West River" && rootFilter != null)
            {
                rootFilter.sharedMesh = westMesh;
                root.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            }
            else if (root.name == "East River" && rootFilter != null)
            {
                rootFilter.sharedMesh = eastMesh;
                root.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            }
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("Baker House_", StringComparison.Ordinal))
                {
                    foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = houseMaterial;
                    houses++;
                }
                else if (child.name.StartsWith("Barrel_", StringComparison.Ordinal))
                {
                    foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = barrelMaterial;
                    barrels++;
                }
            }
        }
        Paint(terrainData);
        EditorUtility.SetDirty(terrainData);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previousScene.IsValid() && previousScene.isLoaded) EditorSceneManager.SetActiveScene(previousScene);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Root + "/RepairResult.txt", "Houses=" + houses + Environment.NewLine + "Barrels=" + barrels);
    }

    [MenuItem("Tools/Terrain/Create Village Valley")]
    public static void Generate()
    {
        Directory.CreateDirectory(Root);
        const string scenePath = Root + "/VillageValley.unity";
        Scene oldVillageScene = SceneManager.GetSceneByPath(scenePath);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (oldVillageScene.IsValid() && oldVillageScene.isLoaded)
            EditorSceneManager.CloseScene(oldVillageScene, true);
        EditorSceneManager.SetActiveScene(scene);

        string terrainPath = Root + "/VillageValleyTerrain.asset";
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
        bool newTerrainAsset = data == null;
        if (newTerrainAsset) data = new TerrainData();
        data.heightmapResolution = HeightResolution;
        data.alphamapResolution = AlphaResolution;
        data.baseMapResolution = 1024;
        data.size = new Vector3(Size, MaxHeight, Size);
        data.SetHeights(0, 0, BuildHeights());
        data.terrainLayers = LoadLayers();
        Paint(data);

        if (newTerrainAsset) AssetDatabase.CreateAsset(data, terrainPath);
        else EditorUtility.SetDirty(data);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Village Valley Terrain";
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 1800f;

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Houidisoft technology/One Click Add Water -Stylized Water Shader/Resources/water.mat");
        if (water == null) throw new InvalidOperationException("Downloaded Houidisoft water material was not found.");
        CreateRiver("West River", data, water, false);
        CreateRiver("East River", data, water, true);
        CreateVillage(data);
        CreateLightingAndCamera();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.67f, 0.77f, 0.82f);
        RenderSettings.fogStartDistance = 700f;
        RenderSettings.fogEndDistance = 1600f;
        RenderSettings.ambientIntensity = 1.1f;

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrainObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        AssetDatabase.Refresh();
        Debug.Log("Village Valley rebuilt from downloaded assets at " + scenePath);
    }

    private static float[,] BuildHeights()
    {
        var heights = new float[HeightResolution, HeightResolution];
        for (int z = 0; z < HeightResolution; z++)
        {
            float v = z / (float)(HeightResolution - 1);
            for (int x = 0; x < HeightResolution; x++)
            {
                float u = x / (float)(HeightResolution - 1);
                float baseLand = 0.078f + (Fractal(u * 7f, v * 7f, 3) - 0.5f) * 0.018f;

                // A high jagged mountain wall across the rear of the map.
                float rear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.88f, v));
                rear *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, v));
                float horizontalMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.20f, u));
                horizontalMask *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.20f, 1f - u));
                float ridge = 1f - Mathf.Abs(Fractal(u * 12f + 8f, v * 8f + 20f, 4) * 2f - 1f);
                ridge = Mathf.Pow(ridge, 2.4f);
                float mountain = rear * horizontalMask * (0.40f + ridge * 0.37f + Fractal(u * 23f, v * 18f, 3) * 0.12f);

                // Foothills transition down into the broad settlement plain.
                float foothill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.67f, v));
                foothill *= (0.07f + Fractal(u * 11f + 30f, v * 11f, 3) * 0.09f);
                float h = baseLand + mountain + foothill;

                // Water channels isolate the central village peninsula on both sides.
                float west = RiverCenter(v, false);
                float east = RiverCenter(v, true);
                float westDistance = Mathf.Abs(u - west);
                float eastDistance = Mathf.Abs(u - east);
                float channel = Mathf.Max(Mathf.Exp(-westDistance * westDistance / 0.0017f), Mathf.Exp(-eastDistance * eastDistance / 0.0017f));
                h = Mathf.Lerp(h, 0.025f + v * 0.006f, channel * 0.94f);

                // Soft sandy banks around both rivers.
                float bank = Mathf.Max(Mathf.Exp(-westDistance * westDistance / 0.0055f), Mathf.Exp(-eastDistance * eastDistance / 0.0055f));
                h -= bank * (1f - channel) * 0.012f;
                heights[z, x] = Mathf.Clamp01(h);
            }
        }
        return heights;
    }

    private static TerrainLayer[] LoadLayers()
    {
        string p = "Assets/TerrainSampleAssets/TerrainLayers/";
        string[] names = { "Grass_A", "Rock", "Snow", "Sand", "Soil_Rocks" };
        var result = new TerrainLayer[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            result[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p + names[i] + "_TerrainLayer.terrainlayer");
            if (result[i] == null) throw new InvalidOperationException("Missing terrain layer: " + names[i]);
        }
        return result;
    }

    private static void Paint(TerrainData data)
    {
        var map = new float[AlphaResolution, AlphaResolution, 5];
        for (int z = 0; z < AlphaResolution; z++)
        {
            float v = z / (float)(AlphaResolution - 1);
            for (int x = 0; x < AlphaResolution; x++)
            {
                float u = x / (float)(AlphaResolution - 1);
                float height = data.GetInterpolatedHeight(u, v) / MaxHeight;
                float slope = data.GetSteepness(u, v) / 90f;
                float riverDistance = Mathf.Min(Mathf.Abs(u - RiverCenter(v, false)), Mathf.Abs(u - RiverCenter(v, true)));
                float grass = Mathf.Clamp01(1f - slope * 2.8f) * Mathf.Clamp01(1f - Mathf.InverseLerp(0.48f, 0.66f, height));
                // Rear mountains should read primarily as exposed grey-brown rock.
                float rock = Mathf.Clamp01(slope * 2.6f) + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0.48f, height)) * 2.4f;
                float snow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.92f, height)) * 0.12f;
                float sand = Mathf.Clamp01(1f - riverDistance / 0.075f) * Mathf.Clamp01(1f - slope * 4f) * 2.2f;
                float soil = Mathf.Clamp01(slope * 1.4f) * Mathf.Clamp01(1f - snow) * 0.55f;
                float total = grass + rock + snow + sand + soil;
                map[z, x, 0] = grass / total;
                map[z, x, 1] = rock / total;
                map[z, x, 2] = snow / total;
                map[z, x, 3] = sand / total;
                map[z, x, 4] = soil / total;
            }
        }
        data.SetAlphamaps(0, 0, map);
    }

    private static void CreateRiver(string name, TerrainData data, Material material, bool east)
    {
        const int segments = 120;
        float halfWidth = east ? 35f : 31f;
        var vertices = new Vector3[(segments + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float v = i / (float)segments;
            float u = RiverCenter(v, east);
            float next = RiverCenter(Mathf.Min(1f, v + 0.004f), east);
            float previous = RiverCenter(Mathf.Max(0f, v - 0.004f), east);
            Vector2 tangent = new Vector2((next - previous) * Size, 0.008f * Size).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float cx = u * Size;
            float cz = v * Size;
            float y = data.GetInterpolatedHeight(u, v) + 1.6f;
            vertices[i * 2] = new Vector3(cx + normal.x * halfWidth, y, cz + normal.y * halfWidth);
            vertices[i * 2 + 1] = new Vector3(cx - normal.x * halfWidth, y, cz - normal.y * halfWidth);
            uv[i * 2] = new Vector2(0f, v * 10f);
            uv[i * 2 + 1] = new Vector2(1f, v * 10f);
            if (i < segments)
            {
                int b = i * 2, t = i * 6;
                triangles[t] = b; triangles[t + 1] = b + 2; triangles[t + 2] = b + 1;
                triangles[t + 3] = b + 1; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }
        }
        string path = Root + "/" + name.Replace(" ", "") + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool newMeshAsset = mesh == null;
        if (newMeshAsset) mesh = new Mesh();
        mesh.Clear();
        mesh.name = name + " Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (newMeshAsset) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateVillage(TerrainData data)
    {
        Material path = CreateMaterial("Village Paths", new Color(0.28f, 0.21f, 0.14f, 1f), 0.05f, false);
        GameObject housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowpolyBakersHouse/Prefabs/Baker_house.prefab");
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowpolyBakersHouse/Prefabs/Barrel.prefab");
        if (housePrefab == null) throw new InvalidOperationException("Baker_house prefab was not found.");
        Material houseMaterial = CreateTexturedUrpMaterial("BakerHouseURP", "Assets/LowpolyBakersHouse/Textures/Baker_house.tif");
        Material barrelMaterial = CreateTexturedUrpMaterial("BarrelURP", "Assets/LowpolyBakersHouse/Textures/Barrel.tif");
        GameObject root = new GameObject("Village");

        Vector2[] positions =
        {
            new Vector2(330,180), new Vector2(450,170), new Vector2(590,190), new Vector2(690,225),
            new Vector2(285,295), new Vector2(405,285), new Vector2(535,310), new Vector2(650,330),
            new Vector2(345,410), new Vector2(470,430), new Vector2(595,425), new Vector2(700,465),
            new Vector2(390,525), new Vector2(525,535), new Vector2(635,550)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            float scale = i == 5 || i == 9 ? 1.35f : UnityEngine.Random.Range(0.82f, 1.12f);
            CreateHouse(root.transform, data, positions[i], scale, housePrefab, houseMaterial, i);
            if (barrelPrefab != null && i % 3 == 0)
                CreateBarrels(root.transform, data, positions[i] + new Vector2(13f, -9f), barrelPrefab, barrelMaterial, i);
        }

        // Simple dirt paths give the settlement the connected layout seen in the reference.
        CreatePath(root.transform, data, path, new Vector2(300, 245), new Vector2(710, 450), 11f);
        CreatePath(root.transform, data, path, new Vector2(360, 155), new Vector2(610, 555), 9f);
        CreatePath(root.transform, data, path, new Vector2(280, 390), new Vector2(700, 310), 8f);
    }

    private static void CreateHouse(Transform parent, TerrainData data, Vector2 p, float scale, GameObject prefab, Material material, int index)
    {
        float y = data.GetInterpolatedHeight(p.x / Size, p.y / Size);
        GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        house.name = "Baker House_" + (index + 1).ToString("00");
        house.transform.SetParent(parent);
        house.transform.position = new Vector3(p.x, y, p.y);
        house.transform.rotation = Quaternion.Euler(0f, (index * 37f) % 85f - 42f, 0f);
        house.transform.localScale = Vector3.one * scale;
        foreach (Renderer renderer in house.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
    }

    private static void CreateBarrels(Transform parent, TerrainData data, Vector2 p, GameObject prefab, Material material, int index)
    {
        for (int i = 0; i < 2; i++)
        {
            Vector2 bp = p + new Vector2(i * 4f, i * 2f);
            float y = data.GetInterpolatedHeight(bp.x / Size, bp.y / Size);
            GameObject barrel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            barrel.name = "Barrel_" + index.ToString("00") + "_" + i;
            barrel.transform.SetParent(parent);
            barrel.transform.position = new Vector3(bp.x, y, bp.y);
            barrel.transform.rotation = Quaternion.Euler(0f, index * 29f + i * 20f, 0f);
            foreach (Renderer renderer in barrel.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }
    }

    private static Material CreateTexturedUrpMaterial(string name, string texturePath)
    {
        string path = Root + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader was not found.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException("Texture was not found: " + texturePath);
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.22f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreatePath(Transform parent, TerrainData data, Material material, Vector2 a, Vector2 b, float width)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
        Vector3[] vertices = new Vector3[4];
        Vector2[] points = { a + normal, a - normal, b + normal, b - normal };
        for (int i = 0; i < 4; i++) vertices[i] = new Vector3(points[i].x, data.GetInterpolatedHeight(points[i].x / Size, points[i].y / Size) + 0.35f, points[i].y);
        var mesh = new Mesh { vertices = vertices, triangles = new[] { 0, 2, 1, 1, 2, 3 }, uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one } };
        mesh.RecalculateNormals();
        var go = new GameObject("Dirt Path");
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Material CreateMaterial(string name, Color color, float smoothness, bool transparent)
    {
        string path = Root + "/" + name.Replace(" ", "") + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateLightingAndCamera()
    {
        var sunObject = new GameObject("Sun");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.75f;
        sun.color = new Color(1f, 0.92f, 0.80f);
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        RenderSettings.sun = sun;

        var cameraObject = new GameObject("Village Overview Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 52f;
        camera.farClipPlane = 2200f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(500f, 470f, -330f);
        cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 95f, 520f) - cameraObject.transform.position);
    }

    private static float RiverCenter(float v, bool east)
    {
        if (east) return 0.80f + Mathf.Sin(v * Mathf.PI * 1.25f - 0.4f) * 0.105f;
        return 0.18f + Mathf.Sin(v * Mathf.PI * 1.10f + 0.7f) * 0.065f;
    }

    private static float Fractal(float x, float y, int octaves)
    {
        float value = 0f, amplitude = 0.5f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x, y) * amplitude;
            total += amplitude;
            x = x * 2.03f + 11.7f;
            y = y * 2.03f + 6.1f;
            amplitude *= 0.5f;
        }
        return value / total;
    }
}
