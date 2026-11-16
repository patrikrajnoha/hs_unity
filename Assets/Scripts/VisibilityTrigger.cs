using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilityTrigger : MonoBehaviour
{

    private GameMaster gm;

    // Start is called before the first frame update
    void Start()
    {
        gm = FindObjectOfType<GameMaster>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {

            // Flip the bool once
            gm.isVisible = false;
            Debug.Log("Player is not visible now : " + gm.isVisible);

        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == null)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {

            // Flip the bool once
            gm.isVisible = true;
            Debug.Log("Player is visible again : " + gm.isVisible);

        }
    }

}
