using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraHandler : MonoBehaviour, IPlayerNetworkConfigurable
{
    [Header("References")]
    private PlayerController player;
    private CinemachineCamera cinemachineCam;
    private CinemachineBasicMultiChannelPerlin noiseComponent;

    [Header("Headbob Settings")]
    public float walkFrequency = 2.0f;
    public float walkAmplitude = 0.1f;

    public float sprintFrequency = 3.0f;
    public float sprintAmplitude = 0.15f;

    public float crouchFrequency = 1.3f;
    public float crouchAmplitude = 0.05f;

    public float transitionSpeed = 5f;

    private float targetFrequency = 0f;
    private float targetAmplitude = 0f;
    private bool isSetupComplete = false;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Start()
    {
        // 테스트코드
        //TestLocalCameraSetup();
    }

    public void SetupLocalCamera(CinemachineCamera sceneCam)
    {
        cinemachineCam = sceneCam;
        cinemachineCam.Follow = player.CameraTransform;
        cinemachineCam.LookAt = player.CameraTransform;

        noiseComponent = cinemachineCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        isSetupComplete = true;
    }

    private void Update()
    {
        if (!isSetupComplete || noiseComponent == null || player == null)
        {
            return;
        }

        UpdateHeadbobTargets();

        noiseComponent.AmplitudeGain = Mathf.Lerp(noiseComponent.AmplitudeGain, targetAmplitude, Time.deltaTime * transitionSpeed);
        noiseComponent.FrequencyGain = Mathf.Lerp(noiseComponent.FrequencyGain, targetFrequency, Time.deltaTime * transitionSpeed);
    }

    private void UpdateHeadbobTargets()
    {
        Vector3 horizontalVelocity = new Vector3(player.Controller.velocity.x, 0f, player.Controller.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        if (player.IsGrounded && currentSpeed > 0.1f)
        {
            if (currentSpeed > player.walkSpeed + 0.5f)
            {
                targetAmplitude = sprintAmplitude;
                targetFrequency = sprintFrequency;
            }
            else if (currentSpeed < player.crouchSpeed + 0.5f)
            {
                targetAmplitude = crouchAmplitude;
                targetFrequency = crouchFrequency;
            }
            else
            {
                targetAmplitude = walkAmplitude;
                targetFrequency = walkFrequency;
            }
        }
        else
        {
            targetAmplitude = 0f;
            targetFrequency = 0f;
        }
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        if (!isLocalPlayer)
        {
            enabled = false;
        }
    }

    public void TestLocalCameraSetup()
    {
        // 임시로 씬에서 첫 번째 CinemachineCamera를 찾아 설정
        CinemachineCamera sceneCam = FindAnyObjectByType<CinemachineCamera>();
        if (sceneCam != null)
        {
            SetupLocalCamera(sceneCam);
        }
        else
        {
            Debug.LogWarning("씬에 CinemachineCamera가 없습니다.");
        }
    }
}