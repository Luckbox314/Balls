using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class InstancedBallRenderer : MonoBehaviour
{
    public Mesh sphereMesh;
    public Material ballMaterial;
    private int instancesPerBatch = 1023;

    private List<Matrix4x4[]> chunkPool = new List<Matrix4x4[]>();
    private List<int> chunkCounts = new List<int>();

    private int usedChunkCount;

    private void AllocateChunkPoolIfNeeded(int totalInstances)
    {
        // how many chunks do we need in the worst case
        int needed = Mathf.CeilToInt((float)totalInstances / instancesPerBatch);

        // if we haven't allocated enough chunk arrays yet, allocate more
        while (chunkPool.Count < needed)
        {
            // Typically, we always allocate arrays of size 1023, 
            // but you can optimize the last chunk to match exactly if desired.
            var chunkArray = new Matrix4x4[instancesPerBatch];
            chunkPool.Add(chunkArray);
            chunkCounts.Add(0); // keep track of how many are actually used in each chunk
        }
    }

    public void UpdateBatches(Matrix4x4[] allMatrices, int count)
    {
        AllocateChunkPoolIfNeeded(count);

        int offset = 0;
        usedChunkCount = 0;
        while (offset < count)
        {
            int batchSize = Mathf.Min(instancesPerBatch, count - offset);
            System.Array.Copy(allMatrices, offset, chunkPool[usedChunkCount], 0, batchSize);
            chunkCounts[usedChunkCount] = batchSize;

            offset += batchSize;
            usedChunkCount++;
        }
    }

    void Update()
    {
        if (usedChunkCount == 0)
            return;

        for (int i = 0; i < usedChunkCount; i++)
        {
            Graphics.DrawMeshInstanced(
                sphereMesh,
                0,
                ballMaterial,
                chunkPool[i],
                chunkCounts[i]
            );
        }
    }
}
