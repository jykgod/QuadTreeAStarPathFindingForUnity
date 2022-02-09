using Unity.Mathematics;

namespace JTech.Tools
{
    public interface IObstacle
    {
        /// <summary>
        /// 返回为细分后的小矩形左下坐标以及右上坐标
        /// xy:左下坐标 LeftBottom(x,y)
        /// zw:右上坐标 RightTop(z,w)
        /// 如果想要减少GC开销可以考虑添加一个新的接口来实现细分
        /// </summary>
        /// <param name="padding"></param>
        /// <param name="scale"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        int4[] SplitToRect(in float padding, in float resolution, in int2 boundaryMin, in int2 boundaryMax);
    }
}