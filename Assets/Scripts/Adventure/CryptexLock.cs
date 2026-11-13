using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class CryptexLetterRing
{
    public Transform pivot;
    public Vector3 axis = Vector3.forward;
    public Vector3 letterAEuler;
    public float degreesPerLetter = 360f / 26f;
}

/// <summary>Seven physical A-Z rings. Uses the existing adventure interaction and save system.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(WorldInteraction))]
public sealed class CryptexLock : MonoBehaviour
{
    [Header("Password")]
    [Tooltip("Model có 7 vòng. Nhập đúng 7 chữ A-Z, không dấu. Ví dụ ANCIENT. Không phải mật khẩu tài khoản.")]
    public string password = "ANCIENT";
    [Tooltip("Chữ ban đầu trên 7 vòng. Không tự đổi khi đóng/mở bảng tương tác.")]
    public string initialWord = "AAAAAAA";
    public CryptexLetterRing[] rings = Array.Empty<CryptexLetterRing>();
    [Header("Presentation")]
    public Transform modelRoot;
    public Transform slidingKey;
    public Vector3 unlockedKeyOffset = new Vector3(0, 0, .13f);
    [Min(.05f)] public float ringTurnSeconds = .14f;
    [Min(.1f)] public float unlockSeconds = .65f;
    public CryptexPuzzleView viewPrefab;
    [Header("Unlock actions")]
    [Tooltip("Gọi khi giải đúng và khi khôi phục bản lưu đã mở. Có thể gán Door.SetActive(false), v.v.")]
    public UnityEvent onUnlocked = new UnityEvent();
    [Tooltip("Khôi phục cửa/đối tượng khi tải bản lưu chưa mở khóa.")]
    public UnityEvent onLocked = new UnityEvent();
    public bool IsUnlocked { get; private set; }
    public string CurrentWord => new string(values.Select(v => (char)('A' + v)).ToArray());
    public bool IsTurning => turns != null && turns.Any(t => t != null);
    public bool PasswordValid => rings != null && rings.Length > 0 && rings.All(r => r != null && r.pivot != null) &&
        !string.IsNullOrEmpty(password) && password.Length == rings.Length && password.All(c => c >= 'A' && c <= 'Z');
    int[] values = Array.Empty<int>();
    Coroutine[] turns;
    Coroutine unlocking;
    Vector3 keyClosed;
    bool initialized, savedStateApplied;
    WorldInteraction point;
    CryptexPuzzleView view;

    void OnValidate() { password = (password ?? "").Trim().ToUpperInvariant(); initialWord = (initialWord ?? "").Trim().ToUpperInvariant(); }
    void Awake() => Initialize();
    public void Initialize()
    {
        if (initialized) return;
        initialized = true; point = GetComponent<WorldInteraction>();
        initialWord ??= "";
        rings ??= Array.Empty<CryptexLetterRing>();
        values = new int[rings.Length]; turns = new Coroutine[rings.Length];
        if (slidingKey != null) keyClosed = slidingKey.localPosition;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i < initialWord.Length && initialWord[i] >= 'A' && initialWord[i] <= 'Z' ? initialWord[i] - 'A' : 0;
            SetRingPose(i);
        }
    }
    public void RestoreSavedState(bool unlocked)
    {
        Initialize();
        if (savedStateApplied && IsUnlocked == unlocked) return;
        savedStateApplied = true; StopMotion(); IsUnlocked = unlocked;
        if (unlocked && PasswordValid)
            for (int i = 0; i < values.Length; i++) { values[i] = password[i] - 'A'; SetRingPose(i); }
        else if (!unlocked)
            for (int i = 0; i < values.Length; i++)
            { values[i] = i < initialWord.Length && initialWord[i] >= 'A' && initialWord[i] <= 'Z' ? initialWord[i] - 'A' : 0; SetRingPose(i); }
        if (slidingKey != null) slidingKey.localPosition = keyClosed + (unlocked ? unlockedKeyOffset : Vector3.zero);
        if (unlocked) onUnlocked.Invoke(); else onLocked.Invoke();
    }
    public void Interact(CampaignSession session)
    {
        Initialize();
        if (IsUnlocked || view != null || session == null || session.Busy || session.Menu != AdventureMenu.None || !session.OnMap || !session.IsNear(point)) return;
        if (!PasswordValid || viewPrefab == null || modelRoot == null)
        { session.Notify(GameLocalization.IsEnglish ? "Cryptex is not configured. Check its password and rings." : "Ổ khóa chưa được cấu hình. Kiểm tra Password và các vòng chữ."); return; }
        session.SetMenu(AdventureMenu.Cryptex);
        if (session.Menu != AdventureMenu.Cryptex) return;
        try
        {
            view = Instantiate(viewPrefab);
            SceneManager.MoveGameObjectToScene(view.gameObject, gameObject.scene);
            view.Open(this, session);
        }
        catch { Close(); throw; }
    }
    public char LetterAt(int index) { Initialize(); return (char)('A' + values[index]); }
    public void RotateRing(int index, int direction)
    {
        Initialize();
        if (IsUnlocked || unlocking != null || index < 0 || index >= values.Length || direction == 0) return;
        if (turns[index] != null) StopCoroutine(turns[index]);
        values[index] = (values[index] + (direction > 0 ? 1 : 25)) % 26;
        if (Application.isPlaying) turns[index] = StartCoroutine(Turn(index)); else SetRingPose(index);
    }
    Quaternion Pose(int i) => Quaternion.Euler(rings[i].letterAEuler) * Quaternion.AngleAxis(values[i] * rings[i].degreesPerLetter, rings[i].axis.normalized);
    void SetRingPose(int i) { if (rings[i]?.pivot != null) rings[i].pivot.localRotation = Pose(i); }
    IEnumerator Turn(int i)
    {
        var pivot = rings[i].pivot; Quaternion from = pivot.localRotation, to = Pose(i);
        float elapsed = 0;
        while (elapsed < ringTurnSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            pivot.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, ringTurnSeconds)));
            yield return null;
        }
        pivot.localRotation = to; turns[i] = null;
    }
    public bool CheckCombination() { Initialize(); return PasswordValid && !IsTurning && string.Equals(CurrentWord, password, StringComparison.Ordinal); }
    public bool TryUnlock(CampaignSession session)
    {
        if (session == null || session.Data == null || session.Menu != AdventureMenu.Cryptex || view == null || IsUnlocked || unlocking != null || !CheckCombination()) return false;
        IsUnlocked = true; savedStateApplied = true;
        // Store the solved flag before invoking external door/reward actions.
        if (!session.Data.collected.Contains(point.Id))
        {
            session.Data.collected.Add(point.Id);
            if (point.rewards != null) foreach (var reward in point.rewards) if (reward != null) session.AddItem(reward.id, reward.count);
            session.GainXP(point.experience);
            session.Save();
        }
        onUnlocked.Invoke();
        if (isActiveAndEnabled) unlocking = StartCoroutine(OpenKey());
        return true;
    }
    IEnumerator OpenKey()
    {
        float elapsed = 0;
        while (elapsed < unlockSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            if (slidingKey != null) slidingKey.localPosition = keyClosed + unlockedKeyOffset * Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, unlockSeconds));
            yield return null;
        }
        unlocking = null; Close();
    }
    public void Close()
    {
        StopMotion();
        if (slidingKey != null && IsUnlocked) slidingKey.localPosition = keyClosed + unlockedKeyOffset;
        if (view != null) { var old = view; view = null; old.Close(); }
        var session = CampaignSession.Instance;
        if (session != null) session.CloseCryptexMenu();
    }
    void StopMotion()
    {
        if (turns != null) for (int i = 0; i < turns.Length; i++)
        { if (turns[i] != null) StopCoroutine(turns[i]); turns[i] = null; SetRingPose(i); }
        if (unlocking != null) StopCoroutine(unlocking); unlocking = null;
    }
    void OnDisable() { if (initialized) Close(); }
}
