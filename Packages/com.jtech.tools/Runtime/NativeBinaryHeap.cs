using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace JTech.Tools
{
    /// <summary>
    /// 二叉堆
    /// 堆顶为最小元素
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeBinaryHeap<T> : IDisposable
        where T : struct, IComparable<T>, IEquatable<T>
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#if UNITY_2020_1_OR_NEWER
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeBinaryHeap<T>>();
        [BurstDiscard]
        private static void CreateStaticSafetyId()
        {
            s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<NativeBinaryHeap<T>>();
        }

#endif
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel          m_DisposeSentinel;
#endif
        private Allocator                m_AllocatorLabel;
        
        public NativeBinaryHeap(Allocator allocator)
            : this(1, allocator, 2)
        {
        }
        
        public NativeBinaryHeap(int initialCapacity, Allocator allocator)
            : this(initialCapacity, allocator, 2)
        {
        }

        private NativeBinaryHeap(int initialCapacity, Allocator mAllocator, int disposeSentinelStackDepth)
        {
            var totalSize = UnsafeUtility.SizeOf<T>() * (long)initialCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (mAllocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(mAllocator));
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");

            // CollectionHelper.CheckIsUnmanaged<T>();

            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, mAllocator);
#if UNITY_2020_1_OR_NEWER
            if (s_staticSafetyId.Data == 0)
            {
                CreateStaticSafetyId();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_staticSafetyId.Data);
#endif
#endif
            m_ListData = UnsafeList.Create(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), initialCapacity, mAllocator);

            m_AllocatorLabel = mAllocator;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }
        
        public int Count => m_ListData->Length;

        public T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                CheckIndexInRange(index, m_ListData->Length);
                return UnsafeUtility.ReadArrayElement<T>(m_ListData->Ptr, index);
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

        public NativeArray<T> AsArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_ListData->Ptr, m_ListData->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
            return array;
        }
        
        public T[] ToArray()
        {
            return AsArray().ToArray();
        }

        public bool IsCreated => m_ListData != null;

        private void UpHeap(int now)
        {
            while (now > 0)
            {
                var f = (now - 1) / 2;
                if (this[now].CompareTo(this[f]) < 0)
                {
                    Swap(now, f);
                    now = f;
                }
                else
                {
                    break;
                }
            }
        }

        private void DownHeap(int now)
        {
            while (true)
            {
                var lc = now * 2 + 1;
                var rc = lc + 1;
                if (lc < Count)
                {
                    if (this[lc].CompareTo(this[now]) < 0)
                    {
                        if (rc < Count && this[rc].CompareTo(this[lc]) < 0)
                        {
                            Swap(now, rc);
                            now = rc;
                            continue;
                        }
                        Swap(now, lc);
                        now = lc;
                        continue;
                    }
                    if (rc < Count && this[rc].CompareTo(this[lc]) < 0)
                    {
                        Swap(now, rc);
                        now = rc;
                        continue;
                    }

                }

                break;
            }
        }

        private void Swap(int x, int y)
        {
            var t = this[x];
            this[x] = this[y];
            this[y] = t;
        }
        
        /// <summary>
        /// 取出堆顶元素
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            return this[0];
        }
        /// <summary>
        /// 弹出堆顶元素
        /// </summary>
        /// <returns></returns>
        public T Pop()
        {
            T ret = this[0];
            this[0] = this[Count - 1];
            m_ListData->Length--;
            DownHeap(0);
            return ret;
        }

        /// <summary>
        /// 插入数据
        /// 时间复杂度:O(log(n))
        /// 堆空间不够时会自动扩展，每次新扩展出一倍的空间
        /// 尽量在使用时估算好需要用到的空间大小，避免重新分配内存带来的开销
        /// </summary>
        /// <param name="data"></param>
        public void Push(T data)
        {
            m_ListData->Add(data);
           
            UpHeap(m_ListData->Length - 1);
        }

        /// <summary>
        /// 从堆中移除元素
        /// 时间复杂度:O(n)
        /// </summary>
        /// <param name="data"></param>
        public bool Remove(T data)
        {
            var i = 0;
            var count = m_ListData->Length;
            for (; i < count; i++)
            {
                if (this[i].Equals(data))
                {
                    break;
                }
            }
            if (i == count) return false;
            this[i] = this[count - 1];
            m_ListData->Length--;
            DownHeap(i);
            return true;
        }
        /// <summary>
        /// 清除堆中数据
        /// 并不会释放内存
        /// </summary>
        public void Clear()
        {
            m_ListData->Length = 0;
        }
        /// <summary>
        /// 检查堆数据是否为空
        /// </summary>
        /// <returns></returns>
        public bool Empty()
        {
            return Count == 0;
        }
        
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            UnsafeList.Destroy(m_ListData);
            m_ListData = null;
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }
    }
}