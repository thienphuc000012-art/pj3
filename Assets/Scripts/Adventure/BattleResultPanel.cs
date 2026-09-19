using System.Linq;
using TMPro;
using UnityEngine;

public class BattleResultPanel : MonoBehaviour
{
    CombatManager manager;
    AdventureCanvasRoot view;
    bool visible, victory, leaving;
    float started, duration, oldTimeScale;
    int dealt, received, highest, parries;
    void Awake()
    {
        manager = GetComponent<CombatManager>(); started = Time.unscaledTime;
        view = AdventureCanvasRoot.Acquire(AdventureCanvasKind.BattleResult);
        view.Click("Continue", Continue); view.gameObject.SetActive(false);
    }
    public void RecordDamage(bool fromPlayer, int damage)
    {
        if (visible || damage <= 0) return;
        if (fromPlayer) { dealt += damage; highest = Mathf.Max(highest, damage); } else received += damage;
    }
    public void RecordParry() { if (!visible) parries++; }
    public void Show(bool won)
    {
        if (visible) return;
        victory = won; visible = true; duration = Time.unscaledTime - started;
        oldTimeScale = Time.timeScale; Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        view.gameObject.SetActive(true); AdventureCanvasRoot.EnsureEventSystem();
        Populate();
    }
    void Update() { if (visible && !leaving && Input.GetKeyDown(KeyCode.Return)) Continue(); }
    void Continue()
    {
        if (!visible || leaving) return;
        var session = CampaignSession.Instance;
        bool accepted = session != null && session.InEncounter ? session.ContinueAfterBattle() : LoadingScreen.Load(Resources.Load<CampaignConfig>("Adventure/CampaignConfig")?.mapScene ?? "mapgame");
        if (accepted)
        {
            leaving = true; Time.timeScale = oldTimeScale > 0 ? oldTimeScale : 1;
            view.Enabled("Continue", false); view.Text("Continue/Label", "Đang chuyển cảnh…");
        }
        else view.Text("Error", LoadingScreen.Error ?? "Chưa thể chuyển cảnh. Hãy thử lại.");
    }
    void OnDestroy() { if (visible && !leaving) Time.timeScale = oldTimeScale; }
    void Populate()
    {
        view.Text("Title", victory ? "VICTORY" : "DEFEAT");
        if (!victory) view.Component<TMP_Text>("Title").color = view.defeatAccent;
        view.Text("Subtitle", victory ? "CHIẾN THẮNG • HÀNH TRÌNH TIẾP DIỄN" : "THẤT BẠI • MỘT CƠ HỘI KHÁC");
        var session = CampaignSession.Instance;
        bool campaign = session != null && session.InEncounter;
        view.Text("Experience", (victory && campaign ? session.ResultExperience : 0) + " EXP");
        view.Text("LootTitle", victory ? "Chiến lợi phẩm" : "Trở về điểm nghỉ");
        int lootCount = victory && campaign ? session.ResultRewards.Count : 0;
        view.Active("Loot", lootCount > 0); view.Active("Description", lootCount == 0);
        view.Text("Description", victory ? campaign ? "Không có vật phẩm trong trận này." : "Đã hoàn tất trận thử nghiệm." : "Party sẽ trở về điểm lưu gần nhất và được hồi đầy máu.\n\nQuái vẫn còn trên map để bạn thử lại.");
        var lootRows = view.Rows("Loot/Viewport/Content", lootCount);
        for (int i = 0; i < lootCount; i++)
        {
            var item = session.ResultRewards[i];
            AdventureCanvasRoot.RowText(lootRows[i], "Name", session.Config.FindItem(item.id)?.displayName ?? item.id);
            AdventureCanvasRoot.RowText(lootRows[i], "Count", "×" + item.count);
        }
        var members = view.Rows("Members/Viewport/Content", manager.playerParty.Count);
        for (int i = 0; i < manager.playerParty.Count; i++)
        {
            var unit = manager.playerParty[i]; if (unit == null) continue;
            AdventureCanvasRoot.RowPortrait(members[i], "Portrait", unit.unitPortrait);
            AdventureCanvasRoot.RowText(members[i], "Name", unit.unitName);
            if (campaign && i < session.Data.party.Count)
            {
                var member = session.Data.party[i];
                AdventureCanvasRoot.RowText(members[i], "Progress", "CẤP " + member.level + " • " + member.xp + " / " + member.RequiredXP + " EXP");
                AdventureCanvasRoot.RowFill(members[i], "XP/Fill", (float)member.xp / member.RequiredXP);
            }
            else
            {
                AdventureCanvasRoot.RowText(members[i], "Progress", "HP " + unit.currentHP + " / " + unit.maxHP);
                AdventureCanvasRoot.RowFill(members[i], "XP/Fill", (float)unit.currentHP / unit.maxHP);
            }
        }
        view.Text("Stats/Damage", dealt.ToString()); view.Text("Stats/Highest", highest.ToString());
        view.Text("Stats/Received", received.ToString()); view.Text("Stats/Parries", parries.ToString());
        view.Text("Stats/Kills", manager.enemyParty.Count(x => x != null && x.IsDead).ToString());
        view.Text("Stats/Time", ((int)duration / 60) + ":" + ((int)duration % 60).ToString("00"));
        view.Text("Error", "");
    }
}
