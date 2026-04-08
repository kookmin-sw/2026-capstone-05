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
            flattener.FlattenTerrain();

        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
        if (GUILayout.Button("⬡  Copy Origin (순수 원본)", GUILayout.Height(32)))
            flattener.CopyOriginTerrainRaw();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("── 절차적 지형 생성 ──", EditorStyles.centeredGreyMiniLabel);

        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("🌍  원본 없이 신규 지형 생성", GUILayout.Height(38)))
            flattener.GenerateTerrainFromScratch();

        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "에디터 / 런타임 모두 동작합니다.\n" +
            "에디터에서 실행 시 Ctrl+Z(Undo)로 되돌릴 수 있습니다.",
            MessageType.Info);
    }
}
#endif
