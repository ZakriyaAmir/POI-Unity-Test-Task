using UnityEngine;

namespace Poi.Runtime
{
    // WASD / arrows to move over the ground, Shift = faster. AutoRun drives in a straight line
    // and bounces off the world edge (used for the benchmark so the player is always moving).
    public class PlayerController : MonoBehaviour
    {
        public float Speed = 80f;       // m/s, the world is big
        public float SprintMultiplier = 4f;
        public bool AutoRun;
        public float WorldMin = -8000f;
        public float WorldSize = 16000f;

        Vector3 _autoDir = new Vector3(0.8f, 0f, 0.6f);

        void Update()
        {
            Vector3 dir;
            if (AutoRun)
            {
                dir = _autoDir;
            }
            else
            {
                dir = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
                if (dir.sqrMagnitude > 1f) dir.Normalize();
            }

            float speed = Speed * (Input.GetKey(KeyCode.LeftShift) ? SprintMultiplier : 1f);
            var p = transform.position + dir * (speed * Time.deltaTime);

            float max = WorldMin + WorldSize;
            if (p.x < WorldMin || p.x > max) { _autoDir.x = -_autoDir.x; p.x = Mathf.Clamp(p.x, WorldMin, max); }
            if (p.z < WorldMin || p.z > max) { _autoDir.z = -_autoDir.z; p.z = Mathf.Clamp(p.z, WorldMin, max); }
            transform.position = p;
        }
    }
}
