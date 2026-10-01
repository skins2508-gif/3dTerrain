using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrassFlowerFieldInstaller
{
    private const string Request = "Assets/VillageValley/CreateGrassField.request";

    [InitializeOnLoadMethod]
    private static void InstallWhenRequested()
    {
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += RunWhenReady;
    }

    private static void RunWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RunWhenReady;
            return;
        }
        try
        {
            File.Delete("Assets/VillageValley/GrassFieldError.txt");
            Install();
        }
        catch (Exception exception)
        {
            File.WriteAllText("Assets/VillageValley/GrassFieldError.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Terrain/Create Grass Flower Field Object")]
    public static void Install()
    {
        string[] paths = { "Assets/VillageValley/VillageValley.unity", "Assets/VillageValley/VillageValley_Final.unity" };
        int installed = 0;
        foreach (string path in paths)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            bool wasLoaded = loaded.IsValid() && loaded.isLoaded;
            Scene scene = wasLoaded ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Terrain terrain = null;
            GrassFlowerField field = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (terrain == null) terrain = root.GetComponent<Terrain>();
                if (field == null) field = root.GetComponent<GrassFlowerField>();
            }
            if (terrain == null) throw new InvalidOperationException("Terrain is missing in " + path);
            if (field == null)
            {
                var go = new GameObject("Grass Flower Field");
                SceneManager.MoveGameObjectToScene(go, scene);
                field = go.AddComponent<GrassFlowerField>();
            }
            field.terrain = terrain;
            field.textures = new Texture2D[11];
            string rootPath = "Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/";
            field.textures[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(rootPath + "grass01.tga");
            for (int i = 1; i <= 10; i++) field.textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(rootPath + "grassFlower" + i.ToString("00") + ".tga");
            field.spacing = 3.2f;
            field.flowerChance = 0.38f;
            field.windStrength = 0.42f;
            field.windSpeed = 1.35f;
            field.Rebuild();
            EditorUtility.SetDirty(field);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            installed++;
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/VillageValley/GrassFieldResult.txt", "Scenes=" + installed + Environment.NewLine + "Component=GrassFlowerField" + Environment.NewLine + "Textures=11");
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Terrain/Add Grass Field To Current Scene")]
    public static void InstallIntoCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("No active scene is open.");
        Terrain terrain = null;
        GrassFlowerField field = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (terrain == null) terrain = root.GetComponent<Terrain>();
            if (field == null) field = root.GetComponent<GrassFlowerField>();
        }
        if (terrain == null) throw new InvalidOperationException("The active scene has no Terrain.");
        if (field == null)
        {
            var go = new GameObject("Grass Flower Field");
            SceneManager.MoveGameObjectToScene(go, scene);
            field = go.AddComponent<GrassFlowerField>();
        }
        Configure(field, terrain);
        field.Rebuild();
        EditorUtility.SetDirty(field);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = field.gameObject;
        File.WriteAllText("Assets/VillageValley/CurrentGrassFieldResult.txt",
            "Scene=" + scene.path + Environment.NewLine + "Object=" + field.gameObject.name + Environment.NewLine + "Textures=" + field.textures.Length);
        SceneView.RepaintAll();
    }

    private static void Configure(GrassFlowerField field, Terrain terrain)
    {
        field.terrain = terrain;
        field.textures = new Texture2D[11];
        string rootPath = "Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/";
        field.textures[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(rootPath + "grass01.tga");
        for (int i = 1; i <= 10; i++) field.textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(rootPath + "grassFlower" + i.ToString("00") + ".tga");
        field.spacing = 3.2f;
        field.flowerChance = 0.38f;
        field.windStrength = 0.42f;
        field.windSpeed = 1.35f;
        field.windColor = new Color(1f, 0.08f, 0.04f, 1f);
        field.windColorStrength = 0.72f;
    }
}
