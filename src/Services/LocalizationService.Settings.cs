// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;

namespace SmartLampApp.Services
{
    public static partial class LocalizationService
    {
        static partial void RegisterSettingsStrings(Dictionary<string, Dictionary<string, string>> dict)
        {
            // --- Settings Tab ---
            dict["LanguageSettings"] = new()
            {
                ["en"] = "🌐 Interface Language",
                ["de"] = "🌐 Sprache der Benutzeroberfläche",
                ["ru"] = "🌐 Язык интерфейса"
            };
            dict["DeviceCredentials"] = new()
            {
                ["en"] = "📌 Device Credentials & Group",
                ["de"] = "📌 Geräteanmeldedaten & Gruppe",
                ["ru"] = "📌 Параметры устройства и группа"
            };
            dict["AddNewLamp"] = new()
            {
                ["en"] = "➕ New Lamp",
                ["de"] = "➕ Neue Lampe",
                ["ru"] = "➕ Новая лампа"
            };
            dict["TooltipNewLamp"] = new()
            {
                ["en"] = "Add a new smart lamp or device",
                ["de"] = "Neue smarte Lampe oder Gerät hinzufügen",
                ["ru"] = "Добавить новую смарт-лампу или устройство"
            };
            dict["DeviceName"] = new()
            {
                ["en"] = "Device Name:",
                ["de"] = "Gerätename:",
                ["ru"] = "Имя устройства:"
            };
            dict["IpAddress"] = new()
            {
                ["en"] = "IP Address:",
                ["de"] = "IP-Adresse:",
                ["ru"] = "IP-адрес:"
            };
            dict["DeviceId"] = new()
            {
                ["en"] = "Device ID:",
                ["de"] = "Geräte-ID:",
                ["ru"] = "ID устройства:"
            };
            dict["LocalKey"] = new()
            {
                ["en"] = "Local Key (16 Chars):",
                ["de"] = "Lokaler Schlüssel (16 Zeichen):",
                ["ru"] = "Локальный ключ (16 знаков):"
            };
            dict["GroupName"] = new()
            {
                ["en"] = "Group Name (e.g. Living Room):",
                ["de"] = "Gruppenname (z. B. Wohnzimmer):",
                ["ru"] = "Имя группы (напр. Гостиная):"
            };
            dict["SaveDevice"] = new()
            {
                ["en"] = "💾 Save Device",
                ["de"] = "💾 Gerät speichern",
                ["ru"] = "💾 Сохранить устройство"
            };
            dict["Delete"] = new()
            {
                ["en"] = "🗑️ Delete",
                ["de"] = "🗑️ Löschen",
                ["ru"] = "🗑️ Удалить"
            };
            dict["AppBehavior"] = new()
            {
                ["en"] = "⚙️ App Behavior & Integrations",
                ["de"] = "⚙️ Anwendungsverhalten & Integrationen",
                ["ru"] = "⚙️ Поведение приложения и интеграции"
            };
            dict["Autostart"] = new()
            {
                ["en"] = "Launch automatically on Windows Startup",
                ["de"] = "Automatisch mit Windows starten",
                ["ru"] = "Автозапуск при старте Windows"
            };
            dict["CloseToTray"] = new()
            {
                ["en"] = "Minimize to System Tray on window close (✕)",
                ["de"] = "Beim Schließen ins System-Tray minimieren (✕)",
                ["ru"] = "Сворачивать в трей при закрытии (✕)"
            };
            dict["TimerNotifications"] = new()
            {
                ["en"] = "Show Windows notifications for Sleep Timer",
                ["de"] = "Windows-Benachrichtigungen für Sleep-Timer anzeigen",
                ["ru"] = "Уведомления Windows для таймера сна"
            };
            dict["EnableGoogle"] = new()
            {
                ["en"] = "Enable Google Home Bridge Integration",
                ["de"] = "Google Home Bridge-Integration aktivieren",
                ["ru"] = "Включить интеграцию с Google Home"
            };
            dict["EnableMqtt"] = new()
            {
                ["en"] = "Enable Home Assistant & MQTT Bridge",
                ["de"] = "Home Assistant & MQTT Bridge aktivieren",
                ["ru"] = "Включить Home Assistant & MQTT мост"
            };
            dict["CheckUpdatesStartup"] = new()
            {
                ["en"] = "Automatically check for updates on startup",
                ["de"] = "Beim Start automatisch nach Updates suchen",
                ["ru"] = "Проверять обновления при запуске"
            };
            dict["SystemUpdates"] = new()
            {
                ["en"] = "⚡ Shortcuts, System & Updates",
                ["de"] = "⚡ Verknüpfungen, System & Updates",
                ["ru"] = "⚡ Ярлыки, система и обновления"
            };
            dict["CheckUpdatesBtn"] = new()
            {
                ["en"] = "🔍 Check Updates",
                ["de"] = "🔍 Updates prüfen",
                ["ru"] = "🔍 Обновления"
            };
            dict["TooltipCheckUpdates"] = new()
            {
                ["en"] = "Check GitHub for the latest application updates",
                ["de"] = "Auf GitHub nach den neuesten Anwendungsupdates suchen",
                ["ru"] = "Проверить наличие обновлений на GitHub"
            };
            dict["InstallApp"] = new()
            {
                ["en"] = "📦 Install App to System",
                ["de"] = "📦 App im System installieren",
                ["ru"] = "📦 Установить программу в систему"
            };
            dict["UninstallApp"] = new()
            {
                ["en"] = "🗑️ Uninstall App",
                ["de"] = "🗑️ App deinstallieren",
                ["ru"] = "🗑️ Удалить программу"
            };
            dict["DesktopShortcut"] = new()
            {
                ["en"] = "⚡ Desktop Shortcut",
                ["de"] = "⚡ Desktop-Verknüpfung",
                ["ru"] = "⚡ Ярлык на рабочем столе"
            };
            dict["HotkeyTitle"] = new()
            {
                ["en"] = "⌨️ Custom Global Windows Hotkey",
                ["de"] = "⌨️ Benutzerdefinierter globaler Hotkey",
                ["ru"] = "⌨️ Глобальный хоткей Windows"
            };
            dict["SaveHotkey"] = new()
            {
                ["en"] = "💾 Register & Save Hotkey",
                ["de"] = "💾 Hotkey registrieren & speichern",
                ["ru"] = "💾 Зарегистрировать и сохранить хоткей"
            };
            dict["WifiDiscovery"] = new()
            {
                ["en"] = "🔍 Wi-Fi Discovery",
                ["de"] = "🔍 Wi-Fi Erkennung",
                ["ru"] = "🔍 Wi-Fi Поиск"
            };
            dict["ScanWifi"] = new()
            {
                ["en"] = "⚡ Scan",
                ["de"] = "⚡ Scannen",
                ["ru"] = "⚡ Сканировать"
            };
            dict["CloudSync"] = new()
            {
                ["en"] = "☁️ Auto-Fetch Key from Tuya Cloud",
                ["de"] = "☁️ Schlüssel aus Tuya Cloud abrufen",
                ["ru"] = "☁️ Получение ключа из Tuya Cloud"
            };
            dict["AccessId"] = new()
            {
                ["en"] = "Access ID:",
                ["de"] = "Access-ID:",
                ["ru"] = "Access ID:"
            };
            dict["AccessKey"] = new()
            {
                ["en"] = "Access Key (Secret):",
                ["de"] = "Access-Schlüssel (Geheimnis):",
                ["ru"] = "Access Key (Секрет):"
            };
            dict["FetchKeyCloud"] = new()
            {
                ["en"] = "⚡ Fetch Key",
                ["de"] = "⚡ Schlüssel abrufen",
                ["ru"] = "⚡ Получить ключ"
            };
            dict["CloudSyncAll"] = new()
            {
                ["en"] = "📥 Sync All",
                ["de"] = "📥 Alle Geräte",
                ["ru"] = "📥 Все устройства"
            };
            dict["TooltipSyncAll"] = new()
            {
                ["en"] = "Fetch credentials and synchronize all devices from your Tuya Cloud account",
                ["de"] = "Zugangsdaten abrufen und alle Geräte aus dem Tuya Cloud-Konto synchronisieren",
                ["ru"] = "Получить данные и синхронизировать все устройства из аккаунта Tuya Cloud"
            };
            dict["Instructions"] = new()
            {
                ["en"] = "❓ Guide",
                ["de"] = "❓ Hilfe",
                ["ru"] = "❓ Справка"
            };
            dict["TooltipGuide"] = new()
            {
                ["en"] = "Open step-by-step setup guide for obtaining Tuya IoT API credentials",
                ["de"] = "Schritt-für-Schritt-Anleitung für Tuya IoT API-Zugangsdaten öffnen",
                ["ru"] = "Открыть пошаговую инструкцию по получению API-ключей Tuya IoT"
            };
            dict["ControlModeTitle"] = new()
            {
                ["en"] = "🎮 Control Mode & Fallback Priority",
                ["de"] = "🎮 Steuerungsmodus & Priorität",
                ["ru"] = "🎮 Режим управления и приоритет"
            };
            dict["ControlMode"] = new()
            {
                ["en"] = "Control Mode:",
                ["de"] = "Steuerungsmodus:",
                ["ru"] = "Режим управления:"
            };
            dict["ControlModeAuto"] = new()
            {
                ["en"] = "🔄 Auto (Hybrid Fallback)",
                ["de"] = "🔄 Auto (Hybrides Fallback)",
                ["ru"] = "🔄 Авто (Гибридный режим)"
            };
            dict["ControlModeLocal"] = new()
            {
                ["en"] = "⚡ Local LAN Only",
                ["de"] = "⚡ Nur lokales LAN",
                ["ru"] = "⚡ Только локальная сеть"
            };
            dict["ControlModeCloud"] = new()
            {
                ["en"] = "☁️ Tuya Cloud API Only",
                ["de"] = "☁️ Nur Tuya Cloud API",
                ["ru"] = "☁️ Только Tuya Cloud API"
            };
            dict["AutoPriority"] = new()
            {
                ["en"] = "Auto Priority Order:",
                ["de"] = "Prioritätsreihenfolge:",
                ["ru"] = "Порядок приоритета:"
            };
            dict["PriorityCloudFirst"] = new()
            {
                ["en"] = "☁️ Cloud ➔ Local LAN",
                ["de"] = "☁️ Cloud ➔ Lokales LAN",
                ["ru"] = "☁️ Облако ➔ Резерв: LAN"
            };
            dict["PriorityLocalFirst"] = new()
            {
                ["en"] = "⚡ Local LAN ➔ Cloud",
                ["de"] = "⚡ Lokales LAN ➔ Cloud",
                ["ru"] = "⚡ LAN ➔ Резерв: Облако"
            };

            // --- Dialogs & Alerts ---
            dict["DeviceSavedMsg"] = new()
            {
                ["en"] = "Device saved successfully!",
                ["de"] = "Gerät erfolgreich gespeichert!",
                ["ru"] = "Устройство успешно сохранено!"
            };
            dict["DeviceDeletedMsg"] = new()
            {
                ["en"] = "Device deleted successfully.",
                ["de"] = "Gerät erfolgreich gelöscht.",
                ["ru"] = "Устройство успешно удалено."
            };
            dict["ConfirmDeleteDevice"] = new()
            {
                ["en"] = "Are you sure you want to delete '{0}' ({1})?",
                ["de"] = "Möchten Sie '{0}' ({1}) wirklich löschen?",
                ["ru"] = "Вы уверены, что хотите удалить '{0}' ({1})?"
            };
            dict["NoDevicesConfigured"] = new()
            {
                ["en"] = "No devices configured yet. Switch to Settings tab to add one.",
                ["de"] = "Noch keine Geräte konfiguriert. Wechseln Sie zu Einstellungen, um eines hinzuzufügen.",
                ["ru"] = "Устройства ещё не настроены. Перейдите в 'Настройки' для добавления."
            };
            dict["AddDeviceInSettings"] = new()
            {
                ["en"] = "➕ Add Device in Settings",
                ["de"] = "➕ Gerät in Einstellungen hinzufügen",
                ["ru"] = "➕ Добавить устройство в Настройках"
            };
            dict["SynchronizedLamps"] = new()
            {
                ["en"] = "{0} synchronized lamps ({1})",
                ["de"] = "{0} synchronisierte Lampen ({1})",
                ["ru"] = "{0} синхронизированных ламп ({1})"
            };
            dict["DefaultDeviceInfo"] = new()
            {
                ["en"] = "IP: Select or Add Device  |  ID: --",
                ["de"] = "IP: Gerät auswählen oder hinzufügen  |  ID: --",
                ["ru"] = "IP: Выберите или добавьте устройство  |  ID: --"
            };

            // --- Backup & Data Portability ---
            dict["BackupTitle"] = new()
            {
                ["en"] = "💾 Backup & Data Migration",
                ["de"] = "💾 Sicherung & Datenmigration",
                ["ru"] = "💾 Резервное копирование и перенос"
            };
            dict["BackupDesc"] = new()
            {
                ["en"] = "Export or import all devices, room groups, custom presets, and settings to a portable JSON backup file.",
                ["de"] = "Alle Geräte, Raumgruppen, eigene Presets und Einstellungen in eine portable JSON-Sicherung exportieren oder importieren.",
                ["ru"] = "Экспорт и импорт всех устройств, групп комнат, пресетов и настроек в переносимый JSON-файл."
            };
            dict["ExportBackup"] = new()
            {
                ["en"] = "📤 Export Backup",
                ["de"] = "📤 Sicherung exportieren",
                ["ru"] = "📤 Экспорт настроек"
            };
            dict["ImportBackup"] = new()
            {
                ["en"] = "📥 Import Backup",
                ["de"] = "📥 Sicherung importieren",
                ["ru"] = "📥 Импорт настроек"
            };
            dict["BackupExportSuccess"] = new()
            {
                ["en"] = "Configuration successfully exported to:\n{0}",
                ["de"] = "Konfiguration erfolgreich exportiert nach:\n{0}",
                ["ru"] = "Конфигурация успешно экспортирована в:\n{0}"
            };
            dict["BackupImportConfirm"] = new()
            {
                ["en"] = "Found in backup file:\n• {0} device(s)\n• {1} room group(s)\n• {2} custom preset(s)\n\nClick [Yes] to OVERWRITE and REPLACE existing configuration.\nClick [No] to MERGE new items with current ones.\nClick [Cancel] to abort.",
                ["de"] = "In Sicherung gefunden:\n• {0} Gerät(e)\n• {1} Raumgruppe(n)\n• {2} eigene(s) Preset(s)\n\nKlicken Sie [Ja], um aktuelle Konfiguration zu ERSETZEN.\nKlicken Sie [Nein], um neue Elemente ZUSAMMENZUFÜHREN.\nKlicken Sie [Abbrechen] zum Abbrechen.",
                ["ru"] = "Найдено в файле резервной копии:\n• {0} устройств\n• {1} групп комнат\n• {2} пресетов\n\nНажмите [Да], чтобы ПОЛНОСТЬЮ ЗАМЕНИТЬ текущие настройки.\nНажмите [Нет], чтобы ОБЪЕДИНИТЬ новые устройства с текущими.\nНажмите [Отмена] для отмены."
            };
            dict["BackupImportSuccess"] = new()
            {
                ["en"] = "Configuration successfully restored ({0} mode)!",
                ["de"] = "Konfiguration erfolgreich wiederhergestellt ({0}-Modus)!",
                ["ru"] = "Конфигурация успешно восстановлена (режим: {0})!"
            };
            dict["BackupImportError"] = new()
            {
                ["en"] = "Failed to parse backup file. Please select a valid SmartLamp backup or config JSON file.",
                ["de"] = "Fehler beim Lesen der Sicherungsdatei. Bitte wählen Sie eine gültige SmartLamp-Sicherungsdatei aus.",
                ["ru"] = "Не удалось прочитать файл резервной копии. Убедитесь, что выбран корректный JSON-файл SmartLamp."
            };
        }
    }
}
