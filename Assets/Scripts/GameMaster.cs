using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
//using UnityEngine.UIElements;
using System.Collections;
using Unity.VisualScripting;


public class GameMaster : MonoBehaviour
{

    [Header("PoliceDetectionHelper")]
    public bool hasIllegal;
    public bool crimeHappened;
    public static GameMaster Instance;
    public GameObject criminal;   // The actual suspect who did the crime
    public bool isChasingSomeone;
    public bool arrest;
    public bool isVisible = true;

    public bool IsGameOver { get; private set; }
    public static bool GameOverActive => Instance != null && Instance.IsGameOver;

    [HideInInspector]
    [Header("ScriptCalling")]
    private PackingSys ps;

    private StealMaster stealMaster;
    private PlayerUIManager puim;
    private GameOverController gameOverController;


    private void Awake()
    {
        Instance = this;
        gameOverController = GameOverController.GetOrCreate();
    }

    [System.Obsolete]
    void Start()
    {
        ps = FindObjectOfType<PackingSys>();

        hasIllegal = false;
        crimeHappened = false;
        isChasingSomeone = false;
        arrest = false;
        IsGameOver = false;

        stealMaster = GetComponent<StealMaster>(); // same object as GM
        

    }

    void Update()
    {
        if (IsGameOver)
            return;

        if (Input.GetKeyDown(KeyCode.R) && FindAnyObjectByType<HighStakes.City.CityMissionDirector>() == null)
        {
            RestartGame();
            return;
        }

        if (!isChasingSomeone && ps != null && ps.isPacking)
        {
            criminal = null;
            crimeHappened = false;
            ps.isPacking = false;
        }

        if (stealMaster != null && stealMaster.currentPlayer != null)
        {
            puim = stealMaster.currentPlayer.GetComponent<PlayerUIManager>();
        }

    }

    private void RestartGame()
    {
        Debug.Log("Restart cheat activated.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public bool IsPlayerSelling(GameObject player)
    {
        if (player == null || IsGameOver)
            return false;

        foreach (BuyerBehavior buyer in PoliceHeatRegistry.ActiveBuyers)
        {
            if (buyer != null && buyer.IsSellingTo(player))
                return true;
        }

        return false;
    }

    public bool TryCatchPlayer(GameObject suspect)
    {
        if (IsGameOver || suspect == null)
            return false;

        IsGameOver = true;
        crimeHappened = true;
        criminal = suspect;

        PlayerUIManager playerUi = suspect.GetComponent<PlayerUIManager>();
        if (playerUi != null)
        {
            playerUi.HidePopup();
            playerUi.CloseAllInteractionUI();
        }

        if (gameOverController == null)
            gameOverController = GameOverController.GetOrCreate();

        gameOverController.Show(suspect);
        Debug.Log($"[GameMaster] Player caught selling drugs: {suspect.name}");
        return true;
    }

    /// <summary>
    /// Starts the wanted chase only after the per-player heat controller reaches
    /// 100%. Seeing a deal never calls this method and never causes Game Over.
    /// </summary>
    public bool BeginPoliceChase(GameObject suspect)
    {
        if (IsGameOver || suspect == null)
            return false;

        crimeHappened = true;
        criminal = suspect;
        isChasingSomeone = true;
        Debug.Log($"[GameMaster] Police pursuit started for {suspect.name}.");
        return true;
    }

    public void ClearPoliceChase(GameObject suspect)
    {
        if (suspect != null && criminal != suspect)
            return;

        crimeHappened = false;
        criminal = null;
        isChasingSomeone = false;
    }

    public bool HeDoes()
    {
        if (hasIllegal)
        {
            return true;

        }
        else
        {
            return false;
        }
    }

    public void ReportCrime(GameObject suspect)
    {
        crimeHappened = true;
        criminal = suspect;  // Store who did it
        Debug.Log($"Crime committed by {suspect.name}");
    }

    public void CallPolice(GameObject suspect, Transform caller)
    {
        GameObject[] npcs = GameObject.FindGameObjectsWithTag("npc");
        float detectionRange = 40f;
        GameObject closestPolice = null;
        float closestDist = Mathf.Infinity;

        foreach (GameObject npc in npcs)
        {
            PoliceBehavior pb = npc.GetComponent<PoliceBehavior>();
            if (pb == null) continue;

            float dist = Vector3.Distance(caller.position, npc.transform.position);
            if (dist < closestDist && dist <= detectionRange)
            {
                closestDist = dist;
                closestPolice = npc;
            }
        }

        if (closestPolice != null)
        {
            StartCoroutine(PoliceReactionCountdown(suspect, closestPolice.transform, caller));
        }
        else
        {
            if (puim != null)
                puim.ShowPopup("No police nearby!");
        }
    }

    private IEnumerator PoliceReactionCountdown(GameObject suspect, Transform police, Transform caller)
    {
        if (puim == null)
        {
            Debug.LogWarning("PlayerUIManager not found on player!");
            yield break;
        }

        // show initial message
        puim.ShowWantedPanel(true); // activate wanted panel
        puim.UpdateWantedCountdown("What are you doing man? I'm calling the police...");

        int countdown = 10;
        while (countdown > 0)
        {
            puim.UpdateWantedCountdown(countdown.ToString());
            yield return new WaitForSeconds(1f);
            countdown--;
        }

        // hide wanted panel and trigger police
        puim.ShowWantedPanel(false);
        ReportCrime(suspect);

        PoliceBehavior pb = police.GetComponent<PoliceBehavior>();
        if (pb != null)
        {
            pb.SendMessage("StartChase", suspect.transform, SendMessageOptions.DontRequireReceiver);
            isChasingSomeone = true;
            pb.isCalled = true;
        }

        

        Debug.Log(" Police called after countdown.");
    }




}
