using System;
using System.Linq;
using UnityEngine;

namespace ConvenienceStore.Prototype.Editor
{
    // Deterministic integration checks for transactions, opportunity cost and persistence.
    public static class PrototypeChecks
    {
        private static int count;
        private static void Check(bool valid, string name)
        { if (!valid) throw new InvalidOperationException("Prototype check failed: " + name); count++; }
        private static void Reject(Action action, string name)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
        private static DialogueChoice Choice(GameConfig c, string dialogue, string id)
        { return c.Dialogue(dialogue).nodes.SelectMany(n => n.choices ?? Array.Empty<DialogueChoice>()).Single(x => x.id == id); }
        private static ExplorationEvent Event(GameConfig c, string id)
        { return c.locations.SelectMany(x => x.events).Single(x => x.id == id); }
        public static void Run(GameConfig c)
        {
            count = 0; var g = new GameSession(c);
            Check(g.State.day == 1 && g.State.slots == 3 && g.State.money == 30, "initial state");
            g.Buy("food"); Check(g.Stock("food").count == 4 && g.State.money == 26 && g.State.slots == 3, "purchase has no time cost");
            g.ToggleShelf("medicine"); Check(!g.Stock("medicine").onShelf, "shelf capacity enforced");
            g.ToggleShelf("water"); g.ToggleShelf("medicine"); Check(g.Stock("medicine").onShelf && g.Stock("water").count == 3, "shelf swap preserves stock");
            int demand = g.Demand("food"); g.TogglePrice("food"); Check(g.Demand("food") == demand - 1 && g.Price("food") == 10, "price affects demand");
            g = new GameSession(c); string prior = g.Serialize(); g.Buy("medicine"); g.Buy("medicine"); g.Buy("medicine"); string poor = g.Serialize();
            g.Buy("medicine"); Check(g.Serialize() == poor, "unaffordable purchase is atomic");
            g = new GameSession(c); g.State.money = 100;
            for (int i = 0; i < c.dailyPurchaseLimit + 2; i++) g.Buy("water");
            Check(g.Stock("water").purchased == c.dailyPurchaseLimit, "daily supplier limit");
            g = new GameSession(c); var talk = new DialogueRunner(g, c.Dialogue("lin_intro"));
            Check(g.Serialize() == new GameSession(c).Serialize(), "opening dialogue costs nothing");
            talk.Advance(); var mapChoice = talk.Current.choices[0]; talk.Choose(mapChoice);
            Check(g.Has("hospital_intel") && g.State.slots == 3, "NPC intel unlock");
            string once = g.Serialize(); Reject(() => talk.Choose(mapChoice), "stale pointer rejects duplicate choice");
            Check(g.Serialize() == once, "duplicate reward leaves state unchanged");
            Check(new DialogueRunner(g, c.Dialogue("lin_intro")).Current.id == "repeat", "repeat NPC dialogue");
            var hospital = Event(c, "hospital_cache");
            var preview = new DialogueRunner(g, c.Dialogue(hospital.dialogue), hospital);
            Check(g.Serialize() == once, "exploration preview/cancel has no cost");
            preview.Choose(Choice(c, "hospital_cache", "hospital_doctor"));
            Check(g.State.slots == 1 && g.State.support == 1 && g.Stock("medicine").count == 2, "exploration commits effects and time together");
            Check(g.Demand("medicine") == 2, "medical support changes business");
            string visited = g.Serialize(); Reject(() => g.Choose(Choice(c, "hospital_cache", "hospital_supplies"), hospital), "visited event rejects alternative reward");
            Check(g.Serialize() == visited, "event replay atomic");
            g.Business(); Check(g.State.settled && g.State.history.Count == 1, "day ends when slots exhausted");
            string ended = g.Serialize(); g.Business(); Check(g.Serialize() == ended, "no action after settlement");
            string error; var restore = new GameSession(c); Check(restore.TryRestore(ended, out error) && restore.Serialize() == ended, "settled save roundtrip");
            Check(!restore.TryRestore("{}", out error) && restore.Serialize() == ended, "invalid save preserves session");
            Check(!restore.TryRestore(ended.Replace("\"version\":1", "\"version\":9"), out error), "future save rejected");
            g.NextDay(); Check(g.State.day == 2 && g.State.slots == 3 && g.State.support == 1, "next day preserves support");
            Check(g.EventBlocked(hospital) == "已完成", "completed event state retained");
            var fresh = new GameSession(c); fresh.Flag("hospital_intel"); fresh.Rest(); fresh.Rest();
            string noTime = fresh.Serialize(); Reject(() => fresh.Choose(Choice(c, "hospital_cache", "hospital_doctor"), hospital), "insufficient time rejects exploration");
            Check(fresh.Serialize() == noTime, "time rejection is atomic"); fresh.Rest(); fresh.NextDay();
            Check(fresh.EventBlocked(hospital) == "机会已过期", "intel window expires");
            g.Flag("school_intel"); g.Stock("food").count = 1;
            var aid = Event(c, "school_aid"); string beforeAid = g.Serialize();
            Reject(() => g.Choose(Choice(c, "school_aid", "school_give"), aid), "insufficient aid stock rejects");
            Check(g.Serialize() == beforeAid, "stock rejection is atomic");
            g.Stock("food").count = 2; g.Choose(Choice(c, "school_aid", "school_give"), aid);
            Check(g.Stock("food").count == 0 && g.State.trust == 3 && g.Demand("food") == 2, "aid consumes stock and boosts demand");
            g.Choose(Choice(c, "hospital_records", "hospital_truth"), Event(c, "hospital_records")); g.Rest(); g.NextDay();
            Check(g.State.day == 3 && g.EventBlocked(Event(c, "school_return")) == null, "prior aid opens next-day event");
            g.Choose(Choice(c, "radio_3", "radio_route"));
            g.Choose(Choice(c, "subway_route", "subway_find"), Event(c, "subway_route"));
            Check(g.EventBlocked(Event(c, "subway_convoy")) == null, "route unlocks followup event");
            g.Choose(Choice(c, "subway_convoy", "convoy_truth"), Event(c, "subway_convoy")); g.Business();
            Check(g.Finished && g.State.history.Count == 3 && g.Ending().StartsWith("灯火之外"), "complete three-day investigation ending");
            var reload = new GameSession(c); Check(reload.TryRestore(g.Serialize(), out error) && reload.Finished, "final save resumes ending");
            var trade = new GameSession(c);
            for (int d = 0; d < 3; d++) { while (trade.CanAct) trade.Business(); if (!trade.Finished) trade.NextDay(); }
            Check(trade.Finished && trade.Ending().StartsWith("仍然营业") && trade.State.history.Sum(x => x.revenue) > 0, "business-only route completes");
            var shelter = new GameSession(c); shelter.State.trust = 2;
            Check(shelter.Ending().StartsWith("街角的避难所"), "community ending");
            var locked = new GameSession(c); Check(locked.ChoiceBlocked(Choice(c, "subway_convoy", "convoy_truth")) != null, "conditional choice locks without intel");
            Debug.Log("[Prototype] " + count + " gameplay checks passed.");
        }
    }
}
