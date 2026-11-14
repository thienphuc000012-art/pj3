using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BattleResultPanel : MonoBehaviour
{
    CombatManager manager;
    AdventureCanvasRoot view;
    BattleResultPresentation presentation;
    bool visible, victory, leaving;
    float started, duration, oldTimeScale;
    int dealt, received, highest, parries;
    void Awake()
    {
        manager = GetComponent<CombatManager>(); started = Time.unscaledTime;
        view = AdventureCanvasRoot.Acquire(AdventureCanvasKind.BattleResult);
        presentation = view.GetComponent<BattleResultPresentation>();
        view.Click("Continue", Continue);
        if (presentation != null && presentation.returnButton != null) presentation.returnButton.onClick.AddListener(Continue);
        view.gameObject.SetActive(false);
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
        ScreenSpaceCut.StopActive();
        victory = won; visible = true; duration = Time.unscaledTime - started;
        oldTimeScale = Time.timeScale; Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        view.gameObject.SetActive(true); AdventureCanvasRoot.EnsureEventSystem();
        if (presentation != null) presentation.Configure(victory);
        Populate();
    }
    void Update() { if (visible && !leaving && Input.GetKeyDown(KeyCode.Return)) Continue(); }
    void Continue()
    {
        if (!visible || leaving) return;
        if (presentation != null && presentation.IsAnimating) { presentation.Complete(); return; }
        var session = CampaignSession.Instance;
        bool accepted = session != null && session.InEncounter ? session.ContinueAfterBattle() : LoadingScreen.Load(Resources.Load<CampaignConfig>("Adventure/CampaignConfig")?.mapScene ?? "mapgame");
        if (accepted)
        {
            leaving = true; Time.timeScale = oldTimeScale > 0 ? oldTimeScale : 1;
            view.Enabled("Continue", false); view.Text("Continue/Label", "Đang chuyển cảnh…");
            if (presentation != null && presentation.returnButton != null) presentation.returnButton.interactable = false;
        }
        else view.Text("Error", LoadingScreen.Error ?? "Chưa thể chuyển cảnh. Hãy thử lại.");
    }
    void OnDestroy()
    {
        if (view != null)
        {
            var button = view.transform.Find("Continue")?.GetComponent<UnityEngine.UI.Button>();
            if (button != null) button.onClick.RemoveListener(Continue);
        }
        if (presentation != null && presentation.returnButton != null) presentation.returnButton.onClick.RemoveListener(Continue);
        if (visible && !leaving) Time.timeScale = oldTimeScale;
    }
    void Populate()
    {
        view.Text("Title", victory ? "CHIẾN THẮNG" : "THẤT BẠI");
        view.Text("Subtitle", victory ? "CHIẾN THẮNG • HÀNH TRÌNH TIẾP DIỄN" : "THẤT BẠI • MỘT CƠ HỘI KHÁC");
        var session = CampaignSession.Instance;
        bool campaign = session != null && session.InEncounter;
        view.Text("Experience", "Kinh nghiệm: " + (victory && campaign ? session.ResultExperience : 0));
        view.Text("LootTitle", victory ? "Chiến lợi phẩm" : "Trở về điểm nghỉ");
        int lootCount = victory && campaign ? session.ResultRewards.Count : 0;
        view.Active("Loot", lootCount > 0); view.Active("Description", lootCount == 0);
        view.Text("Description", victory ? campaign ? "Không có vật phẩm trong trận này." : "Đã hoàn tất trận thử nghiệm." : "Đội hình sẽ trở về điểm lưu gần nhất và được hồi đầy máu.\n\nQuái vẫn còn trên bản đồ để bạn thử lại.");
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
                AdventureCanvasRoot.RowText(members[i], "Progress", "CẤP " + member.level + " • " + member.xp + " / " + member.RequiredXP + " KN");
                AdventureCanvasRoot.RowFill(members[i], "XP/Fill", (float)member.xp / member.RequiredXP);
            }
            else
            {
                AdventureCanvasRoot.RowText(members[i], "Progress", "Máu " + unit.currentHP + " / " + unit.maxHP);
                AdventureCanvasRoot.RowFill(members[i], "XP/Fill", (float)unit.currentHP / unit.maxHP);
            }
        }
        view.Text("Stats/Damage", dealt.ToString()); view.Text("Stats/Highest", highest.ToString());
        view.Text("Stats/Received", received.ToString()); view.Text("Stats/Parries", parries.ToString());
        view.Text("Stats/Kills", manager.enemyParty.Count(x => x != null && x.IsDead).ToString());
        view.Text("Stats/Time", ((int)duration / 60) + ":" + ((int)duration % 60).ToString("00"));
        view.Text("Error", "");
        if (victory && presentation != null)
        {
            var progress = campaign ? session.ResultExperienceChanges : null;
            presentation.Begin(view, new[] { dealt, highest, received, parries,
                manager.enemyParty.Count(x => x != null && x.IsDead), Mathf.Max(0, (int)duration) },
                campaign ? session.ResultExperience : 0, members, progress, lootRows,
                campaign ? session.ResultRewards : new List<ItemStack>());
        }
    }
}
