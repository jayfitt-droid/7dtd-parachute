using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal enum DeployInput
{
    DoubleTapJump,
    Key,
    Both
}

internal enum LeftRightMode
{
    Strafe,
    Turn
}

/// <summary>
/// Every tunable in one place. The file is JSON with // comments; Json.NET
/// ignores comments by default. An invalid value logs a warning and falls back
/// to the default, so a bad edit can never break the mod.
/// </summary>
internal sealed class ParachuteConfig
{
    public DeployInput DeployInput = DeployInput.DoubleTapJump;
    public string DeployKey = "";
    public KeyCode DeployKeyCode = KeyCode.None;
    public int DoubleTapWindowMs = 350;
    public float MinDeployHeight = 3f;
    public float FallSpeed = 4f;
    public float FallSpeedForward = 5.5f;
    public float FallSpeedBack = 2.75f;
    public float GlideSpeed = 4f;
    public float GlideSpeedForward = 6f;
    public float GlideSpeedBack = 1.5f;
    public LeftRightMode LeftRight = LeftRightMode.Strafe;
    public float StrafeSpeed = 3f;
    public float TurnRate = 75f;
    public float ControlResponse = 6f;
    public float DeployTime = 0.8f;
    public float StowAnimTime = 0.4f;
    public bool AllowCutaway = true;
    public int RedeployCooldownMs = 300;
    public int LandingGraceMs = 500;
    public float CanopyScale = 1f;
    public float CanopyHeight = 3.2f;
    public bool PlayFlutterLoop = true;
    public float FlutterPitch = 0.7f;
    public bool Debug;

    public float DoubleTapWindowSeconds => DoubleTapWindowMs / 1000f;
    public float RedeployCooldownSeconds => RedeployCooldownMs / 1000f;
    public float LandingGraceSeconds => LandingGraceMs / 1000f;

    public static ParachuteConfig Load(string modDirectory)
    {
        var result = new ParachuteConfig();
        string path = Path.Combine(modDirectory, "Config", "ParachuteConfig.json");
        if (!File.Exists(path))
        {
            Log.Warning("[Parachute] Config missing; using defaults: " + path);
            return result;
        }
        try
        {
            JObject data = JObject.Parse(File.ReadAllText(path));
            result.DeployInput = Input(data, "deploy_input", DeployInput.DoubleTapJump);
            result.DeployKey = Text(data, "deploy_key", "");
            result.DeployKeyCode = ParseKey(result.DeployKey);
            result.DoubleTapWindowMs = Integer(data, "double_tap_window_ms", 350, 150, 1000);
            result.MinDeployHeight = Number(data, "min_deploy_height", 3f, 0f, 50f);
            result.FallSpeed = Number(data, "fall_speed", 4f, 1f, 30f);
            result.FallSpeedForward = Number(data, "fall_speed_forward", 5.5f, 1f, 30f);
            result.FallSpeedBack = Number(data, "fall_speed_back", 2.75f, 1f, 30f);
            result.GlideSpeed = Number(data, "glide_speed", 4f, 0f, 20f);
            result.GlideSpeedForward = Number(data, "glide_speed_forward", 6f, 0f, 25f);
            result.GlideSpeedBack = Number(data, "glide_speed_back", 1.5f, 0f, 20f);
            result.LeftRight = LeftRightInput(data, "left_right", LeftRightMode.Strafe);
            result.StrafeSpeed = Number(data, "strafe_speed", 3f, 0f, 15f);
            result.TurnRate = Number(data, "turn_rate", 75f, 10f, 360f);
            result.ControlResponse = Number(data, "control_response", 6f, 0.5f, 50f);
            result.DeployTime = Number(data, "deploy_time", 0.8f, 0.1f, 3f);
            result.StowAnimTime = Number(data, "stow_anim_time", 0.4f, 0.05f, 2f);
            result.AllowCutaway = Boolean(data, "allow_cutaway", true);
            result.RedeployCooldownMs = Integer(data, "redeploy_cooldown_ms", 300, 0, 5000);
            result.LandingGraceMs = Integer(data, "landing_grace_ms", 500, 0, 3000);
            result.CanopyScale = Number(data, "canopy_scale", 1f, 0.25f, 3f);
            result.CanopyHeight = Number(data, "canopy_height", 3.2f, 1f, 8f);
            result.PlayFlutterLoop = Boolean(data, "play_flutter_loop", true);
            result.FlutterPitch = Number(data, "flutter_pitch", 0.7f, 0.3f, 1.5f);
            result.Debug = Boolean(data, "debug", false);
            Log.Out("[Parachute] Config loaded from " + path);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Invalid config; using defaults: " + ex.Message);
            return new ParachuteConfig();
        }
        return result;
    }

    private static KeyCode ParseKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return KeyCode.None;
        try
        {
            if (Enum.TryParse(name, true, out KeyCode key) && key != KeyCode.None) return key;
        }
        catch (Exception) { }
        Log.Warning("[Parachute] Invalid deploy_key \"" + name + "\"; the direct key is disabled.");
        return KeyCode.None;
    }

    private static DeployInput Input(JObject data, string key, DeployInput fallback)
    {
        JToken token = data[key];
        if (token == null || token.Type != JTokenType.String) return fallback;
        switch (token.ToString().Trim().ToLowerInvariant())
        {
            case "double_tap_jump": return DeployInput.DoubleTapJump;
            case "key": return DeployInput.Key;
            case "both": return DeployInput.Both;
            default:
                Log.Warning("[Parachute] Invalid " + key + " (double_tap_jump, key or both); using " + fallback);
                return fallback;
        }
    }

    private static LeftRightMode LeftRightInput(JObject data, string key, LeftRightMode fallback)
    {
        JToken token = data[key];
        if (token == null || token.Type != JTokenType.String) return fallback;
        switch (token.ToString().Trim().ToLowerInvariant())
        {
            case "strafe": return LeftRightMode.Strafe;
            case "turn": return LeftRightMode.Turn;
            default:
                Log.Warning("[Parachute] Invalid " + key + " (strafe or turn); using " + fallback);
                return fallback;
        }
    }

    private static string Text(JObject data, string key, string fallback)
    {
        JToken token = data[key];
        return token != null && token.Type == JTokenType.String ? token.ToString() : fallback;
    }

    private static float Number(JObject data, string key, float fallback, float min, float max)
    {
        JToken token = data[key];
        if (token == null) return fallback;
        float value;
        if ((token.Type == JTokenType.Float || token.Type == JTokenType.Integer) &&
            float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max)
            return value;
        Log.Warning("[Parachute] Invalid " + key + " (allowed " + min + " to " + max + "); using " + fallback);
        return fallback;
    }

    private static int Integer(JObject data, string key, int fallback, int min, int max)
    {
        JToken token = data[key];
        if (token == null) return fallback;
        int value;
        if ((token.Type == JTokenType.Integer || token.Type == JTokenType.Float) &&
            int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
            value >= min && value <= max)
            return value;
        Log.Warning("[Parachute] Invalid " + key + " (allowed " + min + " to " + max + "); using " + fallback);
        return fallback;
    }

    private static bool Boolean(JObject data, string key, bool fallback)
    {
        JToken token = data[key];
        if (token == null) return fallback;
        if (token.Type == JTokenType.Boolean) return token.Value<bool>();
        Log.Warning("[Parachute] Invalid " + key + "; using " + fallback);
        return fallback;
    }
}
