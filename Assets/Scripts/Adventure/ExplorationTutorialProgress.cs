using System;

[Serializable]
public sealed class ExplorationTutorialProgress
{
    public string id;
    public bool introComplete;
    public int nextLesson;
    public bool complete;
    public int curriculumVersion;
    public bool movementCompleted;
    public bool jumpCompleted;
    public bool enemyEncountered;
}
