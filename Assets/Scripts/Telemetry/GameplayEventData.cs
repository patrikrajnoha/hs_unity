using System;
using UnityEngine;

// Local, anonymous gameplay measurements. IDs identify authored content, never a person.
[Serializable]
public sealed class GameplayEventData
{
    public string missionId, objectiveId, dialogueId, choice, signal, itemId, reason, strategy, rank, mode;
    public int missionNumber, attempt, score, totalScore, penalties, failures, mistakes, retries, detections;
    public float elapsed, duration, objectiveSeconds, navigationSeconds, efficiency;
    public bool completed, hasPosition;
    public Vector3 position, rotation;
}
