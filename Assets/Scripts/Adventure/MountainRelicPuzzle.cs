using UnityEngine;
using UnityEngine.UI;

// A small scene-local exploration puzzle; does not change campaign saves or combat.
public sealed class MountainRelicPuzzle : MonoBehaviour
{
    public Transform[] stones;
    public Renderer[] indicators;
    public int[] sequence = { 0, 1, 2 };
    public Transform gate;
    public string clue = "1 - 2 - 3";
    public float interactionRange = 3.5f;
    public PlayerScript player;
    public Text prompt;
    int progress;
    bool solved;
    float opened, messageUntil;
    Vector3 closedPosition;
    string message;
    void Start() { if (gate != null) closedPosition = gate.position; }
    void Update()
    {
        if (player == null) return;
        if (solved && gate != null)
        {
            opened = Mathf.MoveTowards(opened, 1, Time.deltaTime * .5f);
            gate.position = closedPosition + Vector3.down * (9 * Mathf.SmoothStep(0, 1, opened));
        }
        int nearest = -1; float best = interactionRange * interactionRange;
        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null) continue;
            float d = (player.transform.position - stones[i].position).sqrMagnitude;
            if (d < best) { best = d; nearest = i; }
        }
        bool nearby = (player.transform.position - transform.position).sqrMagnitude < 45 * 45;
        if (prompt != null)
        {
            prompt.transform.parent.gameObject.SetActive(nearby);
            prompt.text = solved ? "CONG DA DA MO" : Time.time < messageUntil ? message :
                "BIA DA: " + clue + "   |   " + progress + "/" + sequence.Length +
                (nearest >= 0 ? "   [E] Kich hoat bia " + (nearest + 1) : "   Tim cac bia da quanh khu vuc");
        }
        if (solved || nearest < 0 || CampaignSession.InputBlocked || !Input.GetKeyDown(KeyCode.E)) return;
        ActivateStone(nearest);
    }
    void ActivateStone(int nearest)
    {
        if (solved) return;
        if (sequence[progress] == nearest)
        {
            Tint(nearest, new Color(.2f, .85f, 1));
            progress++; solved = progress == sequence.Length;
            message = solved ? "CONG DA DA MO" : "Da kich hoat " + progress + "/" + sequence.Length;
        }
        else
        {
            progress = 0;
            for (int i = 0; i < indicators.Length; i++) Tint(i, new Color(.65f,.45f,.18f));
            message = "Sai thu tu. Goi y: " + clue;
        }
        messageUntil = Time.time + 2;
    }
    void Tint(int index, Color color)
    {
        if (index >= indicators.Length || indicators[index] == null) return;
        var block = new MaterialPropertyBlock(); indicators[index].GetPropertyBlock(block);
        block.SetColor("_BaseColor", color); indicators[index].SetPropertyBlock(block);
    }
}
