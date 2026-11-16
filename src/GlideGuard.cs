using HarmonyLib;
using UnityEngine;

namespace Skysail
{
    // Two small rules around the glide:
    //  - while the sail is open the body takes no walking input (no side, back or run steps that bend the held
    //    pose); the flight itself still turns with the camera and reads W (dive) and S (brake) on its own;
    //  - after the sail closes in the air, the next fall hurts half as much (SoftFallShare), until you touch the
    //    ground: the first fall damage or the landing ends it, whichever comes first.
    [HarmonyPatch]
    internal static class GlideGuard
    {
        internal static bool SoftFall;
        private static float _groundSince = -1f;

        [HarmonyPatch(typeof(Player), "SetControls"), HarmonyPrefix]
        private static void Controls(Player __instance, ref Vector3 movedir, ref bool crouch, ref bool run, ref bool autoRun, ref bool dodge)
        {
            if (__instance != Player.m_localPlayer || Flight.OpenTier == 0) return;
            movedir = Vector3.zero;
            crouch = run = autoRun = dodge = false;
        }

        // the sail closed with air under you
        internal static void Closed(string why)
        {
            if (why == "landed" || why == "water" || why == "attached" || why == "dead") return;
            SoftFall = true;
            _groundSince = -1f;
        }

        [HarmonyPatch(typeof(Character), "Damage"), HarmonyPrefix]
        private static void Damage(Character __instance, HitData hit)
        {
            if (!SoftFall || hit == null || __instance != Player.m_localPlayer || hit.m_hitType != HitData.HitType.Fall) return;
            float was = hit.m_damage.m_damage;
            hit.m_damage.m_damage *= Plugin.SoftFallShare.Value;
            SoftFall = false;
            Plugin.Debug("[glide] fall after the sail: " + was.ToString("0") + " -> " + hit.m_damage.m_damage.ToString("0") + " damage");
        }

        // touching the ground ends it (the landing's fall damage comes first, in the same physics step)
        [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
        private static void Tick(Player __instance)
        {
            if (!SoftFall || __instance != Player.m_localPlayer || Flight.OpenTier > 0) return;
            if (!__instance.IsOnGround() && !__instance.InWater()) { _groundSince = -1f; return; }
            if (_groundSince < 0f) { _groundSince = Time.time; return; }
            if (Time.time - _groundSince > 0.3f) { SoftFall = false; Plugin.Debug("[glide] landed, soft fall over"); }
        }
    }
}
