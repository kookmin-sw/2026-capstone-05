using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 평탄화된 구역에 건물이나 프랍들을 배치하는 전용 컴포넌트입니다.
/// 추후 다수 건물 파싱, 회전, 그리드 정렬 등 복잡한 생성 로직을 이 클래스에서 독립적으로 관리할 수 있습니다.
/// </summary>
public class BuildingSpawner : MonoBehaviour
{
    [Header("Buildings")]
    [Tooltip("스폰할 건물 프리팹 목록 (Building_1 ~ 13 등 연결)")]
    public GameObject[] buildingPrefabs;

    /// <summary>
    /// 지정된 위치(지면 높이)에 건물을 한 채 배치합니다.
    /// </summary>
    public void SpawnBuilding(Vector3 position, Transform parent, string instName = "Building")
    {
        if (buildingPrefabs == null || buildingPrefabs.Length == 0)
        {
            Debug.LogWarning("[BuildingSpawner] 등록된 건물 프리팹이 없습니다.");
            return;
        }

        GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
        if (prefab != null)
        {
#if UNITY_EDITOR
            GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            GameObject building = Instantiate(prefab, parent);
#endif
            building.name = instName;
            building.transform.position = position;
            
            // TODO: 이곳에 무작위 회전 보정이나, 건물 반경을 고려해 여러 개를 겹치지 않게 배치하는 로직 등을 점차 확장할 수 있습니다.
        }
    }
}
