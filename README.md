# AnalizatorWiFi

Wieloplatformowa aplikacja desktopowa do analizy sieci WiFi i połączeń sieciowych. Napisana w C# (.NET 9) z interfejsem Avalonia UI, działa na Windows i Linux.

## Funkcje

### Sieci WiFi
- Skanowanie dostępnych sieci — jednorazowe lub ciągłe (konfigurowalny interwał)
- Filtrowanie po paśmie: 2.4 GHz / 5 GHz / 6 GHz
- Grupowanie po SSID z widokiem wszystkich punktów dostępowych (BSSID)
- Dla każdej sieci: siła sygnału (dBm i %), kanał, szerokość kanału, standard (802.11b/g/n / WiFi 4/5/6/7), zabezpieczenia (WEP/WPA/WPA2/WPA3/Enterprise), szacunkowa odległość, producent karty (OUI)

### Widmo kanałów
- Wizualizacja zajętości kanałów dla każdego pasma
- Kolorowy wykres nakładania się sieci na osi częstotliwości

### Aktualne połączenie
- Szczegóły bieżącej sesji: SSID, BSSID, IP, brama, DNS, prędkość łącza, sygnał, kanał, standard
- Szacunkowa odległość od routera na podstawie poziomu sygnału
- Łączenie i rozłączanie z sieciami, ping do bramy

### Historia sygnału
- Wykresy zmian siły sygnału wybranych BSSID w czasie
- Zakresy: 1h / 6h / 24h / 7d / 30d
- Dane przechowywane lokalnie w bazie SQLite

### Test prędkości
- Pomiar download/upload za pomocą **iperf3**
- Wyniki: przepustowość (Mbps), jitter (ms), utrata pakietów (%)
- Test ping z pełnymi statystykami (avg/min/max/utrata)
- Obsługa wielu serwerów iperf3 konfigurowanych w ustawieniach

### Analizator połączeń TCP
- Monitorowanie aktywnych połączeń TCP w czasie rzeczywistym (odświeżanie co 3 s)
- Geolokalizacja zdalnych adresów IP z mapą świata
- Podsumowanie po portach (top 40) z nazwami usług
- Bieżąca prędkość pobierania/wysyłania na poziomie interfejsów sieciowych
- Filtrowanie listy połączeń po adresie, kraju, usłudze lub stanie

### Ustawienia
- Motyw: systemowy / jasny / ciemny
- Tryb skanowania i interwał
- Język interfejsu (pl, en, de, fr, es, it, ru, cs, uk)
- Konfiguracja serwerów iperf3
- Alert sygnałowy (próg dBm)
- Wybór adaptera sieciowego

## Wymagania

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Windows**: brak dodatkowych wymagań (korzysta z natywnego WLAN API)
- **Linux**: `iw` lub `iwlist` i `nmcli` — wymagane do skanowania i zarządzania połączeniami
- **Test prędkości**: opcjonalnie `iperf3` zainstalowany w systemie

## Struktura projektu

```
src/
├── AnalizatorWiFi.Core/             # Modele, interfejsy, serwisy niezależne od platformy
├── AnalizatorWiFi.Platform.Windows/ # Implementacja WLAN API (Windows)
├── AnalizatorWiFi.Platform.Linux/   # Implementacja przez powłokę (Linux)
└── AnalizatorWiFi.UI/               # Aplikacja Avalonia UI (MVVM)
```

## Kompilacja

### Szybki start (debug)

```bash
dotnet build src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj
dotnet run --project src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj
```

### Publikacja — Windows (samodzielny .exe)

```bash
dotnet publish src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj ^
  -c Release -r win-x64 --self-contained ^
  -o publish/win-x64
```

### Publikacja — Linux (samodzielny plik wykonywalny)

```bash
dotnet publish src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj \
  -c Release -r linux-x64 --self-contained \
  -o publish/linux-x64
chmod +x publish/linux-x64/AnalizatorWiFi.UI
```

### Publikacja bez osadzania środowiska uruchomieniowego

```bash
dotnet publish src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj \
  -c Release -o publish/portable
```

## Użyte technologie

| Biblioteka | Wersja | Rola |
|---|---|---|
| Avalonia UI | 12.0.4 | Wieloplatformowy interfejs graficzny |
| CommunityToolkit.Mvvm | 8.4.1 | MVVM, komendy, source generators |
| Microsoft.Extensions.Hosting | 9.0.0 | DI, konfiguracja |
| SQLite (via ADO.NET) | — | Baza historii skanowań |
