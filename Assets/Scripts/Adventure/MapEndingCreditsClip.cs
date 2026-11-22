using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public sealed class MapEndingCreditsClip : PlayableAsset, ITimelineClipAsset
{
    public string title;
    [TextArea(4, 25)] public string body;
    public bool scroll;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CreditsFrame>.Create(graph);
        playable.GetBehaviour().clip = this;
        return playable;
    }

    public sealed class CreditsFrame : PlayableBehaviour
    {
        public MapEndingCreditsClip clip;
    }
}
