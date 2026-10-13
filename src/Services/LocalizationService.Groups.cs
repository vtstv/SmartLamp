// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;

namespace SmartLampApp.Services
{
    public static partial class LocalizationService
    {
        static partial void RegisterGroupsStrings(Dictionary<string, Dictionary<string, string>> dict)
        {
            // --- Rooms & Lamp Groups ---
            dict["RoomsAndGroups"] = new()
            {
                ["en"] = "Rooms & Lamp Groups",
                ["de"] = "Räume & Lampengruppen",
                ["ru"] = "Комнаты и группы ламп"
            };
            dict["AddRoom"] = new()
            {
                ["en"] = "➕ Add Room",
                ["de"] = "➕ Neuer Raum",
                ["ru"] = "➕ Новая комната"
            };
            dict["TooltipAddRoom"] = new()
            {
                ["en"] = "Create a new room or device group for unified lighting control",
                ["de"] = "Neuen Raum oder Lampengruppe für gemeinsame Steuerung erstellen",
                ["ru"] = "Создать новую комнату или группу устройств для совместного управления"
            };
            dict["NoGroupsCreated"] = new()
            {
                ["en"] = "👥 No groups or rooms created yet",
                ["de"] = "👥 Noch keine Gruppen oder Räume erstellt",
                ["ru"] = "👥 Группы и комнаты пока не созданы"
            };
            dict["NoGroupsHint"] = new()
            {
                ["en"] = "Click '+ Add Room' above to group your lamps for simultaneous control.",
                ["de"] = "Klicken Sie oben auf '+ Raum hinzufügen', um Lampen gemeinsam zu steuern.",
                ["ru"] = "Нажмите '+ Добавить комнату' выше для одновременного управления лампами."
            };
            dict["LampUnit"] = new()
            {
                ["en"] = "lamp",
                ["de"] = "Lampe",
                ["ru"] = "лампа"
            };
            dict["LampsUnit"] = new()
            {
                ["en"] = "lamps",
                ["de"] = "Lampen",
                ["ru"] = "ламп"
            };
            dict["EditRoomAndRules"] = new()
            {
                ["en"] = "✏️ Edit Room & Rules",
                ["de"] = "✏️ Raum & Regeln bearbeiten",
                ["ru"] = "✏️ Настроить комнату"
            };
            dict["GroupConfigRules"] = new()
            {
                ["en"] = "⚙️ Group Configuration & Rules",
                ["de"] = "⚙️ Gruppenkonfiguration & Regeln",
                ["ru"] = "⚙️ Настройки и правила группы"
            };
            dict["RoomIcon"] = new()
            {
                ["en"] = "Room Icon:",
                ["de"] = "Raumsymbol:",
                ["ru"] = "Иконка комнаты:"
            };
            dict["GroupNameEdit"] = new()
            {
                ["en"] = "Group / Room Name:",
                ["de"] = "Gruppen- / Raumname:",
                ["ru"] = "Название группы / комнаты:"
            };
            dict["IncludedLamps"] = new()
            {
                ["en"] = "Included Lamps in this Room:",
                ["de"] = "Lampen in diesem Raum:",
                ["ru"] = "Лампы в этой комнате:"
            };
            dict["SyncRules"] = new()
            {
                ["en"] = "Synchronization Rules & Power Policy:",
                ["de"] = "Synchronisationsregeln & Strom:",
                ["ru"] = "Правила синхронизации и питания:"
            };
            dict["DimmingMode"] = new()
            {
                ["en"] = "Dimming Mode:",
                ["de"] = "Dimm-Modus:",
                ["ru"] = "Режим регулировки:"
            };
            dict["SyncModeSynchronized"] = new()
            {
                ["en"] = "🔄 Synchronized (1:1 Brightness & Color)",
                ["de"] = "🔄 Synchron (Gleiche Helligkeit & Farbe)",
                ["ru"] = "🔄 Синхронный (Одинаковая яркость и цвет)"
            };
            dict["SyncModeProportional"] = new()
            {
                ["en"] = "⚖️ Proportional (Keep Relative Offsets)",
                ["de"] = "⚖️ Proportional (Relative Abstufung)",
                ["ru"] = "⚖️ Пропорциональный (Относительная яркость)"
            };
            dict["TurnOffAllLamps"] = new()
            {
                ["en"] = "Turn off all lamps when room is turned off",
                ["de"] = "Alle Lampen ausschalten, wenn der Raum ausgeschaltet wird",
                ["ru"] = "Выключать все лампы при выключении комнаты"
            };
            dict["TurnOnAllLamps"] = new()
            {
                ["en"] = "Turn on all lamps when room is turned on",
                ["de"] = "Alle Lampen einschalten, wenn der Raum eingeschaltet wird",
                ["ru"] = "Включать все лампы при включении комнаты"
            };
            dict["SaveRoom"] = new()
            {
                ["en"] = "💾 Save Room",
                ["de"] = "💾 Raum speichern",
                ["ru"] = "💾 Сохранить комнату"
            };
            dict["TestToggle"] = new()
            {
                ["en"] = "⚡ Test",
                ["de"] = "⚡ Testen",
                ["ru"] = "⚡ Тест"
            };
            dict["TooltipTestToggle"] = new()
            {
                ["en"] = "Send a test power toggle command to all lamps in this room",
                ["de"] = "Testbefehl zum Umschalten an alle Lampen im Raum senden",
                ["ru"] = "Отправить тестовую команду переключения всем лампам в комнате"
            };
            dict["NoDevicesInRoomHint"] = new()
            {
                ["en"] = "No devices configured yet. Add lamps in Settings tab first.",
                ["de"] = "Noch keine Geräte konfiguriert. Fügen Sie zuerst Lampen in den Einstellungen hinzu.",
                ["ru"] = "Устройства ещё не добавлены. Добавьте лампы во вкладке 'Настройки'."
            };
            dict["RoomSavedMsg"] = new()
            {
                ["en"] = "Room '{0}' saved successfully with {1} lamp(s)!",
                ["de"] = "Raum '{0}' erfolgreich mit {1} Lampe(n) gespeichert!",
                ["ru"] = "Комната '{0}' успешно сохранена с {1} ламп(ами)!"
            };
            dict["ConfirmDeleteRoom"] = new()
            {
                ["en"] = "Are you sure you want to delete room '{0}'?",
                ["de"] = "Möchten Sie den Raum '{0}' wirklich löschen?",
                ["ru"] = "Вы уверены, что хотите удалить комнату '{0}'?"
            };

            // --- Bridges (Google Home & MQTT) ---
            dict["GoogleHomeBridgeTitle"] = new()
            {
                ["en"] = "🌐 Google Smart Home Local Bridge",
                ["de"] = "🌐 Google Smart Home lokale Bridge",
                ["ru"] = "🌐 Локальный мост Google Smart Home"
            };
            dict["GoogleHomeBridgeDesc"] = new()
            {
                ["en"] = "Integrate your Smart Lamp with Google Home & Assistant ecosystem via Local Home SDK / REST Webhook Bridge.",
                ["de"] = "Integrieren Sie Ihre Smart Lamp in das Google Home Ökosystem über die lokale Bridge.",
                ["ru"] = "Интеграция умной лампы с Google Home и Assistant через локальный мост."
            };
            dict["RestartGoogleBridge"] = new()
            {
                ["en"] = "⚡ Restart Google Home Bridge",
                ["de"] = "⚡ Google Home Bridge neu starten",
                ["ru"] = "⚡ Перезапустить Google Home мост"
            };
            dict["GoogleSimTitle"] = new()
            {
                ["en"] = "🗣️ Google Assistant Voice Simulator",
                ["de"] = "🗣️ Google Assistant Sprachsimulator",
                ["ru"] = "🗣️ Симулятор команд Google Assistant"
            };
            dict["GoogleSimOn"] = new()
            {
                ["en"] = "🗣️ 'Turn On Lamp'",
                ["de"] = "🗣️ 'Lampe einschalten'",
                ["ru"] = "🗣️ 'Включи лампу'"
            };
            dict["GoogleSimOff"] = new()
            {
                ["en"] = "🗣️ 'Turn Off Lamp'",
                ["de"] = "🗣️ 'Lampe ausschalten'",
                ["ru"] = "🗣️ 'Выключи лампу'"
            };
            dict["GoogleEventLog"] = new()
            {
                ["en"] = "Google Assistant Event Log:",
                ["de"] = "Google Assistant Ereignisprotokoll:",
                ["ru"] = "Журнал событий Google Assistant:"
            };
            dict["MqttBridgeTitle"] = new()
            {
                ["en"] = "🤖 Home Assistant & MQTT Bridge",
                ["de"] = "🤖 Home Assistant & MQTT Bridge",
                ["ru"] = "🤖 Мост Home Assistant & MQTT"
            };
            dict["MqttBridgeDesc"] = new()
            {
                ["en"] = "Native Home Assistant MQTT Auto-Discovery & REST Bridge Server on Port 1883.",
                ["de"] = "Home Assistant MQTT Auto-Discovery & REST Bridge Server auf Port 1883.",
                ["ru"] = "Сервер MQTT Auto-Discovery и REST мост для Home Assistant на порту 1883."
            };
            dict["RestartMqttBridge"] = new()
            {
                ["en"] = "⚡ Restart HA Bridge",
                ["de"] = "⚡ HA Bridge neu starten",
                ["ru"] = "⚡ Перезапустить HA мост"
            };
            dict["CopyMqttJson"] = new()
            {
                ["en"] = "📋 Copy HA Discovery JSON",
                ["de"] = "📋 HA-Discovery-JSON kopieren",
                ["ru"] = "📋 Скопировать JSON для HA"
            };
            dict["MqttSimTitle"] = new()
            {
                ["en"] = "🤖 Home Assistant Event Simulator",
                ["de"] = "🤖 Home Assistant Simulator",
                ["ru"] = "🤖 Симулятор событий Home Assistant"
            };
            dict["MqttSimOn"] = new()
            {
                ["en"] = "🤖 HA: 'Turn ON'",
                ["de"] = "🤖 HA: 'Einschalten'",
                ["ru"] = "🤖 HA: 'Включить'"
            };
            dict["MqttSimOff"] = new()
            {
                ["en"] = "🤖 HA: 'Turn OFF'",
                ["de"] = "🤖 HA: 'Ausschalten'",
                ["ru"] = "🤖 HA: 'Выключить'"
            };
            dict["MqttEventLog"] = new()
            {
                ["en"] = "Home Assistant & MQTT Log:",
                ["de"] = "Home Assistant & MQTT Protokoll:",
                ["ru"] = "Журнал событий MQTT:"
            };

            // --- Modals ---
            dict["SavePresetTitle"] = new()
            {
                ["en"] = "⭐ Save Custom Preset",
                ["de"] = "⭐ Eigenes Preset speichern",
                ["ru"] = "⭐ Сохранить пресет"
            };
            dict["PresetName"] = new()
            {
                ["en"] = "Preset Name:",
                ["de"] = "Preset-Name:",
                ["ru"] = "Имя пресета:"
            };
            dict["CurrentSettingsToSave"] = new()
            {
                ["en"] = "Current Settings to Save:",
                ["de"] = "Aktuelle Einstellungen zum Speichern:",
                ["ru"] = "Текущие настройки для сохранения:"
            };
            dict["Cancel"] = new()
            {
                ["en"] = "Cancel",
                ["de"] = "Abbrechen",
                ["ru"] = "Отмена"
            };
            dict["SavePreset"] = new()
            {
                ["en"] = "💾 Save Preset",
                ["de"] = "💾 Preset speichern",
                ["ru"] = "💾 Сохранить пресет"
            };
            dict["UpdateAvailableTitle"] = new()
            {
                ["en"] = "🚀 Update Available!",
                ["de"] = "🚀 Update verfügbar!",
                ["ru"] = "🚀 Доступно обновление!"
            };
            dict["UpdateSubtitle"] = new()
            {
                ["en"] = "A new version of SmartLamp Studio is available.",
                ["de"] = "Eine neue Version von SmartLamp Studio ist verfügbar.",
                ["ru"] = "Доступна новая версия SmartLamp Studio."
            };
            dict["ReleaseNotes"] = new()
            {
                ["en"] = "Release Notes / What's New:",
                ["de"] = "Versionshinweise / Neuerungen:",
                ["ru"] = "Список изменений / Что нового:"
            };
            dict["SkipUpdate"] = new()
            {
                ["en"] = "Skip Version",
                ["de"] = "Version überspringen",
                ["ru"] = "Пропустить версию"
            };
            dict["RemindLater"] = new()
            {
                ["en"] = "Remind Later",
                ["de"] = "Später erinnern",
                ["ru"] = "Напомнить позже"
            };
            dict["UpdateNow"] = new()
            {
                ["en"] = "⚡ Update Now",
                ["de"] = "⚡ Jetzt aktualisieren",
                ["ru"] = "⚡ Обновить сейчас"
            };
            dict["TuyaGuideTitle"] = new()
            {
                ["en"] = "How to Get Access ID & Key in Tuya Cloud",
                ["de"] = "So erhalten Sie Access-ID & Schlüssel in der Tuya Cloud",
                ["ru"] = "Как получить Access ID и ключ в Tuya Cloud"
            };
            dict["GotIt"] = new()
            {
                ["en"] = "Got it!",
                ["de"] = "Verstanden!",
                ["ru"] = "Понятно!"
            };
        }
    }
}
