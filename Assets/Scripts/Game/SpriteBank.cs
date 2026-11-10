using System.Collections.Generic;
using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>
    /// Loads the generated PNGs from Resources/Art and turns them into Sprites in code.
    /// Doing it here (instead of in each texture's import settings) keeps pivots and
    /// pixels-per-unit in one place and means the art can be regenerated freely.
    /// </summary>
    public static class SpriteBank
    {
        /// <summary>Must match PAD_B in Tools/art/iso.py: empty pixels below a building's front corner.</summary>
        const float BuildingBottomPad = 16f;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static Font font, fontBold;

        public static Sprite Get(string name, Vector2 pivot, Vector4 border = default(Vector4), float pixelsPerUnit = IsoGrid.PixelsPerUnit)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("Art/" + name);
            Sprite sprite = null;
            if (tex == null)
            {
                Debug.LogWarning("Missing art: " + name + " (run Tools/art/generate_art.py)");
            }
            else
            {
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit,
                    0, SpriteMeshType.FullRect, border);
                sprite.name = name;
            }
            Cache[name] = sprite;
            return sprite;
        }

        public static Sprite Centered(string name)
        {
            return Get(name, new Vector2(0.5f, 0.5f));
        }

        /// <summary>A stretchable UI sprite: the border (in pixels) stays crisp while the middle stretches.</summary>
        public static Sprite Sliced(string name, float border)
        {
            // 100 pixels per unit matches the UI canvas, so a 16px border is 16 canvas units wide
            return Get(name, new Vector2(0.5f, 0.5f), new Vector4(border, border, border, border), 100f);
        }

        /// <summary>Building sprite, pivoted on the front corner of its footprint.</summary>
        public static Sprite Building(string defId, int level)
        {
            return Grounded("b_" + defId + "_" + level);
        }

        /// <summary>Any sprite drawn by the iso kit: pivot is bottom-centre, just above the padding.</summary>
        public static Sprite Grounded(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("Art/" + name);
            float pivotY = tex != null ? BuildingBottomPad / tex.height : 0f;
            return Get(name, new Vector2(0.5f, pivotY));
        }

        public static AudioClip Clip(string name)
        {
            if (!Clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                Clips[name] = clip;
            }
            return clip;
        }

        public static Font Font(bool bold)
        {
            if (font == null)
            {
                font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");
                fontBold = Resources.Load<Font>("Fonts/BarlowCondensed-Bold");
                var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = fallback;
                if (fontBold == null) fontBold = font;
            }
            return bold ? fontBold : font;
        }
    }
}
