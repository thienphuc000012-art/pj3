using UnityEngine;

/// <summary>A read-only presentation snapshot. Sampling never grants XP or changes the save.</summary>
public sealed class BattleExperienceChange
{
    public readonly int startLevel, startXP, endLevel, endXP, gained;
    public int LevelsGained => Mathf.Max(0, endLevel - startLevel);

    public BattleExperienceChange(int fromLevel, int fromXP, int toLevel, int toXP)
    {
        startLevel = Mathf.Clamp(fromLevel, 1, 100); startXP = Mathf.Max(0, fromXP);
        endLevel = Mathf.Clamp(toLevel, startLevel, 100); endXP = Mathf.Max(0, toXP);
        gained = Mathf.Max(0, Total(endLevel, endXP) - Total(startLevel, startXP));
    }

    static int Total(int level, int xp)
    {
        int total = xp;
        for (int i = 1; i < level; i++) total += PartyMemberProgress.RequiredXPForLevel(i);
        return total;
    }

    public float Duration(float fillSeconds, float levelHold) => gained == 0 ? 0f :
        Mathf.Max(.01f, fillSeconds) + LevelsGained * Mathf.Max(0f, levelHold);

    public void Sample(float seconds, float fillSeconds, float levelHold, float pulseSeconds,
        out int level, out int xp, out float fill, out float pulse)
    {
        level = startLevel; xp = startXP; fill = (float)xp / PartyMemberProgress.RequiredXPForLevel(level); pulse = 0f;
        if (gained == 0) { level = endLevel; xp = endXP; fill = (float)xp / PartyMemberProgress.RequiredXPForLevel(level); return; }
        float cursor = 0f, lastLevelTime = float.NegativeInfinity;
        float rate = Mathf.Max(.01f, fillSeconds) / gained;
        for (int current = startLevel; current <= endLevel; current++)
        {
            int begin = current == startLevel ? startXP : 0;
            int required = PartyMemberProgress.RequiredXPForLevel(current);
            int target = current == endLevel ? endXP : required;
            float span = Mathf.Max(0, target - begin) * rate;
            level = current;
            if (seconds < cursor + span)
            {
                float value = Mathf.Lerp(begin, target, Mathf.Clamp01((seconds - cursor) / Mathf.Max(.0001f, span)));
                xp = Mathf.FloorToInt(value); fill = value / required;
                pulse = Mathf.Clamp01(1f - (seconds - lastLevelTime) / Mathf.Max(.01f, pulseSeconds));
                return;
            }
            cursor += span; xp = target; fill = (float)target / required;
            if (current == endLevel) break;
            if (seconds < cursor + Mathf.Max(0, levelHold)) return; // Show a full bar before resetting it.
            cursor += Mathf.Max(0, levelHold); lastLevelTime = cursor;
        }
        pulse = Mathf.Clamp01(1f - (seconds - lastLevelTime) / Mathf.Max(.01f, pulseSeconds));
    }
}
