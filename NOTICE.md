# NOTICE — Hollow Knight Co-op 3.0.0-coop.12

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
* новые файлы `HKMP/Game/Client/SoulManager.cs`,
  `HKMP/Game/Client/TeammateHud.cs` и
  `HKMP/Game/Client/SessionStats.cs`, правки `Entity.MakeHost()` и
  `HKMP/Resource/action-registry.json` (исправление замирания ходячих врагов
  после передачи роли хоста сцены), `save-data.json` (экономика и арены);
* новый тип сетевого обновления `PlayerUpdateType.Health` и его обработка в
  `ClientUpdateManager`/`ServerUpdateManager`;
* новые настройки и их значения по умолчанию (`HKMP/Game/Settings/ModSettings.cs`,
  меню `HKMP/Menu/ModMenu.cs`), в том числе `MapIconsRequireCompass`,
  `TeammateHud`, `SpectatorCamera`, `ReviveHealth`, `ReviverHealAmount`,
  `AutoReconnect`, `SessionStats` и `ArenaSync`;
* русский интерфейс мода;
* строка версии `3.0.0-coop.12`.

## Соответствие LGPL v2.1

* Текст лицензии приложен (`LICENSE`, `LICENSE.txt` в архиве мода).
* Изменённые исходники доступны в архиве
  `HollowKnightCoop-3.0.0-coop.12-source.zip` в Releases этого репозитория.
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
