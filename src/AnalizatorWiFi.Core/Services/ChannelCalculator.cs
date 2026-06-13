using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Services;

public static class ChannelCalculator
{
    public static WifiBand GetBand(double frequencyMhz) => frequencyMhz switch
    {
        >= 2400 and < 2500 => WifiBand.GHz2_4,
        >= 5000 and < 5900 => WifiBand.GHz5,
        >= 5925 and < 7125 => WifiBand.GHz6,
        _ => WifiBand.Unknown
    };

    public static int FrequencyToChannel(double frequencyMhz)
    {
        return (int)frequencyMhz switch
        {
            >= 2412 and <= 2484 => frequencyMhz == 2484 ? 14 : (int)((frequencyMhz - 2412) / 5) + 1,
            >= 5180 and <= 5825 => (int)((frequencyMhz - 5000) / 5),
            >= 5955 and <= 7115 => (int)((frequencyMhz - 5955) / 5) + 1,
            _ => 0
        };
    }

    public static double ChannelToFrequency(string bandLabel, int channel)
    {
        if (channel <= 0) return 0;
        if (bandLabel.Contains("2.4")) return channel == 14 ? 2484 : 2407 + channel * 5;
        if (bandLabel.Contains("5"))   return 5000 + channel * 5;
        if (bandLabel.Contains("6"))   return 5950 + channel * 5;
        // Fallback: guess band from channel number
        if (channel <= 14)  return channel == 14 ? 2484 : 2407 + channel * 5;
        if (channel <= 177) return 5000 + channel * 5;
        return 5950 + channel * 5;
    }

    // Returns (startMhz, endMhz) of a channel given its center frequency and width
    public static (double Start, double End) GetChannelRange(double centerMhz, int widthMhz)
    {
        double half = widthMhz / 2.0;
        return (centerMhz - half, centerMhz + half);
    }

    // Checks if two channels overlap
    public static bool DoChannelsOverlap(double center1, int width1, double center2, int width2)
    {
        var (s1, e1) = GetChannelRange(center1, width1);
        var (s2, e2) = GetChannelRange(center2, width2);
        return s1 < e2 && s2 < e1;
    }
}
