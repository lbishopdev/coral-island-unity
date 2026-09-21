using System;
using UnityEngine;

namespace TideAndTill
{
    public enum FarmTool
    {
        Hoe,
        WateringCan,
        Seeds,
        HarvestBasket,
        Axe
    }

    /// <summary>A small, UI-friendly state model for inventory and player progression.</summary>
    public sealed class GameState
    {
        public event Action Changed;
        public event Action<string> ToastRequested;

        public int Coins { get; private set; } = 420;
        public int Seeds { get; private set; } = 12;
        public int Produce { get; private set; }
        public int Water { get; private set; } = 8;
        public int MaxWater => 8;
        public float Stamina { get; private set; } = 100f;
        public int MaxStamina => 100;
        public FarmTool SelectedTool { get; private set; } = FarmTool.Hoe;

        public int TilledCount { get; private set; }
        public int PlantedCount { get; private set; }
        public int HarvestedCount { get; private set; }

        public string QuestText
        {
            get
            {
                if (TilledCount == 0) return "FIRST SPROUT  •  Till one garden plot";
                if (PlantedCount == 0) return "FIRST SPROUT  •  Plant moonmelon seeds";
                if (HarvestedCount == 0) return "FIRST SPROUT  •  Harvest a ripe crop";
                return "FIRST SPROUT  •  Complete! Visit Maris in town";
            }
        }

        public void SelectTool(FarmTool tool)
        {
            if (SelectedTool == tool) return;
            SelectedTool = tool;
            Changed?.Invoke();
        }

        public void CycleTool(int direction)
        {
            int count = Enum.GetValues(typeof(FarmTool)).Length;
            int next = ((int)SelectedTool + direction + count) % count;
            SelectTool((FarmTool)next);
        }

        public bool SpendStamina(float amount)
        {
            if (Stamina < amount)
            {
                ShowToast("You're worn out. Rest in the cottage to start a new day.");
                return false;
            }

            Stamina = Mathf.Max(0f, Stamina - amount);
            Changed?.Invoke();
            return true;
        }

        public void RegenerateStamina(float amount)
        {
            float next = Mathf.Min(MaxStamina, Stamina + amount);
            if (Mathf.Approximately(next, Stamina)) return;
            Stamina = next;
            Changed?.Invoke();
        }

        public bool ConsumeSeed()
        {
            if (Seeds <= 0)
            {
                ShowToast("No moonmelon seeds left. The market sells fresh packets.");
                return false;
            }
            Seeds--;
            Changed?.Invoke();
            return true;
        }

        public bool ConsumeWater()
        {
            if (Water <= 0)
            {
                ShowToast("The watering can is empty. Refill it at the stone well.");
                return false;
            }
            Water--;
            Changed?.Invoke();
            return true;
        }

        public void RefillWater()
        {
            Water = MaxWater;
            Changed?.Invoke();
            ShowToast("Watering can refilled with cool spring water.");
        }

        public void BuySeeds()
        {
            const int price = 40;
            if (Coins < price)
            {
                ShowToast("You need 40 shells for a seed packet.");
                return;
            }
            Coins -= price;
            Seeds += 5;
            Changed?.Invoke();
            ShowToast("Bought 5 moonmelon seeds for 40 shells.");
        }

        public void AddHarvest()
        {
            Produce++;
            HarvestedCount++;
            Changed?.Invoke();
            ShowToast("Moonmelon harvested! Ship it from the farm crate.");
        }

        public void ShipProduce()
        {
            if (Produce <= 0)
            {
                ShowToast("The shipping crate is empty. Ripe moonmelons go here.");
                return;
            }
            int earnings = Produce * 65;
            int shipped = Produce;
            Produce = 0;
            Coins += earnings;
            Changed?.Invoke();
            ShowToast($"Shipped {shipped} moonmelon{(shipped == 1 ? "" : "s")} for {earnings} shells!");
        }

        public void MarkTilled()
        {
            TilledCount++;
            Changed?.Invoke();
        }

        public void MarkPlanted()
        {
            PlantedCount++;
            Changed?.Invoke();
        }

        public void RestoreForNewDay()
        {
            Stamina = MaxStamina;
            Changed?.Invoke();
        }

        public void ShowToast(string message) => ToastRequested?.Invoke(message);

        internal void Restore(int coins, int seeds, int produce, int water, float stamina)
        {
            Coins = Mathf.Max(0, coins);
            Seeds = Mathf.Max(0, seeds);
            Produce = Mathf.Max(0, produce);
            Water = Mathf.Clamp(water, 0, MaxWater);
            Stamina = Mathf.Clamp(stamina, 0f, MaxStamina);
            Changed?.Invoke();
        }
    }
}
