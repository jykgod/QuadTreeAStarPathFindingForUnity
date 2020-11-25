using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JTech.Tools;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JTech.PathFinding.QuadTree
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeQuadTree : IDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_ListData;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_SequenceId;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#if UNITY_2020_1_OR_NEWER
private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeList<T>>();
        [BurstDiscard]
        private static void CreateStaticSafetyId()
        {
            s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<NativeList<T>>();
        }

#endif
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel          m_DisposeSentinel;
#endif
        private Allocator                m_AllocatorLabel;
        
        public bool IsCreated => m_ListData != null;
        public int Length => m_ListData->Length;
        
        internal readonly float _resolution;
        internal readonly float _eps;
        internal readonly int2 _min;
        internal readonly int2 _max;

        /// <summary>
        /// 创建四叉树
        /// </summary>
        /// <param name="rect">世界坐标下的四叉树区域范围float4(x,y,width,height)</param>
        /// <param name="resolution">分辨率，代表最小细分大小,分辨率越高，最小细分区域越小</param>
        /// <param name="initCapital">初始化容量</param>
        /// <param name="allocator"></param>
        /// <param name="eps">误差,用于合并</param>
        public NativeQuadTree(float4 rect, float resolution, int initCapital, Allocator allocator, float eps = 0.001f) : this(initCapital <= 0 ? 1 : initCapital, allocator, 2)
        {
            _eps = eps;
            _resolution = resolution;
            _min = new int2((int) (rect.x * _resolution - rect.z / 2f * _resolution),
                (int) (rect.y * _resolution - rect.w / 2f * _resolution));
            _max = new int2((int) math.ceil(rect.x * _resolution + rect.z / 2f * _resolution),
                (int) math.ceil(rect.y * _resolution + rect.w / 2f * _resolution));
            m_ListData->Add(new NativeQuadTreeNode(0, 0, _min, _max, allocator));
        }

        private NativeQuadTree(int capacity, Allocator allocator, int disposeSentinelStackDepth)
        {
            var totalSize = UnsafeUtility.SizeOf(typeof(NativeQuadTreeNode)) * (long)capacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be >= 0");

            CollectionHelper.CheckIsUnmanaged<NativeQuadTreeNode>();

            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(capacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#if UNITY_2020_1_OR_NEWER
            if (s_staticSafetyId.Data == 0)
            {
                CreateStaticSafetyId();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_staticSafetyId.Data);
#endif
#endif
            m_ListData = UnsafeList.Create(UnsafeUtility.SizeOf<NativeQuadTreeNode>(), UnsafeUtility.AlignOf<NativeQuadTreeNode>(), capacity, allocator);
            m_SequenceId = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), 0, allocator);
            m_AllocatorLabel = allocator;
            _eps = 0;
            _resolution = 1;
            _min = int2.zero;
            _max = int2.zero;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        private int CreateNodeId()
        {
            if (m_SequenceId->Length == 0)
            {
                return m_ListData->Length;
            }

            var ret = UnsafeUtility.ReadArrayElement<int>(m_SequenceId->Ptr, m_SequenceId->Length - 1);
            m_SequenceId->Length--;
            return ret;
        }

        private void CollectNodeId(int index)
        {
            m_SequenceId->Add(index);
        }
        
        public NativeQuadTreeNode this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                CheckIndexInRange(index, m_ListData->Length);
                return UnsafeUtility.ReadArrayElement<NativeQuadTreeNode>(m_ListData->Ptr, index);
            }
            private set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                CheckIndexInRange(index, m_ListData->Length);
                UnsafeUtility.WriteArrayElement(m_ListData->Ptr, index, value);
            }
        }
        
        private bool DownTree(ref NativeQuadTreeNode now)
        {
            if (now.Flag == false) return false;
            for (int i = 0; i < 4; i++)
            {
                if (now[i] >= 0)
                {
                    var child = this[now[i]];
                    child.Flag = true;
                    child.Data = now.Data;
                }
            }

            now.Flag = false;
            return true;
        }
        
        private void UpTree(ref NativeQuadTreeNode now)
        {
            if (now.Has == false) return;

            var s = float4.zero;
            var k = 0L;
            for (int i = 0; i < 4; i++)
            {
                if (now[i] >= 0)
                {
                    var childNode = this[now[i]];
                    var temp = childNode.Max - childNode.Min;
                    s += childNode.Data * (temp.x + 1) * (temp.y + 1);
                    k += (long)(temp.x + 1) * (temp.y + 1);
                }
            }
            now.Data = s / k;

            for (int i = 0; i < 4; i++)
            {
                if (now[i] >= 0)
                {
                    if (this[now[i]].Flag == false)
                    {
                        return;
                    }
                    else
                    {
                        var b = this[now[i]].Data - now.Data > _eps;
                        if (b.x && b.y && b.z && b.w)
                        {
                            return;
                        }
                    }
                }
            }

            now.Flag = true;
            for (int i = 0; i < 4; i++)
            {
                if (now[i] >= 0)
                {
                    CollectNodeId(now[i]);
                    now[i] = -1;
                }
            }
        }
        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="now"></param>
        private void CreateChildren(ref NativeQuadTreeNode now)
        {
            for (int i = 0; i < 4; i++)
            {
                if (now[i] == -1)
                {
                    if (i == 0 || (i == 1 && now.Max.x != now.Min.x) ||
                        (i == 2 && now.Max.y != now.Min.y) ||
                        (i == 3 && now.Max.x != now.Min.x && now.Max.y != now.Min.y))
                    {
                        CreateChildren(ref now, i);
                    }
                }
            }
        }

        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="now"></param>
        /// <param name="index"></param>
        private void CreateChildren(ref NativeQuadTreeNode now, int index)
        {
            int id = 0;
            int2 min = int2.zero, max = int2.zero;
            switch (index)
            {
                case 0:
                    id = now[0] = CreateNodeId();
                    min = now.Min;
                    max = now.Min + (now.Max - now.Min) / 2;
                    break;
                case 1:
                    id = now[1] = CreateNodeId();
                    min = new int2(now.Min.x + (now.Max.x - now.Min.x) / 2 + 1, now.Min.y);
                    max = new int2(now.Max.x, now.Min.y + (now.Max.y - now.Min.y) / 2);
                    break;
                case 2:
                    id = now[2] = CreateNodeId();
                    min = new int2(now.Min.x, now.Min.y + (now.Max.y - now.Min.y) / 2 + 1);
                    max = new int2(now.Min.x + (now.Max.x - now.Min.x) / 2, now.Max.y);
                    break;
                case 3:
                    id = now[3] = CreateNodeId();
                    min = now.Min + (now.Max - now.Min) / 2 + new int2(1, 1);
                    max = now.Max;
                    break;
            }

            if (id < m_ListData->Length)
            {
                this[id] = new NativeQuadTreeNode(id, 0, min, max, m_AllocatorLabel);
            }
            else
            {
                m_ListData->Add(new NativeQuadTreeNode(id, 0, min, max, m_AllocatorLabel));
            }
        }

        /// <summary>
        /// 区域更新
        /// </summary>
        /// <param name="nowIndex">当前节点的下标</param>
        /// <param name="min">区域最小点</param>
        /// <param name="max">区域最大点</param>
        private void UpdateRect(in int nowIndex, in int2 min, in int2 max, in float4 data)
        {
            var now = this[nowIndex];
            if (now.Flag == true) return;
            if (min.x <= now.Min.x && min.y <= now.Min.y && max.x >= now.Max.x && max.y >= now.Max.y)
            {
                now.Has = now.Flag = true;
                now.Data = data;
                this[nowIndex] = now;
                return;
            }

            CreateChildren(ref now);

            DownTree(ref now);

            int2 center = now.Min + (now.Max - now.Min) / 2;
            if (min.x <= center.x && min.y <= center.y)
            {
                UpdateRect(now[0], in min, in max, data);
                now.Has = true;
            }

            if (now[1] >= 0 && max.x > center.x && min.y <= center.y)
            {
                UpdateRect(now[1], in min, in max, data);
                now.Has = true;
            }

            if (now[2] >= 0 && min.x <= center.x && max.y > center.y)
            {
                UpdateRect(now[2], in min, in max, data);

                now.Has = true;
            }

            if (now[3] >= 0 && max.x > center.x && max.y > center.y)
            {
                UpdateRect(now[3], in min, in max, data);
                now.Has = true;
            }
            UpTree(ref now);
            this[nowIndex] = now;
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
        internal void FindNodesWithoutObjects(int nowIndex, in int2 min, in int2 max, NativeList<int> nodes)
        {
            while (true)
            {
                //if (_visitNewNode && _visited[now.Id]) return; TODO: A* 计算时再判断
                var now = this[nowIndex];
                if (now.Flag)
                {
                    return;
                }

                if (now.Has == false)
                {
                    nodes.Add(nowIndex);
                    return;
                }

                if (DownTree(ref now)) this[nowIndex] = now;
                var center = now.Min + (now.Max - now.Min) / 2;
                if (now[0] >= 0 && min.x <= center.x && min.y <= center.y)
                {
                    FindNodesWithoutObjects(now[0], in min, in max, nodes);
                }

                if (now[1] >= 0 && max.x > center.x && min.y <= center.y)
                {
                    FindNodesWithoutObjects(now[1], in min, in max, nodes);
                }

                if (now[2] >= 0 && min.x <= center.x && max.y > center.y)
                {
                    FindNodesWithoutObjects(now[2], in min, in max, nodes);
                }

                if (now[3] >= 0 && max.x > center.x && max.y > center.y)
                {
                    nowIndex = now[3];
                    continue;
                }
                break;
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
        public void AddParallelRectObject(in float2 halfSize, in float2 pos, in float4 data, float offset)
        {
            if (IsCreated == false) return;
            int2 min = new int2((int) (pos.x * _resolution - (halfSize.x + offset) * _resolution), (int) (pos.y * _resolution - (halfSize.y + offset) * _resolution));
            int2 max = new int2((int) math.ceil(pos.x * _resolution + (halfSize.x + offset) * _resolution),
                (int) math.ceil(pos.y * _resolution + (halfSize.y + offset) * _resolution));
            min = math.clamp(min, _min, _max);
            max = math.clamp(max, _min, _max);
            AddRect(min, max, data);
        }
        
        [NativeSetClassTypeToNullOnSchedule]
        private static readonly int2[] _delta = new int2[] {new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1)};
        [NativeSetClassTypeToNullOnSchedule]
        private static readonly BinaryHeap<int2> _rectClipHeap = new BinaryHeap<int2>((a, b) => a.x < b.x || (a.x == b.x && a.y < b.y));
        /// <summary>
        /// 添加任意矩形区域
        /// ps:
        ///     如果添加的矩形平行于最标轴或近似平行于坐标轴请使用AddParallelRectEntity函数添加该对象。
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="halfSize"></param>
        /// <param name="pos"></param>
        /// <param name="forward">xz平面下对象本地z轴的世界朝向</param>
        public void  AddRectObject(in float2 halfSize, in float2 pos, in float2 forward, in float4 data, float offset)
        {
            if (IsCreated == false) return;
            _rectClipHeap.FakeClear();
            var lenForward = math.length(forward);
            var cosa = forward.y / lenForward;
            var sina = forward.x / lenForward;
            var points = new int2[4];
            var rotateMatrix = new float2x2(cosa, -sina, sina, cosa);
            var hs = halfSize + new float2(offset, offset);
            for (int i = 0; i < 4; i++)
            {
                points[i] = (int2) math.round((math.mul(hs * _delta[i], rotateMatrix) + pos) * _resolution);
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
                    AddRect(s, s, data);
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
                                AddRect(lastmin, lastmax, data);
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
                AddRect(min, max, data);
            }
            else
            {
                lastmin.y = minup < 0 ? lastmin.y + minup : lastmin.y;
                lastmax.y = maxup > 0 ? lastmax.y + maxup : lastmax.y;
                lastmax.x = max.x;
                AddRect(lastmin, lastmax, data);
            }
        }

        /// <summary>
        /// 添加坐标换算后的矩形区域
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        private void AddRect(in int2 min, in int2 max, in float4 data)
        {
            if (_max.x < min.x || _min.x > max.x || _max.y < min.y || _min.y > max.y) return;
            UpdateRect(0, min, max, data);
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            UnsafeList.Destroy(m_ListData);
            m_ListData = null;
            UnsafeList.Destroy(m_SequenceId);
            m_SequenceId = null;
        }
        
        #if QUAD_TREE_DEBUG
        private void Output(int index, int deep, float time)
        {
            var now = this[index];
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
            for (int i = 0; i < 4; i++)
            {
                if (now[i] >= 0)
                {
                    Output(now[i], deep + 1, time);
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
            if (IsCreated == false) return;
            Output(0, 0, time);
#endif
        }
    }
}