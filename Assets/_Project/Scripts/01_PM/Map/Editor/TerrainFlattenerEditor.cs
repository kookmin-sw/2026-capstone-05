#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainFlattener))]
public class TerrainFlattenerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        var flattener = (TerrainFlattener)target;

        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
        if (GUILayout.Button("▶  Flatten Terrain", GUILayout.Height(38)))
        {
            flattener.FlattenTerrain();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "에디터 / 런타임 모두 동작합니다.\n" +
            "에디터에서 실행 시 Ctrl+Z(Undo)로 되돌릴 수 있습니다.\n" +
            "런타임에서는 UI Button → FlattenTerrain() 을 연결하세요.",
            MessageType.Info);
    }
}
#endif
