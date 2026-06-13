using System.Runtime.InteropServices;

namespace AnalizatorWiFi.Platform.Windows.WlanApi;

internal static class NativeMethods
{
    internal const uint WLAN_CLIENT_VERSION_VISTA = 2;
    internal const uint ERROR_SUCCESS = 0;

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanGetNetworkBssList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, uint dot11BssType, bool bSecurityEnabled, IntPtr pReserved, out IntPtr ppWlanBssList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanConnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, ref WlanConnectionParameters pConnectionParameters, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanDisconnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, WlanIntfOpcode opCode, IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, out WlanOpcodeValueType pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    internal static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("wlanapi.dll", SetLastError = true)]
    internal static extern uint WlanSetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string strProfileXml, [MarshalAs(UnmanagedType.LPWStr)] string? strAllUserProfileSecurity, bool bOverwrite, IntPtr pReserved, out uint pdwReasonCode);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WlanInterfaceInfo
{
    public Guid InterfaceGuid;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string strInterfaceDescription;
    public WlanInterfaceState isState;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanInterfaceInfoList
{
    public uint dwNumberOfItems;
    public uint dwIndex;
    public WlanInterfaceInfo[] InterfaceInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WlanAvailableNetwork
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string strProfileName;
    public Dot11Ssid dot11Ssid;
    public Dot11BssType dot11BssType;
    public uint uNumberOfBssids;
    public bool bNetworkConnectable;
    public uint wlanNotConnectableReason;
    public uint uNumberOfPhyTypes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public Dot11PhyType[] dot11PhyTypes;
    public bool bMorePhyTypes;
    public uint wlanSignalQuality;  // 0-100
    public bool bSecurityEnabled;
    public Dot11AuthAlgorithm dot11DefaultAuthAlgorithm;
    public Dot11CipherAlgorithm dot11DefaultCipherAlgorithm;
    public uint dwFlags;
    public uint dwReserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanAvailableNetworkList
{
    public uint dwNumberOfItems;
    public uint dwIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanBssEntry
{
    public Dot11Ssid dot11Ssid;
    public uint uPhyId;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public byte[] dot11Bssid;
    public Dot11BssType dot11BssType;
    public Dot11PhyType dot11BssPhyType;
    public int lRssi;               // dBm
    public uint uLinkQuality;
    public bool bInRegDomain;
    public ushort usBeaconPeriod;
    public ulong ullTimestamp;
    public ulong ullHostTimestamp;
    public ushort usCapabilityInformation;
    public uint ulChCenterFrequency;  // kHz
    public WlanRateSet wlanRateSet;
    public uint ulIeOffset;
    public uint ulIeSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanRateSet
{
    public uint uRateSetLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
    public ushort[] usRateSet;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanBssList
{
    public uint dwTotalSize;
    public uint dwNumberOfItems;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Dot11Ssid
{
    public uint uSSIDLength;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] ucSSID;

    public override string ToString()
    {
        if (uSSIDLength == 0) return string.Empty;
        return System.Text.Encoding.UTF8.GetString(ucSSID, 0, (int)uSSIDLength);
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WlanConnectionParameters
{
    public WlanConnectionMode wlanConnectionMode;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string strProfile;
    public IntPtr pDot11Ssid;
    public IntPtr pDesiredBssidList;
    public Dot11BssType dot11BssType;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanConnectionAttributes
{
    public WlanInterfaceState isState;
    public WlanConnectionMode wlanConnectionMode;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string strProfileName;
    public WlanAssociationAttributes wlanAssociationAttributes;
    public WlanSecurityAttributes wlanSecurityAttributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanAssociationAttributes
{
    public Dot11Ssid dot11Ssid;
    public Dot11BssType dot11BssType;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public byte[] dot11Bssid;
    public Dot11PhyType dot11PhyType;
    public uint uDot11PhyIndex;
    public uint wlanSignalQuality;
    public uint ulRxRate;
    public uint ulTxRate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlanSecurityAttributes
{
    public bool bSecurityEnabled;
    public bool bOneXEnabled;
    public Dot11AuthAlgorithm dot11AuthAlgorithm;
    public Dot11CipherAlgorithm dot11CipherAlgorithm;
}

internal enum WlanInterfaceState
{
    NotReady,
    Connected,
    AdHocNetworkFormed,
    Disconnecting,
    Disconnected,
    Associating,
    Discovering,
    Authenticating
}

internal enum WlanConnectionMode
{
    Profile,
    TemporaryProfile,
    DiscoverySecure,
    DiscoveryUnsecure,
    Auto,
    Invalid
}

internal enum Dot11BssType { Infrastructure = 1, Independent, Any }
internal enum Dot11PhyType { Unknown, Any, Fhss, Dsss, IrBaseband, Ofdm, HrDsss, Erp, Ht, Vht, He, Eht = 12 }
internal enum Dot11AuthAlgorithm { Open80211 = 1, SharedKey80211, Wpa, WpaPsk, WpaNone, Rsna, RsnaPsk, Wpa3, Wpa3Sae = 9 }
internal enum Dot11CipherAlgorithm { None, Wep40, Tkip = 4, Ccmp = 8, Wep104 = 0x109, Wpa, RsnUseGroup, Wep = 0x101, Gcmp256 = 0x105 }
internal enum WlanIntfOpcode { AutoconfEnabled = 1, BackgroundScanEnabled, MediaStreamingMode, RadioState, BssType, InterfaceState, CurrentConnection = 7, ChannelNumber, SupportedInfrastructureAuthCipherPairs, SupportedAdhocAuthCipherPairs, SupportedCountryOrRegionStringList, CurrentOperationMode, SupportedSafeMode, CertifiedSafeMode, HostedNetworkCapable, ManagementFrameProtectionCapable, SecondaryStaBssEntry, AutoPowerSaveRxFailureCount }
internal enum WlanOpcodeValueType { QueryOnly, SetByGroupPolicy, SetByUser, Invalid }
