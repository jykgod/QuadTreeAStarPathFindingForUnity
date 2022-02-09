using System;
using System.Collections;
using System.Collections.Generic;
using JTech.PathFinding.QuadTree;
using JTech.Tools;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestNativeQuadTree : MonoBehaviour
{
    private NativeQuadTree _quadTree;
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
        Case1();
    }

    /// <summary>
    /// 案例1：添加N个任意朝向的矩形区域，并开启寻路
    /// （拖动StartPos或EndPos对象开启寻路）
    /// </summary>
    void Case1()
    {
        _case = 5;
        _quadTree = new NativeQuadTree(new float4(0, 0, MapSize, MapSize), Resolution, 1, Allocator.Persistent);
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, ObjectSize), Random.Range(1, ObjectSize));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddRectObject(in size, in pos, forward, 1, Offset);
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
                obj.transform.rotation =
                    quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
            }
        }
        _quadTree.Output(1000);
        _startPos = new GameObject("StartPos");
        _endPos = new GameObject("EndPos");
        _player = Instantiate(Cube);
        _player.name = "Player";
    }
    
    private NativeQueue<float2> path;

    void Update()
    {
        if (_quadTree.IsCreated == false) return;
        NodeCount = _quadTree.Length;
        if (_startPos != null && _lastStartPos != _startPos.gameObject.transform.position)
        {
            _lastStartPos = _startPos.gameObject.transform.position;
            _player.transform.position = _lastStartPos;
            var date = DateTime.UtcNow;
            path = NativeQuadTreePathFinding.RunAStar(_quadTree, new float2(_lastStartPos.x, _lastStartPos.z), new float2(_lastEndPos.x, _lastEndPos.z), Allocator.Persistent);
            UnityEngine.Debug.Log((DateTime.UtcNow - date).TotalSeconds);
        }

        if (_endPos != null && _lastEndPos != _endPos.gameObject.transform.position)
        {
            _lastEndPos = _endPos.gameObject.transform.position;
            _player.transform.position = _lastStartPos;
            path = NativeQuadTreePathFinding.RunAStar(_quadTree, new float2(_lastStartPos.x, _lastStartPos.z), new float2(_lastEndPos.x, _lastEndPos.z), Allocator.Persistent);
        }

        if (path.IsCreated && path.Count > 0)
        {
            var pos = path.Peek();
            var v = new float2(pos.x - _player.transform.position.x, pos.y - _player.transform.position.z);
            if (math.lengthsq(v) < 0.5f)
            {
                path.Dequeue();
            }
            else
            {
                v = math.normalizesafe(v);
                _player.transform.position = new Vector3(_player.transform.position.x + v.x * 0.5f, 0,
                    _player.transform.position.z + v.y * 0.5f);
            }
        }
    }

    private void OnDestroy()
    {
        NativeQuadTreePathFinding.ClearAll();
        if (_quadTree.IsCreated)
        {
            _quadTree.Dispose();
        }
    }
}
