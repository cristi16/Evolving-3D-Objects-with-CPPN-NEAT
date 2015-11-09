using System.Collections.Generic;
using SharpNeat.Genomes.Neat;
using UnityEngine;
using UnityEngine.Networking;

// This class is Server-Side Only
public class ArtefactEvolver : NetworkBehaviour
{
    public GameObject artefactPrefab;
    public GameObject seedPrefab;

    private EvolutionHelper evolutionHelper;
    // maps seed unique ID to seed genome
    private Dictionary<uint, NeatGenome> seedsDictionary = new Dictionary<uint, NeatGenome>();
    private const int k_numberOfInputs = 4;
    private const int k_numberOfOutputs = 1;
    private const int k_numberOfSeeds = 5;

    private uint idCount;

    public override void OnStartServer()
    {
        base.OnStartServer();

        evolutionHelper = new EvolutionHelper(k_numberOfInputs, k_numberOfOutputs);

        // Spawn Initial Artefact
        var initialGenome = evolutionHelper.CreateInitialGenome();

        SpawnArtefactWithSeeds(initialGenome);
    }

    private void SpawnArtefactWithSeeds(NeatGenome genome)
    {
        // Spawn Parent
        var artefactInstance = CreateArtefactInstance<Artefact>(genome, artefactPrefab, Vector3.up*5f);
        NetworkServer.Spawn(artefactInstance.gameObject);
        // Spawn Seeds
        for(int i = 0; i < k_numberOfSeeds; i++)
        {
            var seedGenome = evolutionHelper.MutateGenome(genome);
            var direction = Quaternion.Euler(0f, (360f / k_numberOfSeeds) * i, 0f) * Vector3.forward;

            var seedInstance = CreateArtefactInstance<ArtefactSeed>(seedGenome, seedPrefab, direction * 5f);
            seedInstance.ID = GenerateSeedID();

            NetworkServer.Spawn(seedInstance.gameObject);

            seedsDictionary.Add(seedInstance.ID, seedGenome);       
        }
    }

    private T CreateArtefactInstance<T>(NeatGenome genome, GameObject prefab ,Vector3 initialPosition) where T: Artefact
    {
        var artefactInstance = Instantiate(prefab);

        var serializedGenome = NeatGenomeXmlIO.Save(genome, true).OuterXml;
        //var byteCount = System.Text.ASCIIEncoding.ASCII.GetByteCount(doc.OuterXml);
        //Debug.LogWarning("Byte count: " + byteCount); 

        var artefact = artefactInstance.GetComponent<T>();
        artefact.SerializedGenome = serializedGenome;
        artefact.transform.position = initialPosition;
        return artefact;
    }

    private uint GenerateSeedID()
    {
        return ++idCount;
    }
}
