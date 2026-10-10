using System;

namespace ConvenienceStore.Prototype
{
    // A reusable graph runner; presentation (typing, scrolling, portraits) stays in the UI.
    public sealed class DialogueRunner
    {
        private readonly GameSession session;
        private readonly DialogueDefinition definition;
        private ExplorationEvent exploration;
        public DialogueNode Current { get; private set; }
        public string Receipt { get; private set; }
        public bool Closed { get; private set; }
        public bool CanCancel { get { return exploration != null || Current.id == definition.entry || Closed; } }
        public DialogueRunner(GameSession session, DialogueDefinition definition, ExplorationEvent exploration = null)
        {
            this.session = session; this.definition = definition; this.exploration = exploration;
            string entry = !string.IsNullOrEmpty(definition.completedFlag) && session.Has(definition.completedFlag) && !string.IsNullOrEmpty(definition.repeatEntry)
                ? definition.repeatEntry : definition.entry;
            Current = definition.Node(entry);
        }
        public string Blocked(DialogueChoice choice)
        { return exploration == null ? session.ChoiceBlocked(choice) : session.EventBlocked(exploration) ?? session.ChoiceBlocked(choice); }
        public void Choose(DialogueChoice choice)
        {
            if (Closed || Array.IndexOf(Current.choices ?? Array.Empty<DialogueChoice>(), choice) < 0) throw new InvalidOperationException("Invalid active choice.");
            Receipt = session.Choose(choice, exploration); exploration = null;
            Move(choice.next);
        }
        public void Advance()
        {
            if (Closed || (Current.choices != null && Current.choices.Length > 0)) return;
            Move(Current.next);
        }
        private void Move(string next)
        {
            if (string.IsNullOrEmpty(next)) { Closed = true; return; }
            Current = definition.Node(next);
        }
    }
}
