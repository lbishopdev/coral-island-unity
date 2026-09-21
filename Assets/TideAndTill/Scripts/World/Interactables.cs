using System.Collections.Generic;
using UnityEngine;

namespace TideAndTill
{
    public abstract class WorldInteractable : MonoBehaviour
    {
        private static readonly List<WorldInteractable> Registry = new List<WorldInteractable>();
        public abstract string Prompt { get; }

        protected virtual void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        protected virtual void OnDisable() => Registry.Remove(this);

        public abstract void Interact(PlayerController player);

        public static WorldInteractable FindNearest(Vector3 position, float maxDistance)
        {
            WorldInteractable nearest = null;
            float best = maxDistance * maxDistance;
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                WorldInteractable item = Registry[i];
                if (item == null)
                {
                    Registry.RemoveAt(i);
                    continue;
                }
                float distance = (item.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = item;
                }
            }
            return nearest;
        }

        public static T FindNearest<T>(Vector3 position, float maxDistance) where T : WorldInteractable
        {
            T nearest = null;
            float best = maxDistance * maxDistance;
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                if (!(Registry[i] is T item) || item == null) continue;
                float distance = (item.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = item;
                }
            }
            return nearest;
        }
    }

    public sealed class WellInteractable : WorldInteractable
    {
        public override string Prompt => "E  •  Refill watering can";
        public override void Interact(PlayerController player) => IslandGame.Instance.State.RefillWater();
    }

    public sealed class ShippingCrateInteractable : WorldInteractable
    {
        public override string Prompt => "E  •  Ship harvested produce";
        public override void Interact(PlayerController player) => IslandGame.Instance.State.ShipProduce();
    }

    public sealed class MarketInteractable : WorldInteractable
    {
        public override string Prompt => "E  •  Buy 5 seeds  ·  40 shells";
        public override void Interact(PlayerController player) => IslandGame.Instance.State.BuySeeds();
    }

    public sealed class BedInteractable : WorldInteractable
    {
        public override string Prompt => "E  •  Sleep until a new morning";
        public override void Interact(PlayerController player) => IslandGame.Instance.BeginNextDay();
    }

    public sealed class VillagerInteractable : WorldInteractable
    {
        private string villagerName = "Islander";
        private string[] lines = { "What a beautiful day on Sunpetal Island." };
        private int line;

        public override string Prompt => $"E  •  Talk to {villagerName}";

        public VillagerInteractable SetVillager(string newName, string[] dialogue)
        {
            villagerName = newName;
            if (dialogue != null && dialogue.Length > 0) lines = dialogue;
            return this;
        }

        public override void Interact(PlayerController player)
        {
            IslandGame.Instance.State.ShowToast($"{villagerName}: “{lines[line]}”");
            line = (line + 1) % lines.Length;
        }
    }

    public sealed class ChoppableInteractable : WorldInteractable
    {
        private int hitsRemaining = 3;
        public override string Prompt => "Equip axe + Space  •  Clear old stump";

        public override void Interact(PlayerController player)
        {
            IslandGame.Instance.State.ShowToast("Select the axe, then press Space to chop this stump.");
        }

        public bool Chop(GameState state)
        {
            if (!state.SpendStamina(4f)) return false;
            hitsRemaining--;
            if (hitsRemaining > 0)
            {
                state.ShowToast($"Solid driftwood — {hitsRemaining} more swing{(hitsRemaining == 1 ? "" : "s")}.");
                transform.localScale *= 0.88f;
            }
            else
            {
                state.ShowToast("Stump cleared! The farm feels a little more open.");
                gameObject.SetActive(false);
            }
            return true;
        }
    }
}
