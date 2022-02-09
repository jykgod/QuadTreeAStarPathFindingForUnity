using System;
using System.Collections;
using System.Collections.Generic;
using JTech.PathFinding.QuadTree;
using JTech.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Jobs;

// public struct TestBinaryHeapData : IComparable<TestBinaryHeapData>
// {
//     public int value;
//     public int CompareTo(TestBinaryHeapData other)
//     {
//         return value.CompareTo(other.value);
//     }
// }

public class TestBinaryHeap : MonoBehaviour
{
    private NativeBinaryHeap<int> binaryHeap;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(UnsafeUtility.SizeOf(typeof(NativeQuadTreeNode)));

        binaryHeap = new NativeBinaryHeap<int>(100000, Allocator.Persistent);
        var binaryHeap2 = new BinaryHeap<int>((a, b) => { return a < b; });

        var date2 = DateTime.UtcNow;

        for (var i = 0; i < 100000; i++)
        {
            binaryHeap2.Push(i);
        }

        for (var i = 10000; i >= 0; i--)
        {
            binaryHeap2.Remove(i);
        }

        Debug.Log((DateTime.UtcNow - date2).TotalSeconds);
        Debug.Log(binaryHeap2.Count);
        Debug.Log(binaryHeap2.Peek());

        var date = DateTime.UtcNow;
        new AddJob()
        {
            p = binaryHeap,
        }.Run();
        new RemoveJob()
        {
            p = binaryHeap
        }.Run();

        Debug.Log((DateTime.UtcNow - date).TotalSeconds);
        Debug.Log(binaryHeap.Count);
        Debug.Log(binaryHeap.Peek());
    }

    void OnDestroy()
    {
        if (binaryHeap.IsCreated)
        {
            binaryHeap.Dispose();
        }
    }

    [BurstCompile]
    public struct AddJob : IJob
    {
        public NativeBinaryHeap<int> p;

        public void Execute()
        {
            for (var i = 0; i < 100000; i++)
            {
                p.Push(i);
            }
        }
    }

    [BurstCompile]
    public struct RemoveJob : IJob
    {
        public NativeBinaryHeap<int> p;

        public void Execute()
        {
            for (var i = 10000; i >= 0; i--)
            {
                p.Remove(i);
            }
        }
    }
}
