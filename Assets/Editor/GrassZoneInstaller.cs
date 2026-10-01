using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrassZoneInstaller
{
    private const string Request = "Assets/VillageValley/AddGrassZones.request";

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
        catch (Exception exception) { File.WriteAllText("Assets/VillageValley/GrassZoneError.txt", exception.ToString()); }
    }

    [MenuItem("Tools/Terrain/Add Flower And Erase Zones")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        Terrain terrain = Terrain.activeTerrain;
        if (!scene.IsValid() || terrain == null) throw new InvalidOperationException("Open a scene containing the active Terrain first.");

        FlowerZone flower = UnityEngine.Object.FindFirstObjectByType<FlowerZone>();
        if (flower == null || flower.gameObject.scene != scene)
        {
            var go = new GameObject("Flower Zone 01");
            SceneManager.MoveGameObjectToScene(go, scene);
            flower = go.AddComponent<FlowerZone>();
            go.transform.position = TerrainPoint(terrain, 0.52f, 0.38f);
        }

        GrassEraseZone erase = UnityEngine.Object.FindFirstObjectByType<GrassEraseZone>();
        if (erase == null || erase.gameObject.scene != scene)
        {
            var go = new GameObject("Grass Erase Zone 01");
            SceneManager.MoveGameObjectToScene(go, scene);
            erase = go.AddComponent<GrassEraseZone>();
            go.transform.position = TerrainPoint(terrain, 0.50f, 0.24f);
        }

        GrassFlowerField field = UnityEngine.Object.FindFirstObjectByType<GrassFlowerField>();
        if (field != null) { field.Rebuild(); EditorUtility.SetDirty(field); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.objects = new UnityEngine.Object[] { flower.gameObject, erase.gameObject };
        File.WriteAllText("Assets/VillageValley/GrassZoneResult.txt", "Scene=" + scene.path + Environment.NewLine + "FlowerZone=" + flower.name + Environment.NewLine + "EraseZone=" + erase.name);
        SceneView.RepaintAll();
    }

    private static Vector3 TerrainPoint(Terrain terrain, float u, float v)
    {
        Vector3 origin = terrain.transform.position;
        TerrainData data = terrain.terrainData;
        return origin + new Vector3(u * data.size.x, data.GetInterpolatedHeight(u, v), v * data.size.z);
    }
}
