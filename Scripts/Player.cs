using Godot;
using System;

public partial class Player : CharacterBody3D
{
    public int Health { get; private set; } = 100;
    public int MaxHealth => 100;

    private float cooldown;
    private Node3D? characterModel;
    private Node3D? swordPivot;
    private AnimationPlayer? animator;
    private string? idleAnimation;
    private string? moveAnimation;
    private Vector3 modelBasePosition;
    private Tween? swordTween;
    private float animationClock;
    private bool moving;

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = .42f, Height = 1.8f },
            Position = new Vector3(0, .9f, 0)
        });

        var characterScene = GD.Load<PackedScene>("res://Survival_Character.fbx");
        if (characterScene != null)
        {
            var instance = characterScene.Instantiate();
            if (instance is Node3D model)
            {
                characterModel = model;
                AddChild(model);
                modelBasePosition = model.Position;
                FindAnimationPlayer(model);
                ApplyWarriorMaterials(model);
                ConfigureAnimations();
            }
            else
            {
                instance.QueueFree();
                AddFallbackBody();
            }
        }
        else
        {
            GD.PushWarning("Survival_Character.fbx did not import. Using the fallback player mesh.");
            AddFallbackBody();
        }

        CreateVisibleStingSword();

    }

    private void AddFallbackBody()
    {
        var body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = .42f, Height = 1.8f },
            Position = new Vector3(0, .9f, 0),
            MaterialOverride = MakeMaterial(new Color(.055f, .12f, .16f), .72f, .34f)
        };
        AddChild(body);
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .31f, Height = .62f },
            Position = new Vector3(0, 1.95f, 0),
            MaterialOverride = MakeMaterial(new Color(.72f, .53f, .38f), 0, .72f)
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(.78f, .12f, .5f) },
            Position = new Vector3(0, 1.25f, 0),
            MaterialOverride = MakeMaterial(new Color(.12f, .42f, .48f), .45f, .3f)
        });
    }

    private static StandardMaterial3D MakeMaterial(Color color, float metallic, float roughness, bool glow = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
        if (glow)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 1.7f;
        }
        return material;
    }

    private void ApplyWarriorMaterials(Node node)
    {
        var armor = MakeMaterial(new Color(.055f, .085f, .12f), .78f, .3f);
        var cloth = MakeMaterial(new Color(.025f, .20f, .22f), .18f, .72f);
        var gold = MakeMaterial(new Color(.78f, .48f, .13f), .82f, .24f);
        var skin = MakeMaterial(new Color(.67f, .43f, .29f), 0, .76f);
        var hair = MakeMaterial(new Color(.025f, .022f, .045f), .12f, .52f);
        ApplyMaterialRecursive(node, armor, cloth, gold, skin, hair);
    }

    private void ApplyMaterialRecursive(Node node, StandardMaterial3D armor, StandardMaterial3D cloth,
        StandardMaterial3D gold, StandardMaterial3D skin, StandardMaterial3D hair)
    {
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
        {
            string n = meshInstance.Name.ToString().ToLowerInvariant();
            StandardMaterial3D selected = armor;
            if (n.Contains("gold") || n.Contains("trim") || n.Contains("belt") || n.Contains("ornament"))
                selected = gold;
            else if (n.Contains("skin") || n.Contains("face") || n.Contains("head"))
                selected = skin;
            else if (n.Contains("hair") || n.Contains("beard"))
                selected = hair;
            else if (n.Contains("cloth") || n.Contains("shirt") || n.Contains("pant") || n.Contains("cape") || n.Contains("fabric"))
                selected = cloth;

            int surfaces = meshInstance.Mesh.GetSurfaceCount();
            if (surfaces <= 0)
                meshInstance.MaterialOverride = selected;
            else
                for (int i = 0; i < surfaces; i++)
                    meshInstance.SetSurfaceOverrideMaterial(i, selected);
        }

        foreach (Node child in node.GetChildren())
            ApplyMaterialRecursive(child, armor, cloth, gold, skin, hair);
    }

    private void FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer found)
            animator = found;
        foreach (Node child in node.GetChildren())
            FindAnimationPlayer(child);
    }

    private void ConfigureAnimations()
    {
        if (animator == null)
            return;

        foreach (StringName animationName in animator.GetAnimationList())
        {
            string name = animationName.ToString().ToLowerInvariant();
            if (idleAnimation == null && (name.Contains("idle") || name.Contains("stand")))
                idleAnimation = animationName.ToString();
            if (moveAnimation == null && (name.Contains("walk") || name.Contains("run") || name.Contains("move")))
                moveAnimation = animationName.ToString();
        }

        var animations = animator.GetAnimationList();
        if (idleAnimation == null && animations.Length > 0)
            idleAnimation = animations[0].ToString();
        if (idleAnimation != null)
            animator.Play(idleAnimation);
    }

    private void CreateVisibleStingSword()
    {
        // A hand-bone-independent weapon rig: this stays visible even when the imported FBX rig
        // uses an unexpected bone name or the sword FBX has an unusable origin/material.
        swordPivot = new Node3D
        {
            Name = "StingSwordPivot",
            Position = new Vector3(.48f, 1.02f, -.12f),
            RotationDegrees = new Vector3(0, 0, -24)
        };
        AddChild(swordPivot);

        var bladeMaterial = MakeMaterial(new Color(.10f, .72f, 1f), .82f, .18f, true);
        var edgeMaterial = MakeMaterial(new Color(.72f, .94f, 1f), .65f, .16f, true);
        var goldMaterial = MakeMaterial(new Color(.92f, .58f, .16f), .78f, .23f);
        var gripMaterial = MakeMaterial(new Color(.075f, .035f, .025f), .12f, .78f);

        swordPivot.AddChild(new MeshInstance3D
        {
            Name = "StingBlade",
            Mesh = new BoxMesh { Size = new Vector3(.115f, .88f, .045f) },
            Position = new Vector3(0, .59f, 0),
            MaterialOverride = bladeMaterial
        });
        swordPivot.AddChild(new MeshInstance3D
        {
            Name = "BrightBladeEdge",
            Mesh = new BoxMesh { Size = new Vector3(.025f, .83f, .052f) },
            Position = new Vector3(.061f, .59f, -.002f),
            MaterialOverride = edgeMaterial
        });
        swordPivot.AddChild(new MeshInstance3D
        {
            Name = "SwordCrossguard",
            Mesh = new BoxMesh { Size = new Vector3(.42f, .075f, .09f) },
            Position = new Vector3(0, .13f, 0),
            MaterialOverride = goldMaterial
        });
        swordPivot.AddChild(new MeshInstance3D
        {
            Name = "SwordGrip",
            Mesh = new CylinderMesh { TopRadius = .045f, BottomRadius = .055f, Height = .32f },
            Position = new Vector3(0, -.055f, 0),
            MaterialOverride = gripMaterial
        });
        swordPivot.AddChild(new MeshInstance3D
        {
            Name = "SwordPommel",
            Mesh = new SphereMesh { Radius = .075f, Height = .15f },
            Position = new Vector3(0, -.235f, 0),
            MaterialOverride = goldMaterial
        });
        swordPivot.AddChild(new OmniLight3D
        {
            Name = "SwordGlow",
            Position = new Vector3(0, .62f, .15f),
            LightColor = new Color(.12f, .62f, 1f),
            LightEnergy = .45f,
            OmniRange = 2.2f
        });
    }

    public override void _Process(double delta)
    {
        animationClock += (float)delta;
        if (characterModel != null && GodotObject.IsInstanceValid(characterModel))
        {
            float bob = moving ? Mathf.Abs(Mathf.Sin(animationClock * 11f)) * .045f : Mathf.Sin(animationClock * 1.8f) * .012f;
            characterModel.Position = modelBasePosition + new Vector3(0, bob, 0);
            if (animator == null || moveAnimation == null)
                characterModel.Rotation = new Vector3(0, 0, moving ? Mathf.Sin(animationClock * 11f) * .025f : Mathf.Sin(animationClock * 1.8f) * .012f);
        }

        if (swordPivot != null && GodotObject.IsInstanceValid(swordPivot) && (swordTween == null || !swordTween.IsRunning()))
        {
            float sway = moving ? Mathf.Sin(animationClock * 7f) * 5f : Mathf.Sin(animationClock * 1.6f) * 2.5f;
            swordPivot.RotationDegrees = new Vector3(0, 0, -24f + sway);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        cooldown = Mathf.Max(0, cooldown - dt);

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector2 joystick = GetParent<MainWorld>().GetJoystickDirection();
        if (joystick.Length() > .12f)
            input = joystick;

        // Move relative to the orbit camera: W always moves toward the camera's view direction.
        float cameraYaw = GetParent<MainWorld>().GetCameraYaw();
        Vector3 direction = new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, cameraYaw).Normalized();
        moving = direction.LengthSquared() > .01f;
        Velocity = new Vector3(direction.X * 6.2f, Velocity.Y, direction.Z * 6.2f);

        if (moving)
        {
            LookAt(GlobalPosition + direction, Vector3.Up);
            PlayMovementAnimation(true);
        }
        else
        {
            PlayMovementAnimation(false);
        }

        if (!IsOnFloor())
            Velocity = new Vector3(Velocity.X, Velocity.Y - 20 * dt, Velocity.Z);
        else if (Velocity.Y < 0)
            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

        if (IsOnFloor() && Input.IsActionJustPressed("jump"))
            Velocity = new Vector3(Velocity.X, 7, Velocity.Z);

        MoveAndSlide();

        if (Input.IsActionJustPressed("attack"))
            Attack();
        if (Input.IsActionJustPressed("interact"))
            GetParent<MainWorld>().Interact();
    }

    private void PlayMovementAnimation(bool isMoving)
    {
        if (animator == null) return;
        string? target = isMoving ? (moveAnimation ?? idleAnimation) : idleAnimation;
        if (target != null && animator.CurrentAnimation.ToString() != target)
            animator.Play(target);
    }

    public void Attack()
    {
        if (cooldown > 0) return;
        cooldown = .48f;
        GetParent<MainWorld>().HitClosestEnemy(GlobalPosition, 3.25f, 35);

        if (swordPivot != null && GodotObject.IsInstanceValid(swordPivot))
        {
            if (swordTween != null && swordTween.IsRunning())
                swordTween.Kill();
            swordTween = CreateTween();
            swordTween.TweenProperty(swordPivot, "rotation_degrees", new Vector3(0, 0, 105), .11)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            swordTween.TweenProperty(swordPivot, "rotation_degrees", new Vector3(0, 0, -24), .19)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    public void Hurt(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
        if (Health == 0)
        {
            Health = 100;
            GlobalPosition = new Vector3(0, 1.1f, 0);
        }
    }

    public void Heal(int amount) => Health = Mathf.Min(MaxHealth, Health + amount);
    public void RestoreHealth(int amount) => Health = Mathf.Clamp(amount, 1, MaxHealth);
    public static StandardMaterial3D Mat(Color color) => new() { AlbedoColor = color, Roughness = .9f };
}