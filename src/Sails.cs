using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skysail
{
    // One sail per biome. Every tier comes down at the same 2.4 m/s; higher tiers fly faster forward (+2 m/s each) and tire you less.
    internal class Tier
    {
        internal string Prefab, Name, Desc;
        internal string[] Sources;
        internal string Cost, Station;
        internal int Level;
        internal float Forward, Descent, Turn, Stamina;
        internal Color Hide, Trim;          // membrane and its edge
        internal int Feathers;              // 0 = bare membrane, 3 = feather fringe only, 1 = fringe and a few feathers on the wing, 2 = feathered wings
        internal float FeatherLen = 0.3f;
        internal Color Feather = Color.black;
        internal string CapeLook;           // use this vanilla cape's own material on the membrane instead of the drawn one
    }

    internal static class Sails
    {
        internal static readonly Tier[] Tiers =
        {
            null,
            new Tier { Prefab = "Skysail_DeerHide", Name = "Deer hide wingcape", Sources = new[] { "CapeDeerHide" },
                Desc = "Raw deer hide sewn between your arms and legs. It won't take you far, but it beats the fall.",
                Cost = "DeerHide:6, Wood:10, LeatherScraps:6", Station = "piece_workbench", Level = 2, CapeLook = "CapeDeerHide",
                Forward = 8f, Descent = -2.4f, Turn = 80f, Stamina = 3f, Feathers = 0, FeatherLen = 0f,
                Hide = new Color(0.62f, 0.46f, 0.3f), Trim = new Color(0.36f, 0.25f, 0.15f) },
            new Tier { Prefab = "Skysail_TrollHide", Name = "Troll hide wingcape", Sources = new[] { "CapeTrollHide", "CapeDeerHide" },
                Desc = "Tough troll hide stretched like a bat's wing, stiffened with bronze ribs.",
                Cost = "TrollHide:6, FineWood:8, Bronze:2", Station = "piece_workbench", Level = 3, CapeLook = "CapeTrollHide",
                Forward = 10f, Descent = -2.4f, Turn = 90f, Stamina = 2.4f, Feathers = 0, FeatherLen = 0f,
                Hide = new Color(0.38f, 0.45f, 0.55f), Trim = new Color(0.22f, 0.24f, 0.3f) },
            new Tier { Prefab = "Skysail_WolfPelt", Name = "Wolf pelt wingcape", Sources = new[] { "CapeWolf", "CapeDeerHide" },
                Desc = "Wolf pelts with a fringe of grey feathers along the trailing edge. Made for mountain winds.",
                Cost = "WolfPelt:6, FineWood:10, Silver:4", Station = "piece_workbench", Level = 4,
                Forward = 12f, Descent = -2.4f, Turn = 100f, Stamina = 1.9f, Feathers = 1, FeatherLen = 0.22f, Feather = new Color(0.62f, 0.62f, 0.64f),
                Hide = new Color(0.72f, 0.72f, 0.74f), Trim = new Color(0.92f, 0.9f, 0.86f) },
            new Tier { Prefab = "Skysail_LoxPelt", Name = "Lox pelt wingcape", Sources = new[] { "CapeLox", "CapeDeerHide" },
                Desc = "Lox pelt and linen, edged with long brown feathers.",
                Cost = "LoxPelt:4, LinenThread:10, FineWood:10, BlackMetal:4", Station = "piece_workbench", Level = 4,
                Forward = 14f, Descent = -2.4f, Turn = 110f, Stamina = 1.4f, Feathers = 1, FeatherLen = 0.32f, Feather = new Color(0.4f, 0.27f, 0.16f),
                Hide = new Color(0.76f, 0.62f, 0.42f), Trim = new Color(0.55f, 0.18f, 0.12f) },
            new Tier { Prefab = "Skysail_Carapace", Name = "Mistfeather wingcape", Sources = new[] { "CapeFeather", "CapeLox", "CapeDeerHide" },
                Desc = "Grey feathers over a thin scale hide, ribbed with carapace. Light as mist.",
                Cost = "Carapace:8, ScaleHide:6, YggdrasilWood:10, Feathers:10", Station = "blackforge", Level = 1,
                Forward = 16f, Descent = -2.4f, Turn = 120f, Stamina = 1f, Feathers = 2, FeatherLen = 0.36f, Feather = new Color(0.5f, 0.48f, 0.56f),
                Hide = new Color(0.45f, 0.4f, 0.6f), Trim = new Color(0.25f, 0.55f, 0.6f) },
            new Tier { Prefab = "Skysail_Ashwing", Name = "Ravenwing cape", Sources = new[] { "CapeAsksvin", "CapeFeather", "CapeDeerHide" },
                Desc = "Black feathers like the raven who watched over you from the start. It opens wide and carries you far.",
                Cost = "AskHide:8, FlametalNew:4, CelestialFeather:2, YggdrasilWood:10", Station = "blackforge", Level = 2,
                Forward = 18f, Descent = -2.4f, Turn = 130f, Stamina = 0.7f, Feathers = 2, FeatherLen = 0.46f, Feather = new Color(0.07f, 0.07f, 0.085f),
                Hide = new Color(0.1f, 0.1f, 0.12f), Trim = new Color(0.05f, 0.05f, 0.06f) },
        };

        internal static int Count { get { return Tiers.Length - 1; } }

        internal static void Register()
        {
            for (int i = 1; i <= Count; i++)
            {
                Tier t = Tiers[i];
                int tier = i;
                Items.All.Add(new CustomItem
                {
                    PrefabName = t.Prefab,
                    Sources = t.Sources,
                    Cost = t.Cost,
                    Station = t.Station,
                    Level = t.Level,
                    Setup = p => SetupItem(p, tier)
                });
            }
        }

        private static void SetupItem(GameObject p, int tier)
        {
            Tier t = Tiers[tier];
            ItemDrop.ItemData.SharedData sh = p.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = t.Name;
            sh.m_description = t.Desc + "\n\nWear it as your cape. Jump in the air to spread it into wings, jump again to fold it. Hold forward to dive, back to slow down.";
            sh.m_armor = 1f;
            sh.m_armorPerLevel = 0f;
            sh.m_maxQuality = 1;
            sh.m_movementModifier = 0f;
            sh.m_equipStatusEffect = null;
            sh.m_setStatusEffect = null;
            sh.m_setName = "";
            sh.m_setSize = 0;
            sh.m_damageModifiers = new List<HitData.DamageModPair>();
            sh.m_weight = 4f;
            sh.m_value = 0;
            if (sh.m_icons != null && sh.m_icons.Length > 0) sh.m_icons = new[] { Tint(sh.m_icons[0], t.Hide, t.Prefab + "_icon") };
            p.AddComponent<SailTier>().Tier = tier;
        }

        // the game's own capes glide: each tier is the vanilla cape listed first in its Sources
        private static readonly System.Collections.Generic.Dictionary<string, int> Extra = new System.Collections.Generic.Dictionary<string, int>
        {
            { "CapeLinen", 4 }, { "CapeOdin", 3 }, { "CapeAsh", 6 }, { "CapeDeepNorth", 6 }, { "CapeDeepNorthMage", 6 }, { "CapeTest", 0 },
        };

        internal static int TierOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return 0;
            string n = item.m_dropPrefab.name;
            for (int i = 1; i <= Count; i++) if (Tiers[i].Sources[0] == n) return i;
            int t;
            if (Extra.TryGetValue(n, out t)) return t;
            // any other cape (other mods' too) glides: guess its tier from its name, else the middle one
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shoulder) return 0;
            string l = n.ToLowerInvariant();
            if (l.Contains("deer") || l.Contains("meadow")) return 1;
            if (l.Contains("troll") || l.Contains("bforest") || l.Contains("swamp")) return 2;
            if (l.Contains("wolf") || l.Contains("mountain")) return 3;
            if (l.Contains("lox") || l.Contains("linen") || l.Contains("plains")) return 4;
            if (l.Contains("feather") || l.Contains("mist")) return 5;
            if (l.Contains("asksvin") || l.Contains("ash")) return 6;
            return 3;
        }

        private static Sprite Tint(Sprite s, Color tint, string name)
        {
            try
            {
                Texture2D src = s.texture;
                Rect r = s.textureRect;
                RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height);
                Graphics.Blit(src, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(r.x, src.height - r.y - r.height, r.width, r.height), 0, 0);
                t.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = t.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    float l = (c.r + c.g + c.b) / 3f * 1.6f;
                    px[i] = new Color(tint.r * l, tint.g * l, tint.b * l, c.a);
                }
                t.SetPixels(px);
                t.Apply();
                Sprite o = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                o.name = name;
                return o;
            }
            catch (Exception e)
            {
                Plugin.Debug("[items] icon tint failed: " + e.Message);
                return s;
            }
        }
    }

    // marks the item prefab with its tier (kept when the game copies the prefab)
    internal class SailTier : MonoBehaviour
    {
        public int Tier = 1;
    }
}
