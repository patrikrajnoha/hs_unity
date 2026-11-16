using UnityEngine;

public class DoorInteract : MonoBehaviour
{
    public float openDistance;
    public float openRotation = 270f;
    public string openMessage = "Press E to open door";
    public string closeMessage = "Press E to close door";

    private Transform player;
    private bool isOpen = false;

    private Camera playerCamera;
    private PlayerUIManager puim;

    void Start()
    {

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
            if (distance <= openDistance)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
                Vector3 direction = (this.transform.position - playerCamera.transform.position).normalized;
                float dot = Vector3.Dot(playerCamera.transform.forward, direction);
                
                if(dot > 0.7f)
                {
                    puim = player != null ? player.GetComponent<PlayerUIManager>() : null;
                    // Show correct message depending on state
                    puim.ShowInteractionPrompt(KeyCode.E, isOpen ? "Close door" : "Open door");

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        isOpen = !isOpen;

                        if (isOpen)
                        {
                            transform.rotation = Quaternion.Euler(0, openRotation, 0);
                        }
                        else
                        {
                            transform.rotation = Quaternion.Euler(0, 0, 0);
                        }
                    }
                }
            }
        }




    }
}
