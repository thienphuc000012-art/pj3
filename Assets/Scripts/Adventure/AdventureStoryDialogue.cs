using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/Story Dialogue", fileName = "StoryDialogue")]
public sealed class AdventureStoryDialogue : ScriptableObject
{
    public string startNodeId = "start";
    [Tooltip("Tên hiển thị cho câu trả lời mà người chơi chọn.")]
    public string playerSpeakerName = "Player";
    [Tooltip("Animator state của Player khi đang nói; để trống nếu không có state nói chuyện.")]
    public string playerTalkAnimationState = "Base Layer.Talking";
    [Min(1f), Tooltip("Số ký tự hiện ra mỗi giây khi đang nói.")]
    public float charactersPerSecond = 32f;
    public List<AdventureDialogueNode> nodes = new List<AdventureDialogueNode>();

    public AdventureDialogueNode FindNode(string id) =>
        nodes?.FirstOrDefault(node => node != null && string.Equals(node.id, id, StringComparison.Ordinal));
}

[Serializable]
public sealed class AdventureDialogueNode
{
    public string id = "start";
    public AdventureDialogueSpeaker speakerType = AdventureDialogueSpeaker.NPC;
    public string speaker;
    [TextArea(2, 8)] public string text;
    [Tooltip("Used when this node has no choices. Leave empty to end the conversation.")]
    public string nextNodeId;
    public List<AdventureDialogueChoice> choices = new List<AdventureDialogueChoice>();
}

public enum AdventureDialogueSpeaker { NPC, Player }

[Serializable]
public sealed class AdventureDialogueChoice
{
    [Tooltip("Câu người chơi sẽ nói khi chọn phương án này.")]
    [TextArea(1, 2)] public string text = "Continue";
    public string nextNodeId;
    [Tooltip("Selecting this choice offers the quest, then follows Next Node ID.")]
    public AdventureQuest questToOffer;
}
