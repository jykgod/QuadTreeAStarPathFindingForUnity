﻿using System.Collections.Generic;
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
    public class TreeNode<T> where T : IQuadTreeData
    {
        internal readonly int Id;
        internal readonly TreeNode<T>[] C = new TreeNode<T>[4];
        /// <summary>
        /// 距离边界最近的四个顶点
        /// 即最左上、左下、右上、右下的四个顶点
        /// </summary>
        internal readonly int2[] Vertex = new int2[4];
        internal readonly T[] VertexNearestEntity = new T[4];
        internal int2 Min;
        internal int2 Max;
        internal readonly HashSet<T> Objects = new HashSet<T>();
        internal readonly HashSet<T> Has = new HashSet<T>();

        /// <summary>
        /// AStar使用的估值
        /// </summary>
        internal int Value;

        /// <summary>
        /// AStar使用的从起始点到当前点的距离
        /// </summary>
        internal int Dist2Start;

        /// <summary>
        /// AStar使用的路径上一个区域的id
        /// </summary>
        internal TreeNode<T> LastNode;

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

        public int count { get; private set; } = 0;

        internal NodePool(int initCapital)
        {
            count = initCapital;
            _queue = new Queue<TreeNode<T>>();
            for (var i = 0; i < initCapital; i++)
            {
                _queue.Enqueue(new TreeNode<T>(i));
            }
        }

        internal TreeNode<T> Get(int2 min, int2 max)
        {
            var ret = _queue.Count > 0 ? _queue.Dequeue() : new TreeNode<T>(count++);

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
    public class QuadTree<T> where T : IQuadTreeData
    {
        private readonly float _offset = 0;
        private readonly NodePool<T> _pool;
        private readonly int2[] _delta = new int2[] { new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1) };
        private TreeNode<T> _head;

        public int count => _pool.count;

        public float scale { get; }

        public QuadTree(float offset = 0, float resolution = 2, int initCapital = 1)
        {
            _offset = offset;
            scale = resolution;
            _pool = new NodePool<T>(initCapital);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rect"></param>
        public void Init(Rect rect)
        {
            _head = _pool.Get(
                new int2((int)(rect.x * scale - rect.width / 2f * scale),
                    (int)(rect.y * scale - rect.height / 2f * scale)),
                new int2((int)math.ceil(rect.x * scale + rect.width / 2f * scale),
                    (int)math.ceil(rect.y * scale + rect.height / 2f * scale)));
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

            int2 center = now.Min + (now.Max - now.Min) / 2;
            bool has = false;
            for (int i = 0; i < 4; i++)
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
                for (int i = 0; i < 4; i++)
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
                int f1 = -1;
                int f2 = -1;
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
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        private void FindNodesWithoutObjects(TreeNode<T> now, in int2 min, in int2 max, ICollection<TreeNode<T>> nodes)
        {
            if (_visitNewNode && _visited[now.Id]) return;
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
                for (int i = 0; i < 4; i++)
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

            int2 center = now.Min + (now.Max - now.Min) / 2;
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    if (CheckChildrenOverlapRect(in min, in max, in center, i))
                    {
                        RemoveObjectInRect(now.C[i], obj, in min, in max);
                    }
                    UpdateVertexNearestEntityWithChild(now, now.C[i]);
                }
            }

            for (int i = 0; i < 4; i++)
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
            for (int i = 0; i < 4; i++)
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
            var iMin = (int2)math.round(min * scale);
            var iMax = (int2)math.round(max * scale);
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
            var iMin = (int2)math.round(min * scale);
            var iMax = (int2)math.round(max * scale);
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
            var iPos = new int2((int)(pos.x * scale), (int)(pos.y * scale));
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
            var iPos = new int2((int)(pos.x * scale), (int)(pos.y * scale));
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
        private bool CheackInited()
        {
            if (_head != null) return true;
            Debug.LogError("pls call Init method before call the other methods!");
            return false;

        }

        /// <summary>
        /// 添加圆形区域对象
        /// 这里做简单细分，容忍误差
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="radius"></param>
        /// <param name="pos"></param>
        public void AddCircleObject(T obj, in float radius, in float2 pos)
        {
            if (CheackInited() == false) return;
            const float f1 = 2 - math.SQRT2;
            var r = (radius + _offset) * scale;
            if (f1 * r < 2)
            {
                var min = new int2((int)(pos.x * scale - r), (int)(pos.y * scale - r));
                var max = new int2((int)math.ceil(pos.x * scale + r),
                    (int)math.ceil(pos.y * scale + r));
                AddRect(obj, min, max);
            }
            else if (f1 * r < 4)
            {
                var min = new int2((int)math.floor(pos.x * scale - r * math.SQRT2 * 0.5f), (int)math.floor(pos.y * scale - r * math.SQRT2 * 0.5f));
                var max = new int2((int)math.ceil(pos.x * scale + r * math.SQRT2 * 0.5f), (int)math.ceil(pos.y * scale + r * math.SQRT2 * 0.5f));

                var tMin = new int2((int)math.floor(pos.x * scale - r), min.y);
                var tMax = new int2((int)math.ceil(pos.x * scale + r), max.y);
                AddRect(obj, tMin, tMax); //left|center|right

                tMin = new int2(min.x, max.y);
                tMax = new int2(max.x, (int)math.ceil(pos.y * scale + r));
                AddRect(obj, tMin, tMax); //top

                tMin = new int2(min.x, (int)math.floor(pos.y * scale - r));
                tMax = new int2(max.x, min.y);
                AddRect(obj, tMin, tMax); //bottom
            }
            else
            {
                var off0 = (int)(f1 * r * 0.5);
                var min = new int2((int)math.floor(pos.x * scale - r * math.SQRT2 * 0.5f), (int)math.floor(pos.y * scale - r * math.SQRT2 * 0.5f)) - new int2(off0 * 0.25);
                var max = new int2((int)math.ceil(pos.x * scale + r * math.SQRT2 * 0.5f), (int)math.ceil(pos.y * scale + r * math.SQRT2 * 0.5f)) + new int2(off0 * 0.25f);
                AddRect(obj, min, max); //center

                off0 += 1;
                var tMin = new int2((int)math.floor(pos.x * scale - r), min.y + off0);
                var tMax = new int2(min.x, max.y - off0);
                AddRect(obj, tMin, tMax); //left

                tMin = new int2(min.x + off0, max.y);
                tMax = new int2(max.x - off0, (int)math.ceil(pos.y * scale + r));
                AddRect(obj, tMin, tMax); //top

                tMin = new int2(max.x, min.y + off0);
                tMax = new int2((int)math.ceil(pos.x * scale + r), max.y - off0);
                AddRect(obj, tMin, tMax); //right

                tMin = new int2(min.x + off0, (int)math.floor(pos.y * scale - r));
                tMax = new int2(max.x - off0, min.y);
                AddRect(obj, tMin, tMax); //bottom
            }
        }

        /// <summary>
        /// 添加平行于坐标轴的矩形区域
        /// ps:
        ///     因为非平行于坐标轴的矩形需要做细分，效率会降低，
        ///     所以能够使用这个方法的情况就不要用添加任意矩形的方法。
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        public void AddParallelRectObject(T obj, in float2 halfSize, in float2 pos)
        {
            if (CheackInited() == false) return;
            var min = new int2((int)(pos.x * scale - (halfSize.x + _offset) * scale), (int)(pos.y * scale - (halfSize.y + _offset) * scale));
            var max = new int2((int)math.ceil(pos.x * scale + (halfSize.x + _offset) * scale),
                (int)math.ceil(pos.y * scale + (halfSize.y + _offset) * scale));
            min = math.clamp(min, _head.Min, _head.Max);
            max = math.clamp(max, _head.Min, _head.Max);
            AddRect(obj, min, max);
        }

        private readonly BinaryHeap<int2> _rectClipHeap = new BinaryHeap<int2>((a, b) => a.x < b.x || (a.x == b.x && a.y < b.y));
        /// <summary>
        /// 添加任意矩形区域
        /// ps:
        ///     如果添加的矩形平行于最标轴或近似平行于坐标轴请使用AddParallelRectEntity函数添加该对象。
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        /// <param name="forward">xz平面下对象本地z轴的世界朝向</param>
        public void AddRectObject(T obj, in float2 halfSize, in float2 pos, in float2 forward)
        {
            if (CheackInited() == false) return;
            _rectClipHeap.FakeClear();
            var lenForward = math.length(forward);
            var cosA = forward.y / lenForward;
            var sinA = forward.x / lenForward;
            var points = new int2[4];
            var rotateMatrix = new float2x2(cosA, -sinA, sinA, cosA);
            var hs = halfSize + new float2(_offset, _offset);
            for (var i = 0; i < 4; i++)
            {
                points[i] = (int2)math.round((math.mul(hs * _delta[i], rotateMatrix) + pos) * scale);
            }

            points[2] += points[3];
            points[3] = points[2] - points[3];
            points[2] -= points[3];

            for (var i = 0; i < 4; i++)
            {
                var s = points[i];
                var t = points[(i + 1) % 4];
                var d = t - s;
                if (d.x == 0 && d.y == 0)
                {
                    AddRect(obj, s, s);
                    continue;
                }

                var dx = math.abs(d.x);
                var dy = math.abs(d.y);
                int2 sign;
                sign.x = s.x > t.x ? -1 : 1;
                sign.y = s.y > t.y ? -1 : 1;
                if (dx > dy)
                {
                    var sy = 0f;
                    for (var j = 0; j <= dx; j++)
                    {
                        _rectClipHeap.Push(s);
                        sy += dy;
                        if (sy >= dx)
                        {
                            s += sign;
                            sy -= dx;
                        }
                        else
                        {
                            s.x += sign.x;
                        }

                        if (s.x == t.x && s.y == t.y) break;
                    }
                }
                else
                {
                    var sx = 0f;
                    for (var j = 0; j <= dy; j++)
                    {
                        _rectClipHeap.Push(s);
                        sx += dx;
                        if (sx >= dy)
                        {
                            s += sign;
                            sx -= dy;
                        }
                        else
                        {
                            s.y += sign.y;
                        }

                        if (s.x == t.x && s.y == t.y) break;
                    }
                }
            }

            var nowX = int.MaxValue;
            var min = int2.zero;
            var max = int2.zero;
            var lastMin = int2.zero;
            var lastMax = int2.zero;
            lastMax.y = -1;
            var minUp = 0;
            var maxUp = 0;
            while (_rectClipHeap.Count > 0)
            {
                var p = _rectClipHeap.Pop();
                if (nowX != p.x)
                {
                    if (nowX == int.MaxValue)
                    {
                        min = max = p;
                    }
                    else
                    {
                        if (lastMin.y > lastMax.y)
                        {
                            lastMin = min;
                            lastMax = max;
                            min = max = p;
                        }
                        else
                        {
                            if ((minUp == 0 || minUp + lastMin.y == min.y || lastMin.y == min.y) && (maxUp == 0 || maxUp + lastMax.y == max.y || lastMax.y == max.y))
                            {
                                if (minUp == 0)
                                {
                                    minUp = min.y - lastMin.y;
                                }
                                if (maxUp == 0)
                                {
                                    maxUp = max.y - lastMax.y;
                                }
                                min = max = p;
                            }
                            else
                            {
                                lastMin.y = minUp < 0 ? lastMin.y + minUp : lastMin.y;
                                lastMax.y = maxUp > 0 ? lastMax.y + maxUp : lastMax.y;
                                lastMax.x = max.x - 1;
                                AddRect(obj, lastMin, lastMax);
                                lastMax = max;
                                lastMin = min;
                                minUp = maxUp = 0;
                                min = max = p;
                            }
                        }
                    }
                    nowX = p.x;
                }
                else
                {
                    max.y = p.y;
                }
            }

            if (lastMax.y < lastMin.y)
            {
                AddRect(obj, min, max);
            }
            else
            {
                lastMin.y = minUp < 0 ? lastMin.y + minUp : lastMin.y;
                lastMax.y = maxUp > 0 ? lastMax.y + maxUp : lastMax.y;
                lastMax.x = max.x;
                AddRect(obj, lastMin, lastMax);
            }
        }



        private readonly BinaryHeap<TreeNode<T>> _open = new BinaryHeap<TreeNode<T>>((a, b) => a.Value < b.Value);
        private readonly List<TreeNode<T>> _tempList = new List<TreeNode<T>>();
        private Stack<float2> _ans = new Stack<float2>();
        private bool[] _visited;
        private bool _visitNewNode;

        /// <summary>
        /// 查找和指定区域有重合部分的所有没有障碍对象存在的节点
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        internal void FindNodesWithoutObjects(in float2 min, in float2 max, List<TreeNode<T>> nodes)
        {
            var iMin = (int2)math.round(min * scale);
            var iMax = (int2)math.round(max * scale);
            _visitNewNode = false;
            FindNodesWithoutObjects(_head, in iMin, in iMax, nodes);
        }
        /// <summary>
        /// A星寻路
        /// 求出来的并非最优解
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public Stack<float2> AStar(float2 start, float2 end)
        {
            if (CheackInited() == false) return null;
            var iStart = new int2((int)(start.x * scale), (int)(start.y * scale));
            var iEnd = new int2((int)(end.x * scale), (int)(end.y * scale));
            if (_visited == null || _visited.Length < _pool.count)
            {
                _visited = new bool[_pool.count];
            }
            else
            {
                for (int i = 0; i < _visited.Length; i++)
                {
                    _visited[i] = false;
                }
            }

            _visitNewNode = true;
            _tempList.Clear();
            FindNodesWithoutObjects(_head, iStart, iStart, _tempList);
            _open.FakeClear();
            for (var i = 0; i < _tempList.Count; i++)
            {
                var temp = math.abs(((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd));
                _tempList[i].Value = temp.x + temp.y;
                _tempList[i].Dist2Start = 0;
                _tempList[i].LastNode = null;
                _open.Push(_tempList[i]);
                _visited[_tempList[i].Id] = true;
            }

            _ans.Clear();
            if (_open.Count == 0) return _ans;
            var now = _open.Peek();
            var minS = new int2[4];
            var maxS = new int2[4];
            while (_open.Count > 0)
            {
                now = _open.Pop();
                if (now.Min.x <= iEnd.x && now.Min.y <= iEnd.y && now.Max.x >= iEnd.x && now.Max.y >= iEnd.y)
                {
                    break;
                }
                minS[0] = new int2(now.Min.x, now.Min.y - 1);
                maxS[0] = new int2(now.Max.x + 1, now.Min.y - 1);

                minS[1] = new int2(now.Max.x + 1, now.Min.y);
                maxS[1] = new int2(now.Max.x + 1, now.Max.y + 1);

                minS[2] = new int2(now.Min.x - 1, now.Max.y + 1);
                maxS[2] = new int2(now.Max.x, now.Max.y + 1);

                minS[3] = new int2(now.Min.x - 1, now.Min.y - 1);
                maxS[3] = new int2(now.Min.x - 1, now.Max.y);

                for (var k = 0; k < 4; k++)
                {
                    _tempList.Clear();
                    FindNodesWithoutObjects(_head, in minS[k], in maxS[k], _tempList);
                    for (var i = 0; i < _tempList.Count; i++)
                    {
                        var temp2End = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd);
                        var temp2Last = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - (now.Min + now.Max) / 2);
                        _tempList[i].Dist2Start = now.Dist2Start + temp2Last.x + temp2Last.y;
                        _tempList[i].Value = temp2End.x + temp2End.y + _tempList[i].Dist2Start;
                        _tempList[i].LastNode = now;
                        _open.Push(_tempList[i]);
                        _visited[_tempList[i].Id] = true;
                    }
                }
            }

            {
                {
                    _ans.Push(end);
                    var lastPos = ((float2)(now.Min + now.Max)) / 2;
                    var lastNow = now;
                    now = now.LastNode;
                    while (now != null)
                    {
                        var nowPos = ((float2)(now.Min + now.Max)) / 2;
                        var p = (lastPos.x * nowPos.y - nowPos.x * lastPos.y);
                        if ((lastNow.Max.y + 1 == now.Min.y) || (lastNow.Min.y == now.Max.y + 1))
                        {
                            var samey = lastNow.Max.y + 1 == now.Min.y ? now.Min.y : now.Max.y;
                            if ((lastNow.Max.x + 1 == now.Min.x) || (lastNow.Min.x == now.Max.x + 1))
                            {
                                var sameX = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                                _ans.Push(new float2(sameX, samey) / scale);
                            }
                            else
                            {
                                var minX = math.max(now.Min.x, lastNow.Min.x);
                                var maxX = math.min(now.Max.x, lastNow.Max.x);
                                var ax = ((lastPos.x - nowPos.x) * samey - p) / (lastPos.y - nowPos.y);
                                if (ax < minX)
                                {
                                    _ans.Push(new float2(minX, samey) / scale);
                                }
                                else if (ax > maxX)
                                {
                                    _ans.Push(new float2(maxX, samey) / scale);
                                }
                                else
                                {
                                    _ans.Push(new float2(ax, samey) / scale);
                                }
                            }
                        }
                        else if ((lastNow.Max.x + 1 == now.Min.x) || (lastNow.Min.x == now.Max.x + 1))
                        {
                            var sameX = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                            var minY = math.max(now.Min.y, lastNow.Min.y);
                            var maxY = math.min(now.Max.y, lastNow.Max.y);
                            var ay = ((lastPos.y - nowPos.y) * sameX + p) /
                                     (lastPos.x - nowPos.x);
                            if (ay < minY)
                            {
                                _ans.Push(new float2(sameX, minY) / scale);
                            }
                            else if (ay > maxY)
                            {
                                _ans.Push(new float2(sameX, maxY) / scale);
                            }
                            else
                            {
                                _ans.Push(new float2(sameX, ay) / scale);
                            }
                        }
                        else
                        {
                            _ans.Push(nowPos / scale);
                        }

                        lastNow = now;
                        lastPos = nowPos;
                        now = now.LastNode;
                    }
                    _ans.Push(start);
#if QUAD_TREE_DEBUG
                    var tans = new Stack<float2>(_ans.Reverse());
                    var last = _ans.Pop();
                    while (_ans.Count > 0)
                    {
                        var temp = _ans.Pop();
                        Debug.DrawLine(new Vector3(last.x, 3, last.y), new Vector3(temp.x, 3, temp.y), Color.yellow,
                            1f);
                        last = temp;
                    }
                    _ans = tans;
#endif
                }
            }
            return _ans;
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

            var iPos = new int2((int)(pos.x * scale), (int)(pos.y * scale));
            return FindNearObject(_head, iPos, out obj) / scale;
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
                FakeClear(now.C[i], true);
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
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / scale, 1, (now.Min.y - 0.5f) / scale),
                    new Vector3((now.Min.x - 0.5f) / scale, 1, (now.Max.y + 0.5f) / scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / scale, 1, (now.Max.y + 0.5f) / scale),
                    new Vector3((now.Max.x + 0.5f) / scale, 1, (now.Max.y + 0.5f) / scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / scale, 1, (now.Max.y + 0.5f) / scale),
                    new Vector3((now.Max.x + 0.5f) / scale, 1, (now.Min.y - 0.5f) / scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / scale, 1, (now.Min.y - 0.5f) / scale),
                    new Vector3((now.Min.x - 0.5f) / scale, 1, (now.Min.y - 0.5f) / scale), Color.red, time);
            }
            else
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / scale, 0, (now.Min.y - 0.5f) / scale),
                    new Vector3((now.Min.x - 0.5f) / scale, 0, (now.Max.y + 0.5f) / scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / scale, 0, (now.Max.y + 0.5f) / scale),
                    new Vector3((now.Max.x + 0.5f) / scale, 0, (now.Max.y + 0.5f) / scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / scale, 0, (now.Max.y + 0.5f) / scale),
                    new Vector3((now.Max.x + 0.5f) / scale, 0, (now.Min.y - 0.5f) / scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / scale, 0, (now.Min.y - 0.5f) / scale),
                    new Vector3((now.Min.x - 0.5f) / scale, 0, (now.Min.y - 0.5f) / scale), Color.black, time);
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