using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hkmp.Game;
using Hkmp.Game.Client;
using Hkmp.Game.Server;
using Hkmp.Game.Settings;
using Hkmp.Networking.Client;
using Hkmp.Util;
using Modding;
using Modding.Menu;
using Modding.Menu.Config;
using UnityEngine;
using UnityEngine.UI;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Menu;

/// <summary>
/// Class for building the HKMP mod menu.
/// </summary>
internal class ModMenu {
    /// <summary>
    /// The time in seconds that a modified setting needs to be stopped being modified by the user before being
    /// applied.
    /// </summary>
    private const float SettingApplyDelay = 1.5f;

    /// <summary>
    /// Labels for the enemy health multiplier menu entry.
    /// </summary>
    private static readonly string[] EnemyHealthMultiplierOptions = ["1.0", "1.25", "1.5", "1.75", "2.0"];

    /// <summary>
    /// Values corresponding to the enemy health multiplier menu labels.
    /// </summary>
    private static readonly float[] EnemyHealthMultiplierValues = [1.0f, 1.25f, 1.5f, 1.75f, 2.0f];

    /// <summary>
    /// Labels for the number of hit points of the soul that rises from a downed teammate.
    /// </summary>
    private static readonly string[] SoulHitPointOptions = ["10", "15", "20", "25", "30", "40", "50", "60", "80", "100"];

    /// <summary>
    /// Values corresponding to the labels above.
    /// </summary>
    private static readonly int[] SoulHitPointValues = [10, 15, 20, 25, 30, 40, 50, 60, 80, 100];

    /// <summary>
    /// Labels for the amount of masks a revived player reappears with.
    /// </summary>
    private static readonly string[] ReviveHealthOptions = ["1", "2", "3", "4", "5", "6", "7", "8"];

    /// <summary>
    /// Labels for the amount of masks the player that revived a teammate is healed for.
    /// </summary>
    private static readonly string[] ReviverHealOptions = ["0", "1", "2", "3", "4", "5", "6", "7", "8"];

    /// <summary>
    /// Find the index of the enemy health multiplier option closest to the given value.
    /// </summary>
    /// <param name="value">The multiplier value.</param>
    /// <returns>The index of the closest option.</returns>
    private static int FindClosestEnemyHealthMultiplierIndex(float value) {
        var bestIndex = 0;
        var bestDifference = float.MaxValue;

        for (var i = 0; i < EnemyHealthMultiplierValues.Length; i++) {
            var difference = System.Math.Abs(EnemyHealthMultiplierValues[i] - value);
            if (difference < bestDifference) {
                bestDifference = difference;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Find the index of the soul hit point option closest to the given value.
    /// </summary>
    /// <param name="value">The hit point value.</param>
    /// <returns>The index of the closest option.</returns>
    private static int FindClosestSoulHitPointIndex(int value) {
        var bestIndex = 0;
        var bestDifference = int.MaxValue;

        for (var i = 0; i < SoulHitPointValues.Length; i++) {
            var difference = System.Math.Abs(SoulHitPointValues[i] - value);
            if (difference < bestDifference) {
                bestDifference = difference;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// The HKMP mod settings instance.
    /// </summary>
    private readonly ModSettings _modSettings;
    
    /// <summary>
    /// The client manager instance.
    /// </summary>
    private readonly ClientManager _clientManager;

    /// <summary>
    /// The server manager instance.
    /// </summary>
    private readonly ServerManager _serverManager;

    /// <summary>
    /// The net client instance.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// List of callbacks that should be fired if the server settings change.
    /// </summary>
    private readonly List<Action<ServerSettings>> _serverSettingsChangedCallbacks;
    
    /// <summary>
    /// The top-level HKMP mod menu.
    /// </summary>
    private MenuScreen _hkmpMenu;
    /// <summary>
    /// The menu containing the client settings. Needs to be a static variable here to allow it to be accessed by
    /// lambdas and modified.
    /// </summary>
    private MenuScreen _clientSettingsMenu;

    /// <summary>
    /// The menu containing the server settings. Needs to be a static variable here to allow it to be accessed by
    /// lambdas and modified.
    /// </summary>
    private MenuScreen _serverSettingsMenu;

    /// <summary>
    /// A local copy of the server settings for modification through the menu that will be used to either network to
    /// the server or modify our own hosted servers.
    /// </summary>
    private ServerSettings _localServerSettings;

    /// <summary>
    /// Coroutine that delays applying new server settings until no more changes are made within a certain time period.
    /// </summary>
    private Coroutine _delayedApplyServerSettingsRoutine;

    /// <summary>
    /// Coroutine that delays applying the team setting until no more changes are made within a certain time period.
    /// </summary>
    private Coroutine _delayedApplyTeamRoutine;
    
    /// <summary>
    /// Coroutine that delays applying the skin setting until no more changes are made within a certain time period.
    /// </summary>
    private Coroutine _delayedApplySkinRoutine;

    /// <summary>
    /// The horizontal option for changing teams.
    /// </summary>
    private MenuOptionHorizontal _teamHorizontalOption;
    
    /// <summary>
    /// The horizontal option for changing skins.
    /// </summary>
    private MenuOptionHorizontal _skinHorizontalOption;

    /// <summary>
    /// Whether the horizontal option for changing teams is enabled (as in, whether the user can modify their team).
    /// </summary>
    private bool _teamHorizontalOptionEnabled;
    
    /// <summary>
    /// Whether the horizontal option for changing skins is enabled (as in, whether the user can modify their skin).
    /// </summary>
    private bool _skinHorizontalOptionEnabled;

    /// <summary>
    /// Constructs the mod menu class for HKMP.
    /// </summary>
    /// <param name="modSettings">The mod settings for HKMP.</param>
    /// <param name="clientManager">The client manager to register a callback for when server settings are updated.
    /// </param>
    /// <param name="serverManager">The server manager to get the initial server settings and update them if we are
    /// not connected to a server.</param>
    /// <param name="netClient">The net client to network changes to the server settings if we are connected to a
    /// server.</param>
    public ModMenu(
        ModSettings modSettings, 
        ClientManager clientManager, 
        ServerManager serverManager, 
        NetClient netClient
    ) {
        _modSettings = modSettings;
        _clientManager = clientManager;
        _serverManager = serverManager;
        _netClient = netClient;

        _serverSettingsChangedCallbacks = [];
    }

    /// <summary>
    /// Initialize the mod menu by registering hooks.
    /// </summary>
    public void Initialize() {
        _netClient.ConnectEvent += _ => {
            OnClientConnectionChange(true);
        };
        _netClient.DisconnectEvent += () => {
            OnClientConnectionChange(false);
        };
        _clientManager.TeamChangedEvent += team => {
            _teamHorizontalOption?.SetOptionTo((int) team);
        };
        _clientManager.SkinChangedEvent += skinId => {
            _skinHorizontalOption?.SetOptionTo(skinId);
        };
        _clientManager.ServerSettingsChangedEvent += newSettings => {
            if (newSettings.TeamsEnabled != _teamHorizontalOptionEnabled) {
                ModifyTeamHorizontalOption(newSettings.TeamsEnabled);
            }

            if (newSettings.AllowSkins != _skinHorizontalOptionEnabled) {
                ModifySkinHorizontalOption(newSettings.AllowSkins);
            }

            foreach (var action in _serverSettingsChangedCallbacks) {
                action.Invoke(newSettings);
            }
        };
    }

    /// <summary>
    /// Create the mod menu for HKMP. This consists of client-side settings (such as HUD elements and keybinds) and
    /// server settings.
    /// </summary>
    /// <param name="modListMenu">The MenuScreen for the mod list menu to return to.</param>
    /// <returns>The built HKMP menu screen.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the menu could not be created due to missing
    /// implementation for a type in the server settings.</exception>
    public MenuScreen CreateMenu(MenuScreen modListMenu) {
        var builder = MenuUtils.CreateMenuBuilderWithBackButton("HKMP", modListMenu, out _);

        builder.AddContent(
            RegularGridLayout.CreateVerticalLayout(150f),
            c => {
                c.AddMenuButton("Client Settings", new MenuButtonConfig {
                    Label = Localization.Get("Client Settings"),
                    SubmitAction = _ => UIManager.instance.UIGoToDynamicMenu(_clientSettingsMenu),
                    Proceed = true,
                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                    Description = new DescriptionInfo {
                        Text = Localization.Get("Menu for changing the settings of the client")
                    }
                });

                c.AddMenuButton("Server Settings", new MenuButtonConfig {
                    Label = Localization.Get("Server Settings"),
                    SubmitAction = _ => UIManager.instance.UIGoToDynamicMenu(_serverSettingsMenu),
                    Proceed = true,
                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                    Description = new DescriptionInfo {
                        Text = Localization.Get("Menu for changing the settings of the server for authorized players")
                    }
                });
            }
        );

        _hkmpMenu = builder.Build();

        _clientSettingsMenu = CreateClientSettingsMenu();
        _serverSettingsMenu = CreateServerSettingsMenu();

        return _hkmpMenu;
    }

    /// <summary>
    /// Create the client settings menu.
    /// </summary>
    /// <returns>A <see cref="MenuScreen"/> for the client settings menu.</returns>
    private MenuScreen CreateClientSettingsMenu() {
        var builder = MenuUtils.CreateMenuBuilderWithBackButton("HKMP Client Settings", _hkmpMenu, out _);

        builder.AddContent(
            RegularGridLayout.CreateVerticalLayout(150f),
            c => {
                var teamOptionNames = Enum.GetNames(typeof(Team));
                for (var i = 0; i < teamOptionNames.Length; i++) {
                    teamOptionNames[i] = Localization.Get(teamOptionNames[i]);
                }

                c.AddHorizontalOption(
                    "TeamOption",
                    new HorizontalOptionConfig {
                        Options = teamOptionNames,
                        Label = Localization.Get("Team"),
                        ApplySetting = (_, _) => { },
                        RefreshSetting = (s, _) => s.optionList.SetOptionTo((int) _clientManager.Team),
                        CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu),
                        Description = new DescriptionInfo {
                            Text = ""
                        }
                    },
                    out _teamHorizontalOption
                );
                ModifyTeamHorizontalOption(false);

                var skinOptions = new string[256];
                for (var i = 0; i < 256; i++) {
                    skinOptions[i] = i.ToString();
                }
                
                c.AddHorizontalOption(
                    "SkinOption",
                    new HorizontalOptionConfig {
                        Options = skinOptions,
                        Label = Localization.Get("Skin"),
                        ApplySetting = (_, _) => { },
                        RefreshSetting = (s, _) => s.optionList.SetOptionTo(0),
                        CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu),
                        Description = new DescriptionInfo {
                            Text = ""
                        }
                    },
                    out _skinHorizontalOption
                );
                ModifySkinHorizontalOption(false);
                
                MenuUtils.AddModMenuContent(
                    [
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Full Synchronisation"),
                            Description = Localization.Get("Synchronise enemies, bosses, world changes, and saves in multiplayer games"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.FullSynchronisation = index == 1,
                            Loader = () => _modSettings.FullSynchronisation ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Ping Display"),
                            Description = Localization.Get("HUD element that shows the player's ping in multiplayer games"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.DisplayPing = index == 1,
                            Loader = () => _modSettings.DisplayPing ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Enemy Health Bars"),
                            Description = Localization.Get("Show a health bar above damaged enemies and bosses"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.EnemyHealthBars = index == 1,
                            Loader = () => _modSettings.EnemyHealthBars ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Enemy Health Multiplier"),
                            Description = Localization.Get("Multiplies the maximum health of enemies and bosses while connected"),
                            Values = EnemyHealthMultiplierOptions,
                            Saver = index => _modSettings.EnemyHealthMultiplier =
                                EnemyHealthMultiplierValues[index],
                            Loader = () => FindClosestEnemyHealthMultiplierIndex(
                                _modSettings.EnemyHealthMultiplier
                            )
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Revive Teammates"),
                            Description = Localization.Get("Allow players to revive downed teammates by defeating the soul that rises from their body"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.ReviveEnabled = index == 1,
                            Loader = () => _modSettings.ReviveEnabled ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Soul Hit Points"),
                            Description = Localization.Get("Number of hit points of the soul that must be defeated to revive a teammate"),
                            Values = SoulHitPointOptions,
                            Saver = index => _modSettings.SoulHitPoints = SoulHitPointValues[index],
                            Loader = () => FindClosestSoulHitPointIndex(_modSettings.SoulHitPoints)
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Enemy Targeting"),
                            Description = Localization.Get("Make enemies target the closest living player and bosses switch targets"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.EnemyTargeting = index == 1,
                            Loader = () => _modSettings.EnemyTargeting ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Gate Hold Open"),
                            Description = Localization.Get("Keep battle gates open until every player has entered the arena"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.GateHoldOpen = index == 1,
                            Loader = () => _modSettings.GateHoldOpen ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Map Icons With Compass"),
                            Description = Localization.Get("Show other players on the map only while the Wayward Compass charm is equipped"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.MapIconsRequireCompass = index == 1,
                            Loader = () => _modSettings.MapIconsRequireCompass ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Teammate HUD"),
                            Description = Localization.Get("Show the list of teammates with their masks and a blinking mask marker that dissolves when the teammate is visible"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.TeammateHud = index == 1,
                            Loader = () => _modSettings.TeammateHud ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Spectator Camera"),
                            Description = Localization.Get("While downed, the camera follows a living teammate"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.SpectatorCamera = index == 1,
                            Loader = () => _modSettings.SpectatorCamera ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Revive Health"),
                            Description = Localization.Get("Amount of masks a revived player reappears with"),
                            Values = ReviveHealthOptions,
                            Saver = index => _modSettings.ReviveHealth = index + 1,
                            Loader = () => Mathf.Clamp(_modSettings.ReviveHealth - 1, 0, ReviveHealthOptions.Length - 1)
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Reviver Heal"),
                            Description = Localization.Get("Amount of masks the player that defeated the soul is healed for"),
                            Values = ReviverHealOptions,
                            Saver = index => _modSettings.ReviverHealAmount = index,
                            Loader = () => Mathf.Clamp(_modSettings.ReviverHealAmount, 0, ReviverHealOptions.Length - 1)
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Auto Reconnect"),
                            Description = Localization.Get("Reconnect to the last server automatically when the connection is lost"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.AutoReconnect = index == 1,
                            Loader = () => _modSettings.AutoReconnect ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Session Statistics"),
                            Description = Localization.Get("Show the time, downs, revives and give-ups of the current session in chat"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.SessionStats = index == 1,
                            Loader = () => _modSettings.SessionStats ? 1 : 0
                        },
                        new IMenuMod.MenuEntry {
                            Name = Localization.Get("Arena Sync"),
                            Description = Localization.Get("Share Colosseum and Godhome progress and update arena doors immediately"),
                            Values = [Localization.Get("Off"), Localization.Get("On")],
                            Saver = index => _modSettings.ArenaSync = index == 1,
                            Loader = () => _modSettings.ArenaSync ? 1 : 0
                        }
                    ],
                    c,
                    _hkmpMenu
                );

                c.AddKeybind(
                    "OpenChatKeybind",
                    _modSettings.Keybinds.OpenChat,
                    new KeybindConfig {
                        Label = Localization.Get("Key to open the chat"),
                        CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu)
                    }
                );

                c.AddKeybind(
                    "RespawnAtBenchKeybind",
                    _modSettings.Keybinds.RespawnAtBench,
                    new KeybindConfig {
                        Label = Localization.Get("Key to give up and respawn at the last bench"),
                        CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu)
                    }
                );

                c.AddKeybind(
                    "ShowSessionStatsKeybind",
                    _modSettings.Keybinds.ShowSessionStats,
                    new KeybindConfig {
                        Label = Localization.Get("Key to show session statistics"),
                        CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu)
                    }
                );
            }
        );

        return builder.Build();
    }

    /// <summary>
    /// Create the server settings menu.
    /// </summary>
    /// <returns>A <see cref="MenuScreen"/> for the server settings menu.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the menu could not be created due to missing
    /// implementation for a type in the server settings.</exception>
    private MenuScreen CreateServerSettingsMenu() {
        _serverSettingsChangedCallbacks.Clear();
        
        // We keep track of an instance of server settings specifically for this mod menu
        // This will initially be a copy of the server manager settings, which come from the mod settings
        _localServerSettings = _serverManager.InternalServerSettings.GetCopy();

        var serverSettingsProps = typeof(ServerSettings).GetProperties();

        var builder = MenuUtils.CreateMenuBuilderWithBackButton(
            "HKMP Server Settings", 
            _hkmpMenu, 
            out var serverSettingsBackButton
        );

        builder.AddContent(new NullContentLayout(), nullContentArea => {
            nullContentArea.AddScrollPaneContent(
                new ScrollbarConfig {
                    Navigation = new Navigation {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = serverSettingsBackButton,
                        selectOnDown = serverSettingsBackButton
                    },
                    Position = new AnchoredPosition {
                        ChildAnchor = new Vector2(0.0f, 1f),
                        ParentAnchor = new Vector2(1f, 1f),
                        Offset = new Vector2(-310f, 0.0f)
                    },
                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu)
                },
                new RelLength(serverSettingsProps.Length * 150f),
                RegularGridLayout.CreateVerticalLayout(150f),
                scrollGridContentArea => {
                    foreach (var propInfo in serverSettingsProps) {
                        var name = propInfo.Name;
                        var type = propInfo.PropertyType;

                        // Define variables for the eventual creation of an individual setting that depend on the
                        // type of the setting (for example, the options will be different for a byte than for a bool)
                        string[] options;
                        Action<int> saver;
                        Func<int> loader;
                        // This action will be invoked whenever the server settings are changed externally, so outside
                        // the mod menu
                        Action<ServerSettings, MenuOptionHorizontal> serverSettingsChangedAction;

                        if (type == typeof(bool)) {
                            options = [Localization.Get("Off"), Localization.Get("On")];

                            saver = i => {
                                ReflectionHelper.SetProperty(_localServerSettings, name, i == 1);
                                HandleUpdateServerSettings();
                            };
                            loader = () => ReflectionHelper.GetProperty<ServerSettings, bool>(_localServerSettings, name)
                                ? 1
                                : 0;

                            serverSettingsChangedAction = (newSettings, horizontalOptionToChange) => {
                                // Get the old and new values and check whether there is a change
                                var oldValue = ReflectionHelper.GetProperty<ServerSettings, bool>(_localServerSettings, name);
                                var newValue = ReflectionHelper.GetProperty<ServerSettings, bool>(newSettings, name);

                                if (oldValue != newValue) {
                                    // Set the mod menu option and update our local server settings instance
                                    horizontalOptionToChange.SetOptionTo(newValue ? 1 : 0);
                                    ReflectionHelper.SetProperty(_localServerSettings, name, newValue);
                                }
                            };
                        } else if (type == typeof(byte) && name.EndsWith("Damage")) {
                            // If the field is for the amount of damage for something, we fill the values with 0 through 20
                            options = new string[21];
                            for (var i = 0; i <= 20; i++) {
                                options[i] = i.ToString();
                            }

                            saver = i => {
                                ReflectionHelper.SetProperty(_localServerSettings, name, (byte) i);
                                HandleUpdateServerSettings();
                            };
                            loader = () => ReflectionHelper.GetProperty<ServerSettings, byte>(_localServerSettings, name);

                            serverSettingsChangedAction = (newSettings, horizontalOptionToChange) => {
                                // Get the old and new values and check whether there is a change
                                var oldValue = ReflectionHelper.GetProperty<ServerSettings, byte>(_localServerSettings, name);
                                var newValue = ReflectionHelper.GetProperty<ServerSettings, byte>(newSettings, name);

                                if (oldValue != newValue) {
                                    // Set the mod menu option and update our local server settings instance
                                    horizontalOptionToChange.SetOptionTo(newValue);
                                    ReflectionHelper.SetProperty(_localServerSettings, name, newValue);
                                }
                            };
                        } else {
                            throw new InvalidOperationException(
                                $"Could not make menu entry for unknown field type: {type}, for field: {name}");
                        }

                        // Try to obtain the label and description of the setting using the mod menu attribute
                        var label = name;
                        DescriptionInfo? descriptionInfo = null;
                        var menuSettingAttr = propInfo.GetCustomAttribute<ModMenuSettingAttribute>();
                        if (menuSettingAttr != null) {
                            label = Localization.Get($"server.{name}.label", menuSettingAttr.Name);
                            if (menuSettingAttr.Description != null) {
                                descriptionInfo = new DescriptionInfo {
                                    Text = Localization.Get($"server.{name}.desc", menuSettingAttr.Description)
                                };
                            }
                        }

                        scrollGridContentArea.AddHorizontalOption(
                            name,
                            new HorizontalOptionConfig {
                                Options = options,
                                Label = label,
                                ApplySetting = (_, i) => saver.Invoke(i),
                                RefreshSetting = (s, _) => s.optionList.SetOptionTo(loader.Invoke()),
                                CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(_hkmpMenu),
                                Description = descriptionInfo
                            },
                            out var horizontalOption
                        );
                        horizontalOption.menuSetting.RefreshValueFromGameSettings();

                        _serverSettingsChangedCallbacks.Add(newSettings => {
                            serverSettingsChangedAction.Invoke(newSettings, horizontalOption);
                        });
                    }
                }
            );
        });

        return builder.Build();
    }

    /// <summary>
    /// Run the given action after a delay (defined in <see cref="SettingApplyDelay"/>) and store the delayed execution coroutine in the referenced
    /// variable. If this method is called again with the same referenced coroutine before the action is executed,
    /// it will be cancelled and a new delayed execution is scheduled.
    /// Can be used to apply the changing of certain settings only if the user stops modifying the setting for a
    /// certain period of time.
    /// </summary>
    /// <param name="action">The action to execute delayed if this method is not called again within a certain period
    /// of time.</param>
    /// <param name="coroutine">The coroutine containing the previous delayed execution of the action. This variable
    /// will be used to store the new coroutine.</param>
    private void RunActionWhenNoChanges(Action action, ref Coroutine coroutine) {
        if (coroutine != null) {
            MonoBehaviourUtil.Instance.StopCoroutine(coroutine);
        }

        coroutine = MonoBehaviourUtil.Instance.StartCoroutine(RunActionWithDelay(action, SettingApplyDelay));
    }

    /// <summary>
    /// Handles updates to the server settings. Checks whether we are connected to a server and only sends updates
    /// if no changes were made to the local server settings in this class for a certain period of time to prevent
    /// sending unnecessary updates.
    /// </summary>
    private void HandleUpdateServerSettings() {
        if (!_netClient.IsConnected) {
            // If the client is not connected to any server, we simply update the server settings of the 
            // server manager so that when we host a server, it will have the same settings as in this menu
            _serverManager.InternalServerSettings.SetAllProperties(_localServerSettings);
            return;
        }
        
        RunActionWhenNoChanges(() => {
            _netClient.UpdateManager.SetServerSettingsUpdate(_localServerSettings);
        }, ref _delayedApplyServerSettingsRoutine);
    }

    /// <summary>
    /// Callback method for the client connection changes, when either connected or disconnected from a server.
    /// This will modify the team selection depending on whether team selection should be allowed or not.
    /// </summary>
    /// <param name="connected"></param>
    private void OnClientConnectionChange(bool connected) {
        if (!connected) {
            ModifyTeamHorizontalOption(false);
            ModifySkinHorizontalOption(false);
            return;
        }

        if (_localServerSettings.TeamsEnabled) {
            ModifyTeamHorizontalOption(true);
        }

        if (_localServerSettings.AllowSkins) {
            ModifySkinHorizontalOption(true);
        }
    }

    /// <summary>
    /// Modify the horizontal option for teams to either allow or disallow team selection.
    /// </summary>
    /// <param name="allowTeams">Whether to allow the player to select a team.</param>
    private void ModifyTeamHorizontalOption(bool allowTeams) {
        string description;
        MenuSetting.ApplySetting applySetting;

        if (allowTeams) {
            applySetting = (_, i) => {
                if (_netClient.IsConnected) {
                    RunActionWhenNoChanges(() => {
                        _netClient.UpdateManager.AddPlayerSettingUpdate(team: (Team) i);
                    }, ref _delayedApplyTeamRoutine);
                }
            };

            description = Localization.Get("Select one of several teams");
        } else {
            applySetting = (m, _) => {
                m.optionList.SetOptionTo(0);
            };

            description = Localization.Get("Team selection is currently disabled");
        }
        
        _teamHorizontalOption.menuSetting.customApplySetting = applySetting;

        _teamHorizontalOption.gameObject.transform.Find("Description").GetComponent<Text>().text = description;

        _teamHorizontalOptionEnabled = allowTeams;
    }
    
    /// <summary>
    /// Modify the horizontal option for skins to either allow or disallow skin selection.
    /// </summary>
    /// <param name="allowSkins">Whether to allow the player to select a skin.</param>
    private void ModifySkinHorizontalOption(bool allowSkins) {
        string description;
        MenuSetting.ApplySetting applySetting;

        if (allowSkins) {
            applySetting = (_, i) => {
                if (_netClient.IsConnected) {
                    RunActionWhenNoChanges(() => {
                        _netClient.UpdateManager.AddPlayerSettingUpdate(skinId: (byte) i);
                    }, ref _delayedApplySkinRoutine);
                }
            };

            description = Localization.Get("Select a skin");
        } else {
            applySetting = (m, _) => {
                m.optionList.SetOptionTo(0);
            };

            description = Localization.Get("Skin selection is currently disabled");
        }
        
        _skinHorizontalOption.menuSetting.customApplySetting = applySetting;

        _skinHorizontalOption.gameObject.transform.Find("Description").GetComponent<Text>().text = description;

        _skinHorizontalOptionEnabled = allowSkins;
    }

    /// <summary>
    /// Run the given action with the given delay.
    /// </summary>
    /// <param name="action">The action to invoke after the delay.</param>
    /// <param name="delay">The delay in seconds.</param>
    /// <returns>The coroutine of the delayed invocation.</returns>
    private static IEnumerator RunActionWithDelay(Action action, float delay) {
        yield return new WaitForSeconds(delay);
        
        action.Invoke();
    }
}
