using Godot;

public partial class Enemy : CharacterBody3D
{
    public int Health { get; private set; } = 70;
    private Player? _target;
    private float _attackCooldown;

    public override void _Ready()
    {
        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.6f }, Position = new Vector3(0, 0.6f, 0) });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.62f, Height = 1.24f }, Position = new Vector3(0, 0.6f, 0), Scale = new Vector3(1.1f, 0.8f, 1.1f), MaterialOverride = Player.Mat(new Color(0.18f, 0.72f, 0.38f)) });
    }

    public void SetTarget(Player target) => _target = target;

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target)) return;
        float dt = (float)delta;
        _attackCooldown -= dt;
        Vector3 offset = _target.GlobalPosition - GlobalPosition;
        float distance = offset.Length();
        Vector3 dir = new Vector3(offset.X, 0, offset.Z).Normalized();
        if (distance > 1.5f && distance < 24f)
        {
            Velocity = new Vector3(dir.X * 2.2f, Velocity.Y, dir.Z * 2.2f);
            if (dir.LengthSquared() > 0.01f) LookAt(GlobalPosition + dir, Vector3.Up);
        }
        else Velocity = new Vector3(0, Velocity.Y, 0);
        if (!IsOnFloor()) Velocity = new Vector3(Velocity.X, Velocity.Y - 18f * dt, Velocity.Z);
        MoveAndSlide();
        if (distance < 1.8f && _attackCooldown <= 0)
        {
            _target.Hurt(7);
            _attackCooldown = 1.3f;
        }
    }

    public void TakeHit(int damage)
    {
        Health -= damage;
        if (Health <= 0) QueueFree();
    }
}
