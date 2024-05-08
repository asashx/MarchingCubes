using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityGenerator : MonoBehaviour
{
    public virtual ComputeBuffer Generate(ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 worldBounds, Vector3 centre, Vector3 offset, float spacing)
    {
        return pointsBuffer;
    }
}
