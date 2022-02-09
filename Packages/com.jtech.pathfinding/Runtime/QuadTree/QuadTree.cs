using System.Collections.Generic;
using System.Linq;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;

namespace JTech.PathFinding.QuadTree
{

    public interface IQuadTreeData
    {
    }
    /// <summary>
    /// 四叉树节点
    /// </summary>
    public class TreeNode<T> : IQuadTreeNode where T : IQuadTreeData
    {
        public int Id { get; set; }
        internal readonly TreeNode<T>[] C = new TreeNode<T>[4];
        /// <summary>
        /// 距离边界最近的四个顶点
        /// 即最左上、左下、右上、右下的四个顶点
        /// </summary>
        internal readonly int2[] Vertex = new int2[4];
        internal readonly T[] VertexNearestEntity = new T[4];
        public int2 Min { get; set; }
        public int2 Max { get; set; }
        internal readonly HashSet<T> Objects = new HashSet<T>();
        internal readonly HashSet<T> Has = new HashSet<T>();

        /// <summary>
        /// AStar使用的估值
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// AStar使用的从起始点到当前点的距离
        /// </summary>
        public int Dist2Start { get; set; }

        /// <summary>
        /// AStar使用的路径上一个区域的id
        /// </summary>
        public IQuadTreeNode LastNode { get; set; }

        internal TreeNode(int id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// 节点对象池
    /// </summary>
    public class NodePool<T> where T : IQuadTreeData
    {
        private readonly Queue<TreeNode<T>> _queue;

        public int Count { get; private set; } = 0;

        internal NodePool(int initCapital)
        {
            Count = initCapital;
            _queue = new Queue<TreeNode<T>>();
            for (var i = 0; i < initCapital; i++)
            {
                _queue.Enqueue(new TreeNode<T>(i));
            }
        }

        internal TreeNode<T> Get(int2 min, int2 max)
        {
            var ret = _queue.Count > 0 ? _queue.Dequeue() : new TreeNode<T>(Count++);

            ret.Min = min;
            ret.Max = max;
            ret.Vertex[0] = ret.Vertex[1] = ret.Vertex[2] = ret.Vertex[3] = new int2(int.MaxValue, int.MaxValue);
            ret.Objects.Clear();
            ret.Has.Clear();
            ret.C[0] = ret.C[1] = ret.C[2] = ret.C[3] = null;
            return ret;
        }

        internal void Collect(TreeNode<T> node)
        {
            _queue.Enqueue(node);
        }
    }

    /// <summary>
    /// 四叉树
    /// </summary>
    public class QuadTree<T> : IQuadTree where T : IQuadTreeData
    {
        private readonly float _padding;
        private readonly NodePool<T> _pool;
        private readonly int2[] _delta = { new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1) };
        private TreeNode<T> _head;
        public IQuadTreeNode Head => _head;
        public int Count => _pool.Count;

        public float Resolution { get; }

        public QuadTree(float padding = 0, float resolution = 2, int initCapital = 1)
        {
            _padding = 0;
            _padding = padding;
            Resolution = resolution;
            _pool = new NodePool<T>(initCapital);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rect"></param>
        public void Init(Rect rect)
        {
            _head = _pool.Get(
                new int2((int)(rect.x * Resolution - rect.width / 2f * Resolution),
                    (int)(rect.y * Resolution - rect.height / 2f * Resolution)),
                new int2((int)math.ceil(rect.x * Resolution + rect.width / 2f * Resolution),
                    (int)math.ceil(rect.y * Resolution + rect.height / 2f * Resolution)));
        }
        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="now"></param>
        /// <param name="index"></param>
        private void CreateChildren(TreeNode<T> now, int index)
        {
            switch (index)
            {
                case 0:
                    now.C[0] = _pool.Get(now.Min, now.Min + (now.Max - now.Min) / 2);
                    break;
                case 1:
                    now.C[1] = _pool.Get(new int2(now.Min.x + (now.Max.x - now.Min.x) / 2 + 1, now.Min.y),
                        new int2(now.Max.x, now.Min.y + (now.Max.y - now.Min.y) / 2));
                    break;
                case 2:
                    now.C[2] = _pool.Get(new int2(now.Min.x, now.Min.y + (now.Max.y - now.Min.y) / 2 + 1),
                        new int2(now.Min.x + (now.Max.x - now.Min.x) / 2, now.Max.y));
                    break;
                case 3:
                    now.C[3] = _pool.Get(now.Min + (now.Max - now.Min) / 2 + new int2(1, 1), now.Max);
                    break;
            }
        }
        /// <summary>
        /// 懒操作向下维护
        /// </summary>
        /// <param name="now"></param>
        private static void DownTree(TreeNode<T> now)
        {
            if (now.Objects.Count == 0) return;
            var anyOne = now.Objects.First();
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                now.C[i].Vertex[0] = now.C[i].Min;
                now.C[i].Vertex[1] = new int2(now.C[i].Max.x, now.C[i].Min.y);
                now.C[i].Vertex[2] = new int2(now.C[i].Min.x, now.C[i].Max.y);
                now.C[i].Vertex[3] = now.C[i].Max;
                now.C[i].VertexNearestEntity[0] =
                    now.C[i].VertexNearestEntity[1] =
                        now.C[i].VertexNearestEntity[2] =
                            now.C[i].VertexNearestEntity[3] = anyOne;
                now.C[i].Objects.UnionWith(now.Objects);
                now.C[i].Has.UnionWith(now.Objects);
            }

            now.Objects.Clear();
        }

        /// <summary>
        /// 向上合并
        /// </summary>
        /// <param name="now"></param>
        private void UpTree(TreeNode<T> now)
        {
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                if (now.C[i].Has.Count != now.C[i].Objects.Count) return;
                for (var j = i + 1; j < 4; j++)
                {
                    if (now.C[j] == null) continue;
                    if (now.C[j].Has.Count != now.C[j].Objects.Count ||
                        now.C[j].Objects.Count != now.C[i].Objects.Count) return;
                    if (now.C[i].Objects.Except(now.C[j].Objects).Any()) return;
                }
            }

            now.Objects.Clear();
            now.Objects.UnionWith(now.Has);
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                _pool.Collect(now.C[i]);
                now.C[i] = null;
            }
        }

        /// <summary>
        /// 创建儿子节点
        /// </summary>
        /// <param name="now"></param>
        private void CreateChildren(TreeNode<T> now)
        {
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null) continue;
                if (i == 0 || (i == 1 && now.Max.x != now.Min.x) ||
                    (i == 2 && now.Max.y != now.Min.y) ||
                    (i == 3 && now.Max.x != now.Min.x && now.Max.y != now.Min.y))
                {
                    CreateChildren(now, i);
                }
            }
        }

        /// <summary>
        /// 当一个儿子节点更新过后更新距离四周最近的对象
        /// </summary>
        /// <param name="now"></param>
        /// <param name="child"></param>
        private void UpdateVertexNearestEntityWithChild(TreeNode<T> now, TreeNode<T> child)
        {
            for (var i = 0; i < 4; i++)
            {
                if (child.Vertex[i].x == int.MaxValue || (now.Vertex[i].x != int.MaxValue &&
                                                          math.dot(now.Vertex[i] - child.Vertex[i], _delta[i]) <= 0))
                    continue;
                now.Vertex[i] = child.Vertex[i];
                now.VertexNearestEntity[i] = child.VertexNearestEntity[i];
            }
        }

        /// <summary>
        /// 检查一个儿子是否和区域有重叠关系
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="center"></param>
        /// <param name="index">儿子索引</param>
        /// <returns></returns>
        private static bool CheckChildrenOverlapRect(in int2 min, in int2 max, in int2 center, in int index)
        {
            switch (index)
            {
                case 0:
                    return min.x <= center.x && min.y <= center.y;
                case 1:
                    return max.x > center.x && min.y <= center.y;
                case 2:
                    return min.x <= center.x && max.y > center.y;
                case 3:
                    return max.x > center.x && max.y > center.y;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 检查当前节点是否处于某个矩形中
        /// </summary>
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static bool CheckNowInRect(in TreeNode<T> now, in int2 min, in int2 max)
        {
            return min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y;
        }

        /// <summary>
        /// 清除维护的边界顶点信息
        /// </summary>
        private static void ClearVertexData(in TreeNode<T> now)
        {
            for (var i = 0; i < 4; i++)
            {
                now.Vertex[i].x = int.MaxValue;
                now.VertexNearestEntity[i] = default;
            }
        }

        /// <summary>
        /// 区域更新操作
        /// </summary>
        /// <param name="now"></param>
        /// <param name="entity"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void UpdateRect(TreeNode<T> now, T entity, in int2 min, in int2 max)
        {
            if (now.Objects.Contains(entity)) return;
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Vertex[0] = now.Min;
                now.Vertex[1] = new int2(now.Max.x, now.Min.y);
                now.Vertex[2] = new int2(now.Min.x, now.Max.y);
                now.Vertex[3] = now.Max;
                now.VertexNearestEntity[0] = now.VertexNearestEntity[1] =
                    now.VertexNearestEntity[2] = now.VertexNearestEntity[3] = entity;
                now.Objects.Add(entity);
                now.Has.Add(entity);
                return;
            }

            CreateChildren(now);

            DownTree(now);

            var center = now.Min + (now.Max - now.Min) / 2;
            var has = false;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && CheckChildrenOverlapRect(in min, in max, in center, in i))
                {
                    UpdateRect(now.C[i], entity, in min, in max);
                    UpdateVertexNearestEntityWithChild(now, now.C[i]);
                    has = true;
                }
            }

            if (has)
            {
                now.Has.Add(entity);
            }

            UpTree(now);
        }

        /// <summary>
        /// 查询一个位置附近的对象（非最优解）
        /// </summary>
        /// <param name="now"></param>
        /// <param name="pos"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        private int FindNearObject(TreeNode<T> now, in int2 pos, out T obj)
        {
            obj = default;
            if (now.Has.Count == 0) return int.MaxValue;
            if (now.Objects.Count > 0)
            {
                obj = now.Objects.First();
                var d = 0;
                if (pos.x < now.Min.x) d += now.Min.x - pos.x;
                else if (pos.x > now.Max.x) d += pos.x - now.Max.x;
                if (pos.y < now.Min.y) d += now.Min.y - pos.y;
                else if (pos.y > now.Max.y) d += pos.y - now.Max.y;
                return d;
            }
            var dist = int.MaxValue;
            T tEntity;
            if (pos.x <= now.Max.x && pos.x >= now.Min.x && pos.y <= now.Max.y && pos.y >= now.Min.y)
            {
                for (var i = 0; i < 4; i++)
                {
                    if (now.C[i] != null)
                    {
                        var tDist = FindNearObject(now.C[i], in pos, out tEntity);
                        if (tDist < dist)
                        {
                            obj = tEntity;
                            dist = tDist;
                        }
                    }
                }
            }
            else
            {
                if (pos.x < now.Min.x && pos.y < now.Min.y)
                {
                    obj = now.VertexNearestEntity[0];
                    if (now.Vertex[0].x == int.MaxValue) return int.MaxValue;
                    return math.dot(now.Vertex[0] - pos, _delta[0]);
                }

                if (pos.x > now.Max.x && pos.y < now.Min.y)
                {
                    obj = now.VertexNearestEntity[1];
                    if (now.Vertex[1].x == int.MaxValue) return int.MaxValue;
                    return math.dot(now.Vertex[1] - pos, _delta[1]);
                }

                if (pos.x < now.Min.x && pos.y > now.Max.y)
                {
                    obj = now.VertexNearestEntity[2];
                    if (now.Vertex[2].x == int.MaxValue) return int.MaxValue;
                    return math.dot(now.Vertex[2] - pos, _delta[2]);
                }

                if (pos.x > now.Max.x && pos.y > now.Max.y)
                {
                    obj = now.VertexNearestEntity[3];
                    if (now.Vertex[3].x == int.MaxValue) return int.MaxValue;
                    return math.dot(now.Vertex[3] - pos, _delta[3]);
                }

                //下面不是最优策略
                var f1 = -1;
                var f2 = -1;
                if (pos.y < now.Min.y)
                {
                    f1 = now.C[0] != null ? 0 : ((now.C[2] != null) ? 2 : -1);
                    f2 = now.C[1] != null ? 1 : ((now.C[3] != null) ? 3 : -1);
                }

                if (pos.x < now.Min.x)
                {
                    f1 = now.C[0] != null ? 0 : ((now.C[1] != null) ? 1 : -1);
                    f2 = now.C[2] != null ? 2 : ((now.C[3] != null) ? 3 : -1);
                }

                if (pos.x > now.Max.x)
                {
                    f1 = now.C[1] != null ? 1 : ((now.C[0] != null) ? 0 : -1);
                    f2 = now.C[3] != null ? 3 : ((now.C[2] != null) ? 2 : -1);
                }

                if (pos.y > now.Max.y)
                {
                    f1 = now.C[2] != null ? 2 : ((now.C[0] != null) ? 0 : -1);
                    f2 = now.C[3] != null ? 3 : ((now.C[1] != null) ? 1 : -1);
                }

                if (f1 == -1) f1 = f2;
                if (f1 == -1)
                {
                    return int.MaxValue;
                }

                var tDist = FindNearObject(now.C[f1], in pos, out tEntity);
                if (tDist < dist)
                {
                    obj = tEntity;
                    dist = tDist;
                }

                if (f2 == -1) return dist;
                tDist = FindNearObject(now.C[f2], in pos, out tEntity);
                if (tDist >= dist) return dist;
                obj = tEntity;
                dist = tDist;
            }

            return dist;
        }

        /// <summary>
        /// 寻找包含给定区域且没有实体存在的节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        public void FindNodesWithoutObjects(IQuadTreeNode node, in int2 min, in int2 max, ICollection<IQuadTreeNode> nodes)
        {
            var now = node as TreeNode<T>;
            if (now.Objects.Count > 0)
            {
                return;
            }

            if (now.Has.Count == 0)
            {
                nodes.Add(now);
                return;
            }

            DownTree(now);
            var center = now.Min + (now.Max - now.Min) / 2;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && CheckChildrenOverlapRect(in min, in max, in center, in i))
                    FindNodesWithoutObjects(now.C[i], in min, in max, nodes);
            }
        }

        /// <summary>
        /// 删除指定对象
        /// TODO: 应该添加懒操作来降低操作复杂度
        /// </summary>
        /// <param name="now"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        private void RemoveObject(TreeNode<T> now, T obj)
        {
            if (now.Has.Contains(obj) == false) return;
            now.Has.Remove(obj);
            now.Objects.Remove(obj);
            var objectsCount = now.Objects.Count;
            if (objectsCount > 0)
            {
                var anyOne = now.Objects.First();
                for (var i = 0; i < 4; i++)
                {
                    now.VertexNearestEntity[i] = anyOne;
                }
            }
            else
            {
                ClearVertexData(now);
            }
            //TODO:这段仅节省内存和加快a星速度,可以加上对对象池里面的对象个数判断来决定要不要回收节点
            if (now.Has.Count == 0)
            {
                for (var i = 0; i < 4; i++)
                {
                    if (now.C[i] == null) continue;
                    FakeClear(now.C[i]);
                    now.C[i] = null;
                }

                return;
            }
            if (objectsCount > 0) CreateChildren(now);
            DownTree(now);
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                RemoveObject(now.C[i], obj);
                if (objectsCount == 0)
                {
                    UpdateVertexNearestEntityWithChild(now, now.C[i]);
                }
            }
        }

        /// <summary>
        /// 删除指定区域内的指定对象
        /// TODO: 应该添加懒操作来降低操作复杂度
        /// </summary>
        /// <param name="now"></param>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void RemoveObjectInRect(TreeNode<T> now, T obj, in int2 min, in int2 max)
        {
            if (now.Has.Contains(obj) == false) return;
            if (CheckNowInRect(in now, in min, in max))
            {
                now.Has.Remove(obj);
                now.Objects.Remove(obj);
                if (now.Has.Count == 0)
                {
                    FakeClear(now, false);
                    ClearVertexData(now);
                    return;
                }
            }

            if (now.Objects.Count > 0)
            {
                var anyOne = now.Objects.First();
                for (var i = 0; i < 4; i++)
                {
                    now.VertexNearestEntity[i] = anyOne;
                }
            }
            else
            {
                ClearVertexData(now);
            }

            //所有区域update操作都需要创建子节点
            if (now.Objects.Count > 0) CreateChildren(now);

            DownTree(now);

            var center = now.Min + (now.Max - now.Min) / 2;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                if (CheckChildrenOverlapRect(in min, in max, in center, i))
                {
                    RemoveObjectInRect(now.C[i], obj, in min, in max);
                }
                UpdateVertexNearestEntityWithChild(now, now.C[i]);
            }

            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && now.C[i].Has.Contains(obj)) return;
            }

            now.Has.Remove(obj);
            //TODO:这段仅节省内存和加快a星速度,可以加上对对象池里面的对象个数判断来决定要不要回收节点
            if (now.Has.Count == 0)
            {
                FakeClear(now, false);
            }
        }

        /// <summary>
        /// 删除指定区域内的所有对象
        /// </summary>
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void RemoveAllObjectsInRect(TreeNode<T> now, in int2 min, in int2 max)
        {
            ClearVertexData(now);
            if (CheckNowInRect(in now, in min, in max))
            {
                now.Has.Clear();
                now.Objects.Clear();
                FakeClear(now, false);
                return;
            }

            if (now.Objects.Count > 0) CreateChildren(now);

            DownTree(now);
            var center = now.Min + (now.Max - now.Min) / 2;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && CheckChildrenOverlapRect(in min, in max, in center, in i))
                    RemoveAllObjectsInRect(now.C[i], in min, in max);
            }
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    UpdateVertexNearestEntityWithChild(now, now.C[i]);
                }
            }


            now.Has.Clear();
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null) now.Has.UnionWith(now.C[i].Has);
            }

            if (now.Has.Count == 0)
            {
                FakeClear(now, false);
            }
        }

        /// <summary>
        /// 删除指定对象
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveObject(T obj)
        {
            if (CheackInited() == false) return;
            RemoveObject(_head, obj);
        }

        /// <summary>
        /// 删除指定区域内的指定对象
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void RemoveObjectInRect(T obj, in float2 min, in float2 max)
        {
            if (CheackInited() == false) return;
            var iMin = (int2)math.round(min * Resolution);
            var iMax = (int2)math.round(max * Resolution);
            RemoveObjectInRect(_head, obj, in iMin, in iMax);
        }

        /// <summary>
        /// 删除指定区域内的所有对象
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void RemoveAllObjectsInRect(in float2 min, in float2 max)
        {
            if (CheackInited() == false) return;
            var iMin = (int2)math.round(min * Resolution);
            var iMax = (int2)math.round(max * Resolution);
            RemoveAllObjectsInRect(_head, in iMin, in iMax);
        }

        /// <summary>
        /// 检查一个点是否在儿子内部
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="center"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool CheckPosInChildren(in int2 pos, in int2 center, in int index)
        {
            switch (index)
            {
                case 0: return pos.x <= center.x && pos.y <= center.y;
                case 1: return pos.x > center.x && pos.y <= center.y;
                case 2: return pos.x <= center.x && pos.y > center.y;
                case 3: return pos.x > center.x && pos.y > center.y;
                default: return false;
            }
        }

        /// <summary>
        /// 检查一个位置是否被障碍物占据
        /// </summary>
        /// <param name="now"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private bool CheckHasAnyObject(TreeNode<T> now, in int2 pos)
        {
            if (now.Objects.Count > 0)
            {
                return true;
            }

            if (now.Max.x == now.Min.x && now.Max.y == now.Min.y)
            {
                return now.Has.Count > 0;
            }

            var center = now.Min + (now.Max - now.Min) / 2;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && CheckPosInChildren(in pos, in center, in i))
                    return CheckHasAnyObject(now.C[i], in pos);
            }

            return false;
        }

        /// <summary>
        /// 检查位置上是否被障碍障碍物占据
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool CheckHasAnyObject(float2 pos)
        {
            var iPos = new int2((int)(pos.x * Resolution), (int)(pos.y * Resolution));
            return CheckHasAnyObject(_head, in iPos);
        }

        private bool CheckHasObject(TreeNode<T> now, T obj, in int2 pos)
        {
            if (now.Objects.Contains(obj))
            {
                return true;
            }

            if (now.Has.Contains(obj) == false) return false;

            var center = now.Min + (now.Max - now.Min) / 2;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && CheckPosInChildren(in pos, in center, in i))
                    return CheckHasObject(now.C[i], obj, in pos);
            }

            return false;
        }

        /// <summary>
        /// 检查位置上是否被特定障碍对象占据
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool CheckHasObject(float2 pos, T obj)
        {
            var iPos = new int2((int)(pos.x * Resolution), (int)(pos.y * Resolution));
            return CheckHasObject(_head, obj, in iPos);
        }

        /// <summary>
        /// 添加坐标换算后的矩形区域
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void AddRect(T obj, in int2 min, in int2 max)
        {
            if (_head.Max.x < min.x || _head.Min.x > max.x || _head.Max.y < min.y || _head.Min.y > max.y) return;
            UpdateRect(_head, obj, in min, in max);
        }

        /// <summary>
        /// 检查是否已经完成初始化
        /// </summary>
        public bool CheackInited()
        {
            if (_head != null) return true;
            Debug.LogError("pls call Init method before call the other methods!");
            return false;

        }

        public void AddObject(T obj, IObstacle obstacle)
        {
            if (CheackInited() == false) return;
            var rects = obstacle.SplitToRect(_padding, Resolution, _head.Min, _head.Max);
            for (var i = 0; i < rects.Length; i++)
            {
                AddRect(obj, rects[i].xy, rects[i].zw);
            }
        }

        /// <summary>
        /// 查找和指定区域有重合部分的所有没有障碍对象存在的节点
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        internal void FindNodesWithoutObjects(in float2 min, in float2 max, List<IQuadTreeNode> nodes)
        {
            var iMin = (int2)math.round(min * Resolution);
            var iMax = (int2)math.round(max * Resolution);
            FindNodesWithoutObjects(_head, in iMin, in iMax, nodes);
        }
        
        /// <summary>
        /// 查询距离给定位置最近的实体
        /// （非最优解）
        /// </summary>
        /// <param name="pos">给定位置</param>
        /// <param name="obj">返回的最近对象</param>
        /// <returns></returns>
        public float FindNearObject(float2 pos, out T obj)
        {
            if (CheackInited() == false)
            {
                obj = default;
                return 0;
            }

            var iPos = new int2((int)(pos.x * Resolution), (int)(pos.y * Resolution));
            return FindNearObject(_head, iPos, out obj) / Resolution;
        }

        /// <summary>
        /// 清除某个节点及其所有子节点
        /// </summary>
        /// <param name="now">节点</param>
        /// <param name="clearNow"></param>
        private void FakeClear(TreeNode<T> now, bool clearNow = true)
        {
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                FakeClear(now.C[i]);
                now.C[i] = null;
            }
            if (clearNow)
                _pool.Collect(now);
        }

        /// <summary>
        /// 数据回收
        /// </summary>
        public void FakeClear()
        {
            if (CheackInited() == false) return;
            FakeClear(_head);
            _head = null;
        }

#if QUAD_TREE_DEBUG
        private void Output(TreeNode<T> now, int deep, float time)
        {
            if (now.Objects.Count > 0)
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / Resolution, 1, (now.Min.y - 0.5f) / Resolution),
                    new Vector3((now.Min.x - 0.5f) / Resolution, 1, (now.Max.y + 0.5f) / Resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / Resolution, 1, (now.Max.y + 0.5f) / Resolution),
                    new Vector3((now.Max.x + 0.5f) / Resolution, 1, (now.Max.y + 0.5f) / Resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / Resolution, 1, (now.Max.y + 0.5f) / Resolution),
                    new Vector3((now.Max.x + 0.5f) / Resolution, 1, (now.Min.y - 0.5f) / Resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / Resolution, 1, (now.Min.y - 0.5f) / Resolution),
                    new Vector3((now.Min.x - 0.5f) / Resolution, 1, (now.Min.y - 0.5f) / Resolution), Color.red, time);
            }
            else
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / Resolution, 0, (now.Min.y - 0.5f) / Resolution),
                    new Vector3((now.Min.x - 0.5f) / Resolution, 0, (now.Max.y + 0.5f) / Resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / Resolution, 0, (now.Max.y + 0.5f) / Resolution),
                    new Vector3((now.Max.x + 0.5f) / Resolution, 0, (now.Max.y + 0.5f) / Resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / Resolution, 0, (now.Max.y + 0.5f) / Resolution),
                    new Vector3((now.Max.x + 0.5f) / Resolution, 0, (now.Min.y - 0.5f) / Resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / Resolution, 0, (now.Min.y - 0.5f) / Resolution),
                    new Vector3((now.Min.x - 0.5f) / Resolution, 0, (now.Min.y - 0.5f) / Resolution), Color.black, time);
            }
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    Output(now.C[i], deep + 1, time);
                }
            }
        }
#endif

        /// <summary>
        /// 在scene视窗内绘制出四叉树地图
        /// 并打印出所有节点信息
        /// </summary>
        /// <param name="time">绘制持续时间</param>
        public void Output(float time)
        {
#if QUAD_TREE_DEBUG
            if (CheackInited() == false) return;
            Output(_head, 0, time);
#endif
        }
    }
}