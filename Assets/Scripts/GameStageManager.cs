using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum GameStage
{
    Starter,
    SmallDealer,
    GangLeader,
    CrimeBoss,
    Mafia,
}

[Serializable]
public sealed class QuestGuidanceContent
{
    public QuestGuidanceStage[] stages;
}

[Serializable]
public sealed class QuestGuidanceStage
{
    public string id;
    public string title;
    public string transitionMessage;
    public QuestGuidanceObjective[] objectives;
}

[Serializable]
public sealed class QuestGuidanceObjective
{
    public string id;
    public string title;
    public string description;
    public string progress;
    public string hint;
}

public class GameStageManager : MonoBehaviour
{
    public const int StarterItemsRequired = 15;
    public const int StarterClienteleRequired = 10;
    public const int DealersRequired = 3;
    public const int CrimeBossEarningsRequired = 20000;

    private const string GuidanceResourcePath = "Guidance/quest_guidance";

    [Header("Stage Logic")]
    public GameStage currentStage = GameStage.Starter;
    public int totalEarnings;
    public int successfulSales;
    public int successfulClientel;
    public int recruitedDealers;
    public int maxRecruitable;

    public static GameStageManager Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TextMeshProUGUI popupText;

    [Header("Stage Progress")]
    public GameObject progressPan;
    public TMP_Text stageTxt;
    public TMP_Text questTxt;

    public event Action GuidanceChanged;

    public string CurrentStageId => GetStageId(currentStage);
    public string CurrentObjectiveId { get; private set; }

    private QuestGuidanceContent guidanceContent;
    private string lastRenderedStageId;
    private string lastRenderedObjectiveId;
    private string lastRenderedProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[GameStageManager] Multiple managers exist in the active scene. Keeping the first instance.");
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ResolveUiReferences();
        LoadGuidanceContent();

        if (progressPan != null)
            progressPan.SetActive(true);
        if (panel != null)
            panel.SetActive(false);

        CheckProgress();
        RefreshGuidance(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveUiReferences()
    {
        if (stageTxt != null && questTxt != null)
            return;

        PlayerUIManager playerUi = FindAnyObjectByType<PlayerUIManager>();
        if (playerUi == null)
        {
            Debug.LogError("[GameStageManager] Could not find PlayerUIManager to bind QuestPanel text.");
            return;
        }

        if (stageTxt == null)
            stageTxt = playerUi.stageTxt;
        if (questTxt == null)
            questTxt = playerUi.questTxt;
        if (progressPan == null)
            progressPan = playerUi.stageProgressPanel;
    }

    private void LoadGuidanceContent()
    {
        TextAsset json = Resources.Load<TextAsset>(GuidanceResourcePath);
        if (json == null)
        {
            Debug.LogError("[GameStageManager] Missing guidance JSON at Resources/" + GuidanceResourcePath + ".json");
            guidanceContent = new QuestGuidanceContent { stages = Array.Empty<QuestGuidanceStage>() };
            return;
        }

        try
        {
            guidanceContent = JsonUtility.FromJson<QuestGuidanceContent>(json.text);
        }
        catch (Exception exception)
        {
            Debug.LogError("[GameStageManager] Could not parse guidance JSON: " + exception.Message);
            guidanceContent = new QuestGuidanceContent { stages = Array.Empty<QuestGuidanceStage>() };
            return;
        }

        if (guidanceContent == null || guidanceContent.stages == null)
        {
            Debug.LogError("[GameStageManager] Guidance JSON is malformed: the stages array is missing.");
            guidanceContent = new QuestGuidanceContent { stages = Array.Empty<QuestGuidanceStage>() };
        }
    }

    public void RecordPlayerSale(int itemCount, int earnings)
    {
        if (itemCount <= 0 || earnings < 0)
            return;

        successfulSales = AddWithoutOverflow(successfulSales, itemCount);
        successfulClientel = AddWithoutOverflow(successfulClientel, 1);
        totalEarnings = AddWithoutOverflow(totalEarnings, earnings);
        FirebaseGameLogger.LogInfo("economy", "sale_completed", "Sold " + itemCount + " item(s) for $" + earnings);
        UpdateProgressAndGuidance();
    }

    public void RecordEarnings(int earnings)
    {
        if (earnings <= 0)
            return;

        totalEarnings = AddWithoutOverflow(totalEarnings, earnings);
        FirebaseGameLogger.LogInfo("economy", "earnings_recorded", "Recorded $" + earnings + " earnings");
        UpdateProgressAndGuidance();
    }

    public void RecordDealerRecruitment()
    {
        recruitedDealers = Mathf.Min(AddWithoutOverflow(recruitedDealers, 1), Mathf.Max(DealersRequired, maxRecruitable));
        FirebaseGameLogger.LogInfo("progression", "dealer_recruited", "Dealer recruited (" + recruitedDealers + ")");
        UpdateProgressAndGuidance();
    }

    private void UpdateProgressAndGuidance()
    {
        CheckProgress();
        RefreshGuidance(false);
    }

    private void CheckProgress()
    {
        if (currentStage == GameStage.Starter &&
            successfulSales >= StarterItemsRequired &&
            successfulClientel >= StarterClienteleRequired)
        {
            AdvanceTo(GameStage.SmallDealer);
        }
        else if (currentStage == GameStage.SmallDealer && recruitedDealers >= DealersRequired)
        {
            AdvanceTo(GameStage.GangLeader);
        }
        else if (currentStage == GameStage.GangLeader && totalEarnings >= CrimeBossEarningsRequired)
        {
            AdvanceTo(GameStage.CrimeBoss);
        }
    }

    private void AdvanceTo(GameStage nextStage)
    {
        if (currentStage == nextStage)
            return;

        GameStage previousStage = currentStage;
        currentStage = nextStage;
        FirebaseGameLogger.LogInfo("progression", "stage_changed", previousStage + " -> " + nextStage);
        UnlockFeatures(nextStage);
        RefreshGuidance(false);
    }

    private void UnlockFeatures(GameStage stage)
    {
        switch (stage)
        {
            case GameStage.SmallDealer:
                maxRecruitable = 3;
                break;
            case GameStage.GangLeader:
                maxRecruitable = 5;
                break;
            case GameStage.CrimeBoss:
                break;
        }

        QuestGuidanceStage content = FindStage(GetStageId(stage));
        if (content != null && !string.IsNullOrWhiteSpace(content.transitionMessage) && panel != null && popupText != null)
            StartCoroutine(StageUpdate(content.transitionMessage));
    }

    private IEnumerator StageUpdate(string text)
    {
        panel.SetActive(true);
        popupText.text = text;
        yield return new WaitForSeconds(10f);
        panel.SetActive(false);
        popupText.text = string.Empty;
    }

    private void RefreshGuidance(bool force)
    {
        string stageId = GetStageId(currentStage);
        QuestGuidanceStage stage = FindStage(stageId);
        if (stage == null)
        {
            Debug.LogError("[GameStageManager] No guidance stage exists for stage ID '" + stageId + "'.");
            RenderFallback(stageId, "unknown_objective", force);
            return;
        }

        QuestGuidanceObjective objective = FindObjective(stage, GetCurrentObjectiveId());
        if (objective == null)
        {
            Debug.LogError("[GameStageManager] No guidance objective exists for stage ID '" + stageId + "'.");
            RenderFallback(stageId, "unknown_objective", force);
            return;
        }

        CurrentObjectiveId = objective.id;
        string progress = FormatObjectiveText(objective.progress);
        bool changed = force || stageId != lastRenderedStageId || objective.id != lastRenderedObjectiveId || progress != lastRenderedProgress;
        if (!changed)
            return;

        if (stageTxt != null)
            stageTxt.text = RequireText(stage.title, stageId + ".title");
        if (questTxt != null)
        {
            string title = RequireText(objective.title, objective.id + ".title");
            string description = RequireText(objective.description, objective.id + ".description");
            string hint = RequireText(objective.hint, objective.id + ".hint");
            questTxt.text = title + "\n" + description + "\n" + progress + "\n" + hint;
        }

        lastRenderedStageId = stageId;
        lastRenderedObjectiveId = objective.id;
        lastRenderedProgress = progress;
        GuidanceChanged?.Invoke();
    }

    private void RenderFallback(string stageId, string objectiveId, bool force)
    {
        if (!force && stageId == lastRenderedStageId && objectiveId == lastRenderedObjectiveId)
            return;

        CurrentObjectiveId = objectiveId;
        if (stageTxt != null)
            stageTxt.text = string.Empty;
        if (questTxt != null)
            questTxt.text = string.Empty;

        lastRenderedStageId = stageId;
        lastRenderedObjectiveId = objectiveId;
        lastRenderedProgress = string.Empty;
        GuidanceChanged?.Invoke();
    }

    private string GetCurrentObjectiveId()
    {
        switch (currentStage)
        {
            case GameStage.Starter:
                if (successfulSales == 0)
                    return "starter_first_sale";
                if (successfulSales < StarterItemsRequired)
                    return "starter_sell_stock";
                return "starter_reach_clients";
            case GameStage.SmallDealer:
                return "small_dealer_recruit";
            case GameStage.GangLeader:
                return "gang_leader_earnings";
            case GameStage.CrimeBoss:
                return "crime_boss_expand";
            case GameStage.Mafia:
                return "mafia_content_unavailable";
            default:
                return "unknown_objective";
        }
    }

    private string FormatObjectiveText(string template)
    {
        string text = RequireText(template, CurrentObjectiveId + ".progress");
        return text
            .Replace("{itemsSold}", successfulSales.ToString())
            .Replace("{itemsRequired}", StarterItemsRequired.ToString())
            .Replace("{clients}", successfulClientel.ToString())
            .Replace("{clientsRequired}", StarterClienteleRequired.ToString())
            .Replace("{dealers}", recruitedDealers.ToString())
            .Replace("{dealersRequired}", DealersRequired.ToString())
            .Replace("{earnings}", totalEarnings.ToString())
            .Replace("{earningsRequired}", CrimeBossEarningsRequired.ToString());
    }

    private QuestGuidanceStage FindStage(string id)
    {
        if (guidanceContent == null || guidanceContent.stages == null)
            return null;

        foreach (QuestGuidanceStage stage in guidanceContent.stages)
            if (stage != null && stage.id == id)
                return stage;
        return null;
    }

    private static QuestGuidanceObjective FindObjective(QuestGuidanceStage stage, string id)
    {
        if (stage == null || stage.objectives == null)
            return null;

        foreach (QuestGuidanceObjective objective in stage.objectives)
            if (objective != null && objective.id == id)
                return objective;
        return null;
    }

    private static string RequireText(string value, string contentId)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        Debug.LogError("[GameStageManager] Missing player-facing guidance text for '" + contentId + "'.");
        return string.Empty;
    }

    private static string GetStageId(GameStage stage)
    {
        return stage.ToString().ToLowerInvariant()
            .Replace("smalldealer", "small_dealer")
            .Replace("gangleader", "gang_leader")
            .Replace("crimeboss", "crime_boss");
    }

    private static int AddWithoutOverflow(int current, int amount)
    {
        if (current >= int.MaxValue - amount)
            return int.MaxValue;
        return current + amount;
    }

    // Existing development shortcuts are preserved.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            AdvanceTo(GameStage.SmallDealer);
        if (Input.GetKeyDown(KeyCode.K))
            AdvanceTo(GameStage.GangLeader);

    }
}
