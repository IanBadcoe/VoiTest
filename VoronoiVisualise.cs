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
public partial class VoronoiVisualise : Node3D
{
    // --------------------------------------------------------------
    // IAutoNode boilerplate
    public override void _Notification(int what) => this.Notify(what);
    // --------------------------------------------------------------

    bool Dirty = true;

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

    public override void _Ready()
    {
        Red = GD.Load<BaseMaterial3D>("res://Materials/BrightRed.tres");
        Green = GD.Load<BaseMaterial3D>("res://Materials/BrightGreen.tres");
    }

    public override void _Process(double delta)
    {
        if (Dirty)
        {
            Dirty = false;

            Construct();
        }

        // TurntableAngle += (float)delta * CameraOrbitSpeed;


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

    private Material RandMaterial(ClRand rand)
    {
        BaseMaterial3D material = (BaseMaterial3D)Green.Duplicate();
        material.AlbedoColor = Color.FromHsv(rand.Float(), 0.5f, 0.75f);
//        material.Emission = material.AlbedoColor;

        Util.Assert(material != null);

        return material;
    }

    void Construct()
    {
        const int size = 10;
        ClRand rand = new(1);

        IProgressiveVoronoi PV = VoronoiUtil.CreateProgressiveVoronoi(size, 0.0001f, 0.2f, rand);

        List<ImVec3> vecs = [];

        ImVec3Int min = new(1, 1, 1);
        // bounds contains accepts points on the very edge...
        ImVec3Int max = new(size - 2, size - 2, size - 2);
        ImBounds bounds = new(min.ToImVec3(), max.ToImVec3());

        for (int i = 0; i < 10; i++)
        {
            ImVec3Int where = new(size / 2, size / 2, size / 2);

            Material material = RandMaterial(rand);

            do
            {
                PV.AddPoint(where, IPolyhedron.MeshType.Faces, material);

                where += RandDirStep(rand);
            }
            while (bounds.Contains(where.ToImVec3()));
        }

        bounds = new();

        foreach (IProgressivePoint point in PV.AllPoints.Where(x => x.Solidity == IProgressiveVoronoi.Solidity.Solid))
        {
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

            bounds = bounds.UnionedWith(surf.GetBounds());

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
        }

        bounds.ExpandedBy(1);

        CameraDist = bounds.Size.Length() * 0.6f;
        ObjectCentre = bounds.Centre.ToVector3();

        DirectionalLight.LookAt(Vector3.Zero);
    }

    private ImVec3Int RandDirStep(ClRand rand)
    {
        int dir = rand.IntRange(0, 6);

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
