using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConvenienceStore.Prototype
{
    [Serializable] public sealed class StockState { public string id; public int count, purchased; public bool onShelf, premium; }
    [Serializable] public sealed class DayRecord { public int day, revenue, exploration, closingMoney; public string notes; }
    [Serializable] public sealed class GameState
    {
        public int version = 1, day = 1, slots, money, trust, support, revenue, exploration;
        public bool settled;
        public List<StockState> stock = new List<StockState>();
        public List<string> flags = new List<string>();
        public List<string> journal = new List<string>();
        public List<DayRecord> history = new List<DayRecord>();
    }

    // All mutations live here. UI and dialogue read the same state; preparations cost no time.
    public sealed class GameSession
    {
        public GameConfig Config { get; private set; }
        public GameState State { get; private set; }
        public bool Finished { get { return State.settled && State.day == Config.days.Length; } }
        public bool CanAct { get { return !State.settled && State.slots > 0; } }
        public GameSession(GameConfig config)
        {
            Config = config;
            State = new GameState { slots = config.slotsPerDay, money = config.startingMoney };
            for (int i = 0; i < config.goods.Length; i++)
                State.stock.Add(new StockState { id = config.goods[i].id, count = config.goods[i].initialStock, onShelf = i < config.shelfCapacity });
            Note("开店第 1 天。阅读情报、进货和配置货架不消耗时段。");
        }
        public StockState Stock(string id) { return State.stock.First(x => x.id == id); }
        public bool Has(string flag) { return string.IsNullOrEmpty(flag) || State.flags.Contains(flag); }
        public void Flag(string flag) { if (!string.IsNullOrEmpty(flag) && !Has(flag)) State.flags.Add(flag); }
        public void Note(string note) { State.journal.Add("D" + State.day + " · " + note); }
        public string Buy(string id)
        {
            var good = Config.Good(id); var stock = Stock(id);
            if (!CanAct) return "今天已结束，请先查看日结。";
            if (stock.purchased >= Config.dailyPurchaseLimit) return "今日这类商品的供应已用完。";
            if (State.money < good.buyPrice) return "资金不足，无法进货。";
            State.money -= good.buyPrice; stock.count++; stock.purchased++;
            return good.name + " +1，花费 ¥" + good.buyPrice + "（不消耗时段）";
        }
        public string ToggleShelf(string id)
        {
            if (!CanAct) return "今天已结束。";
            var stock = Stock(id);
            if (!stock.onShelf && State.stock.Count(x => x.onShelf) >= Config.shelfCapacity) return "货架已满，请先撤下一类商品。";
            stock.onShelf = !stock.onShelf;
            return Config.Good(id).name + (stock.onShelf ? "已上架。" : "已撤下，仍保留库存。");
        }
        public string TogglePrice(string id)
        {
            if (!CanAct) return "今天已结束。";
            Stock(id).premium = !Stock(id).premium;
            return "已调整售价；高价会减少需求。";
        }
        public int Price(string id) { return Config.Good(id).sellPrice + (Stock(id).premium ? 2 : 0); }
        public int Demand(string id)
        {
            int index = Array.FindIndex(Config.goods, x => x.id == id);
            int value = Config.days[State.day - 1].demand[index];
            if (id == "medicine") value += State.support;
            if (id == "food" && Has("school_helped")) value++;
            if (Stock(id).premium) value = Mathf.Max(0, value - 1);
            return value;
        }
        public string Business()
        {
            if (!CanAct) return "没有可用营业时段。";
            int income = 0; var lines = new List<string>();
            foreach (var good in Config.goods)
            {
                var stock = Stock(good.id);
                int sold = stock.onShelf ? Mathf.Min(stock.count, Demand(good.id)) : 0;
                stock.count -= sold; income += sold * Price(good.id);
                lines.Add(good.name + "售出 " + sold);
            }
            State.money += income; State.revenue += income;
            string report = "营业 1 时段：" + string.Join("，", lines) + "；收入 ¥" + income + "。";
            Note(report); Spend(1); return report;
        }
        public string Rest()
        {
            if (!CanAct) return "没有可用时段。";
            string report = "休整 1 时段，没有获得营业收入。";
            Note(report); Spend(1); return report;
        }
        private void Spend(int slots)
        {
            State.slots -= slots;
            if (State.slots == 0)
            {
                State.settled = true;
                State.history.Add(new DayRecord { day = State.day, revenue = State.revenue, exploration = State.exploration, closingMoney = State.money,
                    notes = string.Join("\n", State.journal.Where(x => x.StartsWith("D" + State.day + " · "))) });
            }
        }
        public bool NextDay()
        {
            if (!State.settled || Finished) return false;
            State.day++; State.slots = Config.slotsPerDay; State.settled = false; State.revenue = 0; State.exploration = 0;
            foreach (var stock in State.stock) stock.purchased = 0;
            Note(Config.days[State.day - 1].briefing); return true;
        }
        public string EventBlocked(ExplorationEvent item)
        {
            if (Has("event:" + item.id)) return "已完成";
            if (State.day > item.maxDay) return "机会已过期";
            if (State.day < item.minDay) return "第 " + item.minDay + " 天开放";
            if (!Has(item.requiredFlag)) return "需要相关情报";
            if (!CanAct || State.slots < item.cost) return "剩余时段不足";
            return null;
        }
        public string ChoiceBlocked(DialogueChoice choice)
        {
            if (Has("choice:" + choice.id)) return "已选择过";
            if (!Has(choice.requiredFlag)) return "需要相关线索";
            if (!string.IsNullOrEmpty(choice.forbiddenFlag) && Has(choice.forbiddenFlag)) return "条件已改变";
            int money = State.money;
            var stocks = State.stock.ToDictionary(x => x.id, x => x.count);
            foreach (var effect in choice.effects ?? Array.Empty<Effect>())
            {
                if (effect.type == "money") money += effect.amount;
                if (effect.type == "stock") stocks[effect.id] += effect.amount;
            }
            if (money < 0) return "资金不足";
            if (stocks.Any(x => x.Value < 0)) return "所需物资不足";
            return null;
        }
        public string Choose(DialogueChoice choice, ExplorationEvent exploration = null)
        {
            string blocked = exploration == null ? null : EventBlocked(exploration);
            if (blocked == null) blocked = ChoiceBlocked(choice);
            if (blocked != null) throw new InvalidOperationException(blocked);
            foreach (var effect in choice.effects ?? Array.Empty<Effect>())
            {
                switch (effect.type)
                {
                    case "money": State.money += effect.amount; break;
                    case "stock": Stock(effect.id).count += effect.amount; break;
                    case "flag": Flag(effect.id); break;
                    case "trust": State.trust += effect.amount; break;
                    case "support": State.support += effect.amount; break;
                }
            }
            Flag("choice:" + choice.id);
            string receipt = EffectText(choice.effects);
            Note((exploration == null ? "对话选择" : exploration.title) + "：" + choice.text + "。" + receipt);
            if (exploration != null)
            {
                Flag("event:" + exploration.id); State.exploration++;
                Spend(exploration.cost);
                receipt = "消耗 " + exploration.cost + " 时段。" + receipt;
            }
            return receipt;
        }
        public string EffectText(Effect[] effects)
        {
            var parts = new List<string>();
            foreach (var effect in effects ?? Array.Empty<Effect>())
            {
                string signed = effect.amount >= 0 ? "+" + effect.amount : effect.amount.ToString();
                switch (effect.type)
                {
                    case "stock": parts.Add(Config.Good(effect.id).name + " " + signed); break;
                    case "money": parts.Add("资金 " + signed); break;
                    case "support": parts.Add("医疗支援 " + signed + "（药品需求提升）"); break;
                    case "trust": parts.Add("社区信任 " + signed); break;
                    case "flag": parts.Add(FlagLabel(effect.id)); break;
                }
            }
            return string.Join("；", parts);
        }
        public static string FlagLabel(string id)
        {
            switch (id)
            {
                case "hospital_intel": return "情报：医院药房今日仍可进入";
                case "school_intel": return "情报：校园避难点第 2 天开放";
                case "subway_intel": return "情报：地铁撤离点第 3 天开放";
                case "school_helped": return "校园互助建立（食品需求 +1）";
                case "truth": return "获得封锁真相线索";
                case "route": return "找到安全撤离路线";
                default: return "线索已记录";
            }
        }
        public string Ending()
        {
            if (Has("truth") && Has("route")) return "灯火之外\n你拼出了封锁的真相，也为街区找到了撤离路线。便利店成为了人们出发前的最后一站。";
            if (State.trust >= 2 || State.support > 0) return "街角的避难所\n你没能查清所有秘密，但物资与援手让这间便利店成为值得信任的据点。";
            return "仍然营业\n你保住了店铺的生意。城市的秘密仍在门外，下一次或许可以留一点时间去寻找答案。";
        }
        public string Serialize() { return JsonUtility.ToJson(State); }
        public bool TryRestore(string json, out string error)
        {
            error = null;
            try
            {
                var s = JsonUtility.FromJson<GameState>(json);
                if (s == null || s.version != 1 || s.day < 1 || s.day > Config.days.Length || s.slots < 0 || s.slots > Config.slotsPerDay || s.money < 0 ||
                    s.trust < 0 || s.support < 0 || s.revenue < 0 || s.exploration < 0 || s.settled != (s.slots == 0) ||
                    s.stock == null || s.flags == null || s.journal == null || s.history == null || s.stock.Count != Config.goods.Length ||
                    s.history.Count != s.day - 1 + (s.settled ? 1 : 0) || s.stock.Any(x => x == null || x.count < 0 || x.purchased < 0 || x.purchased > Config.dailyPurchaseLimit) ||
                    s.stock.Select(x => x.id).Distinct().Count() != Config.goods.Length || Config.goods.Any(g => !s.stock.Any(x => x.id == g.id)) ||
                    s.stock.Count(x => x.onShelf) > Config.shelfCapacity || s.flags.Any(x => x == null) || s.journal.Any(x => x == null))
                    throw new FormatException("存档数据不完整或版本不匹配。");
                for (int i = 0; i < s.history.Count; i++) if (s.history[i] == null || s.history[i].day != i + 1) throw new FormatException("日结记录无效。");
                State = s; return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }
    }
}
