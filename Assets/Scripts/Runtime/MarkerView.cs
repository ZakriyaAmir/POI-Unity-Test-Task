using Poi.Core;
using UnityEngine;

namespace Poi.Runtime
{
    // The marker prefab script: an icon sprite and a TextMesh label, lying flat for the top-down camera.
    // _version goes up every time the marker is bound, unbound or its icon key changes.
    // An icon load that finishes with an old version is ignored, so a recycled marker never shows a stale icon.
    public class MarkerView : MonoBehaviour, IMarkerView, IIconReceiver
    {
        [SerializeField] SpriteRenderer _icon;
        [SerializeField] TextMesh _label;
        [SerializeField] MeshRenderer _labelRenderer;

        IIconLoader _loader;
        int _version;
        string _iconKey;
        string _name;

        public int PoiId { get; private set; } = -1;
        public int Version => _version;
        public Sprite CurrentIcon => _icon.sprite;
        public string CurrentLabel => _label.text;
        public bool IconVisible => _icon.enabled;
        public bool LabelVisible => _labelRenderer.enabled;

        // used by the prefab builder and by tests
        public void Setup(SpriteRenderer icon, TextMesh label, MeshRenderer labelRenderer)
        {
            _icon = icon;
            _label = label;
            _labelRenderer = labelRenderer;
        }

        public void Init(IIconLoader loader) => _loader = loader;

        public void Bind(in PoiData poi, MarkerLod lod)
        {
            PoiId = poi.Id;
            _version++;
            _iconKey = null;
            _name = null;
            _icon.sprite = null;
            Apply(poi, lod);
            gameObject.SetActive(true);
        }

        public void Apply(in PoiData poi, MarkerLod lod)
        {
            transform.position = new Vector3(poi.X, 0.5f, poi.Z);

            if (_iconKey != poi.IconKey)
            {
                _iconKey = poi.IconKey;
                _version++;
                _icon.sprite = null;
                _loader?.Request(_iconKey, this, _version);
            }

            if (!ReferenceEquals(_name, poi.Name))
            {
                _name = poi.Name;
                _label.text = poi.Name;
            }

            _icon.enabled = lod != MarkerLod.None;
            _labelRenderer.enabled = lod == MarkerLod.IconAndLabel;
        }

        public void Unbind()
        {
            PoiId = -1;
            _version++;
            _iconKey = null;
            _name = null;
            _icon.sprite = null;
            _label.text = string.Empty;
            gameObject.SetActive(false);
        }

        public void OnIconLoaded(int token, Sprite sprite)
        {
            if (token != _version) return; // late answer for something we don't show anymore
            _icon.sprite = sprite;
        }
    }
}
