using UnityEngine;

public enum CivilianPersonality
{
    Friendly,
    Neutral,
    Suspicious,
    Aggressive
}

public class CivilianBehavior : MonoBehaviour, INpcBehavior
{
    [Header("Civilian Personality")]
    public CivilianPersonality personality = CivilianPersonality.Neutral;

    public void Interact(PlayerStats playerStats)
    {
        NpcInteraction npcInteraction = GetComponent<NpcInteraction>();
        if (npcInteraction == null)
            return;

        string greeting = GetGreeting(npcInteraction);
        bool confrontational = personality == CivilianPersonality.Suspicious ||
            personality == CivilianPersonality.Aggressive;
        string secondChoice = confrontational ? NpcDialogue.Get("choice.ask_directly") : NpcDialogue.Get("choice.make_conversation");

        npcInteraction.ShowConversationChoices(
            greeting,
            NpcDialogue.Get("choice.anything_happening_around_here"), () =>
            {
                npcInteraction.RememberChoice("civilian.neighborhood", 1);
                npcInteraction.ShowConversationChoices(
                    GetNeighborhoodResponse(playerStats),
                    NpcDialogue.Get("choice.what_have_you_heard"), () =>
                    {
                        npcInteraction.RememberChoice("civilian.reputation", 1);
                        npcInteraction.ShowConversationChoices(
                            NpcDialogue.Get("civilian.reputation"),
                            NpcDialogue.Get("choice.ask_one_more_question"), () => ShowCasualChoice(npcInteraction, playerStats),
                            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
                            null, null);
                    },
                    NpcDialogue.Get("choice.keep_it_casual"), () => ShowCasualChoice(npcInteraction, playerStats),
                    NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")));
            },
            secondChoice, () =>
            {
                if (confrontational)
                    ShowPressChoice(npcInteraction, playerStats);
                else
                    ShowCasualChoice(npcInteraction, playerStats);
            },
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")));

        Debug.Log($"Civilian ({personality}) opened a contextual conversation.");
    }

    private void ShowCasualChoice(NpcInteraction npcInteraction, PlayerStats playerStats)
    {
        string previousChoice = npcInteraction.LastChoiceId;
        npcInteraction.RememberChoice("civilian.casual", 0);
        npcInteraction.ShowConversationChoices(
            previousChoice == "civilian.reputation"
                ? NpcDialogue.Get("civilian.remembers_reputation")
                : NpcDialogue.Get($"civilian.{personality.ToString().ToLowerInvariant()}.casual"),
            NpcDialogue.Get("choice.ask_one_more_question"), () =>
            {
                npcInteraction.RememberChoice("civilian.follow_up", 0);
                npcInteraction.ShowConversationChoices(
                    GetNeighborhoodResponse(playerStats),
                    NpcDialogue.Get("choice.that_s_enough"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
                    NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
                    null, null);
            },
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
            null, null);
    }

    private void ShowPressChoice(NpcInteraction npcInteraction, PlayerStats playerStats)
    {
        npcInteraction.RememberChoice("civilian.press", -2);
        npcInteraction.ShowConversationChoices(
            GetNeighborhoodResponse(playerStats),
                    NpcDialogue.Get("choice.ask_one_direct_question"), () =>
            {
                npcInteraction.RememberChoice("civilian.press_follow_up", -1);
                npcInteraction.ShowConversationChoices(
                    NpcDialogue.Get("civilian.suspicion"),
                    NpcDialogue.Get("choice.back_off"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
                    NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
                    null, null);
            },
            NpcDialogue.Get("choice.back_off"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")),
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("civilian.leave")));
    }

    private string GetNeighborhoodResponse(PlayerStats playerStats)
    {
        NpcInteraction npcInteraction = GetComponent<NpcInteraction>();
        if (npcInteraction != null && npcInteraction.RelationshipScore <= -2)
            return NpcDialogue.Get("civilian.cold");

        if (npcInteraction != null && npcInteraction.RelationshipScore >= 2)
            return NpcDialogue.Get("civilian.trusted");

        if (playerStats != null && playerStats.suspicion >= 50f)
            return NpcDialogue.Get("civilian.suspicion");

        if (playerStats != null && playerStats.reputation >= 50f)
            return NpcDialogue.Get("civilian.reputation");

        if (GameStageManager.Instance != null &&
            GameStageManager.Instance.currentStage != GameStage.Starter)
            return NpcDialogue.Get("civilian.stage");

        return NpcDialogue.Get($"civilian.{personality.ToString().ToLowerInvariant()}.neighborhood");
    }

    private string GetGreeting(NpcInteraction npcInteraction)
    {
        string personalityKey = personality.ToString().ToLowerInvariant();
        if (npcInteraction.ConversationCount <= 1)
            return NpcDialogue.Get($"civilian.{personalityKey}.first");

        if (npcInteraction.LastChoiceId == "civilian.reputation")
            return NpcDialogue.Get("civilian.remembers_reputation");

        if (npcInteraction.RelationshipScore >= 2)
            return NpcDialogue.Get("civilian.trusted");

        if (npcInteraction.RelationshipScore <= -2)
            return NpcDialogue.Get("civilian.cold");

        return NpcDialogue.Get($"civilian.{personalityKey}.return");
    }

    private string GetResponse(PlayerStats playerStats)
    {
        string personalityKey = personality.ToString().ToLowerInvariant();
        string moodKey = playerStats.currentMood == Mood.Good
            ? "good"
            : playerStats.currentMood == Mood.Angry
                ? "angry"
                : playerStats.currentMood == Mood.Sad ? "sad" : "default";

        if (personality == CivilianPersonality.Aggressive && moodKey != "angry")
            moodKey = "default";

        return NpcDialogue.Get($"personality.{personalityKey}.{moodKey}");
    }
}
