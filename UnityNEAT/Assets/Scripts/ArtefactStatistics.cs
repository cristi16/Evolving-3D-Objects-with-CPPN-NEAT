using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ArtefactStatistics
{
    // This is the genome ID generated by SharpNEAT. Can be used to identify artefacts
    public uint genomeID;
    public uint generation;
    public Color color;

    public List<uint> parents;
    public List<string> usersInteracted;

    public Vector3 spawnPosition;
    public int numberOfSeedsReplanted;

    public ArtefactStatistics(uint genomeID, uint generation)
    {
        this.genomeID = genomeID;
        this.generation = generation;
        parents = new List<uint>();
        usersInteracted = new List<string>();
    }

    public void AddParents(uint parent1, uint parent2)
    {
        if(parent1 != 0 && parents.Contains(parent1) == false)
            parents.Add(parent1);

        if (parent2 != 0 && parents.Contains(parent2) == false)
            parents.Add(parent2);
    }

    public void AddUsersFromParents(uint parent1, uint parent2)
    {
        if(parent1 != 0)
            usersInteracted.AddRange(Statistics.Instance.artefacts[parent1].usersInteracted);
        if (parent2 != 0)
            usersInteracted.AddRange(Statistics.Instance.artefacts[parent2].usersInteracted);
    }
}
