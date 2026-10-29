using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // Bird's eye camera: pitch fixed at 90 (straight down), follows the target, height 50..400 m.
    // Q/E or mouse wheel change the height at runtime.
    [RequireComponent(typeof(Camera))]
    public class TopDownCamera : MonoBehaviour
    {
        public Transform Target;
        public float MinHeight = 50f;
        public float MaxHeight = 400f;
        [SerializeField] float _height = 200f;
        public float HeightSpeed = 150f;

        Camera _cam;

        public float Height
        {
            get => _height;
            set => _height = Mathf.Clamp(value, MinHeight, MaxHeight);
        }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            Height = _height;
        }

        void LateUpdate()
        {
            float input = 0f;
            if (Input.GetKey(KeyCode.Q)) input -= 1f;
            if (Input.GetKey(KeyCode.E)) input += 1f;
            Height += input * HeightSpeed * Time.unscaledDeltaTime - Input.mouseScrollDelta.y * 20f;
            Snap();
        }

        public void Snap()
        {
            var p = Target != null ? Target.position : Vector3.zero;
            transform.SetPositionAndRotation(new Vector3(p.x, p.y + _height, p.z), Quaternion.Euler(90f, 0f, 0f));
        }

        // camera footprint on the ground plane (y = 0), valid because pitch is exactly 90
        public GroundRect GetVisibleGroundRect()
        {
            var p = transform.position;
            if (_cam.orthographic)
                return GroundRect.TopDownOrthographic(p.x, p.z, _cam.orthographicSize, _cam.aspect);
            return GroundRect.TopDownPerspective(p.x, p.z, p.y, _cam.fieldOfView, _cam.aspect);
        }
    }
}
