using System;

[Serializable]
public sealed class ExplorationTutorialProgress
{
    public string id;
    public bool introComplete;
    public int nextLesson;
    public bool complete;
}
