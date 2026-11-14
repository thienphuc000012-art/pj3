using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The authored exploration clock is shared by m and mapgame; it never consumes input.</summary>
[DisallowMultipleComponent]
public sealed class AdventureTimeHUD : MonoBehaviour
{
    public TMP_Text timeLabel;
    void Awake()
    {
        if (timeLabel == null) timeLabel = transform.Find("HUD/World Time/Time")?.GetComponent<TMP_Text>();
    }
    void LateUpdate()
    {
        if (timeLabel == null) return;
        string scene = SceneManager.GetActiveScene().name;
        var session = CampaignSession.Instance;
        bool visible = (scene == "m" || scene == "mapgame") && session != null && session.OnMap && session.MapPlayer != null;
        if (timeLabel.transform.parent.gameObject.activeSelf != visible) timeLabel.transform.parent.gameObject.SetActive(visible);
        if (visible)
        {
            string value = AdventureWorldTime.Format(AdventureWorldTime.Read(session.Data));
            if (timeLabel.text != value) timeLabel.SetText(value);
        }
    }
}
