using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Hkmp.Game.Settings;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Networking;

/// <summary>
/// A server that was found by searching the local network.
/// </summary>
internal class LanServer {
    /// <summary>
    /// The IP address of the server.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// The game port of the server.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// The name of the server.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The message of the day of the server.
    /// </summary>
    public string Motd { get; }

    /// <summary>
    /// Whether the server requires a password to connect.
    /// </summary>
    public bool PasswordRequired { get; }

    public LanServer(string address, int port, string name, string motd, bool passwordRequired) {
        Address = address;
        Port = port;
        Name = name;
        Motd = motd;
        PasswordRequired = passwordRequired;
    }
}

/// <summary>
/// Class that announces a hosted server on the local network and helps finding such servers.
/// </summary>
internal static class LanDiscovery {
    /// <summary>
    /// The UDP port used for the discovery messages.
    /// </summary>
    public const int DiscoveryPort = 47777;

    /// <summary>
    /// The prefix of every discovery message, used to recognise our own messages.
    /// </summary>
    private const string MessagePrefix = "HKMP1";

    /// <summary>
    /// The time in milliseconds between two announcements of the server.
    /// </summary>
    private const int BeaconInterval = 2000;

    /// <summary>
    /// The thread that announces the server on the local network.
    /// </summary>
    private static Thread _beaconThread;

    /// <summary>
    /// Whether the beacon thread should keep running.
    /// </summary>
    private static volatile bool _beaconRunning;

    /// <summary>
    /// Start announcing a server with the given port and settings on the local network.
    /// </summary>
    /// <param name="gamePort">The port the game server listens on.</param>
    /// <param name="serverSettings">The settings of the server.</param>
    public static void StartBeacon(int gamePort, ServerSettings serverSettings) {
        StopBeacon();

        if (serverSettings == null) {
            return;
        }

        var serverName = serverSettings.ServerName;
        if (string.IsNullOrEmpty(serverName)) {
            serverName = "Hollow Knight Co-op";
        }

        var message = string.Join(
            "|",
            MessagePrefix,
            gamePort.ToString(),
            string.IsNullOrEmpty(serverSettings.Password) ? "0" : "1",
            Sanitize(serverName),
            Sanitize(serverSettings.Motd ?? "")
        );

        _beaconRunning = true;
        _beaconThread = new Thread(() => BeaconLoop(message)) {
            IsBackground = true,
            Name = "HKMP LAN beacon"
        };
        _beaconThread.Start();

        Logger.Info($"Announcing server on the local network as '{serverName}' on port {gamePort}");
    }

    /// <summary>
    /// Stop announcing the server on the local network.
    /// </summary>
    public static void StopBeacon() {
        _beaconRunning = false;

        var thread = _beaconThread;
        _beaconThread = null;

        if (thread != null && thread.IsAlive) {
            thread.Join(500);
        }
    }

    /// <summary>
    /// The loop of the beacon thread that sends the announcement message every few seconds.
    /// </summary>
    /// <param name="message">The message to send.</param>
    private static void BeaconLoop(string message) {
        var payload = Encoding.UTF8.GetBytes(message);
        var endPoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        UdpClient client = null;
        try {
            client = new UdpClient { EnableBroadcast = true };

            while (_beaconRunning) {
                try {
                    client.Send(payload, payload.Length, endPoint);
                } catch (Exception exception) {
                    Logger.Debug($"Failed to send LAN discovery announcement: {exception.Message}");
                }

                // Sleep in small steps, so stopping the beacon does not have to wait for the whole interval
                for (var elapsed = 0; elapsed < BeaconInterval && _beaconRunning; elapsed += 100) {
                    Thread.Sleep(100);
                }
            }
        } catch (Exception exception) {
            Logger.Warn($"Failed to start LAN discovery announcement: {exception.Message}");
        } finally {
            client?.Close();
        }
    }

    /// <summary>
    /// Replace characters that would break the discovery message format.
    /// </summary>
    /// <param name="value">The value to sanitize.</param>
    /// <returns>The sanitized value.</returns>
    private static string Sanitize(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }

        return value
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    /// <summary>
    /// Try to parse a discovery message into a server description.
    /// </summary>
    /// <param name="message">The received message.</param>
    /// <param name="address">The address the message came from.</param>
    /// <param name="server">The parsed server, if parsing succeeded.</param>
    /// <returns>true if the message is a valid discovery message, false otherwise.</returns>
    public static bool TryParseMessage(string message, string address, out LanServer server) {
        server = null;

        if (string.IsNullOrEmpty(message)) {
            return false;
        }

        var parts = message.Split(new[] { '|' }, 5);
        if (parts.Length < 2 || parts[0] != MessagePrefix) {
            return false;
        }

        if (!int.TryParse(parts[1], out var port) || port <= 0 || port > 65535) {
            return false;
        }

        var passwordRequired = parts.Length > 2 && parts[2] == "1";
        var name = parts.Length > 3 && parts[3].Length > 0 ? parts[3] : "Hollow Knight Co-op";
        var motd = parts.Length > 4 ? parts[4] : "";

        server = new LanServer(address, port, name, motd, passwordRequired);
        return true;
    }
}

/// <summary>
/// Listener that collects server announcements on the local network.
/// </summary>
internal class LanDiscoveryListener : IDisposable {
    /// <summary>
    /// The UDP client used to receive the announcements.
    /// </summary>
    private UdpClient _client;

    /// <summary>
    /// The thread that receives the announcements.
    /// </summary>
    private Thread _thread;

    /// <summary>
    /// Whether the listener is currently running.
    /// </summary>
    private volatile bool _running;

    /// <summary>
    /// Lock used to protect the list of found servers.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// The servers that were found since the listener was started.
    /// </summary>
    private readonly List<LanServer> _servers = [];

    /// <summary>
    /// Start listening for server announcements on the local network.
    /// </summary>
    /// <exception cref="SocketException">Thrown when the discovery port cannot be bound.</exception>
    public void Start() {
        if (_running) {
            return;
        }

        lock (_lock) {
            _servers.Clear();
        }

        var client = new UdpClient();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, LanDiscovery.DiscoveryPort));

        _client = client;
        _running = true;
        _thread = new Thread(() => ReceiveLoop(client)) {
            IsBackground = true,
            Name = "HKMP LAN discovery listener"
        };
        _thread.Start();
    }

    /// <summary>
    /// Stop listening for server announcements.
    /// </summary>
    public void Stop() {
        _running = false;

        try {
            _client?.Close();
        } catch (Exception) {
            // Ignore exceptions when closing the client
        }

        var thread = _thread;
        _thread = null;

        if (thread != null && thread.IsAlive) {
            thread.Join(500);
        }

        _client = null;
    }

    /// <summary>
    /// Get the first server that was found, or null if no server was found.
    /// </summary>
    /// <returns>The first found server, or null.</returns>
    public LanServer GetFirstServer() {
        lock (_lock) {
            return _servers.Count > 0 ? _servers[0] : null;
        }
    }

    /// <summary>
    /// The number of servers that were found.
    /// </summary>
    public int ServerCount {
        get {
            lock (_lock) {
                return _servers.Count;
            }
        }
    }

    /// <summary>
    /// The loop of the listener thread that receives announcements.
    /// </summary>
    /// <param name="client">The UDP client to receive with.</param>
    private void ReceiveLoop(UdpClient client) {
        var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (_running) {
            try {
                var data = client.Receive(ref remoteEndPoint);
                var message = Encoding.UTF8.GetString(data);

                if (!LanDiscovery.TryParseMessage(
                        message,
                        remoteEndPoint.Address.ToString(),
                        out var server
                    )) {
                    continue;
                }

                lock (_lock) {
                    var duplicate = false;
                    foreach (var found in _servers) {
                        if (found.Address == server.Address && found.Port == server.Port) {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate) {
                        _servers.Add(server);
                    }
                }
            } catch (ObjectDisposedException) {
                break;
            } catch (SocketException) {
                if (!_running) {
                    break;
                }
            } catch (Exception exception) {
                Logger.Debug($"Failed to receive LAN discovery message: {exception.Message}");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        Stop();
    }
}
