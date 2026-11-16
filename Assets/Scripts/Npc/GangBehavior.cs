using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class InStockItem
{
    public string itemName;
    public int amount;
    public int basePrice = 100;

    public Item item;

    public InStockItem(Item item, int quantity, Image icon)
    {
        this.item = item;
        this.amount = quantity;
    }

}

public enum GangPersonality
{
    Friendly,
    Neutral,
    Suspicious,
    Aggressive
}

public class GangBehavior : MonoBehaviour, INpcBehavior
{

    public List<InStockItem> inStockItems = new List<InStockItem>();

    [Header("Gang Personality")]
    public GangPersonality personality = GangPersonality.Neutral;

    // Track relationship with player
    //private int trust = 50;  // 0–100, starts neutral

    private GameMaster gm;
    private GameStageManager gsm;
    private NpcInteraction npcInteraction;
    private PlayerUIManager puim;

    [HideInInspector]
    public GameObject player;


    [Header("Recruit logic")]
    public bool recruited = false;


    public int money = 148;


    private void Awake()
    {
        npcInteraction = GetComponent<NpcInteraction>();

    }

    void Start()
    {
        gsm = FindObjectOfType<GameStageManager>();
        gm = FindObjectOfType<GameMaster>();
    }

    public List<InStockItem> GetInventory()
    {
        return inStockItems;
    }


    public void Interact(PlayerStats playerStats)
    {
        player = npcInteraction != null ? npcInteraction.currentPlayer : null;
        if (player == null || npcInteraction == null)
            return;

        puim = player.GetComponent<PlayerUIManager>();
        npcInteraction.ShowConversationChoices(
            GetGreeting(),
            NpcDialogue.Get("choice.talk_business"), () => ShowShopTalk(playerStats),
            NpcDialogue.Get("choice.ask_about_teaming_up"), () => ShowRecruitPath(),
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")));

        Debug.Log($"GangMember ({personality}) opened a contextual conversation.");

    }

    private string GetGreeting()
    {
        if (npcInteraction.ConversationCount <= 1)
            return GetVoiceGreeting("first");

        if (npcInteraction.LastChoiceId == "gang.recruit.ask" ||
            npcInteraction.LastChoiceId == "gang.recruit.rejected")
            return GetVoiceGreeting("recruit_followup");

        if (npcInteraction.RelationshipScore >= 2)
            return GetVoiceGreeting("trusted");

        if (npcInteraction.RelationshipScore <= -2)
            return GetVoiceGreeting("cold");

        return GetVoiceGreeting("return");
    }

    private string GetVoiceGreeting(string state)
    {
        string npcName = npcInteraction != null ? npcInteraction.gameObject.name.ToLowerInvariant() : string.Empty;
        if (npcName == "laura" || npcName == "susan")
            return NpcDialogue.Get($"gang.greeting.{npcName}.{state}");

        return NpcDialogue.Get($"gang.greeting.{state}");
    }

    private void ShowShopTalk(PlayerStats playerStats)
    {
        string stageKey = GameStageManager.Instance == null
            ? "gang.stage_starter"
            : "gang." + GameStageManager.Instance.currentStage.ToString().ToLowerInvariant();

        if (stageKey == "gang.smalldealer")
            stageKey = "gang.stage_recruiting";
        else if (stageKey == "gang.gangleader" || stageKey == "gang.crimeboss" || stageKey == "gang.mafia")
            stageKey = "gang.stage_leader";
        else
            stageKey = "gang.stage_starter";

        npcInteraction.RememberChoice("gang.shop", 1);
        npcInteraction.ShowConversationChoices(
            NpcDialogue.Get("gang.talk_shop") + "\n" + NpcDialogue.Get(stageKey),
            NpcDialogue.Get("choice.what_comes_next"), ShowRecruitPath,
            NpcDialogue.Get("choice.keep_this_professional"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")),
            NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")));
    }

    private void ShowRecruitPath()
    {
        bool canRecruit = gsm != null && GameStageManager.Instance != null &&
            GameStageManager.Instance.currentStage != GameStage.Starter &&
            gsm.recruitedDealers < gsm.maxRecruitable && !recruited;

        if (!canRecruit)
        {
            string explanation = GameStageManager.Instance != null &&
                GameStageManager.Instance.currentStage == GameStage.Starter
                ? NpcDialogue.Get("gang.stage_starter")
                : NpcDialogue.Get("gang.recruit.more");

            npcInteraction.ShowConversationChoices(
                explanation,
                NpcDialogue.Get("choice.what_stock_do_you_have"), () => ShowShopTalk(null),
                NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")),
                null, null);
            return;
        }

        npcInteraction.RememberChoice("gang.recruit.ask", 2);
        npcInteraction.ShowConversationChoices(
            NpcDialogue.Get("gang.recruit_offer"),
            NpcDialogue.Get("choice.make_the_offer"), OnRecruitRoll,
            NpcDialogue.Get("choice.what_can_you_handle"), () =>
            {
                npcInteraction.RememberChoice("gang.recruit.questions", 1);
                npcInteraction.ShowConversationChoices(
                    NpcDialogue.Get("gang.recruit.more"),
                    NpcDialogue.Get("choice.make_the_offer"), OnRecruitRoll,
                    NpcDialogue.Get("choice.not_today"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")),
                    null, null);
            },
            NpcDialogue.Get("choice.not_today"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")));
    }

    void OnRecruitRoll()
    {

        // Show in popup instead of log
        NpcInteraction npcInteraction = GetComponent<NpcInteraction>();

        float chance = 0.4f; // 40% base chance


        if (Random.value < chance)
        {
            recruited = true;

            if (npcInteraction != null)
            {
                npcInteraction.RememberChoice("gang.recruit.accepted", 10);
                npcInteraction.ShowConversationChoices(
                    NpcDialogue.Get("gang.recruit.accepted"),
                    NpcDialogue.Get("choice.continue"), () =>
                    {
                        RecruitBehavior recruit = GetComponent<RecruitBehavior>();
                        if (recruit != null)
                            recruit.enabled = true;

                        if (gsm != null)
                            gsm.RecordDealerRecruitment();

                        // Remove the old behavior after the choice so the next
                        // interaction resolves to the recruited-dealer flow.
                        Destroy(this);
                        npcInteraction.OnChangeBehavior();
                    },
                    NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.accepted")),
                    null, null);
            }

        }
        else
        {
            if (npcInteraction != null)
            {
                npcInteraction.RememberChoice("gang.recruit.rejected", -5);
                npcInteraction.ShowConversationChoices(
                    NpcDialogue.Get("gang.recruit.rejected"),
                    NpcDialogue.Get("choice.try_another_pitch"), ShowRecruitPath,
                    NpcDialogue.Get("choice.leave"), () => npcInteraction.CloseConversation(NpcDialogue.Get("gang.recruit.declined")),
                    null, null);
            }
        }
    }

    private string GetResponse(PlayerStats playerStats)
    {
        string personalityKey = personality.ToString().ToLowerInvariant();
        string moodKey = playerStats.currentMood == Mood.Good
            ? "good"
            : playerStats.currentMood == Mood.Angry
                ? "angry"
                : playerStats.currentMood == Mood.Sad ? "sad" : "default";

        if (personality == GangPersonality.Aggressive && moodKey != "angry")
            moodKey = "default";

        return NpcDialogue.Get($"personality.{personalityKey}.{moodKey}");
    }

    public void NpcDeal()
    {



        StartCoroutine(DealRoutine());
        Debug.Log("deal in proggress..");
        //animacka dealu..



    }

    private IEnumerator DealRoutine()
    {
        Debug.Log("deal animation...");
        // pick random duration between 30 and 60 sec
        float interactionTime = Random.Range(5f, 7f);

        /*
        // start animation
        if (animator != null)
        {
            animator.SetBool(animationName, true);
            // or: animator.Play(animationName);
        }
        */

        // wait for that duration
        yield return new WaitForSeconds(interactionTime);
        /*
        // stop animation / release interaction
        if (animator != null)
        {
            animator.SetBool(animationName, false);
        }
        */

        Debug.Log("deal happened.. hopefully..");
        gm.crimeHappened = true;
        NpcInteraction npc = GetComponent<NpcInteraction>();
        npc.isInteracting = false;
        npc.gameObject.GetComponent<NPCWanderRoadOnly>().ResumeMovement();

    }


    private void CloseInteraction()
    {
        if (npcInteraction != null)
            npcInteraction.EndDialogue();

        npcInteraction.interactionMessage = NpcDialogue.Get("gang.recruit.declined");
        puim.yesButton.SetActive(false);
        puim.noButton.SetActive(false);
        player.GetComponent<PlayerController>().LockMouse();

    }

}
