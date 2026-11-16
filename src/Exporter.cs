using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Skysail
{
    // For the sail editor: saves your character standing still (mesh, textures and skeleton) to
    // BepInEx/config/Skysail_editor/player.json, in the player's own frame (Unity axes). Runs once while
    // ExportPlayer is on, a few seconds after you stand still on the ground.
    [HarmonyPatch]
    internal static class Exporter
    {
        private static float _stillSince = -1f;
        private static bool _capesDone;
        private static float _air, _fallFor;
        private static bool _fallSaved;   // this fall is already saved
        private static readonly int FallingTag = Animator.StringToHash("falling");

        [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
        private static void Tick(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (!_capesDone && ObjectDB.instance != null)
            {
                _capesDone = true;
                try { Plugin.Log.LogInfo("[export] cape textures saved to " + SaveCapes()); }
                catch (Exception e) { Plugin.Log.LogWarning("[export] capes: " + e); }
            }
            FallTick(__instance);
            if (!Plugin.ExportPlayer.Value) return;
            Player p = __instance;
            Rigidbody rb = p.GetComponent<Rigidbody>();
            bool still = p.IsOnGround() && !p.IsAttached() && !p.InWater() && rb != null && rb.linearVelocity.magnitude < 0.1f;
            if (!still) { _stillSince = -1f; return; }
            if (_stillSince < 0f) { _stillSince = Time.time; return; }
            if (Time.time - _stillSince < 3f) return;
            Plugin.ExportPlayer.Value = false;
            try
            {
                string path = Save(p);
                Plugin.Log.LogInfo("[export] character saved to " + path);
                p.Message(MessageHud.MessageType.Center, "Skysail: your character was saved for the sail editor");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[export] " + e); }
        }

        // the fall pose, on every fall without the sail open: saved 1 s after leaving the ground, or just before
        // landing if the jump is shorter (any jump). Each fall replaces the last in Skysail_editor/fall.json.
        private static string _lastSnap;
        private static float _snapAt;

        private static void FallTick(Player p)
        {
            if (!Plugin.ExportFall.Value) return;
            if (Flight.OpenTier > 0) _fallSaved = true;   // the sail opened: not a plain fall
            bool inAir = !p.IsOnGround() && !p.InWater() && !p.IsAttached();
            if (!inAir)
            {
                if (_air > 0.15f)
                {
                    Plugin.Debug("[fall] " + _air.ToString("0.00") + " s in the air, " + _fallFor.ToString("0.00") + " s in the fall animation" + (_fallSaved ? "" : ", saving the pose from just before landing"));
                    if (!_fallSaved && _lastSnap != null) Write(p, _lastSnap, "just before landing");
                }
                _air = 0f; _fallFor = 0f; _fallSaved = false; _lastSnap = null;
                return;
            }
            _air += Time.deltaTime;
            Animator a = p.GetComponentInChildren<Animator>();
            if (a != null && a.GetCurrentAnimatorStateInfo(0).tagHash == FallingTag) _fallFor += Time.deltaTime;
            if (_fallSaved) return;
            if (_air >= 1f) { Write(p, SaveFall(p), "1 s into the fall"); _fallSaved = true; return; }
            if (_air >= 0.1f && Time.time >= _snapAt) { _lastSnap = SaveFall(p); _snapAt = Time.time + 0.1f; }
        }

        private static void Write(Player p, string json, string when)
        {
            try
            {
                string dir = Path.Combine(BepInEx.Paths.ConfigPath, "Skysail_editor");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "fall.json");
                File.WriteAllText(path, json);
                Plugin.Log.LogInfo("[export] fall pose saved to " + path + " (" + when + ")");
                p.Message(MessageHud.MessageType.Center, "Skysail: fall pose saved for the editor");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[export] fall: " + e); }
        }

        private static string SaveFall(Player p)
        {
            Transform root = p.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            Quaternion rootInv = Quaternion.Inverse(root.rotation);
            List<Transform> bones = new List<Transform>();
            HashSet<Transform> seen = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer sk in p.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (sk.bones != null)
                    foreach (Transform b in sk.bones)
                        for (Transform t = b; t != null && t != root; t = t.parent)
                            if (seen.Add(t)) bones.Add(t);
            StringBuilder sb = new StringBuilder("{\"bones\":[");
            for (int i = 0; i < bones.Count; i++)
            {
                Transform b = bones[i];
                Vector3 pos = toRoot.MultiplyPoint3x4(b.position);
                Quaternion rot = rootInv * b.rotation;
                if (i > 0) sb.Append(",");
                sb.Append("{\"n\":\"").Append(b.name).Append("\",\"pos\":[").Append(F(pos.x)).Append(",").Append(F(pos.y)).Append(",").Append(F(pos.z))
                  .Append("],\"rot\":[").Append(F(rot.x)).Append(",").Append(F(rot.y)).Append(",").Append(F(rot.z)).Append(",").Append(F(rot.w)).Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string F(float f) { return f.ToString("0.####", CultureInfo.InvariantCulture); }

        private static string Save(Player p)
        {
            Transform root = p.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            Quaternion rootInv = Quaternion.Inverse(root.rotation);

            List<SkinnedMeshRenderer> sks = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer sk in p.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (sk.enabled && sk.sharedMesh != null && sk.bones != null && sk.bones.Length > 0) sks.Add(sk);

            // the skeleton: every bone any part uses, plus their ancestors up to the player
            List<Transform> bones = new List<Transform>();
            Dictionary<Transform, int> index = new Dictionary<Transform, int>();
            Action<Transform> add = null;
            add = t =>
            {
                if (t == null || t == root || index.ContainsKey(t)) return;
                add(t.parent);
                index[t] = bones.Count;
                bones.Add(t);
            };
            foreach (SkinnedMeshRenderer sk in sks) foreach (Transform b in sk.bones) add(b);

            // parts first (each cape below may add bones), then the skeleton
            StringBuilder ps = new StringBuilder();
            Dictionary<Texture, string> texCache = new Dictionary<Texture, string>();
            for (int s = 0; s < sks.Count; s++)
            {
                if (s > 0) ps.Append(",");
                Part(ps, sks[s], toRoot, bones, index, texCache);
            }
            StringBuilder cs = new StringBuilder();
            AllCapes(p, cs, toRoot, bones, index, add, texCache);

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(p.GetPlayerName().Replace("\"", "")).Append("\",\"bones\":[");
            for (int i = 0; i < bones.Count; i++)
            {
                Transform b = bones[i];
                int parent = b.parent != null && index.ContainsKey(b.parent) ? index[b.parent] : -1;
                Vector3 pos = toRoot.MultiplyPoint3x4(b.position);
                Quaternion rot = rootInv * b.rotation;
                Quaternion lr = b.localRotation;
                if (i > 0) sb.Append(",");
                sb.Append("{\"n\":\"").Append(b.name).Append("\",\"p\":").Append(parent)
                  .Append(",\"pos\":[").Append(F(pos.x)).Append(",").Append(F(pos.y)).Append(",").Append(F(pos.z))
                  .Append("],\"rot\":[").Append(F(rot.x)).Append(",").Append(F(rot.y)).Append(",").Append(F(rot.z)).Append(",").Append(F(rot.w))
                  .Append("],\"lr\":[").Append(F(lr.x)).Append(",").Append(F(lr.y)).Append(",").Append(F(lr.z)).Append(",").Append(F(lr.w)).Append("]}");
            }
            sb.Append("],\"parts\":[").Append(ps.ToString()).Append("],\"capes\":[").Append(cs.ToString()).Append("]}");
            string dir = Path.Combine(BepInEx.Paths.ConfigPath, "Skysail_editor");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "player.json");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        // one skinned part (mesh as drawn now, uvs, triangles, main bone per vertex, texture, colour)
        private static void Part(StringBuilder sb, SkinnedMeshRenderer sk, Matrix4x4 toRoot, List<Transform> bones, Dictionary<Transform, int> index, Dictionary<Texture, string> texCache)
        {
            Part(sb, sk, toRoot, bones, index, texCache, false);
        }

        private static void Part(StringBuilder sb, SkinnedMeshRenderer sk, Matrix4x4 toRoot, List<Transform> bones, Dictionary<Transform, int> index, Dictionary<Texture, string> texCache, bool sheet)
        {
            Mesh baked = new Mesh();
            sk.BakeMesh(baked, true);
            Matrix4x4 mx = toRoot * sk.transform.localToWorldMatrix;
            Vector3[] v = baked.vertices;
            Vector2[] uv = baked.uv;
            int[] tri = baked.triangles;
            int[] vb = VertexBones(sk, v, mx, bones, index);
            sb.Append("{\"name\":\"").Append(sk.name).Append("\",");
            if (sheet) sb.Append("\"cloth\":true,\"mesh\":\"").Append(sk.sharedMesh.name).Append("\",");
            sb.Append("\"v\":[");
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 q = mx.MultiplyPoint3x4(v[i]);
                if (i > 0) sb.Append(",");
                sb.Append(F(q.x)).Append(",").Append(F(q.y)).Append(",").Append(F(q.z));
            }
            sb.Append("],\"uv\":[");
            for (int i = 0; i < uv.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(F(uv[i].x)).Append(",").Append(F(uv[i].y));
            }
            sb.Append("],\"t\":[");
            for (int i = 0; i < tri.Length; i++) { if (i > 0) sb.Append(","); sb.Append(tri[i]); }
            sb.Append("],\"b\":[");
            for (int i = 0; i < vb.Length; i++) { if (i > 0) sb.Append(","); sb.Append(vb[i]); }
            sb.Append("]");
            Material mat = sk.sharedMaterial;
            Texture tex = mat != null && mat.HasProperty("_MainTex") ? mat.mainTexture : null;
            if (tex != null)
            {
                string png;
                if (!texCache.TryGetValue(tex, out png)) texCache[tex] = png = Png(tex, 512);
                if (png != null) sb.Append(",\"tex\":\"").Append(png).Append("\"");
            }
            if (mat != null && mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                sb.Append(",\"col\":[").Append(F(c.r)).Append(",").Append(F(c.g)).Append(",").Append(F(c.b)).Append("]");
            }
            sb.Append("}");
            Object.Destroy(baked);
        }

        // every vanilla cape, put on the character for an instant (same frame, so nobody sees it) and saved
        // like the body, so the capes editor can pin each one. The cape you wear comes back right after.
        private static void AllCapes(Player p, StringBuilder sb, Matrix4x4 toRoot, List<Transform> bones, Dictionary<Transform, int> index, Action<Transform> add, Dictionary<Texture, string> texCache)
        {
            VisEquipment ve = p.GetComponent<VisEquipment>();
            if (ve == null) return;
            System.Reflection.MethodInfo equip = AccessTools.Method(typeof(VisEquipment), "SetShoulderEquipped");
            System.Reflection.FieldInfo inst = AccessTools.Field(typeof(VisEquipment), "m_shoulderItemInstances");
            System.Reflection.FieldInfo hashF = AccessTools.Field(typeof(VisEquipment), "m_currentShoulderItemHash");
            System.Reflection.FieldInfo varF = AccessTools.Field(typeof(VisEquipment), "m_currentShoulderItemVariant");
            System.Reflection.FieldInfo qualF = AccessTools.Field(typeof(VisEquipment), "m_currentShoulderItemQuality");
            if (equip == null || inst == null || hashF == null) { Plugin.Log.LogWarning("[export] capes: VisEquipment changed"); return; }
            int oldHash = (int)hashF.GetValue(ve);
            int oldVar = varF != null ? (int)varF.GetValue(ve) : 0;
            int oldQual = qualF != null ? (int)qualF.GetValue(ve) : 1;
            bool first = true;
            int count = 0;
            HashSet<string> seen = new HashSet<string>();
            try
            {
                foreach (GameObject item in ObjectDB.instance.m_items)
                {
                    ItemDrop d = item != null ? item.GetComponent<ItemDrop>() : null;
                    if (d == null || d.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shoulder) continue;
                    if (item.name.StartsWith("Skysail_")) continue;
                    equip.Invoke(ve, new object[] { item.name.GetStableHashCode(), 0, 1 });
                    List<GameObject> objs = inst.GetValue(ve) as List<GameObject>;
                    if (objs == null) continue;
                    List<SkinnedMeshRenderer> rs = new List<SkinnedMeshRenderer>();
                    foreach (GameObject o in objs)
                        if (o != null)
                            foreach (SkinnedMeshRenderer r in o.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                if (r.sharedMesh != null && r.bones != null && r.bones.Length > 0) rs.Add(r);
                    // the sheet: the renderer with a Cloth on it, else the biggest (the same rule as CapeGlide.FindCloth)
                    SkinnedMeshRenderer sheet = null;
                    foreach (SkinnedMeshRenderer r in rs) if (r.GetComponent<Cloth>() != null) { sheet = r; break; }
                    if (sheet == null)
                        foreach (SkinnedMeshRenderer r in rs)
                            if (sheet == null || r.sharedMesh.vertexCount > sheet.sharedMesh.vertexCount) sheet = r;
                    if (sheet == null) { Plugin.Debug("[export] " + item.name + " has no skinned mesh, skipped"); continue; }
                    string key = sheet.sharedMesh.name + "|" + (sheet.sharedMaterial != null ? sheet.sharedMaterial.name : "");
                    if (seen.Contains(key)) { Plugin.Debug("[export] " + item.name + " looks like a cape already saved, skipped"); continue; }
                    seen.Add(key);
                    Plugin.Debug("[export] " + item.name + ": sheet " + sheet.name + " (" + sheet.sharedMesh.vertexCount + " vertices" + (sheet.GetComponent<Cloth>() != null ? ", cloth" : ", no cloth") + ")");
                    foreach (SkinnedMeshRenderer r in rs) foreach (Transform b in r.bones) add(b);
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{\"item\":\"").Append(item.name).Append("\",\"label\":\"")
                      .Append(Localization.instance.Localize(d.m_itemData.m_shared.m_name).Replace("\"", "")).Append("\",\"parts\":[");
                    for (int i = 0; i < rs.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        Part(sb, rs[i], toRoot, bones, index, texCache, rs[i] == sheet);
                    }
                    sb.Append("]}");
                    count++;
                }
            }
            finally
            {
                equip.Invoke(ve, new object[] { 0, 0, 0 });
                equip.Invoke(ve, new object[] { oldHash, oldVar, oldQual });
            }
            Plugin.Log.LogInfo("[export] " + count + " capes saved for the capes editor");
        }

        // every vanilla cape's texture (the one it wears on your back), for the editor's "use the cape's leather"
        private static string SaveCapes()
        {
            StringBuilder sb = new StringBuilder("{");
            Dictionary<Texture, string> cache = new Dictionary<Texture, string>();
            bool first = true;
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                ItemDrop d = item != null ? item.GetComponent<ItemDrop>() : null;
                if (d == null || d.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shoulder) continue;
                if (item.name.StartsWith("Skysail_")) continue;
                Material m = null;
                foreach (SkinnedMeshRenderer r in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null) { m = r.sharedMaterial; break; }
                if (m == null) continue;
                string png;
                if (!cache.TryGetValue(m.mainTexture, out png)) cache[m.mainTexture] = png = Png(m.mainTexture, 512);
                if (png == null) continue;
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(item.name).Append("\":{\"name\":\"").Append(Localization.instance.Localize(d.m_itemData.m_shared.m_name).Replace("\"", ""))
                  .Append("\",\"tex\":\"").Append(png).Append("\"}");
            }
            sb.Append("}");
            string dir = Path.Combine(BepInEx.Paths.ConfigPath, "Skysail_editor");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "capes.json");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        // the bone that moves each vertex most (from the mesh's weights when readable, else the nearest bone)
        private static int[] VertexBones(SkinnedMeshRenderer sk, Vector3[] v, Matrix4x4 mx, List<Transform> bones, Dictionary<Transform, int> index)
        {
            int[] r = new int[v.Length];
            BoneWeight[] w = null;
            try { if (sk.sharedMesh.isReadable) w = sk.sharedMesh.boneWeights; } catch (Exception) { }
            Transform[] skb = sk.bones;
            if (w != null && w.Length == v.Length)
            {
                for (int i = 0; i < v.Length; i++)
                {
                    int bi = w[i].boneIndex0;
                    Transform b = bi >= 0 && bi < skb.Length ? skb[bi] : null;
                    r[i] = b != null && index.ContainsKey(b) ? index[b] : 0;
                }
                return r;
            }
            Matrix4x4 toRoot = mx * sk.transform.worldToLocalMatrix;   // world → root frame (mx = root ← mesh)
            List<Vector3> bp = new List<Vector3>();
            List<int> bi2 = new List<int>();
            foreach (Transform b in skb)
            {
                if (b == null || !index.ContainsKey(b)) continue;
                bp.Add(toRoot.MultiplyPoint3x4(b.position));
                bi2.Add(index[b]);
            }
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 q = mx.MultiplyPoint3x4(v[i]);
                float best = float.MaxValue;
                int bb = 0;
                for (int k = 0; k < bp.Count; k++)
                {
                    float d = (bp[k] - q).sqrMagnitude;
                    if (d < best) { best = d; bb = bi2[k]; }
                }
                r[i] = bb;
            }
            return r;
        }

        // a texture as base64 PNG, scaled down to at most 'max' pixels wide (works for unreadable textures too)
        internal static string Png(Texture tex, int max)
        {
            try
            {
                int w = tex.width, h = tex.height;
                if (w > max) { h = Mathf.Max(1, h * max / w); w = max; }
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                string s = Convert.ToBase64String(ImageConversion.EncodeToPNG(t));
                Object.Destroy(t);
                return s;
            }
            catch (Exception e)
            {
                Plugin.Debug("[export] texture " + tex.name + ": " + e.Message);
                return null;
            }
        }
    }
}
