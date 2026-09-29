using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>High three-quarter follow camera. Keeps the horde readable around the player.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public float pitch = 55f, distance = 23f, yaw = 0f;
        public bool orbit, firstPerson;
        private float kick;
        private Vector3 focus;
        private float shake, shakeSeed;
        private float zoom = 1f;

        public void Init()
        {
            Cam = gameObject.AddComponent<Camera>();
            Cam.fieldOfView = 42f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 400f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.55f, 0.75f, 0.92f);
            Cam.allowMSAA = true;
            gameObject.tag = "MainCamera";
            gameObject.AddComponent<AudioListener>();
            shakeSeed = Random.value * 100f;
        }

        public void Snap(Vector3 target)
        {
            focus = target;
            Apply(0f);
        }

        public void Shake(float amount) => shake = Mathf.Min(1.2f, Mathf.Max(shake, amount));
        public void Kick(float degrees) => kick = Mathf.Max(kick, degrees);
        public void SetZoom(float z) => zoom = z;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (firstPerson && G.Player != null && !orbit)
            {
                FirstPersonView(dt);
                return;
            }
            Cam.fieldOfView = 42f;
            Cam.nearClipPlane = 0.5f;
            if (orbit) yaw += dt * 6f;
            else if (G.Player != null)
            {
                var target = G.Player.transform.position + G.Player.Velocity * 0.18f;
                focus = Vector3.Lerp(focus, target, 1f - Mathf.Exp(-8f * dt));
            }
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            Apply(dt);
        }

        private void Apply(float dt)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -distance * zoom);
            Vector3 jitter = Vector3.zero;
            if (shake > 0f)
            {
                float t = Time.unscaledTime * 30f;
                jitter = new Vector3(Mathf.PerlinNoise(shakeSeed, t) - 0.5f, Mathf.PerlinNoise(shakeSeed + 5f, t) - 0.5f, 0f) * shake * 0.9f;
            }
            transform.SetPositionAndRotation(focus + offset + rot * jitter, rot);
        }

        public void SetFocus(Vector3 f) => focus = f;

        private void FirstPersonView(float dt)
        {
            var p = G.Player;
            Cam.fieldOfView = 78f;
            Cam.nearClipPlane = 0.05f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            kick = Mathf.MoveTowards(kick, 0f, dt * 30f);
            float speed = p.Velocity.magnitude;
            float bob = Mathf.Sin(Time.time * 11f) * 0.04f * Mathf.Clamp01(speed / 5f);
            var jitter = Vector3.zero;
            if (shake > 0f)
            {
                float t = Time.unscaledTime * 30f;
                jitter = new Vector3(Mathf.PerlinNoise(shakeSeed, t) - 0.5f, Mathf.PerlinNoise(shakeSeed + 5f, t) - 0.5f, 0f) * shake * 4f;
            }
            var rot = Quaternion.Euler(p.LookPitch - kick + jitter.y, p.LookYaw + jitter.x, 0f);
            var eye = p.Position + Vector3.up * (PlayerAnim.EyeHeight + bob);
            transform.SetPositionAndRotation(eye, rot);
            focus = p.Position;
        }
    }
}
