using UnityEngine;

namespace TcgEngine.Client
{
    public enum SlotVisualType
    {
        Battlefield = 0,
        Talent = 1,
        Market = 2
    }

    /// <summary>
    /// Controls the visual appearance of a BoardSlot (border color, type indicator)
    /// Borders are always visible; set Highlighted during placement for valid slots.
    /// </summary>
    public class SlotVisual : MonoBehaviour
    {
        public SlotVisualType slotType = SlotVisualType.Battlefield;
        public int talentLevel;

        [Header("Border Colors")]
        public Color battlefieldColor = new Color(0.25f, 0.25f, 0.25f, 0.3f);
        public Color talentColor = new Color(0.18f, 0.8f, 0.35f, 0.55f);
        public Color marketColor = new Color(0.6f, 0.25f, 0.75f, 0.55f);
        public Color highlightColor = new Color(0.2f, 0.9f, 1f, 0.85f);

        [Header("Highlight")]
        public float highlightScale = 1.15f;

        private SpriteRenderer borderRenderer;
        private Color normalColor;
        private Vector3 normalScale;
        private bool highlighted;

        void Awake()
        {
            CreateBorder();
        }

        void Start()
        {
            ApplyVisual();
        }

        private void CreateBorder()
        {
            GameObject borderObj = new GameObject("SlotBorder");
            borderObj.transform.SetParent(transform);
            borderObj.transform.localPosition = Vector3.zero;
            borderObj.transform.localRotation = Quaternion.identity;
            borderObj.transform.localScale = Vector3.one;

            borderRenderer = borderObj.AddComponent<SpriteRenderer>();
            borderRenderer.sprite = CreateBorderSprite();
            borderRenderer.sortingOrder = -1;
            borderRenderer.drawMode = SpriteDrawMode.Sliced;
            borderRenderer.size = new Vector2(4.0f, 4.0f);
        }

        private Sprite CreateBorderSprite()
        {
            int size = 32;
            int border = 1;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                    pixels[y * size + x] = isBorder ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = "SlotBorderSprite";
            return sprite;
        }

        public void ApplyVisual()
        {
            if (borderRenderer == null) return;

            switch (slotType)
            {
                case SlotVisualType.Talent:
                    normalColor = talentColor;
                    break;
                case SlotVisualType.Market:
                    normalColor = marketColor;
                    break;
                default:
                    normalColor = battlefieldColor;
                    break;
            }

            borderRenderer.color = normalColor;
            borderRenderer.enabled = true;
            normalScale = borderRenderer.transform.localScale;
        }

        public void SetHighlighted(bool highlight)
        {
            if (borderRenderer == null) return;

            highlighted = highlight;
            if (highlight)
            {
                borderRenderer.color = highlightColor;
                borderRenderer.transform.localScale = normalScale * highlightScale;
            }
            else
            {
                borderRenderer.color = normalColor;
                borderRenderer.transform.localScale = normalScale;
            }
        }

        public bool IsHighlighted()
        {
            return highlighted;
        }

        public void SetVisualType(SlotVisualType type, int level = 0)
        {
            slotType = type;
            talentLevel = level;
            ApplyVisual();
        }
    }
}
