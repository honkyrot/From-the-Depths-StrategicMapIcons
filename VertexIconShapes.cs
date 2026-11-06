using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategicMapIcons
{
    public enum CombatDomain
    {
        Ship,
        Air,
        Land,
        Orbital,
        Fortress
    }

    public readonly struct LineSegment2D
    {
        public readonly Vector2 A;
        public readonly Vector2 B;

        public LineSegment2D(float ax, float ay, float bx, float by)
        {
            A = new Vector2(ax, ay);
            B = new Vector2(bx, by);
        }

        public LineSegment2D(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }
    }

    public static class VertexIconShapes
    {
        private static readonly Dictionary<StrategicIconMode, Texture2D> _buttonTextures = new Dictionary<StrategicIconMode, Texture2D>();
        private static Texture2D? _toggleButtonTexture;

        // Pre-built vertex segment arrays in normalized [-1, 1] coordinates (+Y is forward/up)
        public static readonly LineSegment2D[] ToggleMenuButtonSegments = BuildToggleMenuButtonShape();
        public static readonly LineSegment2D[] DefaultEmblemButtonSegments = BuildDefaultEmblemShape();
        public static readonly LineSegment2D[] AutoButtonSegments = BuildAutoShape();
        public static readonly LineSegment2D[] CombatShipSegments = BuildCombatShipShape();
        public static readonly LineSegment2D[] CombatAirSegments = BuildCombatAirShape();
        public static readonly LineSegment2D[] CombatLandSegments = BuildCombatLandShape();
        public static readonly LineSegment2D[] CombatOrbitalSegments = BuildCombatOrbitalShape();
        public static readonly LineSegment2D[] CombatFortressSegments = BuildCombatFortressShape();
        public static readonly LineSegment2D[] HarvesterSegments = BuildHarvesterShape();
        public static readonly LineSegment2D[] CargoSegments = BuildCargoShape();
        public static readonly LineSegment2D[] ScoutSegments = BuildScoutShape();
        public static readonly LineSegment2D[] SupportSegments = BuildSupportShape();

        // Simple geometric shape segment arrays
        public static readonly LineSegment2D[] ShapeSquareSegments = BuildShapeSquare();
        public static readonly LineSegment2D[] ShapeCircleSegments = BuildShapeCircle();
        public static readonly LineSegment2D[] ShapeTriangleSegments = BuildShapeTriangle();
        public static readonly LineSegment2D[] ShapePentagonSegments = BuildShapePentagon();
        public static readonly LineSegment2D[] ShapeHexagonSegments = BuildShapeHexagon();
        public static readonly LineSegment2D[] ShapeOctagonSegments = BuildShapeOctagon();
        public static readonly LineSegment2D[] ShapeStarSegments = BuildShapeStar();
        public static readonly LineSegment2D[] ShapeCrossSegments = BuildShapeCross();
        public static readonly LineSegment2D[] ShapeDiamondSegments = BuildShapeDiamond();
        public static readonly LineSegment2D[] ShapeChevronSegments = BuildShapeChevron();
        // Custom shapes
        public static readonly LineSegment2D[] MissileSegments = BuildMissileShape();
        public static CombatDomain GetCombatDomain(Force? f)
        {
            if (f == null)
            {
                return CombatDomain.Ship;
            }

            var type = f.GetConstructableType();
            if (type == enumConstructableTypes.fortress || type == enumConstructableTypes.structure)
            {
                return CombatDomain.Fortress;
            }

            var csi = f.CSI;
            if (csi != null && csi.TopSpeed <= SMI_ModSettings.FortressMaxTopSpeed && f.TravelRestrictions == ForceTravelRestrictions.Water)
            {
                return CombatDomain.Fortress;
            }

            if (f.TravelRestrictions == ForceTravelRestrictions.Air)
            {
                if (csi != null && csi.MaxAltitude > SMI_ModSettings.OrbitalMinAltitude)
                {
                    return CombatDomain.Orbital;
                }
                return CombatDomain.Air;
            }

            if (f.TravelRestrictions == ForceTravelRestrictions.Land)
            {
                return CombatDomain.Land;
            }

            return CombatDomain.Ship;
        }

        public static LineSegment2D[] GetSegmentsForMap(StrategicIconMode resolvedMode, Force? representativeForce, out bool rotateWithHeading)
        {
            rotateWithHeading = true;
            switch (resolvedMode)
            {
                // Standard
                case StrategicIconMode.Harvester:
                    rotateWithHeading = false;
                    return HarvesterSegments;
                case StrategicIconMode.Cargo:
                    rotateWithHeading = true;
                    return CargoSegments;
                case StrategicIconMode.Scout:
                    rotateWithHeading = true;
                    return ScoutSegments;
                case StrategicIconMode.Support:
                    rotateWithHeading = false;
                    return SupportSegments;
                case StrategicIconMode.Fortress:
                    rotateWithHeading = false;
                    return CombatFortressSegments;
                case StrategicIconMode.Air_Combat:
                    rotateWithHeading = true;
                    return CombatAirSegments;
                case StrategicIconMode.Land_Combat:
                    rotateWithHeading = true;
                    return CombatLandSegments;
                case StrategicIconMode.Orbital_Combat:
                    rotateWithHeading = false;
                    return CombatOrbitalSegments;
                case StrategicIconMode.Water_Combat:
                    rotateWithHeading = true;
                    return CombatShipSegments;
                // Geometric Shapes
                case StrategicIconMode.Shapes_Square:
                    rotateWithHeading = false;
                    return ShapeSquareSegments;
                case StrategicIconMode.Shapes_Circle:
                    rotateWithHeading = false;
                    return ShapeCircleSegments;
                case StrategicIconMode.Shapes_Triangle:
                    rotateWithHeading = true;
                    return ShapeTriangleSegments;
                case StrategicIconMode.Shapes_Pentagon:
                    rotateWithHeading = false;
                    return ShapePentagonSegments;
                case StrategicIconMode.Shapes_Hexagon:
                    rotateWithHeading = false;
                    return ShapeHexagonSegments;
                case StrategicIconMode.Shapes_Octagon:
                    rotateWithHeading = false;
                    return ShapeOctagonSegments;
                case StrategicIconMode.Shapes_Star:
                    rotateWithHeading = false;
                    return ShapeStarSegments;
                case StrategicIconMode.Shapes_Cross:
                    rotateWithHeading = false;
                    return ShapeCrossSegments;
                case StrategicIconMode.Shapes_Diamond:
                    rotateWithHeading = false;
                    return ShapeDiamondSegments;
                case StrategicIconMode.Shapes_Chevron:
                    rotateWithHeading = true;
                    return ShapeChevronSegments;
                // Custom
                case StrategicIconMode.Missile:
                    rotateWithHeading = true;
                    return BuildMissileShape();
                // Default/Auto
                default:
                    CombatDomain domain = GetCombatDomain(representativeForce);
                    switch (domain)
                    {
                        case CombatDomain.Orbital:
                            rotateWithHeading = false;
                            return CombatOrbitalSegments;
                        case CombatDomain.Air:
                            rotateWithHeading = true;
                            return CombatAirSegments;
                        case CombatDomain.Land:
                            rotateWithHeading = true;
                            return CombatLandSegments;
                        case CombatDomain.Fortress:
                            rotateWithHeading = false;
                            return CombatFortressSegments;
                        case CombatDomain.Ship:
                        default:
                            rotateWithHeading = true;
                            return CombatShipSegments;
                    }
            }
        }

        public static LineSegment2D[] GetSegmentsForButton(StrategicIconMode mode)
        {
            return mode switch
            {
                StrategicIconMode.DefaultEmblem => DefaultEmblemButtonSegments,
                StrategicIconMode.Auto => AutoButtonSegments,
                StrategicIconMode.Water_Combat => CombatShipSegments,
                StrategicIconMode.Air_Combat => CombatAirSegments,
                StrategicIconMode.Land_Combat => CombatLandSegments,
                StrategicIconMode.Orbital_Combat => CombatOrbitalSegments,
                StrategicIconMode.Fortress => CombatFortressSegments,
                StrategicIconMode.Harvester => HarvesterSegments,
                StrategicIconMode.Cargo => CargoSegments,
                StrategicIconMode.Scout => ScoutSegments,
                StrategicIconMode.Support => SupportSegments,
                StrategicIconMode.Shapes_Square => ShapeSquareSegments,
                StrategicIconMode.Shapes_Circle => ShapeCircleSegments,
                StrategicIconMode.Shapes_Triangle => ShapeTriangleSegments,
                StrategicIconMode.Shapes_Pentagon => ShapePentagonSegments,
                StrategicIconMode.Shapes_Hexagon => ShapeHexagonSegments,
                StrategicIconMode.Shapes_Octagon => ShapeOctagonSegments,
                StrategicIconMode.Shapes_Star => ShapeStarSegments,
                StrategicIconMode.Shapes_Cross => ShapeCrossSegments,
                StrategicIconMode.Shapes_Diamond => ShapeDiamondSegments,
                StrategicIconMode.Shapes_Chevron => ShapeChevronSegments,
                _ => AutoButtonSegments
            };
        }

        public static Texture2D GetToggleButtonTexture()
        {
            if (_toggleButtonTexture != null)
            {
                return _toggleButtonTexture;
            }

            _toggleButtonTexture = RasterizeSegmentsToTexture(ToggleMenuButtonSegments, 64, 2.3f);
            return _toggleButtonTexture;
        }

        public static Texture2D GetButtonTexture(StrategicIconMode mode)
        {
            if (_buttonTextures.TryGetValue(mode, out Texture2D existing) && existing != null)
            {
                return existing;
            }

            LineSegment2D[] segments = GetSegmentsForButton(mode);
            Texture2D tex = RasterizeSegmentsToTexture(segments, 64, 2.3f);
            _buttonTextures[mode] = tex;
            return tex;
        }

        private static Texture2D RasterizeSegmentsToTexture(LineSegment2D[] segments, int size, float lineThickness)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, 0f);
            }

            float margin = size * 0.14f;
            float halfSpan = (size - margin * 2f) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int s = 0; s < segments.Length; s++)
            {
                Vector2 p0 = center + segments[s].A * halfSpan;
                Vector2 p1 = center + segments[s].B * halfSpan;
                DrawAntialiasedLine(pixels, size, p0, p1, lineThickness);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void DrawAntialiasedLine(Color[] pixels, int size, Vector2 p0, Vector2 p1, float radius)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x) - radius - 2f), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x) + radius + 2f), 0, size - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y) - radius - 2f), 0, size - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y) + radius + 2f), 0, size - 1);

            Vector2 ab = p1 - p0;
            float abLenSq = ab.sqrMagnitude;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                    float dist;
                    if (abLenSq < 0.0001f)
                    {
                        dist = Vector2.Distance(pt, p0);
                    }
                    else
                    {
                        float t = Mathf.Clamp01(Vector2.Dot(pt - p0, ab) / abLenSq);
                        Vector2 proj = p0 + ab * t;
                        dist = Vector2.Distance(pt, proj);
                    }

                    if (dist <= radius + 1.0f)
                    {
                        float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                        int idx = y * size + x;
                        if (alpha > pixels[idx].a)
                        {
                            pixels[idx] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                }
            }
        }

        private static void AddPolygon(List<LineSegment2D> list, params Vector2[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                list.Add(new LineSegment2D(pts[i], pts[(i + 1) % pts.Length]));
            }
        }

        private static void AddPolyline(List<LineSegment2D> list, params Vector2[] pts)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                list.Add(new LineSegment2D(pts[i], pts[i + 1]));
            }
        }

        private static LineSegment2D[] BuildDefaultEmblemShape()
        {
            var list = new List<LineSegment2D>();
            // Pentagon Frame
            AddPolygon(list,
                new Vector2(0.00f, 0.90f),
                new Vector2(0.90f, 0.30f),
                new Vector2(0.60f, -0.80f),
                new Vector2(-0.60f, -0.80f),
                new Vector2(-0.90f, 0.30f));
            // Star Crest
            AddPolygon(list,
                new Vector2(0.00f, 0.50f),
                new Vector2(-0.12f, 0.13f),
                new Vector2(-0.50f, 0.10f),
                new Vector2(-0.20f, -0.10f),
                new Vector2(-0.30f, -0.50f),
                new Vector2(0.00f, -0.20f),
                new Vector2(0.30f, -0.50f),
                new Vector2(0.20f, -0.10f),
                new Vector2(0.50f, 0.10f),
                new Vector2(0.12f, 0.13f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildAutoShape()
        {
            var list = new List<LineSegment2D>();
            // 4 corner targeting brackets
            float c = 0.85f;
            float s = 0.42f;
            AddPolyline(list, new Vector2(-c, s), new Vector2(-c, c), new Vector2(-s, c));
            AddPolyline(list, new Vector2(s, c), new Vector2(c, c), new Vector2(c, s));
            AddPolyline(list, new Vector2(c, -s), new Vector2(c, -c), new Vector2(s, -c));
            AddPolyline(list, new Vector2(-s, -c), new Vector2(-c, -c), new Vector2(-c, -s));

            // Crisp 'A' in center
            AddPolyline(list, new Vector2(-0.38f, -0.46f), new Vector2(0f, 0.50f), new Vector2(0.38f, -0.46f));
            list.Add(new LineSegment2D(-0.23f, -0.08f, 0.23f, -0.08f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCombatShipShape()
        {
            var list = new List<LineSegment2D>();
            // water
            AddPolygon(list,
                new Vector2(0.00f, 0.90f),
                new Vector2(0.40f, 0.15f),
                new Vector2(0.40f, -0.80f),
                new Vector2(-0.40f, -0.80f),
                new Vector2(-0.40f, 0.15f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCombatAirShape()
        {
            var list = new List<LineSegment2D>();
            // Polygon 1
            AddPolygon(list,
                new Vector2(0.00f, 0.70f),
                new Vector2(-0.60f, -0.50f),
                new Vector2(0.00f, -0.30f),
                new Vector2(0.60f, -0.50f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCombatLandShape()
        {
            var list = new List<LineSegment2D>();
            // Polygon 1
            AddPolygon(list,
                new Vector2(-0.70f, 0.70f),
                new Vector2(-0.40f, 0.70f),
                new Vector2(-0.40f, -0.70f),
                new Vector2(-0.70f, -0.70f));
            // Polygon 2
            AddPolygon(list,
                new Vector2(0.40f, 0.70f),
                new Vector2(0.70f, 0.70f),
                new Vector2(0.70f, -0.70f),
                new Vector2(0.40f, -0.70f));
            // Polygon 3
            AddPolygon(list,
                new Vector2(-0.40f, 0.60f),
                new Vector2(0.40f, 0.60f),
                new Vector2(0.40f, -0.60f),
                new Vector2(-0.40f, -0.60f));
            // Segment 5
            list.Add(new LineSegment2D(0.00f, 1.00f, 0.00f, 0.00f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCombatOrbitalShape()
        {
            var list = new List<LineSegment2D>();
            // Polygon 1
            AddPolygon(list,
                new Vector2(0.00f, 0.40f),
                new Vector2(0.40f, 0.00f),
                new Vector2(0.00f, -0.40f),
                new Vector2(-0.40f, 0.00f));
            // Polygon 2
            AddPolygon(list,
                new Vector2(-0.90f, 0.40f),
                new Vector2(-0.50f, 0.40f),
                new Vector2(-0.50f, -0.40f),
                new Vector2(-0.90f, -0.40f));
            // Polygon 3
            AddPolygon(list,
                new Vector2(0.50f, 0.34f),
                new Vector2(0.90f, 0.34f),
                new Vector2(0.90f, -0.34f),
                new Vector2(0.50f, -0.34f));
            // Segment 4
            list.Add(new LineSegment2D(-0.50f, 0.00f, -0.40f, 0.00f));
            // Segment 5
            list.Add(new LineSegment2D(0.40f, 0.00f, 0.50f, 0.00f));
            // Segment 6
            list.Add(new LineSegment2D(0.00f, 0.40f, 0.00f, 0.90f));
            // Segment 7
            list.Add(new LineSegment2D(0.00f, -0.40f, 0.00f, -0.90f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCombatFortressShape()
        {
            var list = new List<LineSegment2D>();
            // Star Bastion
            AddPolygon(list,
                new Vector2(0.00f, 0.50f),
                new Vector2(0.90f, 0.90f),
                new Vector2(0.50f, 0.00f),
                new Vector2(0.90f, -0.90f),
                new Vector2(0.00f, -0.50f),
                new Vector2(-0.90f, -0.90f),
                new Vector2(-0.50f, 0.00f),
                new Vector2(-0.90f, 0.90f));
            // Inner Citadel
            AddPolygon(list,
                new Vector2(-0.30f, 0.30f),
                new Vector2(0.30f, 0.30f),
                new Vector2(0.30f, -0.30f),
                new Vector2(-0.30f, -0.30f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildHarvesterShape()
        {
            var list = new List<LineSegment2D>();
            // Outer Hexagon
            AddPolygon(list,
                new Vector2(0.75f, 0.45f),
                new Vector2(0.00f, 0.88f),
                new Vector2(-0.76f, 0.44f),
                new Vector2(-0.76f, -0.44f),
                new Vector2(0.00f, -0.88f),
                new Vector2(0.76f, -0.44f));
            // Polyline 2
            AddPolyline(list,
                new Vector2(-0.75f, -0.45f),
                new Vector2(0.00f, 0.85f),
                new Vector2(0.75f, -0.45f),
                new Vector2(-0.75f, -0.45f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildCargoShape()
        {
            var list = new List<LineSegment2D>();
            // Cargo Frame
            AddPolygon(list,
                new Vector2(0.00f, 0.60f),
                new Vector2(0.60f, 0.00f),
                new Vector2(0.00f, -0.60f),
                new Vector2(-0.60f, 0.00f));
            // Segment 2
            list.Add(new LineSegment2D(0.00f, 0.00f, 0.00f, 0.60f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildScoutShape()
        {
            var list = new List<LineSegment2D>();
            // Recon Dart
            AddPolygon(list,
                new Vector2(0.00f, 0.50f),
                new Vector2(0.40f, -0.50f),
                new Vector2(0.00f, -0.20f),
                new Vector2(-0.40f, -0.50f));
            // Right Sweep Ray
            AddPolyline(list,
                new Vector2(0.30f, 0.30f),
                new Vector2(0.90f, 0.90f));
            // Left Sweep Ray
            AddPolyline(list,
                new Vector2(-0.30f, 0.30f),
                new Vector2(-0.90f, 0.90f));
            // Forward Sweep Ray
            AddPolyline(list,
                new Vector2(0.00f, 0.60f),
                new Vector2(0.00f, 0.90f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildSupportShape()
        {
            var list = new List<LineSegment2D>();
            // Polygon 2
            AddPolygon(list,
                new Vector2(0.00f, 0.70f),
                new Vector2(0.20f, 0.20f),
                new Vector2(0.70f, 0.00f),
                new Vector2(0.20f, -0.20f),
                new Vector2(0.00f, -0.70f),
                new Vector2(-0.20f, -0.20f),
                new Vector2(-0.70f, 0.00f),
                new Vector2(-0.20f, 0.20f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildToggleMenuButtonShape()
        {
            var list = new List<LineSegment2D>();
            // Polygon 1
            AddPolygon(list,
                new Vector2(-1.00f, 0.50f),
                new Vector2(1.00f, 0.50f),
                new Vector2(1.00f, -0.50f),
                new Vector2(-1.00f, -0.50f));
            // Segment 2
            list.Add(new LineSegment2D(-1.00f, -0.50f, 1.00f, 0.50f));
            // Segment 3
            list.Add(new LineSegment2D(-1.00f, 0.50f, 1.00f, -0.50f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeSquare()
        {
            var list = new List<LineSegment2D>();
            AddPolygon(list,
                new Vector2(-0.70f, 0.70f),
                new Vector2(0.70f, 0.70f),
                new Vector2(0.70f, -0.70f),
                new Vector2(-0.70f, -0.70f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeCircle()
        {
            var list = new List<LineSegment2D>();
            AddArc(list, Vector2.zero, 0.78f, 0f, 360f, 20);
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeTriangle()
        {
            var list = new List<LineSegment2D>();
            AddPolygon(list,
                new Vector2(0.00f, 0.85f),
                new Vector2(0.75f, -0.70f),
                new Vector2(-0.75f, -0.70f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapePentagon()
        {
            var list = new List<LineSegment2D>();
            AddRegularPolygon(list, 5, 0.80f, 90f);
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeHexagon()
        {
            var list = new List<LineSegment2D>();
            AddRegularPolygon(list, 6, 0.80f, 90f);
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeOctagon()
        {
            var list = new List<LineSegment2D>();
            AddRegularPolygon(list, 8, 0.80f, 22f);
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeStar()
        {
            var list = new List<LineSegment2D>();
            const int points = 5;
            const float outerR = 0.88f;
            const float innerR = 0.36f;
            Vector2[] pts = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? outerR : innerR;
                float ang = (90f + i * (360f / (points * 2))) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }
            AddPolygon(list, pts);
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeCross()
        {
            var list = new List<LineSegment2D>();
            const float a = 0.26f;
            const float b = 0.82f;
            AddPolygon(list,
                new Vector2(-a, b),
                new Vector2(a, b),
                new Vector2(a, a),
                new Vector2(b, a),
                new Vector2(b, -a),
                new Vector2(a, -a),
                new Vector2(a, -b),
                new Vector2(-a, -b),
                new Vector2(-a, -a),
                new Vector2(-b, -a),
                new Vector2(-b, a),
                new Vector2(-a, a));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeDiamond()
        {
            var list = new List<LineSegment2D>();
            AddPolygon(list,
                new Vector2(0.00f, 0.75f),
                new Vector2(0.75f, 0.00f),
                new Vector2(0.00f, -0.75f),
                new Vector2(-0.75f, 0.00f));
            return list.ToArray();
        }

        private static LineSegment2D[] BuildShapeChevron()
        {
            var list = new List<LineSegment2D>();
            // Polygon 1
            AddPolygon(list,
                new Vector2(0.00f, 0.80f),
                new Vector2(0.80f, 0.00f),
                new Vector2(0.40f, -0.40f),
                new Vector2(0.00f, 0.00f),
                new Vector2(-0.40f, -0.40f),
                new Vector2(-0.80f, 0.00f));
            return list.ToArray();
        }

        // other shapes.

        private static LineSegment2D[] BuildMissileShape()
        {
            var list = new List<LineSegment2D>();
            // Warship Hull
            AddPolygon(list,
                new Vector2(0.00f, 1.00f),
                new Vector2(0.20f, 0.20f),
                new Vector2(-0.20f, 0.20f));
            // Polygon 4
            AddPolygon(list,
                new Vector2(-0.20f, 0.20f),
                new Vector2(-0.20f, -1.00f),
                new Vector2(0.20f, -1.00f),
                new Vector2(0.20f, 0.20f));
            return list.ToArray();
        }

        //

        private static void AddRegularPolygon(List<LineSegment2D> list, int sides, float radius, float startAngleDeg)
        {
            Vector2[] pts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float ang = (startAngleDeg + i * (360f / sides)) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
            }
            AddPolygon(list, pts);
        }

        private static void AddArc(List<LineSegment2D> list, Vector2 center, float radius, float startDeg, float endDeg, int steps)
        {
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                Vector2 cur = center + new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                if (i > 0)
                {
                    list.Add(new LineSegment2D(prev, cur));
                }
                prev = cur;
            }
        }
    }
}

