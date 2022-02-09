﻿using System;
using System.Collections.Generic;
using JTech.PathFinding.QuadTree;
using JTech.Tools;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestSimplifyQuadTree : MonoBehaviour
{
    private SimplifyQuadTree _quadTree;
    public int NodeCount;
    public float Resolution = 2;
    public float Offset = 0;
    public int MapSize = 100;
    public int ObjectCount = 50;
    public bool Debug = true;
    public GameObject Sphere;
    public GameObject Cube;
    private GameObject _endPos;
    private GameObject _startPos;
    private GameObject _player;
    private Vector3 _lastEndPos;
    private Vector3 _lastStartPos;

    // Start is called before the first frame update
    void Start()
    {
        _quadTree = new SimplifyQuadTree(Offset, Resolution);
        Case5();
    }

    void Case1()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            float radius = Random.Range(1, 10);
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddObject(new Circle(radius, pos));
            if (Debug)
            {
                var obj = Instantiate(Sphere);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                obj.name = "Sphere:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(radius, radius, radius);
            }
        }

        if (Debug) _quadTree.Output(1000);
        _quadTree.FakeClear();
    }

    void Case2()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            _quadTree.AddObject(new ParallelRect(size, pos));
            if (Debug)
            {
                var obj = Instantiate(Cube);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                obj.name = "Cube:" + i;
                obj.transform.position = new Vector3(pos.x, 0, pos.y);
                obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
            }
        }

        if (Debug) _quadTree.Output(1000);
        _quadTree.FakeClear();
    }

    void Case3()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        var size = new float2(5, 10);
        var pos = new float2(0, 0);
        var forward = math.normalize(new float2(-1, 1));
        if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
        _quadTree.AddObject(new AnyForwardRect(size, pos, forward));
        if (Debug)
        {
            var obj = Instantiate(Cube);
            obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            obj.name = "Cube:" + 0;
            obj.transform.position = new Vector3(pos.x, 0, pos.y);
            obj.transform.localScale = new Vector3(size.x * 2, 1, size.y * 2);
            obj.transform.rotation = quaternion.LookRotation(new float3(forward.x, 0, forward.y), new float3(0, 1, 0));
        }

        if (Debug) _quadTree.Output(1000);
        _quadTree.FakeClear();
    }

    void Case4()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(new AnyForwardRect(size, pos, forward));
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

        if (Debug) _quadTree.Output(1000);
        _quadTree.FakeClear();
    }

    void Case5()
    {
        _quadTree.Init(new Rect(0, 0, MapSize, MapSize));
        for (var i = 0; i < ObjectCount; i++)
        {
            var size = new float2(Random.Range(1, 10), Random.Range(1, 10));
            var pos = new float2(Random.Range(-MapSize / 2, MapSize / 2), Random.Range(-MapSize / 2, MapSize / 2));
            var forward = math.normalizesafe(new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            if (math.lengthsq(forward) < 0.1f) forward = new float2(0, 1);
            _quadTree.AddObject(new AnyForwardRect(size, pos, forward));
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
        if (Debug) _quadTree.Output(1000);
    }

    private Stack<float2> path;

    void Update()
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
}