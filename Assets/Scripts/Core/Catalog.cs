using System;
using System.Collections.Generic;

namespace StartupClicker.Core
{
    /// <summary>Static description of one kind of building.</summary>
    public sealed class BuildingDef
    {
        public string Id;
        public string Name;
        public string Blurb;
        /// <summary>Footprint is Size x Size grid cells.</summary>
        public int Size;
        public double BaseCost;
        /// <summary>Credits per second produced by one level-1 instance.</summary>
        public double BaseCps;
        /// <summary>Command Core level needed before this shows up in the shop.</summary>
        public int UnlockCoreLevel;
    }

    public enum UpgradeKind
    {
        /// <summary>Multiplies the base value of a click.</summary>
        ClickMult,
        /// <summary>Each click also earns Value (a fraction) of current credits/sec.</summary>
        ClickCpsShare,
        /// <summary>Multiplies the output of every building.</summary>
        GlobalMult,
        /// <summary>Multiplies the output of one building type (TargetId).</summary>
        BuildingMult,
    }

    public sealed class UpgradeDef
    {
        public string Id;
        public string Name;
        public string Blurb;
        public double Cost;
        public UpgradeKind Kind;
        public double Value;
        public string TargetId;
        public int RequireCoreLevel = 1;
        /// <summary>If set, the player must own RequireCount of TargetId first.</summary>
        public int RequireCount;
    }

    /// <summary>All game data lives here so balance can be tuned in one file.</summary>
    public static class Catalog
    {
        public const string CoreId = "core";
        public const int GridSize = 25;
        public const int CoreSize = 3;
        public const int CoreOrigin = 11; // (11,11)..(13,13) is the centre of a 25x25 grid
        public const int MaxLevel = 5;
        public const double CostGrowth = 1.15;
        public const double SellRefund = 0.25;
        public const double OfflineCapSeconds = 8 * 3600;
        public const double OfflineRate = 0.5;
        public const double FrenzyMult = 7;
        public const double FrenzySeconds = 30;

        /// <summary>Cost to take the Command Core from level (index+1) to the next level.</summary>
        public static readonly double[] CoreUpgradeCosts = { 1000, 50000, 2500000, 150000000 };

        public static readonly BuildingDef Core = new BuildingDef
        {
            Id = CoreId, Name = "Command Core", Size = CoreSize, UnlockCoreLevel = 1,
            Blurb = "The heart of your agency. Click it to generate credits.",
        };

        public static readonly BuildingDef[] Buildings =
        {
            B("garage",  "Garage Outpost", "Two founders, one soldering iron.",            1, 15,        0.1,   1),
            B("recon",   "Recon Desk",     "Analysts turning coffee into intel.",          1, 100,       1,     1),
            B("drone",   "Drone Bay",      "Autonomous couriers that never clock out.",    2, 1100,      8,     2),
            B("signal",  "Signal Tower",   "Listens to everything. Bills for all of it.",  1, 12000,     47,    2),
            B("vault",   "Server Vault",   "Cold racks, hot margins.",                     2, 130000,    260,   3),
            B("lab",     "R&D Lab",        "Prototype tech of questionable legality.",     2, 1400000,   1400,  3),
            B("academy", "Agent Academy",  "Trains field agents for premium contracts.",   3, 20000000,  7800,  4),
            B("uplink",  "Orbital Uplink", "Your own satellite constellation.",            3, 330000000, 44000, 5),
        };

        public static readonly UpgradeDef[] Upgrades = BuildUpgrades();

        static readonly Dictionary<string, BuildingDef> BuildingById = IndexBuildings();
        static readonly Dictionary<string, UpgradeDef> UpgradeById = IndexUpgrades();

        public static BuildingDef Building(string id)
        {
            return BuildingById.TryGetValue(id, out var def) ? def : null;
        }

        public static UpgradeDef Upgrade(string id)
        {
            return UpgradeById.TryGetValue(id, out var def) ? def : null;
        }

        static BuildingDef B(string id, string name, string blurb, int size, double cost, double cps, int unlock)
        {
            return new BuildingDef
            {
                Id = id, Name = name, Blurb = blurb, Size = size,
                BaseCost = cost, BaseCps = cps, UnlockCoreLevel = unlock,
            };
        }

        static UpgradeDef[] BuildUpgrades()
        {
            var list = new List<UpgradeDef>
            {
                new UpgradeDef { Id = "click1", Name = "Haptic Gloves", Blurb = "Clicks are twice as strong.",
                    Cost = 100, Kind = UpgradeKind.ClickMult, Value = 2 },
                new UpgradeDef { Id = "click2", Name = "Neural Trigger", Blurb = "Clicks are twice as strong.",
                    Cost = 5000, Kind = UpgradeKind.ClickMult, Value = 2, RequireCoreLevel = 2 },
                new UpgradeDef { Id = "click3", Name = "Overclock Rig", Blurb = "Clicks are twice as strong.",
                    Cost = 500000, Kind = UpgradeKind.ClickMult, Value = 2, RequireCoreLevel = 3 },
                new UpgradeDef { Id = "link1", Name = "Kinetic Link", Blurb = "Each click also earns 1% of your credits/sec.",
                    Cost = 50000, Kind = UpgradeKind.ClickCpsShare, Value = 0.01, RequireCoreLevel = 2 },
                new UpgradeDef { Id = "link2", Name = "Kinetic Link II", Blurb = "Each click earns a further 2% of your credits/sec.",
                    Cost = 5000000, Kind = UpgradeKind.ClickCpsShare, Value = 0.02, RequireCoreLevel = 3 },
                new UpgradeDef { Id = "fund1", Name = "Seed Round", Blurb = "All buildings produce 25% more.",
                    Cost = 25000, Kind = UpgradeKind.GlobalMult, Value = 1.25, RequireCoreLevel = 2 },
                new UpgradeDef { Id = "fund2", Name = "Series A", Blurb = "All buildings produce 25% more.",
                    Cost = 2000000, Kind = UpgradeKind.GlobalMult, Value = 1.25, RequireCoreLevel = 3 },
                new UpgradeDef { Id = "fund3", Name = "Series B", Blurb = "All buildings produce 50% more.",
                    Cost = 150000000, Kind = UpgradeKind.GlobalMult, Value = 1.5, RequireCoreLevel = 4 },
                new UpgradeDef { Id = "fund4", Name = "Going Public", Blurb = "All buildings produce twice as much.",
                    Cost = 10000000000, Kind = UpgradeKind.GlobalMult, Value = 2, RequireCoreLevel = 5 },
            };

            // Two tiers of "this building type is twice as good", unlocked by owning enough of them.
            foreach (var b in Buildings)
            {
                list.Add(new UpgradeDef { Id = b.Id + "_mk2", Name = b.Name + " Mk II",
                    Blurb = b.Name + "s produce twice as much.", Cost = b.BaseCost * 10,
                    Kind = UpgradeKind.BuildingMult, Value = 2, TargetId = b.Id, RequireCount = 5 });
                list.Add(new UpgradeDef { Id = b.Id + "_mk3", Name = b.Name + " Mk III",
                    Blurb = b.Name + "s produce twice as much.", Cost = b.BaseCost * 100,
                    Kind = UpgradeKind.BuildingMult, Value = 2, TargetId = b.Id, RequireCount = 15 });
            }

            list.Sort((a, c) => a.Cost.CompareTo(c.Cost));
            return list.ToArray();
        }

        static Dictionary<string, BuildingDef> IndexBuildings()
        {
            var d = new Dictionary<string, BuildingDef> { { Core.Id, Core } };
            foreach (var b in Buildings) d[b.Id] = b;
            return d;
        }

        static Dictionary<string, UpgradeDef> IndexUpgrades()
        {
            var d = new Dictionary<string, UpgradeDef>();
            foreach (var u in Upgrades) d[u.Id] = u;
            return d;
        }
    }
}
