using UnityEngine;

namespace Gita.World
{
    /// <summary>
    /// Waves a banner mesh as if caught in the dawn wind. The cloth is pinned at its
    /// left edge (the pole) and free at the right, so amplitude grows across the span.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class ClothWave : MonoBehaviour
    {
        public float amplitude = 0.16f;
        public float frequency = 2.4f;
        public float speed = 2.0f;
        public float phase;

        Mesh _mesh;
        Vector3[] _rest;
        Vector3[] _work;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            // Instance the mesh so several banners can wave independently.
            _mesh = Instantiate(filter.sharedMesh);
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            _rest = _mesh.vertices;
            _work = new Vector3[_rest.Length];

            if (phase == 0f) phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            if (_rest == null) return;

            float t = Time.time * speed + phase;
            float width = Mathf.Max(_mesh.bounds.size.x, 0.0001f);

            for (int i = 0; i < _rest.Length; i++)
            {
                var p = _rest[i];
                // 0 at the pole, 1 at the free edge - the tail whips, the root stays put.
                float grip = Mathf.Clamp01(p.x / width);
                float falloff = grip * grip;

                float wave = Mathf.Sin(p.x * frequency - t) * amplitude * falloff
                           + Mathf.Sin(p.y * frequency * 0.7f + t * 1.3f) * amplitude * 0.35f * falloff;

                p.z += wave;
                p.y += Mathf.Sin(p.x * frequency * 0.5f - t * 0.8f) * amplitude * 0.25f * falloff;
                _work[i] = p;
            }

            _mesh.vertices = _work;
            _mesh.RecalculateNormals();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
