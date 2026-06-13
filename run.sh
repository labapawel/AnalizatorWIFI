#!/usr/bin/env bash
# Uruchamia / publikuje AnalizatorWiFi.
# Użycie:
#   ./run.sh                        # dotnet run (development, auto-wykryj OS)
#   ./run.sh --publish              # publish self-contained (auto-wykryj OS) i uruchom
#   ./run.sh --publish --windows    # publish win-x64
#   ./run.sh --publish --linux      # publish linux-x64
#   ./run.sh --build                # wymusza Release przy dotnet run

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/src/AnalizatorWiFi.UI/AnalizatorWiFi.UI.csproj"

MODE="run"
CONFIG="Debug"
TARGET_OS=""   # windows | linux | (pusty = auto)

for arg in "$@"; do
    case $arg in
        --publish) MODE="publish" ;;
        --build)   CONFIG="Release" ;;
        --windows) TARGET_OS="windows" ;;
        --linux)   TARGET_OS="linux" ;;
        *) echo "Nieznany argument: $arg"; exit 1 ;;
    esac
done

# --- Auto-wykryj OS jeśli nie podano ---
if [[ -z "$TARGET_OS" ]]; then
    case "$(uname -s)" in
        Linux*)  TARGET_OS="linux" ;;
        *)       TARGET_OS="windows" ;;
    esac
fi

if [[ "$TARGET_OS" == "windows" ]]; then
    RID="win-x64"
    PUBLISH_DIR="$SCRIPT_DIR/publish/win-x64"
    EXE_NAME="AnalizatorWiFi.UI.exe"
else
    RID="linux-x64"
    PUBLISH_DIR="$SCRIPT_DIR/publish/linux-x64"
    EXE_NAME="AnalizatorWiFi.UI"
fi

# --- Check .NET 9 ---
if ! command -v dotnet &>/dev/null; then
    echo "BŁĄD: Nie znaleziono 'dotnet'. Zainstaluj .NET 9 SDK:"
    echo "  https://dotnet.microsoft.com/download"
    exit 1
fi

SDK_VER=$(dotnet --version 2>&1)
if [[ "$SDK_VER" != 9.* ]]; then
    echo "OSTRZEŻENIE: Aktywna wersja SDK: $SDK_VER. Wymagany .NET 9."
fi

# --- Sprawdź nmcli (wymagane na Linux) ---
if [[ "$TARGET_OS" == "linux" ]] && ! command -v nmcli &>/dev/null; then
    echo "OSTRZEŻENIE: 'nmcli' nie znaleziony. Zainstaluj: sudo apt install network-manager"
fi

# --- Restore ---
echo "Przywracanie pakietów..."
dotnet restore "$PROJECT"

if [[ "$MODE" == "publish" ]]; then
    echo "Publikowanie ($RID, self-contained)..."
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$PUBLISH_DIR"

    EXE="$PUBLISH_DIR/$EXE_NAME"
    [[ "$TARGET_OS" == "linux" ]] && chmod +x "$EXE"
    echo "Opublikowano: $EXE"

    # Uruchom tylko jeśli bieżący OS odpowiada targetowi
    CURRENT_OS="linux"
    [[ "$(uname -s)" != "Linux" ]] && CURRENT_OS="windows"

    if [[ "$CURRENT_OS" != "$TARGET_OS" ]]; then
        echo "INFORMACJA: Publikowanie zakończone. Uruchom plik na docelowym systemie."
        exit 0
    fi

    echo "Uruchamianie..."
    "$EXE"
else
    echo "Uruchamianie ($CONFIG)..."
    dotnet run --project "$PROJECT" -c "$CONFIG"
fi
