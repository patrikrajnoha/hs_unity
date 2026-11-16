using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PackingSys : MonoBehaviour
{

    private Item item;
    private PickableObject po;
    public string msg = "Press Q to pack weed";
    public GameObject pack;
    private GameMaster gm;
    public bool isPacking;

    private PlayerUIManager puim;

    // Start is called before the first frame update
    void Start()
    {
        gm = FindObjectOfType<GameMaster>();
        puim = this.GetComponent<PlayerUIManager>();

    }

    // Update is called once per frame
    void Update()
    {
        if (GameMaster.GameOverActive)
            return;

        Inventory inventory = this.GetComponent<Inventory>();
        Item selectedItem = inventory.GetSelectedItem(); // or inventory.SelectedItem

        if(selectedItem != null)
        {
            if (selectedItem.name == "PackEmpty" && inventory.HasItemByName("GreenBud"))
            {
                puim.ShowInteractionPrompt(KeyCode.Q, "Pack crop");
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    inventory.RemoveItemByName("GreenBud", 1);
                    inventory.RemoveItemByName("PackEmpty", 1);
                    Vector3 spawnPos = transform.position + transform.forward * 2f;

                    GameObject go = Instantiate(pack, spawnPos, Quaternion.identity);
                    var pck = go.GetComponent<PickableObject>();
                    pck.spawned = true;
                    isPacking = true;
                    if (!gm.isChasingSomeone && gm.criminal == null && gm.isVisible)
                    {
                        Debug.Log("[ps] criminal is set to player cause he is packing");
                        gm.criminal = this.gameObject;
                        gm.crimeHappened = true;
                    }
                }
            }
        }
        

    }
}
