using Unity.Collections;
using Unity.Mathematics;

namespace JTech.PathFinding.QuadTree
{
    /// <summary>
    /// 四叉树
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct NativeQuadTreeNode
    {
        /// <summary>
        /// id
        /// </summary>
        internal int Id;
        /// <summary>
        /// 数据
        /// 维护数据平均值
        /// </summary>
        internal float4 Data;

        /// <summary>
        /// 子节点ID
        /// </summary>
        internal int childId0, childId1, childId2, childId3;

        internal int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return childId0;
                    case 1:
                        return childId1;
                    case 2:
                        return childId2;
                    case 3:
                        return childId3;
                }

                return -1;
            }
            set
            {
                switch (index)
                {
                    case 0:
                        childId0 = value;
                        break;
                    case 1:
                        childId1 = value;
                        break;
                    case 2:
                        childId2 = value;
                        break;
                    case 3:
                        childId3 = value;
                        break;
                }
            }
        }
        
        internal int2 Min;
        internal int2 Max;
        /// <summary>
        /// 标记节点被染色
        /// </summary>
        internal bool Flag;
        /// <summary>
        /// 标记节点部分被染色
        /// </summary>
        internal bool Has;

        internal NativeQuadTreeNode(int id, float4 data, int2 min, int2 max, Allocator allocator)
        {
            childId0 = childId1 = childId2 = childId3 = -1;
            Id = id;
            Data = data;
            Min = min;
            Max = max;
            Flag = Has = false;
        }
    }
}