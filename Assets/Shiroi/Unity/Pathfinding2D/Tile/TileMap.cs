﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shiroi.Unity.Pathfinding2D.Link;
using Shiroi.Unity.Pathfinding2D.Util;
using UnityEngine;
using Vexe.Runtime.Extensions;
using Vexe.Runtime.Types;

namespace Shiroi.Unity.Pathfinding2D.Tile {
    /// <summary>
    /// A Tile Map is responsible for detecting and generating <see cref="Node"/>s in a scene using the
    /// provided <see cref="LayerMask"/> on various <see cref="Physics2D"/> BoxCasts.
    /// </summary>
    /// This class does not handle links, if you wish to see the class for generating links, check
    /// <see cref="LinkMap"/>
    /// <seealso cref="LinkMap"/>
    public class TileMap : BaseBehaviour, IEnumerable<Node> {
        public const byte DefaultColorAlpha = 0x9B; //155
        public const float DefaultNodeBoxCastSize = 0.9F;
        public Color MaxColor = GetColor("E91E63");
        public Color MinColor = GetColor("00BCD4");
        public Color BorderLineColor = Color.green;
        public Color PlatformNodeColor = GetColor("3F51B5");
        public Color EdgeNodeColor = GetColor("9C27B0");
        public Color SoloNodeColor = GetColor("FF9800");
        public bool DrawBorder = true;
        public bool DrawPoints = true;
        public bool DrawNodes = true;
        public bool DrawPlatforms = true;

        [SerializeField, Hide]
        private MapPosition mapMinPos;

        [SerializeField, Hide]
        private MapPosition mapMaxPos;

        public LayerMask WorldMask;
        public Vector2 NodeSize;
        public Vector2 NodeBoxCastSize;

        [SerializeField, Hide]
        private TileDictionary nodeMap;

        [SerializeField]
        private List<Platform> platforms;

        private static Color GetColor(string color) {
            return ColorUtil.FromHex(color, DefaultColorAlpha);
        }

        private void Reset() {
            mapMinPos = new MapPosition(0, 0);
            mapMaxPos = new MapPosition(20, 20);
            NodeSize = Vector2.one;
            NodeBoxCastSize = new Vector2(0.9f, 0.9f);
        }

        public Platform GetPlatform(Node position) {
            var found = platforms.GetAllOrPut(platform => PlatformCheck(platform, position),
                () => new Platform(position));
            Platform plat;
            if (found.HasSingle()) {
                plat = found.Single();
            } else {
                plat = found.First();
                for (var i = 1; i < found.Count; i++) {
                    var p = found[i];
                    plat.Merge(p);
                    platforms.Remove(p);
                }
            }
            return plat;
        }

        private static bool PlatformCheck(Platform platform, Node position) {
            return platform.Contains(position) || platform.IsNextToAndAdd(position);
        }

        public List<Platform> Platforms {
            get { return platforms; }
        }

        public int TotalPlatforms {
            get { return platforms.Count; }
        }

        public int TotalNodes {
            get { return WalkableNodes.Count; }
        }

        public Vector2 Center {
            get { return new Vector2(XCenter, YCenter); }
        }

        public Vector2 Size {
            get { return new Vector2(XSize, YSize); }
        }

        public Vector2 SizeRaw {
            get { return new Vector2(XSizeRaw, YSizeRaw); }
        }

        public float NodeSizeX {
            get { return NodeSize.x; }
        }

        public float NodeSizeY {
            get { return NodeSize.y; }
        }

        public float XCenter {
            get { return (mapMaxPos.X * NodeSizeX + mapMinPos.X * NodeSizeX) / 2; }
        }

        public float YCenter {
            get { return (mapMaxPos.Y * NodeSizeY + mapMinPos.Y * NodeSizeY) / 2; }
        }

        public float XCenterRaw {
            get { return (float) (mapMaxPos.X + mapMinPos.X) / 2; }
        }

        public float YCenterRaw {
            get { return (float) (mapMaxPos.Y + mapMinPos.Y) / 2; }
        }

        public float XSize {
            get { return XSizeRaw * NodeSizeX; }
        }

        public float YSize {
            get { return YSizeRaw * NodeSizeY; }
        }

        public int XSizeRaw {
            get {
                AdjustXy();
                return MapMaxPos.X - MapMinPos.X;
            }
        }

        public int YSizeRaw {
            get {
                AdjustXy();
                return MapMaxPos.Y - MapMinPos.Y;
            }
        }

        public float DirectionVectorConversionLimitX {
            get { return NodeSizeX / 2 * 0.8f; }
        }

        public float DirectionVectorConversionLimitY {
            get { return NodeSizeX / 2 * 0.8f; }
        }

        [Show]
        public MapPosition MapMinPos {
            get { return mapMinPos; }
            set {
                mapMinPos = value;
                AdjustXy();
            }
        }

        [Show]
        public MapPosition MapMaxPos {
            get { return mapMaxPos; }
            set {
                mapMaxPos = value;
                AdjustXy();
            }
        }

        public void AdjustXy() {
            var minX = Mathf.Min(MapMaxPos.X, MapMinPos.X);
            var minY = Mathf.Min(MapMaxPos.Y, MapMinPos.Y);
            var maxX = Mathf.Max(MapMaxPos.X, MapMinPos.X);
            var maxY = Mathf.Max(MapMaxPos.Y, MapMinPos.Y);
            mapMinPos.X = minX;
            mapMinPos.Y = minY;
            mapMaxPos.X = maxX;
            mapMaxPos.Y = maxY;
        }
        
        [Show]
        public void Clear() {
            nodeMap.Clear();
            platforms.Clear();
        }

        [Show]
        public void GenerateNodes() {
            Clear();
            for (var x = MinX * NodeSizeX; x < MaxX * NodeSizeX; x += NodeSizeX) {
                for (var y = MinY * NodeSizeY; y < MaxY * NodeSizeY; y += NodeSizeY) {
                    GetNode(new Vector2(x / NodeSizeX, y / NodeSizeY));
                }
            }
            Debug.Log(string.Format("Calculated a total of {0} nodes.", TotalNodes));
        }

        public int MinX {
            get { return mapMinPos.X; }
        }

        public int MinY {
            get { return mapMinPos.Y; }
        }

        public int MaxX {
            get { return mapMaxPos.X; }
        }

        public int MaxY {
            get { return mapMaxPos.Y; }
        }

        public Node GetNode(int x, int y) {
            return GetNode(new MapPosition(x, y));
        }

        private Node GetNode(MapPosition position) {
            if (nodeMap.ContainsKey(position)) {
                return nodeMap[position];
            }
            var node = GenerateNode(position);
            if (node == null) {
                nodeMap[position] = null;
                return null;
            }
            if (node.Walkable) {
                GetPlatform(node).AddNode(node);
            }
            nodeMap[position] = node;
            return node;
        }

        private Node GenerateNode(MapPosition mapPosition) {
            var type = CheckNodeType(mapPosition);
            if (type == Node.NodeType.Empty) {
                return null;
            }
            return new Node(mapPosition, type);
        }

        private Node.NodeType CheckNodeType(MapPosition mapPosition) {
            if (BoxCast(mapPosition)) {
                //Inside an object
                return Node.NodeType.Blocked;
            }
            var x = mapPosition.X;
            var y = mapPosition.Y;
            if (!BoxCast(x, y - 1)) {
                return Node.NodeType.Empty;
            }
            //Is on solid ground, check for edges
            var leftLowBoxCast = BoxCast(x - 1, y - 1);
            var rightLowBoxCast = BoxCast(x + 1, y - 1);
            var leftBoxCast = BoxCast(x - 1, y);
            var rightBoxCast = BoxCast(x + 1, y);
            if (leftLowBoxCast && rightLowBoxCast && !leftBoxCast && !rightBoxCast) {
                return Node.NodeType.Platform;
            }
            if (leftLowBoxCast || leftBoxCast) {
                return Node.NodeType.RightEdge;
            }
            if (rightLowBoxCast || rightBoxCast) {
                return Node.NodeType.LeftEdge;
            }
            return Node.NodeType.Solo;
        }

        private RaycastHit2D BoxCast(MapPosition mapPosition) {
            return BoxCast(mapPosition.X, mapPosition.Y);
        }

        private RaycastHit2D BoxCast(int x, int y) {
            return BoxCast(new Vector2(x, y));
        }

        private RaycastHit2D BoxCast(Vector2 pos) {
            return Physics2D.BoxCast(pos, NodeBoxCastSize, 0f, Vector2.zero, 1F, WorldMask);
        }

        private void OnDrawGizmosSelected() {
            DrawGizmos();
        }

        public void DrawGizmos() {
            if (DrawPoints) {
                Gizmos.color = MinColor;
                Gizmos.DrawCube((Vector2) mapMinPos, NodeSize);
                Gizmos.color = MaxColor;
                Gizmos.DrawCube((Vector2) mapMaxPos, NodeSize);
            }
            if (DrawBorder) {
                Gizmos.color = BorderLineColor;
                Gizmos.DrawWireCube(Center, Size);
            }
            if (DrawNodes) {
                foreach (var node in nodeMap.Values) {
                    if (node == null || !node.Walkable || node.Empty) {
                        continue;
                    }
                    Gizmos.color = GetColor(node);
                    var pos = node.Position;
                    Gizmos.DrawCube(new Vector2(pos.X * NodeSizeX, pos.Y * NodeSizeY), NodeSize);
                }
            }
            if (DrawPlatforms) {
                foreach (var platform in platforms) {
                    platform.DrawGizmos();
                }
            }
        }

        private Color GetColor(Node node) {
            switch (node.Type) {
                case Node.NodeType.LeftEdge:
                case Node.NodeType.RightEdge: return EdgeNodeColor;
                case Node.NodeType.Solo: return SoloNodeColor;
                case Node.NodeType.Platform: return PlatformNodeColor;
            }
            throw new ArgumentOutOfRangeException(node.Type.ToString());
        }

        public Node GetNode(Vector2 position) {
            return GetNode((int) (position.x * NodeSizeX), (int) (position.y * NodeSizeY));
        }

        public Node GetNode(Node node, Direction direction) {
            return GetNode(node.X + direction.X, node.Y + direction.Y);
        }

        public bool IsOutOfBounds(MapPosition position) {
            return !position.IsWithin(mapMaxPos, mapMinPos);
        }

        public bool IsOutOfBounds(Vector2 position) {
            return !position.IsWithin(mapMaxPos, mapMinPos);
        }

        public bool IsOutOfBounds(float x, float y) {
            return x >= MaxX || x <= MinX || y <= MinY || y >= MaxY;
        }

        public List<Node> WalkableNodes {
            get { return nodeMap.Values.Where((node, i) => node != null && node.Walkable).ToList(); }
        }

        public IEnumerator<Node> GetEnumerator() {
            return WalkableNodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}