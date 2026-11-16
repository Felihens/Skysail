using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Skysail
{
    // For the pose picker: plays every game emote on your character, one after the other, and saves how the
    // body and the cape look in each (the game's own animation and the game's own cape) to
    // BepInEx/config/Skysail_editor/poses.json. One-shot emotes are saved at three moments. Runs once while
    // ExportPoses is on, after you stand still for 3 s; moving stops it.
    [HarmonyPatch]
    internal static class PoseExport
    {
        private static float _stillSince = -1f;
        private static bool _running;

        [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
        private static void Tick(Player __instance)
        {
            if (__instance != Player.m_localPlayer || _running || !Plugin.ExportPoses.Value) return;
            Player p = __instance;
            Rigidbody rb = p.GetComponent<Rigidbody>();
            bool still = p.IsOnGround() && !p.IsAttached() && !p.InWater() && rb != null && rb.linearVelocity.magnitude < 0.1f;
            if (!still) { _stillSince = -1f; return; }
            if (_stillSince < 0f) { _stillSince = Time.time; return; }
            if (Time.time - _stillSince < 3f) return;
            Plugin.ExportPoses.Value = false;
            _running = true;
            p.Message(MessageHud.MessageType.Center, "Skysail: saving the emote poses, stand still...");
            p.StartCoroutine(Run(p));
        }

        private static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }

        private static IEnumerator Run(Player p)
        {
            Transform root = p.transform;
            Vector3 startPos = root.position;
            System.Reflection.MethodInfo doEmote = AccessTools.Method(typeof(Emote), "DoEmote");
            System.Reflection.MethodInfo stopEmote = AccessTools.Method(typeof(Character), "StopEmote");
            List<SkinnedMeshRenderer> sks = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer sk in p.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (sk.enabled && sk.sharedMesh != null) sks.Add(sk);

            StringBuilder poses = new StringBuilder();
            int count = 0;
            bool aborted = false;
            Snap(poses, "stand", sks, root, ref count);
            foreach (object e in Enum.GetValues(typeof(Emotes)))
            {
                string name = e.ToString();
                bool oneshot = true;
                System.Reflection.FieldInfo fi = typeof(Emotes).GetField(name);
                object[] at = fi != null ? fi.GetCustomAttributes(typeof(Emote), false) : new object[0];
                if (at.Length > 0) oneshot = ((Emote)at[0]).OneShot;
                try { if (doEmote != null) doEmote.Invoke(null, new object[] { e }); else p.StartEmote(name.ToLower(), oneshot); }
                catch (Exception ex) { Plugin.Debug("[poses] " + name + ": " + ex.Message); continue; }
                float[] moments = oneshot ? new float[] { 0.5f, 1f, 1.6f } : new float[] { 1.8f };
                float t0 = Time.time;
                foreach (float m in moments)
                {
                    while (Time.time - t0 < m) yield return null;
                    if ((root.position - startPos).sqrMagnitude > 0.25f || p.IsDead()) { aborted = true; break; }
                    Snap(poses, oneshot ? name + " " + m.ToString("0.0", CultureInfo.InvariantCulture) + "s" : name, sks, root, ref count);
                }
                if (stopEmote != null) stopEmote.Invoke(p, null);
                if (aborted) break;
                float t1 = Time.time;
                while (Time.time - t1 < 0.6f) yield return null;
            }
            _running = false;
            if (aborted) { p.Message(MessageHud.MessageType.Center, "Skysail: you moved, the poses were not saved"); Plugin.ExportPoses.Value = true; yield break; }

            // what doesn't change between poses: triangles, uvs, textures (once per part)
            StringBuilder sb = new StringBuilder("{\"parts\":[");
            Dictionary<Texture, string> texCache = new Dictionary<Texture, string>();
            for (int s = 0; s < sks.Count; s++)
            {
                SkinnedMeshRenderer sk = sks[s];
                if (s > 0) sb.Append(",");
                Mesh baked = new Mesh();
                sk.BakeMesh(baked, true);
                Vector2[] uv = baked.uv;
                int[] tri = baked.triangles;
                Object.Destroy(baked);
                sb.Append("{\"name\":\"").Append(sk.name).Append("\",\"uv\":[");
                for (int i = 0; i < uv.Length; i++) { if (i > 0) sb.Append(","); sb.Append(F(uv[i].x)).Append(",").Append(F(uv[i].y)); }
                sb.Append("],\"t\":[");
                for (int i = 0; i < tri.Length; i++) { if (i > 0) sb.Append(","); sb.Append(tri[i]); }
                sb.Append("]");
                Material mat = sk.sharedMaterial;
                Texture tex = mat != null && mat.HasProperty("_MainTex") ? mat.mainTexture : null;
                if (tex != null)
                {
                    string png;
                    if (!texCache.TryGetValue(tex, out png)) texCache[tex] = png = Exporter.Png(tex, 512);
                    if (png != null) sb.Append(",\"tex\":\"").Append(png).Append("\"");
                }
                if (mat != null && mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    sb.Append(",\"col\":[").Append(F(c.r)).Append(",").Append(F(c.g)).Append(",").Append(F(c.b)).Append("]");
                }
                sb.Append("}");
            }
            sb.Append("],\"poses\":[").Append(poses.ToString()).Append("]}");
            string dir = Path.Combine(BepInEx.Paths.ConfigPath, "Skysail_editor");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "poses.json");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo("[poses] " + count + " poses saved to " + path);
            p.Message(MessageHud.MessageType.Center, "Skysail: " + count + " poses saved for the pose picker");
        }

        // every part as drawn right now, in the player's own frame
        private static void Snap(StringBuilder sb, string name, List<SkinnedMeshRenderer> sks, Transform root, ref int count)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            if (count > 0) sb.Append(",");
            sb.Append("{\"name\":\"").Append(name).Append("\",\"v\":[");
            for (int s = 0; s < sks.Count; s++)
            {
                SkinnedMeshRenderer sk = sks[s];
                if (s > 0) sb.Append(",");
                sb.Append("[");
                if (sk != null)
                {
                    Mesh baked = new Mesh();
                    sk.BakeMesh(baked, true);
                    Matrix4x4 mx = toRoot * sk.transform.localToWorldMatrix;
                    Vector3[] v = baked.vertices;
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 q = mx.MultiplyPoint3x4(v[i]);
                        if (i > 0) sb.Append(",");
                        sb.Append(F(q.x)).Append(",").Append(F(q.y)).Append(",").Append(F(q.z));
                    }
                    Object.Destroy(baked);
                }
                sb.Append("]");
            }
            sb.Append("]}");
            count++;
        }
    }
}
