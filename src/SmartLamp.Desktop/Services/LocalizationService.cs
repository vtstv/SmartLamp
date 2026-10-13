// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;

namespace SmartLamp.Desktop.Services
{
    public static class LocalizationService
    {
        public static event Action? LanguageChanged;

        private static string _currentLang = "en";

        public static string CurrentLanguage
        {
            get => _currentLang;
            set
            {
                string norm = value?.ToLowerInvariant() switch
                {
                    "de" => "de",
                    "ru" => "ru",
                    _ => "en"
                };
                if (_currentLang != norm)
                {
                    _currentLang = norm;
                    LanguageChanged?.Invoke();
                }
            }
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["AppTitle"] = new()
            {
                ["en"] = $"SmartLamp Studio v{SmartLampApp.AppVersion.Version}",
                ["de"] = $"SmartLamp Studio v{SmartLampApp.AppVersion.Version}",
                ["ru"] = $"SmartLamp Studio v{SmartLampApp.AppVersion.Version}"
            },
            ["ActiveDevice"] = new()
            {
                ["en"] = "Active Device:",
                ["de"] = "Aktives Gerät:",
                ["ru"] = "Активное устройство:"
            },
            ["PoweredOn"] = new()
            {
                ["en"] = "⚡ POWERED ON",
                ["de"] = "⚡ EINGESCHALTET",
                ["ru"] = "⚡ ВКЛЮЧЕНО"
            },
            ["PoweredOff"] = new()
            {
                ["en"] = "⚡ POWERED OFF",
                ["de"] = "⚡ AUSGESCHALTET",
                ["ru"] = "⚡ ВЫКЛЮЧЕНО"
            },
            ["NavDash"] = new()
            {
                ["en"] = "💡 Control Dashboard",
                ["de"] = "💡 Steuerungs-Dashboard",
                ["ru"] = "💡 Панель управления"
            },
            ["NavSettings"] = new()
            {
                ["en"] = "⚙️ Settings & Hotkeys",
                ["de"] = "⚙️ Einstellungen & Hotkeys",
                ["ru"] = "⚙️ Настройки и хоткеи"
            },
            ["SleepTimer"] = new()
            {
                ["en"] = "⏳ Sleep Timer (Auto Off)",
                ["de"] = "⏳ Sleep-Timer (Auto-Aus)",
                ["ru"] = "⏳ Таймер сна (Автовыключение)"
            },
            ["SetCustomMins"] = new()
            {
                ["en"] = "⚡ Set Custom Mins",
                ["de"] = "⚡ Min. festlegen",
                ["ru"] = "⚡ Задать минуты"
            },
            ["BrightnessLevel"] = new()
            {
                ["en"] = "☀️ Brightness Level",
                ["de"] = "☀️ Helligkeitsstufe",
                ["ru"] = "☀️ Уровень яркости"
            },
            ["ColorTemperature"] = new()
            {
                ["en"] = "🌡️ Color Temperature",
                ["de"] = "🌡️ Farbtemperatur",
                ["ru"] = "🌡️ Цветовая температура"
            },
            ["RgbPalette"] = new()
            {
                ["en"] = "🎨 RGB Color Palette",
                ["de"] = "🎨 RGB-Farbpalette",
                ["ru"] = "🎨 RGB Палитра цветов"
            },
            ["ColorStudio"] = new()
            {
                ["en"] = "🎨 Random",
                ["de"] = "🎨 Zufall",
                ["ru"] = "🎨 Случайно"
            },
            ["Apply"] = new()
            {
                ["en"] = "Apply",
                ["de"] = "Anwenden",
                ["ru"] = "Применить"
            },
            ["CustomPresets"] = new()
            {
                ["en"] = "⭐ Custom User Presets",
                ["de"] = "⭐ Benutzerdefinierte Vorlagen",
                ["ru"] = "⭐ Пользовательские пресеты"
            },
            ["SaveCurrent"] = new()
            {
                ["en"] = "➕ Save Current",
                ["de"] = "➕ Aktuelle speichern",
                ["ru"] = "➕ Сохранить"
            },
            ["LightingScenes"] = new()
            {
                ["en"] = "✨ Lighting Scenes",
                ["de"] = "✨ Lichtszenen",
                ["ru"] = "✨ Световые сцены"
            },
            ["SceneNight"] = new()
            {
                ["en"] = "🌙 Night",
                ["de"] = "🌙 Nacht",
                ["ru"] = "🌙 Ночь"
            },
            ["SceneRead"] = new()
            {
                ["en"] = "📖 Read",
                ["de"] = "📖 Lesen",
                ["ru"] = "📖 Чтение"
            },
            ["SceneFocus"] = new()
            {
                ["en"] = "💻 Focus",
                ["de"] = "💻 Fokus",
                ["ru"] = "💻 Работа"
            },
            ["SceneRelax"] = new()
            {
                ["en"] = "🛋️ Relax",
                ["de"] = "🛋️ Entspannen",
                ["ru"] = "🛋️ Отдых"
            },
            ["SceneParty"] = new()
            {
                ["en"] = "🎉 Party",
                ["de"] = "🎉 Party",
                ["ru"] = "🎉 Вечеринка"
            },
            ["SceneCycle"] = new()
            {
                ["en"] = "🌈 Cycle",
                ["de"] = "🌈 Zyklus",
                ["ru"] = "🌈 Радуга"
            },
            ["Warm"] = new()
            {
                ["en"] = "Warm 2700K",
                ["de"] = "Warm 2700K",
                ["ru"] = "Тёплый 2700K"
            },
            ["Neutral"] = new()
            {
                ["en"] = "Neutral 4000K",
                ["de"] = "Neutral 4000K",
                ["ru"] = "Нейтральный 4000K"
            },
            ["Cool"] = new()
            {
                ["en"] = "Cool 6500K",
                ["de"] = "Kühl 6500K",
                ["ru"] = "Холодный 6500K"
            },
            ["Refresh"] = new()
            {
                ["en"] = "🔄 Refresh",
                ["de"] = "🔄 Aktualisieren",
                ["ru"] = "🔄 Обновить"
            },
            ["Online"] = new()
            {
                ["en"] = "🟢 ONLINE",
                ["de"] = "🟢 ONLINE",
                ["ru"] = "🟢 В СЕТИ"
            },
            ["Offline"] = new()
            {
                ["en"] = "❌ OFFLINE",
                ["de"] = "❌ OFFLINE",
                ["ru"] = "❌ НЕ В СЕТИ"
            },
            ["Connecting"] = new()
            {
                ["en"] = "⏳ CONNECTING...",
                ["de"] = "⏳ VERBINDUNG...",
                ["ru"] = "⏳ ПОДКЛЮЧЕНИЕ..."
            },
            ["DeviceCredentials"] = new()
            {
                ["en"] = "📌 Device Credentials & Group",
                ["de"] = "📌 Geräteanmeldedaten & Gruppe",
                ["ru"] = "📌 Параметры устройства и группа"
            },
            ["DeviceName"] = new()
            {
                ["en"] = "Device Name:",
                ["de"] = "Gerätename:",
                ["ru"] = "Имя устройства:"
            },
            ["IpAddress"] = new()
            {
                ["en"] = "IP Address:",
                ["de"] = "IP-Adresse:",
                ["ru"] = "IP-адрес:"
            },
            ["DeviceId"] = new()
            {
                ["en"] = "Device ID:",
                ["de"] = "Geräte-ID:",
                ["ru"] = "ID устройства:"
            },
            ["LocalKey"] = new()
            {
                ["en"] = "Local Key (16 Chars):",
                ["de"] = "Lokaler Schlüssel (16 Zeichen):",
                ["ru"] = "Локальный ключ (16 знаков):"
            },
            ["GroupName"] = new()
            {
                ["en"] = "Group Name (e.g. Living Room):",
                ["de"] = "Gruppenname (z.B. Wohnzimmer):",
                ["ru"] = "Имя группы (напр. Гостиная):"
            },
            ["SaveDevice"] = new()
            {
                ["en"] = "💾 Save Device",
                ["de"] = "💾 Gerät speichern",
                ["ru"] = "💾 Сохранить устройство"
            },
            ["Delete"] = new()
            {
                ["en"] = "🗑️ Delete",
                ["de"] = "🗑️ Löschen",
                ["ru"] = "🗑️ Удалить"
            },
            ["WifiDiscovery"] = new()
            {
                ["en"] = "🔍 Wi-Fi Auto-Discovery",
                ["de"] = "🔍 Wi-Fi Auto-Erkennung",
                ["ru"] = "🔍 Автопоиск в Wi-Fi сети"
            },
            ["ScanWifi"] = new()
            {
                ["en"] = "⚡ Scan Local Wi-Fi Subnet",
                ["de"] = "⚡ Lokales Wi-Fi scannen",
                ["ru"] = "⚡ Сканировать сеть"
            },
            ["CompactMode"] = new()
            {
                ["en"] = "🗗 Compact Mode",
                ["de"] = "🗗 Kompakter Modus",
                ["ru"] = "🗗 Компактный режим"
            },
            ["FullMode"] = new()
            {
                ["en"] = "🗖 Full Mode",
                ["de"] = "🗖 Vollmodus",
                ["ru"] = "🗖 Полный режим"
            },
            ["SavePresetTitle"] = new()
            {
                ["en"] = "⭐ Save Custom Preset",
                ["de"] = "⭐ Vorlage speichern",
                ["ru"] = "⭐ Сохранение пресета"
            },
            ["PresetName"] = new()
            {
                ["en"] = "Preset Name:",
                ["de"] = "Vorlagenname:",
                ["ru"] = "Имя пресета:"
            },
            ["Cancel"] = new()
            {
                ["en"] = "Cancel",
                ["de"] = "Abbrechen",
                ["ru"] = "Отмена"
            },
            ["SavePreset"] = new()
            {
                ["en"] = "💾 Save Preset",
                ["de"] = "💾 Vorlage speichern",
                ["ru"] = "💾 Сохранить пресет"
            },
            ["Off"] = new()
            {
                ["en"] = "Off",
                ["de"] = "Aus",
                ["ru"] = "Выкл"
            },
            ["NoPresets"] = new()
            {
                ["en"] = "No custom presets saved yet. Click '+ Save Current' to add one!",
                ["de"] = "Noch keine Vorlagen gespeichert. Klicken Sie auf '+ Aktuelle speichern'!",
                ["ru"] = "Пресеты ещё не созданы. Нажмите '+ Сохранить' для добавления!"
            },
            ["LanguageSettings"] = new()
            {
                ["en"] = "🌐 Interface Language",
                ["de"] = "🌐 Sprache der Benutzeroberfläche",
                ["ru"] = "🌐 Язык интерфейса"
            },
            ["AppBehavior"] = new()
            {
                ["en"] = "⚙️ App Behavior & Integrations",
                ["de"] = "⚙️ Anwendungsverhalten & Integrationen",
                ["ru"] = "⚙️ Поведение приложения и интеграции"
            },
            ["CloseToTray"] = new()
            {
                ["en"] = "Minimize to System Tray on window close (✕)",
                ["de"] = "Beim Schließen ins Tray minimieren (✕)",
                ["ru"] = "Сворачивать в системный трей при закрытии (✕)"
            },
            ["TimerNotifications"] = new()
            {
                ["en"] = "Show Desktop notifications for Sleep Timer",
                ["de"] = "Desktop-Benachrichtigungen für Timer anzeigen",
                ["ru"] = "Показывать уведомления для таймера сна"
            },
            ["EnableGoogle"] = new()
            {
                ["en"] = "Enable Google Home Bridge Integration",
                ["de"] = "Google Home Bridge Integration aktivieren",
                ["ru"] = "Включить интеграцию с Google Home"
            },
            ["EnableMqtt"] = new()
            {
                ["en"] = "Enable Home Assistant & MQTT Bridge",
                ["de"] = "Home Assistant & MQTT Bridge aktivieren",
                ["ru"] = "Включить Home Assistant & MQTT мост"
            },
            ["CheckUpdates"] = new()
            {
                ["en"] = "Automatically check for updates on startup",
                ["de"] = "Beim Start automatisch nach Updates suchen",
                ["ru"] = "Проверять обновления при запуске"
            },
            ["SystemUpdates"] = new()
            {
                ["en"] = "⚡ Shortcuts & Updates",
                ["de"] = "⚡ Verknüpfungen & Updates",
                ["ru"] = "⚡ Ярлыки и обновления"
            },
            ["CheckUpdatesBtn"] = new()
            {
                ["en"] = "🔍 Check for Updates",
                ["de"] = "🔍 Nach Updates suchen",
                ["ru"] = "🔍 Проверить обновления"
            },
            ["CreateShortcut"] = new()
            {
                ["en"] = "⚡ Desktop Shortcut",
                ["de"] = "⚡ Desktop-Verknüpfung",
                ["ru"] = "⚡ Ярлык на рабочем столе"
            },
            ["CloudSync"] = new()
            {
                ["en"] = "☁️ Auto-Fetch Key from Tuya Cloud",
                ["de"] = "☁️ Schlüssel aus Tuya Cloud abrufen",
                ["ru"] = "☁️ Получение ключа из Tuya Cloud"
            },
            ["AccessId"] = new()
            {
                ["en"] = "Access ID:",
                ["de"] = "Access-ID:",
                ["ru"] = "Access ID:"
            },
            ["AccessKey"] = new()
            {
                ["en"] = "Access Key (Secret):",
                ["de"] = "Access-Schlüssel (Geheimnis):",
                ["ru"] = "Access Key (Секрет):"
            },
            ["FetchKeyCloud"] = new()
            {
                ["en"] = "⚡ Fetch Key from Cloud",
                ["de"] = "⚡ Schlüssel aus Cloud abrufen",
                ["ru"] = "⚡ Получить ключ из Cloud"
            },
            ["Instructions"] = new()
            {
                ["en"] = "❓ Instructions",
                ["de"] = "❓ Anleitung",
                ["ru"] = "❓ Инструкция"
            },
            ["ControlModeTitle"] = new()
            {
                ["en"] = "🎮 Control Mode & Fallback Priority",
                ["de"] = "🎮 Steuerungsmodus & Priorität",
                ["ru"] = "🎮 Режим управления и приоритет"
            },
            ["ControlMode"] = new()
            {
                ["en"] = "Control Mode:",
                ["de"] = "Steuerungsmodus:",
                ["ru"] = "Режим управления:"
            },
            ["AutoPriority"] = new()
            {
                ["en"] = "Auto Priority Order:",
                ["de"] = "Prioritätsreihenfolge:",
                ["ru"] = "Порядок приоритета:"
            },
            ["NavGoogleHome"] = new()
            {
                ["en"] = "🌐 Google Home",
                ["de"] = "🌐 Google Home",
                ["ru"] = "🌐 Google Home"
            },
            ["NavMqtt"] = new()
            {
                ["en"] = "🤖 MQTT & HA",
                ["de"] = "🤖 MQTT & HA",
                ["ru"] = "🤖 MQTT & HA"
            },
            ["LangEn"] = new()
            {
                ["en"] = "English",
                ["de"] = "English",
                ["ru"] = "English"
            },
            ["LangDe"] = new()
            {
                ["en"] = "Deutsch",
                ["de"] = "Deutsch",
                ["ru"] = "Deutsch"
            },
            ["LangRu"] = new()
            {
                ["en"] = "Русский",
                ["de"] = "Русский",
                ["ru"] = "Русский"
            }
        };

        public static string T(string key)
        {
            if (Strings.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(_currentLang, out var val))
                    return val;
                if (langDict.TryGetValue("en", out var defVal))
                    return defVal;
            }
            return key;
        }
    }
}
