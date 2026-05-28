using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ToonShadersPro.URP
{
    public class VertexColorRandom : MonoBehaviour
    {
        [SerializeField] private bool randomPerTriangle = false;

        private void Start()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var mesh = meshFilter.mesh;

            var randomColor = Random.ColorHSV();

            var colors = new Color[mesh.vertexCount];

            for(int i = 0; i < mesh.vertexCount; i++)
            {
                if(randomPerTriangle)
                {
                    randomColor = Random.ColorHSV();
                }

                colors[i] = randomColor;
            }

            mesh.SetColors(colors);
        }
    }
}
