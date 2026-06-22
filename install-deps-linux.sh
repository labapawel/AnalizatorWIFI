#!/usr/bin/env bash
# install-deps-linux.sh
# Instaluje pakiety systemowe wymagane do uruchomienia AnalizatorWiFi na Linuksie.
#
# Użycie:
#   sudo ./install-deps-linux.sh
#
# Obsługiwane dystrybucje: Debian / Ubuntu, Fedora / RHEL / CentOS, Arch Linux

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
info()  { echo -e "${GREEN}[INFO]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error() { echo -e "${RED}[BŁĄD]${NC}  $*"; }

# ---------- Sprawdzenie roota ----------
if [[ $EUID -ne 0 ]]; then
    error "Skrypt wymaga uprawnień roota."
    echo  "Uruchom:  sudo $0"
    exit 1
fi

# ---------- Wykrywanie dystrybucji ----------
detect_distro() {
    if [[ -f /etc/os-release ]]; then
        # shellcheck source=/dev/null
        . /etc/os-release
        echo "${ID_LIKE:-$ID}"
    elif command -v apt-get &>/dev/null; then echo "debian"
    elif command -v dnf     &>/dev/null; then echo "fedora"
    elif command -v pacman  &>/dev/null; then echo "arch"
    else echo "unknown"
    fi
}

DISTRO_LIKE="$(detect_distro)"

# ---------- Listy pakietów ----------

# Debian / Ubuntu
PKGS_DEBIAN=(
    # WiFi i sieć
    network-manager        # nmcli — skanowanie i zarządzanie WiFi
    iperf3                 # test prędkości
    iputils-ping           # ping

    # Avalonia — renderowanie X11
    libx11-6
    libx11-xcb1
    libxext6
    libxrandr2
    libxcursor1
    libxi6
    libxcomposite1
    libice6
    libsm6

    # Avalonia — czcionki, OpenGL, Wayland
    libfontconfig1
    libpangocairo-1.0-0
    libglib2.0-0
    libgl1
    libgbm1
    libwayland-client0
    libwayland-egl1
)

# Fedora / RHEL / CentOS
PKGS_FEDORA=(
    NetworkManager
    iperf3
    iputils
    libX11
    libX11-xcb
    libXext
    libXrandr
    libXcursor
    libXi
    libXcomposite
    libICE
    libSM
    fontconfig
    pango
    glib2
    mesa-libGL
    mesa-libgbm
    wayland-devel
)

# Arch Linux
PKGS_ARCH=(
    networkmanager
    iperf3
    iputils
    libx11
    libxext
    libxrandr
    libxcursor
    libxi
    libxcomposite
    libice
    libsm
    fontconfig
    pango
    glib2
    mesa
    libwayland
)

# ---------- Instalacja ----------
case "$DISTRO_LIKE" in
    *debian*|*ubuntu*)
        info "Wykryto system oparty na Debianie/Ubuntu"
        apt-get update -q
        DEBIAN_FRONTEND=noninteractive apt-get install -y "${PKGS_DEBIAN[@]}"
        ;;
    *fedora*|*rhel*|*centos*|*ol*)
        info "Wykryto system oparty na Fedorze/RHEL"
        dnf install -y "${PKGS_FEDORA[@]}"
        ;;
    *arch*)
        info "Wykryto Arch Linux"
        pacman -Sy --noconfirm "${PKGS_ARCH[@]}"
        ;;
    *)
        warn "Nierozpoznana dystrybucja (ID_LIKE='$DISTRO_LIKE')."
        warn "Zainstaluj ręcznie: network-manager iperf3 iputils-ping"
        warn "oraz biblioteki X11 / OpenGL / FontConfig wymagane przez Avalonia UI."
        exit 1
        ;;
esac

# ---------- Włączenie NetworkManager ----------
if command -v systemctl &>/dev/null; then
    if systemctl is-enabled --quiet NetworkManager 2>/dev/null; then
        if ! systemctl is-active --quiet NetworkManager 2>/dev/null; then
            info "Uruchamianie NetworkManager..."
            systemctl start NetworkManager
        else
            info "NetworkManager już działa."
        fi
    else
        info "Włączanie i uruchamianie NetworkManager..."
        systemctl enable --now NetworkManager
    fi
fi

# ---------- Weryfikacja ----------
echo ""
info "=== Weryfikacja zainstalowanych narzędzi ==="

check_cmd() {
    local cmd="$1"
    if command -v "$cmd" &>/dev/null; then
        info "  ✓ $cmd → $(command -v "$cmd")"
    else
        warn "  ✗ $cmd — nie znaleziono (sprawdź powyższe błędy)"
    fi
}

check_cmd nmcli
check_cmd iperf3
check_cmd ping

echo ""
info "Gotowe. Uruchom aplikację poleceniem:  ./run.sh"
