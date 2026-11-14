using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Canvas-authored result styling and unscaled, presentation-only reward animation.</summary>
[DisallowMultipleComponent]
public sealed class BattleResultPresentation : MonoBehaviour
{
    [Header("Win / Lose Layout")]
    public GameObject[] victoryContent;
    public GameObject diamondPattern;
    public GameObject defeatPanel;
    public RawImage defeatBackdrop;
    public Button returnButton;
    [Header("Count-Up Animation")]
    [Min(.1f)] public float statisticsSeconds = 1.1f;
    [Min(.1f)] public float experienceSeconds = 1.8f;
    [Min(0f)] public float memberStaggerSeconds = .12f;
    [Min(0f)] public float fullBarHoldSeconds = .2f;
    [Min(.1f)] public float levelUpPulseSeconds = .7f;
    public Color levelUpColor = new Color(1f, .82f, .42f);
    [Header("Defeat Background")]
    public bool captureCombatBackground = true;

    sealed class Member
    {
        public TMP_Text progress, levelUp;
        public Image fill;
        public RectTransform portrait;
        public BattleExperienceChange experience;
        public Vector3 scale;
        public Color fillColor;
    }
    readonly List<Member> members = new List<Member>();
    readonly List<(TMP_Text label, int count)> loot = new List<(TMP_Text, int)>();
    TMP_Text[] numbers;
    int[] totals;
    TMP_Text experienceLabel;
    int experienceTotal;
    float elapsed, endTime;
    bool running;
    Texture2D screenshot;
    Coroutine capture;
    float captureDeadline;
    public bool IsAnimating => running;

    public void Configure(bool victory)
    {
        running = false;
        if (victoryContent != null) foreach (var node in victoryContent) if (node != null) node.SetActive(victory);
        if (diamondPattern != null) diamondPattern.SetActive(victory);
        if (defeatPanel != null) defeatPanel.SetActive(!victory);
        if (capture != null) { StopCoroutine(capture); capture = null; }
        ReleaseScreenshot();
        if (defeatBackdrop != null) defeatBackdrop.color = new Color(.12f, .12f, .12f, 1f);
        if (!victory && captureCombatBackground && Application.isPlaying && isActiveAndEnabled)
        {
            captureDeadline = Time.unscaledTime + 1f;
            capture = StartCoroutine(CaptureDefeat());
        }
    }

    public void Begin(AdventureCanvasRoot view, int[] statistics, int xp,
        IList<GameObject> rows, IReadOnlyList<BattleExperienceChange> changes, IList<GameObject> lootRows, IList<ItemStack> rewards)
    {
        members.Clear(); loot.Clear(); elapsed = 0f;
        totals = (int[])statistics.Clone(); experienceTotal = Mathf.Max(0, xp);
        string[] paths = { "Stats/Damage", "Stats/Highest", "Stats/Received", "Stats/Parries", "Stats/Kills", "Stats/Time" };
        numbers = new TMP_Text[paths.Length];
        for (int i = 0; i < paths.Length; i++) numbers[i] = view.Component<TMP_Text>(paths[i]);
        experienceLabel = view.Component<TMP_Text>("Experience");
        endTime = Mathf.Max(statisticsSeconds, experienceSeconds);
        for (int i = 0; i < rows.Count && changes != null && i < changes.Count; i++)
        {
            var row = rows[i].transform;
            var member = new Member
            {
                progress = row.Find("Progress")?.GetComponent<TMP_Text>(),
                levelUp = row.Find("Level Up")?.GetComponent<TMP_Text>(),
                fill = row.Find("XP/Fill")?.GetComponent<Image>(),
                portrait = row.Find("Portrait") as RectTransform,
                experience = changes[i]
            };
            member.scale = member.portrait != null ? member.portrait.localScale : Vector3.one;
            member.fillColor = member.fill != null ? member.fill.color : levelUpColor;
            members.Add(member);
            endTime = Mathf.Max(endTime, i * memberStaggerSeconds + changes[i].Duration(experienceSeconds, fullBarHoldSeconds) +
                (changes[i].LevelsGained > 0 ? levelUpPulseSeconds : 0f));
        }
        for (int i = 0; lootRows != null && rewards != null && i < lootRows.Count && i < rewards.Count; i++)
            loot.Add((lootRows[i].transform.Find("Count")?.GetComponent<TMP_Text>(), rewards[i].count));
        running = true;
        Sample(0f);
    }

    void Update()
    {
        // In the Editor, end-of-frame can wait while Game View is hidden. Never leave the result invisible.
        if (capture != null && Time.unscaledTime >= captureDeadline)
        {
            StopCoroutine(capture); capture = null;
            var group = GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1f;
        }
        if (!running) return;
        elapsed += Time.unscaledDeltaTime;
        Sample(elapsed);
    }

    public void Complete() { Sample(endTime + 1f); }

    // Public to support native Edit Mode previews without entering a campaign or writing a save.
    public void Sample(float seconds)
    {
        if (numbers == null) return;
        elapsed = Mathf.Max(0f, seconds);
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, statisticsSeconds));
        float count = 1f - Mathf.Pow(1f - t, 3f);
        for (int i = 0; i < numbers.Length; i++)
        {
            int value = Mathf.RoundToInt(totals[i] * count);
            Write(numbers[i], i == 5 ? (value / 60) + ":" + (value % 60).ToString("00") : value.ToString("N0"));
        }
        int awarded = Mathf.RoundToInt(experienceTotal * Mathf.Clamp01(elapsed / Mathf.Max(.1f, experienceSeconds)));
        Write(experienceLabel, awarded.ToString("N0") + (GameLocalization.IsEnglish ? " XP" : " KN"));
        foreach (var item in loot) Write(item.label, "×" + Mathf.RoundToInt(item.count * count));
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            member.experience.Sample(elapsed - i * memberStaggerSeconds, experienceSeconds, fullBarHoldSeconds,
                levelUpPulseSeconds, out int level, out int xp, out float fill, out float pulse);
            Write(member.progress, (GameLocalization.IsEnglish ? "LEVEL " : "CẤP ") + level + " • " + xp + " / " +
                PartyMemberProgress.RequiredXPForLevel(level) + (GameLocalization.IsEnglish ? " XP" : " KN"));
            if (member.fill != null) { member.fill.fillAmount = Mathf.Clamp01(fill); member.fill.color = Color.Lerp(member.fillColor, Color.white, pulse); }
            bool leveled = level > member.experience.startLevel;
            if (member.levelUp != null)
            {
                member.levelUp.gameObject.SetActive(leveled);
                Write(member.levelUp, GameLocalization.IsEnglish ? "LEVEL UP!" : "LÊN CẤP!");
                member.levelUp.color = Color.Lerp(levelUpColor, Color.white, pulse);
                member.levelUp.rectTransform.localScale = Vector3.one * (1f + .15f * pulse);
            }
            if (member.portrait != null) member.portrait.localScale = member.scale * (1f + .065f * pulse);
        }
        running = elapsed < endTime;
    }

    static void Write(TMP_Text target, string value)
    {
        if (target != null && target.text != value) GameLocalization.SetText(target, value);
    }

    IEnumerator CaptureDefeat()
    {
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        float previous = group.alpha; group.alpha = 0f;
        try
        {
            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (defeatBackdrop != null) { defeatBackdrop.texture = screenshot; defeatBackdrop.color = Color.white; }
        }
        finally { if (group != null) group.alpha = previous; capture = null; }
    }

    void OnEnable() { GameLocalization.LanguageChanged += RefreshLanguage; }
    void RefreshLanguage() { if (numbers != null) Sample(elapsed); }
    void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshLanguage;
        if (capture != null)
        {
            StopCoroutine(capture); capture = null;
            var group = GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1f;
        }
        running = false; ReleaseScreenshot();
    }
    void ReleaseScreenshot()
    {
        if (defeatBackdrop != null) defeatBackdrop.texture = null;
        if (screenshot != null) { if (Application.isPlaying) Destroy(screenshot); else DestroyImmediate(screenshot); }
        screenshot = null;
    }
}
