using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Ui;

/// <summary>
/// A single server in the server list of the connect interface.
/// </summary>
internal class ServerListEntry {
    /// <summary>
    /// The address of the server.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// The port of the server.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The name of the server, if it was found on the local network.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Whether the server was marked as a favourite.
    /// </summary>
    public bool Favorite { get; set; }

    /// <summary>
    /// The time the server was last connected to, in UTC ticks. Used to sort the history.
    /// </summary>
    public long LastUsedTicks { get; set; }

    /// <summary>
    /// The name that is shown in the server list.
    /// </summary>
    [JsonIgnore]
    public string DisplayName {
        get {
            if (!string.IsNullOrEmpty(Name)) {
                return Name;
            }

            return $"{Address}:{Port}";
        }
    }
}

/// <summary>
/// Persistent list of servers that were connected to before, with favourites that are always shown first and a
/// history of the most recently used servers. The list is stored as JSON next to the other mod settings.
/// </summary>
internal class ServerListStore {
    /// <summary>
    /// Maximum number of servers that are stored.
    /// </summary>
    private const int MaxEntries = 12;

    /// <summary>
    /// Path of the file the list is stored in.
    /// </summary>
    private static readonly string FilePath = Path.Combine(
        Application.persistentDataPath,
        "HkmpServerList.json"
    );

    /// <summary>
    /// The stored servers.
    /// </summary>
    private readonly List<ServerListEntry> _entries = new List<ServerListEntry>();

    public ServerListStore() {
        Load();
    }

    /// <summary>
    /// The servers in the list, favourites first and then the most recently used ones.
    /// </summary>
    /// <returns>The ordered servers.</returns>
    public List<ServerListEntry> GetOrderedEntries() {
        var ordered = new List<ServerListEntry>(_entries);
        ordered.Sort((first, second) => {
            if (first.Favorite != second.Favorite) {
                return first.Favorite ? -1 : 1;
            }

            return second.LastUsedTicks.CompareTo(first.LastUsedTicks);
        });

        return ordered;
    }

    /// <summary>
    /// Add a server to the list or update it when it is already present, and mark it as the most recently used.
    /// </summary>
    /// <param name="address">The address of the server.</param>
    /// <param name="port">The port of the server.</param>
    /// <param name="name">The name of the server, if known.</param>
    public void AddOrUpdate(string address, int port, string name) {
        if (string.IsNullOrEmpty(address) || port <= 0) {
            return;
        }

        ServerListEntry entry = null;
        foreach (var existing in _entries) {
            if (existing.Port == port && string.Equals(existing.Address, address, StringComparison.OrdinalIgnoreCase)) {
                entry = existing;
                break;
            }
        }

        if (entry == null) {
            entry = new ServerListEntry {
                Address = address,
                Port = port
            };
            _entries.Add(entry);
        }

        if (!string.IsNullOrEmpty(name)) {
            entry.Name = name;
        }

        entry.LastUsedTicks = DateTime.UtcNow.Ticks;

        // Drop the oldest servers that are not favourites when the list grows too large
        while (_entries.Count > MaxEntries) {
            ServerListEntry oldest = null;
            foreach (var candidate in _entries) {
                if (candidate.Favorite) {
                    continue;
                }

                if (oldest == null || candidate.LastUsedTicks < oldest.LastUsedTicks) {
                    oldest = candidate;
                }
            }

            if (oldest == null) {
                break;
            }

            _entries.Remove(oldest);
        }

        Save();
    }

    /// <summary>
    /// Toggle whether the given server is a favourite and save the list.
    /// </summary>
    /// <param name="entry">The server to toggle.</param>
    public void ToggleFavorite(ServerListEntry entry) {
        if (entry == null) {
            return;
        }

        entry.Favorite = !entry.Favorite;
        Save();
    }

    /// <summary>
    /// Load the list from disk. A missing or broken file results in an empty list.
    /// </summary>
    private void Load() {
        try {
            if (!File.Exists(FilePath)) {
                return;
            }

            var json = File.ReadAllText(FilePath);
            var entries = JsonConvert.DeserializeObject<List<ServerListEntry>>(json);

            if (entries == null) {
                return;
            }

            _entries.AddRange(entries);
        } catch (Exception exception) {
            Logger.Warn($"Failed to load the server list: {exception.Message}");
        }
    }

    /// <summary>
    /// Save the list to disk.
    /// </summary>
    private void Save() {
        try {
            var json = JsonConvert.SerializeObject(_entries, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        } catch (Exception exception) {
            Logger.Warn($"Failed to save the server list: {exception.Message}");
        }
    }
}
