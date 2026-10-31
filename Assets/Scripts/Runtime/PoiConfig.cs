using UnityEngine;

namespace Poi.Runtime
{
    // all the main numbers in one asset so you can tweak them in the Inspector without touching code.
    // PoiDemo copies these at startup (see PoiDemo.ApplyConfig). if no asset is set, scripts just use their own defaults.
    [CreateAssetMenu(menuName = "POI/Poi Config", fileName = "PoiConfig")]
    public class PoiConfig : ScriptableObject
    {
        [Header("Markers")]
        public int Budget = 250;             // max markers on screen
        public float IconRange = 350f;       // show icon within this ground distance (m)
        public float LabelRange = 80f;       // show icon + label within this distance (m)
        public bool DeclutterLabels = true;  // hide labels that overlap

        [Header("Camera height")]
        public float MinHeight = 50f;
        public float MaxHeight = 400f;

        [Header("Feed simulator")]
        public int InitialCount = 1000;
        public float AddsPerSecond = 50f;
        public float RemovesPerSecond = 50f;
        [Range(0f, 1f)] public float MoveShare = 0.05f;
        public float MoveSpeed = 10f;        // m/s

        [Header("Edge indicators")]
        public int EdgeMinPriority = 4;
        public float EdgeSearchRange = 1000f;
        public int EdgeMaxArrows = 8;

        [Header("Click details")]
        public float ClickRadius = 8f;          // meters
    }
}
