using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/Story Dialogue", fileName = "StoryDialogue")]
public sealed class AdventureStoryDialogue : ScriptableObject
{
    [Tooltip("Stable ID used for story progression checks. Leave empty to use the asset name.")]
    public string dialogueId;
    [Tooltip("When enabled, this dialogue is available only once per NPC. Default NPC dialogue is repeatable.")]
    public bool playOnce = true;
    [Tooltip("Optional quest IDs that must be completed before this dialogue unlocks.")]
    public List<string> requiredCompletedQuestIds = new List<string>();
    [Tooltip("Optional dialogue IDs that must have been completed anywhere in the story before this dialogue unlocks.")]
    public List<string> requiredCompletedDialogueIds = new List<string>();
    public string startNodeId = "start";
    [Tooltip("Tên hiển thị cho câu trả lời mà người chơi chọn.")]
    public string playerSpeakerName = "Người chơi";
    [Tooltip("Animator state của Player khi đang nói; để trống nếu không có state nói chuyện.")]
    public string playerTalkAnimationState = "Base Layer.Talking";
    [Tooltip("Animator state giữ Player ở tư thế chờ khi NPC đang nói. Có thể dùng Base Layer.Basic Movement với movementvalue = 0.")]
    public string playerDialogueIdleAnimationState = "Base Layer.Basic Movement";
    [Min(1f), Tooltip("Số ký tự hiện ra mỗi giây khi đang nói.")]
    public float charactersPerSecond = 32f;
    public List<AdventureDialogueNode> nodes = new List<AdventureDialogueNode>();

    public string Id => string.IsNullOrWhiteSpace(dialogueId) ? name : dialogueId.Trim();

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
    [Tooltip("Dùng khi nút hội thoại không có lựa chọn. Để trống để kết thúc cuộc trò chuyện.")]
    public string nextNodeId;
    public List<AdventureDialogueChoice> choices = new List<AdventureDialogueChoice>();
}

public enum AdventureDialogueSpeaker { NPC, Player }

[Serializable]
public sealed class AdventureDialogueChoice
{
    [Tooltip("Câu người chơi sẽ nói khi chọn phương án này.")]
    [TextArea(1, 2)] public string text = "Tiếp tục";
    public string nextNodeId;
    [Tooltip("Selecting this choice offers the quest, then follows Next Node ID.")]
    public AdventureQuest questToOffer;
}
