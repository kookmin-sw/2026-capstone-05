using UnityEngine;

namespace ToonShadersPro.URP
{
    public class Worm : MonoBehaviour
    {
        [SerializeField]
        private float rotationSpeed = 2.0f;

        [SerializeField]
        private float rotationSize = 5.0f;

        [SerializeField]
        private Vector3 offsetPosition = new Vector3(0.0f, 0.1f, 0.0f);

        [SerializeField]
        private Terrain terrain;

        private void Update()
        {
            float t = Time.time * rotationSpeed / (2 * Mathf.PI);

            var position = new Vector3(Mathf.Cos(t) * rotationSize, 0.0f, Mathf.Sin(t) * rotationSize);
            position += offsetPosition;

            if (terrain != null)
            {
                position.y += terrain.SampleHeight(position);
            }

            transform.position = position;

            var rotation = Quaternion.Euler(0.0f, -t * Mathf.Rad2Deg + 90.0f, 0.0f);

            transform.rotation = rotation;
        }
    }
}
