using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // Click a marker -> a small panel shows that POI's details.
    // No colliders: we turn the mouse into a ground spot and ask the picker what is there.
    // The panel reads the POI again every frame, so it stays live (moves, priority changes).
    public class MarkerClickDetails : MonoBehaviour
    {
        public PoiDemo Demo;
        public Camera Cam;

        [Header("Tweak")]
        public float ClickRadius = 8f;          // meters, about the marker icon size
        public float IgnoreClicksLeftOf = 350f; // pixels, the control panel lives there
        public Rect PanelRect = new Rect(0, 10, 260, 150); // x is set from the right edge

        PoiPicker _picker;
        int _selectedId = -1;

        void Start()
        {
            _picker = new PoiPicker(Demo.Store);
            _picker.CanPick = IsMarkerOnScreen; // made once, no alloc per click
        }

        bool IsMarkerOnScreen(int id) => Demo.Presenter != null && Demo.Presenter.IsShowing(id);

        void Update()
        {
            if (_picker == null || !Demo.Ready) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Input.mousePosition.x < IgnoreClicksLeftOf) return;
            if (IsMouseOverPanel()) return;

            if (TryGetGroundSpot(Input.mousePosition, out var spot))
            {
                _selectedId = _picker.FindPoiAt(spot.x, spot.z, ClickRadius);
            }
        }

        // shoot a ray from the camera through the mouse and see where it hits the ground (y = 0)
        bool TryGetGroundSpot(Vector3 mouse, out Vector3 spot)
        {
            var ray = Cam.ScreenPointToRay(mouse);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance))
            {
                spot = ray.GetPoint(distance);
                return true;
            }
            spot = Vector3.zero;
            return false;
        }

        bool IsMouseOverPanel()
        {
            if (_selectedId < 0) return false;
            var guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return GetPanelRect().Contains(guiMouse);
        }

        Rect GetPanelRect()
        {
            var r = PanelRect;
            r.x = Screen.width - r.width - 10;
            return r;
        }

        void OnGUI()
        {
            if (_selectedId < 0 || Demo == null || Demo.Store == null) return;
            ShowDetailsPanel();
        }

        // strings are made here only while the panel is open, that's ok for a debug panel
        void ShowDetailsPanel()
        {
            if (!Demo.Store.TryGet(_selectedId, out var poi))
            {
                _selectedId = -1; // the POI was removed by the feed
                return;
            }

            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("<b>" + poi.Name + "</b>");
            GUILayout.Label("id " + poi.Id + "   type " + poi.Type + "   priority " + poi.Priority);
            GUILayout.Label("position x " + poi.X.ToString("F0") + "  z " + poi.Z.ToString("F0"));
            GUILayout.Label("icon " + poi.IconKey);
            if (GUILayout.Button("Close")) HideDetailsPanel();
            GUILayout.EndArea();
        }

        public void HideDetailsPanel() => _selectedId = -1;
    }
}
