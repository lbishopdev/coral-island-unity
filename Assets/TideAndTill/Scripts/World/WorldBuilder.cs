using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TideAndTill
{
    /// <summary>
    /// Builds an original tropical farm-and-town playground from lightweight procedural meshes.
    /// The prototype deliberately has no dependency on third-party or copied game assets.
    /// </summary>
    public static class WorldBuilder
    {
        public const float IslandWidth = 88f;
        public const float IslandDepth = 68f;

        public static void Build(Transform root)
        {
            IslandMaterials.Initialize();
            BuildOcean(root);
            BuildIsland(root);
            BuildPaths(root);
            BuildFarmstead(root);
            BuildVillage(root);
            BuildHarbor(root);
            BuildLighthouse(root);
            BuildNature(root);
            BuildLighting();
        }

        public static float SampleHeight(float x, float z)
        {
            float nx = x / (IslandWidth * 0.5f);
            float nz = z / (IslandDepth * 0.5f);
            float ellipse = Mathf.Sqrt(nx * nx + nz * nz);
            float shoreBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1.03f - ellipse) / 0.13f));
            float shore = Mathf.Lerp(-1.6f, 0.15f, shoreBlend);
            float noise = (Mathf.PerlinNoise(x * 0.055f + 12.7f, z * 0.055f + 4.1f) - 0.5f) * 0.85f;
            float broadHill = Mathf.Exp(-((x + 25f) * (x + 25f) + (z - 16f) * (z - 16f)) / 360f) * 2.2f;
            float height = shore + noise * Mathf.Clamp01((1.02f - ellipse) * 4f) + broadHill;

            // The working farm and village square stay comfortably walkable.
            float farmBlend = SmoothBox(x, z, -8f, -1f, 16f, 15f, 5f);
            height = Mathf.Lerp(height, 0.32f, farmBlend * 0.94f);
            float townBlend = SmoothBox(x, z, 23f, 5f, 14f, 12f, 4f);
            height = Mathf.Lerp(height, 0.48f, townBlend * 0.88f);
            return height;
        }

        private static float SmoothBox(float x, float z, float cx, float cz, float halfX, float halfZ, float feather)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - halfX);
            float dz = Mathf.Max(0f, Mathf.Abs(z - cz) - halfZ);
            return 1f - Mathf.SmoothStep(0f, feather, Mathf.Sqrt(dx * dx + dz * dz));
        }

        private static void BuildOcean(Transform root)
        {
            GameObject ocean = PrimitiveFactory.Quad("Shimmering Sea", root, new Vector3(0f, -1.05f, 0f),
                new Vector3(180f, 180f, 1f), IslandMaterials.Water, false);
            ocean.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // A second translucent layer creates distant parallax and a stronger horizon.
            GameObject deep = PrimitiveFactory.Quad("Deep Water", root, new Vector3(0f, -1.38f, 0f),
                new Vector3(260f, 260f, 1f), IslandMaterials.DeepWater, false);
            deep.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildIsland(Transform root)
        {
            const int xCount = 56;
            const int zCount = 44;
            var vertices = new Vector3[(xCount + 1) * (zCount + 1)];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[xCount * zCount * 6];

            for (int z = 0; z <= zCount; z++)
            {
                for (int x = 0; x <= xCount; x++)
                {
                    float wx = Mathf.Lerp(-IslandWidth * 0.5f, IslandWidth * 0.5f, x / (float)xCount);
                    float wz = Mathf.Lerp(-IslandDepth * 0.5f, IslandDepth * 0.5f, z / (float)zCount);
                    float y = SampleHeight(wx, wz);
                    int index = z * (xCount + 1) + x;
                    vertices[index] = new Vector3(wx, y, wz);
                    uv[index] = new Vector2(wx * 0.08f, wz * 0.08f);

                    if (y < -0.48f) colors[index] = new Color(0.91f, 0.72f, 0.43f, 1f);
                    else if (y < -0.05f) colors[index] = new Color(0.78f, 0.68f, 0.38f, 1f);
                    else if (y > 1.45f) colors[index] = new Color(0.22f, 0.45f, 0.23f, 1f);
                    else colors[index] = new Color(0.32f, 0.63f, 0.31f, 1f);
                }
            }

            int t = 0;
            for (int z = 0; z < zCount; z++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    int a = z * (xCount + 1) + x;
                    int b = a + 1;
                    int c = a + xCount + 1;
                    int d = c + 1;
                    // Order each triangle so its face normal points up. A
                    // downward-facing MeshCollider is one-sided: PhysX ignores
                    // back faces, so the character controller finds no ground
                    // there and falls through the island.
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "Sunpetal Island Terrain" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var island = new GameObject("Sunpetal Island Terrain");
            island.transform.SetParent(root, false);
            island.AddComponent<MeshFilter>().sharedMesh = mesh;
            island.AddComponent<MeshRenderer>().sharedMaterial = IslandMaterials.Ground;
            island.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildPaths(Transform root)
        {
            Transform paths = new GameObject("Shellstone Paths").transform;
            paths.SetParent(root);
            BuildPath(paths, new[]
            {
                new Vector3(-9f, 0f, -1f), new Vector3(1f, 0f, 2f),
                new Vector3(10f, 0f, 4f), new Vector3(21f, 0f, 4.8f), new Vector3(31f, 0f, 2f)
            }, 2.2f);
            BuildPath(paths, new[]
            {
                new Vector3(20f, 0f, 5f), new Vector3(19f, 0f, -5f), new Vector3(23f, 0f, -16f)
            }, 1.75f);
            BuildPath(paths, new[]
            {
                new Vector3(-8f, 0f, 0f), new Vector3(-18f, 0f, 8f), new Vector3(-27f, 0f, 16f)
            }, 1.5f);
        }

        private static void BuildPath(Transform parent, IReadOnlyList<Vector3> points, float width)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = points[i];
                Vector3 end = points[i + 1];
                Vector3 middle = (start + end) * 0.5f;
                middle.y = SampleHeight(middle.x, middle.z) + 0.045f;
                float length = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(end.x, 0f, end.z));
                var segment = PrimitiveFactory.Cube("Path", parent, middle,
                    new Vector3(width, 0.055f, length + 0.4f), IslandMaterials.Path, false);
                float angle = Mathf.Atan2(end.x - start.x, end.z - start.z) * Mathf.Rad2Deg;
                segment.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private static void BuildFarmstead(Transform root)
        {
            Transform farm = new GameObject("Farmstead").transform;
            farm.SetParent(root);
            BuildCottage(farm, new Vector3(6f, SampleHeight(6f, 7f), 7f));
            BuildBarn(farm, new Vector3(-20f, SampleHeight(-20f, 11f), 11f));
            BuildWell(farm, new Vector3(3.2f, SampleHeight(3.2f, -1.5f), -1.5f));
            BuildShippingCrate(farm, new Vector3(-0.2f, SampleHeight(-0.2f, 5.5f), 5.5f));
            BuildStump(farm, new Vector3(-1.7f, SampleHeight(-1.7f, -7f), -7f));
            BuildStump(farm, new Vector3(-15f, SampleHeight(-15f, 6.5f), 6.5f));
            BuildFarmFence(farm);

            // A porch lantern gives the farm a warm focal point after sunset.
            GameObject lantern = new GameObject("Porch Lantern");
            lantern.transform.SetParent(farm);
            lantern.transform.position = new Vector3(4.5f, SampleHeight(6f, 7f) + 2.45f, 4.85f);
            Light light = lantern.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.63f, 0.28f);
            light.intensity = 2.2f;
            light.range = 7f;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildCottage(Transform parent, Vector3 p)
        {
            Transform house = new GameObject("Player Cottage").transform;
            house.SetParent(parent);
            house.position = p;

            PrimitiveFactory.Cube("Foundation", house, new Vector3(0f, 0.25f, 0f), new Vector3(7.2f, 0.5f, 5.6f), IslandMaterials.Stone, true, true);
            PrimitiveFactory.Cube("Walls", house, new Vector3(0f, 1.75f, 0f), new Vector3(6.8f, 3f, 5.2f), IslandMaterials.Cream, true, true);
            PrimitiveFactory.GableRoof("Terracotta Roof", house, new Vector3(0f, 3.25f, 0f), 7.6f, 2.25f, 6.2f, IslandMaterials.Roof);
            PrimitiveFactory.Cube("Door", house, new Vector3(-1.3f, 1.25f, -2.63f), new Vector3(1.25f, 2.35f, 0.15f), IslandMaterials.TealWood, false);
            PrimitiveFactory.Cube("Window L", house, new Vector3(-2.5f, 1.9f, -2.72f), new Vector3(1.25f, 1.25f, 0.12f), IslandMaterials.Glass, false);
            PrimitiveFactory.Cube("Window R", house, new Vector3(1.65f, 1.9f, -2.72f), new Vector3(1.25f, 1.25f, 0.12f), IslandMaterials.Glass, false);
            PrimitiveFactory.Cube("Porch", house, new Vector3(0f, 0.35f, -3.25f), new Vector3(5.4f, 0.25f, 1.5f), IslandMaterials.Wood, true);
            PrimitiveFactory.Cube("Chimney", house, new Vector3(2.25f, 4.35f, 0.7f), new Vector3(0.75f, 2.1f, 0.75f), IslandMaterials.Stone, false);

            GameObject bedSpot = new GameObject("Cottage Door - Sleep");
            bedSpot.transform.SetParent(house);
            bedSpot.transform.localPosition = new Vector3(-1.3f, 0.5f, -3.8f);
            bedSpot.AddComponent<BedInteractable>();
        }

        private static void BuildBarn(Transform parent, Vector3 p)
        {
            Transform barn = new GameObject("Barn").transform;
            barn.SetParent(parent);
            barn.position = p;
            PrimitiveFactory.Cube("Barn Body", barn, new Vector3(0f, 1.9f, 0f), new Vector3(7.5f, 3.8f, 6f), IslandMaterials.BarnRed, true, true);
            PrimitiveFactory.GableRoof("Barn Roof", barn, new Vector3(0f, 3.8f, 0f), 8.2f, 2f, 6.8f, IslandMaterials.DarkRoof);
            PrimitiveFactory.Cube("Barn Door", barn, new Vector3(0f, 1.65f, -3.08f), new Vector3(2.6f, 3.15f, 0.12f), IslandMaterials.TealWood, false);
            PrimitiveFactory.Cube("Hay Loft", barn, new Vector3(0f, 3.3f, -3.14f), new Vector3(1.4f, 1.05f, 0.1f), IslandMaterials.DarkWood, false);

            for (int i = 0; i < 3; i++)
                PrimitiveFactory.Cube("Hay Bale", barn, new Vector3(4.4f + (i % 2) * 1.2f, 0.5f + (i / 2) * 0.8f, -0.5f + i * 0.5f),
                    new Vector3(1.4f, 0.9f, 1f), IslandMaterials.Hay, true);
        }

        private static void BuildWell(Transform parent, Vector3 p)
        {
            Transform well = new GameObject("Spring Well").transform;
            well.SetParent(parent);
            well.position = p;
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                PrimitiveFactory.Cube("Well Stone", well, new Vector3(Mathf.Sin(angle) * 0.85f, 0.45f, Mathf.Cos(angle) * 0.85f),
                    new Vector3(0.55f, 0.65f, 0.45f), IslandMaterials.Stone, true).transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
            PrimitiveFactory.Cylinder("Water", well, new Vector3(0f, 0.48f, 0f), new Vector3(0.75f, 0.03f, 0.75f), IslandMaterials.Water, false);
            PrimitiveFactory.Cylinder("Roof", well, new Vector3(0f, 2.4f, 0f), new Vector3(1.3f, 0.16f, 1.3f), IslandMaterials.DarkRoof, false);
            PrimitiveFactory.Cube("Post L", well, new Vector3(-0.95f, 1.45f, 0f), new Vector3(0.16f, 2.4f, 0.16f), IslandMaterials.DarkWood, false);
            PrimitiveFactory.Cube("Post R", well, new Vector3(0.95f, 1.45f, 0f), new Vector3(0.16f, 2.4f, 0.16f), IslandMaterials.DarkWood, false);
            well.gameObject.AddComponent<WellInteractable>();
        }

        private static void BuildShippingCrate(Transform parent, Vector3 p)
        {
            Transform crate = PrimitiveFactory.Cube("Shipping Crate", parent, p + Vector3.up * 0.65f,
                new Vector3(1.8f, 1.3f, 1.5f), IslandMaterials.TealWood, true).transform;
            PrimitiveFactory.Cube("Lid", crate, new Vector3(0f, 0.72f, -0.12f), new Vector3(1.95f, 0.14f, 1.7f), IslandMaterials.DarkWood, false);
            crate.gameObject.AddComponent<ShippingCrateInteractable>();
        }

        private static void BuildStump(Transform parent, Vector3 p)
        {
            Transform stump = PrimitiveFactory.Cylinder("Old Driftwood Stump", parent, p + Vector3.up * 0.48f,
                new Vector3(0.58f, 0.48f, 0.58f), IslandMaterials.PalmTrunk, true).transform;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                GameObject root = PrimitiveFactory.Cylinder("Root", stump, new Vector3(0f, -0.37f, 0f),
                    new Vector3(0.13f, 0.62f, 0.13f), IslandMaterials.PalmTrunk, false);
                root.transform.localRotation = Quaternion.Euler(68f, angle, 0f);
                root.transform.localPosition += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.42f;
            }
            stump.gameObject.AddComponent<ChoppableInteractable>();
        }

        private static void BuildFarmFence(Transform parent)
        {
            for (int x = -15; x <= 1; x += 2)
            {
                if (x > -4 && x < 0) continue;
                CreateFencePost(parent, new Vector3(x, SampleHeight(x, -10f), -10f));
                CreateFencePost(parent, new Vector3(x, SampleHeight(x, 8f), 8f));
            }
            for (int z = -8; z <= 8; z += 2)
            {
                CreateFencePost(parent, new Vector3(-16f, SampleHeight(-16f, z), z));
                if (z < 3 || z > 7) CreateFencePost(parent, new Vector3(2f, SampleHeight(2f, z), z));
            }
        }

        private static void CreateFencePost(Transform parent, Vector3 p)
        {
            PrimitiveFactory.Cube("Fence Post", parent, p + Vector3.up * 0.55f, new Vector3(0.18f, 1.1f, 0.18f), IslandMaterials.WhiteWood, false);
        }

        private static void BuildVillage(Transform root)
        {
            Transform village = new GameObject("Luma Village").transform;
            village.SetParent(root);
            BuildShop(village, new Vector3(24f, SampleHeight(24f, 10f), 10f));
            BuildCafe(village, new Vector3(32f, SampleHeight(32f, 5f), 5f));
            BuildMarket(village, new Vector3(24f, SampleHeight(24f, -1f), -1f));
            BuildFountain(village, new Vector3(19f, SampleHeight(19f, 4.5f), 4.5f));

            Transform npc = BuildPerson(village, "Maris", new Vector3(20.5f, SampleHeight(20.5f, 1f), 1f), IslandMaterials.Coral);
            npc.gameObject.AddComponent<VillagerInteractable>().SetVillager("Maris", new[]
            {
                "The monsoon left your farm wild, but this soil remembers how to bloom.",
                "Moonmelons grow after two watered mornings. Ripe fruit glows gold at the tips.",
                "Bring produce to the teal shipping crate. The evening boat pays in shells.",
                "That old lighthouse trail has the best sunset view on Sunpetal Island."
            });
        }

        private static void BuildShop(Transform parent, Vector3 p)
        {
            Transform shop = new GameObject("Seed & Sundry").transform;
            shop.SetParent(parent);
            shop.position = p;
            PrimitiveFactory.Cube("Shop", shop, new Vector3(0f, 1.65f, 0f), new Vector3(6.5f, 3.3f, 5f), IslandMaterials.Mint, true, true);
            PrimitiveFactory.GableRoof("Shop Roof", shop, new Vector3(0f, 3.3f, 0f), 7.2f, 1.8f, 5.8f, IslandMaterials.Roof);
            PrimitiveFactory.Cube("Awning", shop, new Vector3(0f, 2.35f, -2.85f), new Vector3(4.8f, 0.18f, 1.2f), IslandMaterials.Coral, false).transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
            PrimitiveFactory.Cube("Door", shop, new Vector3(0f, 1.15f, -2.56f), new Vector3(1.25f, 2.2f, 0.1f), IslandMaterials.DarkWood, false);
        }

        private static void BuildCafe(Transform parent, Vector3 p)
        {
            Transform cafe = new GameObject("Starfish Cafe").transform;
            cafe.SetParent(parent);
            cafe.position = p;
            PrimitiveFactory.Cube("Cafe", cafe, new Vector3(0f, 1.55f, 0f), new Vector3(6f, 3.1f, 5f), IslandMaterials.Cream, true, true);
            PrimitiveFactory.GableRoof("Cafe Roof", cafe, new Vector3(0f, 3.1f, 0f), 6.7f, 1.65f, 5.7f, IslandMaterials.TealWood);
            PrimitiveFactory.Cube("Wide Window", cafe, new Vector3(-1.2f, 1.8f, -2.56f), new Vector3(2.3f, 1.35f, 0.1f), IslandMaterials.Glass, false);
            PrimitiveFactory.Cube("Cafe Door", cafe, new Vector3(1.5f, 1.15f, -2.56f), new Vector3(1.1f, 2.2f, 0.1f), IslandMaterials.Coral, false);
            for (int i = 0; i < 2; i++)
            {
                PrimitiveFactory.Cylinder("Table", cafe, new Vector3(-2f + i * 3.6f, 0.65f, -4f), new Vector3(0.65f, 0.08f, 0.65f), IslandMaterials.WhiteWood, false);
                PrimitiveFactory.Cylinder("Table Post", cafe, new Vector3(-2f + i * 3.6f, 0.32f, -4f), new Vector3(0.08f, 0.32f, 0.08f), IslandMaterials.DarkWood, false);
            }
        }

        private static void BuildMarket(Transform parent, Vector3 p)
        {
            Transform stall = new GameObject("Moonmelon Market Stall").transform;
            stall.SetParent(parent);
            stall.position = p;
            PrimitiveFactory.Cube("Counter", stall, new Vector3(0f, 0.8f, 0f), new Vector3(3.8f, 1.1f, 1.5f), IslandMaterials.Wood, true);
            PrimitiveFactory.Cube("Canopy", stall, new Vector3(0f, 2.75f, 0f), new Vector3(4.6f, 0.18f, 2.4f), IslandMaterials.Coral, false);
            PrimitiveFactory.Cube("Post L", stall, new Vector3(-1.8f, 1.6f, 0.55f), new Vector3(0.14f, 3f, 0.14f), IslandMaterials.DarkWood, false);
            PrimitiveFactory.Cube("Post R", stall, new Vector3(1.8f, 1.6f, 0.55f), new Vector3(0.14f, 3f, 0.14f), IslandMaterials.DarkWood, false);
            for (int i = 0; i < 7; i++)
                PrimitiveFactory.Sphere("Market Fruit", stall, new Vector3(-1.35f + (i % 4) * 0.85f, 1.48f + (i / 4) * 0.35f, -0.25f),
                    Vector3.one * 0.34f, i % 2 == 0 ? IslandMaterials.CropGold : IslandMaterials.Coral, false);
            stall.gameObject.AddComponent<MarketInteractable>();
        }

        private static void BuildFountain(Transform parent, Vector3 p)
        {
            Transform fountain = new GameObject("Village Fountain").transform;
            fountain.SetParent(parent);
            fountain.position = p;
            PrimitiveFactory.Cylinder("Basin", fountain, new Vector3(0f, 0.25f, 0f), new Vector3(1.75f, 0.25f, 1.75f), IslandMaterials.Stone, true);
            PrimitiveFactory.Cylinder("Water", fountain, new Vector3(0f, 0.53f, 0f), new Vector3(1.45f, 0.04f, 1.45f), IslandMaterials.Water, false);
            PrimitiveFactory.Cylinder("Column", fountain, new Vector3(0f, 1.2f, 0f), new Vector3(0.24f, 0.85f, 0.24f), IslandMaterials.Stone, false);
            PrimitiveFactory.Sphere("Pearl", fountain, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.48f, IslandMaterials.Pearl, false);
        }

        private static Transform BuildPerson(Transform parent, string name, Vector3 p, Material shirt)
        {
            Transform person = new GameObject(name).transform;
            person.SetParent(parent);
            person.position = p;
            PrimitiveFactory.Capsule("Body", person, new Vector3(0f, 1.05f, 0f), new Vector3(0.58f, 0.72f, 0.58f), shirt, false);
            PrimitiveFactory.Sphere("Head", person, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.52f, IslandMaterials.Skin, false);
            PrimitiveFactory.Sphere("Hair", person, new Vector3(0f, 2.27f, 0.08f), new Vector3(0.57f, 0.35f, 0.55f), IslandMaterials.DarkWood, false);
            return person;
        }

        private static void BuildHarbor(Transform root)
        {
            Transform harbor = new GameObject("South Harbor").transform;
            harbor.SetParent(root);
            Vector3 start = new Vector3(23f, SampleHeight(23f, -18f), -18f);
            for (int i = 0; i < 9; i++)
            {
                float z = -18f - i * 1.7f;
                PrimitiveFactory.Cube("Dock Plank", harbor, new Vector3(23f, -0.48f, z), new Vector3(4.2f, 0.22f, 1.55f), IslandMaterials.Wood, true);
                PrimitiveFactory.Cylinder("Piling L", harbor, new Vector3(21.4f, -0.75f, z), new Vector3(0.17f, 1.15f, 0.17f), IslandMaterials.DarkWood, false);
                PrimitiveFactory.Cylinder("Piling R", harbor, new Vector3(24.6f, -0.75f, z), new Vector3(0.17f, 1.15f, 0.17f), IslandMaterials.DarkWood, false);
            }
            BuildBoat(harbor, new Vector3(28f, -0.55f, -27f));
        }

        private static void BuildBoat(Transform parent, Vector3 p)
        {
            Transform boat = new GameObject("Island Ferry").transform;
            boat.SetParent(parent);
            boat.position = p;
            PrimitiveFactory.Capsule("Hull", boat, Vector3.zero, new Vector3(1.8f, 0.42f, 3.5f), IslandMaterials.WhiteWood, false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveFactory.Cube("Deck", boat, new Vector3(0f, 0.4f, 0f), new Vector3(2.5f, 0.18f, 4.4f), IslandMaterials.Wood, false);
            PrimitiveFactory.Cube("Cabin", boat, new Vector3(0f, 1.1f, 0.5f), new Vector3(1.8f, 1.4f, 1.9f), IslandMaterials.Cream, false);
            PrimitiveFactory.Cube("Cabin Glass", boat, new Vector3(0f, 1.25f, -0.48f), new Vector3(1.45f, 0.62f, 0.06f), IslandMaterials.Glass, false);
        }

        private static void BuildLighthouse(Transform root)
        {
            Vector3 p = new Vector3(-29f, SampleHeight(-29f, 20f), 20f);
            Transform tower = new GameObject("Sunpetal Lighthouse").transform;
            tower.SetParent(root);
            tower.position = p;
            PrimitiveFactory.Cylinder("Tower", tower, new Vector3(0f, 3.7f, 0f), new Vector3(2.1f, 3.7f, 2.1f), IslandMaterials.Cream, true, true);
            PrimitiveFactory.Cylinder("Gallery", tower, new Vector3(0f, 7.45f, 0f), new Vector3(2.4f, 0.18f, 2.4f), IslandMaterials.DarkRoof, false);
            PrimitiveFactory.Cylinder("Lantern Room", tower, new Vector3(0f, 8.25f, 0f), new Vector3(1.45f, 0.65f, 1.45f), IslandMaterials.Glass, false);
            PrimitiveFactory.Cone("Cap", tower, new Vector3(0f, 9.25f, 0f), 1.8f, 1.5f, IslandMaterials.Coral);
            GameObject beacon = new GameObject("Beacon");
            beacon.transform.SetParent(tower);
            beacon.transform.localPosition = new Vector3(0f, 8.2f, 0f);
            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.84f, 0.47f);
            light.intensity = 5f;
            light.range = 18f;
        }

        private static void BuildNature(Transform root)
        {
            Transform nature = new GameObject("Island Nature").transform;
            nature.SetParent(root);
            var random = new System.Random(7814);

            int treesBuilt = 0;
            for (int attempt = 0; attempt < 100 && treesBuilt < 34; attempt++)
            {
                float x = (float)(random.NextDouble() * 76.0 - 38.0);
                float z = (float)(random.NextDouble() * 56.0 - 28.0);
                float ellipse = Mathf.Sqrt(x * x / (38f * 38f) + z * z / (28f * 28f));
                bool farmClear = x > -18f && x < 9f && z > -12f && z < 11f;
                bool townClear = x > 13f && x < 38f && z > -10f && z < 16f;
                if (ellipse > 0.94f || farmClear || townClear) continue;
                BuildPalm(nature, new Vector3(x, SampleHeight(x, z), z), (float)(0.85 + random.NextDouble() * 0.45));
                treesBuilt++;
            }

            for (int i = 0; i < 22; i++)
            {
                float x = (float)(random.NextDouble() * 70.0 - 35.0);
                float z = (float)(random.NextDouble() * 50.0 - 25.0);
                bool clear = x > -17f && x < 10f && z > -11f && z < 10f;
                if (clear) continue;
                BuildRock(nature, new Vector3(x, SampleHeight(x, z), z), (float)(0.35 + random.NextDouble() * 0.75));
            }

            for (int i = 0; i < 48; i++)
            {
                float x = (float)(random.NextDouble() * 62.0 - 31.0);
                float z = (float)(random.NextDouble() * 44.0 - 22.0);
                bool onFarm = x > -16f && x < 3f && z > -10f && z < 9f;
                if (onFarm) continue;
                BuildFlower(nature, new Vector3(x, SampleHeight(x, z) + 0.1f, z), i % 3);
            }
        }

        private static void BuildPalm(Transform parent, Vector3 p, float scale)
        {
            Transform tree = new GameObject("Breezy Palm").transform;
            tree.SetParent(parent);
            tree.position = p;
            tree.localScale = Vector3.one * scale;
            PrimitiveFactory.Cylinder("Trunk", tree, new Vector3(0f, 2.1f, 0f), new Vector3(0.28f, 2.1f, 0.28f), IslandMaterials.PalmTrunk, true);
            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * 360f;
                GameObject leaf = PrimitiveFactory.Sphere("Palm Frond", tree, new Vector3(0f, 4.25f, 0f),
                    new Vector3(0.35f, 0.09f, 2.35f), IslandMaterials.Leaves, false);
                leaf.transform.rotation = Quaternion.Euler(18f, angle, 0f);
                leaf.transform.localPosition += leaf.transform.forward * 1.35f;
            }
            for (int i = 0; i < 3; i++)
                PrimitiveFactory.Sphere("Coconut", tree, new Vector3(-0.22f + i * 0.22f, 4.02f, 0.12f), Vector3.one * 0.24f, IslandMaterials.DarkWood, false);
        }

        private static void BuildRock(Transform parent, Vector3 p, float scale)
        {
            GameObject rock = PrimitiveFactory.Sphere("Coastal Rock", parent, p + Vector3.up * scale * 0.35f,
                new Vector3(scale, scale * 0.7f, scale * 0.82f), IslandMaterials.Rock, true);
            rock.transform.rotation = Quaternion.Euler(0f, scale * 137f, scale * 19f);
        }

        private static void BuildFlower(Transform parent, Vector3 p, int color)
        {
            Material petal = color == 0 ? IslandMaterials.Coral : color == 1 ? IslandMaterials.CropGold : IslandMaterials.Pearl;
            PrimitiveFactory.Cylinder("Wildflower Stem", parent, p + Vector3.up * 0.16f, new Vector3(0.025f, 0.16f, 0.025f), IslandMaterials.Leaves, false);
            PrimitiveFactory.Sphere("Wildflower", parent, p + Vector3.up * 0.35f, Vector3.one * 0.13f, petal, false);
        }

        private static void BuildLighting()
        {
            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }
            if (sun == null)
            {
                var sunObject = new GameObject("Island Sun");
                sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.name = "Island Sun";
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.9f, 0.72f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 135f;
            RenderSettings.fogColor = new Color(0.53f, 0.78f, 0.85f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.57f, 0.77f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.55f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.22f, 0.16f);
        }
    }

    public static class IslandMaterials
    {
        public static Material Ground, Water, DeepWater, Path, Stone, Rock, Cream, Mint, Roof, DarkRoof;
        public static Material Wood, DarkWood, WhiteWood, TealWood, BarnRed, Hay, Glass, Leaves, PalmTrunk;
        public static Material Skin, Coral, CropGold, Pearl, Soil, WetSoil, PlotGrass, Stem, Highlight;

        public static void Initialize()
        {
            if (Ground != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader ground = Resources.Load<Shader>("StylizedGround") ?? lit;
            Shader foliage = Resources.Load<Shader>("StylizedFoliage") ?? lit;
            Shader water = Resources.Load<Shader>("StylizedWater") ?? lit;

            Ground = Make("Island Ground", ground, Color.white, 0f);
            Water = Make("Lagoon Water", water, new Color(0.08f, 0.66f, 0.77f, 0.72f), 0.75f);
            DeepWater = Make("Deep Sea", water, new Color(0.025f, 0.28f, 0.55f, 0.82f), 0.82f);
            Path = Make("Shell Path", lit, new Color(0.86f, 0.73f, 0.49f), 0.08f);
            Stone = Make("Warm Stone", lit, new Color(0.49f, 0.53f, 0.48f), 0.18f);
            Rock = Make("Coastal Rock", lit, new Color(0.34f, 0.40f, 0.38f), 0.12f);
            Cream = Make("Sun Cream", lit, new Color(0.93f, 0.82f, 0.59f), 0.08f);
            Mint = Make("Sea Mint", lit, new Color(0.47f, 0.75f, 0.61f), 0.12f);
            Roof = Make("Terracotta", lit, new Color(0.78f, 0.29f, 0.20f), 0.12f);
            DarkRoof = Make("Night Slate", lit, new Color(0.12f, 0.25f, 0.28f), 0.18f);
            Wood = Make("Driftwood", lit, new Color(0.56f, 0.34f, 0.18f), 0.15f);
            DarkWood = Make("Dark Wood", lit, new Color(0.22f, 0.12f, 0.085f), 0.12f);
            WhiteWood = Make("White Wood", lit, new Color(0.87f, 0.82f, 0.67f), 0.08f);
            TealWood = Make("Teal Wood", lit, new Color(0.08f, 0.39f, 0.42f), 0.17f);
            BarnRed = Make("Barn Coral", lit, new Color(0.61f, 0.18f, 0.13f), 0.08f);
            Hay = Make("Hay", lit, new Color(0.86f, 0.63f, 0.18f), 0.05f);
            Glass = Make("Ocean Glass", lit, new Color(0.17f, 0.62f, 0.72f), 0.82f, new Color(0.03f, 0.15f, 0.18f));
            Leaves = Make("Palm Leaves", foliage, new Color(0.09f, 0.49f, 0.25f), 0.04f);
            PalmTrunk = Make("Palm Trunk", lit, new Color(0.40f, 0.23f, 0.12f), 0.06f);
            Skin = Make("Sunlit Skin", lit, new Color(0.66f, 0.36f, 0.22f), 0.2f);
            Coral = Make("Coral", lit, new Color(0.95f, 0.31f, 0.26f), 0.12f);
            CropGold = Make("Moonmelon Gold", lit, new Color(0.98f, 0.65f, 0.12f), 0.27f, new Color(0.22f, 0.07f, 0f));
            Pearl = Make("Pearl", lit, new Color(0.90f, 0.91f, 0.78f), 0.7f);
            Soil = Make("Tilled Soil", lit, new Color(0.31f, 0.16f, 0.075f), 0.04f);
            WetSoil = Make("Watered Soil", lit, new Color(0.12f, 0.085f, 0.055f), 0.55f);
            PlotGrass = Make("Plot Grass", lit, new Color(0.28f, 0.53f, 0.23f), 0.03f);
            Stem = Make("Crop Stem", foliage, new Color(0.12f, 0.48f, 0.16f), 0.05f);
            Highlight = Make("Plot Highlight", lit, new Color(1f, 0.78f, 0.18f), 0.4f, new Color(0.35f, 0.15f, 0f));
        }

        private static Material Make(string name, Shader shader, Color color, float smoothness, Color? emission = null)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission.Value);
            }
            return material;
        }
    }

    public static class PrimitiveFactory
    {
        public static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local = false)
            => Primitive(PrimitiveType.Cube, name, parent, position, scale, material, collider, local);
        public static GameObject Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local = false)
            => Primitive(PrimitiveType.Sphere, name, parent, position, scale, material, collider, local);
        public static GameObject Capsule(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local = false)
            => Primitive(PrimitiveType.Capsule, name, parent, position, scale, material, collider, local);
        public static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local = false)
            => Primitive(PrimitiveType.Cylinder, name, parent, position, scale, material, collider, local);
        public static GameObject Quad(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local = false)
            => Primitive(PrimitiveType.Quad, name, parent, position, scale, material, collider, local);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider, bool local)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            // Builder positions are expressed in the coordinate space of their parent.
            // Most grouping transforms sit at the origin; authored props can then reuse this
            // helper for their own local details without special-case coordinate conversion.
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null && !collider) itemCollider.enabled = false;
            return item;
        }

        public static GameObject GableRoof(string name, Transform parent, Vector3 localPosition, float width, float height, float depth, Material material)
        {
            float w = width * 0.5f;
            float d = depth * 0.5f;
            var vertices = new[]
            {
                new Vector3(-w,0f,-d), new Vector3(w,0f,-d), new Vector3(0f,height,-d),
                new Vector3(-w,0f,d), new Vector3(w,0f,d), new Vector3(0f,height,d)
            };
            var triangles = new[] { 0,2,1, 3,4,5, 0,3,5, 0,5,2, 1,2,5, 1,5,4, 0,1,4, 0,4,3 };
            var mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var roof = new GameObject(name);
            roof.transform.SetParent(parent, false);
            roof.transform.localPosition = localPosition;
            roof.AddComponent<MeshFilter>().sharedMesh = mesh;
            roof.AddComponent<MeshRenderer>().sharedMaterial = material;
            return roof;
        }

        public static GameObject Cone(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material, int sides = 18)
        {
            var vertices = new Vector3[sides + 2];
            vertices[0] = new Vector3(0f, height * 0.5f, 0f);
            vertices[1] = new Vector3(0f, -height * 0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Sin(a) * radius, -height * 0.5f, Mathf.Cos(a) * radius);
            }
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = 0; triangles[t + 1] = i + 2; triangles[t + 2] = next + 2;
                triangles[t + 3] = 1; triangles[t + 4] = next + 2; triangles[t + 5] = i + 2;
            }
            var mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var cone = new GameObject(name);
            cone.transform.SetParent(parent, false);
            cone.transform.localPosition = localPosition;
            cone.AddComponent<MeshFilter>().sharedMesh = mesh;
            cone.AddComponent<MeshRenderer>().sharedMaterial = material;
            return cone;
        }
    }
}
