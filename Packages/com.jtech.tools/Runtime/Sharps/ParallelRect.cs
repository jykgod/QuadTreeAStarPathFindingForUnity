using Unity.Mathematics;

namespace JTech.Tools
{
    public class ParallelRect : IObstacle
    {
        private readonly float2 _halfSize;
        private readonly float2 _pos;
        
        public ParallelRect(float2 halfSize, float2 pos)
        {
            this._halfSize = halfSize;
            this._pos = pos;
        }

        public int4[] SplitToRect(in float padding, in float resolution, in int2 boundaryMin, in int2 boundaryMax)
        {
            var min = new int2((int) (_pos.x * resolution - (_halfSize.x + padding) * resolution),
                (int) (_pos.y * resolution - (_halfSize.y + padding) * resolution));
            var max = new int2((int) math.ceil(_pos.x * resolution + (_halfSize.x + padding) * resolution),
                (int) math.ceil(_pos.y * resolution + (_halfSize.y + padding) * resolution));
            min = math.clamp(min, boundaryMin, boundaryMax);
            max = math.clamp(max, boundaryMin, boundaryMax);
            return new[] {new int4(min, max)};
        }
    }
}