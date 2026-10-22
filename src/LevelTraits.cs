using System.Collections.Generic;

namespace PPBTool
{
    /// <summary>Read-only snapshot of mechanics that belong to the current level.</summary>
    internal sealed class LevelTraits
    {
        public IReadOnlyList<World.Attempt> AttemptOrder;
        public bool ShowAttemptOrder;
        public bool Shed;
        public bool InnerPush;

        public static LevelTraits Current()
        {
            var traits = new LevelTraits
            {
                ShowAttemptOrder = World.showAttemptOrder,
                Shed = World.shedEnabled,
                InnerPush = World.innerPushEnabled,
            };

            var order = new List<World.Attempt>();
            if (World.attemptOrder != null)
            {
                order.AddRange(World.attemptOrder);
            }

            traits.AttemptOrder = order;
            return traits;
        }

        public bool HasNonDefaultMechanics
        {
            get { return ShowAttemptOrder || Shed || InnerPush; }
        }

        public string Compact()
        {
            var parts = new List<string>();
            if (ShowAttemptOrder)
            {
                var actions = new List<string>();
                if (AttemptOrder != null)
                {
                    foreach (World.Attempt action in AttemptOrder)
                    {
                        actions.Add(ActionLabel(action));
                    }
                }

                if (actions.Count > 0)
                {
                    parts.Add(UiText.Get("priority") + ": " + string.Join(" → ", actions.ToArray()));
                }
            }

            if (InnerPush)
            {
                parts.Add(UiText.Get("inner_push"));
            }

            if (Shed)
            {
                parts.Add(UiText.Get("shed"));
            }

            return string.Join("    ", parts.ToArray());
        }

        private static string ActionLabel(World.Attempt action)
        {
            switch (action)
            {
                case World.Attempt.Push: return UiText.Get("action_push");
                case World.Attempt.Enter: return UiText.Get("action_enter");
                case World.Attempt.Eat: return UiText.Get("action_eat");
                case World.Attempt.Possess: return UiText.Get("action_possess");
                default: return action.ToString();
            }
        }
    }
}
