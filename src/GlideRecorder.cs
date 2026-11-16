using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using MagicaCloth2;
using Object = UnityEngine.Object;

namespace Skysail
{
    // For the glide editor: while RecordGlide is on, the next glide is recorded once it has flown 0.3 s. For 4 s it
    // saves, every other frame and after everything moved (end of frame): every bone of the body and the cape, and
    // the cape exactly as the game draws it. Written to Skysail_editor/glide_rec.json, then RecordGlide turns off.
    [HarmonyPatch]
    internal static class GlideRecorder
    {
        private static readonly FieldInfo CapeF = AccessTools.Field(typeof(VisEquipment), "m_shoulderItemInstances");
        private static float _glidingSince = -1f;
        private static bool _running;

        [HarmonyPatch(typeof(Player), "Update"), HarmonyPostfix]
        private static void Tick(Player __instance)
        {
            if (__instance != Player.m_localPlayer || _running || !Plugin.RecordGlide.Value) return;
            if (Flight.OpenTier == 0) { _glidingSince = -1f; return; }
            if (_glidingSince < 0f) { _glidingSince = Time.time; return; }
            if (Time.time - _glidingSince < 0.3f) return;
            _running = true;
            __instance.Message(MessageHud.MessageType.TopLeft, "Skysail: recording the glide...");
            __instance.StartCoroutine(Record(__instance));
        }

        // how the game skins the cape: the mesh as made, up to 4 bones and weights per vertex, and each bone's bind
        // pose. With the recorded bones the editor then rebuilds exactly what the game draws, and can change it.
        private static void Skin(StringBuilder sb, SkinnedMeshRenderer sk, List<Transform> bones, Transform root)
        {
            Mesh m = sk != null ? sk.sharedMesh : null;
            if (m == null) return;
            if (!m.isReadable) { Plugin.Log.LogInfo("[record] the cape mesh " + m.name + " is not readable: no weights"); return; }
            Vector3[] v = m.vertices;
            BoneWeight[] w = m.boneWeights;
            Matrix4x4[] bind = m.bindposes;
            Transform[] sb2 = sk.bones;
            if (v.Length == 0 || w.Length != v.Length || sb2 == null || bind.Length != sb2.Length) { Plugin.Log.LogInfo("[record] cape skin data missing (" + v.Length + " vertices, " + w.Length + " weights, " + bind.Length + " bind poses)"); return; }
            sb.Append(",\"mv\":[");
            for (int i = 0; i < v.Length; i++) { if (i > 0) sb.Append(","); sb.Append(F(v[i].x)).Append(",").Append(F(v[i].y)).Append(",").Append(F(v[i].z)); }
            sb.Append("],\"bw\":[");
            for (int i = 0; i < w.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(w[i].boneIndex0).Append(",").Append(w[i].boneIndex1).Append(",").Append(w[i].boneIndex2).Append(",").Append(w[i].boneIndex3).Append(",")
                  .Append(F(w[i].weight0)).Append(",").Append(F(w[i].weight1)).Append(",").Append(F(w[i].weight2)).Append(",").Append(F(w[i].weight3));
            }
            sb.Append("],\"sb\":[");
            for (int j = 0; j < sb2.Length; j++) { if (j > 0) sb.Append(","); sb.Append(sb2[j] != null ? bones.IndexOf(sb2[j]) : -1); }
            sb.Append("],\"bind\":[");
            for (int j = 0; j < bind.Length; j++)
            {
                if (j > 0) sb.Append(",");
                sb.Append("[");
                for (int k = 0; k < 16; k++) { if (k > 0) sb.Append(","); sb.Append(bind[j][k / 4, k % 4].ToString("0.######", CultureInfo.InvariantCulture)); }
                sb.Append("]");
            }
            sb.Append("],\"smr\":[");
            Matrix4x4 smr = root.worldToLocalMatrix * sk.transform.localToWorldMatrix;
            for (int k = 0; k < 16; k++) { if (k > 0) sb.Append(","); sb.Append(smr[k / 4, k % 4].ToString("0.######", CultureInfo.InvariantCulture)); }
            sb.Append("]");
            Plugin.Log.LogInfo("[record] cape skin: " + v.Length + " vertices, " + sb2.Length + " bones");
        }

        private static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }

        private static IEnumerator Record(Player p)
        {
            Transform root = p.transform;
            int tier = Flight.OpenTier;
            VisEquipment ve = p.GetComponent<VisEquipment>();
            List<GameObject> capeObjs = ve != null && CapeF != null ? CapeF.GetValue(ve) as List<GameObject> : null;
            List<SkinnedMeshRenderer> cape = new List<SkinnedMeshRenderer>();
            StringBuilder info = new StringBuilder();
            if (capeObjs != null)
                foreach (GameObject o in capeObjs)
                    if (o != null)
                    {
                        foreach (SkinnedMeshRenderer r in o.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh != null) cape.Add(r);
                        foreach (Cloth c in o.GetComponentsInChildren<Cloth>(true))
                            info.Append(" cloth on ").Append(c.name).Append(" (enabled ").Append(c.enabled).Append(", stretch ").Append(F(c.stretchingStiffness))
                                .Append(", bend ").Append(F(c.bendingStiffness)).Append(", damping ").Append(F(c.damping)).Append(", world velocity ").Append(F(c.worldVelocityScale))
                                .Append(", world acceleration ").Append(F(c.worldAccelerationScale)).Append(", external ").Append(c.externalAcceleration).Append(", random ").Append(c.randomAcceleration).Append(")");
                    }
            // the cloth: MagicaCloth writes the simulated cape into its own mesh
            List<MagicaCloth> cloths = new List<MagicaCloth>();
            if (capeObjs != null) foreach (GameObject o in capeObjs) if (o != null) cloths.AddRange(o.GetComponentsInChildren<MagicaCloth>(true));
            MagicaCloth[] clothOf = new MagicaCloth[cape.Count];
            for (int s = 0; s < cape.Count; s++)
                foreach (MagicaCloth mc in cloths)
                {
                    Mesh cm = null;
                    try { cm = mc.GetCustomMesh(cape[s]); } catch (Exception) { }
                    if (cm == null) continue;
                    clothOf[s] = mc;
                    List<Transform> cb = null;
                    try { cb = mc.GetCustomBones(cape[s]); } catch (Exception) { }
                    info.Append(" magica on ").Append(cape[s].name).Append(": renderer uses the cloth mesh ").Append(cape[s].sharedMesh == cm)
                        .Append(", readable ").Append(cm.isReadable).Append(", ").Append(cm.vertexCount).Append(" vertices, renderer bones ").Append(cape[s].bones != null ? cape[s].bones.Length : -1)
                        .Append(", cloth bones ").Append(cb != null ? cb.Count : -1).Append(", root bone ").Append(cape[s].rootBone != null ? cape[s].rootBone.name : "none")
                        .Append(", write mode ").Append(mc.SerializeData.meshWriteMode).Append(", type ").Append(mc.SerializeData.clothType);
                    break;
                }
            Plugin.Log.LogInfo("[record] cape parts: " + cape.Count + (info.Length > 0 ? ";" + info : "; no Cloth component"));

            // every bone used by the body or the cape, with its ancestors
            List<Transform> bones = new List<Transform>();
            HashSet<Transform> seen = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer sk in p.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (sk.bones != null)
                    foreach (Transform b in sk.bones)
                        for (Transform t = b; t != null && t != root; t = t.parent)
                            if (seen.Add(t)) bones.Add(t);

            StringBuilder frames = new StringBuilder();
            int n = 0;
            float start = Time.time;
            bool skip = false;
            while (Time.time - start < 4f && Flight.OpenTier > 0)
            {
                yield return new WaitForEndOfFrame();
                skip = !skip;
                if (skip) continue;
                Matrix4x4 toRoot = root.worldToLocalMatrix;
                Quaternion rootInv = Quaternion.Inverse(root.rotation);
                if (n > 0) frames.Append(",");
                frames.Append("{\"t\":").Append(F(Time.time - start)).Append(",\"bones\":[");
                for (int i = 0; i < bones.Count; i++)
                {
                    Transform b = bones[i];
                    if (i > 0) frames.Append(",");
                    if (b == null) { frames.Append("null"); continue; }
                    Vector3 pos = toRoot.MultiplyPoint3x4(b.position);
                    Quaternion rot = rootInv * b.rotation;
                    frames.Append("[").Append(F(pos.x)).Append(",").Append(F(pos.y)).Append(",").Append(F(pos.z)).Append(",")
                          .Append(F(rot.x)).Append(",").Append(F(rot.y)).Append(",").Append(F(rot.z)).Append(",").Append(F(rot.w)).Append("]");
                }
                frames.Append("],\"cape\":[");
                for (int s = 0; s < cape.Count; s++)
                {
                    if (s > 0) frames.Append(",");
                    frames.Append("[");
                    SkinnedMeshRenderer sk = cape[s];
                    if (sk != null)
                    {
                        Mesh baked = new Mesh();
                        sk.BakeMesh(baked, true);
                        Matrix4x4 mx = toRoot * sk.transform.localToWorldMatrix;
                        Vector3[] v = baked.vertices;
                        for (int i = 0; i < v.Length; i++)
                        {
                            Vector3 q = mx.MultiplyPoint3x4(v[i]);
                            if (i > 0) frames.Append(",");
                            frames.Append(F(q.x)).Append(",").Append(F(q.y)).Append(",").Append(F(q.z));
                        }
                        Object.Destroy(baked);
                    }
                    frames.Append("]");
                }
                // the cloth mesh as MagicaCloth wrote it, in the renderer's space (no skinning)
                frames.Append("],\"cm\":[");
                for (int s = 0; s < cape.Count; s++)
                {
                    if (s > 0) frames.Append(",");
                    frames.Append("[");
                    Mesh cm = null;
                    if (clothOf[s] != null) { try { cm = clothOf[s].GetCustomMesh(cape[s]); } catch (Exception) { } }
                    if (cm != null && cm.isReadable)
                    {
                        Matrix4x4 mx = toRoot * cape[s].transform.localToWorldMatrix;
                        Vector3[] v = cm.vertices;
                        for (int i = 0; i < v.Length; i++)
                        {
                            Vector3 q = mx.MultiplyPoint3x4(v[i]);
                            if (i > 0) frames.Append(",");
                            frames.Append(F(q.x)).Append(",").Append(F(q.y)).Append(",").Append(F(q.z));
                        }
                    }
                    frames.Append("]");
                }
                frames.Append("]}");
                n++;
            }

            if (n < 10)
            {
                Plugin.Log.LogInfo("[record] only " + n + " frames: the glide was too short, waiting for a longer one");
                p.Message(MessageHud.MessageType.TopLeft, "Skysail: glide too short to record, glide a bit longer");
                _running = false;
                yield break;
            }

            // what doesn't change: bone names and parents, the cape's triangles, uvs and look
            StringBuilder sb = new StringBuilder("{\"tier\":").Append(tier).Append(",\"tilt\":").Append(F(Plugin.TiltFor(tier))).Append(",\"bones\":[");
            for (int i = 0; i < bones.Count; i++)
            {
                Transform b = bones[i];
                int parent = b.parent != null ? bones.IndexOf(b.parent) : -1;
                if (i > 0) sb.Append(",");
                Vector3 sc = (root.worldToLocalMatrix * b.localToWorldMatrix).lossyScale;
                sb.Append("{\"n\":\"").Append(b.name).Append("\",\"p\":").Append(parent)
                  .Append(",\"s\":[").Append(F(sc.x)).Append(",").Append(F(sc.y)).Append(",").Append(F(sc.z)).Append("]}");
            }
            sb.Append("],\"cape\":[");
            Dictionary<Texture, string> texCache = new Dictionary<Texture, string>();
            for (int s = 0; s < cape.Count; s++)
            {
                SkinnedMeshRenderer sk = cape[s];
                if (s > 0) sb.Append(",");
                Mesh baked = new Mesh();
                if (sk != null) sk.BakeMesh(baked, true);
                sb.Append("{\"name\":\"").Append(sk != null ? sk.name : "?").Append("\",\"cloth\":").Append(sk != null && sk.GetComponent<Cloth>() != null ? "true" : "false").Append(",\"uv\":[");
                Vector2[] uv = baked.uv;
                for (int i = 0; i < uv.Length; i++) { if (i > 0) sb.Append(","); sb.Append(F(uv[i].x)).Append(",").Append(F(uv[i].y)); }
                sb.Append("],\"t\":[");
                int[] tri = baked.triangles;
                for (int i = 0; i < tri.Length; i++) { if (i > 0) sb.Append(","); sb.Append(tri[i]); }
                sb.Append("]");
                Object.Destroy(baked);
                Skin(sb, sk, bones, root);
                Material mat = sk != null ? sk.sharedMaterial : null;
                Texture tex = mat != null && mat.HasProperty("_MainTex") ? mat.mainTexture : null;
                if (tex != null)
                {
                    string png;
                    if (!texCache.TryGetValue(tex, out png)) texCache[tex] = png = Exporter.Png(tex, 512);
                    if (png != null) sb.Append(",\"tex\":\"").Append(png).Append("\"");
                }
                sb.Append("}");
            }
            sb.Append("],\"frames\":[").Append(frames.ToString()).Append("]}");
            try
            {
                string dir = Path.Combine(BepInEx.Paths.ConfigPath, "Skysail_editor");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "glide_rec.json");
                File.WriteAllText(path, sb.ToString());
                Plugin.Log.LogInfo("[record] " + n + " frames saved to " + path);
                p.Message(MessageHud.MessageType.TopLeft, "Skysail: glide recorded (" + n + " frames)");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[record] " + e); }
            Plugin.RecordGlide.Value = false;
            _running = false;
        }
    }
}
