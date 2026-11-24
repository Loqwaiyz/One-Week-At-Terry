using UnityEngine;

namespace Terry.MiniGames
{
    // The pivot sits at the feet of each one-piece cutout.
    public sealed class CardboardMotion : MonoBehaviour
    {
        [SerializeField, Min(0)] float tiltDegrees = 2.4f;
        [SerializeField, Min(0)] float cyclesPerSecond = 0.5f;
        [SerializeField, Range(0, 0.4f)] float squeeze;
        [SerializeField] bool varyRhythm = true;

        Quaternion restRotation;
        Vector3 restScale;
        float phase, rate;

        void Awake()
        {
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            rate = cyclesPerSecond * 2 * Mathf.PI;
            if (varyRhythm)
            {
                var random = new System.Random(GetInstanceID());
                phase = (float)random.NextDouble() * 2 * Mathf.PI;
                rate *= Mathf.Lerp(0.78f, 1.22f, (float)random.NextDouble());
            }
        }

        void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * rate + phase);
            transform.localRotation = restRotation * Quaternion.Euler(0, 0, wave * tiltDegrees);
            if (squeeze > 0)
                transform.localScale = Vector3.Scale(restScale, new Vector3(1 + wave * squeeze, 1, 1));
        }
    }
}
