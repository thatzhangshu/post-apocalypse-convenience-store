using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace ConvenienceStore.Prototype
{
    public sealed class PrototypeApp : MonoBehaviour
    {
        public const string StorageKey = "convenience-store.prototype.v1";
        public Font font;
        public TextAsset configuration;
        private GameSession game;
        private PrototypeUI ui;
        private RectTransform safe, frame, page, overlay;
        private CanvasGroup pageInput;
        private Rect lastSafe;
        private Vector2 lastScreen;
        private string tab = "store", selectedGood = "food", notice = "先和小林、许老师聊聊，再决定今天如何分配时间。";
        private bool ready, storageReady, modalActive, typing, reviewingFinished;
        private DialogueRunner dialogue;
        private Text dialogueText;
        private string fullLine;
        private float typeClock;
        private Button[] choiceButtons;
        private Button nextButton;
        private Text nextCaption;
        private string storageWarning;

        private void Start()
        {
            Application.targetFrameRate = 30;
            ui = new PrototypeUI(font); BuildCanvas();
            try { game = new GameSession(GameConfig.Parse(configuration.text)); }
            catch (Exception e) { ui.ScrollText(page, "配置无法读取\n" + e.Message, 32, 80, 656, 800); Debug.LogException(e); return; }
            selectedGood = game.Config.goods[0].id;
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            WX.InitSDK(code =>
            {
                if (this == null) return;
                storageReady = code == 0;
                if (!storageReady) storageWarning = "微信存储初始化失败，本次进度暂无法保存。";
                else { WX.OnHide(OnWxHide); WX.OnShow(OnWxShow); }
                Begin();
            });
            ui.Label(page, "正在打开街角的灯……", 32, 500, 656, 90, 30, PrototypeUI.Mint, TextAnchor.MiddleCenter);
#else
            storageReady = true; Begin();
#endif
        }
        private void Begin()
        {
            ready = true; bool loaded = false;
            if (storageReady && !IsCapture)
            {
                try
                {
                    string saved = ReadSave();
                    if (!string.IsNullOrEmpty(saved))
                    {
                        string error; loaded = game.TryRestore(saved, out error);
                        if (!loaded)
                        {
                            WriteStorage(StorageKey + ".unreadable", saved);
                            storageWarning = "旧存档无法读取，已保留备份。本次从新游戏开始。";
                        }
                    }
                }
                catch (Exception e) { storageWarning = "读取存档失败：" + e.Message; }
            }
            if (loaded) notice = "已继续上次进度。阅读、进货和调整货架不消耗时段。";
            Render();
            if (!loaded && !IsCapture) ShowInfo("欢迎来到街角", "每天只有 3 个行动时段。\n\n准备：和 NPC 交谈、听广播、进货、调整两处货架，不消耗时间。\n\n行动：营业消耗 1 时段；探索按事件消耗时段。地图只预览，确认事件选项才真正出发。\n\n罐头、饮用水、药品只有上架后才能卖。高价每份多卖 ¥2，但每时段需求减少 1。\n\n第一天医院药房与高峰营业争夺时间。试着选择你愿意承担的代价。\n\n每次操作后自动保存。行动结束进入日结，第 3 天结束看到本次结果。");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsCapture) gameObject.AddComponent<PrototypeCaptureDriver>();
#endif
        }
        private void BuildCanvas()
        {
            var canvas = new GameObject("PrototypeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = .5f;
            var background = PrototypeUI.Rect(canvas.transform, "Background", 0, 0, 720, 1280);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = PrototypeUI.Night;
            safe = PrototypeUI.Rect(canvas.transform, "SafeArea", 0, 0, 720, 1280);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
            frame = PrototypeUI.Rect(safe, "Frame", 0, 0, 720, 1280);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            page = PrototypeUI.Rect(frame, "Page", 0, 0, 720, 1280);
            pageInput = page.gameObject.AddComponent<CanvasGroup>();
            overlay = PrototypeUI.Rect(frame, "Modal", 0, 0, 720, 1280); overlay.gameObject.SetActive(false);
            lastSafe = new Rect(-1, -1, -1, -1);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
        }
        private void Update()
        {
            Rect area = Screen.safeArea;
            if (safe != null && Screen.width > 0 && Screen.height > 0 && (area != lastSafe || lastScreen != new Vector2(Screen.width, Screen.height)))
            {
                safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                Canvas.ForceUpdateCanvases();
                frame.localScale = Vector3.one * Mathf.Min(safe.rect.width / 720f, safe.rect.height / 1280f);
                lastSafe = area; lastScreen = new Vector2(Screen.width, Screen.height);
            }
            if (typing && dialogueText != null)
            {
                typeClock += Time.unscaledDeltaTime * 42;
                int count = Mathf.Min(fullLine.Length, (int)typeClock);
                dialogueText.text = fullLine.Substring(0, count);
                if (count >= fullLine.Length) FinishTyping();
            }
        }
        private void Render()
        {
            pageInput.interactable = pageInput.blocksRaycasts = !modalActive;
            PrototypeUI.Clear(page);
            var day = game.Config.days[game.State.day - 1];
            ui.Label(page, "街角 / SURVIVAL JOURNAL", 30, 22, 520, 28, 17, PrototypeUI.Muted);
            ui.Label(page, game.Config.title, 30, 56, 440, 54, 38);
            ui.Button(page, "玩法", 572, 48, 118, 60, () => ShowInfo("经营与行动", "每天 3 个时段。准备不花时间，营业用 1 时段，探索按事件扣除。\n\n只有上架的商品参与营业。两处货架限制商品种类；撤下商品保留库存。\n\n高价每件加 ¥2，每时段需求减 1。医疗支援增加药品需求；校园互助增加罐头需求。\n\n情报和事件有开放日期。探索先预览，确认选项才消耗时间和物资。\n\n行动后自动保存；恢复时返回店铺、日结或最终结果。"));
            Pill("第 " + game.State.day + " / " + game.Config.days.Length + " 天", 30, 128, 172, PrototypeUI.Gold);
            Pill("¥ " + game.State.money, 214, 128, 184, PrototypeUI.Mint);
            Pill("剩余 " + game.State.slots + " / " + game.Config.slotsPerDay + " 时段", 410, 128, 280, PrototypeUI.Ink);
            ui.Label(page, day.title + "  /  " + day.weather, 32, 195, 656, 40, 23, PrototypeUI.Muted);
            if (tab == "map") Map(); else if (tab == "journal") Journal(); else Store();
            ui.Button(page, "便利店", 30, 1186, 212, 68, () => Switch("store"), true, tab == "store");
            ui.Button(page, "城市地图", 254, 1186, 212, 68, () => Switch("map"), true, tab == "map");
            ui.Button(page, "情报日志", 478, 1186, 212, 68, () => Switch("journal"), true, tab == "journal");
            if (!modalActive && game.State.settled && !(game.Finished && reviewingFinished)) ShowSettlement();
        }
        private void Pill(string text, float x, float y, float w, Color color)
        { var r = ui.Box(page, "Status", x, y, w, 54, PrototypeUI.Panel); ui.Label(r, text, 10, 5, w - 20, 44, 25, color, TextAnchor.MiddleCenter); }
        private void Switch(string view) { if (modalActive) return; tab = view; Render(); }
        private void Store()
        {
            var room = ui.Box(page, "Convenience store", 30, 250, 660, 425, PrototypeUI.Hex(0x293B40));
            ui.Box(room, "Floor", 0, 286, 660, 139, PrototypeUI.Hex(0x475153), false);
            for (int i = 0; i < 6; i++) ui.Box(room, "Floor line", 0, 308 + i * 22, 660, 2, PrototypeUI.Hex(0x566363), false);
            for (int i = 0; i < 8; i++) ui.Box(room, "Tile seam", i * 90, 286, 2, 139, PrototypeUI.Hex(0x566363), false);
            ui.Box(room, "Window frame", 22, 24, 270, 124, PrototypeUI.Hex(0x72827B));
            var window = ui.Box(room, "Night window", 28, 30, 258, 112, PrototypeUI.Hex(0x152D38), false);
            for (int i = 0; i < 7; i++)
            {
                float height = 38 + i % 3 * 17;
                ui.Box(window, "City", i * 38, 112 - height, 32, height, PrototypeUI.Hex(0x203E48), false);
                ui.Box(window, "City light", i * 38 + 10, 116 - height, 5, 7, PrototypeUI.Gold, false);
            }
            ui.Box(room, "Window divider", 152, 26, 4, 120, PrototypeUI.Hex(0x72827B), false);
            ui.Label(room, "封锁城区 · 仍然营业", 42, 46, 230, 30, 18, PrototypeUI.Muted);
            var sign = ui.Box(room, "Shop sign", 324, 22, 314, 60, PrototypeUI.Hex(0xD6BA7C));
            ui.Label(sign, "街角便利店", 14, 7, 286, 46, 30, PrototypeUI.Night, TextAnchor.MiddleCenter);
            Shelf(room, "food", 26, 180); Shelf(room, "water", 176, 180); Shelf(room, "medicine", 326, 180);
            ui.Button(room, "广播", 316, 102, 106, 60, () => OpenDialogue(game.Config.days[game.State.day - 1].radioDialogue), true, false, 20);
            ui.Button(room, "小林", 448, 100, 86, 62, () => OpenDialogue(game.Config.npcs[0].dialogue), true, false, 22);
            ui.Button(room, "许老师", 540, 100, 96, 62, () => OpenDialogue(game.Config.npcs[1].dialogue), true, false, 20);
            var counter = ui.Box(room, "Checkout", 478, 203, 158, 148, PrototypeUI.Hex(0xB69775));
            ui.Box(counter, "Counter top", 0, 0, 158, 14, PrototypeUI.Hex(0xDEC8A2));
            ui.Box(counter, "Register", 30, -32, 65, 42, PrototypeUI.Hex(0x1B2C32));
            ui.Label(counter, "¥ " + game.State.money, 32, -23, 66, 30, 16, PrototypeUI.Mint);
            ui.Button(counter, "柜台", 12, 66, 134, 62, () => ShowInfo("柜台 · 营业预览", BusinessPreview()), true, false, 23);
            ui.Button(room, "出门探索 →", 468, 361, 172, 50, () => Switch("map"), true, true, 20);
            ui.Label(room, "点货架配置商品 · 点人物获取情报", 26, 372, 432, 32, 19, PrototypeUI.Ink);
            StockPanel();
            ui.ScrollText(page, notice, 32, 987, 656, 65, 21, PrototypeUI.Gold);
            ui.Button(page, "营业 1 时段", 30, 1062, 418, 82, () => Action(game.Business), game.CanAct, true, 29);
            ui.Button(page, "休整 1 时段", 462, 1062, 228, 82, () => Confirm("休整", "本次消耗 1 时段，没有营业收入。是否休整？", () => Action(game.Rest)), game.CanAct, false, 23);
            ui.Label(page, "准备不耗时 · 操作后自动保存", 32, 1152, 656, 26, 17, PrototypeUI.Muted);
        }
        private void Shelf(Transform room, string id, float x, float y)
        {
            var good = game.Config.Good(id); var stock = game.Stock(id);
            var r = ui.Box(room, "Shelf " + id, x, y, 132, 171, stock.onShelf ? PrototypeUI.Hex(0x697A70) : PrototypeUI.Hex(0x394E51));
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if (row * 4 + col >= stock.count) break;
                    var color = id == "food" ? PrototypeUI.Gold : id == "water" ? PrototypeUI.Hex(0x9CBFCE) : PrototypeUI.Hex(0xDDA7A0);
                    ui.Box(r, "Goods", 12 + col * 28, 15 + row * 53, 18, 35, color);
                    if (id == "medicine") ui.Label(r, "+", 12 + col * 28, 15 + row * 53, 18, 35, 22, PrototypeUI.Ink, TextAnchor.MiddleCenter);
                }
                ui.Box(r, "Shelf board", 6, 53 + row * 53, 120, 7, PrototypeUI.Hex(0xB4BEA5), false);
            }
            ui.Button(r, good.name + " " + stock.count, 7, 116, 118, 48, () => { selectedGood = id; Render(); }, true, id == selectedGood, 21);
            ui.Label(room, stock.onShelf ? "已上架" : "仓库中", x, y - 25, 132, 24, 17, stock.onShelf ? PrototypeUI.Mint : PrototypeUI.Muted, TextAnchor.MiddleCenter);
        }
        private void StockPanel()
        {
            var panel = ui.Box(page, "Inventory", 30, 696, 660, 274, PrototypeUI.Panel);
            ui.Label(panel, "库存与货架", 18, 15, 360, 38, 25);
            ui.Label(panel, "已用 " + game.State.stock.Count(x => x.onShelf) + " / " + game.Config.shelfCapacity + " 架位", 400, 20, 242, 30, 20, PrototypeUI.Muted, TextAnchor.UpperRight);
            for (int i = 0; i < game.Config.goods.Length; i++)
            {
                var good = game.Config.goods[i]; string id = good.id;
                ui.Button(panel, good.name + " ×" + game.Stock(id).count, 18 + i * 210, 62, 202, 52, () => { selectedGood = id; Render(); }, true, selectedGood == id, 22);
            }
            var current = game.Config.Good(selectedGood); var stock = game.Stock(selectedGood);
            ui.Label(panel, "进价 ¥" + current.buyPrice + "  /  售价 ¥" + game.Price(selectedGood) + "  /  需求 " + game.Demand(selectedGood) + " 每时段", 20, 128, 618, 32, 21, PrototypeUI.Ink);
            ui.Label(panel, "今日供应 " + (game.Config.dailyPurchaseLimit - stock.purchased) + " 份 · " + (stock.onShelf ? "营业会出售" : "未上架，不会出售"), 20, 165, 618, 30, 19, PrototypeUI.Muted);
            ui.Button(panel, "进货 +1", 18, 205, 202, 53, () => Action(() => game.Buy(selectedGood)), game.CanAct && game.State.money >= current.buyPrice && stock.purchased < game.Config.dailyPurchaseLimit, true, 22);
            ui.Button(panel, stock.onShelf ? "撤下货架" : "上架商品", 228, 205, 202, 53, () => Action(() => game.ToggleShelf(selectedGood)), game.CanAct, false, 22);
            ui.Button(panel, stock.premium ? "改为平价" : "改为高价", 438, 205, 202, 53, () => Action(() => game.TogglePrice(selectedGood)), game.CanAct, false, 22);
        }
        private string BusinessPreview()
        {
            string text = "营业消耗 1 时段。以下是当前配置下的预计销量：\n\n";
            int sum = 0;
            foreach (var good in game.Config.goods)
            {
                var stock = game.Stock(good.id); int sold = stock.onShelf ? Mathf.Min(stock.count, game.Demand(good.id)) : 0;
                int income = sold * game.Price(good.id); sum += income;
                text += good.name + "：" + (stock.onShelf ? "上架" : "仓库") + "，库存 " + stock.count + "，需求 " + game.Demand(good.id) + "\n预计售出 " + sold + "，收入 ¥" + income + "\n\n";
            }
            return text + "预计总收入 ¥" + sum + "。\n\n医疗支援 " + game.State.support + "：提高药品需求。\n校园互助：" + (game.Has("school_helped") ? "已建立，罐头需求 +1。" : "尚未建立。");
        }
        private void Map()
        {
            var map = ui.Box(page, "Map", 30, 250, 660, 362, PrototypeUI.Hex(0x22383F));
            for (int i = 0; i < 7; i++) ui.Box(map, "Street", 0, 26 + i * 50, 660, 2, PrototypeUI.Hex(0x3B5459), false);
            for (int i = 0; i < 10; i++) ui.Box(map, "Street", 22 + i * 67, 0, 2, 362, PrototypeUI.Hex(0x3B5459), false);
            ui.Box(map, "Route", 90, 215, 490, 6, PrototypeUI.Gold, false);
            ui.Box(map, "Route", 320, 105, 6, 116, PrototypeUI.Gold, false);
            ui.Label(map, "封锁城区 / NODE MAP", 20, 14, 610, 35, 21, PrototypeUI.Muted);
            ui.Button(map, "便利店\n当前据点", 260, 72, 142, 85, () => Switch("store"), true, true, 21);
            for (int i = 0; i < game.Config.locations.Length; i++)
            {
                var location = game.Config.locations[i];
                bool available = location.events.Any(e => game.EventBlocked(e) == null);
                ui.Button(map, location.name, 18 + i * 214, 233, 196, 72, () => Location(location), true, available, 23);
                ui.Label(map, available ? "有可用行动" : "查看开放条件", 18 + i * 214, 318, 196, 25, 18, available ? PrototypeUI.Mint : PrototypeUI.Muted, TextAnchor.MiddleCenter);
            }
            ui.Label(page, "情报决定机会，时间决定代价", 32, 632, 656, 40, 27);
            for (int i = 0; i < game.Config.locations.Length; i++)
            {
                var location = game.Config.locations[i]; var item = location.events.FirstOrDefault(e => game.EventBlocked(e) == null) ?? location.events.FirstOrDefault(e => game.State.day >= e.minDay && game.State.day <= e.maxDay) ?? location.events[0];
                var r = ui.Box(page, "Location card", 30, 690 + i * 132, 660, 120, PrototypeUI.Panel);
                ui.Label(r, location.name + " · " + item.cost + " 时段", 18, 12, 452, 38, 25);
                string block = game.EventBlocked(item);
                ui.Label(r, item.title + " / " + (block ?? "可出发"), 18, 54, 452, 54, 20, block == null ? PrototypeUI.Mint : PrototypeUI.Muted);
                ui.Button(r, "查看", 500, 25, 142, 68, () => Location(location), true, block == null, 23);
            }
            ui.Label(page, "确认事件选项才扣时段；退出预览没有消耗。", 32, 1100, 656, 54, 21, PrototypeUI.Gold);
        }
        private void Location(LocationDefinition location)
        {
            Modal(); var card = Card(location.name, location.subtitle);
            ui.ScrollText(card, "注意：" + location.risk + "\n可获得：" + location.reward + "\n\n确认选项后才出发。每次行动只能选择一个结果，请先查看物资条件和剩余时段。", 24, 124, 624, 200, 24, PrototypeUI.Muted);
            for (int i = 0; i < location.events.Length; i++)
            {
                var item = location.events[i]; string blocked = game.EventBlocked(item); float y = 350 + i * 166;
                ui.Label(card, item.title + " · " + item.cost + " 时段", 24, y, 624, 35, 27);
                ui.Label(card, "第 " + item.minDay + (item.minDay == item.maxDay ? "" : "–" + item.maxDay) + " 天 / " + item.description, 24, y + 40, 624, 54, 20, PrototypeUI.Muted);
                ui.Button(card, blocked ?? "预览行动 →", 24, y + 97, 624, 58, () => OpenDialogue(item.dialogue, item), blocked == null, blocked == null, 22);
            }
            ui.Button(card, "返回地图", 24, 798, 624, 66, CloseModal);
        }
        private void Journal()
        {
            var r = ui.Box(page, "Journal", 30, 250, 660, 736, PrototypeUI.Panel);
            ui.Label(r, "情报与行动记录", 22, 18, 610, 44, 29);
            string text = "医疗支援 " + game.State.support + "  /  社区信任 " + game.State.trust + "\n\n";
            string[] ids = { "hospital_intel", "school_intel", "subway_intel", "school_helped", "truth", "route" };
            foreach (string id in ids) if (game.Has(id)) text += "• " + GameSession.FlagLabel(id) + "\n";
            text += "\n—— 最近行动 ——\n\n" + string.Join("\n\n", game.State.journal.AsEnumerable().Reverse());
            ui.ScrollText(r, text, 22, 80, 616, 626, 24);
            ui.Button(page, "保存进度", 30, 1010, 320, 70, () => { Save(); ShowInfo("进度保存", storageWarning ?? "当前进度已保存。下次打开自动继续。\n\n编辑器与微信使用各自的存储空间，进度不会跨平台同步。"); }, storageReady, true);
            ui.Button(page, "重新开始", 368, 1010, 322, 70, () => Confirm("重新开始", "将覆盖本机当前三日原型的进度，回到第 1 天。", NewGame));
            if (game.Finished) ui.Button(page, "查看三日结果", 30, 1102, 660, 64, ShowSettlement, true, true, 23);
            else ui.Label(page, storageWarning ?? "存档版本 1 · 每次有效操作后自动保存", 32, 1104, 656, 68, 20, storageWarning == null ? PrototypeUI.Muted : PrototypeUI.Gold);
        }
        private void Action(Func<string> action)
        {
            if (!ready) return;
            notice = action(); Save(); Render();
        }
        private void Modal()
        {
            typing = false; dialogue = null; dialogueText = null; choiceButtons = null; nextButton = null;
            modalActive = true; PrototypeUI.Clear(overlay); overlay.gameObject.SetActive(true); overlay.SetAsLastSibling();
            pageInput.interactable = pageInput.blocksRaycasts = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ui.Box(overlay, "Modal blocker", 0, 0, 720, 1280, PrototypeUI.Hex(0x030C10, .9f), false, true);
        }
        private RectTransform Card(string title, string subtitle = null)
        {
            var card = ui.Box(overlay, "Modal card", 24, 188, 672, 896, PrototypeUI.Panel);
            ui.Box(card, "Accent", 24, 24, 5, 51, PrototypeUI.Mint);
            ui.Label(card, title, 44, 24, 588, 52, 32);
            if (!string.IsNullOrEmpty(subtitle)) ui.Label(card, subtitle, 44, 82, 588, 40, 20, PrototypeUI.Muted);
            return card;
        }
        private void CloseModal()
        {
            typing = false; dialogue = null; modalActive = false; overlay.gameObject.SetActive(false); PrototypeUI.Clear(overlay);
            Render();
        }
        private void ShowInfo(string title, string text)
        {
            Modal(); var card = Card(title, "向上滑动可阅读全部内容");
            ui.ScrollText(card, text, 24, 145, 624, 614, 25);
            ui.Button(card, "知道了", 24, 798, 624, 66, CloseModal, true, true);
        }
        private void Confirm(string title, string text, UnityEngine.Events.UnityAction action)
        {
            Modal(); var card = Card(title); ui.ScrollText(card, text, 24, 145, 624, 590, 28);
            ui.Button(card, "取消", 24, 798, 302, 66, CloseModal);
            ui.Button(card, "确认", 342, 798, 306, 66, () => { modalActive = false; overlay.gameObject.SetActive(false); action(); }, true, true);
        }
        private void OpenDialogue(string id, ExplorationEvent exploration = null)
        {
            Modal(); dialogue = new DialogueRunner(game, game.Config.Dialogue(id), exploration); DialoguePage();
        }
        private void DialoguePage()
        {
            PrototypeUI.Clear(overlay); ui.Box(overlay, "Modal blocker", 0, 0, 720, 1280, PrototypeUI.Hex(0x030C10, .9f), false, true);
            var node = dialogue.Current; var card = Card(node.speaker, "点击「继续」显示全文 / 下一句 · 长文可滑动");
            ui.Box(card, "Portrait", 24, 144, 66, 78, PrototypeUI.Hex(0x426057));
            ui.Label(card, Portrait(node.speaker), 29, 158, 56, 56, 36, PrototypeUI.Gold, TextAnchor.MiddleCenter);
            fullLine = node.text; typeClock = 0; typing = true;
            dialogueText = ui.ScrollText(card, fullLine, 110, 144, 538, 362, 26); dialogueText.text = "";
            if (!string.IsNullOrEmpty(dialogue.Receipt)) ui.ScrollText(card, dialogue.Receipt, 24, 520, 624, 98, 22, PrototypeUI.Gold);
            var choices = node.choices ?? Array.Empty<DialogueChoice>(); choiceButtons = new Button[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i]; string blocked = dialogue.Blocked(choice);
                choiceButtons[i] = ui.Button(card, choice.text + (blocked == null ? "" : "\n" + blocked), 24, 630 + i * 91, 624, 80, () => Choose(choice), false, blocked == null, 23);
            }
            nextButton = ui.Button(card, "显示全文", 24, choices.Length > 0 ? 818 : 716, choices.Length > 0 ? 438 : 624, 58, Advance, true, false, 22);
            nextCaption = nextButton.GetComponentInChildren<Text>();
            if (choices.Length > 0) ui.Button(card, "退出", 476, 818, 172, 58, CloseModal, true, false, 22);
            else ui.Button(card, "结束阅读", 24, 798, 624, 66, CloseModal, true, false, 23);
        }
        private static string Portrait(string speaker)
        {
            if (speaker.Contains("小林")) return "林";
            if (speaker.Contains("老师")) return "许";
            if (speaker.Contains("广播")) return "播";
            if (speaker.Contains("医务")) return "医";
            return "探";
        }
        private void FinishTyping()
        {
            typing = false; dialogueText.text = fullLine;
            for (int i = 0; i < choiceButtons.Length; i++) choiceButtons[i].interactable = dialogue.Blocked(dialogue.Current.choices[i]) == null;
            bool hasChoices = choiceButtons.Length > 0;
            nextButton.interactable = !hasChoices;
            nextCaption.text = hasChoices ? "请选择行动" : string.IsNullOrEmpty(dialogue.Current.next) ? "阅读完毕 → 返回" : "继续 →";
        }
        private void Advance()
        {
            if (typing) { FinishTyping(); return; }
            dialogue.Advance();
            if (dialogue.Closed) CloseModal(); else DialoguePage();
        }
        private void Choose(DialogueChoice choice)
        {
            if (typing || dialogue == null) return;
            try
            {
                dialogue.Choose(choice); notice = dialogue.Receipt; Save();
                // Repeated pointer events cannot act on the previous choice after Current changes.
                if (dialogue.Closed) CloseModal(); else DialoguePage();
            }
            catch (InvalidOperationException e) { notice = e.Message; }
        }
        private void ShowSettlement()
        {
            Modal(); var last = game.State.history.Last();
            var card = Card(game.Finished ? "三日结算" : "第 " + game.State.day + " 天结束", game.Finished ? "你让街角的灯变成了什么？" : "所有行动时段已使用");
            string text = "今日营业收入 ¥" + last.revenue + "\n今日探索 " + last.exploration + " 次\n当前资金 ¥" + game.State.money + "\n社区信任 " + game.State.trust + " / 医疗支援 " + game.State.support + "\n\n";
            if (game.Finished)
            {
                text = game.Ending() + "\n\n—— 本次成果 ——\n\n" + text + "三日营业收入 ¥" + game.State.history.Sum(x => x.revenue) + "\n探索完成 " + game.State.history.Sum(x => x.exploration) + " / " + game.Config.locations.Sum(x => x.events.Length) + " 个事件\n\n";
                text += "库存：" + string.Join(" / ", game.Config.goods.Select(g => g.name + " " + game.Stock(g.id).count)) + "\n\n";
            }
            text += "—— 今日记录 ——\n\n" + last.notes;
            if (!game.Finished) text += "\n\n明日：" + game.Config.days[game.State.day].briefing;
            ui.ScrollText(card, text, 24, 144, 624, 620, 25);
            ui.Button(card, game.Finished ? "再开一局" : "开始第 " + (game.State.day + 1) + " 天", 24, 798, game.Finished ? 302 : 624, 66, () =>
            {
                if (game.Finished) NewGame();
                else { game.NextDay(); notice = game.Config.days[game.State.day - 1].briefing; Save(); modalActive = false; overlay.gameObject.SetActive(false); tab = "store"; Render(); }
            }, true, true);
            if (game.Finished) ui.Button(card, "回顾日志", 342, 798, 306, 66, () => { reviewingFinished = true; modalActive = false; overlay.gameObject.SetActive(false); tab = "journal"; Render(); });
        }
        private void NewGame()
        {
            game = new GameSession(game.Config); notice = "新的一局开始。先获取情报，再决定营业还是探索。";
            tab = "store"; modalActive = false; reviewingFinished = false; overlay.gameObject.SetActive(false); Save(); Render();
        }
        private void Save()
        {
            if (!storageReady || game == null || IsCapture) return;
            try { WriteStorage(StorageKey, game.Serialize()); storageWarning = null; }
            catch (Exception e) { storageWarning = "保存失败：" + e.Message; Debug.LogWarning("[Prototype] " + storageWarning); }
        }
        private string ReadSave()
        {
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            return WX.StorageGetStringSync(StorageKey, "");
#else
            return PlayerPrefs.GetString(StorageKey, "");
#endif
        }
        private static void WriteStorage(string key, string value)
        {
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            WX.StorageSetStringSync(key, value);
#else
            PlayerPrefs.SetString(key, value); PlayerPrefs.Save();
#endif
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() { Save(); }
        private static bool IsCapture
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Environment.GetCommandLineArgs().Contains("-prototypeCapture");
#else
                return false;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewForCapture(string view)
        {
            if (!IsCapture) throw new InvalidOperationException("Capture mode is required.");
            modalActive = false; overlay.gameObject.SetActive(false); tab = view;
            if (view == "dialogue") { tab = "store"; Render(); OpenDialogue("hospital_cache", game.Config.locations.SelectMany(x => x.events).Single(x => x.id == "hospital_cache")); FinishTyping(); }
            else if (view == "ending")
            {
                while (!game.Finished) { while (game.CanAct) game.Rest(); if (!game.Finished) game.NextDay(); }
                Render();
            }
            else Render();
        }
#endif
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
        private void OnWxHide(GeneralCallbackResult result) { Save(); }
        private void OnWxShow(OnShowListenerResult result) { lastSafe = new Rect(-1, -1, -1, -1); }
        private void OnDestroy()
        {
            if (!storageReady) return;
            WX.OffHide(OnWxHide); WX.OffShow(OnWxShow);
        }
#endif
    }
}
