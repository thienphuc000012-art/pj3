using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(.78f, .61f, .35f)]
[TrackClipType(typeof(MapEndingCreditsClip))]
[TrackBindingType(typeof(MapEndingCreditsView))]
public sealed class MapEndingCreditsTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount) =>
        ScriptPlayable<CreditsMixer>.Create(graph, inputCount);

    public sealed class CreditsMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is MapEndingCreditsView view)) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                var input = (ScriptPlayable<MapEndingCreditsClip.CreditsFrame>)playable.GetInput(i);
                double duration = input.GetDuration();
                float fraction = duration > 0 ? Mathf.Clamp01((float)(input.GetTime() / duration)) : 0f;
                view.Show(input.GetBehaviour().clip, fraction);
                return;
            }
            view.HideText();
        }
    }
}
