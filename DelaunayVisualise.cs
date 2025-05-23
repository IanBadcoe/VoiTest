using Godot;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Voi;
using Geom_Util.Immutable;
using Godot_Util;
using Geom_Util;
using System;
using System.Diagnostics;
using SubD;
using System.Collections.Generic;

[Meta(typeof(IAutoConnect))]
public partial class DelaunayVisualise : Node3D
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

    BaseMaterial3D Red;
    BaseMaterial3D Green;

    float CameraDist = 10;
    float CameraOrbitSpeed = 0.3f; // radians/s
    float CameraHeightRatio = 0.5f; // 50% as high, as we are far away from the target
    Vector3 CameraLookAt;
    float CameraAngle = 0;

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

        CameraAngle += (float)delta * CameraOrbitSpeed;
        Camera.Position = CameraLookAt + new Vector3
        (
            Mathf.Sin(CameraAngle) * CameraDist,
            CameraHeightRatio * CameraDist,
            Mathf.Cos(CameraAngle) * CameraDist
        );

        Camera.LookAt(CameraLookAt);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouse_button)
        {
            if ((mouse_button.ButtonMask & MouseButtonMask.Right) != 0)
            {
                Debug.Print("xxx");
            }
        }
    }

    void Construct()
    {
        Delaunay D = new(0.0001f);

        List<ImVec3> vecs = [];

        ClRand rand = new(1);

        for (int i = 0; i < 10; i++)
        {
            vecs.Add(rand.ImVec3() * 10);
        }

        D.InitialiseWithVerts(vecs);

        ImBounds bounds = new();

        foreach (DTetrahedron tet in D.Tets)
        {
            Surface surf = SubD2VoiUtil.Tet2Surf(tet);
            CatmullClarkSubdivider CCS = new();
            foreach (Edge edge in surf.Edges.Values)
            {
                edge.IsSetSharp = true;
            }
            surf = CCS.Subdivide(surf);
            surf = CCS.Subdivide(surf);

            bounds = bounds.UnionedWith(surf.GetBounds());

            MeshInstance3D inst = new();
            AddChild(inst);
            inst.Mesh = surf.ToMesh(Surface.MeshMode.Surface);

            BaseMaterial3D material = (BaseMaterial3D)Green.Duplicate();
            material.AlbedoColor = Color.FromHsv(rand.Float(), 0.5f, 0.5f);
            material.Emission = material.AlbedoColor;

            inst.MaterialOverride = material;

            inst = new();
            AddChild(inst);
            inst.Mesh = surf.ToMesh(Surface.MeshMode.Edges);

            BaseMaterial3D material2 = (BaseMaterial3D)Red.Duplicate();
            material2.AlbedoColor = Color.FromHsv(material.AlbedoColor.H, 1.0f, 0.25f);
            material2.Emission = material2.AlbedoColor;

            inst.MaterialOverride = material2;
        }

        bounds.ExpandedBy(1);

        CameraDist = bounds.Size.Length();
        CameraLookAt = bounds.Centre.ToVector3();

        DirectionalLight.LookAt(bounds.Centre.ToVector3());
    }
}
