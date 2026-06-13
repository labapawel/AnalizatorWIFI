namespace AnalizatorWiFi.Core.Models;

public sealed record GeoLocationResult(
    string Country,
    string CountryCode,
    double Lat,
    double Lon);
