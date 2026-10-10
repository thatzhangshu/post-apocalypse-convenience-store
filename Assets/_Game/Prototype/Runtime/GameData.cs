using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConvenienceStore.Prototype
{
    [Serializable] public sealed class GoodDefinition
    {
        public string id, name, description;
        public int buyPrice, sellPrice, initialStock;
    }
    [Serializable] public sealed class DayDefinition
    {
        public string title, weather, briefing, radioDialogue;
        public int[] demand;
    }
    [Serializable] public sealed class Effect { public string type, id; public int amount; }
    [Serializable] public sealed class DialogueChoice
    {
        public string id, text, next, requiredFlag, forbiddenFlag;
        public Effect[] effects;
    }
    [Serializable] public sealed class DialogueNode
    {
        public string id, speaker, text, next;
        public DialogueChoice[] choices;
    }
    [Serializable] public sealed class DialogueDefinition
    {
        public string id, entry, repeatEntry, completedFlag;
        public DialogueNode[] nodes;
        public DialogueNode Node(string id) { return nodes.First(x => x.id == id); }
    }
    [Serializable] public sealed class NpcDefinition { public string id, name, role, dialogue; }
    [Serializable] public sealed class ExplorationEvent
    {
        public string id, title, description, dialogue, requiredFlag;
        public int minDay, maxDay, cost;
    }
    [Serializable] public sealed class LocationDefinition
    {
        public string id, name, subtitle, risk, reward;
        public ExplorationEvent[] events;
    }
    [Serializable] public sealed class GameConfig
    {
        public int version, startingMoney, slotsPerDay, shelfCapacity, dailyPurchaseLimit;
        public string title;
        public GoodDefinition[] goods;
        public DayDefinition[] days;
        public NpcDefinition[] npcs;
        public DialogueDefinition[] dialogues;
        public LocationDefinition[] locations;

        public GoodDefinition Good(string id) { return goods.First(x => x.id == id); }
        public DialogueDefinition Dialogue(string id) { return dialogues.First(x => x.id == id); }
        public static GameConfig Parse(string json)
        {
            var config = JsonUtility.FromJson<GameConfig>(json);
            if (config == null) throw new FormatException("Missing prototype config.");
            config.Validate();
            return config;
        }
        public void Validate()
        {
            Require(version == 1 && startingMoney >= 0 && slotsPerDay > 0 && shelfCapacity > 0 && dailyPurchaseLimit > 0, "Invalid settings.");
            Require(!string.IsNullOrWhiteSpace(title) && goods != null && goods.Length >= shelfCapacity && days != null && days.Length > 0, "Missing goods/days.");
            Unique(goods.Select(x => x.id), "goods");
            foreach (var good in goods) Require(good.buyPrice > 0 && good.sellPrice >= good.buyPrice && good.initialStock >= 0, "Invalid good: " + good.id);
            Require(dialogues != null && npcs != null && locations != null, "Missing content arrays.");
            Unique(dialogues.Select(x => x.id), "dialogues");
            Unique(npcs.Select(x => x.id), "NPCs");
            Unique(locations.Select(x => x.id), "locations");
            foreach (var day in days)
            {
                Require(day.demand != null && day.demand.Length == goods.Length && day.demand.All(x => x >= 0), "Invalid day demand.");
                Require(dialogues.Any(x => x.id == day.radioDialogue), "Missing radio dialogue.");
            }
            var choices = new List<string>();
            foreach (var dialogue in dialogues)
            {
                Require(dialogue.nodes != null && dialogue.nodes.Length > 0, "Empty dialogue.");
                Unique(dialogue.nodes.Select(x => x.id), "nodes in " + dialogue.id);
                Require(dialogue.nodes.Any(x => x.id == dialogue.entry), "Missing entry: " + dialogue.id);
                if (!string.IsNullOrEmpty(dialogue.repeatEntry)) Require(dialogue.nodes.Any(x => x.id == dialogue.repeatEntry), "Missing repeat entry.");
                foreach (var node in dialogue.nodes)
                {
                    Require(!string.IsNullOrEmpty(node.text), "Empty dialogue text.");
                    CheckNext(dialogue, node.next);
                    Require(string.IsNullOrEmpty(node.next) || node.choices == null || node.choices.Length == 0, "Node cannot have next and choices.");
                    foreach (var choice in node.choices ?? Array.Empty<DialogueChoice>())
                    {
                        choices.Add(choice.id);
                        CheckNext(dialogue, choice.next);
                        Require(!string.IsNullOrEmpty(choice.text), "Empty choice.");
                        foreach (var effect in choice.effects ?? Array.Empty<Effect>())
                        {
                            Require(new[] { "money", "stock", "flag", "trust", "support" }.Contains(effect.type), "Unknown effect: " + effect.type);
                            if (effect.type == "stock") Require(goods.Any(x => x.id == effect.id), "Unknown good: " + effect.id);
                            if (effect.type == "flag") Require(!string.IsNullOrEmpty(effect.id), "Empty flag.");
                            if (effect.type == "support" || effect.type == "trust") Require(effect.amount >= 0, "Negative support/trust.");
                        }
                    }
                }
            }
            Unique(choices, "global choices");
            foreach (var npc in npcs) Require(dialogues.Any(x => x.id == npc.dialogue), "Missing NPC dialogue.");
            foreach (var location in locations) Require(location.events != null && location.events.Length > 0, "Empty location.");
            Unique(locations.SelectMany(x => x.events).Select(x => x.id), "events");
            foreach (var location in locations)
            {
                Require(location.events != null && location.events.Length > 0, "Empty location.");
                foreach (var item in location.events)
                {
                    Require(item.minDay >= 1 && item.maxDay >= item.minDay && item.maxDay <= days.Length && item.cost > 0 && item.cost <= slotsPerDay, "Invalid event: " + item.id);
                    Require(dialogues.Any(x => x.id == item.dialogue), "Missing event dialogue.");
                    var start = Dialogue(item.dialogue).Node(Dialogue(item.dialogue).entry);
                    Require(start.choices != null && start.choices.Length > 0, "Exploration entry must offer choices.");
                }
            }
        }
        private static void CheckNext(DialogueDefinition d, string id)
        { if (!string.IsNullOrEmpty(id)) Require(d.nodes.Any(x => x.id == id), "Missing node: " + id); }
        private static void Unique(IEnumerable<string> ids, string label)
        {
            var list = ids.ToArray();
            Require(list.All(x => !string.IsNullOrWhiteSpace(x)) && list.Distinct().Count() == list.Length, "Duplicate/empty IDs: " + label);
        }
        private static void Require(bool valid, string error) { if (!valid) throw new FormatException(error); }
    }
}
