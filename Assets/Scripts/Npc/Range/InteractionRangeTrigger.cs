using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class InteractionRangeTrigger : MonoBehaviour
{
    private NpcInteraction npc;
    //private PoliceBehavior policeBehavior;

    private GameMaster gm;
    private PlayerUIManager puim;

    void Awake()
    {
        gm = FindObjectOfType<GameMaster>();
        npc = GetComponentInParent<NpcInteraction>();
        //policeBehavior = GetComponentInParent<PoliceBehavior>();
    }

    private void Update()
    {
        if (npc != null && npc.currentPlayer != null)
        {
            puim = npc.currentPlayer.GetComponent<PlayerUIManager>();
        }
    }

    private List<string> suspectsInZone = new List<string>();

    private void OnTriggerStay(Collider other)
    {
        TryArrest(other);
    }

    private void TryArrest(Collider other)
    {
        if (gm == null) gm = GameMaster.Instance;
        if (npc == null || gm == null || gm.arrest || !npc.chase || gm.criminal == null) return;
        PlayerController target = other.GetComponentInParent<PlayerController>();
        if (target == null || target.gameObject != gm.criminal) return;
        PoliceBehavior officer = GetComponentInParent<PoliceBehavior>();
        if (officer == null || Vector3.Distance(officer.transform.position, target.transform.position) > 1.8f ||
            !officer.CanSeeTarget(target.transform, 2f)) return;
        npc.isClose = true;
        npc.currentPlayer = target.gameObject;
        gm.arrest = true;
        target.StopMove();
        officer.Arrest();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        npc ??= GetComponentInParent<NpcInteraction>();
        if (npc == null)
            return;

        NpcInteraction otherNpc = other.GetComponentInParent<NpcInteraction>();

        // Ignore environment or unrelated colliders early
        if (!other.CompareTag("Player") && otherNpc == null)
            return;

        if (otherNpc != null || other.CompareTag("Player"))
        {
            TryArrest(other);

            // Check if the object has a Suspect component
            Suspect suspect = other.GetComponent<Suspect>() ?? other.GetComponentInParent<Suspect>();
            if (!npc.isClose)
            {
                if (suspect != null)
                {
                    
                    if (suspect.suspectName == "Player")
                    {
                        PlayerController playerController = other.GetComponentInParent<PlayerController>();
                        GameObject playerObject = playerController != null ? playerController.gameObject : other.gameObject;
                        npc.currentPlayer = playerObject;
                        npc.playerCamera = playerObject.GetComponentInChildren<Camera>(true);
                        if (npc.playerCamera == null)
                            npc.playerCamera = other.GetComponentInParent<Camera>();
                        if (npc.playerCamera == null && playerController != null && playerController.playerCamera != null)
                            npc.playerCamera = playerController.playerCamera.GetComponent<Camera>();
                        if (npc.playerCamera == null)
                            npc.playerCamera = Camera.main;
                        if (npc.playerCamera == null)
                            return;

                        // Store the suspect's name
                        suspectsInZone.Add(suspect.suspectName);
                        Vector3 direction = (this.transform.position - npc.playerCamera.transform.position).normalized;
                        if (Vector3.Dot(npc.playerCamera.transform.forward, direction) > 0.95f && suspect.suspectName == "Player")
                        {
                            if (!npc.isInteracting)
                            {
                                npc.isClose = true;
                                npc.currentNpcIner = true;

                                //Debug.Log("[interRange] current player is on : " + transform.parent.name + " and the current player is : " + npc.currentPlayer);
                                // The NPC stops when a dialogue actually begins.

                                /*
                                // HERE I WILL PROBABLY MAKE THE RECTRUIT ACTIVATION LOGIC BASED ON STAGE
                                if (GameStageManager.Instance.currentStage == GameStage.Starter && npc.GetComponent<GangBehavior>())
                                {
                                    Debug.Log("You r still just a small fish");
                                }
                                if (GameStageManager.Instance.currentStage == GameStage.SmallDealer)
                                {
                                    Debug.Log("Wowow progress man gj u r known dealer already ");
                                }
                                */



                            }

                        }
                        return;
                    }
                }

                if (otherNpc == null)
                    return;


                if (!npc.isInteracting && (!npc.isClose || !otherNpc.isClose))
                {

                    if (npc.chase || otherNpc.chase)
                    {
                        return;
                    }

                    if (npc.isClose || otherNpc.isClose)
                    {
                        return;
                    }


                    /*

                    float chance = 0.1f; // 10% base chance

                    //If this NPC is flagged as suspect raise the chance to 30-40 %
                    if(suspect != null)
                    {
                        chance = Random.Range(0.3f, 0.4f);
                    }

                    if(Random.value < chance)
                    {
                        // Store the suspect's name
                        suspectsInZone.Add(suspect.suspectName);
                        isInteracting = true;
                        other.gameObject.GetComponent<NpcInteraction>().isInteracting = true;
                        StartCoroutine(InteractionRoutine());
                        npcMoveMesh.canMove = false;
                    }
                    */

                    // Store the suspect's name
                    if (suspect != null)
                    {
                        suspectsInZone.Add(suspect.suspectName);
                    }

                    if(this.GetComponentInParent<Suspect>() != null && gm != null && gm.criminal == null)
                    {
                        gm.criminal = npc.npcGo;
                        //Debug.Log("THIS moj suspect.. : " + gm.criminal);
                    }
                    if(suspect != null && gm != null && gm.criminal == null)
                    {
                        gm.criminal = other.gameObject;
                        //Debug.Log("OTHER moj suspect.. : " + gm.criminal);
                    }

                    if (gm != null && gm.criminal != null)
                    {
                        if(npc.gameObject.GetComponent<BuyerBehavior>() != null || other.gameObject.GetComponent<BuyerBehavior>() != null)
                        {
                            if (this.GetComponentInParent<GangBehavior>() != null)
                            {
                                this.GetComponentInParent<GangBehavior>().NpcDeal();
                                //StartCoroutine(InteractionRoutine());
                            }
                            if (other.GetComponent<GangBehavior>() != null)
                            {
                                other.GetComponent<GangBehavior>().NpcDeal();
                                //StartCoroutine(InteractionRoutine());
                            }

                            if (this.GetComponentInParent<RecruitBehavior>() != null)
                            {
                                this.GetComponentInParent<RecruitBehavior>().TryAutoSell();
                                //StartCoroutine(InteractionRoutine());
                            }
                            if (other.GetComponent<RecruitBehavior>() != null)
                            {
                                other.GetComponent<RecruitBehavior>().TryAutoSell();
                                //StartCoroutine(InteractionRoutine());
                            }


                        }

                    }


                    npc.isInteracting = true;
                    otherNpc.isInteracting = true;
                    if (!npc.deal && (!npc.isClose || !otherNpc.isClose))
                    {
                        StartCoroutine(InteractionRoutine());
                    }                    


                    if (this.gameObject.GetComponentInParent<NPCWanderRoadOnly>() != null)
                    {
                        this.gameObject.GetComponentInParent<NPCWanderRoadOnly>().StopAgent();
                    }
                    if (other.gameObject.GetComponent<NPCWanderRoadOnly>() != null)
                    {
                        other.gameObject.GetComponent<NPCWanderRoadOnly>().StopAgent();
                    }



                }
            }
        }
        

    }

    private void OnTriggerExit(Collider other)
    {
        /*
        if (this.gameObject.GetComponentInParent<NPCWanderRoadOnly>() != null)
        {
            this.gameObject.GetComponentInParent<NPCWanderRoadOnly>().canMove = true;
        }

        if (other.gameObject.GetComponent<NPCWanderRoadOnly>() != null)
        {
            other.gameObject.GetComponent<NPCWanderRoadOnly>().canMove = true;
        }
        */

        Suspect suspect = other.GetComponent<Suspect>();
        if (npc.isInteracting)
        {

            // Remove the suspect from the list when leaving
            if (suspect != null)
            {
                suspectsInZone.Remove(suspect.suspectName);
            }


            // police didnt see the crime so we remove the criminal GO to follow...
            if (!gm.isChasingSomeone)
            {
                //Debug.Log("police wasnt in the range to see the crime so the criminal is null..");
                gm.criminal = null;
                gm.crimeHappened = false;
                //Debug.Log("uz by mal byt asi null triggerEXIT : " + gm.criminal);
            }



            if (other.gameObject.GetComponent<NpcInteraction>() != null)
            {
                other.gameObject.GetComponent<NpcInteraction>().isInteracting = false;
            }
            
            npc.isInteracting = false;
            if (other.gameObject.GetComponent<NPCWanderRoadOnly>() != null)
            {

                other.gameObject.GetComponent<NPCWanderRoadOnly>().ResumeMovement();
                
            }

        }
        // Remove the suspect from the list when leaving
        if (suspect != null)
        {
            suspectsInZone.Remove(suspect.suspectName);
        }

        // resume the movement when leaving 
        bool dialogueActive = npc.IsDialogueActive;

        if(other.gameObject.GetComponent<PlayerController>() != null && npc.isClose && !dialogueActive)
        {
            other.gameObject.GetComponent<PlayerController>().LockMouse();
            other.gameObject.GetComponent<PlayerController>().ResumeMove();
        }
        
        if(other.gameObject.tag == "Player")
        {
            this.gameObject.GetComponentInParent<NPCWanderRoadOnly>().ResumeMovement();
        }
        
        if(other.gameObject.tag == "Player" && npc.isClose && !dialogueActive)
        {
            npc.EndDialogue();
            npc.currentPlayer = null;
            npc.currentNpcIner = false;
            
            npc.interactionMessage = NpcDialogue.Get("interaction.prompt");
            npc.isClose = false;
            if (puim != null)
            {
                puim.selectedTradeItem = null;  // clear memory
                if (puim.selectedItemImage != null)
                {
                    puim.selectedItemImage.sprite = null; // remove sprite
                    puim.selectedItemImage.enabled = false;
                }

                // Defer all UI deactivation until after the physics callback. TMP_InputField
                // uses DestroyImmediate internally on disable, which Unity forbids here.
                StartCoroutine(ResetPlayerInteractionUI(puim));
            }
        }

        this.gameObject.GetComponentInParent<NPCWanderRoadOnly>().ResumeMovement();

    }

    private IEnumerator ResetPlayerInteractionUI(PlayerUIManager ui)
    {
        yield return null;

        if (ui == null)
            yield break;

        ui.HidePopup();
        ui.HideStealUI();

        // Buyer interaction UI
        if (ui.yesButton != null) ui.yesButton.SetActive(false);
        if (ui.noButton != null) ui.noButton.SetActive(false);
        if (ui.inputPriceGO != null) ui.inputPriceGO.SetActive(false);
        if (ui.lockPanel != null) ui.lockPanel.SetActive(false);

        // Recruit interaction UI
        if (ui.dropButton != null) ui.dropButton.SetActive(false);
        if (ui.itemImgGo != null) ui.itemImgGo.SetActive(false);
        if (ui.popupImageBorder != null) ui.popupImageBorder.SetActive(false);
        if (ui.inputQuantityGO != null) ui.inputQuantityGO.SetActive(false);
    }

    private IEnumerator InteractionRoutine()
    {
        Debug.Log("interaction animation... on this object.. : " + transform.parent.gameObject);
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


        npc.isInteracting = false;

        this.gameObject.GetComponentInParent<NPCWanderRoadOnly>().ResumeMovement();
    }


}
