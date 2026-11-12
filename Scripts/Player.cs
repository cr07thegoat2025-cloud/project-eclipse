using Godot;

public partial class Player : CharacterBody3D
{
    public int Health { get; private set; } = 100;
    private float _cooldown;
    private Camera3D _camera = null!;

    public override void _Ready()
    {
        var collider = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.42f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) };
        AddChild(collider);
        var body = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.42f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0), MaterialOverride = Mat(new Color(0.16f, 0.3f, 0.8f)) };
        AddChild(body);
        var head = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.31f, Height = 0.62f }, Position = new Vector3(0, 1.95f, 0), MaterialOverride = Mat(new Color(0.94f, 0.72f, 0.5f)) };
        AddChild(head);
        _camera = new Camera3D { Position = new Vector3(0, 5.0f, 8.0f), Current = true, Fov = 72 };
        AddChild(_camera);
        _camera.LookAt(new Vector3(0, 1.0f, 0), Vector3.Up);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _cooldown = Mathf.Max(0, _cooldown - dt);
        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 dir = new Vector3(input.X, 0, input.Y).Normalized();
        Velocity = new Vector3(dir.X * 6.0f, Velocity.Y, dir.Z * 6.0f);
        if (dir.LengthSquared() > 0.01f) LookAt(GlobalPosition + dir, Vector3.Up);
        if (!IsOnFloor()) Velocity = new Vector3(Velocity.X, Velocity.Y - 20f * dt, Velocity.Z);
        else if (Velocity.Y < 0) Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
        MoveAndSlide();
        if (Input.IsActionJustPressed("attack")) Attack();
    }

    public void Attack()
    {
        if (_cooldown > 0) return;
        _cooldown = 0.5f;
        GetParent<MainWorld>().HitClosestEnemy(GlobalPosition, 3.2f, 35);
    }

    public void Hurt(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
        if (Health == 0) { Health = 100; GlobalPosition = new Vector3(0, 1, 0); }
    }

    public static StandardMaterial3D Mat(Color color) => new() { AlbedoColor = color, Roughness = 0.9f };
}
