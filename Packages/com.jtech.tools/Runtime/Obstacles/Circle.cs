using System.Collections.Generic;
using Unity.Mathematics;

namespace JTech.Tools
{
    public class Circle : IObstacle
    {
        private readonly float _radius;
        private readonly float2 _pos;
        
        public Circle(float radius, float2 pos)
        {
            _radius = radius;
            _pos = pos;
        }

        public int4[] SplitToRect(in float padding, in float resolution, in int2 boundaryMin, in int2 boundaryMax)
        {
            var ret = new List<int4>();
            const float f1 = 2 - math.SQRT2;
            var r = (_radius + padding) * resolution;
            if (f1 * r < 2)
            {
                ret.Add(new int4(
                    (int) (_pos.x * resolution - r),
                    (int) (_pos.y * resolution - r),
                    (int) math.ceil(_pos.x * resolution + r),
                    (int) math.ceil(_pos.y * resolution + r)));
            }
            else if (f1 * r < 4)
            {
                var min = new int2((int) math.floor(_pos.x * resolution - r * math.SQRT2 * 0.5f),
                    (int) math.floor(_pos.y * resolution - r * math.SQRT2 * 0.5f));
                var max = new int2((int) math.ceil(_pos.x * resolution + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(_pos.y * resolution + r * math.SQRT2 * 0.5f));

                ret.Add(new int4((int) math.floor(_pos.x * resolution - r), min.y, (int) math.ceil(_pos.x * resolution + r),
                    max.y)); //left|center|right
                ret.Add(new int4(min.x, max.y, max.x, (int) math.ceil(_pos.y * resolution + r))); //top
                ret.Add(new int4(min.x, (int) math.floor(_pos.y * resolution - r), max.x, min.y)); //bottom
            }
            else
            {
                var off0 = (int) (f1 * r * 0.5);
                var min = new int2((int) math.floor(_pos.x * resolution - r * math.SQRT2 * 0.5f),
                    (int) math.floor(_pos.y * resolution - r * math.SQRT2 * 0.5f)) - new int2(off0 * 0.25);
                var max = new int2((int) math.ceil(_pos.x * resolution + r * math.SQRT2 * 0.5f),
                    (int) math.ceil(_pos.y * resolution + r * math.SQRT2 * 0.5f)) + new int2(off0 * 0.25f);
                ret.Add(new int4(min, max)); //center

                off0 += 1;
                ret.Add(new int4((int) math.floor(_pos.x * resolution - r), min.y + off0, min.x, max.y - off0)); //left
                ret.Add(new int4(min.x + off0, max.y, max.x - off0, (int) math.ceil(_pos.y * resolution + r))); //top
                ret.Add(new int4(max.x, min.y + off0, (int) math.ceil(_pos.x * resolution + r), max.y - off0)); //right
                ret.Add(new int4(min.x + off0, (int) math.floor(_pos.y * resolution - r), max.x - off0, min.y)); //bottom
            }

            return ret.ToArray();
        }
    }
}