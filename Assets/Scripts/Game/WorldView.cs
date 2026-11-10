using System.Collections.Generic;
using StartupClicker.Core;
using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>
    /// Draws the world: ground tiles, one sprite per building, the placement ghost and the
    /// selection highlight. It only displays what the GameSim says - it never changes game state.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public static readonly Color Teal = new Color(84 / 255f, 226 / 255f, 206 / 255f);
        public static readonly Color Red = new Color(238 / 255f, 66 / 255f, 82 / 255f);
        public static readonly Color Gold = new Color(1f, 198 / 255f, 84 / 255f);
        public static readonly Color Cream = new Color(236 / 255f, 232 / 255f, 225 / 255f);

        const int GroundOrder = -30000;
        const int HighlightOrder = -100;
        const int GhostOrder = 25000;

        sealed class View
        {
            public PlacedBuilding Data;
            public BuildingDef Def;
            public SpriteRenderer Renderer;
            public PunchScale Punch;
            public int ShownLevel;
        }

        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<SpriteRenderer> ghostTiles = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> selectTiles = new List<SpriteRenderer>();
        SpriteRenderer ghostSprite;
        GameSim sim;
        int selectedUid;
        int hiddenUid;

        public void Init(GameSim gameSim)
        {
            sim = gameSim;
            BuildGround();

            ghostSprite = NewRenderer("Ghost", transform, GhostOrder);
            ghostSprite.gameObject.SetActive(false);
            for (int i = 0; i < Catalog.CoreSize * Catalog.CoreSize; i++)
            {
                ghostTiles.Add(NewRenderer("GhostTile", transform, HighlightOrder + 1));
                ghostTiles[i].sprite = SpriteBank.Centered("tile_fill");
                ghostTiles[i].gameObject.SetActive(false);
                selectTiles.Add(NewRenderer("SelectTile", transform, HighlightOrder));
                selectTiles[i].sprite = SpriteBank.Centered("tile_outline");
                selectTiles[i].gameObject.SetActive(false);
            }
            foreach (var b in sim.State.Buildings) Add(b, false);
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        void BuildGround()
        {
            var ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            int n = Catalog.GridSize;
            var a = SpriteBank.Centered("tile_a");
            var b = SpriteBank.Centered("tile_b");
            var c = SpriteBank.Centered("tile_c");
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var sr = NewRenderer("Tile", ground, GroundOrder);
                    // checkerboard, with an occasional marked tile for variety
                    sr.sprite = (x * 7 + y * 13) % 11 == 0 ? c : ((x + y) % 2 == 0 ? a : b);
                    sr.transform.position = IsoGrid.ToWorld(x + 0.5f, y + 0.5f);
                }
            }
            // cliff faces along the two front edges so the map reads as a floating slab
            var left = SpriteBank.Get("edge_l", new Vector2(0f, 1f));
            var right = SpriteBank.Get("edge_r", new Vector2(1f, 1f));
            for (int i = 0; i < n; i++)
            {
                var l = NewRenderer("EdgeL", ground, GroundOrder - 1);
                l.sprite = left;
                l.transform.position = IsoGrid.ToWorld(i, n);
                var r = NewRenderer("EdgeR", ground, GroundOrder - 1);
                r.sprite = right;
                r.transform.position = IsoGrid.ToWorld(n, i);
            }
        }

        // ------------------------------------------------------------ buildings

        public void Add(PlacedBuilding b, bool animate)
        {
            var def = Catalog.Building(b.DefId);
            var sr = NewRenderer(def.Name, transform, 0);
            var view = new View { Data = b, Def = def, Renderer = sr, Punch = sr.gameObject.AddComponent<PunchScale>() };
            view.Punch.SetBase(Vector3.one);
            views[b.Uid] = view;
            Refresh(b.Uid);
            if (animate) DropIn(view);
        }

        public void Remove(int uid)
        {
            if (!views.TryGetValue(uid, out var v)) return;
            views.Remove(uid);
            Destroy(v.Renderer.gameObject);
            if (selectedUid == uid) Select(0);
        }

        /// <summary>Re-read position and level from the sim and update the sprite.</summary>
        public void Refresh(int uid)
        {
            if (!views.TryGetValue(uid, out var v)) return;
            var b = v.Data;
            v.Renderer.sprite = SpriteBank.Building(b.DefId, b.Level);
            v.Renderer.transform.position = IsoGrid.FootprintFront(b.X, b.Y, v.Def.Size);
            v.Renderer.sortingOrder = IsoGrid.Order(b.X + v.Def.Size * 0.5f, b.Y + v.Def.Size * 0.5f);
            v.ShownLevel = b.Level;
            if (uid == selectedUid) Select(uid);
        }

        public void Punch(int uid, float strength)
        {
            if (views.TryGetValue(uid, out var v)) v.Punch.Punch(strength);
        }

        /// <summary>World position roughly at the top of a building's sprite (for floating text).</summary>
        public Vector3 TopOf(int uid)
        {
            if (!views.TryGetValue(uid, out var v) || v.Renderer.sprite == null) return Vector3.zero;
            var bounds = v.Renderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.15f, 0f);
        }

        public Vector3 CentreOf(int uid)
        {
            if (!views.TryGetValue(uid, out var v)) return Vector3.zero;
            return v.Renderer.bounds.center;
        }

        void DropIn(View v)
        {
            // new buildings fall a short distance and land with a squash
            var tr = v.Renderer.transform;
            Vector3 end = tr.position;
            var sr = v.Renderer;
            Tween.Run(0.18f, t =>
            {
                if (tr == null) return;
                tr.position = end + new Vector3(0f, (1f - t) * 0.9f, 0f);
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 2f));
            }, () =>
            {
                if (tr == null) return;
                tr.position = end;
                sr.color = Color.white;
                v.Punch.Punch(-0.9f);
            }, x => x * x);
        }

        // ------------------------------------------------------------ picking

        /// <summary>Which building is under this world point? Returns its uid, or 0.</summary>
        public int Pick(Vector3 world)
        {
            // 1) Test the visible pixels of every building sprite and keep the front-most hit.
            //    (Bounding rectangles are not enough: the big HQ sprite is mostly empty space and
            //    its rectangle would swallow clicks meant for buildings tucked beside it.)
            View best = null;
            foreach (var v in views.Values)
            {
                if (v.Data.Uid == hiddenUid || v.Renderer.sprite == null) continue;
                if (best != null && v.Renderer.sortingOrder <= best.Renderer.sortingOrder) continue;
                if (!v.Renderer.bounds.Contains(new Vector3(world.x, world.y, v.Renderer.bounds.center.z))) continue;
                if (HitsVisiblePixel(v.Renderer, world)) best = v;
            }
            if (best != null) return best.Data.Uid;

            // 2) Otherwise whatever owns the ground tile under the cursor.
            var g = IsoGrid.ToGrid(world);
            int uid = sim.Grid.At(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y));
            return uid == hiddenUid ? 0 : uid;
        }

        /// <summary>
        /// Converts the world point into a pixel of the sprite's texture and checks it is solid.
        /// InverseTransformPoint undoes position and the squash-and-stretch scale, giving a point
        /// in sprite units measured from the pivot.
        /// </summary>
        static bool HitsVisiblePixel(SpriteRenderer renderer, Vector3 world)
        {
            var sprite = renderer.sprite;
            var mask = SpriteBank.Mask(sprite.name);
            if (mask == null) return true; // no mask available: fall back to the rectangle test
            Vector3 local = renderer.transform.InverseTransformPoint(world);
            float px = local.x * sprite.pixelsPerUnit + sprite.pivot.x;
            float py = local.y * sprite.pixelsPerUnit + sprite.pivot.y;
            // > 50% opaque: ignores the soft ground shadow and faint glow halos
            return mask.Sample(px / sprite.rect.width, py / sprite.rect.height) > 128;
        }

        // ------------------------------------------------------------ selection + ghost

        public void Select(int uid)
        {
            selectedUid = uid;
            View v = null;
            bool show = uid != 0 && views.TryGetValue(uid, out v);
            int size = show ? v.Def.Size : 0;
            for (int i = 0; i < selectTiles.Count; i++)
            {
                bool on = show && i < size * size;
                selectTiles[i].gameObject.SetActive(on);
                if (on) selectTiles[i].transform.position = IsoGrid.ToWorld(v.Data.X + i % size + 0.5f, v.Data.Y + i / size + 0.5f);
            }
        }

        /// <summary>Show a translucent building following the cursor, with its footprint tinted teal (ok) or red (blocked).</summary>
        public void ShowGhost(string defId, int level, int x, int y, bool valid, int hideUid = 0)
        {
            var def = Catalog.Building(defId);
            SetHidden(hideUid);
            ghostSprite.gameObject.SetActive(true);
            ghostSprite.sprite = SpriteBank.Building(defId, level);
            ghostSprite.transform.position = IsoGrid.FootprintFront(x, y, def.Size) + new Vector3(0f, 0.08f, 0f);
            ghostSprite.color = valid ? new Color(1f, 1f, 1f, 0.82f) : new Color(1f, 0.45f, 0.5f, 0.6f);
            Color tint = valid ? Teal : Red;
            tint.a = 0.55f;
            for (int i = 0; i < ghostTiles.Count; i++)
            {
                bool on = i < def.Size * def.Size;
                ghostTiles[i].gameObject.SetActive(on);
                if (!on) continue;
                ghostTiles[i].transform.position = IsoGrid.ToWorld(x + i % def.Size + 0.5f, y + i / def.Size + 0.5f);
                ghostTiles[i].color = tint;
            }
        }

        public void HideGhost()
        {
            ghostSprite.gameObject.SetActive(false);
            foreach (var t in ghostTiles) t.gameObject.SetActive(false);
            SetHidden(0);
        }

        void SetHidden(int uid)
        {
            if (hiddenUid == uid) return;
            if (views.TryGetValue(hiddenUid, out var old)) old.Renderer.enabled = true;
            hiddenUid = uid;
            if (views.TryGetValue(uid, out var now)) now.Renderer.enabled = false;
        }

        void Update()
        {
            // pulse the selection outline
            float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            var c = Cream;
            c.a = a;
            foreach (var t in selectTiles) if (t.gameObject.activeSelf) t.color = c;
        }
    }
}
