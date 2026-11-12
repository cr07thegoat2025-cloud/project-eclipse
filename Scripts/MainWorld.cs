using Godot;
using System.Collections.Generic;

public partial class MainWorld : Node3D
{
    private Player _player = null!;
    private Label _hud = null!;
    private readonly List<Enemy> _enemies = new();
    private int _kills;

    public override void _Ready()
    {
        SetupInput();
        BuildWorld();
        _player = new Player { Position = new Vector3(0, 1, 0) };
        AddChild(_player);
        SpawnSlime(new Vector3(6, 0.7f, -6));
        SpawnSlime(new Vector3(-7, 0.7f, -10));
        SpawnSlime(new Vector3(12, 0.7f, 7));
        BuildHud();
    }

    private void SetupInput()
    {
        AddKey("move_forward", Key.W); AddKey("move_back", Key.S);
        AddKey("move_left", Key.A); AddKey("move_right", Key.D);
        AddKey("attack", Key.Space);
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        var ev = new InputEventKey { PhysicalKeycode = key };
        InputMap.ActionAddEvent(action, ev);
    }

    private void BuildWorld()
    {
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -30, 0), ShadowEnabled = true, LightEnergy = 1.25f });
        var worldEnv = new WorldEnvironment();
        worldEnv.Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.43f, 0.62f, 0.78f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.8f, 0.92f), AmbientLightEnergy = 0.7f };
        AddChild(worldEnv);

        var ground = new StaticBody3D();
        ground.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(100, 1, 100) }, Position = new Vector3(0, -0.5f, 0), MaterialOverride = Player.Mat(new Color(0.24f, 0.42f, 0.24f)) });
        ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) }, Position = new Vector3(0, -0.5f, 0) });
        AddChild(ground);

        for (int i = 0; i < 24; i++)
        {
            float x = Mathf.Sin(i * 13.7f) * 38f;
            float z = Mathf.Cos(i * 8.9f) * 38f;
            if (new Vector2(x, z).Length() < 9) continue;
            AddTree(new Vector3(x, 0, z), 0.8f + (i % 3) * 0.18f);
        }
        for (int i = 0; i < 16; i++)
        {
            float x = Mathf.Sin(i * 5.1f) * 29f;
            float z = Mathf.Cos(i * 7.3f) * 29f;
            var rock = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.9f, Height = 1.5f }, Position = new Vector3(x, 0.35f, z), Scale = new Vector3(1.3f, 0.7f, 0.9f), MaterialOverride = Player.Mat(new Color(0.38f, 0.39f, 0.42f)) };
            AddChild(rock);
        }
        BuildCabin();
    }

    private void AddTree(Vector3 p, float s)
    {
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = 2.4f }, Position = p + new Vector3(0, 1.2f * s, 0), Scale = Vector3.One * s, MaterialOverride = Player.Mat(new Color(0.32f, 0.19f, 0.1f)) });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.25f, Height = 2.3f }, Position = p + new Vector3(0, 2.8f * s, 0), Scale = new Vector3(1, 1.15f, 1) * s, MaterialOverride = Player.Mat(new Color(0.1f, 0.34f, 0.17f)) });
    }

    private void BuildCabin()
    {
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(5, 3, 4) }, Position = new Vector3(-12, 1.5f, -4), MaterialOverride = Player.Mat(new Color(0.48f, 0.31f, 0.18f)) });
        AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(6, 2, 5) }, Position = new Vector3(-12, 4, -4), RotationDegrees = new Vector3(0, 0, 180), MaterialOverride = Player.Mat(new Color(0.28f, 0.15f, 0.12f)) });
        AddChild(new Label3D { Text = "ECLIPSE OUTPOST", FontSize = 42, Position = new Vector3(-12, 5.3f, -6.1f), Modulate = new Color(1, 0.85f, 0.55f) });
    }

    private void SpawnSlime(Vector3 p)
    {
        var enemy = new Enemy { Position = p };
        AddChild(enemy); enemy.SetTarget(_player); _enemies.Add(enemy);
    }

    public void HitClosestEnemy(Vector3 from, float range, int damage)
    {
        Enemy? closest = null; float best = range;
        foreach (var enemy in _enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy)) continue;
            float d = from.DistanceTo(enemy.GlobalPosition);
            if (d < best) { best = d; closest = enemy; }
        }
        if (closest == null) return;
        closest.TakeHit(damage);
        if (!GodotObject.IsInstanceValid(closest) || closest.Health <= 0) _kills++;
        _hud.Text = $"HP: {_player.Health}     Slimes defeated: {_kills}";
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var title = new Label { Text = "PROJECT ECLIPSE  |  THE FORGOTTEN VALE", Position = new Vector2(20, 16) };
        title.AddThemeFontSizeOverride("font_size", 22); layer.AddChild(title);
        _hud = new Label { Text = $"HP: {_player.Health}     Slimes defeated: 0", Position = new Vector2(20, 50) }; layer.AddChild(_hud);
        var hint = new Label { Text = "WASD to move  |  SPACE to attack", Position = new Vector2(20, 80) }; layer.AddChild(hint);
        AddMoveButton(layer, "↑", 100, -178, "move_forward");
        AddMoveButton(layer, "↓", 100, -88, "move_back");
        AddMoveButton(layer, "←", 10, -88, "move_left");
        AddMoveButton(layer, "→", 190, -88, "move_right");
        var attack = new Button { Text = "ATTACK", AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = -185, OffsetTop = -125, OffsetRight = -25, OffsetBottom = -30 };
        attack.AddThemeFontSizeOverride("font_size", 22); attack.ButtonDown += _player.Attack; layer.AddChild(attack);
    }

    private static void AddMoveButton(CanvasLayer layer, string caption, float x, float y, string action)
    {
        var button = new Button { Text = caption, AnchorLeft = 0, AnchorTop = 1, AnchorRight = 0, AnchorBottom = 1, OffsetLeft = x, OffsetTop = y, OffsetRight = x + 80, OffsetBottom = y + 80 };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.ButtonDown += () => Input.ActionPress(action);
        button.ButtonUp += () => Input.ActionRelease(action);
        layer.AddChild(button);
    }
}
