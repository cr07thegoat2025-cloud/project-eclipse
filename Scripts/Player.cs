using Godot;

public partial class Player : CharacterBody3D
{
    public int Health { get; private set; } = 100;
    public int MaxHealth => 100;

    private float cooldown;
    private Node3D? characterModel;
    private Node3D? swordVisual;
    private AnimationPlayer? animator;
    private string? idleAnimation;
    private string? moveAnimation;

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = .42f, Height = 1.8f },
            Position = new Vector3(0, .9f, 0)
        });

        // Load the character directly from the FBX uploaded to the repository.
        var characterScene = GD.Load<PackedScene>("res://Survival_Character.fbx");
        if (characterScene != null)
        {
            var instance = characterScene.Instantiate();
            if (instance is Node3D model)
            {
                characterModel = model;
                AddChild(model);
                FindAnimationPlayer(model);
                ConfigureAnimations();
                AttachSword(model);
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

        var cam = new Camera3D { Position = new Vector3(0, 5, 8), Current = true, Fov = 72 };
        AddChild(cam);
        cam.LookAt(new Vector3(0, 1, 0), Vector3.Up);
    }

    private void AddFallbackBody()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = .42f, Height = 1.8f },
            Position = new Vector3(0, .9f, 0),
            MaterialOverride = Mat(new Color(.16f, .29f, .78f))
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .31f, Height = .62f },
            Position = new Vector3(0, 1.95f, 0),
            MaterialOverride = Mat(new Color(.94f, .72f, .5f))
        });
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
            if (idleAnimation == null && name.Contains("idle"))
                idleAnimation = animationName;
            if (moveAnimation == null && (name.Contains("walk") || name.Contains("run") || name.Contains("move")))
                moveAnimation = animationName;
        }

        if (idleAnimation == null && animator.GetAnimationList().Length > 0)
            idleAnimation = animator.GetAnimationList()[0].ToString();

        if (idleAnimation != null)
            animator.Play(idleAnimation);
    }

    private void AttachSword(Node3D model)
    {
        var swordScene = GD.Load<PackedScene>("res://55-sting-sword-lowpoly.fbx");
        if (swordScene == null)
        {
            GD.PushWarning("Sting sword FBX could not be imported; character will load without the weapon.");
            return;
        }

        var swordInstance = swordScene.Instantiate();
        if (swordInstance is not Node3D sword)
        {
            swordInstance.QueueFree();
            return;
        }

        swordVisual = sword;
        Skeleton3D? skeleton = FindSkeleton(model);
        if (skeleton != null)
        {
            string[] rightHandNames = { "RightHand", "hand.R", "Hand_R", "mixamorig:RightHand", "mixamorig_RightHand", "右手" };
            int handBone = -1;
            for (int i = 0; i < skeleton.GetBoneCount(); i++)
            {
                string boneName = skeleton.GetBoneName(i).ToString();
                foreach (string candidate in rightHandNames)
                {
                    if (boneName.Equals(candidate, System.StringComparison.OrdinalIgnoreCase))
                    {
                        handBone = i;
                        break;
                    }
                }
                if (handBone >= 0) break;
            }

            if (handBone >= 0)
            {
                var attachment = new BoneAttachment3D
                {
                    BoneName = skeleton.GetBoneName(handBone)
                };
                skeleton.AddChild(attachment);
                attachment.AddChild(sword);
                sword.Position = new Vector3(0, 0, 0);
                sword.RotationDegrees = new Vector3(0, 0, 90);
                sword.Scale = Vector3.One;
                return;
            }
        }

        // Fallback placement keeps the weapon visible if the imported rig uses an unknown bone name.
        model.AddChild(sword);
        sword.Position = new Vector3(.38f, 1.05f, -.12f);
        sword.RotationDegrees = new Vector3(0, 0, 90);
    }

    private Skeleton3D? FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton)
            return skeleton;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSkeleton(child);
            if (found != null) return found;
        }
        return null;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        cooldown = Mathf.Max(0, cooldown - dt);

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector2 joystick = GetParent<MainWorld>().GetJoystickDirection();
        if (joystick.Length() > .12f)
            input = joystick;

        Vector3 direction = new Vector3(input.X, 0, input.Y).Normalized();
        Velocity = new Vector3(direction.X * 6.2f, Velocity.Y, direction.Z * 6.2f);

        if (direction.LengthSquared() > .01f)
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

    private void PlayMovementAnimation(bool moving)
    {
        if (animator == null) return;
        string? target = moving ? (moveAnimation ?? idleAnimation) : idleAnimation;
        if (target != null && animator.CurrentAnimation != target)
            animator.Play(target);
    }

    public void Attack()
    {
        if (cooldown > 0) return;
        cooldown = .48f;
        GetParent<MainWorld>().HitClosestEnemy(GlobalPosition, 3.25f, 35);

        if (swordVisual != null && GodotObject.IsInstanceValid(swordVisual))
        {
            var tween = CreateTween();
            tween.TweenProperty(swordVisual, "rotation_degrees",
                swordVisual.RotationDegrees + new Vector3(0, 0, -105), .12);
            tween.TweenProperty(swordVisual, "rotation_degrees",
                swordVisual.RotationDegrees, .18);
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
