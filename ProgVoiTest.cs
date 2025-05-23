using Godot;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Voi;
using Geom_Util.Immutable;
using Godot_Util;
using Geom_Util;
using SubD;
using System.Collections.Generic;
using Voi.Interfaces;
using Voi.Geom.Interfaces;
using System.Linq;

[Tool]
[Meta(typeof(IAutoConnect))]
public partial class ProgVoiTest : Node3D
{
    // --------------------------------------------------------------
    // IAutoNode boilerplate
    public override void _Notification(int what) => this.Notify(what);
    // --------------------------------------------------------------

    ulong NextAdd = 0;

    [Node]
    Camera3D Camera { get; set; }

    [Node]
    DirectionalLight3D DirectionalLight { get; set; }

    [Node]
    Node3D Turntable { get; set; }

    [Node]
    Node3D TurntablePivot { get; set; }

    BaseMaterial3D Red;
    BaseMaterial3D Green;

    float CameraDist = 10;
    float TurntableSpeed = 0.3f; // radians/s
    float CameraHeightRatio = 0.5f; // 50% as high, as we are far away from the target
    Vector3 ObjectCentre;
    float TurntableAngle = 0;

    ImBounds OverAllBounds = new();


    const int Size = 10;
    ClRand Rand = new(1);
    IProgressiveVoronoi PV = null;
    ImVec3Int Where = null;
    ulong WhereCode = 0;

    public override void _Ready()
    {
        Red = GD.Load<BaseMaterial3D>("res://Materials/BrightRed.tres");
        Green = GD.Load<BaseMaterial3D>("res://Materials/BrightGreen.tres");
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksMsec();

        if (NextAdd < now)
        {
            NextAdd = now + 10;

            AddOne();
        }

        if (!Engine.IsEditorHint())
        {
            Turntable.Position = -ObjectCentre;
            TurntablePivot.RotateY((float)delta * TurntableSpeed);
            //Turntable.Position += CameraLookAt;

            Camera.Position = new Vector3
            (
                0,
                CameraHeightRatio * CameraDist,
                CameraDist
            );

            Camera.LookAt(Vector3.Zero);
        }
    }

    private Material RandMaterial()
    {
        BaseMaterial3D material = (BaseMaterial3D)Green.Duplicate();
        material.AlbedoColor = Color.FromHsv(Rand.Float(), 0.5f, 0.75f);
//        material.Emission = material.AlbedoColor;

        Util.Assert(material != null);

        return material;
    }

    void AddOne()
    {
        const int size = 10;

        PV ??= VoronoiUtil.CreateProgressiveVoronoi(size, 0.0001f, 0.2f, Rand);

        List<ImVec3> vecs = [];

        ImVec3Int min = new(1, 1, 1);
        // bounds contains accepts points on the very edge...
        ImVec3Int max = new(size - 2, size - 2, size - 2);
        ImBounds bounds = new(min.ToImVec3(), max.ToImVec3());

        ulong here_code = WhereCode++;

        int x = (int)(here_code % (Size - 2) + 1);
        here_code /= (Size - 2);
        int y = (int)(here_code % (Size - 2) + 1);
        here_code /= (Size - 2);
        int z = (int)(here_code % (Size - 2) + 1);
        Where = new(x, y, z);

        if (!bounds.Contains(Where.ToImVec3()))
        {
            return;
        }

        Material material = RandMaterial();

        // if (PV.Point(Where).Solidity == IProgressiveVoronoi.Solidity.Solid)
        // {
        //     Where = new_where;

        //     return;
        // }

        IProgressivePoint point = PV.AddPoint(Where, IPolyhedron.MeshType.Faces, material);

        IPolyhedron polyhedron = point.Polyhedron;
        Surface surf = SubD2VoiUtil.PolyhedronToSurf(polyhedron);

        CatmullClarkSubdivider CCS = new();
        foreach (Edge edge in surf.Edges.Values)
        {
            edge.IsSetSharp = true;
        }
        surf = CCS.Subdivide(surf);
        // foreach (Edge edge in surf.Edges.Values)
        // {
        //     edge.IsSetSharp = false;
        // }
        surf = CCS.Subdivide(surf);
        Surface line_surf = surf;
        surf = CCS.Subdivide(surf);

        OverAllBounds = OverAllBounds.UnionedWith(surf.GetBounds());

        MeshInstance3D inst = new();
        Turntable.AddChild(inst);
        inst.Mesh = surf.ToMesh(Surface.MeshMode.Surface);
        inst.MaterialOverride = point.Material;

        inst = new();
        Turntable.AddChild(inst);
        inst.Mesh = line_surf.ToMesh(Surface.MeshMode.Edges);

        BaseMaterial3D material2 = (BaseMaterial3D)Red.Duplicate();
        material2.AlbedoColor = Color.FromHsv(((BaseMaterial3D)point.Material).AlbedoColor.H, 1.0f, 0.25f);
        material2.Emission = material2.AlbedoColor;

        inst.MaterialOverride = material2;

        CameraDist = OverAllBounds.Size.Length() * 0.6f;
        ObjectCentre = OverAllBounds.Centre.ToVector3();

        DirectionalLight.LookAt(Vector3.Zero);
    }

    private ImVec3Int RandDirStep()
    {
        int dir = Rand.IntRange(0, 6);

        switch (dir)
        {
            case 0:
                return new(1, 0, 0);
            case 1:
                return new(-1, 0, 0);
            case 2:
                return new(0, 1, 0);
            case 3:
                return new(0, -1, 0);
            case 4:
                return new(0, 0, 1);
            case 5:
                return new(0, 0, -1);
        }

        Util.Assert(false);

        return null;
    }
}
