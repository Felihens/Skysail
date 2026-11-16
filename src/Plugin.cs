using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Skysail
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "felih.Skysail";
        public const string NAME = "Skysail";
        public const string VERSION = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> MinHeight;
        internal static ConfigEntry<float> MinStamina;
        internal static ConfigEntry<bool> DetailedLog;
        internal static ConfigEntry<bool> ExportPlayer;
        internal static ConfigEntry<bool> ExportPoses;
        internal static ConfigEntry<bool> ExportFall;
        internal static ConfigEntry<float> SoftFallShare;
        internal static ConfigEntry<float> LandHeight;
        internal static ConfigEntry<bool> WindLines;
        internal static ConfigEntry<bool> RecordGlide;
        internal static ConfigEntry<float> CapeStream, CapeGravity, CapeFlutter, CapeTurbulence, CapeFrequency, CapeMovingWind;

        private void Awake()
        {
            Log = Logger;
            MinHeight = Config.Bind("1 - General", "MinHeight", 3f,
                new ConfigDescription("Metres above the ground needed to open the sail (press Jump in the air).", new AcceptableValueRange<float>(1f, 20f)));
            MinStamina = Config.Bind("1 - General", "MinStamina", 5f,
                new ConfigDescription("Stamina needed to open the sail.", new AcceptableValueRange<float>(0f, 50f)));
            LandHeight = Config.Bind("1 - General", "LandHeight", 1f,
                new ConfigDescription("The sail closes by itself this many metres above the ground, so you land with the game's own landing.", new AcceptableValueRange<float>(0f, 5f)));
            WindLines = Config.Bind("2 - Look", "WindLines", true, "White wind streaks along your sides while gliding.");
            SoftFallShare = Config.Bind("1 - General", "SoftFallShare", 0.5f,
                new ConfigDescription("After you close the sail in the air, the next fall does this share of its damage (0.5 = half), until you touch the ground.", new AcceptableValueRange<float>(0f, 1f)));
            CapeStream = Config.Bind("3 - Cape", "CapeStream", 0.6f,
                new ConfigDescription("While gliding, how far the cape's gravity turns from straight down (0) to straight behind you (1): the air pushing it back.", new AcceptableValueRange<float>(0f, 1f)));
            CapeGravity = Config.Bind("3 - Cape", "CapeGravity", 1f,
                new ConfigDescription("While gliding, the cape's gravity strength (1 = the game's).", new AcceptableValueRange<float>(0f, 3f)));
            CapeFlutter = Config.Bind("3 - Cape", "CapeFlutter", 1.5f,
                new ConfigDescription("While gliding, how much the wind moves the cape (the game uses about 1).", new AcceptableValueRange<float>(0f, 3f)));
            CapeTurbulence = Config.Bind("3 - Cape", "CapeTurbulence", 1f,
                new ConfigDescription("While gliding, how uneven the wind on the cape is.", new AcceptableValueRange<float>(0f, 2f)));
            CapeFrequency = Config.Bind("3 - Cape", "CapeFrequency", 1.5f,
                new ConfigDescription("While gliding, how fast the cape flutters.", new AcceptableValueRange<float>(0f, 3f)));
            CapeMovingWind = Config.Bind("3 - Cape", "CapeMovingWind", 0.5f,
                new ConfigDescription("While gliding, how much your own speed blows on the cape.", new AcceptableValueRange<float>(0f, 1f)));
            DetailedLog = Config.Bind("9 - Debug", "DetailedLog", false, "Writes glide details to the BepInEx log.");
            ExportPlayer = Config.Bind("9 - Debug", "ExportCharacterAndCapes", false, "Saves your character and every vanilla cape once (standing still for 3 s) for the capes editor, then turns itself off.");
            ExportPoses = Config.Bind("9 - Debug", "ExportPoses", false, "Plays every emote once (standing still for 3 s) and saves each pose with your cape for the pose picker, then turns itself off.");
            RecordGlide = Config.Bind("9 - Debug", "RecordGlide", false, "Records 4 s of your next glide (body, cape) for the glide editor, then turns itself off.");
            ExportFall = Config.Bind("9 - Debug", "ExportFallPose", false, "Saves your skeleton about 1 s into every fall (without opening the sail) for the editor; each fall replaces the last.");
            // no new items: every vanilla cape glides (see Sails.TierOf)
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }

        // how far the body leans for a cape tier
        internal static float TiltFor(int tier) { return 35f + 5f * Mathf.Max(0, tier - 1); }   // 35 at tier 1 ... 60 at tier 6

        internal static void Debug(string s) { if (DetailedLog.Value) Log.LogInfo(s); }
    }
}
