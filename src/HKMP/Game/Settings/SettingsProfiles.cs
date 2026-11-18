namespace Hkmp.Game.Settings;

/// <summary>
/// Ready-made sets of client and server settings, so a group can switch the whole mod between a relaxed
/// cooperative game, a harder challenge run and a testing setup with one menu entry instead of changing every
/// setting by hand.
/// </summary>
internal static class SettingsProfiles {
    /// <summary>
    /// Key of the profile that is used when the settings were changed by hand.
    /// </summary>
    public const string Custom = "custom";

    /// <summary>
    /// Keys of the profiles in the order they are shown in the menu.
    /// </summary>
    public static readonly string[] Keys = ["casual", "hardcore", "test", Custom];

    /// <summary>
    /// Get the English display name of a profile.
    /// </summary>
    /// <param name="key">The key of the profile.</param>
    /// <returns>The English display name.</returns>
    public static string GetName(string key) {
        switch (key) {
            case "casual":
                return "Casual";
            case "hardcore":
                return "Hardcore";
            case "test":
                return "Test";
            default:
                return "Custom";
        }
    }

    /// <summary>
    /// Get the index of a profile key in <see cref="Keys"/>.
    /// </summary>
    /// <param name="key">The key of the profile.</param>
    /// <returns>The index of the profile, or the index of the custom profile if the key is unknown.</returns>
    public static int GetIndex(string key) {
        for (var i = 0; i < Keys.Length; i++) {
            if (Keys[i] == key) {
                return i;
            }
        }

        return Keys.Length - 1;
    }

    /// <summary>
    /// Apply the settings of the given profile. Values that are personal to the player, such as the username,
    /// the authentication key, the keybinds and the connection address, are never changed by a profile.
    /// </summary>
    /// <param name="key">The key of the profile to apply.</param>
    /// <param name="modSettings">The client settings to change.</param>
    /// <param name="serverSettings">The server settings to change.</param>
    /// <returns>Whether a profile was applied. The custom profile does not change anything.</returns>
    public static bool Apply(string key, ModSettings modSettings, ServerSettings serverSettings) {
        if (modSettings == null || key == Custom) {
            return false;
        }

        switch (key) {
            case "casual":
                ApplyCasual(modSettings);
                break;
            case "hardcore":
                ApplyHardcore(modSettings);
                break;
            case "test":
                ApplyTest(modSettings);
                break;
            default:
                return false;
        }

        modSettings.SettingsProfile = key;

        if (serverSettings != null) {
            serverSettings.IsPvpEnabled = false;
            serverSettings.IsBodyDamageEnabled = true;
            serverSettings.DisplayNames = true;
            serverSettings.AllowSkins = true;
        }

        return true;
    }

    /// <summary>
    /// A relaxed cooperative setup: generous revives, extra health and everything shared with the team.
    /// </summary>
    private static void ApplyCasual(ModSettings settings) {
        settings.FullSynchronisation = true;
        settings.ReviveEnabled = true;
        settings.SoulHitPoints = 20;
        settings.ReviveHealth = 4;
        settings.ReviverHealAmount = 2;
        settings.SpectatorCamera = true;
        settings.EnemyTargeting = true;
        settings.GateHoldOpen = true;
        settings.ArenaSync = true;
        settings.EnemyHealthBars = true;
        settings.EnemyHealthMultiplier = 1.25f;
        settings.BossHealthScaling = true;
        settings.BossHealthPerPlayer = 0.5f;
        settings.HealTeammate = true;
        settings.AvatarSmoothing = true;
        settings.TeammateHud = true;
        settings.TeammateColors = true;
        settings.HudCompact = false;
        settings.HudScale = 1f;
        settings.HudShowPing = true;
        settings.ShareMap = true;
        settings.ShareJournal = true;
        settings.ShareEssence = true;
        settings.ShareRewards = true;
        settings.MapIconsRequireCompass = false;
        settings.AutoReconnect = true;
        settings.BossHud = true;
        settings.TeleportToTeammate = true;
        settings.ShareNpcDialogue = true;
    }

    /// <summary>
    /// A harder setup: fewer revives, tougher bosses and progress that every player has to earn themselves.
    /// </summary>
    private static void ApplyHardcore(ModSettings settings) {
        settings.FullSynchronisation = true;
        settings.ReviveEnabled = true;
        settings.SoulHitPoints = 60;
        settings.ReviveHealth = 2;
        settings.ReviverHealAmount = 0;
        settings.SpectatorCamera = false;
        settings.EnemyTargeting = true;
        settings.GateHoldOpen = false;
        settings.ArenaSync = true;
        settings.EnemyHealthBars = true;
        settings.EnemyHealthMultiplier = 2f;
        settings.BossHealthScaling = true;
        settings.BossHealthPerPlayer = 1f;
        settings.HealTeammate = false;
        settings.AvatarSmoothing = true;
        settings.TeammateHud = true;
        settings.TeammateColors = true;
        settings.HudCompact = true;
        settings.HudScale = 0.9f;
        settings.HudShowPing = true;
        settings.ShareMap = true;
        settings.ShareJournal = false;
        settings.ShareEssence = false;
        settings.ShareRewards = false;
        settings.MapIconsRequireCompass = true;
        settings.AutoReconnect = false;
        settings.BossHud = true;
        settings.TeleportToTeammate = false;
        settings.ShareNpcDialogue = false;
    }

    /// <summary>
    /// A setup for testing: quick revives, normal enemy health and every shared feature turned on.
    /// </summary>
    private static void ApplyTest(ModSettings settings) {
        settings.FullSynchronisation = true;
        settings.ReviveEnabled = true;
        settings.SoulHitPoints = 5;
        settings.ReviveHealth = 5;
        settings.ReviverHealAmount = 3;
        settings.SpectatorCamera = true;
        settings.EnemyTargeting = true;
        settings.GateHoldOpen = true;
        settings.ArenaSync = true;
        settings.EnemyHealthBars = true;
        settings.EnemyHealthMultiplier = 1f;
        settings.BossHealthScaling = false;
        settings.BossHealthPerPlayer = 0.5f;
        settings.HealTeammate = true;
        settings.AvatarSmoothing = true;
        settings.TeammateHud = true;
        settings.TeammateColors = true;
        settings.HudCompact = false;
        settings.HudScale = 1f;
        settings.HudShowPing = true;
        settings.ShareMap = true;
        settings.ShareJournal = true;
        settings.ShareEssence = true;
        settings.ShareRewards = true;
        settings.MapIconsRequireCompass = false;
        settings.AutoReconnect = true;
        settings.BossHud = true;
        settings.TeleportToTeammate = true;
        settings.ShareNpcDialogue = true;
    }
}
