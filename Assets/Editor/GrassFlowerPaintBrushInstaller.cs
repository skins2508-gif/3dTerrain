using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrassFlowerPaintBrushInstaller
{
    private const string Request = "Assets/VillageValley/AddPaintBrush.request";

    [InitializeOnLoadMethod]
    private static void Run()
    {
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += InstallWhenReady;
    }

    private static void InstallWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += InstallWhenReady;
            return;
        }
        try { Install(); }
        catch (Exception exception) { File.WriteAllText("Assets/VillageValley/PaintBrushError.txt", exception.ToString()); }
    }

    [MenuItem("Tools/Terrain/Add Grass Flower Paint Brush")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        Terrain terrain = Terrain.activeTerrain;
        GrassFlowerField field = UnityEngine.Object.FindFirstObjectByType<GrassFlowerField>();
        if (!scene.IsValid() || terrain == null || field == null) throw new InvalidOperationException("The current scene needs Terrain and Grass Flower Field.");
        GrassFlowerPaintBrush brush = UnityEngine.Object.FindFirstObjectByType<GrassFlowerPaintBrush>();
        if (brush == null || brush.gameObject.scene != scene)
        {
            var go = new GameObject("Grass Flower Paint Brush");
            SceneManager.MoveGameObjectToScene(go, scene);
            brush = go.AddComponent<GrassFlowerPaintBrush>();
        }
        brush.terrain = terrain;
        brush.field = field;
        EditorUtility.SetDirty(brush);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = brush.gameObject;
        File.WriteAllText("Assets/VillageValley/PaintBrushResult.txt", "Scene=" + scene.path + Environment.NewLine + "Object=" + brush.name);
        SceneView.RepaintAll();
    }
}
