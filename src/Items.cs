using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Skysail
{
    // Our items: copies of vanilla items with their own name, recipe and stats, registered in ObjectDB and ZNetScene
    // (same scheme as Beastkeeper).
    internal class CustomItem
    {
        internal string PrefabName;
        internal string[] Sources;
        internal Action<GameObject> Setup;
        internal string Cost, Station;
        internal int Level;
        internal GameObject Prefab;
    }

    [HarmonyPatch]
    internal static class Items
    {
        private static readonly MethodInfo UpdateRegisters = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
        private static readonly FieldInfo NamedPrefabs = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
        internal static readonly List<CustomItem> All = new List<CustomItem>();
        private static GameObject _holder;

        private static GameObject Build(CustomItem item, Func<string, GameObject> find)
        {
            if (item.Prefab != null) return item.Prefab;
            GameObject src = null;
            foreach (string s in item.Sources) { src = find(s); if (src != null) break; }
            if (src == null) return null;
            if (_holder == null)
            {
                _holder = new GameObject("Skysail_Prefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject p = Object.Instantiate(src, _holder.transform, false);
            p.name = item.PrefabName;
            p.GetComponent<ItemDrop>().m_itemData.m_dropPrefab = p;
            item.Prefab = p;
            item.Setup(p);
            Plugin.Debug("[items] built " + item.PrefabName + " from " + src.name);
            return p;
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake"), HarmonyPostfix]
        private static void ObjectDBAwake(ObjectDB __instance) { Register(__instance); }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB"), HarmonyPostfix]
        private static void ObjectDBCopy(ObjectDB __instance) { Register(__instance); }

        [HarmonyPatch(typeof(ZNetScene), "Awake"), HarmonyPostfix]
        private static void ZNetSceneAwake(ZNetScene __instance)
        {
            var named = NamedPrefabs != null ? NamedPrefabs.GetValue(__instance) as Dictionary<int, GameObject> : null;
            foreach (CustomItem item in All)
            {
                GameObject p = Build(item, n => __instance.m_prefabs.Find(g => g != null && g.name == n));
                if (p == null) { Plugin.Log.LogWarning("Could not build " + item.PrefabName + ": no source item found."); continue; }
                if (!__instance.m_prefabs.Contains(p)) __instance.m_prefabs.Add(p);
                if (named != null) named[item.PrefabName.GetStableHashCode()] = p;
            }
            if (ObjectDB.instance != null) Register(ObjectDB.instance);
        }

        private static void Register(ObjectDB db)
        {
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;
            bool changed = false;
            foreach (CustomItem item in All)
            {
                GameObject p = Build(item, n => db.m_items.Find(g => g != null && g.name == n));
                if (p == null) continue;
                if (!db.m_items.Contains(p)) { db.m_items.Add(p); changed = true; }
            }
            if (changed && UpdateRegisters != null) UpdateRegisters.Invoke(db, null);
            foreach (CustomItem item in All)
                if (item.Prefab != null) AddRecipe(db, item);
        }

        private static void AddRecipe(ObjectDB db, CustomItem item)
        {
            ItemDrop drop = item.Prefab.GetComponent<ItemDrop>();
            Recipe recipe = db.m_recipes.Find(r => r != null && r.m_item == drop);
            CraftingStation station = null;
            string st = (item.Station ?? "").Trim();
            if (st.Length > 0)
            {
                GameObject sp = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(st) : null;
                if (sp == null) return;   // the station isn't known yet: try again when ZNetScene wakes up
                station = sp.GetComponent<CraftingStation>();
            }
            List<Piece.Requirement> reqs = new List<Piece.Requirement>();
            foreach (string part in (item.Cost ?? "").Split(','))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2) continue;
                GameObject ip = db.GetItemPrefab(kv[0].Trim());
                int amount;
                if (ip == null || !int.TryParse(kv[1].Trim(), out amount)) { Plugin.Log.LogWarning(item.PrefabName + " recipe: unknown item '" + part + "'."); continue; }
                reqs.Add(new Piece.Requirement { m_resItem = ip.GetComponent<ItemDrop>(), m_amount = amount, m_recover = true });
            }
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_" + item.PrefabName;
                db.m_recipes.Add(recipe);
            }
            recipe.m_item = drop;
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = station;
            recipe.m_minStationLevel = Mathf.Max(1, item.Level);
            recipe.m_resources = reqs.ToArray();
        }
    }
}
