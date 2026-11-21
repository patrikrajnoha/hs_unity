using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps one safehouse PackEmpty supply slot replenished after a successful
/// pickup. Dropped bags are ordinary pickups and never become spawn points.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PickableObject))]
public sealed class PackEmptyRespawnPoint : MonoBehaviour
{
    [Min(0f)]
    public float respawnDelay = 25f;

    private PickableObject pickable;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector3 spawnWorldScale;
    private bool isWorldSpawnPoint;
    private bool pickupHandled;

    private void Awake()
    {
        pickable = GetComponent<PickableObject>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        spawnWorldScale = transform.lossyScale;
    }

    private void OnEnable()
    {
        if (pickable == null)
            pickable = GetComponent<PickableObject>();
        if (pickable != null)
            pickable.PickedUp += HandlePickedUp;
    }

    private void OnDisable()
    {
        if (pickable != null)
            pickable.PickedUp -= HandlePickedUp;
    }

    public void ConfigureAsWorldSpawnPoint(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
        spawnWorldScale = transform.lossyScale;
        isWorldSpawnPoint = true;
        pickupHandled = false;
    }

    private void HandlePickedUp(PickableObject pickedObject)
    {
        if (!isWorldSpawnPoint || pickupHandled || pickedObject != pickable)
            return;

        pickupHandled = true;
        GameObject replacement = Instantiate(gameObject, spawnPosition, spawnRotation);
        replacement.transform.localScale = spawnWorldScale;
        replacement.SetActive(false);

        Rigidbody replacementBody = replacement.GetComponent<Rigidbody>();
        if (replacementBody != null)
        {
            replacementBody.linearVelocity = Vector3.zero;
            replacementBody.angularVelocity = Vector3.zero;
        }

        PackEmptyRespawnPoint replacementPoint = replacement.GetComponent<PackEmptyRespawnPoint>();
        if (replacementPoint == null)
        {
            Destroy(replacement);
            return;
        }

        PackEmptyRespawnTimer.Schedule(replacement, replacementPoint, spawnPosition, spawnRotation,
            respawnDelay, gameObject.scene);
    }
}

internal sealed class PackEmptyRespawnTimer : MonoBehaviour
{
    private GameObject replacement;
    private PackEmptyRespawnPoint replacementPoint;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float delay;
    private Scene sourceScene;

    public static void Schedule(GameObject replacement, PackEmptyRespawnPoint replacementPoint,
        Vector3 spawnPosition, Quaternion spawnRotation, float delay, Scene sourceScene)
    {
        var timerObject = new GameObject("PackEmptyRespawnTimer");
        var timer = timerObject.AddComponent<PackEmptyRespawnTimer>();
        timer.replacement = replacement;
        timer.replacementPoint = replacementPoint;
        timer.spawnPosition = spawnPosition;
        timer.spawnRotation = spawnRotation;
        timer.delay = delay;
        timer.sourceScene = sourceScene;
        timer.StartCoroutine(timer.ActivateAfterDelay());
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        if (replacement != null && replacementPoint != null && sourceScene.isLoaded &&
            gameObject.scene == sourceScene)
        {
            replacementPoint.ConfigureAsWorldSpawnPoint(spawnPosition, spawnRotation);
            replacement.SetActive(true);
        }
        Destroy(gameObject);
    }
}
