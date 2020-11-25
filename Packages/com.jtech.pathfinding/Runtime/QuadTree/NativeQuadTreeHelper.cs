using System;
using JTech.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace JTech.PathFinding.QuadTree
{
    public static class NativeQuadTreeHelper
    {
        public static NativeArray<bool> Visited;
        public static NativeArray<int> LastNodeId;
        public static NativeArray<int> Dist2Start;
        public static NativeList<int> TempList = new NativeList<int>(Allocator.Persistent);
        public static NativeBinaryHeap<AStarStruct> Open = new NativeBinaryHeap<AStarStruct>(Allocator.Persistent);
        private static NativeQueue<float2> Path;
        
        public static NativeQueue<float2> RunAStar(in NativeQuadTree quadTree, float2 start, float2 end, Allocator allocator)
        {
            if (Visited.IsCreated == false || Visited.Length < quadTree.Length)
            {
                if (Visited.IsCreated)
                {
                    Visited.Dispose();
                    LastNodeId.Dispose();
                    Dist2Start.Dispose();
                }
                Visited = new NativeArray<bool>(quadTree.Length, Allocator.Persistent);
                LastNodeId = new NativeArray<int>(quadTree.Length, Allocator.Persistent);
                Dist2Start = new NativeArray<int>(quadTree.Length, Allocator.Persistent);
            }

            for (int i = 0; i < quadTree.Length; i++)
            {
                Visited[i] = false;
            }

            if (Path.IsCreated == false)
            {
                Path = new NativeQueue<float2>(allocator);
            }
            else
            {
                Path.Dispose();
                Path = new NativeQueue<float2>(allocator);
            }
            
            new AStar()
            {
                Start = end,
                End = start,
                QuadTree = quadTree,
                Path = Path,
                Visited = Visited,
                LastNodeId = LastNodeId,
                Dist2Start = Dist2Start,
                TempList = TempList,
                Open = Open
            }.Run();
            if (Path.Count == 0) Path.Enqueue(end);
            return Path;
        }
        
        public struct AStarStruct : IComparable<AStarStruct>, IEquatable<AStarStruct>
        {
            public int Id;
            public int Value;
            
            public int CompareTo(AStarStruct other)
            {
                return Value - other.Value;
            }

            public bool Equals(AStarStruct other)
            {
                return Id == other.Id;
            }

            public override bool Equals(object obj)
            {
                return obj is AStarStruct other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Id;
            }
        }
        
        /// <summary>
        /// A星寻路
        /// 求出来的并非最优解
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [BurstCompile]
        public struct AStar : IJob
        {
            public float2 Start;
            public float2 End;
            [ReadOnly] public NativeQuadTree QuadTree;
            [WriteOnly] public NativeQueue<float2> Path;
            
            public NativeArray<bool> Visited;
            public NativeArray<int> LastNodeId;
            public NativeArray<int> Dist2Start;
            public NativeList<int> TempList;
            public NativeBinaryHeap<AStarStruct> Open;

            public void Execute()
            {
                if (QuadTree.IsCreated == false) return;
                var iStart = new int2((int) (Start.x * QuadTree._resolution), (int) (Start.y * QuadTree._resolution));
                var iEnd = new int2((int) (End.x * QuadTree._resolution), (int) (End.y * QuadTree._resolution));
                TempList.Clear();
                QuadTree.FindNodesWithoutObjects(0, iStart, iStart, TempList);
                Open.Clear();
                for (int i = 0; i < TempList.Length; i++)
                {
                    var tempNode = QuadTree[TempList[i]];
                    var temp = math.abs(((tempNode.Max + tempNode.Min) / 2 - iEnd));
                    var data = new AStarStruct
                    {
                        Id = tempNode.Id, 
                        Value = temp.x + temp.y
                    };
                    Open.Push(data);
                    LastNodeId[tempNode.Id] = -1;
                    Dist2Start[tempNode.Id] = 0;
                    Visited[tempNode.Id] = true;
                }

                while (Open.Count > 0)
                {
                    var nowData = Open.Pop();
                    var now = QuadTree[nowData.Id];
                    if (now.Min.x <= iEnd.x && now.Min.y <= iEnd.y && now.Max.x >= iEnd.x && now.Max.y >= iEnd.y)
                    {
                        Path.Enqueue(End);
                        var lastPos = ((float2) (now.Min + now.Max)) / 2;
                        var lastNow = now;
                        var nowIndex = LastNodeId[nowData.Id];
                        while (nowIndex >= 0)
                        {
                            now = QuadTree[nowIndex];
                            var nowPos = ((float2) (now.Min + now.Max)) / 2;
                            var p = (lastPos.x * nowPos.y - nowPos.x * lastPos.y);
                            if ((lastNow.Max.y + 1 == now.Min.y) || (lastNow.Min.y == now.Max.y + 1))
                            {
                                var samey = lastNow.Max.y + 1 == now.Min.y ? now.Min.y : now.Max.y;
                                if ((lastNow.Max.x + 1 == now.Min.x) || (lastNow.Min.x == now.Max.x + 1))
                                {
                                    var samex = lastNow.Max.x + 1 == now.Min.x ? now.Min.x : now.Max.x;
                                    Path.Enqueue(new float2(samex, samey) / QuadTree._resolution);
                                }
                                else
                                {
                                    var minx = math.max(now.Min.x, lastNow.Min.x);
                                    var maxx = math.min(now.Max.x, lastNow.Max.x);
                                    var ax = ((lastPos.x - nowPos.x) * samey - p) /
                                             (lastPos.y - nowPos.y);
                                    if (ax < minx)
                                    {
                                        Path.Enqueue(new float2(minx, samey) / QuadTree._resolution);
                                    }
                                    else if (ax > maxx)
                                    {
                                        Path.Enqueue(new float2(maxx, samey) / QuadTree._resolution);
                                    }
                                    else
                                    {
                                        Path.Enqueue(new float2(ax, samey) / QuadTree._resolution);
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
                                    Path.Enqueue(new float2(samex, miny) / QuadTree._resolution);
                                }
                                else if (ay > maxy)
                                {
                                    Path.Enqueue(new float2(samex, maxy) / QuadTree._resolution);
                                }
                                else
                                {
                                    Path.Enqueue(new float2(samex, ay) / QuadTree._resolution);
                                }
                            }
                            else
                            {
                                Path.Enqueue(nowPos / QuadTree._resolution);
                            }

                            lastNow = now;
                            lastPos = nowPos;
                            nowIndex = LastNodeId[now.Id];
                        }

                        Path.Enqueue(Start);
                        break;
                    }
                    
                    var min = int2.zero;
                    var max = int2.zero;
                    for (int i = 0; i < 4; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                min = now.Min - new int2(1, 1);
                                max = new int2(now.Max.x + 1, now.Min.y - 1);
                                break;
                            case 1:
                                min = new int2(now.Max.x + 1, now.Min.y);
                                max = new int2(now.Max.x + 1, now.Max.y + 1);
                                break;
                            case 2:
                                min = new int2(now.Min.x - 1, now.Max.y + 1);
                                max = new int2(now.Max.x, now.Max.y + 1);
                                break;
                            case 3:
                                min = new int2(now.Min.x - 1, now.Min.y - 1);
                                max = new int2(now.Min.x - 1, now.Max.y);
                                break;
                        }

                        TempList.Clear();
                        QuadTree.FindNodesWithoutObjects(0, in min, in max, TempList);
                        for (int j = 0; j < TempList.Length; j++)
                        {
                            var t = TempList[j];
                            //TODO: 目前考虑稀疏图的情况下非最优解和最优解差距不是太大，这个地方对每个点只做了一次遍历，以后再修改。
                            if (Visited[t]) continue;
                            var tempNode = QuadTree[t];
                            var temp2End = math.abs((tempNode.Max + tempNode.Min) / 2 - iEnd);
                            var temp2Last = math.abs((tempNode.Max + tempNode.Min) / 2 - (now.Min + now.Max) / 2);
                            Dist2Start[tempNode.Id] = Dist2Start[now.Id] + temp2Last.x + temp2Last.y;
                            LastNodeId[t] = now.Id;
                            Open.Push(new AStarStruct
                            {
                                Id = tempNode.Id,
                                Value = temp2End.x + temp2End.y + Dist2Start[tempNode.Id]
                            });
                            Visited[tempNode.Id] = true;
                        }
                    }
                }
            }
        }
    }
}