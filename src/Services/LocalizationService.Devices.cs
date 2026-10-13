// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Collections.Generic;

namespace SmartLampApp.Services
{
    public static partial class LocalizationService
    {
        static partial void RegisterDevicesStrings(Dictionary<string, Dictionary<string, string>> dict)
        {
            // --- Devices Tab Navigation & Header ---
            dict["NavDevices"] = new()
            {
                ["en"] = "🔌 Devices",
                ["de"] = "🔌 Geräte",
                ["ru"] = "🔌 Устройства"
            };
            dict["MyDevicesTitle"] = new()
            {
                ["en"] = "💡 My Smart Lamps & Devices",
                ["de"] = "💡 Meine smarten Lampen & Geräte",
                ["ru"] = "💡 Мои смарт-лампы и устройства"
            };
            dict["AddDeviceWizardBtn"] = new()
            {
                ["en"] = "🪄 Setup Wizard",
                ["de"] = "🪄 Einrichtungsassistent",
                ["ru"] = "🪄 Мастер подключения"
            };
            dict["TooltipWizard"] = new()
            {
                ["en"] = "Launch interactive step-by-step smart lamp setup wizard",
                ["de"] = "Interaktiven Schritt-für-Schritt-Assistenten starten",
                ["ru"] = "Запустить пошаговый мастер подключения умной лампы"
            };
            dict["TestDeviceBtn"] = new()
            {
                ["en"] = "⚡ Test",
                ["de"] = "⚡ Testen",
                ["ru"] = "⚡ Тест"
            };

            // --- Device Setup Wizard Modal ---
            dict["WizardTitle"] = new()
            {
                ["en"] = "🪄 Smart Lamp Setup Wizard",
                ["de"] = "🪄 Smart-Lamp Einrichtungsassistent",
                ["ru"] = "🪄 Мастер подключения умной лампы"
            };
            dict["WizardStep1Subtitle"] = new()
            {
                ["en"] = "Step 1 of 3: Choose Connection Method",
                ["de"] = "Schritt 1 von 3: Verbindungsmethode wählen",
                ["ru"] = "Шаг 1 из 3: Выберите способ подключения"
            };
            dict["WizardStep2Subtitle"] = new()
            {
                ["en"] = "Step 2 of 3: Discover & Pair Lamp",
                ["de"] = "Schritt 2 von 3: Lampe suchen & koppeln",
                ["ru"] = "Шаг 2 из 3: Поиск и привязка лампы"
            };
            dict["WizardStep3Subtitle"] = new()
            {
                ["en"] = "Step 3 of 3: Name & Test Lamp",
                ["de"] = "Schritt 3 von 3: Lampe benennen & testen",
                ["ru"] = "Шаг 3 из 3: Название и проверка связи"
            };

            // Step 1 Options
            dict["WizardOptWifiTitle"] = new()
            {
                ["en"] = "🔍 Wi-Fi Auto-Discovery",
                ["de"] = "🔍 Wi-Fi Auto-Erkennung",
                ["ru"] = "🔍 Автопоиск в сети Wi-Fi"
            };
            dict["WizardOptWifiDesc"] = new()
            {
                ["en"] = "Scan local network for Tuya lamps and bulbs connected to your Wi-Fi router",
                ["de"] = "Lokales Netzwerk nach Tuya-Lampen durchsuchen, die mit dem WLAN verbunden sind",
                ["ru"] = "Поиск ламп и устройств Tuya, подключенных к вашему домашнему Wi-Fi роутеру"
            };

            dict["WizardOptCloudTitle"] = new()
            {
                ["en"] = "☁️ Tuya Cloud One-Click Sync",
                ["de"] = "☁️ Tuya Cloud 1-Klick-Synchronisierung",
                ["ru"] = "☁️ Импорт из Tuya Cloud"
            };
            dict["WizardOptCloudDesc"] = new()
            {
                ["en"] = "Automatically import all devices and encryption keys from your Tuya / Smart Life account",
                ["de"] = "Automatisch alle Geräte & Schlüssel aus Ihrem Tuya/Smart Life-Konto importieren",
                ["ru"] = "Автоматический импорт всех ламп и локальных ключей из вашего аккаунта Tuya / Smart Life"
            };

            dict["WizardOptManualTitle"] = new()
            {
                ["en"] = "✍️ Manual Configuration",
                ["de"] = "✍️ Manuelle Konfiguration",
                ["ru"] = "✍️ Ручной ввод параметров"
            };
            dict["WizardOptManualDesc"] = new()
            {
                ["en"] = "Directly enter IP address, Device ID, and 16-character Local Key manually",
                ["de"] = "IP-Adresse, Geräte-ID und 16-stelligen lokalen Schlüssel direkt manuell eingeben",
                ["ru"] = "Прямой ввод IP-адреса, ID устройства и 16-значного локального ключа вручную"
            };

            // Wizard Navigation & Buttons
            dict["WizardBack"] = new()
            {
                ["en"] = "◀ Back",
                ["de"] = "◀ Zurück",
                ["ru"] = "◀ Назад"
            };
            dict["WizardNext"] = new()
            {
                ["en"] = "Next ▶",
                ["de"] = "Weiter ▶",
                ["ru"] = "Далее ▶"
            };
            dict["WizardScanNow"] = new()
            {
                ["en"] = "⚡ Scan Network",
                ["de"] = "⚡ Netzwerk scannen",
                ["ru"] = "⚡ Сканировать сеть"
            };
            dict["WizardScanning"] = new()
            {
                ["en"] = "⏳ Scanning Wi-Fi subnet...",
                ["de"] = "⏳ WLAN-Subnetz wird gescannt...",
                ["ru"] = "⏳ Сканирование сети Wi-Fi..."
            };
            dict["WizardFoundDevices"] = new()
            {
                ["en"] = "Discovered Devices:",
                ["de"] = "Gefundene Geräte:",
                ["ru"] = "Найденные устройства:"
            };
            dict["WizardNoDevicesFound"] = new()
            {
                ["en"] = "No new devices found. Please ensure the lamp is turned on and connected to the same Wi-Fi.",
                ["de"] = "Keine neuen Geräte gefunden. Stellen Sie sicher, dass die Lampe im selben WLAN ist.",
                ["ru"] = "Устройства не найдены. Убедитесь, что лампа включена и подключена к той же сети Wi-Fi."
            };
            dict["WizardSelectDevicePrompt"] = new()
            {
                ["en"] = "Please select a lamp from the discovered list above.",
                ["de"] = "Bitte wählen Sie eine Lampe aus der Liste oben aus.",
                ["ru"] = "Пожалуйста, выберите лампу из списка найденных выше."
            };
            dict["WizardKeyDetected"] = new()
            {
                ["en"] = "🟢 Local Key automatically detected!",
                ["de"] = "🟢 Lokaler Schlüssel automatisch erkannt!",
                ["ru"] = "🟢 Локальный ключ определен автоматически!"
            };
            dict["WizardKeyNeededPrompt"] = new()
            {
                ["en"] = "Local Key needed (fetch from Cloud or enter 16 chars):",
                ["de"] = "Lokaler Schlüssel erforderlich (aus Cloud abrufen oder eingeben):",
                ["ru"] = "Требуется локальный ключ (получите из Cloud или введите 16 знаков):"
            };
            dict["WizardTestConnection"] = new()
            {
                ["en"] = "⚡ Test Power Toggle",
                ["de"] = "⚡ Test-Umschaltung",
                ["ru"] = "⚡ Проверить связь (Тест)"
            };
            dict["WizardTestSuccess"] = new()
            {
                ["en"] = "🟢 Lamp responded! Connection verified.",
                ["de"] = "🟢 Lampe hat geantwortet! Verbindung verifiziert.",
                ["ru"] = "🟢 Лампа откликнулась! Связь подтверждена."
            };
            dict["WizardTestFail"] = new()
            {
                ["en"] = "🔴 Lamp did not respond. Check IP address & Local Key.",
                ["de"] = "🔴 Lampe hat nicht geantwortet. IP & Schlüssel prüfen.",
                ["ru"] = "🔴 Лампа не ответила. Проверьте IP-адрес и локальный ключ."
            };
            dict["WizardFinish"] = new()
            {
                ["en"] = "✅ Add to System",
                ["de"] = "✅ Zum System hinzufügen",
                ["ru"] = "✅ Добавить в систему"
            };
            dict["WizardTestPrompt"] = new()
            {
                ["en"] = "Click 'Test Power Toggle' to verify socket connectivity",
                ["de"] = "Klicken Sie auf 'Test-Umschaltung', um die Verbindung zu prüfen",
                ["ru"] = "Нажмите 'Проверить связь', чтобы протестировать сокет"
            };
            dict["WizardCloudFetchBtn"] = new()
            {
                ["en"] = "📥 Fetch Lamps from Cloud Project",
                ["de"] = "📥 Lampen aus Cloud-Projekt abrufen",
                ["ru"] = "📥 Загрузить лампы из облачного проекта"
            };
            dict["WizardBadgeStep1"] = new()
            {
                ["en"] = "1. Method",
                ["de"] = "1. Methode",
                ["ru"] = "1. Способ"
            };
            dict["WizardBadgeStep2"] = new()
            {
                ["en"] = "2. Discover / Connect",
                ["de"] = "2. Suchen & Verbinden",
                ["ru"] = "2. Поиск и связь"
            };
            dict["WizardBadgeStep3"] = new()
            {
                ["en"] = "3. Name & Test",
                ["de"] = "3. Benennen & Test",
                ["ru"] = "3. Имя и тест"
            };
            dict["WizardSuccessMsg"] = new()
            {
                ["en"] = "Smart Lamp '{0}' was successfully added and is ready for control!",
                ["de"] = "Smarte Lampe '{0}' wurde erfolgreich hinzugefügt und ist betriebsbereit!",
                ["ru"] = "Умная лампа '{0}' успешно добавлена и готова к управлению!"
            };

            // --- QR Code & Mobile App Login ---
            dict["QrLoginBtn"] = new()
            {
                ["en"] = "📲 QR Login",
                ["de"] = "📲 QR-Anmeldung",
                ["ru"] = "📲 Вход по QR"
            };
            dict["TooltipQrLogin"] = new()
            {
                ["en"] = "Quick login via Smart Life / Tuya Smart mobile app QR scan (no developer account)",
                ["de"] = "Schnellanmeldung per QR-Scan in Smart Life / Tuya Smart (kein Entwicklerkonto)",
                ["ru"] = "Быстрый вход через QR-код в Smart Life / Tuya Smart (без аккаунта разработчика)"
            };
            dict["WizardOptQrTitle"] = new()
            {
                ["en"] = "📲 Scan QR Code / App Login (Recommended)",
                ["de"] = "📲 QR-Code scannen / App-Anmeldung (Empfohlen)",
                ["ru"] = "📲 Вход через QR-код / Приложение (Рекомендуется)"
            };
            dict["WizardOptQrDesc"] = new()
            {
                ["en"] = "Log in instantly with Smart Life or Tuya Smart app — scan a QR code to import all lamps and keys automatically",
                ["de"] = "Sofort mit Smart Life oder Tuya Smart anmelden — QR-Code scannen, um alle Lampen & Schlüssel automatisch zu laden",
                ["ru"] = "Быстрый вход через приложение Smart Life или Tuya Smart — сканируйте QR-код для автоматического импорта ламп и ключей"
            };
            dict["QrInstruction"] = new()
            {
                ["en"] = "Log in with your Smart Life or Tuya Smart phone app:",
                ["de"] = "Melden Sie sich mit Ihrer Smart Life- oder Tuya Smart-App an:",
                ["ru"] = "Войдите через мобильное приложение Smart Life или Tuya Smart:"
            };
            dict["UserCodePrompt"] = new()
            {
                ["en"] = "User Code from App:",
                ["de"] = "Benutzercode aus App:",
                ["ru"] = "Код пользователя из приложения:"
            };
            dict["UserCodeHelp"] = new()
            {
                ["en"] = "💡 In app: Me ➔ Settings (gear) ➔ Account and Security ➔ User Code",
                ["de"] = "💡 In der App: Profil ➔ Einstellungen (Zahnrad) ➔ Konto und Sicherheit ➔ Benutzercode",
                ["ru"] = "💡 В приложении: Профиль ➔ Настройки (шестерёнка) ➔ Безопасность аккаунта ➔ Код пользователя"
            };
            dict["GenerateQrBtn"] = new()
            {
                ["en"] = "📱 Generate Login QR Code",
                ["de"] = "📱 Anmelde-QR-Code erstellen",
                ["ru"] = "📱 Создать QR-код для входа"
            };
            dict["QrWaitingScan"] = new()
            {
                ["en"] = "📲 Scan this QR code with your Smart Life app scanner (+) and confirm authorization on phone.",
                ["de"] = "📲 Scannen Sie diesen QR-Code mit Smart Life (+) und bestätigen Sie die Anmeldung auf dem Telefon.",
                ["ru"] = "📲 Отсканируйте этот QR-код сканером в приложении Smart Life (+) и подтвердите вход на телефоне."
            };
            dict["QrAuthorized"] = new()
            {
                ["en"] = "✅ Login authorized! Fetching lamps and local keys...",
                ["de"] = "✅ Anmeldung autorisiert! Lampen & lokale Schlüssel werden abgerufen...",
                ["ru"] = "✅ Авторизация подтверждена! Загружаем лампы и локальные ключи..."
            };
            dict["QrDevicesImported"] = new()
            {
                ["en"] = "Successfully imported {0} lamp(s) from your mobile app!",
                ["de"] = "{0} Lampe(n) erfolgreich aus Ihrer mobilen App importiert!",
                ["ru"] = "Успешно импортировано {0} ламп из вашего мобильного приложения!"
            };
            dict["QrUserCodeRequired"] = new()
            {
                ["en"] = "Please enter your User Code from the Smart Life or Tuya Smart app.",
                ["de"] = "Bitte geben Sie Ihren Benutzercode aus der Smart Life- oder Tuya Smart-App ein.",
                ["ru"] = "Пожалуйста, введите код пользователя из приложения Smart Life или Tuya Smart."
            };
        }
    }
}
