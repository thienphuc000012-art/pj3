using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PartyHUDPortraitPair
{
    public string characterKey;
    public Sprite waiting;
    public Sprite acting;
}

[CreateAssetMenu(menuName = "Combat/Party HUD Style")]
public sealed class PartyHUDStyle : ScriptableObject
{
    public Sprite barFrame;
    public List<PartyHUDPortraitPair> portraits = new List<PartyHUDPortraitPair>();
    [Header("Layout defaults (apply with Setup Party Turn HUD)")]
    public Vector2 slotSize = new Vector2(210f, 220f);
    [Min(0)] public float slotSpacing = 44f;
    [Header("Turn animation")]
    [Range(1f, 1.5f)] public float activeScale = 1.22f;
    [Min(.01f)] public float transitionDuration = .18f;
    public float activeLift = 8f;
    [Header("Colors")]
    public Color hpColor = new Color(1f, .08f, .18f);
    public Color ultimateColor = new Color(0f, .94f, 1f);

    public Sprite PortraitFor(BattleUnit unit, bool acting)
    {
        if (unit == null) return null;
        Sprite custom = acting ? unit.partyHudTurnPortrait : unit.partyHudWaitingPortrait;
        if (custom != null) return custom;
        if (acting && unit.partyHudWaitingPortrait != null) return unit.partyHudWaitingPortrait;
        string objectKey = Normalize(unit.gameObject.name);
        string displayKey = Normalize(unit.unitName);
        foreach (var pair in portraits)
        {
            if (pair == null || string.IsNullOrWhiteSpace(pair.characterKey)) continue;
            string key = Normalize(pair.characterKey);
            if (key != objectKey && key != displayKey) continue;
            return acting && pair.acting != null ? pair.acting : pair.waiting;
        }
        return unit.unitPortrait;
    }

    // Prefab instances lose their scene suffix; do not bind art to the display name alone.
    static string Normalize(string value)
    {
        string key = (value ?? "").Replace("(Clone)", "").Trim();
        int suffix = key.LastIndexOf('(');
        if (suffix >= 0 && key.EndsWith(")") && int.TryParse(key.Substring(suffix + 1, key.Length - suffix - 2), out _))
            key = key.Substring(0, suffix).TrimEnd();
        return key.ToLowerInvariant();
    }
}
