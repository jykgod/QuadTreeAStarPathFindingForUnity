using System.Collections;
using System.Collections.Generic;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestQuadTree : MonoBehaviour
{
    private QuadTree<TestData> _quadTree;
    /// <summary>
    /// 生成后的四叉树节点个数（仅供查看不能修改）
    /// </summary>
    public int NodeCount;
    /// <summary>
    /// 生成图的分辨率
    /// </summary>
    public float Resolution = 2;
    /// <summary>
    /// 添加进图的对象大小偏移
    /// </summary>
    public float Offset = 0;
    /// <summary>
    /// 地图大小
    /// </summary>
    public int MapSize = 100;
    /// <summary>
    /// 对象个数
    /// </summary>
    public int ObjectCount = 50;
    public bool Debug = true;
    /// <summary>
    /// 圆形对象预制
    /// </summary>
    public GameObject Sphere;
    /// <summary>
    /// 方形对象预制
    /// </summary>
    public GameObject Cube;
    private GameObject _endPos;
    private GameObject _startPos;
    private GameObject _player;
    private Vector3 _lastEndPos;
    private Vector3 _lastStartPos;

    // Start is called before the first frame update
    void Start()
    {
        _quadTree = new QuadTree<TestData>(Offset, Resolution);
        Case5();
    }

    /// <summary>
    /// 案例1：添加N个圆形区域
    /// </summary>
    void Case1()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (int i = 0; i < ObjectCount; i++)
        {
            float radius = Random.Range(1, 10);
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddCircleObject(new TestData(), radius, in pos);
            if (Debug)
            {
                var obj = Instantiate(Sphere);
                obj.name = "Sphere:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(radius, radius, radius);
            }
        }

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    /// <summary>
    /// 案例2：添加N个平行于坐标轴的矩形区域
    /// </summary>
    void Case2()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (int i = 0; i < ObjectCount; i++)
        {
            float2 size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddParallelRectObject(new TestData(), in size, in pos);
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
            }
        }

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    /// <summary>
    /// 案例3：添加1个非平行于坐标轴的矩形区域
    /// </summary>
    void Case3()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        float2 size = new float2(5, 10);
        float2 pos = new float2(0, 0);
        float2 forward = math.normalize(new float2(-1, 1));
        if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
        _quadTree.AddRectObject(new TestData(), in size, in pos, forward);
        if (Debug)
        {
            var obj = Instantiate(Cube);
            obj.name = "Cube:" + 0;
            obj.transform.position = new Vector3(pos.x, 0, pos.y);
            obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
            obj.transform.rotation =
                quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
        }

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    /// <summary>
    /// 案例4：添加N个任意朝向的矩形区域
    /// </summary>
    void Case4()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (int i = 0; i < ObjectCount; i++)
        {
            float2 size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            float2 forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddRectObject(new TestData(), in size, in pos, forward);
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
                obj.transform.rotation =
                    quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
            }
        }

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    /// <summary>
    /// 案例5：添加N个任意朝向的矩形区域，并开启寻路
    /// （拖动StartPos或EndPos对象开启寻路）
    /// </summary>
    void Case5()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (int i = 0; i < ObjectCount; i++)
        {
            float2 size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            float2 forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddRectObject(new TestData(), in size, in pos, forward);
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
                obj.transform.rotation =
                    quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
            }
        }

        _startPos = new GameObject("StartPos");
        _endPos = new GameObject("EndPos");
        _player = Instantiate(Cube);
        _player.name = "Player";
        if (Debug) _quadTree.Output(100);
    }

    /// <summary>
    /// 案例6：添加N个任意朝向的矩形区域，
    /// 并使用RemoveAllObjectsInRect方法删除其中1/4的对象
    /// </summary>
    void Case6()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (int i = 0; i < ObjectCount; i++)
        {
            float2 size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            float2 forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddRectObject(new TestData(), in size, in pos, forward);
        }
        _quadTree.RemoveAllObjectsInRect(new float2(0, 0), new float2(MapSize / 2f, MapSize / 2f));

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    /// <summary>
    /// 案例7：添加3个任意朝向的矩形区域，并使用RemoveObjectInRect方法删除其中一个对象
    /// </summary>
    void Case7()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        var obj = new TestData();
        for (int i = 0; i < 3; i++)
        {
            obj = new TestData();
            float2 size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            float2 pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            float2 forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddRectObject(obj, in size, in pos, forward);
        }
        _quadTree.RemoveObjectInRect(obj, new float2(-MapSize / 2f, -MapSize / 2f), new float2(MapSize / 2f, MapSize / 2f));

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    private Stack<float2> path;

    void Update()
    {
        NodeCount = _quadTree.Count;
        if (_startPos != null && _lastStartPos != _startPos.gameObject.transform.position)
        {
            _lastStartPos = _startPos.gameObject.transform.position;
            _player.transform.position = _lastStartPos;
            path = _quadTree.AStar(new float2(_lastStartPos.x, _lastStartPos.z),
                new float2(_lastEndPos.x, _lastEndPos.z));
        }

        if (_endPos != null && _lastEndPos != _endPos.gameObject.transform.position)
        {
            _lastEndPos = _endPos.gameObject.transform.position;
            _player.transform.position = _lastStartPos;
            path = _quadTree.AStar(new float2(_lastStartPos.x, _lastStartPos.z),
                new float2(_lastEndPos.x, _lastEndPos.z));
        }

        if (path != null && path.Count > 0)
        {
            var pos = path.Peek();
            var v = new float2(pos.x - _player.transform.position.x, pos.y - _player.transform.position.z);
            if (math.lengthsq(v) < 0.5f)
            {
                path.Pop();
            }
            else
            {
                v = math.normalizesafe(v);
                _player.transform.position = new Vector3(_player.transform.position.x + v.x * 0.5f, 0,
                    _player.transform.position.z + v.y * 0.5f);
            }
        }
    }
}
