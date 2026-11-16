using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighStakes.City
{
    [Serializable] public sealed class StoryFile { public Contract[] contracts; public Conversation[] conversations; }
    [Serializable] public sealed class Contract
    {
        public string id, title, description;
        public int reward, reputation, baseScore = 1000;
        public float timeLimit;
        public ContractStep[] steps;
    }
    [Serializable] public sealed class ContractStep
    {
        public string objective, signal, target, dialogue;
        public float holdSeconds;
        public bool requiresClear;
    }
    [Serializable] public sealed class Conversation
    {
        public string id, speaker, text;
        public StoryChoice[] choices;
    }
    [Serializable] public sealed class StoryChoice { public string text, next, signal; }
    [Serializable] public sealed class LevelResult
    {
        public string contractId, title;
        public int baseScore, timeBonus, stealthBonus, penalties, score, failures, mistakes;
        public float seconds;
    }
    [Serializable] public sealed class CampaignSave
    {
        public int version = 2;
        public List<LevelResult> results = new List<LevelResult>();
        public int currentFailures, currentMistakes;
        public float currentTotalTime;
        public bool currentAlarm;
    }

    // Gameplay state and score calculations do not depend on scene objects.
    public sealed class ContractProgress
    {
        public readonly StoryFile Story;
        private readonly List<LevelResult> results = new List<LevelResult>();
        public IReadOnlyList<LevelResult> Results => results;
        public int ContractIndex => results.Count;
        public int StepIndex { get; private set; }
        public bool Finished => ContractIndex >= Story.contracts.Length;
        public Contract Current => Finished ? null : Story.contracts[ContractIndex];
        public ContractStep Step => Current == null ? null : Current.steps[StepIndex];
        public bool Active => !Finished && StepIndex > 0;
        public int Money { get; private set; }
        public int Reputation { get; private set; }
        public int Score { get; private set; }
        public int CurrentFailures { get; private set; }
        public int CurrentMistakes { get; private set; }
        public float CurrentTotalTime { get; private set; }
        public float Elapsed { get; private set; }
        public bool KnowsRoute { get; set; }
        public bool AlarmRaised { get; private set; }
        public int Failures { get { int sum = CurrentFailures; foreach (var result in results) sum += result.failures; return sum; } }
        public int Mistakes { get { int sum = CurrentMistakes; foreach (var result in results) sum += result.mistakes; return sum; } }
        public float TotalTime { get { float sum = CurrentTotalTime; foreach (var result in results) sum += result.seconds; return sum; } }
        public string Rank => Failures == 0 && Mistakes == 0 ? "S" : Failures <= 1 && Mistakes <= 2 ? "A" : Failures <= 3 ? "B" : "C";

        public ContractProgress(StoryFile story, CampaignSave save = null)
        {
            if (story == null || story.contracts == null || story.contracts.Length == 0)
                throw new ArgumentException("Campaign needs at least one level.");
            Story = story;
            if (save == null || save.version != 2 || save.results == null || save.results.Count > story.contracts.Length) return;
            // Reject incompatible or malformed checkpoints instead of unlocking arbitrary levels.
            for (int i = 0; i < save.results.Count; i++)
                if (save.results[i] == null || save.results[i].contractId != story.contracts[i].id || save.results[i].score < 0) return;
            foreach (var result in save.results)
            {
                results.Add(result); Score += result.score;
                Money += story.contracts[results.Count - 1].reward;
                Reputation += story.contracts[results.Count - 1].reputation;
            }
            if (!Finished)
            {
                CurrentFailures = Mathf.Max(0, save.currentFailures);
                CurrentMistakes = Mathf.Max(0, save.currentMistakes);
                CurrentTotalTime = Mathf.Max(0, save.currentTotalTime);
                AlarmRaised = save.currentAlarm;
            }
        }

        public bool Signal(string signal)
        {
            if (Finished || string.IsNullOrEmpty(signal) || signal != Step.signal) return false;
            StepIndex++;
            if (StepIndex == Current.steps.Length)
            {
                Contract level = Current;
                int timeBonus = Mathf.FloorToInt(Mathf.Max(0, level.timeLimit - Elapsed) * 2);
                int stealthBonus = AlarmRaised ? 0 : 300;
                int penalties = CurrentFailures * 200 + CurrentMistakes * 75;
                var result = new LevelResult {
                    contractId = level.id, title = level.title, baseScore = level.baseScore,
                    timeBonus = timeBonus, stealthBonus = stealthBonus, penalties = penalties,
                    score = Mathf.Max(0, level.baseScore + timeBonus + stealthBonus - penalties),
                    failures = CurrentFailures, mistakes = CurrentMistakes, seconds = CurrentTotalTime
                };
                results.Add(result); Score += result.score;
                Money += level.reward; Reputation += level.reputation;
                StepIndex = 0; Elapsed = 0; CurrentTotalTime = 0;
                CurrentFailures = 0; CurrentMistakes = 0; KnowsRoute = false; AlarmRaised = false;
            }
            return true;
        }

        public bool Tick(float delta)
        {
            if (!Active) return false;
            float elapsed = Mathf.Max(0, delta);
            Elapsed += elapsed; CurrentTotalTime += elapsed;
            // The director calls Fail once. Tick only reports expiration.
            return Current.timeLimit > 0 && Elapsed >= Current.timeLimit;
        }
        public void Fail()
        {
            if (Finished) return;
            CurrentFailures++; StepIndex = 0; Elapsed = 0; KnowsRoute = false;
            // An alarm anywhere in an attempt forfeits the level's clean-run bonus.
        }
        public void RecordMistake() { if (!Finished) CurrentMistakes++; }
        public void RaiseAlarm() { if (!Finished) AlarmRaised = true; }
        public CampaignSave Capture() => new CampaignSave {
            results = new List<LevelResult>(results), currentFailures = CurrentFailures,
            currentMistakes = CurrentMistakes, currentTotalTime = CurrentTotalTime, currentAlarm = AlarmRaised
        };
        public Conversation FindConversation(string id)
        {
            if (Story.conversations != null)
                foreach (var item in Story.conversations) if (item.id == id) return item;
            return null;
        }
    }
}
