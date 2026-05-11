using UnityEngine;
using UnityEditor;

public class GenerateFlatAreaTrigger : EditorWindow
{
    [MenuItem("Tools/Generate Flat Area Trigger Collider")]
    static void Execute()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("루트 오브젝트를 선택해주세요.");
            return;
        }

        Transform root = selected[0].transform;
        while (root.parent != null)
            root = root.parent;

        // 1. 전체 Renderer Bounds 계산
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Renderer가 없습니다.");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        // 2. 트리거용 빈 오브젝트 생성
        GameObject triggerObj = new GameObject("FlatArea_Trigger");
        Undo.RegisterCreatedObjectUndo(triggerObj, "Create Flat Area Trigger");

        // 루트의 자식으로 넣기
        triggerObj.transform.SetParent(root, worldPositionStays: false);

        // 3. 월드 Bounds 중심을 로컬 좌표로 변환해서 배치
        triggerObj.transform.position = new Vector3(
            bounds.center.x,
            bounds.min.y,       // Y는 바닥에 맞춤
            bounds.center.z
        );

        // 4. BoxCollider 추가 — Y는 얇게 (평탄화 트리거이므로)
        BoxCollider box = triggerObj.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(
            bounds.size.x,
            0.1f,               // 평면 트리거이므로 Y는 얇게
            bounds.size.z
        );
        box.center = Vector3.zero;

        // 5. 레이어 설정 안내
        Debug.Log($"✅ 트리거 생성 완료!\n" +
                  $"위치: {triggerObj.transform.position}\n" +
                  $"크기: {box.size}\n" +
                  $"⚠️ Layer를 'Trigger' 등으로 수동 설정해주세요.");

        Selection.activeGameObject = triggerObj;
    }
}