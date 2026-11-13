using Godot;

public partial class Enemy : CharacterBody3D
{
    public int Health { get; private set; } = 70;
    private Player? target;
    private float cooldown;

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = .42f, Height = 2.0f },
            Position = new Vector3(0, 1.0f, 0)
        });

        const string modelPath = "res://demogorgon_npc.glb";
        PackedScene? npcScene = ResourceLoader.Exists(modelPath)
            ? ResourceLoader.Load<PackedScene>(modelPath) : null;

        if (npcScene != null)
        {
            var model = npcScene.Instantiate<Node3D>();
            model.Name = "DemogorgonModel";
            AddChild(model);
            NormalizeModel(model, 2.35f);
            FindAndPlayAnimation(model);
            GD.Print("Demogorgon NPC model loaded.");
        }
        else
        {
            GD.PushError("DEMOGORGON MODEL FAILED TO LOAD: " + modelPath);
            BuildDemogorgonFallback();
        }
    }

    private void NormalizeModel(Node3D model, float targetHeight)
    {
        if (!TryGetBounds(model, out Aabb bounds))
            return;

        float scale = targetHeight / Mathf.Max(bounds.Size.Y, 0.001f);
        model.Scale *= scale;
        if (TryGetBounds(model, out Aabb scaled))
        {
            Vector3 center = scaled.Position + scaled.Size * 0.5f;
            model.Position += new Vector3(-center.X, -scaled.Position.Y, -center.Z);
        }
    }

    private bool TryGetBounds(Node node, out Aabb bounds)
    {
        bool found = false;
        bounds = new Aabb();
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            Aabb item = mesh.GlobalTransform * mesh.GetAabb();
            bounds = found ? bounds.Merge(item) : item;
            found = true;
        }
        foreach (Node child in node.GetChildren())
        {
            if (TryGetBounds(child, out Aabb childBounds))
            {
                bounds = found ? bounds.Merge(childBounds) : childBounds;
                found = true;
            }
        }
        return found;
    }

    private void FindAndPlayAnimation(Node node)
    {
        if (node is AnimationPlayer animator)
        {
            var clips = animator.GetAnimationList();
            string? chosen = null;
            foreach (StringName clip in clips)
            {
                string name = clip.ToString().ToLowerInvariant();
                if (name.Contains("idle") || name.Contains("walk") || name.Contains("run"))
                {
                    chosen = clip.ToString();
                    break;
                }
            }
            if (chosen == null && clips.Length > 0)
                chosen = clips[0].ToString();
            if (chosen != null)
                animator.Play(chosen);
        }
        foreach (Node child in node.GetChildren())
            FindAndPlayAnimation(child);
    }

    private static StandardMaterial3D MakeMaterial(Color color, float metallic = 0f, float roughness = .9f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
    }

    // Distinct flower-headed monster fallback; never show the old green slime sphere.
    private void BuildDemogorgonFallback()
    {
        var skin = MakeMaterial(new Color(.20f, .30f, .22f), .05f, .82f);
        var dark = MakeMaterial(new Color(.055f, .075f, .06f), .02f, .9f);
        var body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = .34f, Height = 1.35f },
            Position = new Vector3(0, .88f, 0),
            MaterialOverride = skin
        };
        AddChild(body);
        foreach (float angle in new[] { 0f, 1.2566f, 2.5132f, 3.7699f, 5.0265f })
        {
            var petal = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = .13f, Height = .72f },
                Position = new Vector3(Mathf.Sin(angle) * .24f, 1.88f + Mathf.Cos(angle) * .08f, Mathf.Cos(angle) * .24f),
                Rotation = new Vector3(Mathf.Cos(angle) * .65f, angle, -Mathf.Sin(angle) * .65f),
                MaterialOverride = skin
            };
            AddChild(petal);
        }
        var openMouth = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .23f, Height = .42f },
            Position = new Vector3(0, 1.88f, -.08f),
            Scale = new Vector3(.72f, 1.0f, .45f),
            MaterialOverride = dark
        };
        AddChild(openMouth);
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .07f, Height = .14f },
            Position = new Vector3(-.10f, 1.88f, -.27f),
            MaterialOverride = MakeMaterial(new Color(.8f, .12f, .08f))
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .07f, Height = .14f },
            Position = new Vector3(.10f, 1.88f, -.27f),
            MaterialOverride = MakeMaterial(new Color(.8f, .12f, .08f))
        });
    }

    public void SetTarget(Player p) => target = p;

    public override void _PhysicsProcess(double delta)
    {
        if (target == null || !GodotObject.IsInstanceValid(target)) return;
        float dt = (float)delta;
        cooldown -= dt;
        Vector3 offset = target.GlobalPosition - GlobalPosition;
        float distance = offset.Length();
        Vector3 direction = new Vector3(offset.X, 0, offset.Z).Normalized();
        if (distance > 1.5f && distance < 25)
        {
            Velocity = new Vector3(direction.X * 2.25f, Velocity.Y, direction.Z * 2.25f);
            if (direction.LengthSquared() > .01f)
                LookAt(GlobalPosition + direction, Vector3.Up);
        }
        else Velocity = new Vector3(0, Velocity.Y, 0);

        if (!IsOnFloor()) Velocity = new Vector3(Velocity.X, Velocity.Y - 18 * dt, Velocity.Z);
        else if (Velocity.Y < 0) Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

        MoveAndSlide();
        if (distance < 1.75f && cooldown <= 0)
        {
            target.Hurt(8);
            cooldown = 1.35f;
        }
    }

    public void TakeHit(int damage)
    {
        Health -= damage;
        if (Health <= 0) QueueFree();
    }
}