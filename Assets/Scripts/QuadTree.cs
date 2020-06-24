﻿using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace JTech.Tools
{
    
    public interface IData
    {
    }
    /// <summary>
    /// 四叉树节点
    /// </summary>
    public class TreeNode<T> where  T: IData
    {
        public readonly int Id;
        public readonly TreeNode<T>[] C = new TreeNode<T>[4];
        public readonly int2[] Min2Bounds = new int2[4];
        public readonly T[] Min2BoundsEntities = new T[4];
        public int2 Min;
        public int2 Max;
        public readonly List<T> Objects = new List<T>();
        //public readonly List<T> RemoveList = new List<T>();
        public readonly HashSet<T> Has = new HashSet<T>();

        /// <summary>
        /// Astar使用的估值
        /// </summary>
        public int Value;

        /// <summary>
        /// Astar使用的从起始点到当前点的距离
        /// </summary>
        public int Dist2Start;

        /// <summary>
        /// Astar使用的路径上一个区域的id
        /// </summary>
        public TreeNode<T> LastNode;

        public TreeNode(int id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// 节点对象池
    /// </summary>
    public class NodePool<T> where  T : IData
    {
        private readonly Queue<TreeNode<T>> _queue;

        public int Count { get; private set; } = 0;

        public NodePool(int initCapital)
        {
            Count = initCapital;
            _queue = new Queue<TreeNode<T>>();
            for (int i = 0; i < initCapital; i++)
            {
                _queue.Enqueue(new TreeNode<T>(i));
            }
        }

        public TreeNode<T> Get(int2 min, int2 max)
        {
            TreeNode<T> ret;
            if (_queue.Count > 0)
            {
                ret = _queue.Dequeue();
            }
            else
            {
                ret = new TreeNode<T>(Count++);
            }

            ret.Min = min;
            ret.Max = max;
            ret.Min2Bounds[0] = ret.Min2Bounds[1] = ret.Min2Bounds[2] = ret.Min2Bounds[3] = new int2(int.MaxValue, int.MaxValue);
            ret.Objects.Clear();
            ret.Has.Clear();
            ret.C[0] = ret.C[1] = ret.C[2] = ret.C[3] = null;
            return ret;
        }

        public void Collect(TreeNode<T> node)
        {
            _queue.Enqueue(node);
        }
    }

    /// <summary>
    /// 四叉树
    /// </summary>
    public class QuadTree<T> where  T : IData
    {
        private float _scale = 2;
        private float _offset = 0;
        private readonly NodePool<T> _pool;
        private readonly int2[] _delta = new int2[] {new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1)};
        private TreeNode<T> _head;

        public int Count
        {
            get { return _pool.Count; }
        }

        public QuadTree(float offset = 0, float resolution = 2, int initCapital = 1)
        {
            _offset = offset;
            _scale = resolution;
            _pool = new NodePool<T>(initCapital);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rect"></param>
        public void Init(Rect rect)
        {
            _head = _pool.Get(
                new int2((int) (rect.x * _scale - rect.width / 2f * _scale),
                    (int) (rect.y * _scale - rect.height / 2f * _scale)),
                new int2((int) math.ceil(rect.x * _scale + rect.width / 2f * _scale),
                    (int) math.ceil(rect.y * _scale + rect.height / 2f * _scale)));
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
        private void DownTree(TreeNode<T> now)
        {
            if (now.Objects.Count == 0) return;
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    now.C[i].Min2Bounds[0] = now.C[i].Min;
                    now.C[i].Min2Bounds[1] = new int2(now.C[i].Max.x, now.C[i].Min.y);
                    now.C[i].Min2Bounds[2] = new int2(now.C[i].Min.x, now.C[i].Max.y);
                    now.C[i].Min2Bounds[3] = now.C[i].Max;
                    now.C[i].Objects.AddRange(now.Objects);
                    for (int j = 0; j < now.Objects.Count; j++)
                    {
                        now.C[i].Has.Add(now.Objects[j]);
                    }
                }
            }

            now.Objects.Clear();
        }

        private void UpTree(TreeNode<T> now)
        {
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    if (now.C[i].Has.Count != now.C[i].Objects.Count) return;
                    for (int j = i + 1; j < 4; j++)
                    {
                        if (now.C[j] != null)
                        {
                            if (now.C[j].Has.Count != now.C[j].Objects.Count ||
                                now.C[j].Objects.Count != now.C[i].Objects.Count) return;
                            for (int k = 0; k < now.C[j].Objects.Count; k++)
                            {
                                if (now.C[i].Objects[k].Equals(now.C[i].Objects[k]) == false) return;
                            }
                        }
                    }
                }
            }

            now.Objects.Clear();
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    if (now.Objects.Count == 0)
                    {
                        now.Objects.AddRange(now.C[i].Objects);
                    }

                    _pool.Collect(now.C[i]);
                    now.C[i] = null;
                }
            }
            
        }

        private void CreateChildren(TreeNode<T> now)
        {
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] == null)
                {
                    if (i == 0 || (i == 1 && now.Max.x != now.Min.x) ||
                        (i == 2 && now.Max.y != now.Min.y) ||
                        (i == 3 && now.Max.x != now.Min.x && now.Max.y != now.Min.y))
                    {
                        CreateChildren(now, i);
                    }
                }
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
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Min2Bounds[0] = now.Min;
                now.Min2Bounds[1] = new int2(now.Max.x, now.Min.y);
                now.Min2Bounds[2] = new int2(now.Min.x, now.Max.y);
                now.Min2Bounds[3] = now.Max;
                now.Min2BoundsEntities[0] = now.Min2BoundsEntities[1] =
                    now.Min2BoundsEntities[2] = now.Min2BoundsEntities[3] = entity;
                now.Objects.Add(entity);
                now.Has.Add(entity);
                return;
            }

            CreateChildren(now);

            DownTree(now);

            int2 center = now.Min + (now.Max - now.Min) / 2;
            bool has = false;
            if (min.x <= center.x && min.y <= center.y)
            {
                UpdateRect(now.C[0], entity, in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Min2Bounds[i].x == int.MaxValue ||
                        math.dot(now.Min2Bounds[i] - now.C[0].Min2Bounds[i], _delta[i]) > 0)
                    {
                        now.Min2Bounds[i] = now.C[0].Min2Bounds[i];
                        now.Min2BoundsEntities[i] = entity;
                    }
                }

                has = true;
            }

            if (now.C[1] != null && max.x > center.x && min.y <= center.y)
            {
                UpdateRect(now.C[1], entity, in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Min2Bounds[i].x == int.MaxValue ||
                        math.dot(now.Min2Bounds[i] - now.C[1].Min2Bounds[i], _delta[i]) > 0)
                    {
                        now.Min2Bounds[i] = now.C[1].Min2Bounds[i];
                        now.Min2BoundsEntities[i] = entity;
                    }
                }

                has = true;
            }

            if (now.C[2] != null && min.x <= center.x && max.y > center.y)
            {
                UpdateRect(now.C[2], entity, in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Min2Bounds[i].x == int.MaxValue ||
                        math.dot(now.Min2Bounds[i] - now.C[2].Min2Bounds[i], _delta[i]) > 0)
                    {
                        now.Min2Bounds[i] = now.C[2].Min2Bounds[i];
                        now.Min2BoundsEntities[i] = entity;
                    }
                }

                has = true;
            }

            if (now.C[3] != null && max.x > center.x && max.y > center.y)
            {
                UpdateRect(now.C[3], entity, in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Min2Bounds[i].x == int.MaxValue ||
                        math.dot(now.Min2Bounds[i] - now.C[3].Min2Bounds[i], _delta[i]) > 0)
                    {
                        now.Min2Bounds[i] = now.C[3].Min2Bounds[i];
                        now.Min2BoundsEntities[i] = entity;
                    }
                }

                has = true;
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
                obj = now.Objects[0];
                var d = 0;
                if (pos.x < now.Min.x) d += now.Min.x - pos.x;
                else if (pos.x > now.Max.x) d += pos.x - now.Max.x;
                if (pos.y < now.Min.y) d += now.Min.y - pos.y;
                else if (pos.y > now.Max.y) d += pos.y - now.Max.y;
                return d;
            }
            var dist = int.MaxValue;
            var tEntity = default(T);
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
                    obj = now.Min2BoundsEntities[0];
                    return math.dot(now.Min2Bounds[0] - pos, _delta[0]);
                }

                if (pos.x > now.Max.x && pos.y < now.Min.y)
                {
                    obj = now.Min2BoundsEntities[1];
                    return math.dot(now.Min2Bounds[1] - pos, _delta[1]);
                }

                if (pos.x < now.Min.x && pos.y > now.Max.y)
                {
                    obj = now.Min2BoundsEntities[2];
                    return math.dot(now.Min2Bounds[2] - pos, _delta[2]);
                }

                if (pos.x > now.Max.x && pos.y > now.Max.y)
                {
                    obj = now.Min2BoundsEntities[3];
                    return math.dot(now.Min2Bounds[3] - pos, _delta[3]);
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

                if (f2 != -1)
                {
                    tDist = FindNearObject(now.C[f2], in pos, out tEntity);
                    if (tDist < dist)
                    {
                        obj = tEntity;
                        dist = tDist;
                    }
                }
            }

            return dist;
        }

        /// <summary>
        /// 寻找包含给定区域且没有实体存在的节点
        /// （remove操作暂时没对该方法进行维护，remove过后该方法获取结果会出错）
        /// </summary>
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        /// <param name="visited"></param>
        private void FindNodesWithoutObjects(TreeNode<T> now, in int2 min, in int2 max, List<TreeNode<T>> nodes)
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
            int2 center = now.Min + (now.Max - now.Min) / 2;
            if (now.C[0] != null && min.x <= center.x && min.y <= center.y)
            {
                FindNodesWithoutObjects(now.C[0], in min, in max, nodes);
            }

            if (now.C[1] != null && max.x > center.x && min.y <= center.y)
            {
                FindNodesWithoutObjects(now.C[1], in min, in max, nodes);
            }

            if (now.C[2] != null && min.x <= center.x && max.y > center.y)
            {
                FindNodesWithoutObjects(now.C[2], in min, in max, nodes);
            }

            if (now.C[3] != null && max.x > center.x && max.y > center.y)
            {
                FindNodesWithoutObjects(now.C[3], in min, in max, nodes);
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
            //TODO:这段仅节省内存和加快a星速度,可以加上对对象池里面的对象个数判断来决定要不要回收节点
            if (now.Has.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (now.C[i] != null)
                    {
                        FakeClear(now.C[i]);
                    }
                }

                return;
            }
            if (now.Objects.Count > 0) CreateChildren(now);
            DownTree(now);
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    RemoveObject(now.C[i], obj);
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
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Has.Remove(obj);
                now.Objects.Remove(obj);
                if (now.Has.Count == 0)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (now.C[i] != null)
                        {
                            FakeClear(now.C[i]);
                            now.C[i] = null;
                        }
                    }
                    return;
                }
            }
            
            //所有区域update操作都需要创建子节点
            if (now.Objects.Count > 0) CreateChildren(now);

            DownTree(now);

            int2 center = now.Min + (now.Max - now.Min) / 2;
            if (now.C[0] != null && min.x <= center.x && min.y <= center.y)
            {
                RemoveObjectInRect(now.C[0], obj, in min, in max);
            }

            if (now.C[1] != null && max.x > center.x && min.y <= center.y)
            {
                RemoveObjectInRect(now.C[1], obj, in min, in max);
            }

            if (now.C[2] != null && min.x <= center.x && max.y > center.y)
            {
                RemoveObjectInRect(now.C[2], obj, in min, in max);
            }

            if (now.C[3] != null && max.x > center.x && max.y > center.y)
            {
                RemoveObjectInRect(now.C[3], obj, in min, in max);
            }

            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null && now.C[i].Has.Contains(obj)) return;
            }

            now.Has.Remove(obj);
            //TODO:这段仅节省内存和加快a星速度,可以加上对对象池里面的对象个数判断来决定要不要回收节点
            if (now.Has.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (now.C[i] != null)
                    {
                        _pool.Collect(now.C[i]);
                        now.C[i] = null;
                    }
                }
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
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Has.Clear();
                now.Objects.Clear();
                for (int i = 0; i < 4; i++)
                {
                    if (now.C[i] != null)
                    {
                        FakeClear(now.C[i]);
                        now.C[i] = null;
                    }
                }
                return;
            }

            if (now.Objects.Count > 0) CreateChildren(now);

            DownTree(now);
            int2 center = now.Min + (now.Max - now.Min) / 2;
            if (min.x <= center.x && min.y <= center.y)
            {
                RemoveAllObjectsInRect(now.C[0], in min, in max);
            }

            if (now.C[1] != null && max.x > center.x && min.y <= center.y)
            {
                RemoveAllObjectsInRect(now.C[1], in min, in max);
            }

            if (now.C[2] != null && min.x <= center.x && max.y > center.y)
            {
                RemoveAllObjectsInRect(now.C[2], in min, in max);
            }

            if (now.C[3] != null && max.x > center.x && max.y > center.y)
            {
                RemoveAllObjectsInRect(now.C[3], in min, in max);
            }

            now.Has.Clear();
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null) now.Has.UnionWith(now.C[i].Has);
            }

            if (now.Has.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (now.C[i] != null)
                    {
                        _pool.Collect(now.C[i]);
                        now.C[i] = null;
                    }
                }
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
        /// 删除指定区域内的所有对象
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void RemoveObjectInRect(T obj, in float2 min, in float2 max)
        {
            if (CheackInited() == false) return;
            var iMin = (int2)math.round(min * _scale);
            var iMax = (int2)math.round(max * _scale);
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
            var iMin = (int2)math.round(min * _scale);
            var iMax = (int2)math.round(max * _scale);
            RemoveAllObjectsInRect(_head, in iMin, in iMax);
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
            if (_head == null)
            {
                Debug.LogError("pls call Init method before call the other methods!");
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// 添加圆形区域对象
        /// 这里做简单细分，容忍误差
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="size"></param>
        /// <param name="pos"></param>
        /// <param name="forward"></param>
        public void AddCircleObject(T obj, in float radius, in float2 pos)
        {
            if (CheackInited() == false) return;
            const float f1 = 2 - math.SQRT2;
            var r = (radius + _offset) * _scale;
            if (f1 * r < 2)
            {
                int2 min = new int2((int) (pos.x * _scale - r), (int) (pos.y * _scale - r));
                int2 max = new int2((int) math.ceil(pos.x * _scale + r),
                    (int) math.ceil(pos.y * _scale + r));
                AddRect(obj, min, max);
            }
            else if(f1 * r < 4)
            {
                int2 min = new int2((int) (pos.x * _scale - r * math.SQRT2 * 0.5f), (int) (pos.y * _scale - r * math.SQRT2 * 0.5f));
                int2 max = new int2((int) math.ceil(pos.x * _scale + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(pos.y * _scale + r * math.SQRT2 * 0.5f));
                AddRect(obj, min, max);
            
                int2 tmin = new int2((int) (pos.x * _scale - r), min.y);
                int2 tmax = new int2(min.x, max.y);
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(min.x, max.y);
                tmax = new int2(max.x, (int)math.ceil( pos.y * _scale + r));
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(max.x, min.y);
                tmax = new int2((int)math.ceil( pos.x * _scale + r), max.y);
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(min.x, (int) (pos.y * _scale - r));
                tmax = new int2(min.x, max.y);
                AddRect(obj, tmin, tmax);
            }
            else
            {
                //TODO: 这里应该再做细分，目前先使用和上面一样的细分方案
                int2 min = new int2((int) (pos.x * _scale - r * math.SQRT2 * 0.5f), (int) (pos.y * _scale - r * math.SQRT2 * 0.5f));
                int2 max = new int2((int) math.ceil(pos.x * _scale + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(pos.y * _scale + r * math.SQRT2 * 0.5f));
                AddRect(obj, min, max);
            
                int2 tmin = new int2((int) (pos.x * _scale - r), min.y);
                int2 tmax = new int2(min.x, max.y);
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(min.x, max.y);
                tmax = new int2(max.x, (int)math.ceil( pos.y * _scale + r));
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(max.x, min.y);
                tmax = new int2((int)math.ceil( pos.x * _scale + r), max.y);
                AddRect(obj, tmin, tmax);
                
                tmin = new int2(min.x, (int) (pos.y * _scale - r));
                tmax = new int2(min.x, max.y);
                AddRect(obj, tmin, tmax);
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
        /// <param name="forward"></param>
        public void AddParallelRectObject(T obj, in float2 halfSize, in float2 pos)
        {
            if (CheackInited() == false) return;
            int2 min = new int2((int) (pos.x * _scale - (halfSize.x + _offset) * _scale), (int) (pos.y * _scale - (halfSize.y + _offset) * _scale));
            int2 max = new int2((int) math.ceil(pos.x * _scale + (halfSize.x + _offset) * _scale),
                (int) math.ceil(pos.y * _scale + (halfSize.y + _offset) * _scale));
            min = math.clamp(min, _head.Min, _head.Max);
            max = math.clamp(max, _head.Min, _head.Max);
            AddRect(obj, min, max);
        }

        private BinaryHeap<int2> _rectClipHeap = new BinaryHeap<int2>((a, b) => a.x < b.x || (a.x == b.x && a.y < b.y));
        /// <summary>
        /// 添加任意矩形区域
        /// ps:
        ///     如果添加的矩形平行于最标轴或近似平行于坐标轴请使用AddParallelRectEntity函数添加该对象。
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        /// <param name="forward">xz平面下对象本地z轴的世界朝向</param>
        public void  AddRectObject(T obj, in float2 halfSize, in float2 pos, in float2 forward)
        {
            if (CheackInited() == false) return;
            _rectClipHeap.Clear();
            var lenForward = math.length(forward);
            var cosa = forward.y / lenForward;
            var sina = forward.x / lenForward;
            var points = new int2[4];
            var rotateMatrix = new float2x2(cosa, -sina, sina, cosa);
            var hs = halfSize + new float2(_offset, _offset);
            for (int i = 0; i < 4; i++)
            {
                points[i] = (int2) math.round((math.mul(hs * _delta[i], rotateMatrix) + pos) * _scale);
            }

            points[2] += points[3];
            points[3] = points[2] - points[3];
            points[2] -= points[3];

            for (int i = 0; i < 4; i++)
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
                    for (int j = 0; j <= dx; j++)
                    {
                        // Debug.DrawLine(new Vector3((s.x - 0.5f) / _scale, 2, (s.y - 0.5f) / _scale),
                        //     new Vector3((s.x - 0.5f) / _scale, 2, (s.y + 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x - 0.5f) / _scale, 2, (s.y + 0.5f) / _scale),
                        //     new Vector3((s.x + 0.5f) / _scale, 2, (s.y + 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x + 0.5f) / _scale, 2, (s.y + 0.5f) / _scale),
                        //     new Vector3((s.x + 0.5f) / _scale, 2, (s.y - 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x + 0.5f) / _scale, 2, (s.y - 0.5f) / _scale),
                        //     new Vector3((s.x - 0.5f) / _scale, 2, (s.y - 0.5f) / _scale), Color.cyan, 4);
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
                    for (int j = 0; j <= dy; j++)
                    {
                        // Debug.DrawLine(new Vector3((s.x - 0.5f) / _scale, 2, (s.y - 0.5f) / _scale),
                        //     new Vector3((s.x - 0.5f) / _scale, 2, (s.y + 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x - 0.5f) / _scale, 2, (s.y + 0.5f) / _scale),
                        //     new Vector3((s.x + 0.5f) / _scale, 2, (s.y + 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x + 0.5f) / _scale, 2, (s.y + 0.5f) / _scale),
                        //     new Vector3((s.x + 0.5f) / _scale, 2, (s.y - 0.5f) / _scale), Color.cyan, 4);
                        // Debug.DrawLine(new Vector3((s.x + 0.5f) / _scale, 2, (s.y - 0.5f) / _scale),
                        //     new Vector3((s.x - 0.5f) / _scale, 2, (s.y - 0.5f) / _scale), Color.cyan, 4);
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

            var nowx = int.MaxValue;
            int2 min = int2.zero;
            int2 max = int2.zero;
            int2 lastmin = int2.zero;
            int2 lastmax = int2.zero;
            lastmax.y = -1;
            int minup = 0;
            int maxup = 0;
            while (_rectClipHeap.Count > 0)
            {
                var p = _rectClipHeap.Pop();
                if (nowx != p.x)
                {
                    if (nowx == int.MaxValue)
                    {
                        min = max = p;
                    }
                    else
                    {
                        if (lastmin.y > lastmax.y)
                        {
                            lastmin = min;
                            lastmax = max;
                            min = max = p;
                        }
                        else
                        {
                            if ((minup == 0 || minup + lastmin.y == min.y || lastmin.y == min.y) && (maxup == 0 || maxup + lastmax.y == max.y || lastmax.y == max.y))
                            {
                                if (minup == 0)
                                {
                                    minup = min.y - lastmin.y;
                                }
                                if (maxup == 0)
                                {
                                    maxup = max.y - lastmax.y;
                                }
                                min = max = p;
                            }
                            else
                            {
                                lastmin.y = minup < 0 ? lastmin.y + minup : lastmin.y;
                                lastmax.y = maxup > 0 ? lastmax.y + maxup : lastmax.y;
                                lastmax.x = max.x - 1;
                                AddRect(obj, lastmin, lastmax);
                                lastmax = max;
                                lastmin = min;
                                minup = maxup = 0;
                                min = max = p;
                            }
                        }
                    }
                    nowx = p.x;
                }
                else
                {
                    max.y = p.y;
                }
            }

            if (lastmax.y < lastmin.y)
            {
                AddRect(obj, min, max);
            }
            else
            {
                lastmin.y = minup < 0 ? lastmin.y + minup : lastmin.y;
                lastmax.y = maxup > 0 ? lastmax.y + maxup : lastmax.y;
                lastmax.x = max.x;
                AddRect(obj, lastmin, lastmax);
            }
        }

        

        private readonly BinaryHeap<TreeNode<T>> _open = new BinaryHeap<TreeNode<T>>((a, b) => a.Value < b.Value);
        private readonly List<TreeNode<T>> _tempList = new List<TreeNode<T>>();
        private Stack<float2> _ans = new Stack<float2>();
        private bool[] _visited;
        private bool _visitNewNode;
        
        /// <summary>
        /// 查找和指定区域有重合部分的所有没有对象存在的节点
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        public void FindNodesWithoutObjects(in int2 min, in int2 max, List<TreeNode<T>> nodes)
        {
            _visitNewNode = false;
            FindNodesWithoutObjects(_head, in min, in max, nodes);
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
            int2 iStart = new int2((int) (start.x * _scale), (int) (start.y * _scale));
            int2 iEnd = new int2((int) (end.x * _scale), (int) (end.y * _scale));
            if (_visited == null || _visited.Length < _pool.Count)
            {
                _visited = new bool[_pool.Count];
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
            for (int i = 0; i < _tempList.Count; i++)
            {
                var temp = math.abs(((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd));
                _tempList[i].Value = temp.x + temp.y;
                _tempList[i].Dist2Start = 0;
                _tempList[i].LastNode = null;
                _open.Push(_tempList[i]);
                _visited[_tempList[i].Id] = true;
            }

            while (_open.Count > 0)
            {
                var now = _open.Pop();
                if (now.Min.x <= iEnd.x && now.Min.y <= iEnd.y && now.Max.x >= iEnd.x && now.Max.y >= iEnd.y)
                {
                    _ans.Clear();
                    _ans.Push(end);
                    var lastPos = ((float2) (now.Min + now.Max)) / 2;
                    var lastNow = now;
                    now = now.LastNode;
                    while (now != null)
                    {
                        var nowPos = ((float2) (now.Min + now.Max)) / 2;
                        var p = (lastPos.x * nowPos.y - nowPos.x * lastPos.y);
                        if ((lastNow.Max.y + 1 == now.Min.y) || (lastNow.Min.y == now.Max.y + 1))
                        {
                            var samey = lastNow.Max.y + 1 == now.Min.y ? now.Min.y : now.Max.y;
                            if ((lastNow.Max.x + 1 == now.Min.x) || (lastNow.Min.x == now.Max.x + 1))
                            {
                                var samex = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                                _ans.Push(new float2(samex, samey) / _scale);
                            }
                            else
                            {
                                var minx = math.max(now.Min.x, lastNow.Min.x);
                                var maxx = math.min(now.Max.x, lastNow.Max.x);
                                var ax = ((lastPos.x - nowPos.x) * samey - p) /
                                         (lastPos.y - nowPos.y);
                                if (ax < minx)
                                {
                                    _ans.Push(new float2(minx, samey) / _scale);
                                }
                                else if (ax > maxx)
                                {
                                    _ans.Push(new float2(maxx, samey) / _scale);
                                }
                                else
                                {
                                    _ans.Push(new float2(ax, samey) / _scale);
                                }
                            }
                        }
                        else if ((lastNow.Max.x + 1 == now.Min.x) || (lastNow.Min.x == now.Max.x + 1))
                        {
                            var samex = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                            var miny = math.max(now.Min.y, lastNow.Min.y);
                            var maxy = math.min(now.Max.y, lastNow.Max.y);
                            var ay = ((lastPos.y - nowPos.y) * samex + p) /
                                     (lastPos.x - nowPos.x);
                            if (ay < miny)
                            {
                                _ans.Push(new float2(samex, miny) / _scale);
                            }
                            else if (ay > maxy)
                            {
                                _ans.Push(new float2(samex, maxy) / _scale);
                            }
                            else
                            {
                                _ans.Push(new float2(samex, ay) / _scale);
                            }
                        }
                        else
                        {
                            _ans.Push(nowPos / _scale);
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
                            0.01f);
                        last = temp;
                    }
                    _ans = tans;
#endif
                    break;
                }

                var min = now.Min - new int2(1, 1);
                var max = new int2(now.Max.x + 1, now.Min.y - 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
                for (int i = 0; i < _tempList.Count; i++)
                {
                    var temp2End = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd);
                    var temp2Last = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - (now.Min + now.Max) / 2);
                    _tempList[i].Dist2Start = now.Dist2Start + temp2Last.x + temp2Last.y;
                    _tempList[i].Value = temp2End.x + temp2End.y + _tempList[i].Dist2Start;
                    _tempList[i].LastNode = now;
                    _open.Push(_tempList[i]);
                    _visited[_tempList[i].Id] = true;
                }

                min = new int2(now.Max.x + 1, now.Min.y);
                max = new int2(now.Max.x + 1, now.Max.y + 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
                for (int i = 0; i < _tempList.Count; i++)
                {
                    var temp2End = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd);
                    var temp2Last = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - (now.Min + now.Max) / 2);
                    _tempList[i].Dist2Start = now.Dist2Start + temp2Last.x + temp2Last.y;
                    _tempList[i].Value = temp2End.x + temp2End.y + _tempList[i].Dist2Start;
                    _tempList[i].LastNode = now;
                    _open.Push(_tempList[i]);
                    _visited[_tempList[i].Id] = true;
                }

                min = new int2(now.Min.x - 1, now.Max.y + 1);
                max = new int2(now.Max.x, now.Max.y + 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
                for (int i = 0; i < _tempList.Count; i++)
                {
                    var temp2End = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - iEnd);
                    var temp2Last = math.abs((_tempList[i].Max + _tempList[i].Min) / 2 - (now.Min + now.Max) / 2);
                    _tempList[i].Dist2Start = now.Dist2Start + temp2Last.x + temp2Last.y;
                    _tempList[i].Value = temp2End.x + temp2End.y + _tempList[i].Dist2Start;
                    _tempList[i].LastNode = now;
                    _open.Push(_tempList[i]);
                    _visited[_tempList[i].Id] = true;
                }

                min = new int2(now.Min.x - 1, now.Min.y - 1);
                max = new int2(now.Min.x - 1, now.Max.y);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
                for (int i = 0; i < _tempList.Count; i++)
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

            int2 iPos = new int2((int) (pos.x * _scale), (int) (pos.y * _scale));
            return FindNearObject(_head, iPos, out obj) / _scale;
        }

        /// <summary>
        /// 清除某个节点及其所有子节点
        /// </summary>
        /// <param name="now">节点</param>
        private void FakeClear(TreeNode<T> now)
        {
            for (int i = 0; i < 4; i++)
            {
                if (now.C[i] != null)
                {
                    FakeClear(now.C[i]);
                }
            }
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
            //Debug.LogFormat("{0},{1},{2}", deep, now.Min, now.Max);
            if (now.Objects.Count > 0)
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _scale, 1, (now.Min.y - 0.5f) / _scale),
                    new Vector3((now.Min.x - 0.5f) / _scale, 1, (now.Max.y + 0.5f) / _scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _scale, 1, (now.Max.y + 0.5f) / _scale),
                    new Vector3((now.Max.x + 0.5f) / _scale, 1, (now.Max.y + 0.5f) / _scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _scale, 1, (now.Max.y + 0.5f) / _scale),
                    new Vector3((now.Max.x + 0.5f) / _scale, 1, (now.Min.y - 0.5f) / _scale), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _scale, 1, (now.Min.y - 0.5f) / _scale),
                    new Vector3((now.Min.x - 0.5f) / _scale, 1, (now.Min.y - 0.5f) / _scale), Color.red, time);
            }
            else
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _scale, 0, (now.Min.y - 0.5f) / _scale),
                    new Vector3((now.Min.x - 0.5f) / _scale, 0, (now.Max.y + 0.5f) / _scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _scale, 0, (now.Max.y + 0.5f) / _scale),
                    new Vector3((now.Max.x + 0.5f) / _scale, 0, (now.Max.y + 0.5f) / _scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _scale, 0, (now.Max.y + 0.5f) / _scale),
                    new Vector3((now.Max.x + 0.5f) / _scale, 0, (now.Min.y - 0.5f) / _scale), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _scale, 0, (now.Min.y - 0.5f) / _scale),
                    new Vector3((now.Min.x - 0.5f) / _scale, 0, (now.Min.y - 0.5f) / _scale), Color.black, time);
            }
            for (int i = 0; i < 4; i++)
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