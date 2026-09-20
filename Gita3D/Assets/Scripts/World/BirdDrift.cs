using UnityEngine;

namespace Gita.World
{
    /// <summary>
    /// Carries a bird on a slow, banked circle high above the field, always turned to
    /// face the way it is going. Purely decorative - the only thing in the scene that
    /// moves besides the cloth.
    /// </summary>
    public sealed class BirdDrift : MonoBehaviour
    {
        public Vector3 centre = new(0f, 0f, 70f);
        public float radius = 50f;
        public float height = 34f;
        public float speed = 0.05f;
        public float phase;

        void Update()
        {
            float t = Time.time * speed + phase;

            // Slightly elliptical, so the flock does not read as a perfect circle.
            var pos = centre + new Vector3(
                Mathf.Cos(t) * radius,
                height + Mathf.Sin(t * 1.7f) * 2.2f,
                Mathf.Sin(t) * radius * 0.7f);

            var next = centre + new Vector3(
                Mathf.Cos(t + 0.05f) * radius,
                height,
                Mathf.Sin(t + 0.05f) * radius * 0.7f);

            transform.position = pos;

            var forward = (next - pos).normalized;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up)
                                   * Quaternion.Euler(90f, 0f, 0f); // wings lie in the horizontal
        }
    }
}
