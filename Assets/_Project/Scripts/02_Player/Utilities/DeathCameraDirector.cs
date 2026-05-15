using DG.Tweening;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class DeathCameraDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private RigBuilder[] rigBuilders;

    [Header("Camera Drop Settings")]
    [SerializeField] private float dropDuration = 2f;
    [SerializeField] private Ease dropEase = Ease.InQuad;

    [SerializeField] private Vector3 targetLocalPosition = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 targetLocalRotation = new Vector3(20f, 0f, 35f);

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private Rig[] rigs;

    private void Awake()
    {
        if (rigBuilders != null && rigBuilders.Length > 0)
        {
            rigs = new Rig[rigBuilders.Length];
            for (int i = 0; i < rigBuilders.Length; i++)
            {
                rigs[i] = rigBuilders[i].layers[0].rig;
            }
        }
    }

    private void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = transform;
        }

        if (cameraTransform != null)
        {
            originalLocalPosition = cameraTransform.localPosition;
            originalLocalRotation = cameraTransform.localRotation;
        }
    }

    public void PlayDeathSequence()
    {
        if (cameraTransform == null) return;

        cameraTransform.DOKill();

        cameraTransform.DOLocalMove(targetLocalPosition, dropDuration).SetEase(dropEase);
        cameraTransform.DOLocalRotateQuaternion(Quaternion.Euler(targetLocalRotation), dropDuration).SetEase(dropEase);

        if (rigs != null)
        {
            foreach (var rig in rigs)
            {
                DOTween.To(() => rig.weight, x => rig.weight = x, 0f, dropDuration).SetEase(dropEase);
            }
        }
    }

    public void PlayReviveSequence()
    {
        if (cameraTransform == null) return;

        cameraTransform.DOKill();

        cameraTransform.localPosition = originalLocalPosition;
        cameraTransform.localRotation = originalLocalRotation;

        if (rigs != null)
        {
            foreach (var rig in rigs)
            {
                rig.DOKill();
                rig.weight = 1f;
            }
        }
    }
}