using System;
using System.Collections.Generic;

namespace StartupClicker.Core
{
    public enum DropKind { Credits, Frenzy }

    public struct DropResult
    {
        public DropKind Kind;
        public double Credits;
    }

    /// <summary>
    /// The rules of the game. It owns a GameState and is the only thing allowed to change it.
    /// It knows nothing about Unity, so it can be unit-tested on its own.
    /// </summary>
    public sealed class GameSim
    {
        public GameState State { get; }
        public GridModel Grid { get; }

        readonly Dictionary<int, PlacedBuilding> byUid = new Dictionary<int, PlacedBuilding>();
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        readonly Dictionary<string, double> buildingMult = new Dictionary<string, double>();
        readonly HashSet<string> owned = new HashSet<string>();
        double clickMult = 1, clickCpsShare, globalMult = 1, baseCps;
        PlacedBuilding core;

        public GameSim(GameState state = null)
        {
            State = Sanitise(state ?? GameState.NewGame());
            Grid = new GridModel(Catalog.GridSize);
            foreach (var b in State.Buildings)
            {
                byUid[b.Uid] = b;
                Grid.Fill(b.X, b.Y, Catalog.Building(b.DefId).Size, b.Uid);
                if (b.DefId == Catalog.CoreId) core = b;
            }
            Recalculate();
        }

        // ---------------------------------------------------------------- queries

        public int CoreLevel => core.Level;
        public PlacedBuilding CoreBuilding => core;
        public bool FrenzyActive => State.FrenzyRemaining > 0;
        public double FrenzyFactor => FrenzyActive ? Catalog.FrenzyMult : 1;

        public PlacedBuilding Find(int uid)
        {
            return byUid.TryGetValue(uid, out var b) ? b : null;
        }

        public int CountOf(string defId)
        {
            return counts.TryGetValue(defId, out var n) ? n : 0;
        }

        public bool HasUpgrade(string id) => owned.Contains(id);

        public bool IsUnlocked(BuildingDef def) => CoreLevel >= def.UnlockCoreLevel;

        /// <summary>Price of the next building of this type. Grows 15% per one already owned.</summary>
        public double CostOf(BuildingDef def)
        {
            return Math.Ceiling(def.BaseCost * Math.Pow(Catalog.CostGrowth, CountOf(def.Id)));
        }

        /// <summary>Each level doubles output: 1, 2, 4, 8, 16.</summary>
        public static double LevelMultiplier(int level) => Math.Pow(2, level - 1);

        /// <summary>Credits/sec of one placed building, before frenzy.</summary>
        public double CpsOf(PlacedBuilding b)
        {
            if (b.DefId == Catalog.CoreId) return 0;
            var def = Catalog.Building(b.DefId);
            double mult = buildingMult.TryGetValue(b.DefId, out var m) ? m : 1;
            return def.BaseCps * LevelMultiplier(b.Level) * mult * globalMult;
        }

        /// <summary>Price to raise this building one level, or -1 if it is already max level.</summary>
        public double UpgradeCostOf(PlacedBuilding b)
        {
            if (b.Level >= Catalog.MaxLevel) return -1;
            if (b.DefId == Catalog.CoreId) return Catalog.CoreUpgradeCosts[b.Level - 1];
            return Math.Ceiling(Catalog.Building(b.DefId).BaseCost * 2.5 * Math.Pow(3, b.Level - 1));
        }

        public double SellValueOf(PlacedBuilding b)
        {
            if (b.DefId == Catalog.CoreId) return 0;
            var def = Catalog.Building(b.DefId);
            // Refund a share of what the most recent one of this type cost.
            double lastPaid = def.BaseCost * Math.Pow(Catalog.CostGrowth, Math.Max(0, CountOf(def.Id) - 1));
            return Math.Floor(lastPaid * Catalog.SellRefund);
        }

        /// <summary>Total passive income per second, including an active frenzy.</summary>
        public double Cps => baseCps * FrenzyFactor;

        /// <summary>What one click on the Command Core is worth right now.</summary>
        public double ClickValue
        {
            get
            {
                double basic = LevelMultiplier(CoreLevel) * clickMult;
                return (basic + baseCps * clickCpsShare) * FrenzyFactor;
            }
        }

        public bool IsUpgradeVisible(UpgradeDef u)
        {
            if (owned.Contains(u.Id)) return false;
            if (CoreLevel < u.RequireCoreLevel) return false;
            if (u.RequireCount > 0 && CountOf(u.TargetId) < u.RequireCount) return false;
            return true;
        }

        public bool CanAfford(double cost) => cost >= 0 && State.Credits >= cost;

        // ---------------------------------------------------------------- actions

        /// <summary>Advance time. Adds passive income and runs down the frenzy timer.</summary>
        public void Tick(double dt)
        {
            if (dt <= 0) return;
            if (State.FrenzyRemaining > 0)
            {
                // Split the step so a frenzy that ends mid-tick only boosts its own part.
                double boosted = Math.Min(dt, State.FrenzyRemaining);
                Earn(baseCps * Catalog.FrenzyMult * boosted);
                State.FrenzyRemaining -= boosted;
                dt -= boosted;
            }
            if (dt > 0) Earn(baseCps * dt);
        }

        public double Click()
        {
            double v = ClickValue;
            State.Clicks++;
            Earn(v);
            return v;
        }

        /// <summary>Buy a new building and put it at (x, y). Returns null if unaffordable, locked or blocked.</summary>
        public PlacedBuilding TryBuy(string defId, int x, int y)
        {
            var def = Catalog.Building(defId);
            if (def == null || defId == Catalog.CoreId || !IsUnlocked(def)) return null;
            double cost = CostOf(def);
            if (!CanAfford(cost) || !Grid.CanPlace(x, y, def.Size)) return null;

            State.Credits -= cost;
            var b = new PlacedBuilding { Uid = State.NextUid++, DefId = defId, Level = 1, X = x, Y = y };
            State.Buildings.Add(b);
            byUid[b.Uid] = b;
            Grid.Fill(x, y, def.Size, b.Uid);
            Recalculate();
            return b;
        }

        public bool TryMove(int uid, int x, int y)
        {
            var b = Find(uid);
            if (b == null) return false;
            int size = Catalog.Building(b.DefId).Size;
            if (!Grid.CanPlace(x, y, size, uid)) return false;
            Grid.Clear(uid);
            b.X = x;
            b.Y = y;
            Grid.Fill(x, y, size, uid);
            return true;
        }

        public bool TryUpgrade(int uid)
        {
            var b = Find(uid);
            if (b == null) return false;
            double cost = UpgradeCostOf(b);
            if (!CanAfford(cost)) return false;
            State.Credits -= cost;
            b.Level++;
            Recalculate();
            return true;
        }

        /// <summary>Remove a building for a partial refund. Returns the refund, or -1 if it can't be sold.</summary>
        public double TrySell(int uid)
        {
            var b = Find(uid);
            if (b == null || b.DefId == Catalog.CoreId) return -1;
            double refund = SellValueOf(b);
            State.Credits += refund;
            State.Buildings.Remove(b);
            byUid.Remove(uid);
            Grid.Clear(uid);
            Recalculate();
            return refund;
        }

        public bool TryBuyUpgrade(string id)
        {
            var u = Catalog.Upgrade(id);
            if (u == null || !IsUpgradeVisible(u) || !CanAfford(u.Cost)) return false;
            State.Credits -= u.Cost;
            State.Upgrades.Add(id);
            Recalculate();
            return true;
        }

        /// <summary>
        /// Income earned while the game was closed: a reduced rate, capped at a few hours.
        /// Returns the amount added.
        /// </summary>
        public double ApplyOffline(double secondsAway)
        {
            if (secondsAway <= 0) return 0;
            State.FrenzyRemaining = 0;
            double gain = Math.Floor(baseCps * Math.Min(secondsAway, Catalog.OfflineCapSeconds) * Catalog.OfflineRate);
            Earn(gain);
            return gain;
        }

        /// <summary>
        /// The player grabbed a supply drop. roll is a random number in [0, 1):
        /// mostly a lump of credits, sometimes a frenzy.
        /// </summary>
        public DropResult CollectDrop(double roll)
        {
            if (roll < 0.3 && baseCps > 0)
            {
                State.FrenzyRemaining = Catalog.FrenzySeconds;
                return new DropResult { Kind = DropKind.Frenzy };
            }
            // Worth about two minutes of income, a burst of clicks, or a small floor early on.
            double amount = Math.Ceiling(Math.Max(25, Math.Max(baseCps * 120, ClickValue / FrenzyFactor * 40)));
            Earn(amount);
            return new DropResult { Kind = DropKind.Credits, Credits = amount };
        }

        // ---------------------------------------------------------------- internals

        void Earn(double amount)
        {
            State.Credits += amount;
            State.LifetimeCredits += amount;
        }

        /// <summary>Rebuild every cached multiplier and the income total. Called after anything changes.</summary>
        void Recalculate()
        {
            counts.Clear();
            buildingMult.Clear();
            owned.Clear();
            clickMult = 1;
            clickCpsShare = 0;
            globalMult = 1;

            foreach (var id in State.Upgrades)
            {
                var u = Catalog.Upgrade(id);
                if (u == null || !owned.Add(id)) continue;
                switch (u.Kind)
                {
                    case UpgradeKind.ClickMult: clickMult *= u.Value; break;
                    case UpgradeKind.ClickCpsShare: clickCpsShare += u.Value; break;
                    case UpgradeKind.GlobalMult: globalMult *= u.Value; break;
                    case UpgradeKind.BuildingMult:
                        buildingMult[u.TargetId] = (buildingMult.TryGetValue(u.TargetId, out var m) ? m : 1) * u.Value;
                        break;
                }
            }

            baseCps = 0;
            foreach (var b in State.Buildings)
            {
                counts[b.DefId] = CountOf(b.DefId) + 1;
                baseCps += CpsOf(b);
            }
        }

        /// <summary>Repair a loaded save so bad or outdated data can never crash the game.</summary>
        static GameState Sanitise(GameState s)
        {
            if (s.Buildings == null) s.Buildings = new List<PlacedBuilding>();
            if (s.Upgrades == null) s.Upgrades = new List<string>();
            if (double.IsNaN(s.Credits) || s.Credits < 0) s.Credits = 0;
            if (double.IsNaN(s.FrenzyRemaining) || s.FrenzyRemaining < 0) s.FrenzyRemaining = 0;

            var grid = new GridModel(Catalog.GridSize);
            var kept = new List<PlacedBuilding>();
            var uids = new HashSet<int>();
            bool hasCore = false;
            foreach (var b in s.Buildings)
            {
                var def = b == null ? null : Catalog.Building(b.DefId);
                if (def == null || !uids.Add(b.Uid) || b.Uid <= 0) continue;
                if (def.Id == Catalog.CoreId && hasCore) continue;
                if (!grid.CanPlace(b.X, b.Y, def.Size)) continue;
                b.Level = Math.Max(1, Math.Min(Catalog.MaxLevel, b.Level));
                grid.Fill(b.X, b.Y, def.Size, b.Uid);
                kept.Add(b);
                hasCore |= def.Id == Catalog.CoreId;
                s.NextUid = Math.Max(s.NextUid, b.Uid + 1);
            }
            s.Buildings = kept;

            if (!hasCore)
            {
                // Clear whatever sits on the centre plot, then put the core back.
                int o = Catalog.CoreOrigin;
                s.Buildings.RemoveAll(b =>
                {
                    int size = Catalog.Building(b.DefId).Size;
                    return b.X < o + Catalog.CoreSize && b.X + size > o && b.Y < o + Catalog.CoreSize && b.Y + size > o;
                });
                s.Buildings.Add(new PlacedBuilding { Uid = s.NextUid++, DefId = Catalog.CoreId, Level = 1, X = o, Y = o });
            }
            s.Version = GameState.CurrentVersion;
            return s;
        }
    }
}
