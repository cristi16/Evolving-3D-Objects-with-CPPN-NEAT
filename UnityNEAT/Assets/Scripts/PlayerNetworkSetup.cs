using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using HighlightingSystem;
using SharpNeat.Genomes.Neat;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public class PlayerNetworkSetup : NetworkBehaviour
{
    public GameObject seedSelectionGfx;
    public GameObject artefactGhost;
    public float selectedSeedRotationSpeed = 6f;
    public bool destroySeedsOnPlacement = true;
    [Tooltip("How many times do we show the pick seed helper icon")]
    public int pickupIconMax = 1000;

    [Tooltip("How many times do we show the plant seed helper icon")]
    public int plantIconMax = 1000;

    [Tooltip("The maximum distance at which we can pick up seeds from")]
    public float pickUpDistance = 3f;
    [HideInInspector, SyncVar]
    public string PlayerName;
    private Ray ray;
    private RaycastHit hitInfo;

    private ArtefactEvolver evolver;
    private ScrollViewLayout scrollView;
    private PopupUIElement seedAnimation;
    private PopupUIElement pickUpIcon;
    private PopupUIElement plantIcon;
    private GameObject selectSeedKey;
    private ShowSideUI sideUI;
    private int pickupIconCount = 0;
    private int plantIconCount = 0;
    private bool hoveringOverSeed = false;
    private bool showingPlantIcon = false;
    private bool isDraggingArtefact = false;
    private List<ArtefactSeed> collectedSeeds;
    private GameObject worldCanvas;

    private List<SeedSelection> seedSelections = new List<SeedSelection>();
    private ArtefactGhost placeholderArtefact;
    private Dragable draggedObject;

    public override void OnStartServer()
    {
        base.OnStartServer();
        evolver = FindObjectOfType<ArtefactEvolver>();
    }

    public void Start()
    {
        if (isLocalPlayer == false)
        {
            GetComponent<FirstPersonController>().enabled = false;
            GetComponentInChildren<Camera>().enabled = false;
            GetComponentInChildren<AudioListener>().enabled = false;
            worldCanvas = GetComponentInChildren<Canvas>().gameObject;
        }
        else
        {
            scrollView = FindObjectOfType<ScrollViewLayout>();
            collectedSeeds = new List<ArtefactSeed>();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            CmdSetPlayerName(PlayerPrefs.GetString("PlayerName"));
            UpdateName();

            pickUpIcon = GameObject.FindGameObjectWithTag("PickUpIcon").GetComponent<PopupUIElement>();
            plantIcon = GameObject.FindGameObjectWithTag("PlantIcon").GetComponent<PopupUIElement>();
            seedAnimation = GameObject.FindGameObjectWithTag("SeedAnimation").GetComponent<PopupUIElement>();
            selectSeedKey = GameObject.FindGameObjectWithTag("SelectSeedKey");
            sideUI = GameObject.FindGameObjectWithTag("SideUI").GetComponent<ShowSideUI>();
        }
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            if(gameObject.name != PlayerName)
                UpdateName();

            var directionToPlayer = transform.position - ClientScene.localPlayers[0].gameObject.transform.position;
            if(directionToPlayer != Vector3.zero)
                worldCanvas.transform.rotation = Quaternion.LookRotation(directionToPlayer);
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F))
            GetComponent<FirstPersonController>().IsFrozen = !GetComponent<FirstPersonController>().IsFrozen;

        if (seedSelections.Count == 0)
        {
            ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out hitInfo, pickUpDistance, LayerMask.GetMask("Seed")))
            {
                hitInfo.collider.GetComponent<Highlighter>().On(Color.green);

                if (!hoveringOverSeed && pickupIconCount < pickupIconMax)
                {
                    pickUpIcon.PopUp();
                    hoveringOverSeed = true;
                    pickupIconCount++;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    hitInfo.collider.transform.rotation = Quaternion.identity;
                    hitInfo.collider.GetComponent<Rigidbody>().isKinematic = true;
                    hitInfo.collider.transform.parent = scrollView.transform;
                    hitInfo.collider.gameObject.layer = LayerMask.NameToLayer("UI");

                    hitInfo.collider.transform.localScale = Vector3.one * 4f;

                    scrollView.Reset();

                    collectedSeeds.Add(hitInfo.collider.GetComponent<ArtefactSeed>());

                    sideUI.ShowUI(collectedSeeds.Count);

                    return;
                }
            }
            else
            {
                if (hoveringOverSeed)
                {
                    pickUpIcon.PopDown();
                    hoveringOverSeed = false;
                }
            }

            if (Physics.Raycast(ray, out hitInfo, pickUpDistance, LayerMask.GetMask("Artefact")) && !isDraggingArtefact)
            {
                hitInfo.collider.GetComponent<Highlighter>().On(Color.white);

                if (Input.GetMouseButtonDown(0))
                {
                    PickupArtefact();
                    return;
                }
            }

            if (Input.GetMouseButtonDown(0) && isDraggingArtefact)
            {
                DropArtefact();
            }
        }
        else
        {
            if (hoveringOverSeed)
            {
                pickUpIcon.PopDown();
                hoveringOverSeed = false;
            }
        }

        //if (isDraggingArtefact)
        //{
        //    draggedObject.transform.Rotate(new Vector3(Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0f) * 500f * Time.deltaTime);
        //}

        if (scrollView.transform.childCount == 0) return;

        var currentlySelectedSeed = collectedSeeds[scrollView.selectedIndex];
        currentlySelectedSeed.transform.RotateAround(currentlySelectedSeed.transform.position, Vector3.up + Vector3.forward, Time.deltaTime * selectedSeedRotationSpeed);

        if (Input.GetKeyDown(KeyCode.E))
        {
            if(isDraggingArtefact)
                DropArtefact();

            selectSeedKey.GetComponent<Animation>().Play();

            int previousNumberOrSelectedSeeds = seedSelections.Count;

            if (HasSelectedSeed(currentlySelectedSeed))
            {
                DeselectSeed(currentlySelectedSeed);
                UpdatePlaceholder();
            }
            else if (seedSelections.Count < 2)
            {
                SelectSeed(currentlySelectedSeed);
                UpdatePlaceholder();
            }

            UpdateSeedAnimation(previousNumberOrSelectedSeeds);
        }

        if (Input.GetMouseButtonDown(0) && seedSelections.Count > 0)
        {
            if(seedSelections.Count == 1)
                CmdSpawnSeed(currentlySelectedSeed.ID, placeholderArtefact.transform.position, placeholderArtefact.transform.eulerAngles);
            else
                CmdSpawnFromCrossoverResult(placeholderArtefact.SerializedGenome, placeholderArtefact.transform.position, placeholderArtefact.transform.eulerAngles);    

            if (destroySeedsOnPlacement)
            {
                foreach (var seedSelection in seedSelections)
                {
                    CmdDestroySeed(seedSelection.seed.ID);
                    CmdDestroySeedObject(seedSelection.seed.netId);
                    collectedSeeds.Remove(seedSelection.seed);
                    seedSelection.seed.transform.parent = null;
                }

                if (seedSelections.Count == 1)
                {
                    if (seedSelections[0].indexInInventory > 0)
                        scrollView.MoveToIndex(seedSelections[0].indexInInventory - 1);
                }
                else
                {
                    var minIndex = seedSelections.Select(seedSelection => seedSelection.indexInInventory).Min();

                    if(minIndex > 0)
                        scrollView.MoveToIndex(minIndex - 1);
                }
            }

            Destroy(placeholderArtefact.gameObject);
            seedSelections.Clear();

            if (showingPlantIcon)
            {
                showingPlantIcon = false;
                plantIcon.PopDown();
            }
        }
    }

    [Command]
    void CmdSpawnSeed(uint seedID, Vector3 spawnPosition, Vector3 eulerAngles)
    {
        evolver.SpawnSeedFromMutation(seedID, spawnPosition, eulerAngles);
    }

    [Command]
    void CmdDestroySeed(uint seedID)
    {
        evolver.DeleteSeed(seedID);
    }

    [Command]
    void CmdDestroySeedObject(NetworkInstanceId netID)
    {
        var seedObject = NetworkServer.FindLocalObject(netID);
        NetworkServer.Destroy(seedObject);
    }

    [Command]
    void CmdSpawnFromCrossoverResult(string serializedCrossoverResult, Vector3 spawnPosition, Vector3 eulerAngles)
    {
        evolver.SpawnCrossoverResult(serializedCrossoverResult, spawnPosition, eulerAngles);
    }

    [Command]
    void CmdSetPlayerName(string name)
    {
        PlayerName = name;
    }

    private void UpdateName()
    {    
        gameObject.name = PlayerName;
        GetComponentInChildren<Text>().text = PlayerName;
    }

    private string CombineSeeds(ArtefactSeed seed1, ArtefactSeed seed2)
    {
        var genome1 =  NeatGenomeXmlIO.ReadGenome(XmlReader.Create(new StringReader(seed1.SerializedGenome)), true);
        genome1.GenomeFactory = EvolutionHelper.Instance.GenomeFactory;
        var genome2 =  NeatGenomeXmlIO.ReadGenome(XmlReader.Create(new StringReader(seed2.SerializedGenome)), true);
        genome2.GenomeFactory = EvolutionHelper.Instance.GenomeFactory;

        var result = genome1.CreateOffspring(genome2, (uint)Mathf.Max(genome1.BirthGeneration, genome2.BirthGeneration) + 1);

        return NeatGenomeXmlIO.Save(result, true).OuterXml;
    }

    private bool HasSelectedSeed(ArtefactSeed seed)
    {
        return seedSelections.Any(seedSelection => seedSelection.seed == seed);
    }

    private void SelectSeed(ArtefactSeed seed)
    {
        var selectionGfx = Instantiate(seedSelectionGfx);
        selectionGfx.transform.parent = seed.transform;
        selectionGfx.transform.localPosition = seedSelectionGfx.transform.position;
        selectionGfx.transform.localScale = new Vector3(13.6f, 7.8f, 1f);

        seedSelections.Add(new SeedSelection(seed, selectionGfx, scrollView.selectedIndex));

        plantIconCount++;
        if (!showingPlantIcon && plantIconCount < plantIconMax)
        {
            showingPlantIcon = true;
            plantIcon.PopUp();
        }
    }

    private void DeselectSeed(ArtefactSeed seed)
    {
        var index = -1;
        for(int i = 0; i < seedSelections.Count; i++)
        {
            if (seedSelections[i].seed == seed)
            {
                Destroy(seedSelections[i].selectionGfx);
                index = i;
            }
        }
        seedSelections.RemoveAt(index);

        if (showingPlantIcon && seedSelections.Count == 0)
        {
            showingPlantIcon = false;
            plantIcon.PopDown();
        }
    }

    private void UpdatePlaceholder()
    {
        switch (seedSelections.Count)
        {
            case 0:
                Destroy(placeholderArtefact.gameObject);
                break;
            case 1:
                InstantiatePlaceholder(seedSelections[0].seed.SerializedGenome);
                break;
            case 2:
                // combine 2 seeds and show the output
                var combinedGenome = CombineSeeds(seedSelections[0].seed, seedSelections[1].seed);
                InstantiatePlaceholder(combinedGenome);
                break;
            default:
                Debug.Log("Selected too many seeds!");
                return;
        }      
    }

    private void InstantiatePlaceholder(string serializedGenome)
    {
        if(placeholderArtefact != null)
            Destroy(placeholderArtefact.gameObject);

        placeholderArtefact = Instantiate(artefactGhost).GetComponent<ArtefactGhost>();
        placeholderArtefact.SerializedGenome = serializedGenome;

        var desiredPosition = Camera.main.transform.position + Camera.main.transform.forward * Dragable.k_DragDistance;
        if (desiredPosition.y < 0f)
            desiredPosition = new Vector3(desiredPosition.x, 0f, desiredPosition.z);
        placeholderArtefact.transform.position = desiredPosition;

        placeholderArtefact.GetComponent<Highlighter>().ConstantOn(Color.white);

        draggedObject = placeholderArtefact.gameObject.AddComponent<Dragable>();
        draggedObject.StartDragging();
    }

    void UpdateSeedAnimation(int previousCount)
    {
        switch (seedSelections.Count)
        {
            case 0:
                seedAnimation.transform.GetChild(2).gameObject.SetActive(false);
                break;
            case 1:
                if(previousCount == 2)
                    seedAnimation.PopDown();
                if(previousCount == 0)
                    seedAnimation.transform.GetChild(2).gameObject.SetActive(true);
                break;
            case 2:
                seedAnimation.PopUp();
                break;
        }
    }

    void PickupArtefact()
    {
        draggedObject = hitInfo.collider.gameObject.GetComponent<Dragable>();
        if(draggedObject.IsDragging) return;

        isDraggingArtefact = true;

        draggedObject.StartDragging();
        CmdChangeAuthority(draggedObject.GetComponent<NetworkIdentity>().netId, GetComponent<NetworkIdentity>().netId, true);

        hitInfo.collider.GetComponent<Highlighter>().ConstantOn(Color.white);
    }


    void DropArtefact()
    {
        isDraggingArtefact = false;

        draggedObject.StopDragging();
        CmdChangeAuthority(draggedObject.GetComponent<NetworkIdentity>().netId, GetComponent<NetworkIdentity>().netId, false);
    }

    [Command]
    void CmdChangeAuthority(NetworkInstanceId objectNetId, NetworkInstanceId playerNetId, bool assign)
    {
        var serverPlayer = NetworkServer.FindLocalObject(playerNetId);
        var serverObject = NetworkServer.FindLocalObject(objectNetId);

        if (assign)
        {
            serverObject.GetComponent<NetworkIdentity>()
                .AssignClientAuthority(serverPlayer.GetComponent<NetworkIdentity>().connectionToClient);
        }
        else
        {
            serverObject.GetComponent<NetworkIdentity>()
                .RemoveClientAuthority(serverPlayer.GetComponent<NetworkIdentity>().connectionToClient);
        }
    }
}
