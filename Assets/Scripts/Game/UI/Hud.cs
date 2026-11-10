using System;
using System.Collections.Generic;
using StartupClicker.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StartupClicker.Game
{
    /// <summary>
    /// The whole interface, built in code:
    ///   top-left   - credits, income, core upgrade button
    ///   right      - shop with BUILD and UPGRADES tabs
    ///   bottom     - panel for the selected building, hints, toasts
    /// Numbers are refreshed every frame; the shop list is rebuilt only when something changes.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        /// <summary>One row in the shop. Keeps what is needed to refresh its "can I afford this" look.</summary>
        sealed class Card
        {
            public CanvasGroup Group;
            public Text CostText;
            public Func<double> Cost;
            public bool Locked;
        }

        GameController game;
        GameSim sim;
        RectTransform root, fxLayer, listContent;

        Text creditsText, cpsText, clickText, coreText, coreCostText, frenzyText, hintText, toastText;
        Image coreButtonImage;
        GameObject frenzyTag, hintBar;
        CanvasGroup toastGroup;
        PunchScale creditsPunch;
        Button buildTab, upgradesTab;
        Text buildTabText, upgradesTabText, resetText;

        GameObject selectionPanel;
        Text selName, selStats, selUpgradeText, selSellText;
        Image selUpgradeImage, selIcon;

        readonly List<Card> cards = new List<Card>();
        bool showUpgrades;
        bool listDirty = true;
        double shownCredits;
        float toastUntil, resetArmedUntil;

        // ------------------------------------------------------------ construction

        public void Init(GameController controller)
        {
            game = controller;
            sim = controller.Sim;
            shownCredits = sim.State.Credits;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Scale the whole UI with the window: it is laid out for 1920x1080 and follows the height.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            if (EventSystem.current == null)
            {
                // The EventSystem routes mouse input to UI elements; this module makes it use the new Input System.
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            fxLayer = Ui.Stretch(Ui.Node("Fx", root));
            BuildStats();
            BuildShop();
            BuildSelectionPanel();
            BuildOverlays();
            fxLayer.SetAsLastSibling();

            game.Changed += () => listDirty = true;
            game.SelectionChanged += RefreshSelection;
            RefreshSelection();
        }

        void BuildStats()
        {
            var panel = Ui.Img("Stats", root, "ui_panel", Color.white, 20f).rectTransform;
            Ui.Place(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(440, 176));

            var accent = Ui.Img("Accent", panel, null, Ui.Red).rectTransform;
            Ui.Place(accent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(6, 176));

            var header = Ui.Label("Header", panel, "AGENCY FUNDS", 20, Ui.Teal);
            Ui.Place(header.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -14), new Vector2(300, 24));

            var icon = Ui.Img("Icon", panel, "icon_credit", Color.white).rectTransform;
            Ui.Place(icon, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(54, -78), new Vector2(56, 56));

            creditsText = Ui.Label("Credits", panel, "0", 68, Ui.Cream, TextAnchor.MiddleLeft);
            Ui.Place(creditsText.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(94, -78), new Vector2(330, 76));
            creditsPunch = creditsText.gameObject.AddComponent<PunchScale>();
            creditsPunch.SetBase(Vector3.one);

            cpsText = Ui.Label("Cps", panel, "", 26, Ui.Teal, TextAnchor.MiddleLeft);
            Ui.Place(cpsText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 18), new Vector2(200, 30));
            clickText = Ui.Label("Click", panel, "", 26, Ui.Dim, TextAnchor.MiddleRight);
            Ui.Place(clickText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 18), new Vector2(200, 30));

            // core upgrade button
            var core = Ui.Btn("CoreUpgrade", root, "ui_btn", 14f, Ui.Red, game.UpgradeCore);
            coreButtonImage = core.GetComponent<Image>();
            Ui.Place((RectTransform)core.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -212), new Vector2(440, 64));
            coreText = Ui.Label("Label", core.transform, "", 28, Ui.Navy, TextAnchor.MiddleLeft);
            Ui.Stretch(coreText.rectTransform, 22, 0, 0, 0);
            coreCostText = Ui.Label("Cost", core.transform, "", 28, Ui.Navy, TextAnchor.MiddleRight);
            Ui.Stretch(coreCostText.rectTransform, 0, 0, 22, 0);

            var tag = Ui.Img("Frenzy", root, "ui_tag", Ui.Gold, 14f);
            Ui.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -288), new Vector2(300, 48));
            frenzyText = Ui.Label("Label", tag.transform, "", 26, Ui.Navy, TextAnchor.MiddleCenter);
            Ui.Stretch(frenzyText.rectTransform);
            frenzyTag = tag.gameObject;
            frenzyTag.SetActive(false);
        }

        void BuildShop()
        {
            var panel = Ui.Img("Shop", root, "ui_panel", Color.white, 20f);
            panel.raycastTarget = true; // blocks clicks from reaching the world behind the shop
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-24, 0);
            rt.sizeDelta = new Vector2(440, -48);

            buildTab = Ui.Btn("BuildTab", rt, "ui_btn", 14f, Ui.Red, () => SetTab(false));
            Ui.Place((RectTransform)buildTab.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(200, 52));
            buildTabText = Ui.Label("Label", buildTab.transform, "BUILD", 28, Ui.Navy, TextAnchor.MiddleCenter);
            Ui.Stretch(buildTabText.rectTransform);

            upgradesTab = Ui.Btn("UpgradesTab", rt, "ui_btn", 14f, Ui.Slate, () => SetTab(true));
            Ui.Place((RectTransform)upgradesTab.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(200, 52));
            upgradesTabText = Ui.Label("Label", upgradesTab.transform, "UPGRADES", 28, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Stretch(upgradesTabText.rectTransform);

            // Scrolling list: a ScrollRect moves "Content" inside a masked "Viewport".
            var scrollImage = Ui.Img("Scroll", rt, null, new Color(0, 0, 0, 0));
            scrollImage.raycastTarget = true;
            Ui.Stretch(scrollImage.rectTransform, 16, 84, 16, 20);
            var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            var viewport = Ui.Stretch(Ui.Node("Viewport", scrollImage.transform));
            viewport.gameObject.AddComponent<RectMask2D>();
            listContent = Ui.Node("Content", viewport);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.sizeDelta = Vector2.zero;
            // The layout group stacks the cards; the fitter makes Content as tall as all of them together.
            var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 12f;
        }

        void BuildSelectionPanel()
        {
            var panel = Ui.Img("Selection", root, "ui_panel", Color.white, 20f);
            panel.raycastTarget = true;
            // centred in the space left of the shop
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-232, 24), new Vector2(720, 150));
            selectionPanel = panel.gameObject;

            selIcon = Ui.Img("Icon", panel.transform, null, Color.white);
            selIcon.preserveAspect = true;
            Ui.Place(selIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76, 0), new Vector2(116, 116));

            selName = Ui.Label("Name", panel.transform, "", 34, Ui.Cream);
            Ui.Place(selName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -14), new Vector2(400, 40));
            selStats = Ui.Label("Stats", panel.transform, "", 24, Ui.Teal);
            Ui.Place(selStats.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -54), new Vector2(400, 30));

            var up = Ui.Btn("Upgrade", panel.transform, "ui_btn", 14f, Ui.Teal, game.UpgradeSelected);
            selUpgradeImage = up.GetComponent<Image>();
            Ui.Place((RectTransform)up.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(146, 16), new Vector2(270, 48));
            selUpgradeText = Ui.Label("Label", up.transform, "", 24, Ui.Navy, TextAnchor.MiddleCenter);
            Ui.Stretch(selUpgradeText.rectTransform);

            var move = Ui.Btn("Move", panel.transform, "ui_btn", 14f, Ui.Cream, game.BeginMoveSelected);
            Ui.Place((RectTransform)move.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(428, 16), new Vector2(110, 48));
            Ui.Stretch(Ui.Label("Label", move.transform, "MOVE", 24, Ui.Navy, TextAnchor.MiddleCenter).rectTransform);

            var sell = Ui.Btn("Sell", panel.transform, "ui_btn", 14f, Ui.Slate, game.SellSelected);
            Ui.Place((RectTransform)sell.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(550, 16), new Vector2(154, 48));
            selSellText = Ui.Label("Label", sell.transform, "SELL", 24, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Stretch(selSellText.rectTransform);
        }

        void BuildOverlays()
        {
            var hint = Ui.Img("Hint", root, "ui_panel", Color.white, 20f);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-232, 24), new Vector2(980, 56));
            hintText = Ui.Label("Label", hint.transform, "", 24, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Stretch(hintText.rectTransform);
            hintBar = hint.gameObject;
            hintBar.SetActive(false);

            var toast = Ui.Img("Toast", root, "ui_tag", Ui.Cream, 14f);
            Ui.Place(toast.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-232, -28), new Vector2(760, 56));
            toastText = Ui.Label("Label", toast.transform, "", 28, Ui.Navy, TextAnchor.MiddleCenter);
            Ui.Stretch(toastText.rectTransform);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;

            var help = Ui.Label("Help", root, "CLICK THE CORE TO EARN  //  DRAG TO PAN  //  WHEEL TO ZOOM", 20, Ui.Dim, TextAnchor.LowerLeft);
            Ui.Place(help.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 22), new Vector2(600, 24));

            var reset = Ui.Btn("Reset", root, "ui_btn", 14f, new Color(Ui.Slate.r, Ui.Slate.g, Ui.Slate.b, 0.8f), OnResetClicked);
            Ui.Place((RectTransform)reset.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 56), new Vector2(170, 36));
            resetText = Ui.Label("Label", reset.transform, "RESET SAVE", 20, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Stretch(resetText.rectTransform);
        }

        // ------------------------------------------------------------ shop list

        void SetTab(bool upgrades)
        {
            showUpgrades = upgrades;
            listDirty = true;
            Sfx.Play("select", 0.4f);
        }

        void RebuildList()
        {
            listDirty = false;
            cards.Clear();
            for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);

            buildTab.GetComponent<Image>().color = showUpgrades ? Ui.Slate : Ui.Red;
            buildTabText.color = showUpgrades ? Ui.Cream : Ui.Navy;
            upgradesTab.GetComponent<Image>().color = showUpgrades ? Ui.Red : Ui.Slate;
            upgradesTabText.color = showUpgrades ? Ui.Navy : Ui.Cream;

            int available = 0;
            foreach (var u in Catalog.Upgrades) if (sim.IsUpgradeVisible(u)) available++;
            upgradesTabText.text = available > 0 ? "UPGRADES (" + available + ")" : "UPGRADES";

            if (!showUpgrades)
            {
                foreach (var def in Catalog.Buildings) AddBuildingCard(def);
                return;
            }
            foreach (var u in Catalog.Upgrades)
                if (sim.IsUpgradeVisible(u)) AddUpgradeCard(u);
            if (available == 0)
            {
                var empty = Ui.Label("Empty", listContent, "NO UPGRADES AVAILABLE YET.\nBUILD MORE AND LEVEL UP YOUR CORE.", 22, Ui.Dim,
                    TextAnchor.MiddleCenter);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            }
        }

        Card NewCard(string name, float height, Action onClick, out RectTransform rt)
        {
            var btn = Ui.Btn(name, listContent, "ui_card", 16f, Color.white, onClick);
            btn.GetComponent<ButtonJuice>().HoverScale = 1.015f;
            btn.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            rt = (RectTransform)btn.transform;
            var card = new Card { Group = btn.gameObject.AddComponent<CanvasGroup>() };
            cards.Add(card);
            return card;
        }

        void AddCost(Card card, RectTransform rt, Func<double> cost)
        {
            card.Cost = cost;
            card.CostText = Ui.Label("Cost", rt, "", 26, Ui.Gold, TextAnchor.LowerRight);
            Ui.Place(card.CostText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-18, 10), new Vector2(160, 30));
        }

        void AddBuildingCard(BuildingDef def)
        {
            bool locked = !sim.IsUnlocked(def);
            string id = def.Id;
            RectTransform rt;
            var card = NewCard(def.Name, 96, () => game.BeginPlace(id), out rt);
            card.Locked = locked;

            var icon = Ui.Img("Icon", rt, "b_" + def.Id + "_1", locked ? new Color(0.1f, 0.14f, 0.2f, 1f) : Color.white);
            Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(84, 84));

            var name = Ui.Label("Name", rt, locked ? "LOCKED" : def.Name.ToUpperInvariant(), 28, locked ? Ui.Dim : Ui.Cream);
            Ui.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, -10), new Vector2(280, 32));

            string sub = locked
                ? "REQUIRES CORE LEVEL " + def.UnlockCoreLevel
                : "OWNED " + sim.CountOf(def.Id) + "  //  +" + NumberFormat.Format(def.BaseCps, 1) + "/SEC  //  " + def.Size + "x" + def.Size;
            var subText = Ui.Label("Sub", rt, sub, 19, locked ? Ui.Red : Ui.Dim, TextAnchor.UpperLeft, false);
            Ui.Place(subText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, -44), new Vector2(290, 24));

            if (locked)
            {
                var lockIcon = Ui.Img("Lock", rt, "icon_lock", Ui.Dim);
                Ui.Place(lockIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(34, 34));
            }
            else
            {
                AddCost(card, rt, () => sim.CostOf(def));
            }
        }

        void AddUpgradeCard(UpgradeDef u)
        {
            string id = u.Id;
            RectTransform rt;
            var card = NewCard(u.Name, 104, () => game.BuyUpgrade(id), out rt);

            string iconName = u.Kind == UpgradeKind.GlobalMult ? "icon_global"
                : u.Kind == UpgradeKind.BuildingMult ? "icon_up"
                : u.Kind == UpgradeKind.ClickMult ? "icon_click" : "icon_bolt";
            Color tint = u.Kind == UpgradeKind.GlobalMult ? Ui.Gold : u.Kind == UpgradeKind.BuildingMult ? Ui.Teal : Ui.Red;
            var icon = Ui.Img("Icon", rt, iconName, tint);
            Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 0), new Vector2(50, 50));

            var name = Ui.Label("Name", rt, u.Name.ToUpperInvariant(), 26, Ui.Cream);
            Ui.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -10), new Vector2(300, 30));
            var blurb = Ui.Label("Blurb", rt, u.Blurb, 19, Ui.Dim, TextAnchor.UpperLeft, false, true);
            Ui.Place(blurb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -42), new Vector2(210, 50));
            AddCost(card, rt, () => u.Cost);
        }

        // ------------------------------------------------------------ selection panel

        void RefreshSelection()
        {
            var b = game.Selected;
            bool show = b != null && game.Mode == InputMode.Idle;
            selectionPanel.SetActive(show);
            if (!show) return;
            var def = Catalog.Building(b.DefId);
            selIcon.sprite = SpriteBank.Building(b.DefId, b.Level);
            selName.text = def.Name.ToUpperInvariant() + "   <color=#EE4252>LV " + b.Level + "</color>";
            selStats.text = "+" + NumberFormat.Format(sim.CpsOf(b), 1) + " / SEC";
            selSellText.text = "SELL +" + NumberFormat.Format(sim.SellValueOf(b));
        }

        // ------------------------------------------------------------ per-frame refresh

        void Update()
        {
            if (sim == null) return;
            if (listDirty) RebuildList();

            // roll the displayed number towards the real one instead of jumping
            double credits = sim.State.Credits;
            shownCredits += (credits - shownCredits) * (1.0 - Math.Exp(-14.0 * Time.unscaledDeltaTime));
            if (Math.Abs(credits - shownCredits) < 0.5) shownCredits = credits;
            creditsText.text = NumberFormat.Format(shownCredits);
            cpsText.text = "+" + NumberFormat.Format(sim.Cps, 1) + " / SEC";
            clickText.text = NumberFormat.Format(sim.ClickValue, 1) + " / CLICK";

            var core = sim.CoreBuilding;
            double coreCost = sim.UpgradeCostOf(core);
            if (coreCost < 0)
            {
                coreText.text = "COMMAND CORE  //  MAX LEVEL";
                coreCostText.text = "";
                coreButtonImage.color = Ui.Slate;
            }
            else
            {
                coreText.text = "UPGRADE CORE  LV " + core.Level + " > " + (core.Level + 1);
                coreCostText.text = NumberFormat.Format(coreCost);
                coreButtonImage.color = sim.CanAfford(coreCost) ? Ui.Red : Ui.Slate;
            }
            coreText.color = coreCostText.color = sim.CanAfford(coreCost) ? Ui.Navy : Ui.Cream;

            frenzyTag.SetActive(sim.FrenzyActive);
            if (sim.FrenzyActive)
                frenzyText.text = "FRENZY x" + Catalog.FrenzyMult + "  //  " + Mathf.CeilToInt((float)sim.State.FrenzyRemaining) + "s";

            foreach (var card in cards)
            {
                if (card.Locked) { card.Group.alpha = 0.6f; continue; }
                if (card.Cost == null) continue;
                double cost = card.Cost();
                bool ok = sim.CanAfford(cost);
                card.CostText.text = NumberFormat.Format(cost);
                card.CostText.color = ok ? Ui.Gold : Ui.Red;
                card.Group.alpha = ok ? 1f : 0.62f;
            }

            if (selectionPanel.activeSelf)
            {
                var b = game.Selected;
                if (b == null || game.Mode != InputMode.Idle)
                {
                    selectionPanel.SetActive(false);
                }
                else
                {
                    double cost = sim.UpgradeCostOf(b);
                    selUpgradeText.text = cost < 0 ? "MAX LEVEL" : "UPGRADE  " + NumberFormat.Format(cost);
                    selUpgradeImage.color = cost >= 0 && sim.CanAfford(cost) ? Ui.Teal : Ui.Slate;
                    selUpgradeText.color = cost >= 0 && sim.CanAfford(cost) ? Ui.Navy : Ui.Cream;
                }
            }

            // toast fade in / out
            float target = Time.unscaledTime < toastUntil ? 1f : 0f;
            toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, Time.unscaledDeltaTime * 5f);

            if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
            {
                resetArmedUntil = 0f;
                resetText.text = "RESET SAVE";
            }
        }

        // ------------------------------------------------------------ feedback API (called by GameController)

        public void Toast(string message, float seconds, bool warning = false)
        {
            toastText.text = message;
            toastText.transform.parent.GetComponent<Image>().color = warning ? Ui.Red : Ui.Cream;
            toastUntil = Time.unscaledTime + seconds;
        }

        public void SetHint(string message)
        {
            hintBar.SetActive(!string.IsNullOrEmpty(message));
            if (message != null) hintText.text = message;
            RefreshSelection();
        }

        public void BumpCredits()
        {
            creditsPunch.Punch(0.25f);
        }

        /// <summary>Spawn a number at a screen position that drifts up and fades out.</summary>
        public void FloatText(Vector2 screen, string content, Color color, int size, float life = 0.8f)
        {
            var text = Ui.Label("Float", fxLayer, content, size, color, TextAnchor.MiddleCenter);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.07f, 0.1f, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);
            var rt = text.rectTransform;
            rt.sizeDelta = new Vector2(300, 60);

            // convert screen pixels into the canvas's own (scaled) coordinates
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, screen, null, out local);
            float drift = UnityEngine.Random.Range(-18f, 18f);
            Tween.Run(life, t =>
            {
                if (rt == null) return;
                rt.localPosition = local + new Vector2(drift * t, 90f * Tween.OutCubic(t));
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, (t - 0.15f) / 0.85f);
                rt.localScale = new Vector3(pop, pop, 1f);
                var c = color;
                c.a = color.a * (1f - Mathf.Clamp01((t - 0.55f) / 0.45f));
                text.color = c;
            }, () => { if (rt != null) Destroy(rt.gameObject); });
        }

        void OnResetClicked()
        {
            // two clicks needed, so a stray click cannot wipe progress
            if (Time.unscaledTime < resetArmedUntil)
            {
                game.ResetGame();
                return;
            }
            resetArmedUntil = Time.unscaledTime + 3f;
            resetText.text = "CLICK AGAIN TO WIPE";
            Sfx.Play("error", 0.4f);
        }
    }
}
