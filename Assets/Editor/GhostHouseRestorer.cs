using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GhostHouseRestorer
{
    private const string ScenePath = "Assets/Samples/VillageValley_Final.unity";
    private const string HousePrefabPath = "Assets/LowpolyBakersHouse/Prefabs/Baker_house.prefab";
    private const string RequestPath = "Assets/RESTORE_GOSTHOUSE.request";

    static GhostHouseRestorer()
    {
        EditorApplication.delayCall += RestoreWhenRequested;
    }

    [MenuItem("Tools/Village Valley/Restore Gosthouse")]
    public static void Restore()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject brokenHouse = GameObject.Find("gosthouse");

        Vector3 position = new Vector3(505.8f, 68.6f, 190.6f);
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;
        Transform parent = null;

        if (brokenHouse != null)
        {
            position = brokenHouse.transform.position;
            rotation = brokenHouse.transform.rotation;
            scale = brokenHouse.transform.localScale;
            parent = brokenHouse.transform.parent;
            Object.DestroyImmediate(brokenHouse);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefabPath);
        if (prefab == null)
            throw new FileNotFoundException("복구용 집 프리팹을 찾지 못했습니다.", HousePrefabPath);

        GameObject restoredHouse = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        restoredHouse.name = "gosthouse";
        restoredHouse.transform.SetParent(parent, true);
        restoredHouse.transform.SetPositionAndRotation(position, rotation);
        restoredHouse.transform.localScale = scale;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = restoredHouse;
        Debug.Log($"[GhostHouseRestorer] 원래 집 복구 완료: {HousePrefabPath} at {position}");
    }

    private static void RestoreWhenRequested()
    {
        if (!File.Exists(RequestPath))
            return;

        try
        {
            Restore();
        }
        finally
        {
            if (File.Exists(RequestPath))
                File.Delete(RequestPath);
            AssetDatabase.Refresh();
        }
    }
}
