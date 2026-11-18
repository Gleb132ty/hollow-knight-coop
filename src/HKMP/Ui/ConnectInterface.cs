using System;
using System.Collections;
using System.Collections.Generic;
using Hkmp.Game.Server;
using Hkmp.Game.Settings;
using Hkmp.Networking;
using Hkmp.Networking.Client;
using Hkmp.Networking.Packet.Data;
using Hkmp.Ui.Component;
using Hkmp.Ui.Resources;
using Hkmp.Util;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Ui;

/// <summary>
/// Class for creating and managing the connect interface.
/// </summary>
internal class ConnectInterface {
    /// <summary>
    /// The indent of some text elements.
    /// </summary>
    private const float TextIndentWidth = 5f;

    /// <summary>
    /// The text of the connection button if not connected.
    /// </summary>
    private const string ConnectText = "Connect";

    /// <summary>
    /// The text of the connection button while connecting.
    /// </summary>
    private const string ConnectingText = "Connecting...";

    /// <summary>
    /// The text of the host button while not hosting.
    /// </summary>
    private const string StartHostingText = "Start Hosting";

    /// <summary>
    /// The time in seconds to hide the feedback text after it appeared.
    /// </summary>
    private const float FeedbackTextHideTime = 10f;

    /// <summary>
    /// The mod settings.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// The component group of the connect UI.
    /// </summary>
    private readonly ComponentGroup _connectGroup;

    // /// <summary>
    // /// The component group of the client settings UI.
    // /// </summary>
    // private readonly ComponentGroup _settingsGroup;

    /// <summary>
    /// The username input component.
    /// </summary>
    private IInputComponent _usernameInput;

    /// <summary>
    /// The address input component.
    /// </summary>
    private IInputComponent _addressInput;

    /// <summary>
    /// The port input component.
    /// </summary>
    private IInputComponent _portInput;

    /// <summary>
    /// The password input component.
    /// </summary>
    private IInputComponent _passwordInput;

    /// <summary>
    /// The username label component.
    /// </summary>
    private ITextComponent _usernameLabel;

    /// <summary>
    /// The server address label component.
    /// </summary>
    private ITextComponent _serverLabel;

    /// <summary>
    /// The password label component.
    /// </summary>
    private ITextComponent _passwordLabel;

    /// <summary>
    /// The button that searches for a server on the local network.
    /// </summary>
    private IButtonComponent _findServerButton;

    /// <summary>
    /// The listener used to find servers on the local network.
    /// </summary>
    private LanDiscoveryListener _lanListener;

    /// <summary>
    /// The coroutine that is currently searching for a server on the local network.
    /// </summary>
    private Coroutine _findServerCoroutine;

    /// <summary>
    /// The English text that is currently displayed on the connection button.
    /// </summary>
    private string _connectionButtonText = ConnectText;

    /// <summary>
    /// The connection (connect or disconnect) button component.
    /// </summary>
    private IButtonComponent _connectionButton;

    /// <summary>
    /// The server host button component.
    /// </summary>
    private IButtonComponent _serverButton;

    /// <summary>
    /// The feedback text component.
    /// </summary>
    private ITextComponent _feedbackText;

    /// <summary>
    /// The coroutine that hides the feedback text after a delay.
    /// </summary>
    private Coroutine _feedbackHideCoroutine;

    /// <summary>
    /// Number of rows in the server list.
    /// </summary>
    private const int ServerRowCount = 5;

    /// <summary>
    /// The stored servers with favourites and history.
    /// </summary>
    private ServerListStore _serverList;

    /// <summary>
    /// The label above the server list.
    /// </summary>
    private ITextComponent _serverListLabel;

    /// <summary>
    /// The rows of the server list.
    /// </summary>
    private readonly List<ServerRow> _serverRows = new List<ServerRow>();

    /// <summary>
    /// The name of the server that is currently being connected to, if it is known.
    /// </summary>
    private string _pendingServerName;

    /// <summary>
    /// The address of the server that the pending name belongs to.
    /// </summary>
    private string _pendingServerAddress;

    /// <summary>
    /// The port of the server that the pending name belongs to.
    /// </summary>
    private int _pendingServerPort;

    /// <summary>
    /// A row in the server list: a button that fills in the address and a button that toggles the favourite.
    /// </summary>
    private class ServerRow {
        public IButtonComponent Main;
        public IButtonComponent Star;
        public ServerListEntry Entry;
    }

    /// <summary>
    /// Event that is executed when the connect button is pressed.
    /// </summary>
    public event Action<string, int, string, string> ConnectButtonPressed;

    /// <summary>
    /// Event that is executed when the start hosting button is pressed.
    /// </summary>
    public event Action<string, int> StartHostButtonPressed;

    public ConnectInterface(
        ModSettings modSettings,
        ComponentGroup connectGroup
        // ComponentGroup settingsGroup
    ) {
        _modSettings = modSettings;

        _connectGroup = connectGroup;
        // _settingsGroup = settingsGroup;

        CreateConnectUi();

        Localization.RegisterRefresher(RefreshLocalization);
    }

    /// <summary>
    /// Update all static texts in the connect interface to the current game language.
    /// </summary>
    private void RefreshLocalization() {
        _usernameLabel?.SetText(Localization.Get("Username"));
        _serverLabel?.SetText(Localization.Get("Server IP and port"));
        _passwordLabel?.SetText(Localization.Get("Password"));

        if (_connectionButton != null) {
            _connectionButton.SetText(Localization.Get(_connectionButtonText));
        }

        if (_serverButton != null) {
            _serverButton.SetText(Localization.Get(StartHostingText));
        }

        if (_findServerButton != null) {
            _findServerButton.SetText(Localization.Get("Find Server"));
        }

        if (_serverListLabel != null) {
            _serverListLabel.SetText(Localization.Get("Saved servers"));
        }

        if (_usernameInput is InputComponent usernameInput) {
            usernameInput.SetPlaceholder(Localization.Get("Username"));
        }

        if (_addressInput is InputComponent addressInput) {
            addressInput.SetPlaceholder(Localization.Get("IP Address"));
        }

        if (_portInput is InputComponent portInput) {
            portInput.SetPlaceholder(Localization.Get("Port"));
        }

        if (_passwordInput is InputComponent passwordInput) {
            passwordInput.SetPlaceholder(Localization.Get("Password"));
        }
    }

    /// <summary>
    /// Callback method for when the client disconnects.
    /// </summary>
    public void OnClientDisconnect() {
        SetConnectionButtonText(ConnectText);
        _connectionButton.SetOnPress(OnConnectButtonPressed);
        _connectionButton.SetInteractable(true);
    }

    /// <summary>
    /// Callback method for when the client successfully connects.
    /// </summary>
    /// <param name="motd">The message of the day of the server, if any.</param>
    public void OnSuccessfulConnect(string motd) {
        // Remember the server in the list with favourites and history
        if (_serverList != null) {
            _serverList.AddOrUpdate(_modSettings.ConnectAddress, _modSettings.ConnectPort, _pendingServerName);
            _pendingServerName = null;
            RefreshServerListUi();
        }

        // Let the user know that the connection was successful, and show the message of the day of the server
        if (string.IsNullOrEmpty(motd)) {
            SetFeedbackText(Color.green, "Successfully connected");
        } else {
            SetFeedbackText(Color.green, Localization.Format("Successfully connected\n{0}", motd));
        }

        // Reset the connection button with the disconnect text
        SetConnectionButtonText(ConnectText);
        _connectionButton.SetInteractable(true);
    }

    /// <summary>
    /// Callback method for when the client fails to connect.
    /// </summary>
    /// <param name="result">The result of the failed connection.</param>
    public void OnFailedConnect(ConnectionFailedResult result) {
        // Let the user know that the connection failed based on the result
        switch (result.Reason) {
            case ConnectionFailedReason.InvalidAddons:
                SetFeedbackText(Color.red, "Failed to connect:\nInvalid addons");
                break;
            case ConnectionFailedReason.SocketException:
            case ConnectionFailedReason.IOException:
                SetFeedbackText(Color.red, "Failed to connect:\nInternal error");
                break;
            case ConnectionFailedReason.TimedOut:
                SetFeedbackText(Color.red, "Failed to connect:\nConnection timed out");
                break;
            case ConnectionFailedReason.Other:
                var message = ((ConnectionFailedMessageResult) result).Message;
                // Server rejection reasons are sent in English, so translate the known ones for the local UI
                // (unknown reasons are shown as is).
                SetFeedbackText(Color.red, Localization.Format("Failed to connect:\n{0}", Localization.Get(message)));
                break;
            default:
                SetFeedbackText(Color.red, "Failed to connect:\nUnknown reason");
                break;
        }

        // Enable the connect button again
        SetConnectionButtonText(ConnectText);
        _connectionButton.SetInteractable(true);
    }

    /// <summary>
    /// Create the connection UI.
    /// </summary>
    private void CreateConnectUi() {
        // Now we can start adding individual components to our UI
        // Keep track of current x and y of objects we want to place
        var x = 1920f / 2f;
        var y = 1080f - 400f;

        CreateServerListUi(x);

        const float labelHeight = 20f;

        // ReSharper disable once ObjectCreationAsStatement
        _usernameLabel = new TextComponent(
            _connectGroup,
            new Vector2(x + TextIndentWidth, y),
            new Vector2(212f, labelHeight),
            Localization.Get("Username"),
            UiManager.NormalFontSize,
            alignment: TextAnchor.MiddleLeft
        );

        y -= labelHeight + 14f;

        _usernameInput = new InputComponent(
            _connectGroup,
            new Vector2(x, y),
            _modSettings.Username,
            Localization.Get("Username"),
            characterLimit: 20,
            onValidateInput: (_, _, addedChar) => char.IsLetterOrDigit(addedChar) ? addedChar : '\0'
        );

        y -= InputComponent.DefaultHeight + 20f;

        // ReSharper disable once ObjectCreationAsStatement
        _serverLabel = new TextComponent(
            _connectGroup,
            new Vector2(x + TextIndentWidth, y),
            new Vector2(212f, labelHeight),
            Localization.Get("Server IP and port"),
            UiManager.NormalFontSize,
            alignment: TextAnchor.MiddleLeft
        );

        y -= labelHeight + 14f;

        _addressInput = new IpInputComponent(
            _connectGroup,
            new Vector2(x, y),
            _modSettings.ConnectAddress,
            Localization.Get("IP Address")
        );

        y -= InputComponent.DefaultHeight + 8f;

        var joinPort = _modSettings.ConnectPort;
        _portInput = new PortInputComponent(
            _connectGroup,
            new Vector2(x, y),
            joinPort == -1 ? "" : joinPort.ToString(),
            Localization.Get("Port")
        );

        y -= InputComponent.DefaultHeight + 20f;

        // ReSharper disable once ObjectCreationAsStatement
        _passwordLabel = new TextComponent(
            _connectGroup,
            new Vector2(x + TextIndentWidth, y),
            new Vector2(212f, labelHeight),
            Localization.Get("Password"),
            UiManager.NormalFontSize,
            alignment: TextAnchor.MiddleLeft
        );

        y -= labelHeight + 14f;

        _passwordInput = new InputComponent(
            _connectGroup,
            new Vector2(x, y),
            "",
            Localization.Get("Password"),
            characterLimit: ClientInfo.MaxPasswordLength
        );

        y -= InputComponent.DefaultHeight + 20f;

        _findServerButton = new ButtonComponent(
            _connectGroup,
            new Vector2(x, y),
            Localization.Get("Find Server")
        );
        _findServerButton.SetOnPress(OnFindServerButtonPressed);

        y -= ButtonComponent.DefaultHeight + 8f;

        _connectionButton = new ButtonComponent(
            _connectGroup,
            new Vector2(x, y),
            Localization.Get(ConnectText)
        );
        _connectionButton.SetOnPress(OnConnectButtonPressed);

        y -= ButtonComponent.DefaultHeight + 8f;

        _serverButton = new ButtonComponent(
            _connectGroup,
            new Vector2(x, y),
            Localization.Get(StartHostingText)
        );
        _serverButton.SetOnPress(OnStartButtonPressed);

        y -= ButtonComponent.DefaultHeight + 8f;

        _feedbackText = new TextComponent(
            _connectGroup,
            new Vector2(x, y),
            new Vector2(240f, labelHeight),
            new Vector2(0.5f, 1f),
            "",
            UiManager.SubTextFontSize,
            alignment: TextAnchor.UpperCenter
        );
        _feedbackText.SetActive(false);
    }

    /// <summary>
    /// Create the server list above the connection fields: a header, a row for every stored server and a button
    /// per row to toggle the favourite.
    /// </summary>
    /// <param name="x">The horizontal center of the connect interface.</param>
    private void CreateServerListUi(float x) {
        _serverList = new ServerListStore();

        _serverListLabel = new TextComponent(
            _connectGroup,
            new Vector2(x + TextIndentWidth, 712f),
            new Vector2(212f, 20f),
            Localization.Get("Saved servers"),
            UiManager.NormalFontSize,
            alignment: TextAnchor.MiddleLeft
        );

        for (var i = 0; i < ServerRowCount; i++) {
            var y = 744f + i * 36f;

            var mainButton = new ButtonComponent(
                _connectGroup,
                new Vector2(x - 22f, y),
                new Vector2(196f, 30f),
                "",
                TextureManager.ButtonBg,
                FontManager.UIFontRegular,
                UiManager.SubTextFontSize
            );

            var starButton = new ButtonComponent(
                _connectGroup,
                new Vector2(x + 100f, y),
                new Vector2(34f, 30f),
                "",
                TextureManager.ButtonBg,
                FontManager.UIFontRegular,
                UiManager.NormalFontSize
            );

            var row = new ServerRow {
                Main = mainButton,
                Star = starButton
            };

            mainButton.SetOnPress(() => OnServerRowPressed(row));
            starButton.SetOnPress(() => OnServerStarPressed(row));

            _serverRows.Add(row);
        }

        RefreshServerListUi();
    }

    /// <summary>
    /// Fill the server list rows with the stored servers and hide the rows that are not used.
    /// </summary>
    private void RefreshServerListUi() {
        if (_serverList == null) {
            return;
        }

        var entries = _serverList.GetOrderedEntries();

        if (_serverListLabel != null) {
            _serverListLabel.SetActive(entries.Count > 0);
        }

        for (var i = 0; i < _serverRows.Count; i++) {
            var row = _serverRows[i];

            if (i >= entries.Count) {
                row.Entry = null;
                row.Main.SetActive(false);
                row.Star.SetActive(false);
                continue;
            }

            var entry = entries[i];
            row.Entry = entry;

            row.Main.SetActive(true);
            row.Star.SetActive(true);

            row.Main.SetText((entry.Favorite ? "★ " : "") + entry.DisplayName);
            row.Star.SetText(entry.Favorite ? "★" : "☆");
        }
    }

    /// <summary>
    /// Callback method for when a server in the list is pressed: fill in its address and port.
    /// </summary>
    /// <param name="row">The row that was pressed.</param>
    private void OnServerRowPressed(ServerRow row) {
        var entry = row.Entry;
        if (entry == null) {
            return;
        }

        _addressInput.SetInput(entry.Address);
        _portInput.SetInput(entry.Port.ToString());
        _pendingServerName = entry.Name;
        _pendingServerAddress = entry.Address;
        _pendingServerPort = entry.Port;

        SetFeedbackText(Color.green, Localization.Format("Selected server: {0}", entry.DisplayName));
    }

    /// <summary>
    /// Callback method for when the favourite button of a server is pressed.
    /// </summary>
    /// <param name="row">The row whose favourite button was pressed.</param>
    private void OnServerStarPressed(ServerRow row) {
        if (row.Entry == null || _serverList == null) {
            return;
        }

        _serverList.ToggleFavorite(row.Entry);
        RefreshServerListUi();
    }

    /// <summary>
    /// Callback method for when the connect button is pressed.
    /// </summary>
    private void OnConnectButtonPressed() {
        var address = _addressInput.GetInput();

        if (address.Length == 0) {
            // Let the user know that the address is empty
            SetFeedbackText(Color.red, "Failed to connect:\nYou must enter an address");

            return;
        }

        var portString = _portInput.GetInput();

        var parsedPort = int.TryParse(portString, out var port);
        if (!parsedPort || port == 0) {
            // Let the user know that the entered port is incorrect
            SetFeedbackText(Color.red, "Failed to connect:\nYou must enter a valid port");

            return;
        }

        // Forget the name of a previously selected server when the address or port was changed by hand
        if (!string.Equals(_pendingServerAddress, address, StringComparison.OrdinalIgnoreCase)
            || _pendingServerPort != port) {
            _pendingServerName = null;
        }

        Logger.Debug($"Connect button pressed, address: {address}:{port}");

        if (!ValidateUsername(out var username)) {
            return;
        }

        // Input values were valid, so we can store them in the settings
        Logger.Debug($"Saving join address {address} in global settings");
        Logger.Debug($"Saving join port {port} in global settings");
        Logger.Debug($"Saving join username {username} in global settings");
        _modSettings.ConnectAddress = address;
        _modSettings.ConnectPort = port;
        _modSettings.Username = username;

        // Disable the connect button while we are trying to establish a connection
        SetConnectionButtonText(ConnectingText);
        _connectionButton.SetInteractable(false);

        ConnectButtonPressed?.Invoke(address, port, username, _passwordInput.GetInput());
    }

    /// <summary>
    /// Callback method for when the find server button is pressed.
    /// </summary>
    private void OnFindServerButtonPressed() {
        if (_findServerCoroutine != null) {
            MonoBehaviourUtil.Instance.StopCoroutine(_findServerCoroutine);
        }

        _findServerCoroutine = MonoBehaviourUtil.Instance.StartCoroutine(FindLanServerCoroutine());
    }

    /// <summary>
    /// Coroutine that searches for a server on the local network and fills in its address and port.
    /// </summary>
    /// <returns>An enumerator for the coroutine.</returns>
    private IEnumerator FindLanServerCoroutine() {
        SetFeedbackText(Color.yellow, "Searching for a server on the local network...");
        _findServerButton.SetInteractable(false);

        _lanListener ??= new LanDiscoveryListener();

        try {
            _lanListener.Start();
        } catch (Exception exception) {
            Logger.Warn($"Failed to search for servers on the local network: {exception.Message}");

            SetFeedbackText(Color.red, "Failed to search for a server on the local network");
            _findServerButton.SetInteractable(true);
            _findServerCoroutine = null;
            yield break;
        }

        // Give the servers a few seconds to answer the search
        var timeout = Time.time + 2.5f;
        while (Time.time < timeout && _lanListener.GetFirstServer() == null) {
            yield return null;
        }

        var server = _lanListener.GetFirstServer();
        _lanListener.Stop();

        _findServerButton.SetInteractable(true);
        _findServerCoroutine = null;

        if (server == null) {
            SetFeedbackText(Color.red, "No server found on the local network");
            yield break;
        }

        _addressInput.SetInput(server.Address);
        _portInput.SetInput(server.Port.ToString());

        _pendingServerName = server.Name;
        _pendingServerAddress = server.Address;
        _pendingServerPort = server.Port;

        if (server.PasswordRequired) {
            SetFeedbackText(
                Color.green,
                Localization.Format("Found server: {0} (password required)", server.Name)
            );
        } else {
            SetFeedbackText(Color.green, Localization.Format("Found server: {0}", server.Name));
        }
    }

    /// <summary>
    /// Callback method for when the start hosting button is pressed.
    /// </summary>
    private void OnStartButtonPressed() {
        var portString = _portInput.GetInput();

        var parsedPort = int.TryParse(portString, out var port);
        if (!parsedPort || port == 0) {
            // Let the user know that the entered port is incorrect
            SetFeedbackText(Color.red, "Failed to host:\nYou must enter a valid port");

            return;
        }
        
        if (!ValidateUsername(out var username)) {
            return;
        }

        // Start the server in networkManager
        StartHostButtonPressed?.Invoke(username, port);
    }

    /// <summary>
    /// Set the text of the connection button, keeping track of the English text so it can be localised again when
    /// the game language changes.
    /// </summary>
    /// <param name="text">The English text of the button.</param>
    private void SetConnectionButtonText(string text) {
        _connectionButtonText = text;
        _connectionButton.SetText(Localization.Get(text));
    }

    /// <summary>
    /// Set the feedback text with the given color and content.
    /// </summary>
    /// <param name="color">The color of the text.</param>
    /// <param name="text">The content of the text.</param>
    private void SetFeedbackText(Color color, string text) {
        _feedbackText.SetColor(color);
        _feedbackText.SetText(Localization.Get(text));
        _feedbackText.SetActive(true);

        if (_feedbackHideCoroutine != null) {
            MonoBehaviourUtil.Instance.StopCoroutine(_feedbackHideCoroutine);
        }

        _feedbackHideCoroutine = MonoBehaviourUtil.Instance.StartCoroutine(WaitHideFeedbackText());
    }

    /// <summary>
    /// Coroutine for hiding the feedback text after a delay.
    /// </summary>
    /// <returns>An enumerator for the coroutine.</returns>
    private IEnumerator WaitHideFeedbackText() {
        yield return new WaitForSeconds(FeedbackTextHideTime);

        _feedbackText.SetActive(false);
    }

    private bool ValidateUsername(out string username) {
        username = _usernameInput.GetInput();

        if (username.Length == 0) {
            SetFeedbackText(Color.red, "Failed to connect:\nYou must enter a username");
            return false;
        }

        if (username.Length > ServerManager.UsernameMaxLength) {
            SetFeedbackText(Color.red, "Failed to connect:\nUsername is too long");
            return false;
        }

        return true;
    }
}
