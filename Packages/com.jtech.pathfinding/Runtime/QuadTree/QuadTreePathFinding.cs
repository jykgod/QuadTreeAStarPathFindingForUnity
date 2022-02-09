using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JTech.PathFinding.QuadTree
{
    public class QuadTreePathFinding
    {
        private static readonly BinaryHeap<IQuadTreeNode> Open = new BinaryHeap<IQuadTreeNode>((a, b) => a.Value < b.Value);
        private static readonly List<IQuadTreeNode> TempList = new List<IQuadTreeNode>();
        private static readonly Stack<float2> Ans = new Stack<float2>();
        private static bool[] _visited;
        private static readonly Stopwatch Stopwatch = new Stopwatch();
        
        public static Stack<float2> AStar(IQuadTree quadTree, float2 start, float2 end, bool outputDeltaTime = false)
        {
            if (quadTree.CheackInited() == false) return null;
            if (outputDeltaTime) Stopwatch.Restart();
            var resolution = quadTree.Resolution;
            var iStart = new int2((int) (start.x * resolution), (int) (start.y * resolution));
            var iEnd = new int2((int) (end.x * resolution), (int) (end.y * resolution));
            var head = quadTree.Head;
            if (_visited == null || _visited.Length < quadTree.Count)
            {
                _visited = new bool[quadTree.Count];
            }
            else
            {
                for (var i = 0; i < _visited.Length; i++)
                {
                    _visited[i] = false;
                }
            }

            TempList.Clear();
            quadTree.FindNodesWithoutObjects(head, iStart, iStart, TempList);
            Open.FakeClear();
            for (var i = 0; i < TempList.Count; i++)
            {
                var temp = math.abs(((TempList[i].Max + TempList[i].Min) / 2 - iEnd));
                TempList[i].Value = temp.x + temp.y;
                TempList[i].Dist2Start = 0;
                TempList[i].LastNode = null;
                Open.Push(TempList[i]);
                _visited[TempList[i].Id] = true;
            }

            Ans.Clear();
            if (Open.Count == 0) return Ans;
            var now = Open.Peek();
            var minS = new int2[4];
            var maxS = new int2[4];
            while (Open.Count > 0)
            {
                now = Open.Pop();
                if (now.Min.x <= iEnd.x && now.Min.y <= iEnd.y && now.Max.x >= iEnd.x && now.Max.y >= iEnd.y)
                {
                    break;
                }

                minS[0] = new int2(now.Min.x - 1, now.Min.y - 1);
                maxS[0] = new int2(now.Max.x + 1, now.Min.y - 1);

                minS[1] = new int2(now.Max.x + 1, now.Min.y);
                maxS[1] = new int2(now.Max.x + 1, now.Max.y + 1);

                minS[2] = new int2(now.Min.x - 1, now.Max.y + 1);
                maxS[2] = new int2(now.Max.x, now.Max.y + 1);

                minS[3] = new int2(now.Min.x - 1, now.Min.y);
                maxS[3] = new int2(now.Min.x - 1, now.Max.y);

                for (var k = 0; k < 4; k++)
                {
                    TempList.Clear();
                    quadTree.FindNodesWithoutObjects(head, in minS[k], in maxS[k], TempList);
                    for (var i = 0; i < TempList.Count; i++)
                    {
                        //TODO: 要找优解情况下需要重复查找，这里暂且对每个节点只进行单次查找
                        if (_visited[TempList[i].Id]) continue;
                        var temp2End = math.abs((TempList[i].Max + TempList[i].Min) / 2 - iEnd);
                        var temp2Last = math.abs((TempList[i].Max + TempList[i].Min) / 2 - (now.Min + now.Max) / 2);
                        TempList[i].Dist2Start = now.Dist2Start + temp2Last.x + temp2Last.y;
                        TempList[i].Value = temp2End.x + temp2End.y + TempList[i].Dist2Start;
                        TempList[i].LastNode = now;
                        Open.Push(TempList[i]);
                        _visited[TempList[i].Id] = true;
                    }
                }
            }

            Ans.Push(end);
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
                        Ans.Push(new float2(sameX, samey) / resolution);
                    }
                    else
                    {
                        var minX = math.max(now.Min.x, lastNow.Min.x);
                        var maxX = math.min(now.Max.x, lastNow.Max.x);
                        var ax = ((lastPos.x - nowPos.x) * samey - p) / (lastPos.y - nowPos.y);
                        if (ax < minX)
                        {
                            Ans.Push(new float2(minX, samey) / resolution);
                        }
                        else if (ax > maxX)
                        {
                            Ans.Push(new float2(maxX, samey) / resolution);
                        }
                        else
                        {
                            Ans.Push(new float2(ax, samey) / resolution);
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
                        Ans.Push(new float2(sameX, minY) / resolution);
                    }
                    else if (ay > maxY)
                    {
                        Ans.Push(new float2(sameX, maxY) / resolution);
                    }
                    else
                    {
                        Ans.Push(new float2(sameX, ay) / resolution);
                    }
                }
                else
                {
                    Ans.Push(nowPos / resolution);
                }

                lastNow = now;
                lastPos = nowPos;
                now = now.LastNode;
            }

            Ans.Push(start);

            if (outputDeltaTime)
            {
                Stopwatch.Stop();
                Debug.Log($"[AStar] cost time:{Stopwatch.Elapsed.TotalMilliseconds} ms");
            }
#if QUAD_TREE_DEBUG
            var tans = new Stack<float2>(Ans.Reverse());
            var last = tans.Pop();
            while (tans.Count > 0)
            {
                var temp = tans.Pop();
                Debug.DrawLine(new Vector3(last.x, 3, last.y), new Vector3(temp.x, 3, temp.y), Color.yellow,
                    1f);
                last = temp;
            }
#endif
            return Ans;
        }
    }
}