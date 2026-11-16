using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Skysail
{
    // How a glider looks, on every player (us from the flight state, others from their ZDO). Nothing is built:
    // the game's own fall animation plays (we are in the air), held still a moment after it starts
    // (HoldAfter), with the editor's turns added (PoseTurns), and the body leans forward by tier (Plugin.TiltFor). The
    // cape is the game's own cloth; CapeWind gives it the glide's air.
    internal class GlideView : MonoBehaviour
    {

        private Player _p;
        private ZNetView _nv;
        private Animator _anim;
        private Transform _hips;
        private int _tier;
        private float _next, _open, _airT, _glideT;
        private bool _held;
        private Transform[] _bones;        // the skeleton under the hips
        private Quaternion[] _pose;        // the held fall pose (local rotations)
        private WindLines _wind;
        // from the glide editor: when the fall pose is held, and turns added to it (Bone=x,y,z degrees; ...)
        private const float HoldAfter = 1f;
        private const string PoseTurns = "";
        private Quaternion[] _off;         // PoseTurns per bone
        private string _offFrom;

        // 0 on the ground, 1 once the sail is fully open
        internal float Weight { get { return _tier > 0 ? Mathf.SmoothStep(0f, 1f, _open) : 0f; } }

        private void ReadOffsets()
        {
            string cfg = PoseTurns;
            if (_off != null && _offFrom == cfg && _off.Length == _bones.Length) return;
            _offFrom = cfg;
            Dictionary<string, Quaternion> map = new Dictionary<string, Quaternion>();
            foreach (string part in cfg.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string[] v = part.Substring(eq + 1).Split(',');
                float x, y, z;
                if (v.Length != 3 || !float.TryParse(v[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(v[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                    || !float.TryParse(v[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z)) { Plugin.Log.LogWarning("PoseTurns: can't read '" + part + "'"); continue; }
                map[part.Substring(0, eq).Trim()] = Quaternion.Euler(x, y, z);
            }
            _off = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                Quaternion q;
                _off[i] = _bones[i] != null && map.TryGetValue(_bones[i].name, out q) ? q : Quaternion.identity;
            }
            if (map.Count > 0) Plugin.Debug("[glide] pose turns: " + cfg);
        }

        private void Awake()
        {
            _p = GetComponent<Player>();
            _nv = GetComponent<ZNetView>();
        }

        private void Update()
        {
            int tier;
            if (_p == Player.m_localPlayer) tier = Flight.OpenTier;
            else
            {
                if (Time.time < _next) return;
                _next = Time.time + 0.2f;
                ZDO zdo = _nv != null ? _nv.GetZDO() : null;
                tier = zdo != null ? zdo.GetInt(Flight.ZdoKey, 0) : 0;
            }
            tier = Mathf.Clamp(tier, 0, Sails.Count);
            if (tier == _tier) return;
            _tier = tier;
            if (tier == 0) Release();
            else { _open = 0f; _glideT = 0f; }
        }

        private bool Find()
        {
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_hips == null) { _hips = Utils.FindChild(transform, "Hips"); if (_hips != null) _bones = _hips.GetComponentsInChildren<Transform>(true); }
            return _anim != null && _hips != null;
        }

        // the animation moves again
        private void Release()
        {
            if (_wind != null) _wind.Set(0f);
            _held = false;
            Active.Remove(this);
        }

        private void OnDestroy() { Release(); }

        // The cape's cloth (MagicaCloth) reads the skeleton before LateUpdate, so the pose is put on just before
        // its simulation (OnPreSimulation); LateUpdate puts it on again only if the animation wrote over it.
        private static readonly List<GlideView> Active = new List<GlideView>();
        private static bool _hooked;
        private int _poseFrame = -1, _preCount, _lateCount;
        private Quaternion _leftHips;

        private static void PreSimulation()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                GlideView v = Active[i];
                if (v == null) { Active.RemoveAt(i); continue; }
                if (v._tier > 0 && v.Find()) { v.ApplyPose(); v._preCount++; }
            }
        }

        private void ApplyPose()
        {
            // hold the fall pose once the fall animation has played for a moment (copied from the animation)
            float hold = HoldAfter;
            if (!_held && hold >= 0f)
            {
                float t = _p == Player.m_localPlayer ? _airT : _glideT;
                if (t >= hold)
                {
                    _pose = new Quaternion[_bones.Length];
                    for (int i = 0; i < _bones.Length; i++) _pose[i] = _bones[i] != null ? _bones[i].localRotation : Quaternion.identity;
                    _held = true;
                    Plugin.Debug("[glide] fall pose held, " + t.ToString("0.00") + " s after leaving the ground");
                }
            }
            if (_held)
            {
                ReadOffsets();
                for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _bones[i].localRotation = _pose[i] * _off[i];
            }
            // lean the body forward
            float w = Mathf.SmoothStep(0f, 1f, _open);
            _hips.rotation = Quaternion.AngleAxis(Plugin.TiltFor(_tier) * w, transform.right) * _hips.rotation;
            _poseFrame = Time.frameCount;
            _leftHips = _hips.localRotation;
        }

        private void LateUpdate()
        {
            if (!_hooked) { _hooked = true; MagicaCloth2.MagicaManager.OnPreSimulation += PreSimulation; }
            float dt = Time.deltaTime;
            // time since leaving the ground (our own player; others: since their sail opened)
            if (_p == Player.m_localPlayer && _p.IsOnGround()) _airT = 0f; else _airT += dt;
            if (_tier == 0 || !Find()) return;
            if (!Active.Contains(this)) { Active.Add(this); _preCount = _lateCount = 0; }
            _glideT += dt;
            _open = Mathf.MoveTowards(_open, 1f, dt * 2f);

            if (_poseFrame != Time.frameCount || _hips.localRotation != _leftHips) { ApplyPose(); _lateCount++; }
            if (_glideT > 2f && _glideT - dt <= 2f)
                Plugin.Debug("[glide] pose put on before the cloth " + _preCount + " times, after it " + _lateCount + " times (first 2 s)");

            // wind streaks, more with higher tiers
            if (Plugin.WindLines.Value)
            {
                if (_wind == null) _wind = WindLines.On(transform);
                _wind.Set(_tier);
            }
            else if (_wind != null) _wind.Set(0f);
        }
    }

    [HarmonyPatch]
    internal static class GlideHook
    {
        [HarmonyPatch(typeof(Player), "Awake"), HarmonyPostfix]
        private static void PlayerAwake(Player __instance)
        {
            if (__instance.GetComponent<GlideView>() == null) __instance.gameObject.AddComponent<GlideView>();
        }
    }
}
