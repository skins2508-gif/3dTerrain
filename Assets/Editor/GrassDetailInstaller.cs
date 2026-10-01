using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GrassDetailInstaller
{
    private const string RequestPath = "Assets/VillageValley/InstallGrass.request";
    private const string ResultPath = "Assets/VillageValley/GrassInstallResult.txt";
    private const string TerrainPath = "Assets/VillageValley/VillageValleyTerrain.asset";
    private const int Resolution = 512;

    [InitializeOnLoadMethod]
    private static void InstallWhenRequested()
    {
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        EditorApplication.delayCall += () =>
        {
            try { Install(); }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Terrain/Install Downloaded Grass")]
    public static void Install()
    {
        string textureRoot = "Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/";
        string[] textureNames =
        {
            "grass01.tga",
            "grassFlower01.tga", "grassFlower02.tga", "grassFlower03.tga", "grassFlower04.tga", "grassFlower05.tga",
            "grassFlower06.tga", "grassFlower07.tga", "grassFlower08.tga", "grassFlower09.tga", "grassFlower10.tga"
        };
        foreach (string textureName in textureNames) PrepareGrassTexture(textureRoot + textureName);
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        var textures = new Texture2D[textureNames.Length];
        for (int i = 0; i < textureNames.Length; i++) textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + textureNames[i]);
        if (data == null || Array.Exists(textures, texture => texture == null))
            throw new InvalidOperationException("TerrainData or downloaded grass textures are missing.");

        data.SetDetailResolution(Resolution, 16);
        var prototypes = new DetailPrototype[textures.Length];
        prototypes[0] = CreatePrototype(textures[0], 0.75f, 1.45f, 0.9f, 1.75f, new Color(0.48f, 0.70f, 0.28f), new Color(0.28f, 0.43f, 0.16f));
        for (int i = 1; i < prototypes.Length; i++)
            prototypes[i] = CreatePrototype(textures[i], 0.9f, 1.6f, 1.15f, 2.0f, Color.white, new Color(0.70f, 0.78f, 0.52f));
        data.detailPrototypes = prototypes;

        var grassMap = new int[Resolution, Resolution];
        var flowerMaps = new int[10][,];
        for (int i = 0; i < flowerMaps.Length; i++) flowerMaps[i] = new int[Resolution, Resolution];
        float[,,] alphaMaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        int grassCells = 0, flowerCells = 0;
        for (int z = 0; z < Resolution; z++)
        {
            float v = z / (float)(Resolution - 1);
            for (int x = 0; x < Resolution; x++)
            {
                float u = x / (float)(Resolution - 1);
                float worldX = u * data.size.x;
                float worldZ = v * data.size.z;
                int alphaX = Mathf.Clamp(Mathf.RoundToInt(u * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int alphaZ = Mathf.Clamp(Mathf.RoundToInt(v * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                // Terrain layer 0 is Grass_A. Cover every area visually painted as grass,
                // while naturally excluding Rock, Sand, Snow, and Soil-dominant regions.
                bool grassPainted = alphaMaps.GetLength(2) > 0 && alphaMaps[alphaZ, alphaX, 0] >= 0.34f;
                bool clearOfHouse = !NearHouse(worldX, worldZ);
                if (!grassPainted || !clearOfHouse) continue;

                // Keep a consistent carpet instead of large noisy clumps.
                int grassDensity = 12 + ((x * 17 + z * 31) & 3);
                grassMap[z, x] = grassDensity;
                grassCells++;

                // Distribute flowers sparsely and evenly through the grass carpet.
                // Roughly one third of grass cells receive flowers. A spatial hash
                // cycles evenly through all ten downloaded GrassFlowers variants.
                if (Mathf.Abs(x * 11 + z * 17) % 3 == 0)
                {
                    int flowerSelector = Mathf.Abs(x * 37 + z * 53 + (x / 7) * 19) % 10;
                    flowerMaps[flowerSelector][z, x] = 2 + ((x + z) & 1);
                    flowerCells++;
                }
            }
        }

        data.SetDetailLayer(0, 0, 0, grassMap);
        for (int i = 0; i < flowerMaps.Length; i++) data.SetDetailLayer(0, 0, i + 1, flowerMaps[i]);
        data.wavingGrassStrength = 0.48f;
        data.wavingGrassSpeed = 0.72f;
        data.wavingGrassAmount = 0.34f;
        data.wavingGrassTint = new Color(0.76f, 0.86f, 0.65f, 1f);
        int treeCount = InstallTrees(data);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        File.WriteAllText(ResultPath, "Prototypes=" + data.detailPrototypes.Length + Environment.NewLine + "GrassCells=" + grassCells + Environment.NewLine + "FlowerCells=" + flowerCells + Environment.NewLine + "FlowerVariants=10" + Environment.NewLine + "Trees=" + treeCount + Environment.NewLine + "DetailResolution=" + Resolution);
        AssetDatabase.Refresh();
    }

    private static void PrepareGrassTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Grass texture importer is missing: " + path);
        bool changed = false;
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    private static int InstallTrees(TerrainData data)
    {
        string root = "Assets/R3DWorks/Free/URP_RiversidePlants/Trees/Prefabs/";
        GameObject big = AssetDatabase.LoadAssetAtPath<GameObject>(root + "birch_big_002.prefab");
        GameObject medium = AssetDatabase.LoadAssetAtPath<GameObject>(root + "birch_medium_001.prefab");
        GameObject small = AssetDatabase.LoadAssetAtPath<GameObject>(root + "birch_small_001.prefab");
        if (big == null || medium == null || small == null)
            throw new InvalidOperationException("Downloaded R3DWorks birch prefabs are missing.");

        data.treePrototypes = new[]
        {
            new TreePrototype { prefab = big, bendFactor = 0.15f },
            new TreePrototype { prefab = medium, bendFactor = 0.2f },
            new TreePrototype { prefab = small, bendFactor = 0.25f }
        };

        var trees = new System.Collections.Generic.List<TreeInstance>();
        var random = new System.Random(64031);
        for (int attempt = 0; attempt < 1800 && trees.Count < 230; attempt++)
        {
            float u = 0.06f + (float)random.NextDouble() * 0.88f;
            float v = 0.10f + (float)random.NextDouble() * 0.62f;
            float worldX = u * data.size.x;
            float worldZ = v * data.size.z;
            float height = data.GetInterpolatedHeight(u, v) / data.size.y;
            float slope = data.GetSteepness(u, v);
            bool perimeter = worldZ > 470f || worldX < 255f || worldX > 745f;
            if (!perimeter || height < 0.058f || height > 0.38f || slope > 32f || NearHouse(worldX, worldZ)) continue;

            int prototype = random.Next(0, 3);
            float scale = 0.78f + (float)random.NextDouble() * 0.62f;
            trees.Add(new TreeInstance
            {
                position = new Vector3(u, 0f, v),
                prototypeIndex = prototype,
                widthScale = scale,
                heightScale = scale * (0.92f + (float)random.NextDouble() * 0.18f),
                rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        data.SetTreeInstances(trees.ToArray(), true);
        return trees.Count;
    }

    private static DetailPrototype CreatePrototype(Texture2D texture, float minWidth, float maxWidth, float minHeight, float maxHeight, Color healthy, Color dry)
    {
        return new DetailPrototype
        {
            prototypeTexture = texture,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = minWidth,
            maxWidth = maxWidth,
            minHeight = minHeight,
            maxHeight = maxHeight,
            noiseSpread = 0.18f,
            healthyColor = healthy,
            dryColor = dry
        };
    }

    private static bool NearHouse(float x, float z)
    {
        Vector2[] houses =
        {
            new Vector2(330,180), new Vector2(450,170), new Vector2(590,190), new Vector2(690,225),
            new Vector2(285,295), new Vector2(405,285), new Vector2(535,310), new Vector2(650,330),
            new Vector2(345,410), new Vector2(470,430), new Vector2(595,425), new Vector2(700,465),
            new Vector2(390,525), new Vector2(525,535), new Vector2(635,550)
        };
        Vector2 point = new Vector2(x, z);
        foreach (Vector2 house in houses)
            if ((point - house).sqrMagnitude < 28f * 28f) return true;
        return false;
    }
}
