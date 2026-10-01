using System;
using System.IO;
using UnityEditor;

public static class CurrentGrassFieldRequest
{
    private const string Request = "Assets/VillageValley/AddGrassFieldToCurrentScene.request";

    [InitializeOnLoadMethod]
    private static void Run()
    {
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += TryInstall;
    }

    private static void TryInstall()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += TryInstall;
            return;
        }
        try { GrassFlowerFieldInstaller.InstallIntoCurrentScene(); }
        catch (Exception exception)
        {
            File.WriteAllText("Assets/VillageValley/CurrentGrassFieldError.txt", exception.ToString());
            UnityEngine.Debug.LogException(exception);
        }
    }
}
