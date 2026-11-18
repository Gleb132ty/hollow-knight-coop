using System;
using System.Collections.Generic;
using Hkmp.Ui.Resources;

namespace Hkmp;

/// <summary>
/// Static class that provides localisation for the UI of the mod. The English string itself is used as the lookup
/// key, so any string that is not present in the Russian table simply falls back to the original English text.
/// Only English and Russian are supported by this mod.
/// </summary>
internal static class Localization {
    /// <summary>
    /// Dictionary containing the Russian translations, keyed by the English text.
    /// </summary>
    private static readonly Dictionary<string, string> Russian = new() {
        // Connect interface
        { "Username", "Имя пользователя" },
        { "Server IP and port", "IP-адрес и порт сервера" },
        { "IP Address", "IP-адрес" },
        { "Port", "Порт" },
        { "Connect", "Подключиться" },
        { "Connecting...", "Подключение..." },
        { "Start Hosting", "Начать хостинг" },
        { "Password", "Пароль" },
        { "Find Server", "Найти сервер" },
        { "Searching for a server on the local network...", "Идёт поиск сервера в локальной сети..." },
        { "Failed to search for a server on the local network", "Не удалось выполнить поиск сервера в локальной сети" },
        { "No server found on the local network", "Сервер в локальной сети не найден" },
        { "Found server: {0}", "Найден сервер: {0}" },
        { "Found server: {0} (password required)", "Найден сервер: {0} (нужен пароль)" },
        { "Successfully connected\n{0}", "Подключение выполнено\n{0}" },
        { "Incorrect password", "Неверный пароль" },
        { "Server is full", "Сервер заполнен" },
        { "No limit", "Без ограничения" },
        { "Successfully connected", "Подключение выполнено" },
        { "Failed to connect:\nInvalid addons", "Не удалось подключиться:\nНесовместимые аддоны" },
        { "Failed to connect:\nInternal error", "Не удалось подключиться:\nВнутренняя ошибка" },
        { "Failed to connect:\nConnection timed out", "Не удалось подключиться:\nВремя ожидания истекло" },
        { "Failed to connect:\nUnknown reason", "Не удалось подключиться:\nНеизвестная причина" },
        { "Failed to connect:\n{0}", "Не удалось подключиться:\n{0}" },
        { "Failed to connect:\nYou must enter an address", "Не удалось подключиться:\nВведите адрес" },
        { "Failed to connect:\nYou must enter a valid port", "Не удалось подключиться:\nВведите корректный порт" },
        { "Failed to connect:\nYou must enter a username", "Не удалось подключиться:\nВведите имя пользователя" },
        { "Failed to connect:\nUsername is too long", "Не удалось подключиться:\nИмя пользователя слишком длинное" },
        { "Failed to host:\nYou must enter a valid port", "Не удалось начать хостинг:\nВведите корректный порт" },

        // Main menu
        { "Start Multiplayer", "Начать многопользовательскую игру" },
        { "You will be disconnected", "Вы будете отключены" },

        // Mod menu
        { "Settings", "Настройки" },
        { "Player skin ID", "ID скина игрока" },
        { "Display ping", "Показывать пинг" },
        { "Team selection", "Выбор команды" },
        { "None", "Нет" },
        { "Moss", "Мох" },
        { "Hive", "Улей" },
        { "Grimm", "Гримм" },
        { "Lifeblood", "Жизнетворение" },
        { "Client Settings", "Настройки клиента" },
        { "Menu for changing the settings of the client", "Меню изменения настроек клиента" },
        { "Server Settings", "Настройки сервера" },
        { "Menu for changing the settings of the server for authorized players", "Меню изменения настроек сервера для авторизованных игроков" },
        { "Team", "Команда" },
        { "Skin", "Скин" },
        { "Off", "Выкл" },
        { "On", "Вкл" },
        { "Full Synchronisation", "Полная синхронизация" },
        { "Synchronise enemies, bosses, world changes, and saves in multiplayer games", "Синхронизировать врагов, боссов, изменения мира и сохранения в многопользовательской игре" },
        { "Ping Display", "Отображение пинга" },
        { "HUD element that shows the player's ping in multiplayer games", "Элемент интерфейса, показывающий пинг игрока в многопользовательской игре" },
        { "Enemy Health Bars", "Полоски здоровья врагов" },
        { "Show a health bar above damaged enemies and bosses", "Показывать полоску здоровья над повреждёнными врагами и боссами" },
        { "Enemy Health Multiplier", "Множитель здоровья врагов" },
        { "Multiplies the maximum health of enemies and bosses while connected", "Умножает максимальное здоровье врагов и боссов при подключении" },
        { "Revive Teammates", "Возрождение напарников" },
        { "Allow players to revive downed teammates by defeating the soul that rises from their body", "Позволяет игрокам возрождать упавших напарников, побеждая душу, восставшую из их тела" },
        { "Soul Hit Points", "Здоровье души" },
        { "Number of hit points of the soul that must be defeated to revive a teammate", "Количество единиц здоровья души, которую нужно победить для возрождения напарника" },
        { "Enemy Targeting", "Наведение врагов" },
        { "Make enemies target the closest living player and bosses switch targets", "Враги выбирают целью ближайшего живого игрока, боссы переключают цель" },
        { "Gate Hold Open", "Удержание ворот открытыми" },
        { "Keep battle gates open until every player has entered the arena", "Держать боевые ворота открытыми, пока все игроки не войдут на арену" },
        { "Map Icons With Compass", "Иконки напарников на карте с Компасом" },
        { "Show other players on the map only while the Wayward Compass charm is equipped", "Показывать напарников на карте, только если надет Оберег блуждающего компаса" },
        { "Teammate HUD", "Интерфейс напарников" },
        { "Show the list of teammates with their masks and a blinking mask marker that dissolves when the teammate is visible", "Показывать список напарников с их масками и мигающую метку-маску, которая растворяется, когда напарник виден" },
        { "Spectator Camera", "Камера наблюдателя" },
        { "While downed, the camera follows a living teammate", "Пока игрок лежит, камера следует за живым напарником" },
        { "Revive Health", "Здоровье при возрождении" },
        { "Amount of masks a revived player reappears with", "Сколько масок здоровья получает возрождённый игрок" },
        { "Reviver Heal", "Лечение возродившего" },
        { "Amount of masks the player that defeated the soul is healed for", "Сколько масок здоровья восстанавливается тому, кто победил душу" },
        { "Downed", "лежит" },
        { "Other location", "другая локация" },
        { "No data", "нет данных" },
        { "Key to open the chat", "Клавиша открытия чата" },
        { "Key to give up and respawn at the last bench", "Клавиша отказа и возрождения на последней скамейке" },
        { "Key to show session statistics", "Клавиша показа статистики сессии" },
        { "Auto Reconnect", "Автоматическое переподключение" },
        { "Reconnect to the last server automatically when the connection is lost", "Переподключаться к последнему серверу при потере соединения" },
        { "Session Statistics", "Статистика сессии" },
        { "Show the time, downs, revives and give-ups of the current session in chat", "Показывать в чате время, падения, возрождения и отказы текущей сессии" },
        { "Arena Sync", "Синхронизация арен" },
        { "Share Colosseum and Godhome progress and update arena doors immediately", "Делить прогресс Колизея и Годсхолма и сразу обновлять двери арен" },
        { "Settings Profile", "Профиль настроек" },
        { "Apply a ready-made set of settings for a relaxed, a hard or a testing game; reopen the menu to see the changed values", "Применить готовый набор настроек для спокойной, сложной или тестовой игры; откройте меню заново, чтобы увидеть изменения" },
        { "Casual", "Спокойный" },
        { "Hardcore", "Хардкор" },
        { "Test", "Тестовый" },
        { "Custom", "Свой" },
        { "Teammate Colors", "Цвета напарников" },
        { "Give every teammate their own color in the HUD and above their character", "Дать каждому напарнику свой цвет в интерфейсе и над персонажем" },
        { "Player Color", "Цвет игрока" },
        { "Color of your name and your row in the teammate HUD", "Цвет вашего ника и вашей строки в интерфейсе напарников" },
        { "Auto", "Авто" },
        { "Green", "Зелёный" },
        { "Blue", "Синий" },
        { "Yellow", "Жёлтый" },
        { "Purple", "Фиолетовый" },
        { "Orange", "Оранжевый" },
        { "Cyan", "Голубой" },
        { "Pink", "Розовый" },
        { "Lime", "Салатовый" },
        { "HUD Scale", "Масштаб интерфейса" },
        { "Scale of the teammate HUD", "Масштаб интерфейса напарников" },
        { "Compact HUD", "Компактный интерфейс" },
        { "Use a single row per teammate in the teammate HUD", "Показывать каждого напарника одной строкой в интерфейсе" },
        { "Show Ping In HUD", "Пинг в интерфейсе" },
        { "Show the ping of every teammate in the teammate HUD", "Показывать пинг каждого напарника в интерфейсе" },
        { "Avatar Smoothing", "Сглаживание напарников" },
        { "Smooth the movement of teammates between network updates", "Сглаживать движение напарников между сетевыми обновлениями" },
        { "Heal Teammate", "Лечение напарника" },
        { "Spend soul to heal the closest injured teammate with the heal keybind", "Тратить душу, чтобы лечить ближайшего раненого напарника клавишей лечения" },
        { "Boss Health Scaling", "Здоровье боссов по числу игроков" },
        { "Scale the maximum health of bosses with the number of players in the scene", "Увеличивать максимальное здоровье боссов по числу игроков в сцене" },
        { "Boss Health Per Player", "Здоровье босса за игрока" },
        { "Extra boss health for every additional player in the scene, as a fraction of its base health", "Дополнительное здоровье босса за каждого игрока в сцене, доля от базового здоровья" },
        { "Share Map", "Общая карта" },
        { "Share visited and mapped scenes with the team", "Делить с командой посещённые и нанесённые на карту сцены" },
        { "Share Journal", "Общий журнал охотника" },
        { "Share Hunter's Journal kill counts and entries with the team", "Делить с командой счётчики убийств и записи журнала охотника" },
        { "Share Essence", "Общая эссенция" },
        { "Share dream essence with the team", "Делить с командой эссенцию снов" },
        { "Share Rewards", "Общие награды" },
        { "Share rescued grubs, Grubfather rewards and Sly's and Salubra's stock with the team", "Делить с командой спасённых личинок, награды Отца личинок и товары Слая и Салубры" },
        { "Key to heal the closest injured teammate", "Клавиша лечения ближайшего раненого напарника" },
        { "Select one of several teams", "Выберите одну из команд" },
        { "Team selection is currently disabled", "Выбор команды сейчас отключён" },
        { "Select a skin", "Выберите скин" },
        { "Skin selection is currently disabled", "Выбор скина сейчас отключён" },

        // Server settings
        { "server.IsPvpEnabled.label", "PvP" },
        { "server.IsPvpEnabled.desc", "Урон между игроками" },
        { "server.IsBodyDamageEnabled.label", "Урон от столкновений" },
        { "server.IsBodyDamageEnabled.desc", "Если PvP включено, наносят ли урон хитбоксы игроков" },
        { "server.AlwaysShowMapIcons.label", "Иконки на карте для всех" },
        { "server.AlwaysShowMapIcons.desc", "Всегда показывать иконки всех игроков на карте" },
        { "server.OnlyBroadcastMapIconWithWaywardCompass.label", "Иконки на карте с Компасом" },
        { "server.OnlyBroadcastMapIconWithWaywardCompass.desc", "Показывать иконки на карте, только если надет Оберег блуждающего компаса" },
        { "server.DisplayNames.label", "Показывать имена" },
        { "server.DisplayNames.desc", "Показывать имена игроков над их персонажами" },
        { "server.TeamsEnabled.label", "Команды" },
        { "server.TeamsEnabled.desc", "Могут ли игроки объединяться в команды" },
        { "server.AllowSkins.label", "Скины" },
        { "server.AllowSkins.desc", "Могут ли игроки использовать скины" },
        { "server.AllowParries.label", "Парирование" },
        { "server.AllowParries.desc", "Возможно ли парирование некоторых атак игроков" },
        { "server.NailDamage.label", "Урон гвоздя" },
        { "server.NailDamage.desc", "Количество масок урона от удара гвоздём игрока" },
        { "server.GrubberflyElegyDamage.label", "Урон Элегии мухокрыла" },
        { "server.GrubberflyElegyDamage.desc", "Количество масок урона от Элегии мухокрыла" },
        { "server.VengefulSpiritDamage.label", "Урон Мстительного духа" },
        { "server.VengefulSpiritDamage.desc", "Количество масок урона от Мстительного духа" },
        { "server.ShadeSoulDamage.label", "Урон Тёмной души" },
        { "server.ShadeSoulDamage.desc", "Количество масок урона от Тёмной души" },
        { "server.DesolateDiveDamage.label", "Урон Опустошающего нырка" },
        { "server.DesolateDiveDamage.desc", "Количество масок урона от Опустошающего нырка" },
        { "server.DescendingDarkDamage.label", "Урон Нисходящей тьмы" },
        { "server.DescendingDarkDamage.desc", "Количество масок урона от Нисходящей тьмы" },
        { "server.HowlingWraithDamage.label", "Урон Воющих духов" },
        { "server.HowlingWraithDamage.desc", "Количество масок урона от Воющих духов" },
        { "server.AbyssShriekDamage.label", "Урон Крика Бездны" },
        { "server.AbyssShriekDamage.desc", "Количество масок урона от Крика Бездны" },
        { "server.GreatSlashDamage.label", "Урон Великого удара" },
        { "server.GreatSlashDamage.desc", "Количество масок урона от Великого удара" },
        { "server.DashSlashDamage.label", "Урон Рывкового удара" },
        { "server.DashSlashDamage.desc", "Количество масок урона от Рывкового удара" },
        { "server.CycloneSlashDamage.label", "Урон Циклона" },
        { "server.CycloneSlashDamage.desc", "Количество масок урона от Циклона" },
        { "server.SporeShroomDamage.label", "Урон Грибных спор" },
        { "server.SporeShroomDamage.desc", "Количество масок урона от облака Грибных спор" },
        { "server.SporeDungShroomDamage.label", "Урон Грибных спор с Гребнем" },
        { "server.SporeDungShroomDamage.desc", "Количество масок урона от облака Грибных спор с Гребнем защитника" },
        { "server.ThornOfAgonyDamage.label", "Урон Шипов агонии" },
        { "server.ThornOfAgonyDamage.desc", "Количество масок урона от хлыста Шипов агонии" },
        { "server.SharpShadowDamage.label", "Урон Острого подобия" },
        { "server.SharpShadowDamage.desc", "Количество масок урона от рывка Острого подобия" },

        // Chat, server announcements and disconnect reasons
        { "Server requires the following addons:", "Сервер требует следующие аддоны:" },
        { "Incompatible client addons:", "Несовместимые аддоны клиента:" },
        { "You are banned from the server", "Вы забанены на сервере" },
        { "You are kicked from the server", "Вы исключены с сервера" },
        { "You are disconnected from the server (server is shutting down)", "Вы отключены от сервера (сервер выключается)" },
        { "You are disconnected from the server (server timed out)", "Вы отключены от сервера (сервер не отвечает)" },
        { "Player '{0}' connected to the server", "Игрок '{0}' подключился к серверу" },
        { "Player '{0}' timed out", "Игрок '{0}' не отвечает" },
        { "Player '{0}' disconnected from the server", "Игрок '{0}' отключился от сервера" },
        { "PvP is now enabled", "PvP теперь включено" },
        { "PvP is now disabled", "PvP теперь отключено" },
        { "Body damage is now enabled", "Урон от столкновений теперь включён" },
        { "Body damage is now disabled", "Урон от столкновений теперь отключён" },
        { "Map icons are now always visible", "Иконки на карте теперь всегда видны" },
        { "Map icons are now not always visible", "Иконки на карте больше не видны всегда" },
        { "Map icons are now only broadcast when wearing the Wayward Compass charm", "Иконки на карте теперь видны только с Оберегом блуждающего компаса" },
        { "Map icons are not broadcast when wearing the Wayward Compass charm", "Иконки на карте больше не зависят от Оберега блуждающего компаса" },
        { "Names are now displayed", "Имена теперь отображаются" },
        { "Names are no longer displayed", "Имена больше не отображаются" },
        { "Teams are now enabled", "Команды теперь включены" },
        { "Teams are no longer enabled", "Команды больше не включены" },
        { "Skins are now enabled", "Скины теперь включены" },
        { "Skins are no longer enabled", "Скины больше не включены" },

        // Revive overlay
        { "DOWNED - your soul must be defeated ({0}s)", "ВЫ ПОВЕРЖЕНЫ — победите свою душу ({0} с)" },
        { "Press the give up key to respawn at the last bench", "Нажмите клавишу отказа, чтобы возродиться на последней скамейке" },
        { "Press {0} to give up and respawn at the last bench", "Нажмите {0}, чтобы сдаться и возродиться на последней скамейке" },
        { "Soul of {0}", "Душа {0}" },
        { "Defeat {0}'s soul to revive them", "Победите душу {0}, чтобы возродить напарника" },

        // Reconnection and session statistics
        { "Reconnecting to the server ({0}/{1})", "Переподключение к серверу ({0}/{1})" },
        { "Reconnected to the server", "Подключение к серверу восстановлено" },
        { "Could not reconnect to the server", "Не удалось восстановить подключение к серверу" },
        { "Session statistics", "Статистика сессии" },
        { "Session time: {0}:{1:00}", "Время сессии: {0}:{1:00}" },
        { "Your downs: {0}, revives: {1}, give-ups: {2}", "Ваши падения: {0}, возрождения: {1}, отказы: {2}" },
        { "Team downs: {0}, team revives: {1}", "Падения команды: {0}, возрождения команды: {1}" },

        // Healing teammates
        { "No injured teammate nearby", "Рядом нет раненого напарника" },
        { "Not enough soul to heal a teammate", "Недостаточно души для лечения напарника" },
        { "{0} healed you", "{0} вылечил вас" },

        // Arena progress of teammates
        { "A teammate completed the Trial of the Warrior", "Напарник прошёл Испытание воина" },
        { "A teammate completed the Trial of the Conqueror", "Напарник прошёл Испытание завоевателя" },
        { "A teammate completed the Trial of the Fool", "Напарник прошёл Испытание глупца" },
        { "A teammate completed the Pantheon of the Master", "Напарник прошёл Пантеон Мастера" },
        { "A teammate completed the Pantheon of the Artist", "Напарник прошёл Пантеон Художника" },
        { "A teammate completed the Pantheon of the Sage", "Напарник прошёл Пантеон Мудреца" },
        { "A teammate completed the Pantheon of the Knight", "Напарник прошёл Пантеон Рыцаря" },
        { "A teammate completed the Pantheon of Hallownest", "Напарник прошёл Пантеон Халлоунеста" },

        // Server list
        { "Saved servers", "Сохранённые серверы" },
        { "Selected server: {0}", "Выбран сервер: {0}" },

        // Boss HUD
        { "Boss HUD", "Интерфейс босса" },
        { "Show the health bar of the boss in the current scene and whether a teammate is in a boss fight", "Показывать полосу здоровья босса в текущей локации и статус «в бою» у напарника" },
        { "In fight", "в бою" },
        { "Boss", "Босс" },
        { "Bosses ({0} left)", "Боссы (осталось {0})" },

        // Teleporting to a teammate
        { "Teleport To Teammate", "Телепорт к напарнику" },
        { "Teleport to the closest teammate in the same location for 50 geo or 1 dream essence", "Телепорт к ближайшему напарнику в той же локации за 50 гео или 1 эссенцию" },
        { "Key to teleport to the closest teammate in the same location", "Клавиша телепорта к ближайшему напарнику в той же локации" },
        { "You can teleport again in {0} s", "Телепорт будет доступен через {0} с" },
        { "No teammate in this location", "В этой локации нет напарника" },
        { "Teleporting costs 50 geo or 1 dream essence", "Телепорт стоит 50 гео или 1 эссенцию" },
        { "Teleported to {0} for 50 geo", "Вы телепортированы к {0} за 50 гео" },
        { "Teleported to {0} for 1 dream essence", "Вы телепортированы к {0} за 1 эссенцию" },

        // Sending Geo and dream essence
        { "You are not connected to a server", "Вы не подключены к серверу" },
        { "Amount must be between 1 and {0}", "Количество должно быть от 1 до {0}" },
        { "You cannot send resources while downed", "Нельзя передавать ресурсы, пока вы лежите" },
        { "No teammate nearby", "Рядом нет напарника" },
        { "Teammate {0} is not in this location", "Напарника {0} нет в этой локации" },
        { "Not enough geo", "Недостаточно гео" },
        { "Not enough dream essence", "Недостаточно эссенции" },
        { "Sending {0} geo to {1}", "Отправка {0} гео напарнику {1}" },
        { "Sending {0} dream essence to {1}", "Отправка {0} эссенции напарнику {1}" },
        { "{0} geo sent", "Отправлено {0} гео" },
        { "{0} dream essence sent", "Отправлено {0} эссенции" },
        { "The transfer was rejected, resources returned", "Передача отклонена, ресурсы возвращены" },
        { "{0} sent you {1} geo", "{0} отправил вам {1} гео" },
        { "{0} sent you {1} dream essence", "{0} отправил вам {1} эссенции" },
        { "Usage: /give <amount> [name]", "Использование: /give <количество> [ник]" },
        { "Usage: /giveessence <amount> [name]", "Использование: /giveessence <количество> [ник]" },
        { "The amount must be a whole number", "Количество должно быть целым числом" },

        // Sharing NPC dialogue
        { "Share NPC Dialogue", "Общий прогресс диалогов NPC" },
        { "Share conversation progress with NPCs such as Quirrel, Zote, Bretta and Cloth", "Синхронизировать прогресс разговоров с NPC: Квиррел, Зот, Бретта, Клатч" },
    };

    /// <summary>
    /// List of actions that refresh static UI text when the language changes.
    /// </summary>
    private static readonly List<Action> Refreshers = [];

    /// <summary>
    /// Cached value of whether the game language was Russian the last time it was checked. Null if it was never
    /// checked yet.
    /// </summary>
    private static bool? _lastWasRussian;

    /// <summary>
    /// Whether the current game language is Russian.
    /// </summary>
    public static bool IsRussian =>
        global::Language.Language.CurrentLanguage() == global::Language.LanguageCode.RU;

    /// <summary>
    /// Get the translation for the given English text in the current game language.
    /// </summary>
    /// <param name="english">The English text, which is also used as the lookup key.</param>
    /// <returns>The translated text, or the given English text if there is no translation.</returns>
    public static string Get(string english) {
        if (english == null) {
            return null;
        }

        if (IsRussian && Russian.TryGetValue(english, out var russian)) {
            return russian;
        }

        return english;
    }

    /// <summary>
    /// Get the translation for the given key in the current game language, falling back to the given default text
    /// if the key is not present in the translation table.
    /// </summary>
    /// <param name="key">The lookup key.</param>
    /// <param name="fallback">The text to use if there is no translation for the key.</param>
    /// <returns>The translated text or the fallback text.</returns>
    public static string Get(string key, string fallback) {
        if (IsRussian && Russian.TryGetValue(key, out var russian)) {
            return russian;
        }

        return fallback;
    }

    /// <summary>
    /// Format the translation for the given English text with the given arguments.
    /// </summary>
    /// <param name="english">The English text, which is also used as the lookup key.</param>
    /// <param name="args">The arguments to format the text with.</param>
    /// <returns>The formatted translated text.</returns>
    public static string Format(string english, params object[] args) {
        var text = Get(english);
        if (args == null || args.Length == 0) {
            return text;
        }

        return string.Format(text, args);
    }

    /// <summary>
    /// Register an action that refreshes static UI text. The action is executed whenever the game language changes.
    /// </summary>
    /// <param name="refresher">The action to execute.</param>
    public static void RegisterRefresher(Action refresher) {
        if (refresher == null) {
            return;
        }

        Refreshers.Add(refresher);
    }

    /// <summary>
    /// Refresh all registered static UI text to the current game language.
    /// </summary>
    public static void RefreshTexts() {
        foreach (var refresher in Refreshers) {
            try {
                refresher.Invoke();
            } catch (Exception exception) {
                Logging.Logger.Error($"Failed to refresh localised text: {exception}");
            }
        }
    }

    /// <summary>
    /// Check whether the game language changed since the last check and refresh all localised text and the fonts
    /// used by the mod if it did. Should be called regularly, e.g. from a Unity update loop.
    /// </summary>
    public static void CheckLanguageChanged() {
        var isRussian = IsRussian;

        if (_lastWasRussian == null) {
            _lastWasRussian = isRussian;
            return;
        }

        if (_lastWasRussian == isRussian) {
            return;
        }

        _lastWasRussian = isRussian;

        Logging.Logger.Info($"Game language changed, switching mod language to {(isRussian ? "Russian" : "English")}");

        // Reload the fonts, since a different font is needed for Cyrillic characters, and apply them to all UI
        // elements that were already created
        FontManager.LoadFonts();
        FontManager.ApplyFontsToExistingUi();

        // And update all static texts to the new language
        RefreshTexts();
    }
}
