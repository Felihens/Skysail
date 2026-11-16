using UnityEngine;

namespace Skysail
{
    // Thin white streaks rushing past both sides of a glider, like the game's own wind lines. One small particle
    // system on the player, in the player's space (so it works for other players too), streaming from the head
    // back past the feet. The look (material) is borrowed from the game's wind particles when found.
    internal class WindLines : MonoBehaviour
    {
        private static Material _mat;
        private static bool _looked;
        private ParticleSystem[] _ps = new ParticleSystem[2];

        internal static WindLines On(Transform player)
        {
            WindLines w = player.GetComponentInChildren<WindLines>(true);
            if (w != null) return w;
            GameObject go = new GameObject("Skysail_WindLines");
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            w = go.AddComponent<WindLines>();
            w.Build();
            return w;
        }

        // a particle material from the game: one named like wind first, else any additive/alpha particle one
        private static Material FindMaterial()
        {
            if (_looked) return _mat;
            _looked = true;
            Material any = null;
            foreach (ParticleSystemRenderer r in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;
                string n = (r.gameObject.name + " " + m.name).ToLowerInvariant();
                if (n.Contains("wind") && !n.Contains("leaf") && !n.Contains("leaves")) { _mat = m; break; }
                if (any == null && m.shader.name.ToLowerInvariant().Contains("particle")) any = m;
            }
            if (_mat == null) _mat = any;
            Plugin.Debug("[wind] streak material: " + (_mat != null ? _mat.name + " (" + _mat.shader.name + ")" : "none, using a plain one"));
            if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));
            return _mat;
        }

        private void Build()
        {
            for (int k = 0; k < 2; k++)
            {
                GameObject side = new GameObject(k == 0 ? "left" : "right");
                side.transform.SetParent(transform, false);
                side.transform.localPosition = new Vector3(k == 0 ? -0.6f : 0.6f, 0f, 0f);
                _ps[k] = Side(side);
            }
        }

        private static ParticleSystem Side(GameObject go)
        {
            ParticleSystem _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.maxParticles = 70;

            ParticleSystem.EmissionModule em = _ps.emission;
            em.rateOverTime = 0f;

            // two thin slabs, one on each side of the body, from the head to past the feet
            ParticleSystem.ShapeModule sh = _ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.35f, 1.5f, 0.6f);

            // they rush backwards along the body (local -z is behind; the body leans, so also down the legs)
            ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            vel.z = new ParticleSystem.MinMaxCurve(-16f, -11f);

            ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.09f;
            r.lengthScale = 2f;
            r.sharedMaterial = FindMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return _ps;
        }

        // tier: the cape's tier (1 = few short streaks ... 6 = many long ones); 0 stops them
        internal void Set(float tier)
        {
            foreach (ParticleSystem ps in _ps)
            {
                if (ps == null) continue;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = tier <= 0f ? 0f : 2f + (tier - 1f) * 4f;          // 2, 6, 10, 14, 18, 22 a side
                ParticleSystem.MainModule main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f + 0.025f * tier, 0.2f + 0.03f * tier);
                if (tier > 0f && !ps.isPlaying) ps.Play();
                if (tier <= 0f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
