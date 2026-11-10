using System;
using System.Collections.Generic;

namespace StartupClicker.Core
{
    /// <summary>One building standing on the grid. (X, Y) is the footprint's lowest-coordinate cell.</summary>
    [Serializable]
    public class PlacedBuilding
    {
        public int Uid;
        public string DefId;
        public int Level = 1;
        public int X;
        public int Y;
    }

    /// <summary>
    /// Everything that gets saved. Public fields only, so Unity's JsonUtility can serialise it directly.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public double Credits;
        public double LifetimeCredits;
        public long Clicks;
        public int NextUid = 1;
        public long LastSavedUnix;
        public double FrenzyRemaining;
        public List<PlacedBuilding> Buildings = new List<PlacedBuilding>();
        public List<string> Upgrades = new List<string>();

        public static GameState NewGame()
        {
            var s = new GameState();
            s.Buildings.Add(new PlacedBuilding
            {
                Uid = s.NextUid++, DefId = Catalog.CoreId, Level = 1,
                X = Catalog.CoreOrigin, Y = Catalog.CoreOrigin,
            });
            return s;
        }
    }
}
