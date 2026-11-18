# NOTICE — Hollow Knight Co-op 3.0.0-coop.14

## Что это

Hollow Knight Co-op — **изменённая версия** мода
[HKMP — Hollow Knight Multiplayer](https://github.com/Extremelyd1/HKMP).
Оригинальный HKMP написан Extremelyd1 и распространяется по лицензии
**GNU Lesser General Public License v2.1** (текст — в файле `LICENSE`).

База этой сборки: HKMP, ветка `master`, коммит `f7c35d96`.

## Изменения относительно оригинала

Перечислены в файле `COOP-CHANGES-RU.txt` в архиве исходников и в разделе
«ЧТО ИЗМЕНИЛОСЬ» файла `README-RU.txt` в архиве мода. Кратко:

* новые игровые механики: возрождение товарища через победу над его душой,
  сдача и настоящая смерть, полоски здоровья врагов, множитель здоровья
  врагов, выбор ближайшей цели у врагов и боссов, удержание ворот арены,
  разнесённые места на скамейке, значки напарников на карте, интерфейс
  напарников (список, маски и растворяющаяся метка), синхронизация здоровья
  напарников, камера наблюдателя, красная метка лежащего напарника на карте,
  новые правила возрождения (3 маски и полная душа, +2 маски возродившему),
  защита от дружественного огня при выключенном PvP, общий прогресс мира
  (станции, платные скамейки, личинки, способности), общий прогресс арен
  (Колизей и пантеоны) с обновлением дверей боссов, автопереподключение
  к серверу и статистика сессии;
* механики coop.13: лечение напарника за души, здоровье боссов и врагов по
  числу игроков в сцене, уведомления о прогрессе арен, общий прогресс карты,
  журнала охотника, эссенции и наград (включается по отдельности),
  сглаживание аватаров, пинг каждого игрока в списке напарников, цвета,
  масштаб и компактный режим интерфейса напарников, выбор цвета своего ника
  и своей строки в списке (цвет передаётся остальным игрокам), раздельные
  строки для пинга и состояния напарника (текст не сжимается и не обрезается),
  поиск сервера в локальной сети (UDP 47777), пароль, MOTD и лимит игроков
  на сервере, профили настроек;
* механики coop.14: телепорт к ближайшему напарнику в текущей локации за Гео
  или эссенцию сна с кулдауном и поиском безопасной точки приземления,
  передача Гео и эссенции между игроками командами `/give` и `/giveessence`
  с серверной проверкой и возвратом ресурсов при отказе, общий прогресс
  диалогов NPC (Квиррел, Зот, Бретта, Клатч), список сохранённых серверов
  с избранным и историей в окне подключения, интерфейс босса с полосой
  здоровья и статусом «в бою» у напарника;
* новые файлы `HKMP/Game/Client/SoulManager.cs`,
  `HKMP/Game/Client/TeammateHud.cs`,
  `HKMP/Game/Client/SessionStats.cs`,
  `HKMP/Game/Client/HealManager.cs`,
  `HKMP/Game/Client/TransferManager.cs`,
  `HKMP/Game/Client/TeleportManager.cs`,
  `HKMP/Game/Command/Client/GiveCommand.cs`,
  `HKMP/Networking/Packet/Data/PlayerTransfer.cs`,
  `HKMP/Ui/ServerListStore.cs`,
  `HKMP/Game/Settings/SettingsProfiles.cs`,
  `HKMP/Networking/LanDiscovery.cs` и серверные команды
  `PasswordCommand.cs`, `MotdCommand.cs`, `MaxPlayersCommand.cs`,
  правки `Entity.MakeHost()` и `HKMP/Resource/action-registry.json`
  (исправление замирания ходячих врагов после передачи роли хоста сцены),
  `save-data.json` (экономика и арены);
* новые типы сетевых обновлений `PlayerUpdateType.Health`,
  `PlayerUpdateType.Ping`, `PlayerUpdateType.Color` и `PlayerUpdateType.BossFight`
  и их обработка в `ClientUpdateManager`/`ServerUpdateManager`, а также
  клиентские пакеты `PlayerGive`/`PlayerTransferResult` и серверный
  `PlayerGiveRequest`;
* новые настройки и их значения по умолчанию (`HKMP/Game/Settings/ModSettings.cs`,
  меню `HKMP/Menu/ModMenu.cs`), в том числе `MapIconsRequireCompass`,
  `TeammateHud`, `SpectatorCamera`, `ReviveHealth`, `ReviverHealAmount`,
  `AutoReconnect`, `SessionStats`, `ArenaSync`, `SettingsProfile`,
  `TeammateColors`, `PlayerColorIndex`, `HudScale`, `HudCompact`, `HudShowPing`,
  `AvatarSmoothing`, `HealTeammate`, `BossHealthScaling`,
  `BossHealthPerPlayer`, `ShareMap`, `ShareJournal`, `ShareEssence`,
  `ShareRewards`, `BossHud`, `TeleportToTeammate`, `ShareNpcDialogue`;
* русский интерфейс мода;
* строка версии `3.0.0-coop.14`.

## Соответствие LGPL v2.1

* Текст лицензии приложен (`LICENSE`, `LICENSE.txt` в архиве мода).
* Изменённые исходники доступны в архиве
  `HollowKnightCoop-3.0.0-coop.14-source.zip` в Releases этого репозитория.
* Сборка не выдаёт себя за оригинал: номер версии изменён, изменения
  перечислены в этом файле, в `README.md` и в README архива.
* В распространяемых архивах нет файлов игры и файлов Modding API — только
  сам мод и его зависимости (`BouncyCastle.Cryptography.dll`,
  `Newtonsoft.Json.dll` для сервера).

## Прочее

* Hollow Knight Modding API: <https://github.com/hk-modding/api> — отдельный
  проект, распространяется по своей лицензии, в архивы не входит.
* Hollow Knight и все её ресурсы — © Team Cherry.
* Эта сборка не связана с Extremelyd1 и не одобрена им. Ошибки этой версии
  не следует сообщать в оригинальный проект HKMP.
