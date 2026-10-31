using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // Draws a small colored square on the screen edge for each important off screen POI.
    // The finder does the work, this only draws. No text, so it doesn't allocate.
    public class EdgeIndicatorUi : MonoBehaviour
    {
        public PoiDemo Demo;
        public Camera Cam;
        public bool ShowArrows = true;

        [Header("Tweak")]
        public int MinPriority = 4;          // which POIs count as important
        public float SearchRange = 1000f;    // meters around the player
        public int MaxArrows = 8;
        public float ArrowSize = 18f;        // pixels
        public Color ArrowColor = new Color(1f, 0.6f, 0.1f);

        EdgeIndicatorFinder _finder;

        void Start()
        {
            _finder = new EdgeIndicatorFinder(Demo.Store, MaxArrows);
            _finder.IsTypeEnabled = Demo.Selector.IsTypeEnabled; // made once, no alloc per frame
        }

        // runs after PoiDemo (order 100) so the camera is already in place
        void LateUpdate()
        {
            if (!ShowArrows || _finder == null || !Demo.Ready) return;
            _finder.MinPriority = MinPriority;
            _finder.SearchRange = SearchRange;
            var p = Demo.Player.position;
            _finder.FindOffScreenPois(p.x, p.z, Demo.Camera.GetVisibleGroundRect());
        }

        void OnGUI()
        {
            if (!ShowArrows || _finder == null || Event.current.type != EventType.Repaint) return;
            var oldColor = GUI.color;
            GUI.color = ArrowColor;
            for (int i = 0; i < _finder.Count; i++)
                DrawArrow(_finder.Indicators[i]);
            GUI.color = oldColor;
        }

        // world spot -> screen pixel, then a filled square there
        void DrawArrow(in EdgeIndicator indicator)
        {
            var screen = Cam.WorldToScreenPoint(new Vector3(indicator.EdgeX, 0f, indicator.EdgeZ));
            float x = screen.x - ArrowSize * 0.5f;
            float y = Screen.height - screen.y - ArrowSize * 0.5f; // GUI y goes down
            GUI.DrawTexture(new Rect(x, y, ArrowSize, ArrowSize), Texture2D.whiteTexture);
        }
    }
}
