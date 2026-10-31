namespace Poi.Core
{
    // Hides labels that would sit on top of each other.
    // The selector gives us markers best first, so we just go down the list
    // and keep a label only if it doesn't touch a label we already kept.
    // Only markers with a label are checked (close ones), so this stays small.
    public class LabelDeclutter
    {
        // label box size on the ground, in meters. change these if the label font changes
        public float LetterWidth = 1.7f;
        public float LabelHeight = 3.5f;
        // how far below the icon center the label box sits
        public float LabelOffsetZ = 9f;

        readonly PoiStore _store;
        readonly GroundRect[] _keptBoxes;

        public int LastHiddenCount { get; private set; }

        // maxLabels = most labels kept per frame, extra ones are hidden
        public LabelDeclutter(PoiStore store, int maxLabels = 128)
        {
            _store = store;
            _keptBoxes = new GroundRect[maxLabels];
        }

        // goes over the selected markers and turns overlapping labels into icon only
        public void HideOverlappingLabels(SelectedMarker[] markers, int count)
        {
            int keptCount = 0;
            LastHiddenCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (markers[i].Lod != MarkerLod.IconAndLabel) continue;
                if (!_store.TryGet(markers[i].Id, out var poi)) continue;

                var box = GetLabelBox(poi);
                bool tooManyLabels = keptCount >= _keptBoxes.Length;

                if (tooManyLabels || TouchesAnyKeptBox(box, keptCount))
                {
                    markers[i].Lod = MarkerLod.Icon; // icon stays, label goes
                    LastHiddenCount++;
                }
                else
                {
                    _keptBoxes[keptCount] = box;
                    keptCount++;
                }
            }
        }

        // the rectangle the label text covers on the ground
        public GroundRect GetLabelBox(in PoiData poi)
        {
            int letters = poi.Name == null ? 0 : poi.Name.Length;
            float halfWidth = letters * LetterWidth * 0.5f;
            float top = poi.Z - LabelOffsetZ;
            return new GroundRect(poi.X - halfWidth, top - LabelHeight, poi.X + halfWidth, top);
        }

        bool TouchesAnyKeptBox(in GroundRect box, int keptCount)
        {
            for (int k = 0; k < keptCount; k++)
            {
                if (BoxesOverlap(box, _keptBoxes[k])) return true;
            }
            return false;
        }

        public static bool BoxesOverlap(in GroundRect a, in GroundRect b)
        {
            return a.MinX < b.MaxX && a.MaxX > b.MinX && a.MinZ < b.MaxZ && a.MaxZ > b.MinZ;
        }
    }
}
