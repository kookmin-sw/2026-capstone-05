using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAI))]
public class EnemyLocalDriver : MonoBehaviour
{
    [Header("Local Simulation")]
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private bool disableWhenNetworked = true;
    [SerializeField] private EnemyAI enemy;

    private EnemyHealth health;
    private bool isDriving;
    private bool hasStarted;

    private void Awake()
    {
        enemy = enemy != null ? enemy : GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        hasStarted = true;

        if (startOnEnable)
        {
            StartDriving();
        }
    }

    private void OnEnable()
    {
        if (hasStarted && startOnEnable)
        {
            StartDriving();
        }
    }

    private void OnDisable()
    {
        StopDriving();
    }

    private void Update()
    {
        if (!isDriving)
            return;

        if (ShouldYieldToNetwork())
        {
            StopDriving();
            return;
        }

        enemy.TickLocalSimulation(Time.deltaTime);
    }

    public void StartDriving()
    {
        enemy = enemy != null ? enemy : GetComponent<EnemyAI>();
        health = health != null ? health : GetComponent<EnemyHealth>();

        if (enemy == null || ShouldYieldToNetwork())
            return;

        enemy.StartLocalSimulation();
        if (!enemy.IsLocalSimulationActive)
            return;

        health?.InitializeLocalHealth();
        isDriving = true;
    }

    public void StopDriving()
    {
        if (!isDriving)
            return;

        enemy.StopLocalSimulation();
        isDriving = false;
    }

    private bool ShouldYieldToNetwork()
    {
        return disableWhenNetworked &&
               enemy != null &&
               enemy.Runner != null;
    }
}
