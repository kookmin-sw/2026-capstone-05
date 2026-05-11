using UnityEngine;
using UnityEditor;

public class AlignGroupToFloor : EditorWindow
{
    [MenuItem("Tools/Align Group - Bottom to Y0, Center XZ to 0")]
    static void Execute()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("오브젝트를 선택해주세요.");
            return;
        }

        // 1. 모든 선택 오브젝트의 루트를 찾기
        //    (선택한 것들의 공통 루트 or 각각의 루트)
        Transform commonRoot = selected[0].transform;
        while (commonRoot.parent != null)
            commonRoot = commonRoot.parent;

        // 2. 그룹 전체의 Bounds 계산 (모든 Renderer 기준)
        Renderer[] renderers = commonRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Renderer가 없어서 Bounds를 계산할 수 없습니다.");
            return;
        }

        Bounds totalBounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            totalBounds.Encapsulate(r.bounds);

        // 3. 오프셋 계산
        //    XZ: 그룹 중심을 0으로
        //    Y:  그룹 바닥(min.y)을 0으로
        Vector3 offset = new Vector3(
            -totalBounds.center.x,          // XZ 중심을 0으로
            -totalBounds.min.y,             // 바닥을 Y=0으로
            -totalBounds.center.z
        );

        // 4. 루트 오브젝트에 오프셋 적용
        Undo.RecordObject(commonRoot, "Align Group To Floor");
        commonRoot.position += offset;

        Debug.Log($"완료! Bounds 중심: {totalBounds.center}, 바닥 Y: {totalBounds.min.y}, 적용 오프셋: {offset}");
    }
}