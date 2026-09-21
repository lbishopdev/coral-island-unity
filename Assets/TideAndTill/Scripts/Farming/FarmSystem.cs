using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideAndTill
{
    public sealed class FarmSystem : MonoBehaviour
    {
        private const int Columns = 7;
        private const int Rows = 5;
        private const float TileSize = 1.55f;
        private static readonly Vector3 FarmOrigin = new Vector3(-14.1f, 0f, -7.7f);

        private readonly List<Plot> plots = new List<Plot>();
        private GameState state;
        private Plot selected;
        private GameObject highlight;

        public void Initialize(GameState gameState)
        {
            state = gameState;
            BuildPlots();
            BuildHighlight();
            CreateShowcaseCrops();
        }

        private void BuildPlots()
        {
            for (int z = 0; z < Rows; z++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Vector3 p = FarmOrigin + new Vector3(x * TileSize, 0f, z * TileSize);
                    p.y = WorldBuilder.SampleHeight(p.x, p.z) + 0.07f;
                    GameObject tile = PrimitiveFactory.Cube($"Garden Plot {x + 1},{z + 1}", transform, p,
                        new Vector3(TileSize - 0.12f, 0.11f, TileSize - 0.12f), IslandMaterials.PlotGrass, false);
                    plots.Add(new Plot(x, z, tile));
                }
            }
        }

        private void BuildHighlight()
        {
            highlight = new GameObject("Active Plot");
            highlight.transform.SetParent(transform, false);
            float edge = TileSize * 0.5f;
            float thickness = 0.07f;
            PrimitiveFactory.Cube("North", highlight.transform, new Vector3(0f, 0f, edge), new Vector3(TileSize, 0.04f, thickness), IslandMaterials.Highlight, false);
            PrimitiveFactory.Cube("South", highlight.transform, new Vector3(0f, 0f, -edge), new Vector3(TileSize, 0.04f, thickness), IslandMaterials.Highlight, false);
            PrimitiveFactory.Cube("East", highlight.transform, new Vector3(edge, 0f, 0f), new Vector3(thickness, 0.04f, TileSize), IslandMaterials.Highlight, false);
            PrimitiveFactory.Cube("West", highlight.transform, new Vector3(-edge, 0f, 0f), new Vector3(thickness, 0.04f, TileSize), IslandMaterials.Highlight, false);
            highlight.SetActive(false);
        }

        private void CreateShowcaseCrops()
        {
            Plot sprout = plots[2 + Columns * 3];
            sprout.Tilled = true;
            sprout.Watered = true;
            sprout.GrowthStage = 1;
            Refresh(sprout);

            Plot ripe = plots[4 + Columns * 3];
            ripe.Tilled = true;
            ripe.Watered = false;
            ripe.GrowthStage = 3;
            Refresh(ripe);

            Plot tilled = plots[3 + Columns * 3];
            tilled.Tilled = true;
            Refresh(tilled);
        }

        public void SelectNearest(Vector3 worldPoint)
        {
            Plot nearest = null;
            float nearestDistance = 1.25f;
            foreach (Plot plot in plots)
            {
                float distance = Vector2.Distance(new Vector2(worldPoint.x, worldPoint.z),
                    new Vector2(plot.Object.transform.position.x, plot.Object.transform.position.z));
                if (distance < nearestDistance)
                {
                    nearest = plot;
                    nearestDistance = distance;
                }
            }

            selected = nearest;
            highlight.SetActive(selected != null);
            if (selected != null)
                highlight.transform.position = selected.Object.transform.position + Vector3.up * 0.12f;
        }

        public bool UseSelected(FarmTool tool)
        {
            if (selected == null)
            {
                state.ShowToast("Stand beside a garden plot to use that tool.");
                return false;
            }

            switch (tool)
            {
                case FarmTool.Hoe:
                    if (selected.Tilled)
                    {
                        state.ShowToast("This soil is already soft and ready for seeds.");
                        return false;
                    }
                    if (!state.SpendStamina(3f)) return false;
                    selected.Tilled = true;
                    state.MarkTilled();
                    state.ShowToast("Rich island soil! Now choose the seed pouch.");
                    Refresh(selected);
                    return true;

                case FarmTool.WateringCan:
                    if (!selected.Tilled)
                    {
                        state.ShowToast("Till the grass before watering a garden plot.");
                        return false;
                    }
                    if (selected.Watered)
                    {
                        state.ShowToast("This plot already glistens with water.");
                        return false;
                    }
                    if (state.Water <= 0)
                    {
                        state.ShowToast("The watering can is empty. Refill it at the stone well.");
                        return false;
                    }
                    if (!state.SpendStamina(1.5f) || !state.ConsumeWater()) return false;
                    selected.Watered = true;
                    Refresh(selected);
                    return true;

                case FarmTool.Seeds:
                    if (!selected.Tilled)
                    {
                        state.ShowToast("Moonmelon seeds need freshly tilled soil.");
                        return false;
                    }
                    if (selected.GrowthStage >= 0)
                    {
                        state.ShowToast("Something is already growing in this plot.");
                        return false;
                    }
                    if (state.Seeds <= 0)
                    {
                        state.ShowToast("No seeds left. Visit the coral market stall in town.");
                        return false;
                    }
                    if (!state.SpendStamina(1f) || !state.ConsumeSeed()) return false;
                    selected.GrowthStage = 0;
                    state.MarkPlanted();
                    Refresh(selected);
                    return true;

                case FarmTool.HarvestBasket:
                    if (selected.GrowthStage < 3)
                    {
                        state.ShowToast(selected.GrowthStage < 0 ? "There is nothing to harvest here." : "This moonmelon needs another watered morning.");
                        return false;
                    }
                    if (!state.SpendStamina(1f)) return false;
                    selected.GrowthStage = -1;
                    selected.Watered = false;
                    state.AddHarvest();
                    Refresh(selected);
                    return true;

                case FarmTool.Axe:
                    state.ShowToast("The axe clears driftwood and old stumps, not garden soil.");
                    return false;
                default:
                    return false;
            }
        }

        public void AdvanceDay(bool raining)
        {
            foreach (Plot plot in plots)
            {
                if (raining && plot.Tilled)
                    plot.Watered = true;

                if (plot.GrowthStage >= 0 && plot.GrowthStage < 3 && plot.Watered)
                    plot.GrowthStage++;

                // Rain remains visible in the soil during a rainy morning; otherwise the sun dries it.
                if (!raining)
                    plot.Watered = false;
                Refresh(plot);
            }
        }

        private void Refresh(Plot plot)
        {
            Renderer renderer = plot.Object.GetComponent<Renderer>();
            renderer.sharedMaterial = !plot.Tilled ? IslandMaterials.PlotGrass : plot.Watered ? IslandMaterials.WetSoil : IslandMaterials.Soil;

            if (plot.Plant != null)
                Destroy(plot.Plant);
            plot.Plant = null;
            if (plot.GrowthStage < 0) return;

            var plant = new GameObject("Moonmelon Plant");
            plant.transform.SetParent(transform, false);
            Vector3 basePosition = plot.Object.transform.position + Vector3.up * 0.10f;
            plant.transform.position = basePosition;
            plot.Plant = plant;

            float stemHeight = 0.18f + plot.GrowthStage * 0.19f;
            PrimitiveFactory.Cylinder("Stem", plant.transform, new Vector3(0f, stemHeight * 0.5f, 0f),
                new Vector3(0.045f, stemHeight * 0.5f, 0.045f), IslandMaterials.Stem, false);

            int leafCount = 2 + plot.GrowthStage * 2;
            for (int i = 0; i < leafCount; i++)
            {
                float angle = i / (float)leafCount * Mathf.PI * 2f;
                float radius = 0.16f + plot.GrowthStage * 0.07f;
                GameObject leaf = PrimitiveFactory.Sphere("Leaf", plant.transform,
                    new Vector3(Mathf.Sin(angle) * radius, 0.12f + (i % 2) * 0.16f, Mathf.Cos(angle) * radius),
                    new Vector3(0.10f + plot.GrowthStage * 0.025f, 0.035f, 0.22f + plot.GrowthStage * 0.035f), IslandMaterials.Stem, false);
                leaf.transform.localRotation = Quaternion.Euler(12f, angle * Mathf.Rad2Deg, 0f);
            }

            if (plot.GrowthStage >= 2)
            {
                Material fruitMaterial = plot.GrowthStage >= 3 ? IslandMaterials.CropGold : IslandMaterials.Mint;
                int fruits = plot.GrowthStage >= 3 ? 3 : 1;
                for (int i = 0; i < fruits; i++)
                {
                    float angle = i / (float)fruits * Mathf.PI * 2f + 0.6f;
                    PrimitiveFactory.Sphere("Moonmelon", plant.transform,
                        new Vector3(Mathf.Sin(angle) * 0.25f, 0.28f + (i % 2) * 0.13f, Mathf.Cos(angle) * 0.25f),
                        new Vector3(0.22f, 0.27f, 0.22f), fruitMaterial, false);
                }
            }
        }

        [Serializable]
        private sealed class Plot
        {
            public readonly int X;
            public readonly int Z;
            public readonly GameObject Object;
            public bool Tilled;
            public bool Watered;
            public int GrowthStage = -1;
            public GameObject Plant;

            public Plot(int x, int z, GameObject item)
            {
                X = x;
                Z = z;
                Object = item;
            }
        }
    }
}
