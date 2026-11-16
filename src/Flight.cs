using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Skysail
{
    // The local player's glide: Jump in the air opens the sail, Jump again (or the ground, water, no stamina) closes it.
    // While open we steer the body's velocity towards "forward at the tier's speed, down at the tier's sink rate".
    [HarmonyPatch]
    internal static class Flight
    {
        internal const string ZdoKey = "Skysail_glide";   // int: the open sail's tier, 0 = closed

        private static readonly FieldInfo ShoulderF = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");
        private static readonly FieldInfo AirF = AccessTools.Field(typeof(Character), "m_maxAirAltitude");

        private static ItemDrop.ItemData ShoulderItem(Player p) { return ShoulderF != null ? ShoulderF.GetValue(p) as ItemDrop.ItemData : null; }
        private static void ResetFall(Player p) { if (AirF != null) AirF.SetValue(p, p.transform.position.y); }
        private static readonly MethodInfo TakeInputM = AccessTools.Method(typeof(Player), "TakeInput");

        internal static int OpenTier;          // 0 = not gliding
        private static Vector3 _dir = Vector3.forward;
        private static Vector3 _vel;
        private static float _openedAt;
        private static int _ground;

        internal static int Equipped(Player p)
        {
            return p == null ? 0 : Sails.TierOf(ShoulderItem(p));
        }

        private static bool CanInput(Player p)
        {
            try { return TakeInputM == null || (bool)TakeInputM.Invoke(p, null); }
            catch (Exception) { return true; }
        }

        private static bool JumpDown()
        {
            return ZInput.GetButtonDown("Jump") || ZInput.GetButtonDown("JoyJump");
        }

        private static int GroundMask()
        {
            if (_ground == 0) _ground = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            return _ground;
        }

        // metres of free air under the player (up to 100)
        internal static float Height(Player p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p.transform.position + Vector3.up * 0.3f, Vector3.down, out hit, 100f, GroundMask(), QueryTriggerInteraction.Ignore))
                return hit.distance - 0.3f;
            return 100f;
        }

        [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
        private static void PlayerUpdate(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            Player p = __instance;
            int tier = Equipped(p);
            try
            {
                if (OpenTier > 0)
                {
                    string why = null;
                    if (tier == 0) why = "sail taken off";
                    else if (p.IsDead()) why = "dead";
                    else if (p.IsOnGround() && Time.time - _openedAt > 0.3f) why = "landed";
                    else if (Time.time - _openedAt > 0.3f && Height(p) <= Plugin.LandHeight.Value) why = "landed";   // the game's own landing from here: soft
                    else if (p.InWater()) why = "water";
                    else if (p.IsAttached()) why = "attached";
                    else if (p.GetStamina() <= 0f) why = "out of stamina";
                    else if (JumpDown() && CanInput(p) && Time.time - _openedAt > 0.2f) why = "closed";
                    if (why != null) { Close(p, why); return; }
                    if (tier != OpenTier) { OpenTier = tier; SetZdo(p, tier); }
                    p.UseStamina(Sails.Tiers[OpenTier].Stamina * Time.deltaTime);
                }
                else if (tier > 0 && JumpDown() && CanInput(p) && !p.IsOnGround() && !p.InWater() && !p.IsAttached() && !p.IsDead())
                {
                    float h = Height(p);
                    if (h < Plugin.MinHeight.Value) { Plugin.Debug("[glide] too low to open: " + h.ToString("0.0") + " m (MinHeight " + Plugin.MinHeight.Value + ")"); return; }
                    if (p.GetStamina() < Plugin.MinStamina.Value)
                    {
                        if (Hud.instance != null) Hud.instance.StaminaBarEmptyFlash();
                        p.Message(MessageHud.MessageType.Center, "Not enough stamina to open the sail");
                        return;
                    }
                    Open(p, tier, h);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Update: " + e.Message); }
        }

        private static void Open(Player p, int tier, float h)
        {
            OpenTier = tier;
            _openedAt = Time.time;
            Rigidbody rb = p.GetComponent<Rigidbody>();
            _vel = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 f = p.transform.forward;
            f.y = 0f;
            _dir = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            SetZdo(p, tier);
            Plugin.Debug("[glide] open tier " + tier + " at " + h.ToString("0.0") + " m above ground");
        }

        internal static void Close(Player p, string why)
        {
            if (OpenTier == 0) return;
            OpenTier = 0;
            if (p != null)
            {
                ResetFall(p);   // no fall damage from the height we glided down
                SetZdo(p, 0);
            }
            GlideGuard.Closed(why);
            Plugin.Debug("[glide] close: " + why);
        }

        private static void SetZdo(Player p, int tier)
        {
            ZNetView nv = p.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            if (zdo != null && nv.IsOwner()) zdo.Set(ZdoKey, tier);
        }

        // after the game has moved the player this tick, we set the glide velocity
        [HarmonyPatch(typeof(Player), "FixedUpdate"), HarmonyPostfix]
        private static void PlayerFixedUpdate(Player __instance)
        {
            if (OpenTier == 0 || __instance != Player.m_localPlayer) return;
            Player p = __instance;
            Rigidbody rb = p.GetComponent<Rigidbody>();
            if (rb == null) return;
            Tier t = Sails.Tiers[OpenTier];
            float dt = Time.fixedDeltaTime;

            // the heading turns towards where the camera looks, at the tier's turn rate
            if (GameCamera.instance != null)
            {
                Vector3 look = GameCamera.instance.transform.forward;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    _dir = Vector3.RotateTowards(_dir, look.normalized, t.Turn * Mathf.Deg2Rad * dt, 0f).normalized;
            }

            float fwd = t.Forward, down = t.Descent;
            bool dive = ZInput.GetButton("Forward") || ZInput.GetJoyLeftStickY() < -0.5f;
            bool brake = ZInput.GetButton("Backward") || ZInput.GetJoyLeftStickY() > 0.5f;
            if (!CanInput(p)) dive = brake = false;
            if (dive) { fwd *= 1.35f; down *= 1.8f; }
            else if (brake) { fwd *= 0.5f; down *= 0.8f; }

            // soft landing: the last couple of metres come down gently
            if (Height(p) < 2f) down = Mathf.Max(down, -1f);

            Vector3 target = _dir * fwd + Vector3.up * down;
            _vel = Vector3.Lerp(_vel, target, 1f - Mathf.Exp(-2.5f * dt));
            rb.linearVelocity = _vel;
            ResetFall(p);
            rb.MoveRotation(Quaternion.LookRotation(_dir, Vector3.up));
        }

        // leaving the world or logging out with the sail open
        [HarmonyPatch(typeof(Player), "OnDestroy"), HarmonyPrefix]
        private static void PlayerDestroy(Player __instance)
        {
            if (__instance == Player.m_localPlayer) OpenTier = 0;
        }
    }
}
