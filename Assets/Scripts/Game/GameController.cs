using System;
using System.Collections.Generic;
using StartupClicker.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StartupClicker.Game
{
    public enum InputMode { Idle, Placing, Moving }

    /// <summary>
    /// The conductor. It owns the GameSim (rules), reads mouse/keyboard input, tells the WorldView
    /// and Hud what to show, and handles saving. Flow each frame:
    ///   input -> ask the sim to do something -> if it worked, play feedback and refresh views.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const string SaveKey = "startupclicker.save.v1";
        const float AutosaveSeconds = 15f;
        const float DragThresholdPixels = 8f;

        public static GameController Instance { get; private set; }

        public GameSim Sim { get; private set; }
        public WorldView World { get; private set; }
        public CameraController Cam { get; private set; }
        public Hud Hud { get; private set; }
        public InputMode Mode { get; private set; }
        public string PlacingId { get; private set; }

        /// <summary>Fired when buildings, upgrades or the core level change (the shop needs rebuilding).</summary>
        public event Action Changed;
        /// <summary>Fired when the selected building changes.</summary>
        public event Action SelectionChanged;

        int selectedUid;
        int movingUid;
        int ghostX, ghostY;
        bool ghostValid;

        // Mouse press tracking, to tell a click from a drag. Left = primary, right = secondary.
        bool pressActive, pressOnCore, dragging;
        bool rightActive, rightDragging;
        Vector2 pressPos, rightPressPos, lastMouse;

        float autosaveTimer, dropTimer;
        GameObject dropObject;
        float dropLife;
        /// <summary>For each building, the last whole second (on its own staggered clock) it showed an income pop.</summary>
        readonly Dictionary<int, long> lastPopSecond = new Dictionary<int, long>();
        const int MaxPopsPerFrame = 12;

        public PlacedBuilding Selected { get { return Sim.Find(selectedUid); } }

        // ------------------------------------------------------------ setup

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;

            GameState loaded = Load();
            Sim = new GameSim(loaded);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            Cam = cam.gameObject.AddComponent<CameraController>();
            Cam.Init(cam);

            World = new GameObject("World").AddComponent<WorldView>();
            World.Init(Sim);

            Hud = new GameObject("HUD").AddComponent<Hud>();
            Hud.Init(this);

            if (loaded != null && loaded.LastSavedUnix > 0)
            {
                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - loaded.LastSavedUnix;
                double gained = Sim.ApplyOffline(away);
                if (gained >= 1) Hud.Toast("WELCOME BACK  //  +" + NumberFormat.Format(gained) + " EARNED WHILE AWAY", 5f);
            }
            dropTimer = UnityEngine.Random.Range(35f, 60f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------ main loop

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Sim.Tick(Math.Min(dt, 3600f));

            HandleInput();
            UpdateGhost();
            UpdateDrop(dt);
            UpdateIncomePops();

            autosaveTimer += dt;
            if (autosaveTimer >= AutosaveSeconds) Save();
        }

        void HandleInput()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            Vector2 delta = pos - lastMouse;
            lastMouse = pos;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) CancelOrDeselect();
                Vector2 move = Vector2.zero;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1;
                if (move != Vector2.zero)
                    Cam.PanByWorld(move.normalized * Cam.Camera.orthographicSize * 1.4f * Time.unscaledDeltaTime);
            }

            if (!overUi)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Cam.Zoom(Mathf.Sign(scroll));
            }

            // Middle button always pans.
            if (mouse.middleButton.isPressed && !pressActive && !rightActive) Cam.PanByScreenDelta(delta);

            // Right button: a short click selects a building (or cancels), a drag pans.
            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                rightActive = true;
                rightDragging = false;
                rightPressPos = pos;
            }
            if (rightActive && mouse.rightButton.isPressed)
            {
                if (!rightDragging && (pos - rightPressPos).magnitude > DragThresholdPixels) rightDragging = true;
                if (rightDragging) Cam.PanByScreenDelta(delta);
            }
            if (rightActive && mouse.rightButton.wasReleasedThisFrame)
            {
                rightActive = false;
                if (!rightDragging) OnSecondaryClick(Cam.ScreenToWorld(pos));
            }

            // Left button: clicks the HQ / supply drop / confirms placement; a drag pans.
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                pressActive = true;
                dragging = false;
                pressPos = pos;
                Vector3 world = Cam.ScreenToWorld(pos);

                if (Mode == InputMode.Idle && TryCollectDrop(world))
                {
                    pressActive = false;
                }
                else
                {
                    // The core reacts on press (not release) so rapid clicking feels instant.
                    pressOnCore = Mode == InputMode.Idle && World.Pick(world) == Sim.CoreBuilding.Uid;
                    if (pressOnCore) ClickCore(pos, world);
                }
            }

            if (pressActive && mouse.leftButton.isPressed && !pressOnCore)
            {
                if (!dragging && (pos - pressPos).magnitude > DragThresholdPixels) dragging = true;
                if (dragging) Cam.PanByScreenDelta(delta);
            }

            if (pressActive && mouse.leftButton.wasReleasedThisFrame)
            {
                pressActive = false;
                if (!dragging && !pressOnCore) OnPrimaryClick(Cam.ScreenToWorld(pos));
            }
        }

        /// <summary>Left click on the world (not the HQ, which is handled on press).</summary>
        void OnPrimaryClick(Vector3 world)
        {
            if (Mode == InputMode.Placing) { ConfirmPlacement(); return; }
            if (Mode == InputMode.Moving) { ConfirmMove(); return; }

            int uid = World.Pick(world);
            if (uid != 0) OnBuildingPrimary(uid);
            else Select(0);
        }

        /// <summary>
        /// Left click on a building other than the HQ. For now it just bounces.
        /// This is the hook for future building abilities (e.g. a Drone Bay launching a bonus run).
        /// </summary>
        void OnBuildingPrimary(int uid)
        {
            World.Punch(uid, 0.3f);
        }

        /// <summary>Right click: cancels placing/moving, otherwise selects the building under the cursor (HQ included).</summary>
        void OnSecondaryClick(Vector3 world)
        {
            if (Mode != InputMode.Idle) { EndMode(); return; }
            int uid = World.Pick(world);
            Select(uid);
            if (uid != 0)
            {
                World.Punch(uid, 0.35f);
                Sfx.Play("select", 0.4f);
            }
        }

        // ------------------------------------------------------------ clicking the core

        void ClickCore(Vector2 screen, Vector3 world)
        {
            double gained = Sim.Click();
            World.Punch(Sim.CoreBuilding.Uid, 0.5f);
            Hud.FloatText(screen + new Vector2(UnityEngine.Random.Range(-24f, 24f), 30f), "+" + NumberFormat.Format(gained, 2),
                Sim.FrenzyActive ? WorldView.Gold : WorldView.Cream, 38);
            Hud.BumpCredits();
            ShardBurst.Spawn(world, Sim.FrenzyActive ? WorldView.Gold : WorldView.Red, 5, 0.9f);
            Sfx.Play("click", 0.5f, 1f, 0.12f);
        }

        // ------------------------------------------------------------ placing / moving

        public void BeginPlace(string defId)
        {
            var def = Catalog.Building(defId);
            if (def == null || !Sim.IsUnlocked(def)) return;
            if (!Sim.CanAfford(Sim.CostOf(def)))
            {
                Deny("NOT ENOUGH CREDITS");
                return;
            }
            Select(0);
            Mode = InputMode.Placing;
            PlacingId = defId;
            Sfx.Play("select", 0.4f);
            Hud.SetHint("CLICK A TILE TO BUILD  //  HOLD SHIFT TO KEEP BUILDING  //  RIGHT-CLICK OR ESC TO CANCEL");
        }

        public void BeginMoveSelected()
        {
            var b = Selected;
            if (b == null) return;
            movingUid = b.Uid;
            Mode = InputMode.Moving;
            Sfx.Play("select", 0.4f);
            Hud.SetHint("CLICK A TILE TO MOVE HERE  //  RIGHT-CLICK OR ESC TO CANCEL");
        }

        void UpdateGhost()
        {
            if (Mode == InputMode.Idle || Mouse.current == null) return;
            if (Mode == InputMode.Moving && Sim.Find(movingUid) == null) { EndMode(); return; }
            string defId = Mode == InputMode.Placing ? PlacingId : Sim.Find(movingUid).DefId;
            var def = Catalog.Building(defId);
            Vector2 g = IsoGrid.ToGrid(Cam.ScreenToWorld(Mouse.current.position.ReadValue()));

            // centre the footprint on the cursor and keep it on the map
            int max = Catalog.GridSize - def.Size;
            ghostX = Mathf.Clamp(Mathf.FloorToInt(g.x - (def.Size - 1) * 0.5f), 0, max);
            ghostY = Mathf.Clamp(Mathf.FloorToInt(g.y - (def.Size - 1) * 0.5f), 0, max);

            if (Mode == InputMode.Placing)
            {
                ghostValid = Sim.Grid.CanPlace(ghostX, ghostY, def.Size);
                World.ShowGhost(defId, 1, ghostX, ghostY, ghostValid);
            }
            else
            {
                ghostValid = Sim.Grid.CanPlace(ghostX, ghostY, def.Size, movingUid);
                World.ShowGhost(defId, Sim.Find(movingUid).Level, ghostX, ghostY, ghostValid, movingUid);
            }
        }

        void ConfirmPlacement()
        {
            if (!ghostValid) { Deny("SPACE IS BLOCKED"); return; }
            var b = Sim.TryBuy(PlacingId, ghostX, ghostY);
            if (b == null) { Deny("NOT ENOUGH CREDITS"); EndMode(); return; }

            World.Add(b, true);
            var def = Catalog.Building(b.DefId);
            ShardBurst.Spawn(IsoGrid.FootprintCentre(b.X, b.Y, def.Size), WorldView.Teal, 6 + def.Size * 3, 0.8f + def.Size * 0.2f);
            Sfx.Play("place", 0.7f);
            RaiseChanged();

            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (!shift || !Sim.CanAfford(Sim.CostOf(def))) EndMode();
        }

        void ConfirmMove()
        {
            if (!ghostValid) { Deny("SPACE IS BLOCKED"); return; }
            int uid = movingUid;
            if (Sim.TryMove(uid, ghostX, ghostY))
            {
                EndMode();
                World.Refresh(uid);
                World.Punch(uid, -0.7f);
                Sfx.Play("place", 0.7f);
                Select(uid);
            }
        }

        void EndMode()
        {
            Mode = InputMode.Idle;
            PlacingId = null;
            movingUid = 0;
            World.HideGhost();
            Hud.SetHint(null);
        }

        void CancelOrDeselect()
        {
            if (Mode != InputMode.Idle) EndMode();
            else Select(0);
        }

        // ------------------------------------------------------------ actions called by the HUD

        public void Select(int uid)
        {
            if (uid != 0 && Sim.Find(uid) == null) uid = 0;
            selectedUid = uid;
            World.Select(uid);
            if (SelectionChanged != null) SelectionChanged();
        }

        public void UpgradeSelected()
        {
            var b = Selected;
            if (b != null) Upgrade(b);
        }

        public void UpgradeCore()
        {
            Upgrade(Sim.CoreBuilding);
        }

        void Upgrade(PlacedBuilding b)
        {
            if (Sim.UpgradeCostOf(b) < 0) return;
            if (!Sim.TryUpgrade(b.Uid)) { Deny("NOT ENOUGH CREDITS"); return; }
            World.Refresh(b.Uid);
            World.Punch(b.Uid, 0.9f);
            ShardBurst.Spawn(World.CentreOf(b.Uid), WorldView.Gold, 14, 1.3f);
            Sfx.Play("upgrade", 0.6f, 1f, 0f);
            if (b.DefId == Catalog.CoreId) Hud.Toast("COMMAND CORE  //  LEVEL " + b.Level, 3f);
            RaiseChanged();
        }

        public void SellSelected()
        {
            var b = Selected;
            if (b == null) return;
            if (Mode != InputMode.Idle) EndMode();
            Vector3 at = World.CentreOf(b.Uid);
            double refund = Sim.TrySell(b.Uid);
            if (refund < 0) return;
            World.Remove(b.Uid);
            lastPopSecond.Remove(b.Uid);
            Select(0);
            ShardBurst.Spawn(at, WorldView.Cream, 10, 1f);
            Hud.FloatText(Cam.WorldToScreen(at), "+" + NumberFormat.Format(refund), WorldView.Gold, 34);
            Sfx.Play("sell", 0.5f);
            RaiseChanged();
        }

        public void BuyUpgrade(string id)
        {
            if (!Sim.TryBuyUpgrade(id)) { Deny("NOT ENOUGH CREDITS"); return; }
            Sfx.Play("buy", 0.6f);
            Hud.Toast(Catalog.Upgrade(id).Name.ToUpperInvariant() + "  //  ACQUIRED", 2.5f);
            RaiseChanged();
        }

        public void ResetGame()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            // Stop this session from re-saving, then reload the scene; Bootstrap builds a fresh game.
            skipSave = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void Deny(string message)
        {
            Sfx.Play("error", 0.45f);
            Hud.Toast(message, 1.6f, true);
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
            if (SelectionChanged != null) SelectionChanged();
        }

        // ------------------------------------------------------------ supply drops (the "golden cookie")

        void UpdateDrop(float dt)
        {
            if (dropObject == null)
            {
                dropTimer -= dt;
                if (dropTimer <= 0f) SpawnDrop();
                return;
            }
            dropLife -= dt;
            // bob up and down, and blink when about to expire
            var sr = dropObject.GetComponent<SpriteRenderer>();
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
            dropObject.transform.localScale = Vector3.one * (1f + bob);
            sr.color = new Color(1f, 1f, 1f, dropLife < 4f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f) : 1f);
            if (dropLife <= 0f)
            {
                Destroy(dropObject);
                dropTimer = UnityEngine.Random.Range(50f, 100f);
            }
        }

        void SpawnDrop()
        {
            // try a few random empty tiles not too far from the core
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int x = UnityEngine.Random.Range(5, Catalog.GridSize - 5);
                int y = UnityEngine.Random.Range(5, Catalog.GridSize - 5);
                if (!Sim.Grid.CanPlace(x, y, 1)) continue;

                dropObject = new GameObject("SupplyDrop");
                var sr = dropObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteBank.Grounded("drop");
                dropObject.transform.position = IsoGrid.FootprintFront(x, y, 1);
                sr.sortingOrder = IsoGrid.Order(x + 0.5f, y + 0.5f);
                dropLife = 22f;
                Hud.Toast("SUPPLY DROP INBOUND  //  CLICK IT", 3f);
                Sfx.Play("drop", 0.35f, 0.8f, 0f);
                return;
            }
            dropTimer = 10f;
        }

        bool TryCollectDrop(Vector3 world)
        {
            if (dropObject == null) return false;
            var bounds = dropObject.GetComponent<SpriteRenderer>().bounds;
            bounds.Expand(new Vector3(0.2f, 0.2f, 10f));
            if (!bounds.Contains(world)) return false;

            Vector3 at = bounds.center;
            var result = Sim.CollectDrop(UnityEngine.Random.value);
            Destroy(dropObject);
            dropObject = null;
            dropTimer = UnityEngine.Random.Range(50f, 100f);

            ShardBurst.Spawn(at, WorldView.Gold, 18, 1.5f);
            Sfx.Play("drop", 0.6f, 1f, 0f);
            if (result.Kind == DropKind.Frenzy)
            {
                Hud.Toast("FRENZY  //  x" + Catalog.FrenzyMult + " INCOME FOR " + Catalog.FrenzySeconds + " SECONDS", 4f);
            }
            else
            {
                Hud.FloatText(Cam.WorldToScreen(at), "+" + NumberFormat.Format(result.Credits), WorldView.Gold, 52);
                Hud.BumpCredits();
            }
            return true;
        }

        // ------------------------------------------------------------ ambient income numbers

        /// <summary>
        /// Every building shows "+N" once per second, where N is exactly what it earns per second.
        /// Each building runs on its own clock offset (golden-ratio spacing by uid) so the pops are
        /// spread out instead of all firing on the same frame.
        /// </summary>
        void UpdateIncomePops()
        {
            int budget = MaxPopsPerFrame;
            float now = Time.unscaledTime;
            foreach (var b in Sim.State.Buildings)
            {
                if (b.DefId == Catalog.CoreId) continue;
                float phase = (b.Uid * 0.618034f) % 1f;
                long second = (long)Math.Floor(now + phase);
                if (!lastPopSecond.TryGetValue(b.Uid, out var last))
                {
                    lastPopSecond[b.Uid] = second; // first sighting: start the clock, pop next second
                    continue;
                }
                if (second == last) continue;
                lastPopSecond[b.Uid] = second;

                if (Mode == InputMode.Moving && b.Uid == movingUid) continue;
                if (budget <= 0) continue;
                Vector3 top = World.TopOf(b.Uid);
                Vector3 vp = Cam.Camera.WorldToViewportPoint(top);
                if (vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) continue; // off-screen
                budget--;

                double perSecond = Sim.CpsOf(b) * Sim.FrenzyFactor;
                Hud.FloatText(Cam.WorldToScreen(top), "+" + NumberFormat.Format(perSecond, 2),
                    Sim.FrenzyActive ? WorldView.Gold : new Color(1f, 1f, 1f, 0.8f), 22, 0.9f);
                World.Punch(b.Uid, 0.12f);
            }
        }

        // ------------------------------------------------------------ saving

        bool skipSave;

        static GameState Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<GameState>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save could not be read, starting fresh: " + e.Message);
                return null;
            }
        }

        public void Save()
        {
            if (skipSave || Sim == null) return;
            autosaveTimer = 0f;
            Sim.State.LastSavedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // PlayerPrefs works on every platform including WebGL, where there is no normal file system.
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Sim.State));
            PlayerPrefs.Save();
        }

        void OnApplicationQuit() { Save(); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool focused) { if (!focused) Save(); }
    }
}
