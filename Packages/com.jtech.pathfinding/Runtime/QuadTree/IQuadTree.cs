using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace JTech.PathFinding.QuadTree
{
    public interface IQuadTreeNode
    {
        int Id { get; set; }
        int2 Min { get; set; }
        int2 Max { get; set; }
        /// <summary>
        /// Astar使用的估值
        /// </summary>
        int Value { get; set; }
        /// <summary>
        /// Astar使用的从起始点到当前点的距离
        /// </summary>
        int Dist2Start { get; set; }
        /// <summary>
        /// Astar使用的路径上一个区域的id
        /// </summary>
        IQuadTreeNode LastNode { get; set; }
    }
    
    public interface IQuadTree
    {
        float Resolution { get; }

        int Count { get; }

        IQuadTreeNode Head { get; }

        bool CheackInited();
        void FindNodesWithoutObjects(IQuadTreeNode head, in int2 min, in int2 max, ICollection<IQuadTreeNode> output);
    }
}