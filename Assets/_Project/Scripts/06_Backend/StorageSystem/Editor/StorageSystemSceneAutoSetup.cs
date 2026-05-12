#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StorageSystemSceneAutoSetup
{
    private const string RequestPath = "Assets/_Project/Scripts/06_Backend/StorageSystem/Editor/.storage_scene_setup_request";

    static StorageSystemSceneAutoSetup()
    {
        EditorApplication.delayCall += RunIfRequested;
    }

    private static void RunIfRequested()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
        {
            return;
        }

        try
        {
            File.Delete(RequestPath);
            StorageSystemSceneSetup.SetupGameScene();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
#endif
