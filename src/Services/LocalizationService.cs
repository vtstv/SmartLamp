// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Collections.Generic;

namespace SmartLampApp.Services
{
    public static partial class LocalizationService
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

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = InitStrings();

        private static Dictionary<string, Dictionary<string, string>> InitStrings()
        {
            var dict = new Dictionary<string, Dictionary<string, string>>();
            RegisterCoreStrings(dict);
            RegisterGroupsStrings(dict);
            RegisterSettingsStrings(dict);
            RegisterDevicesStrings(dict);
            return dict;
        }

        static partial void RegisterGroupsStrings(Dictionary<string, Dictionary<string, string>> dict);
        static partial void RegisterSettingsStrings(Dictionary<string, Dictionary<string, string>> dict);
        static partial void RegisterDevicesStrings(Dictionary<string, Dictionary<string, string>> dict);

        public static string T(string key, params object[] args)
        {
            string template = key;
            if (Strings.TryGetValue(key, out var langDict))
            {
                if (!langDict.TryGetValue(_currentLang, out template!) && !langDict.TryGetValue("en", out template!))
                {
                    template = key;
                }
            }
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(template, args);
                }
                catch
                {
                    return template;
                }
            }
            return template;
        }

        private static void RegisterCoreStrings(Dictionary<string, Dictionary<string, string>> dict)
        {
            dict["AppTitle"] = new()
            {
                ["en"] = $"SmartLamp Studio v{AppVersion.Version}",
                ["de"] = $"SmartLamp Studio v{AppVersion.Version}",
                ["ru"] = $"SmartLamp Studio v{AppVersion.Version}"
            };
            dict["ActiveDevice"] = new()
            {
                ["en"] = "Active Device:",
                ["de"] = "Aktives Gerät:",
                ["ru"] = "Активное устройство:"
            };
            dict["ActiveRoomGroup"] = new()
            {
                ["en"] = "Active Room / Group:",
                ["de"] = "Aktiver Raum / Gruppe:",
                ["ru"] = "Активная комната / группа:"
            };
            dict["PoweredOn"] = new()
            {
                ["en"] = "⚡ POWERED ON",
                ["de"] = "⚡ EINGESCHALTET",
                ["ru"] = "⚡ ВКЛЮЧЕНО"
            };
            dict["PoweredOff"] = new()
            {
                ["en"] = "⚡ POWERED OFF",
                ["de"] = "⚡ AUSGESCHALTET",
                ["ru"] = "⚡ ВЫКЛЮЧЕНО"
            };
            dict["GroupActive"] = new()
            {
                ["en"] = "👥 GROUP ACTIVE",
                ["de"] = "👥 GRUPPE AKTIV",
                ["ru"] = "👥 ГРУППА АКТИВНА"
            };
            dict["StatusConnecting"] = new()
            {
                ["en"] = "⏳ CONNECTING...",
                ["de"] = "⏳ VERBINDEN...",
                ["ru"] = "⏳ ПОДКЛЮЧЕНИЕ..."
            };
            dict["StatusOnline"] = new()
            {
                ["en"] = "🟢 ONLINE",
                ["de"] = "🟢 ONLINE",
                ["ru"] = "🟢 В СЕТИ"
            };
            dict["StatusOffline"] = new()
            {
                ["en"] = "🔴 OFFLINE",
                ["de"] = "🔴 OFFLINE",
                ["ru"] = "🔴 НЕ В СЕТИ"
            };
            dict["NavDash"] = new()
            {
                ["en"] = "💡 Control Dashboard",
                ["de"] = "💡 Steuerungs-Dashboard",
                ["ru"] = "💡 Панель управления"
            };
            dict["NavGroups"] = new()
            {
                ["en"] = "👥 Groups & Rooms",
                ["de"] = "👥 Gruppen & Räume",
                ["ru"] = "👥 Группы и комнаты"
            };
            dict["NavSettings"] = new()
            {
                ["en"] = "⚙️ Settings & Hotkeys",
                ["de"] = "⚙️ Einstellungen & Hotkeys",
                ["ru"] = "⚙️ Настройки и хоткеи"
            };
            dict["NavGoogleHome"] = new()
            {
                ["en"] = "🌐 Google Home",
                ["de"] = "🌐 Google Home",
                ["ru"] = "🌐 Google Home"
            };
            dict["NavMqtt"] = new()
            {
                ["en"] = "🤖 Home Assistant & MQTT",
                ["de"] = "🤖 Home Assistant & MQTT",
                ["ru"] = "🤖 Home Assistant & MQTT"
            };
            dict["SleepTimer"] = new()
            {
                ["en"] = "⏳ Sleep Timer (Auto Off)",
                ["de"] = "⏳ Sleep-Timer (Auto-Aus)",
                ["ru"] = "⏳ Таймер сна (Автовыключение)"
            };
            dict["TimerOff"] = new()
            {
                ["en"] = "✕ Off",
                ["de"] = "✕ Aus",
                ["ru"] = "✕ Выкл"
            };
            dict["SetCustomMins"] = new()
            {
                ["en"] = "⚡ Set Custom Mins",
                ["de"] = "⚡ Min. festlegen",
                ["ru"] = "⚡ Задать минуты"
            };
            dict["BrightnessLevel"] = new()
            {
                ["en"] = "☀️ Brightness Level",
                ["de"] = "☀️ Helligkeitsstufe",
                ["ru"] = "☀️ Уровень яркости"
            };
            dict["ColorTemperature"] = new()
            {
                ["en"] = "🌡️ Color Temperature",
                ["de"] = "🌡️ Farbtemperatur",
                ["ru"] = "🌡️ Цветовая температура"
            };
            dict["RgbPalette"] = new()
            {
                ["en"] = "🎨 RGB Color Palette",
                ["de"] = "🎨 RGB-Farbpalette",
                ["ru"] = "🎨 Палитра цветов RGB"
            };
            dict["ColorStudio"] = new()
            {
                ["en"] = "🎨 Color Studio",
                ["de"] = "🎨 Farbstudio",
                ["ru"] = "🎨 Студия цвета"
            };
            dict["Apply"] = new()
            {
                ["en"] = "Apply",
                ["de"] = "Anwenden",
                ["ru"] = "Применить"
            };
            dict["CustomPresets"] = new()
            {
                ["en"] = "⭐ Custom User Presets",
                ["de"] = "⭐ Benutzerdefinierte Presets",
                ["ru"] = "⭐ Пользовательские пресеты"
            };
            dict["SaveCurrent"] = new()
            {
                ["en"] = "➕ Save",
                ["de"] = "➕ Speichern",
                ["ru"] = "➕ Сохранить"
            };
            dict["TooltipSavePreset"] = new()
            {
                ["en"] = "Save current brightness, temp & color as a custom preset",
                ["de"] = "Aktuelle Helligkeit, Farbtemperatur & Farbe als Preset speichern",
                ["ru"] = "Сохранить текущую яркость, температуру и цвет как пресет"
            };
            dict["LightingScenes"] = new()
            {
                ["en"] = "✨ Lighting Scenes",
                ["de"] = "✨ Lichtszenen",
                ["ru"] = "✨ Световые сцены"
            };
            dict["SceneNight"] = new()
            {
                ["en"] = "🌙 Night",
                ["de"] = "🌙 Nacht",
                ["ru"] = "🌙 Ночь"
            };
            dict["SceneRead"] = new()
            {
                ["en"] = "📖 Read",
                ["de"] = "📖 Lesen",
                ["ru"] = "📖 Чтение"
            };
            dict["SceneFocus"] = new()
            {
                ["en"] = "💻 Focus",
                ["de"] = "💻 Fokus",
                ["ru"] = "💻 Фокус"
            };
            dict["SceneRelax"] = new()
            {
                ["en"] = "🛋️ Relax",
                ["de"] = "🛋️ Entspannung",
                ["ru"] = "🛋️ Отдых"
            };
            dict["SceneParty"] = new()
            {
                ["en"] = "🎉 Party",
                ["de"] = "🎉 Party",
                ["ru"] = "🎉 Вечеринка"
            };
            dict["SceneCycle"] = new()
            {
                ["en"] = "🌈 Cycle",
                ["de"] = "🌈 Zyklus",
                ["ru"] = "🌈 Радуга"
            };
            dict["Warm"] = new()
            {
                ["en"] = "Warm 2700K",
                ["de"] = "Warm 2700K",
                ["ru"] = "Теплый 2700K"
            };
            dict["Neutral"] = new()
            {
                ["en"] = "Neutral 4000K",
                ["de"] = "Neutral 4000K",
                ["ru"] = "Нейтральный 4000K"
            };
            dict["Cool"] = new()
            {
                ["en"] = "Cool 6500K",
                ["de"] = "Kalt 6500K",
                ["ru"] = "Холодный 6500K"
            };
            dict["Refresh"] = new()
            {
                ["en"] = "🔄 Refresh",
                ["de"] = "🔄 Aktualisieren",
                ["ru"] = "🔄 Обновить"
            };
            dict["CompactMode"] = new()
            {
                ["en"] = "🗗 Compact Mode",
                ["de"] = "🗗 Kompakter Modus",
                ["ru"] = "🗗 Компактный режим"
            };
            dict["FullMode"] = new()
            {
                ["en"] = "🗖 Full Mode",
                ["de"] = "🗖 Vollmodus",
                ["ru"] = "🗖 Полный режим"
            };
            dict["FooterHotkey"] = new()
            {
                ["en"] = "⌨️ Hotkey: {0}",
                ["de"] = "⌨️ Hotkey: {0}",
                ["ru"] = "⌨️ Горячая клавиша: {0}"
            };
        }
    }
}
