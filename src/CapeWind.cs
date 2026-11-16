using System.Collections.Generic;
using HarmonyLib;
using MagicaCloth2;
using Unity.Mathematics;
using UnityEngine;

namespace Skysail
{
    // Capes are cloth (MagicaCloth, run by the game). While their wearer glides, the cloth gets the glide's air:
    // gravity leans from straight down towards the wearer's back (CapeStream), the wind shakes it harder
    // (CapeFlutter, CapeTurbulence, CapeFrequency) and the flight's own speed blows on it (CapeMovingWind).
    // The game rewrites the wind every frame (PlayerClothWindShelter.Update), so this runs right after it; when
    // the sail closes, the cloth gets back its own values.
    [HarmonyPatch]
    internal static class CapeWind
    {
        private class Orig { public float g, turb, freq, moving; public float3 dir; }
        private static readonly Dictionary<MagicaCloth, Orig> _orig = new Dictionary<MagicaCloth, Orig>();

        [HarmonyPatch(typeof(PlayerClothWindShelter), "Update"), HarmonyPostfix]
        private static void After(Player ___m_player, MagicaCloth ___m_cloth)
        {
            Player p = ___m_player;
            MagicaCloth c = ___m_cloth;
            if (p == null || c == null) return;
            GlideView v = p.GetComponent<GlideView>();
            float w = v != null ? v.Weight : 0f;
            Orig o;
            bool had = _orig.TryGetValue(c, out o);
            ClothSerializeData sd = c.SerializeData;
            if (w <= 0f)
            {
                if (!had) return;
                sd.gravity = o.g; sd.gravityDirection = o.dir;
                sd.wind.turbulence = o.turb; sd.wind.frequency = o.freq; sd.wind.movingWind = o.moving;
                c.SetParameterChange();
                _orig.Remove(c);
                return;
            }
            if (!had)
            {
                o = new Orig { g = sd.gravity, dir = sd.gravityDirection, turb = sd.wind.turbulence, freq = sd.wind.frequency, moving = sd.wind.movingWind };
                _orig[c] = o;
                Plugin.Debug("[cape] cloth " + c.name + ": gravity " + o.g.ToString("0.##") + ", wind " + sd.wind.influence.ToString("0.##") + ", turbulence " + o.turb.ToString("0.##")
                    + ", frequency " + o.freq.ToString("0.##") + ", moving wind " + o.moving.ToString("0.##") + ", damping " + sd.damping.value.ToString("0.###"));
            }
            // straight down -> the wearer's back, as the sail opens
            Vector3 down = new Vector3(o.dir.x, o.dir.y, o.dir.z);
            if (down.sqrMagnitude < 0.01f) down = Vector3.down;
            Vector3 dir = Vector3.Slerp(down.normalized, -p.transform.forward, Plugin.CapeStream.Value * w).normalized;
            sd.gravityDirection = new float3(dir.x, dir.y, dir.z);
            sd.gravity = Mathf.Lerp(o.g, o.g * Plugin.CapeGravity.Value, w);
            sd.wind.influence = Mathf.Lerp(sd.wind.influence, Plugin.CapeFlutter.Value, w);
            sd.wind.turbulence = Mathf.Lerp(o.turb, Plugin.CapeTurbulence.Value, w);
            sd.wind.frequency = Mathf.Lerp(o.freq, Plugin.CapeFrequency.Value, w);
            sd.wind.movingWind = Mathf.Lerp(o.moving, Plugin.CapeMovingWind.Value, w);
            c.SetParameterChange();
        }
    }
}
