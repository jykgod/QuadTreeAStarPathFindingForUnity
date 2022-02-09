using System.Collections.Generic;
using Unity.Mathematics;

namespace JTech.Tools
{
    public class AnyForwardRect : IObstacle
    {
        private readonly float2 halfSize;
        private readonly float2 pos;
        private readonly float2 forward;
        private readonly BinaryHeap<int2> _rectClipHeap = new BinaryHeap<int2>((a, b) => a.x < b.x || (a.x == b.x && a.y < b.y));
        private readonly int2[] _delta = { new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1) };
        
        public AnyForwardRect(float2 halfSize, float2 pos, float2 forward)
        {
            this.halfSize = halfSize;
            this.pos = pos;
            this.forward = forward;
        }

        public int4[] SplitToRect(in float padding, in float resolution, in int2 boundaryMin, in int2 boundaryMax)
        {
            var ret = new List<int4>();
            _rectClipHeap.FakeClear();
            var lenForward = math.length(forward);
            var cosA = forward.y / lenForward;
            var sinA = forward.x / lenForward;
            var points = new int2[4];
            var rotateMatrix = new float2x2(cosA, -sinA, sinA, cosA);
            var hs = halfSize + new float2(padding, padding);
            for (var i = 0; i < 4; i++)
            {
                points[i] = (int2) math.round((math.mul(hs * _delta[i], rotateMatrix) + pos) * resolution);
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
                    ret.Add(new int4(s, s));
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
                            if ((minUp == 0 || minUp + lastMin.y == min.y || lastMin.y == min.y) &&
                                (maxUp == 0 || maxUp + lastMax.y == max.y || lastMax.y == max.y))
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
                                ret.Add(new int4(lastMin, lastMax));
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
                ret.Add(new int4(min, max));
            }
            else
            {
                lastMin.y = minUp < 0 ? lastMin.y + minUp : lastMin.y;
                lastMax.y = maxUp > 0 ? lastMax.y + maxUp : lastMax.y;
                lastMax.x = max.x;
                ret.Add(new int4(lastMin, lastMax));
            }

            return ret.ToArray();
        }
    }
}