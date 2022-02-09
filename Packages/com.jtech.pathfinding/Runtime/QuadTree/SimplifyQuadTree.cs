using System.Collections.Generic;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;

namespace JTech.PathFinding.QuadTree
{
    /// <summary>
    /// 四叉树节点
    /// </summary>
    public class SimplifyTreeNode : IQuadTreeNode
    {
        public int Id { get; set; }
        internal readonly SimplifyTreeNode[] C = new SimplifyTreeNode[4];
        internal readonly int2[] Bounds = new int2[4];
        internal readonly  SimplifyTreeNode[] BoundsNodes = new SimplifyTreeNode[4];
        public int2 Min { get; set; }
        public int2 Max { get; set; }
        internal bool Flag;
        internal bool Has;

        /// <summary>
        /// Astar使用的估值
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Astar使用的从起始点到当前点的距离
        /// </summary>
        public int Dist2Start { get; set; }

        /// <summary>
        /// Astar使用的路径上一个区域的id
        /// </summary>
        public IQuadTreeNode LastNode { get; set; }

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

        internal int Count { get; private set; } = 0;

        internal SimplifyNodePool(int initCapital)
        {
            Count = initCapital;
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
                ret = new SimplifyTreeNode(Count++);
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
    public class SimplifyQuadTree : IQuadTree
    {
        private readonly float _padding;
        private readonly SimplifyNodePool _pool;
        private readonly int2[] _delta = {new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1)};
        private SimplifyTreeNode _head;
        public IQuadTreeNode Head => _head;
        public int Count => _pool.Count;

        public float Resolution { get; }

        public SimplifyQuadTree(float padding = 0, float resolution = 2, int initCapital = 1)
        {
            _padding = padding;
            Resolution = resolution;
            _pool = new SimplifyNodePool(initCapital);
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rect"></param>
        public void Init(Rect rect)
        {
            _head = _pool.Get(
                new int2((int) (rect.x * Resolution - rect.width / 2f * Resolution),
                    (int) (rect.y * Resolution - rect.height / 2f * Resolution)),
                new int2((int) math.ceil(rect.x * Resolution + rect.width / 2f * Resolution),
                    (int) math.ceil(rect.y * Resolution + rect.height / 2f * Resolution)));
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
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void UpdateRect(SimplifyTreeNode now, in int2 min, in int2 max)
        {
            if (now.Flag) return;
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
        /// <param name="node"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        public void FindNodesWithoutObjects(IQuadTreeNode node, in int2 min, in int2 max, ICollection<IQuadTreeNode> nodes)
        {
            var now = node as SimplifyTreeNode;
            while (true)
            {
                // if (_visitNewNode && _visited[now.Id]) return;
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

            if (now.Has) return;
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
            var iMin = (int2)math.round(min * Resolution);
            var iMax = (int2)math.round(max * Resolution);
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
            var iPos = new int2((int) (pos.x * Resolution), (int) (pos.y * Resolution));
            return CheckHasObject(_head, in iPos);
        }

        /// <summary>
        /// 添加坐标换算后的矩形区域
        /// </summary>
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
        public bool CheackInited()
        {
            return _head != null;
        }
        
        public void AddObject(IObstacle obstacle)
        {
            if (CheackInited() == false) return;
            var rects = obstacle.SplitToRect(_padding, Resolution, _head.Min, _head.Max);
            for (var i = 0; i < rects.Length; i++)
            {
                AddRect(rects[i].xy, rects[i].zw);
            }
        }
        
        /// <summary>
        /// 查找和指定区域有重合部分的所有没有对象存在的节点
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="nodes"></param>
        public void FindNodesWithoutObjects(in float2 min, in float2 max, List<IQuadTreeNode> nodes)
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
        public float FindNearObject(float2 pos, out SimplifyTreeNode obj)
        {
            if (CheackInited() == false)
            {
                obj = default;
                return 0;
            }

            var iPos = new int2((int) (pos.x * Resolution), (int) (pos.y * Resolution));
            return FindNearObject(_head, iPos, out obj) / Resolution;
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