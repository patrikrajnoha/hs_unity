using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class LampSwitch : MonoBehaviour
{
    public float switchDistance;
    public string popupMessage = "Press E to toggle lamp";

    private Transform player;
    private Light myLight;
    private bool isOn;
    private PlayerUIManager puim;
    private Camera playerCamera;

    private Transform targetTransform; // this will be parent or self

    void Start()
    {
        //playerCamera = Camera.main;
        //player = GameObject.FindGameObjectWithTag("Player").transform;
        myLight = GetComponent<Light>();
        isOn = myLight != null && myLight.enabled;

        

        // Target the actual fixture/switch. Using the parent made every
        // generated safehouse lamp aim at the whole farming corner.
        targetTransform = transform;
    }

    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(player.transform.position, transform.position);


            // only allow the one actually in range to pick it up
            if (distance <= switchDistance)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
                Vector3 direction = (targetTransform.position - playerCamera.transform.position).normalized;
                float dot = Vector3.Dot(playerCamera.transform.forward, direction);

                if (dot > 0.95f) // more forgiving
                {
                    puim = player != null ? player.GetComponent<PlayerUIManager>() : null;
                    if (puim != null)
                        puim.ShowInteractionPrompt(KeyCode.E,
                            LocalizationManager.GetOrFallback("ui.action.toggle_lamp", "Toggle lamp"));

                    if (Input.GetKeyDown(KeyCode.E) && myLight != null)
                    {
                        isOn = !isOn;
                        myLight.enabled = isOn;
                    }
                    return;
                }
            }
        }

        

    }
}
