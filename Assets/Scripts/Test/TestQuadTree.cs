using System;
using System.Collections;
using System.Collections.Generic;
using JTech.PathFinding.QuadTree;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestQuadTree : MonoBehaviour
{
    private QuadTree<TestQuadTreeData> _quadTree;
    private QuadTree<TestData2> _quadTree2;
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
    /// 对象大小
    /// </summary>
    [Range(1, 10)]
    public float ObjectSize = 10;
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
    private int _case;

    // Start is called before the first frame update
    void Start()
    {
        _quadTree = new QuadTree<TestQuadTreeData>(Offset, Resolution);
        //_quadTree2 = new QuadTree<TestData2>(Offset, Resolution);
        Case5();
    }

    /// <summary>
    /// 案例1：添加N个圆形区域
    /// </summary>
    void Case1()
    {
        _case = 1;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var radius = Random.Range(1, ObjectSize);
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddObject(new TestQuadTreeData(), new Circle(radius, pos));
            if (Debug)
            {
                var obj = Instantiate(Sphere);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
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
        _case = 2;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddObject(new TestQuadTreeData(), new ParallelRect(size, pos));
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
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
        _case = 3;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        var size = new float2(ObjectSize / 2, ObjectSize);
        var pos = new float2(0, 0);
        var forward = math.normalize(new float2(-1, 1));
        if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
        _quadTree.AddObject(new TestQuadTreeData(), new AnyForwardRect(size, pos, forward));
        if (Debug)
        {
            var obj = Instantiate(Cube);
            obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
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
        _case = 4;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(new TestQuadTreeData(), new AnyForwardRect(size, pos, forward));
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
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
        _case = 5;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(new TestQuadTreeData(), new AnyForwardRect(size, pos, forward));
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
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
        _case = 6;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(new TestQuadTreeData(), new AnyForwardRect(size, pos, forward));
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
        _case = 7;
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        var obj = new TestQuadTreeData();
        for (var i = 0; i < 3; i++)
        {
            obj = new TestQuadTreeData();
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(obj, new AnyForwardRect(size, pos, forward));
        }
        _quadTree.RemoveObjectInRect(obj, new float2(-MapSize / 2f, -MapSize / 2f), new float2(MapSize / 2f, MapSize / 2f));

        if (Debug) _quadTree.Output(10);
        _quadTree.FakeClear();
    }

    private MeshRenderer _lastMeshRenderer;
    public Material MatObstacles;
    public Material MatNearnestObstacles;
    /// <summary>
    /// 案例8：添加N个任意朝向的矩形区域
    /// 删除随机区域内的矩形
    /// 调用FindNearObject方法获取Start节点附近的对象
    /// （拖动StartPos可以颜色发生变化的就是查询结果）
    /// </summary>
    void Case8()
    {
        _case = 8;
        _quadTree2.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            
            //if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
                obj.transform.rotation =
                    quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
                _quadTree2.AddObject(new TestData2() { Obstacle = obj }, new AnyForwardRect(size, pos, forward));
            }
        }

        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree2.RemoveAllObjectsInRect(pos - size, pos + size);
        }
        _startPos = new GameObject("StartPos");
        if (Debug) _quadTree2.Output(1000);
    }

    private Stack<float2> path;

    void Update()
    {
        if (_case != 8)
        {
            NodeCount = _quadTree.Count;
            if (_startPos != null && _lastStartPos != _startPos.gameObject.transform.position)
            {
                _lastStartPos = _startPos.gameObject.transform.position;
                _player.transform.position = _lastStartPos;
                path = QuadTreePathFinding.AStar(_quadTree, new float2(_lastStartPos.x, _lastStartPos.z),
                    new float2(_lastEndPos.x, _lastEndPos.z), true);
            }

            if (_endPos != null && _lastEndPos != _endPos.gameObject.transform.position)
            {
                _lastEndPos = _endPos.gameObject.transform.position;
                _player.transform.position = _lastStartPos;
                path = QuadTreePathFinding.AStar(_quadTree, new float2(_lastStartPos.x, _lastStartPos.z),
                    new float2(_lastEndPos.x, _lastEndPos.z), true);
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
        else
        {
            if (_startPos != null && _lastStartPos != _startPos.gameObject.transform.position)
            {
                _lastStartPos = _startPos.gameObject.transform.position;
                TestData2 obstacle = null;
                _quadTree2.FindNearObject(new float2(_lastStartPos.x, _lastStartPos.z), out obstacle);
                if (_lastMeshRenderer != null)
                {
                    _lastMeshRenderer.material = MatObstacles;
                }
                if (obstacle != null)
                {
                    var mr = obstacle.Obstacle.GetComponent<MeshRenderer>();
                    mr.material = MatNearnestObstacles;
                    _lastMeshRenderer = mr;
                }
            }
        }
    }
}
