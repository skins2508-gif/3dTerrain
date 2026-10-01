using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrassVisibilityRepair
{
    private const string Request = "Assets/VillageValley/RepairGrassVisibility.request";

    [InitializeOnLoadMethod]
    private static void RunWhenRequested()
    {
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += Repair;
    }

    [MenuItem("Tools/Terrain/Repair Grass Visibility")]
    public static void Repair()
    {
        GrassDetailInstaller.Install();
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/VillageValley/VillageValleyTerrain.asset");
        if (data == null) throw new InvalidOperationException("Village TerrainData is missing.");

        int grassTotal = Sum(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0));
        int flowerTotal = Sum(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 1));
        string[] scenes = { "Assets/VillageValley/VillageValley.unity", "Assets/VillageValley/VillageValley_Final.unity" };
        foreach (string path in scenes)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            bool wasLoaded = loaded.IsValid() && loaded.isLoaded;
            Scene scene = wasLoaded ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Terrain terrain = root.GetComponent<Terrain>();
                if (terrain == null) continue;
                terrain.terrainData = data;
                terrain.drawTreesAndFoliage = true;
                terrain.detailObjectDistance = 300f;
                terrain.detailObjectDensity = 1f;
                terrain.treeDistance = 1600f;
                TerrainCollider collider = root.GetComponent<TerrainCollider>();
                if (collider != null) collider.terrainData = data;
                EditorUtility.SetDirty(terrain);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/VillageValley/GrassVisibilityResult.txt",
            "Prototypes=" + data.detailPrototypes.Length + Environment.NewLine +
            "GrassInstances=" + grassTotal + Environment.NewLine +
            "FlowerInstances=" + flowerTotal + Environment.NewLine +
            "Trees=" + data.treeInstanceCount);
        SceneView.RepaintAll();
    }

    private static int Sum(int[,] values)
    {
        long sum = 0;
        foreach (int value in values) sum += value;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }
}
