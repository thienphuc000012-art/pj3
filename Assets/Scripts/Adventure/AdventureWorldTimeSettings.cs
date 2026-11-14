using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/World Time Settings")]
public sealed class AdventureWorldTimeSettings : ScriptableObject
{
    [Min(1f), Tooltip("Real minutes for one complete 24-hour game day.")]
    public float dayLengthMinutes = 24f;
    [Range(0f, 23.99f), Tooltip("Starting hour for new games and saves created before the clock existed.")]
    public float startingHour = 8f;
    public bool advanceTime = true;
}

public static class AdventureWorldTime
{
    static AdventureWorldTimeSettings settings;
    public static AdventureWorldTimeSettings Settings => settings != null ? settings : settings = Resources.Load<AdventureWorldTimeSettings>("Adventure/WorldTimeSettings");

    public static double Read(CampaignSave data)
    {
        if (data == null || !data.hasWorldTime || double.IsNaN(data.worldTimeHours) || double.IsInfinity(data.worldTimeHours) || data.worldTimeHours < 0)
            return Settings != null ? Settings.startingHour : 8;
        return data.worldTimeHours;
    }
    public static void Advance(CampaignSave data, float seconds)
    {
        if (data == null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        data.worldTimeHours = Read(data);
        data.hasWorldTime = true;
        if (Settings == null || Settings.advanceTime)
            data.worldTimeHours += seconds * 24d / (Math.Max(1, Settings != null ? Settings.dayLengthMinutes : 24) * 60d);
    }
    public static string Format(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0) hours = 8;
        int minutes = (int)Math.Floor(hours % 24 * 60 + .000001);
        return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
    }
}
