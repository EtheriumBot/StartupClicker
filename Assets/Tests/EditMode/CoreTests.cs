using NUnit.Framework;
using StartupClicker.Core;

namespace StartupClicker.Tests
{
    public class CoreTests
    {
        static GameSim Rich(double credits = 1e15)
        {
            var sim = new GameSim();
            sim.State.Credits = credits;
            return sim;
        }

        [Test]
        public void NewGame_HasCoreInCentre_AndNoIncome()
        {
            var sim = new GameSim();
            Assert.AreEqual(1, sim.CoreLevel);
            Assert.AreEqual(sim.CoreBuilding.Uid, sim.Grid.At(12, 12));
            Assert.AreEqual(0, sim.Cps);
            Assert.AreEqual(1, sim.ClickValue);
        }

        [Test]
        public void Click_AddsCredits()
        {
            var sim = new GameSim();
            sim.Click();
            sim.Click();
            Assert.AreEqual(2, sim.State.Credits);
            Assert.AreEqual(2, sim.State.Clicks);
        }

        [Test]
        public void Buy_ChargesAndRaisesNextPrice()
        {
            var sim = Rich(100);
            var garage = Catalog.Building("garage");
            Assert.AreEqual(15, sim.CostOf(garage));
            Assert.NotNull(sim.TryBuy("garage", 0, 0));
            Assert.AreEqual(85, sim.State.Credits);
            Assert.AreEqual(18, sim.CostOf(garage)); // ceil(15 * 1.15)
            Assert.AreEqual(0.1, sim.Cps, 1e-9);
        }

        [Test]
        public void Buy_FailsWhenBlockedPoorOrLocked()
        {
            var sim = Rich(100);
            Assert.IsNull(sim.TryBuy("garage", 12, 12), "on top of the core");
            Assert.IsNull(sim.TryBuy("garage", -1, 0), "off the map");
            Assert.IsNull(sim.TryBuy("drone", 0, 0), "locked behind core level 2");
            sim.State.Credits = 5;
            Assert.IsNull(sim.TryBuy("garage", 0, 0), "cannot afford");
            Assert.AreEqual(5, sim.State.Credits);
        }

        [Test]
        public void LargeFootprint_MustFitOnMap()
        {
            var sim = Rich();
            while (sim.CoreLevel < 4) sim.TryUpgrade(sim.CoreBuilding.Uid);
            Assert.IsNull(sim.TryBuy("academy", 23, 23));
            Assert.NotNull(sim.TryBuy("academy", 22, 22));
            Assert.IsNull(sim.TryBuy("garage", 24, 24), "cell taken by the academy");
        }

        [Test]
        public void Move_FreesOldCells_AndAllowsOverlapWithSelf()
        {
            var sim = Rich();
            sim.TryUpgrade(sim.CoreBuilding.Uid);
            var b = sim.TryBuy("drone", 0, 0);
            Assert.IsTrue(sim.TryMove(b.Uid, 1, 1));
            Assert.AreEqual(0, sim.Grid.At(0, 0));
            Assert.AreEqual(b.Uid, sim.Grid.At(2, 2));
            Assert.IsFalse(sim.TryMove(b.Uid, 11, 11));
            Assert.AreEqual(b.Uid, sim.Grid.At(1, 1), "failed move leaves it in place");
        }

        [Test]
        public void Upgrade_DoublesOutput_AndStopsAtMax()
        {
            var sim = Rich();
            var b = sim.TryBuy("recon", 0, 0);
            Assert.AreEqual(1, sim.Cps, 1e-9);
            Assert.AreEqual(250, sim.UpgradeCostOf(b));
            Assert.IsTrue(sim.TryUpgrade(b.Uid));
            Assert.AreEqual(2, sim.Cps, 1e-9);
            while (sim.TryUpgrade(b.Uid)) { }
            Assert.AreEqual(Catalog.MaxLevel, b.Level);
            Assert.AreEqual(16, sim.Cps, 1e-9);
            Assert.AreEqual(-1, sim.UpgradeCostOf(b));
        }

        [Test]
        public void CoreUpgrade_UnlocksBuildings_AndBoostsClicks()
        {
            var sim = Rich(1000);
            Assert.IsFalse(sim.IsUnlocked(Catalog.Building("drone")));
            Assert.IsTrue(sim.TryUpgrade(sim.CoreBuilding.Uid));
            Assert.AreEqual(0, sim.State.Credits);
            Assert.IsTrue(sim.IsUnlocked(Catalog.Building("drone")));
            Assert.AreEqual(2, sim.ClickValue);
        }

        [Test]
        public void Upgrades_ApplyAndCannotBeBoughtTwice()
        {
            var sim = Rich();
            Assert.IsTrue(sim.TryBuyUpgrade("click1"));
            Assert.IsFalse(sim.TryBuyUpgrade("click1"));
            Assert.AreEqual(2, sim.ClickValue);

            Assert.IsFalse(sim.TryBuyUpgrade("garage_mk2"), "needs 5 garages");
            for (int i = 0; i < 5; i++) sim.TryBuy("garage", i, 0);
            Assert.IsTrue(sim.TryBuyUpgrade("garage_mk2"));
            Assert.AreEqual(1.0, sim.Cps, 1e-9);
        }

        [Test]
        public void Sell_RefundsAndFreesCells()
        {
            var sim = Rich(15);
            var b = sim.TryBuy("garage", 3, 3);
            Assert.AreEqual(3, sim.TrySell(b.Uid)); // floor(15 * 0.25)
            Assert.AreEqual(0, sim.Grid.At(3, 3));
            Assert.AreEqual(0, sim.CountOf("garage"));
            Assert.AreEqual(-1, sim.TrySell(sim.CoreBuilding.Uid));
        }

        [Test]
        public void Tick_PaysIncome_AndFrenzyOnlyBoostsItsOwnTime()
        {
            var sim = Rich(100);
            sim.TryBuy("recon", 0, 0); // 1 credit/sec
            sim.State.Credits = 0;
            sim.Tick(10);
            Assert.AreEqual(10, sim.State.Credits, 1e-9);

            sim.State.Credits = 0;
            sim.State.FrenzyRemaining = 2;
            sim.Tick(5); // 2s at x7 + 3s at x1
            Assert.AreEqual(17, sim.State.Credits, 1e-9);
            Assert.IsFalse(sim.FrenzyActive);
        }

        [Test]
        public void Offline_IsHalfRateAndCapped()
        {
            var sim = Rich(100);
            sim.TryBuy("recon", 0, 0);
            sim.State.Credits = 0;
            Assert.AreEqual(50, sim.ApplyOffline(100));
            Assert.AreEqual(Catalog.OfflineCapSeconds * 0.5, sim.ApplyOffline(1e9));
        }

        [Test]
        public void Drop_GivesCreditsOrFrenzy()
        {
            var sim = Rich(100);
            Assert.AreEqual(DropKind.Credits, sim.CollectDrop(0.0).Kind, "no frenzy without income");
            sim.TryBuy("recon", 0, 0);
            Assert.AreEqual(DropKind.Frenzy, sim.CollectDrop(0.0).Kind);
            Assert.IsTrue(sim.FrenzyActive);
            Assert.AreEqual(7, sim.Cps, 1e-9);
            var r = sim.CollectDrop(0.9);
            Assert.AreEqual(120, r.Credits, "frenzy does not inflate the lump sum");
        }

        [Test]
        public void LoadingBrokenSave_IsRepaired()
        {
            var s = new GameState { Credits = double.NaN };
            s.Buildings.Add(new PlacedBuilding { Uid = 5, DefId = "garage", X = 12, Y = 12, Level = 99 });
            s.Buildings.Add(new PlacedBuilding { Uid = 6, DefId = "nonsense", X = 0, Y = 0 });
            s.Buildings.Add(new PlacedBuilding { Uid = 7, DefId = "recon", X = 500, Y = 0 });
            var sim = new GameSim(s);
            Assert.AreEqual(0, sim.State.Credits);
            Assert.AreEqual(1, sim.State.Buildings.Count, "only a fresh core survives");
            Assert.AreEqual(Catalog.CoreId, sim.State.Buildings[0].DefId);
            Assert.AreEqual(sim.CoreBuilding.Uid, sim.Grid.At(12, 12));
        }

        [Test]
        public void SavedState_RebuildsIdenticalSim()
        {
            var sim = Rich();
            sim.TryBuy("garage", 1, 1);
            sim.TryBuy("recon", 2, 2);
            sim.TryBuyUpgrade("click1");
            var again = new GameSim(sim.State);
            Assert.AreEqual(sim.Cps, again.Cps);
            Assert.AreEqual(sim.ClickValue, again.ClickValue);
            Assert.AreEqual(sim.Grid.At(2, 2), again.Grid.At(2, 2));
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1234, "1,234")]
        [TestCase(999999, "999,999")]
        [TestCase(1000000, "1M")]
        [TestCase(1234567, "1.23M")]
        [TestCase(1999999999, "1.99B")]
        [TestCase(2.5e12, "2.5T")]
        public void NumberFormat_Short(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Format(value));
        }

        [Test]
        public void NumberFormat_SmallDecimals()
        {
            Assert.AreEqual("0.1", NumberFormat.Format(0.1, 1));
            Assert.AreEqual("8", NumberFormat.Format(8, 1));
            Assert.AreEqual("1,500", NumberFormat.Format(1500.4, 1));
        }
    }
}
