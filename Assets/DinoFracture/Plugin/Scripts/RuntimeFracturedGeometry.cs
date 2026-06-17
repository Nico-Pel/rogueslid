using UnityEngine;
using System.Collections;
using System.Linq;

namespace DinoFracture
{
    /// <summary>
    /// Apply this component to any game object you wish to fracture while
    /// running in game mode.  Runtime fractures will produce a unique 
    /// set of pieces with each fracture.  However, this is at the cost of 
    /// computational time.  It is recommended that both the piece count
    /// and poly count are kept low.  This component is most effective when
    /// FractureRadius is set to a value in-between 0 and 1.
    /// </summary>
    public class RuntimeFracturedGeometry : FractureGeometry
    {
        /// <summary>
        /// If true, the fracture operation is performed on a background thread 
        /// and may not be finished by the time the fracture call returns. A
        /// couple of frames can go by from the time of the fracture to when
        /// the pieces are ready.  If this is false, the fracture will guaranteed
        /// be complete by the end of the call, but the game will be paused while
        /// the fractures are being created.  
        /// </summary>
        /// <remarks>It is recommended to set asynchronous to true whenever possible.</remarks>
        public bool Asynchronous = true;

        protected override AsyncFractureResult FractureInternal(Vector3 localPos)
        {
            if (NumGenerations == 0 || IsProcessingFracture)
            {
                return null;
            }

            if (FractureType == FractureType.Shatter)
            {
                ShatterDetails details = new ShatterDetails();
                details.NumPieces = NumFracturePieces;
                details.NumIterations = NumIterations;
                details.EvenlySizedPieces = EvenlySizedPieces;
                details.UVScale = UVScale;
                details.IssueResolution = FractureIssueResolution.ReplaceMeshCollider;
                details.FractureCenter = localPos;
                details.FractureRadius = FractureRadius;
                details.Asynchronous = Asynchronous;
                details.SeparateDisjointPieces = SeparateDisjointPieces;
                details.RandomSeed = RandomSeed;

                return Fracture(details, true);
            }
            else if (FractureType == FractureType.Slice)
            {
                SliceDetails details = new SliceDetails();
                details.SlicingPlanes.AddRange(SlicePlanes.Select(p => p.ToSlicePlane()));
                details.UVScale = UVScale;
                details.IssueResolution = FractureIssueResolution.ReplaceMeshCollider;
                details.Asynchronous = Asynchronous;
                details.SeparateDisjointPieces = SeparateDisjointPieces;

                return Fracture(details, true);
            }
            else
            {
                Debug.LogError("Unknown FractureType");
                return null;
            }
        }
    }
}