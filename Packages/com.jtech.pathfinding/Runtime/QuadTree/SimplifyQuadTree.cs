using System.Collections.Generic;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;

namespace JTech.PathFinding.QuadTree
{
    /// <summary>
    /// 四叉树节点
    /// </summary>
    public class SimplifyTreeNode
    {
        internal readonly int Id;
        internal readonly SimplifyTreeNode[] C = new SimplifyTreeNode[4];
        internal readonly int2[] Bounds = new int2[4];
        internal readonly  SimplifyTreeNode[] BoundsNodes = new SimplifyTreeNode[4];
        internal int2 Min;
        internal int2 Max;
        internal bool Flag;
        internal bool Has;

        /// <summary>
        /// Astar使用的估值
        /// </summary>
        internal int Value;

        /// <summary>
        /// Astar使用的从起始点到当前点的距离
        /// </summary>
        internal int Dist2Start;

        /// <summary>
        /// Astar使用的路径上一个区域的id
        /// </summary>
        internal SimplifyTreeNode LastNode;

        internal SimplifyTreeNode(int id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// 节点对象池
    /// </summary>
    public class SimplifyNodePool
    {
        private readonly Queue<SimplifyTreeNode> _queue;

        internal int count { get; private set; } = 0;

        internal SimplifyNodePool(int initCapital)
        {
            count = initCapital;
            _queue = new Queue<SimplifyTreeNode>();
            for (int i = 0; i < initCapital; i++)
            {
                _queue.Enqueue(new SimplifyTreeNode(i));
            }
        }

        internal SimplifyTreeNode Get(int2 min, int2 max)
        {
            SimplifyTreeNode ret;
            if (_queue.Count > 0)
            {
                ret = _queue.Dequeue();
            }
            else
            {
                ret = new SimplifyTreeNode(count++);
            }

            ret.Min = min;
            ret.Max = max;
            ret.Bounds[0] = ret.Bounds[1] = ret.Bounds[2] = ret.Bounds[3] = new int2(int.MaxValue, int.MaxValue);
            ret.C[0] = ret.C[1] = ret.C[2] = ret.C[3] = null;
            ret.Has = ret.Flag = false;
            return ret;
        }

        internal void Collect(SimplifyTreeNode node)
        {
            _queue.Enqueue(node);
        }
    }

    /// <summary>
    /// 四叉树
    /// </summary>
    public class SimplifyQuadTree
    {
        private readonly float _offset = 0;
        private readonly SimplifyNodePool _pool;
        private readonly int2[] _delta = new int2[] {new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1)};
        private SimplifyTreeNode _head;

        public int count => _pool.count;

        private readonly float _resolution;

        public SimplifyQuadTree(float offset = 0, float resolution = 2, int initCapital = 1)
        {
            _offset = offset;
            _resolution = resolution;
            _pool = new SimplifyNodePool(initCapital);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rect"></param>
        public void Init(Rect rect)
        {
            _head = _pool.Get(
                new int2((int) (rect.x * _resolution - rect.width / 2f * _resolution),
                    (int) (rect.y * _resolution - rect.height / 2f * _resolution)),
                new int2((int) math.ceil(rect.x * _resolution + rect.width / 2f * _resolution),
                    (int) math.ceil(rect.y * _resolution + rect.height / 2f * _resolution)));
        }
        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="now"></param>
        /// <param name="index"></param>
        private void CreateChildren(SimplifyTreeNode now, int index)
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
        private static void DownTree(SimplifyTreeNode now)
        {
            if (now.Flag == false) return;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                now.C[i].Bounds[0] = now.C[i].Min;
                now.C[i].Bounds[1] = new int2(now.C[i].Max.x, now.C[i].Min.y);
                now.C[i].Bounds[2] = new int2(now.C[i].Min.x, now.C[i].Max.y);
                now.C[i].Bounds[3] = now.C[i].Max;
                now.C[i].Flag = true;
            }

            now.Flag = false;
        }

        private void UpTree(SimplifyTreeNode now)
        {
            if (now.Has == false) return;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null && now.C[i].Flag == false)
                {
                    return;
                }
            }

            now.Flag = true;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                _pool.Collect(now.C[i]);
                now.C[i] = null;
            }
        }

        private void CreateChildren(SimplifyTreeNode now)
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
        /// 区域更新操作
        /// </summary>
        /// <param name="now"></param>
        /// <param name="entity"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void UpdateRect(SimplifyTreeNode now, in int2 min, in int2 max)
        {
            if (now.Flag == true) return;
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Bounds[0] = now.Min;
                now.Bounds[1] = new int2(now.Max.x, now.Min.y);
                now.Bounds[2] = new int2(now.Min.x, now.Max.y);
                now.Bounds[3] = now.Max;
                now.BoundsNodes[0] = now.BoundsNodes[1] =
                    now.BoundsNodes[2] = now.BoundsNodes[3] = now;
                now.Has = now.Flag = true;
                return;
            }

            CreateChildren(now);

            DownTree(now);

            var center = now.Min + (now.Max - now.Min) / 2;
            if (min.x <= center.x && min.y <= center.y)
            {
                UpdateRect(now.C[0], in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Bounds[i].x == int.MaxValue ||
                        math.dot(now.Bounds[i] - now.C[0].Bounds[i], _delta[i]) > 0)
                    {
                        now.Bounds[i] = now.C[0].Bounds[i];
                        now.BoundsNodes[i] = now.C[0].BoundsNodes[i];
                    }
                }

                now.Has = true;
            }

            if (now.C[1] != null && max.x > center.x && min.y <= center.y)
            {
                UpdateRect(now.C[1], in min, in max);
                for (var i = 0; i < 4; i++)
                {
                    if (now.Bounds[i].x == int.MaxValue ||
                        math.dot(now.Bounds[i] - now.C[1].Bounds[i], _delta[i]) > 0)
                    {
                        now.Bounds[i] = now.C[1].Bounds[i];
                        now.BoundsNodes[i] = now.C[1].BoundsNodes[i];
                    }
                }

                now.Has = true;
            }

            if (now.C[2] != null && min.x <= center.x && max.y > center.y)
            {
                UpdateRect(now.C[2], in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Bounds[i].x == int.MaxValue ||
                        math.dot(now.Bounds[i] - now.C[2].Bounds[i], _delta[i]) > 0)
                    {
                        now.Bounds[i] = now.C[2].Bounds[i];
                        now.BoundsNodes[i] = now.C[2].BoundsNodes[i];
                    }
                }

                now.Has = true;
            }

            if (now.C[3] != null && max.x > center.x && max.y > center.y)
            {
                UpdateRect(now.C[3], in min, in max);
                for (int i = 0; i < 4; i++)
                {
                    if (now.Bounds[i].x == int.MaxValue ||
                        math.dot(now.Bounds[i] - now.C[3].Bounds[i], _delta[i]) > 0)
                    {
                        now.Bounds[i] = now.C[3].Bounds[i];
                        now.BoundsNodes[i] = now.C[3].BoundsNodes[i];
                    }
                }

                now.Has = true;
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
        private int FindNearObject(SimplifyTreeNode now, in int2 pos, out SimplifyTreeNode obj)
        {
            obj = default;
            if (now.Has == false) return int.MaxValue;
            if (now.Flag)
            {
                obj = now;
                var d = 0;
                if (pos.x < now.Min.x) d += now.Min.x - pos.x;
                else if (pos.x > now.Max.x) d += pos.x - now.Max.x;
                if (pos.y < now.Min.y) d += now.Min.y - pos.y;
                else if (pos.y > now.Max.y) d += pos.y - now.Max.y;
                return d;
            }
            var dist = int.MaxValue;
            SimplifyTreeNode tEntity;
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
                    obj = now.BoundsNodes[0];
                    return math.dot(now.Bounds[0] - pos, _delta[0]);
                }

                if (pos.x > now.Max.x && pos.y < now.Min.y)
                {
                    obj = now.BoundsNodes[1];
                    return math.dot(now.Bounds[1] - pos, _delta[1]);
                }

                if (pos.x < now.Min.x && pos.y > now.Max.y)
                {
                    obj = now.BoundsNodes[2];
                    return math.dot(now.Bounds[2] - pos, _delta[2]);
                }

                if (pos.x > now.Max.x && pos.y > now.Max.y)
                {
                    obj = now.BoundsNodes[3];
                    return math.dot(now.Bounds[3] - pos, _delta[3]);
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
        /// </summary>
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        private void FindNodesWithoutObjects(SimplifyTreeNode now, in int2 min, in int2 max, List<SimplifyTreeNode> nodes)
        {
            while (true)
            {
                if (_visitNewNode && _visited[now.Id]) return;
                if (now.Flag)
                {
                    return;
                }

                if (now.Has == false)
                {
                    nodes.Add(now);
                    return;
                }

                DownTree(now);
                var center = now.Min + (now.Max - now.Min) / 2;
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
                    now = now.C[3];
                    continue;
                }

                break;
            }
        }

        /// <summary>
        /// 删除指定区域内的所有对象
        /// </summary>
        /// <param name="now"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void RemoveAllObjectsInRect(SimplifyTreeNode now, in int2 min, in int2 max)
        {
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Has = now.Flag = false;
                for (var i = 0; i < 4; i++)
                {
                    if (now.C[i] == null) continue;
                    FakeClear(now.C[i]);
                    now.C[i] = null;
                }

                return;
            }

            if (now.Flag) CreateChildren(now);

            DownTree(now);
            var center = now.Min + (now.Max - now.Min) / 2;
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

            now.Has = false;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] != null) now.Has = now.Has || now.C[i].Has;
            }

            if (now.Has != false) return;
            for (var i = 0; i < 4; i++)
            {
                if (now.C[i] == null) continue;
                _pool.Collect(now.C[i]);
                now.C[i] = null;
            }
        }

        /// <summary>
        /// 删除指定区域内的所有对象
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void RemoveAllObjectsInRect(in float2 min, in float2 max)
        {
            if (CheackInited() == false) return;
            var iMin = (int2)math.round(min * _resolution);
            var iMax = (int2)math.round(max * _resolution);
            RemoveAllObjectsInRect(_head, in iMin, in iMax);
        }

        private static bool CheckHasObject(SimplifyTreeNode now, in int2 pos)
        {
            while (true)
            {
                if (now.Flag)
                {
                    return true;
                }

                if (now.Max.x == now.Min.x && now.Max.y == now.Min.y)
                {
                    return false;
                }

                var center = now.Min + (now.Max - now.Min) / 2;
                if (now.C[0] != null && pos.x <= center.x && pos.y <= center.y)
                {
                    now = now.C[0];
                    continue;
                }

                if (now.C[1] != null && pos.x > center.x && pos.y <= center.y)
                {
                    now = now.C[1];
                    continue;
                }

                if (now.C[2] != null && pos.x <= center.x && pos.y > center.y)
                {
                    now = now.C[2];
                    continue;
                }

                if (now.C[3] != null && pos.x > center.x && pos.y > center.y)
                {
                    now = now.C[3];
                    continue;
                }

                return false;
            }
        }

        public bool CheckHasObject(float2 pos)
        {
            var iPos = new int2((int) (pos.x * _resolution), (int) (pos.y * _resolution));
            return CheckHasObject(_head, in iPos);
        }

        /// <summary>
        /// 添加坐标换算后的矩形区域
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void AddRect(in int2 min, in int2 max)
        {
            if (_head.Max.x < min.x || _head.Min.x > max.x || _head.Max.y < min.y || _head.Min.y > max.y) return;
            UpdateRect(_head, in min, in max);
        }

        /// <summary>
        /// 检查是否已经完成初始化
        /// </summary>
        private bool CheackInited()
        {
            return _head != null;
        }

        /// <summary>
        /// 添加圆形区域对象
        /// 这里做简单细分，容忍误差
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="pos"></param>
        public void AddCircleObject(in float radius, in float2 pos)
        {
            if (CheackInited() == false) return;
            const float f1 = 2 - math.SQRT2;
            var r = (radius + _offset) * _resolution;
            if (f1 * r < 2)
            {
                var min = new int2((int) (pos.x * _resolution - r), (int) (pos.y * _resolution - r));
                var max = new int2((int) math.ceil(pos.x * _resolution + r),
                    (int) math.ceil(pos.y * _resolution + r));
                AddRect(min, max);
            }
            else if(f1 * r < 4)
            {
                var min = new int2((int) (pos.x * _resolution - r * math.SQRT2 * 0.5f), (int) (pos.y * _resolution - r * math.SQRT2 * 0.5f));
                var max = new int2((int) math.ceil(pos.x * _resolution + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(pos.y * _resolution + r * math.SQRT2 * 0.5f));
                AddRect(min, max);
            
                var tMin = new int2((int) (pos.x * _resolution - r), min.y);
                var tMax = new int2(min.x, max.y);
                AddRect(tMin, tMax);
                
                tMin = new int2(min.x, max.y);
                tMax = new int2(max.x, (int)math.ceil( pos.y * _resolution + r));
                AddRect(tMin, tMax);
                
                tMin = new int2(max.x, min.y);
                tMax = new int2((int)math.ceil( pos.x * _resolution + r), max.y);
                AddRect(tMin, tMax);
                
                tMin = new int2(min.x, (int) (pos.y * _resolution - r));
                tMax = new int2(min.x, max.y);
                AddRect(tMin, tMax);
            }
            else
            {
                //TODO: 这里应该再做细分，目前先使用和上面一样的细分方案
                var min = new int2((int) (pos.x * _resolution - r * math.SQRT2 * 0.5f), (int) (pos.y * _resolution - r * math.SQRT2 * 0.5f));
                var max = new int2((int) math.ceil(pos.x * _resolution + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(pos.y * _resolution + r * math.SQRT2 * 0.5f));
                AddRect(min, max);
            
                var tMin = new int2((int) (pos.x * _resolution - r), min.y);
                var tMax = new int2(min.x, max.y);
                AddRect(tMin, tMax);
                
                tMin = new int2(min.x, max.y);
                tMax = new int2(max.x, (int)math.ceil( pos.y * _resolution + r));
                AddRect(tMin, tMax);
                
                tMin = new int2(max.x, min.y);
                tMax = new int2((int)math.ceil( pos.x * _resolution + r), max.y);
                AddRect(tMin, tMax);
                
                tMin = new int2(min.x, (int) (pos.y * _resolution - r));
                tMax = new int2(min.x, max.y);
                AddRect(tMin, tMax);
            }
        }

        /// <summary>
        /// 添加平行于坐标轴的矩形区域
        /// ps:
        ///     因为非平行于坐标轴的矩形需要做细分，效率会降低，
        ///     所以能够使用这个方法的情况就不要用添加任意矩形的方法。
        /// </summary>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        public void AddParallelRectObject(in float2 halfSize, in float2 pos)
        {
            if (CheackInited() == false) return;
            var min = new int2((int) (pos.x * _resolution - (halfSize.x + _offset) * _resolution), (int) (pos.y * _resolution - (halfSize.y + _offset) * _resolution));
            var max = new int2((int) math.ceil(pos.x * _resolution + (halfSize.x + _offset) * _resolution),
                (int) math.ceil(pos.y * _resolution + (halfSize.y + _offset) * _resolution));
            min = math.clamp(min, _head.Min, _head.Max);
            max = math.clamp(max, _head.Min, _head.Max);
            AddRect(min, max);
        }

        private readonly BinaryHeap<int2> _rectClipHeap = new BinaryHeap<int2>((a, b) => a.x < b.x || (a.x == b.x && a.y < b.y));

        /// <summary>
        /// 添加任意矩形区域
        /// ps:
        ///     如果添加的矩形平行于最标轴或近似平行于坐标轴请使用AddParallelRectEntity函数添加该对象。
        /// </summary>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        /// <param name="forward">xz平面下对象本地z轴的世界朝向</param>
        public void  AddRectObject(in float2 halfSize, in float2 pos, in float2 forward)
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
                points[i] = (int2) math.round((math.mul(hs * _delta[i], rotateMatrix) + pos) * _resolution);
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
                    AddRect(s, s);
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
                                AddRect(lastMin, lastMax);
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
                AddRect(min, max);
            }
            else
            {
                lastMin.y = minUp < 0 ? lastMin.y + minUp : lastMin.y;
                lastMax.y = maxUp > 0 ? lastMax.y + maxUp : lastMax.y;
                lastMax.x = max.x;
                AddRect(lastMin, lastMax);
            }
        }

        

        private readonly BinaryHeap<SimplifyTreeNode> _open = new BinaryHeap<SimplifyTreeNode>((a, b) => a.Value < b.Value);
        private readonly List<SimplifyTreeNode> _tempList = new List<SimplifyTreeNode>();
        private readonly Stack<float2> _ans = new Stack<float2>();
        private bool[] _visited;
        private bool _visitNewNode;
        
        /// <summary>
        /// 查找和指定区域有重合部分的所有没有对象存在的节点
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        public void FindNodesWithoutObjects(in float2 min, in float2 max, List<SimplifyTreeNode> nodes)
        {
            var iMin = (int2)math.round(min * _resolution);
            var iMax = (int2)math.round(max * _resolution);
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
            var iStart = new int2((int) (start.x * _resolution), (int) (start.y * _resolution));
            var iEnd = new int2((int) (end.x * _resolution), (int) (end.y * _resolution));
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
            while (_open.Count > 0)
            {
                var now = _open.Pop();
                if (now.Min.x <= iEnd.x && now.Min.y <= iEnd.y && now.Max.x >= iEnd.x && now.Max.y >= iEnd.y)
                {
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
                                var sameX = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                                _ans.Push(new float2(sameX, samey) / _resolution);
                            }
                            else
                            {
                                var minX = math.max(now.Min.x, lastNow.Min.x);
                                var maxX = math.min(now.Max.x, lastNow.Max.x);
                                var ax = ((lastPos.x - nowPos.x) * samey - p) /
                                         (lastPos.y - nowPos.y);
                                if (ax < minX)
                                {
                                    _ans.Push(new float2(minX, samey) / _resolution);
                                }
                                else if (ax > maxX)
                                {
                                    _ans.Push(new float2(maxX, samey) / _resolution);
                                }
                                else
                                {
                                    _ans.Push(new float2(ax, samey) / _resolution);
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
                                _ans.Push(new float2(sameX, minY) / _resolution);
                            }
                            else if (ay > maxY)
                            {
                                _ans.Push(new float2(sameX, maxY) / _resolution);
                            }
                            else
                            {
                                _ans.Push(new float2(sameX, ay) / _resolution);
                            }
                        }
                        else
                        {
                            _ans.Push(nowPos / _resolution);
                        }

                        lastNow = now;
                        lastPos = nowPos;
                        now = now.LastNode;
                    }
                    _ans.Push(start);
                    break;
                }

                var min = now.Min - new int2(1, 1);
                var max = new int2(now.Max.x + 1, now.Min.y - 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
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

                min = new int2(now.Max.x + 1, now.Min.y);
                max = new int2(now.Max.x + 1, now.Max.y + 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
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

                min = new int2(now.Min.x - 1, now.Max.y + 1);
                max = new int2(now.Max.x, now.Max.y + 1);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
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

                min = new int2(now.Min.x - 1, now.Min.y - 1);
                max = new int2(now.Min.x - 1, now.Max.y);
                _tempList.Clear();
                FindNodesWithoutObjects(_head, in min, in max, _tempList);
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

            if (_ans.Count == 0) _ans.Push(end);
            return _ans;
        }

        /// <summary>
        /// 查询距离给定位置最近的实体
        /// （非最优解）
        /// </summary>
        /// <param name="pos">给定位置</param>
        /// <param name="obj">返回的最近对象</param>
        /// <returns></returns>
        public float FindNearObject(float2 pos, out SimplifyTreeNode obj)
        {
            if (CheackInited() == false)
            {
                obj = default;
                return 0;
            }

            var iPos = new int2((int) (pos.x * _resolution), (int) (pos.y * _resolution));
            return FindNearObject(_head, iPos, out obj) / _resolution;
        }

        /// <summary>
        /// 清除某个节点及其所有子节点
        /// </summary>
        /// <param name="now">节点</param>
        private void FakeClear(SimplifyTreeNode now)
        {
            for (var i = 0; i < 4; i++)
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
        private void Output(SimplifyTreeNode now, int deep, float time)
        {
            //Debug.LogFormat("{0},{1},{2}", deep, now.Min, now.Max);
            if (now.Flag)
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _resolution, 1, (now.Min.y - 0.5f) / _resolution),
                    new Vector3((now.Min.x - 0.5f) / _resolution, 1, (now.Max.y + 0.5f) / _resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _resolution, 1, (now.Max.y + 0.5f) / _resolution),
                    new Vector3((now.Max.x + 0.5f) / _resolution, 1, (now.Max.y + 0.5f) / _resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _resolution, 1, (now.Max.y + 0.5f) / _resolution),
                    new Vector3((now.Max.x + 0.5f) / _resolution, 1, (now.Min.y - 0.5f) / _resolution), Color.red, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _resolution, 1, (now.Min.y - 0.5f) / _resolution),
                    new Vector3((now.Min.x - 0.5f) / _resolution, 1, (now.Min.y - 0.5f) / _resolution), Color.red, time);
            }
            else
            {
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _resolution, 0, (now.Min.y - 0.5f) / _resolution),
                    new Vector3((now.Min.x - 0.5f) / _resolution, 0, (now.Max.y + 0.5f) / _resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Min.x - 0.5f) / _resolution, 0, (now.Max.y + 0.5f) / _resolution),
                    new Vector3((now.Max.x + 0.5f) / _resolution, 0, (now.Max.y + 0.5f) / _resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _resolution, 0, (now.Max.y + 0.5f) / _resolution),
                    new Vector3((now.Max.x + 0.5f) / _resolution, 0, (now.Min.y - 0.5f) / _resolution), Color.black, time);
                Debug.DrawLine(new Vector3((now.Max.x + 0.5f) / _resolution, 0, (now.Min.y - 0.5f) / _resolution),
                    new Vector3((now.Min.x - 0.5f) / _resolution, 0, (now.Min.y - 0.5f) / _resolution), Color.black, time);
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