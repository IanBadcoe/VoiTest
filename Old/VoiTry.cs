// experiment in finding Voronoi polygons by examination of the mid-way orthogonals (BoundLines)
// of each edge (between to points)
//
// doing this to try and see if there is any way to examine the points and know which other points are their
// "neighbours" (in the sense of which ones contribute edges to the Voronoi region) which is easier than
// building the whole tessalation, but I am not sure there is
using Godot;
using System.Collections.Generic;

[Tool]
public partial class VoiTry : Node2D
{
    class BoundLine
    {
        public Edge Edge;

        public float StartFrac = 0;
        public float EndFrac = 1;
        public Vector2 Normal;
        public Vector2 Direction => Edge.Direction;
        public Vector2 Vector => Edge.Vector;

        public BoundLine()
        {
        }
    }

    struct Pt
    {
        public Vector2 Pos;

        public List<BoundLine> Bounds = [];

        public Pt()
        {
        }
    }

    List<Pt> Pts = [];

    struct Edge
    {
        public Vector2 Start;
        public Vector2 End;

        public Pt[] Pts = new Pt[2];

        public Vector2 Direction => Vector.Normalized();

        public Vector2 Vector => (End - Start);

        public Edge()
        {
        }
    }

    List<Edge> Edges = [];

    List<Vector2> Intersects = [];

    RandomNumberGenerator RNG = new();

    readonly Color Pt2Pt = (Colors.Red + Colors.White) / 2;
    readonly Color LongRay = (Colors.Blue + Colors.White) / 2;
    readonly Color VoiCol = Colors.Green / 2;

    const float Border = 250;

    public override void _Ready()
    {
        for(int i = 0; i < 6; i++)
        {
            Pts.Add(new Pt{Pos = new Vector2(RNG.RandfRange(Border, 500 + Border), RNG.RandfRange(Border, 500 + Border))});
        }

        for(int i = 0; i < Pts.Count - 1; i++)
        {
            Pt pt_i = Pts[i];

            for(int j = i + 1; j < Pts.Count; j++)
            {
                Pt pt_j = Pts[j];

                Vector2 d = pt_j.Pos - pt_i.Pos;
                Vector2 pos = pt_i.Pos + d * 0.5f;

                d = d.Normalized();

                Vector2 perp = new Vector2(d.Y, -d.X).Normalized();

                Edge edge = new Edge {
                    Start = pos + perp * 1000,
                    End = pos - perp * 1000,
                };

                edge.Pts[0] = pt_i;
                edge.Pts[1] = pt_j;

                Edges.Add(edge);

                pt_i.Bounds.Add(new BoundLine {Edge = edge, Normal = d});
                pt_j.Bounds.Add(new BoundLine {Edge = edge, Normal = -d});
            }
        }

        foreach(Pt pt in Pts)
        {
            for(int i = 0; i < pt.Bounds.Count; i++)
            {
                BoundLine bl = pt.Bounds[i];

                for(int j = 0; j < pt.Bounds.Count; j++)
                {
                    if (j != i)
                    {
                        BoundLine bl_other = pt.Bounds[j];

                        (float, float)? intersect = LineIntersect(bl.Edge.Start, bl.Edge.End, bl_other.Edge.Start, bl_other.Edge.End);

                        if (intersect.HasValue)
                        {
                            Intersects.Add(bl.Edge.Start + bl.Edge.Vector * intersect.Value.Item1);

                            // the normal points *away* from the point
                            // we want to cull away the part of bl that is further away...
                            if (bl.Direction.Dot(bl_other.Normal) > 0)
                            {
                                // so if bl direction is the same way as the other line's normal, we cull away the end
                                bl.EndFrac = Mathf.Min(bl.EndFrac, intersect.Value.Item1);
                            }
                            else
                            {
                                // otherwise the beginning
                                bl.StartFrac = Mathf.Max(bl.StartFrac, intersect.Value.Item1);
                            }
                        }
                    }
                }
            }
        }

        QueueRedraw();
    }

    private (float, float)? LineIntersect(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2)
    {
        //function doLineSegmentsIntersect(p, p2, q, q2) {
        // var r = subtractPoints(p2, p);
        // var s = subtractPoints(q2, q);

        var d1 = end1 - start1;
        var d2 = end2 - start2;

        // let's try to avoid any problems with silly huge numbers
        while(d1.LengthSquared() > 100 || d2.LengthSquared() > 100)
        {
            start1 /= 2;
            end1 /= 2;
            start2 /= 2;
            end2 /= 2;

            d1 = end1 - start1;
            d2 = end2 - start2;
        }

        // var uNumerator = crossProduct(subtractPoints(q, p), r);
        // var denominator = crossProduct(r, s);
        var starts_diff = start2 - start1;
        var numerator = starts_diff.Cross(d1);
        var denominator = d1.Cross(d2);

        // colinear
        // if (numerator == 0 && denominator == 0) {
        //     return null;
        // }

        // parallel
        if (denominator == 0) {
            return null;
        }

        // var u = uNumerator / denominator;
        // var t = crossProduct(subtractPoints(q, p), s) / denominator;
        var f1 = numerator / denominator;
        var f2 = starts_diff.Cross(d2) / denominator;

        if (f1 < 0 || f1 > 1 || f2 < 0 || f2 > 1)
        {
            return null;
        }

        return (f2, f1);
    }

    public override void _Process(double delta)
    {
    }

    public override void _Draw()
    {
        for(int i = 0; i < Pts.Count - 1; i++)
        {
            for(int j = i + 1; j < Pts.Count; j++)
            {
                DrawLine(Pts[i].Pos, Pts[j].Pos, Pt2Pt, 1);
            }
        }

        // foreach(Edge edge in Edges)
        // {
        //     DrawLine(edge.Start, edge.End, LongRay, 1);
        // }

        for(int i = 0; i < Pts.Count - 1; i++)
        {
            for(int j = i + 1; j < Pts.Count; j++)
            {
                Vector2 d = Pts[j].Pos - Pts[i].Pos;
                Vector2 pos = Pts[i].Pos + d * 0.5f;

                Vector2 perp = new Vector2(d.Y, -d.X).Normalized();

                DrawLine(pos + Vector2.Up * 10, pos - Vector2.Up * 10, Colors.Black, 2);
                DrawLine(pos + Vector2.Right * 10, pos - Vector2.Right * 10, Colors.Black, 2);
            }
        }

        foreach(var pt in Pts)
        {
            foreach(var bl in pt.Bounds)
            {
                if (bl.StartFrac < bl.EndFrac)
                {
                    DrawLine(
                        bl.Edge.Start + bl.Edge.Vector * bl.StartFrac,
                        bl.Edge.Start + bl.Edge.Vector * bl.EndFrac,
                        VoiCol,
                        2);
                }
            }
        }

        // Vector2 offset1 = new Vector2(5, 5);
        // Vector2 offset2 = new Vector2(-5, 5);

        // foreach(Vector2 i in Intersects)
        // {
        //     DrawLine(i + offset1, i - offset1, VoiCol, 2);
        //     DrawLine(i + offset2, i - offset2, VoiCol, 2);
        // }
    }
}
