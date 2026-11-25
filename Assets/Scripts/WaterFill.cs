using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class WaterFill : MonoBehaviour
{
    public float fillDistance;
    public string msg = "Press E to fill with water";
    public string msg2 = "Already full";

    private Transform player;
    private Item item;
    private PickableObject po;

    private Camera playerCamera;
    private PlayerUIManager puim;
    private GameObject flowVisual;
    private ParticleSystem flowParticles;
    private Coroutine flowRoutine;

    public bool IsRunning { get; private set; }

    public void ConfigureFlow(GameObject visual, ParticleSystem particles)
    {
        flowVisual = visual;
        flowParticles = particles;
        SetFlowVisuals(false);
    }

    public void StartWaterFlow(float duration = 2.5f)
    {
        if (flowRoutine != null)
            StopCoroutine(flowRoutine);
        flowRoutine = StartCoroutine(WaterFlowRoutine(Mathf.Max(0.25f, duration)));
    }

    private IEnumerator WaterFlowRoutine(float duration)
    {
        SetFlowVisuals(true);
        yield return new WaitForSeconds(duration);
        SetFlowVisuals(false);
        flowRoutine = null;
    }

    private void SetFlowVisuals(bool running)
    {
        IsRunning = running;
        if (flowVisual != null)
            flowVisual.SetActive(running);

        if (flowParticles == null)
            return;

        if (running)
        {
            if (!flowParticles.isPlaying)
                flowParticles.Play();
        }
        else if (flowParticles.isPlaying)
        {
            flowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Start()
    {
        //player = GameObject.FindGameObjectWithTag("Player").transform;
        

    }

    void Update()
    {
        if (HighStakes.City.WarehouseGuideUI.IsOpen || Time.timeScale <= 0) return;
        if (GameMaster.GameOverActive)
            return;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player == null)
                continue;

            Vector3 horizontalOffset = player.transform.position - transform.position;
            horizontalOffset.y = 0f;
            float distance = horizontalOffset.magnitude;

            if (distance > fillDistance)
                continue;

            playerCamera = player.GetComponentInChildren<Camera>();
            Inventory inventory = player.GetComponent<Inventory>();
            puim = player.GetComponent<PlayerUIManager>();
            var detector = player.GetComponent<ObjectDetector>();
            if (detector != null && detector.CurrentTarget != null) continue;
            if (playerCamera == null || inventory == null || puim == null)
                continue;

            if (!IsTargeted(playerCamera))
                continue;

            Item selectedItem = inventory.GetSelectedItem();
            bool selectedWateringCan = selectedItem != null && selectedItem.name == "WateringCan";

            if (!selectedWateringCan)
            {
                puim.ShowInteractionPrompt(KeyCode.E,
                    LocalizationManager.GetOrFallback("ui.action.turn_on_water", "Turn on water"));
                if (Input.GetKeyDown(KeyCode.E) ||
                    (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame))
                    StartWaterFlow();
                continue;
            }

            PickableObject water = selectedItem.prefab != null
                ? selectedItem.prefab.GetComponent<PickableObject>()
                : null;

            if (inventory.IsSelectedWateringCanFull())
            {
                puim.ShowPopup(msg2);
                continue;
            }

            puim.ShowInteractionPrompt(KeyCode.E,
                LocalizationManager.GetOrFallback("ui.action.fill_watering_can", "Fill WateringCan"));
            if (Input.GetKeyDown(KeyCode.E) ||
                (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame))
            {
                StartWaterFlow();
                inventory.SetSelectedWateringCanFull(true);
                if (water != null)
                    water.isFull = true;
                GameLogger.LogInfo("farming", "watering_can_filled", "Watering can refilled with water");
            }
        }
    }

    private bool IsTargeted(Camera camera)
    {
        var owner = camera.GetComponentInParent<PlayerController>();
        return owner != null && HighStakes.City.CityInteractionTargeting.TryScore(owner.transform,
            camera, transform, fillDistance, out _);
    }
}
