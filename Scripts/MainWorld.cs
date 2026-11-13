using Godot;
using System;
using System.Collections.Generic;

public partial class MainWorld : Node3D
{
    private Player player = null!;
    private MobileJoystick? joystick;
    private Label stats = null!, questLabel = null!, message = null!;
    private readonly List<Enemy> enemies = new();
    private int kills, gold = 50, xp, level = 1, potions, swordPower = 35;
    private bool questAccepted, questComplete;
    private float messageTimer;
    private readonly Vector3 elderPos = new(-12, 0, -1);
    private Camera3D worldCamera = null!;
    private float cameraYaw;
    private float cameraPitch = .32f;
    private float cameraDistance = 7.5f;
    private const float MinCameraDistance = 3.2f;
    private const float MaxCameraDistance = 10.5f;
    private bool cameraDragging;

    public override void _Ready()
    {
        SetupInput();
        BuildWorld();

        player = new Player { Position = new Vector3(0, 1.1f, 0) };
        AddChild(player);
        LoadGame();

        worldCamera = new Camera3D { Current = true, Fov = 72, Near = .08f, Far = 180 };
        AddChild(worldCamera);
        UpdateCamera(true);

        Vector3[] spawnPoints =
        {
            new(6, .7f, -6), new(-7, .7f, -10), new(12, .7f, 7),
            new(18, .7f, -12), new(-19, .7f, 14)
        };
        for (int i = Mathf.Clamp(kills, 0, spawnPoints.Length); i < spawnPoints.Length; i++)
            Spawn(spawnPoints[i]);

        BuildHud();
        RefreshHud();
        Tell("Welcome to the Forgotten Vale. Find the Village Elder!");
    }

    public override void _Process(double delta)
    {
        if (messageTimer > 0)
        {
            messageTimer -= (float)delta;
            if (messageTimer <= 0 && GodotObject.IsInstanceValid(message))
                message.Text = "";
        }

        // Camera keyboard controls: arrows orbit/tilt; Q/E also rotate. WASD stays movement-only.
        float cameraStep = 2.15f * (float)delta;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.Q)) cameraYaw += cameraStep;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.E)) cameraYaw -= cameraStep;
        if (Input.IsKeyPressed(Key.Up)) cameraPitch = Mathf.Clamp(cameraPitch + cameraStep * .55f, .10f, 1.05f);
        if (Input.IsKeyPressed(Key.Down)) cameraPitch = Mathf.Clamp(cameraPitch - cameraStep * .55f, .10f, 1.05f);

        if (GodotObject.IsInstanceValid(player) && GodotObject.IsInstanceValid(worldCamera))
            UpdateCamera(false);

        if (questAccepted && !questComplete && kills >= 3)
        {
            questComplete = true;
            gold += 100;
            AddXp(100);
            Tell("Quest complete! +100 Gold and +100 XP.");
            RefreshHud();
            SaveGame();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            cameraDragging = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (e is InputEventMouseButton wheel && wheel.Pressed && wheel.ButtonIndex == MouseButton.WheelUp)
        {
            cameraDistance = Mathf.Clamp(cameraDistance - .65f, MinCameraDistance, MaxCameraDistance);
            return;
        }
        if (e is InputEventMouseButton wheelDown && wheelDown.Pressed && wheelDown.ButtonIndex == MouseButton.WheelDown)
        {
            cameraDistance = Mathf.Clamp(cameraDistance + .65f, MinCameraDistance, MaxCameraDistance);
            return;
        }

        if (e is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Right)
        {
            cameraDragging = mouse.Pressed;
            Input.MouseMode = mouse.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            return;
        }

        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateCamera(motion.Relative);
            return;
        }

        // On touch devices, drag the right half of the screen to orbit the camera.
        if (e is InputEventScreenTouch touch)
        {
            cameraDragging = touch.Pressed && touch.Position.X > GetViewport().GetVisibleRect().Size.X * .42f;
            return;
        }
        if (e is InputEventScreenDrag drag && drag.Position.X > GetViewport().GetVisibleRect().Size.X * .42f)
            RotateCamera(drag.Relative);
    }

    private void RotateCamera(Vector2 delta)
    {
        cameraYaw -= delta.X * .006f;
        cameraPitch = Mathf.Clamp(cameraPitch - delta.Y * .0045f, .12f, .92f);
    }

    private void UpdateCamera(bool instant)
    {
        float horizontal = Mathf.Cos(cameraPitch) * cameraDistance;
        Vector3 focus = player.GlobalPosition + new Vector3(0, 1.2f, 0);
        Vector3 desired = focus + new Vector3(Mathf.Sin(cameraYaw) * horizontal,
            Mathf.Sin(cameraPitch) * cameraDistance + .15f,
            Mathf.Cos(cameraYaw) * horizontal);

        // Pull the camera forward if a wall, tree, or building blocks the ideal follow position.
        var ray = PhysicsRayQueryParameters3D.Create(focus, desired);
        ray.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
        var obstruction = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (obstruction.Count > 0 && obstruction.TryGetValue("position", out Variant hitPosition))
        {
            Vector3 hit = hitPosition.AsVector3();
            Vector3 awayFromFocus = (desired - focus).Normalized();
            desired = hit - awayFromFocus * .28f;
        }

        float blend = 1f - Mathf.Exp(-14f * (float)GetProcessDeltaTime());
        worldCamera.GlobalPosition = instant ? desired : worldCamera.GlobalPosition.Lerp(desired, blend);
        worldCamera.LookAt(focus, Vector3.Up);
    }

    public float GetCameraYaw() => cameraYaw;
    public Vector2 GetJoystickDirection() => joystick?.Direction ?? Vector2.Zero;

    private void SetupInput()
    {
        KeyAction("move_forward", Key.W);
        KeyAction("move_back", Key.S);
        KeyAction("move_left", Key.A);
        KeyAction("move_right", Key.D);
        KeyAction("attack", Key.Space);
        KeyAction("interact", Key.E);
        KeyAction("jump", Key.Shift);
    }

    private static void KeyAction(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private void BuildWorld()
    {
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -30, 0),
            ShadowEnabled = true,
            LightEnergy = 1.35f,
            DirectionalShadowMaxDistance = 75
        });

        var environment = new WorldEnvironment();
        environment.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(.30f, .48f, .62f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(.72f, .79f, .9f),
            AmbientLightEnergy = .72f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = new Color(.48f, .59f, .64f),
            FogDensity = .006f
        };
        AddChild(environment);

        var ground = new StaticBody3D { Name = "SolidWorldGround" };
        ground.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(110, 1, 110) },
            Position = new Vector3(0, -.5f, 0),
            MaterialOverride = Mat(new Color(.19f, .34f, .20f), .05f, .95f)
        });
        ground.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(110, 1, 110) },
            Position = new Vector3(0, -.5f, 0)
        });
        AddChild(ground);

        // A stone-and-dirt road guides the player from spawn to the Elder's outpost.
        var pathMat = Mat(new Color(.39f, .34f, .27f), .05f, .95f);
        for (int i = 0; i < 13; i++)
        {
            float z = -i * 1.15f - .2f;
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(3.4f, .035f, 1.18f) },
                Position = new Vector3(-.7f - i * .78f, .018f, z),
                MaterialOverride = pathMat
            });
        }
        for (int i = 0; i < 8; i++)
        {
            float z = -3.5f - i * .9f;
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(3.2f, .025f, .92f) },
                Position = new Vector3(-12, .02f, z),
                MaterialOverride = pathMat
            });
        }

        // Forest edge: every trunk and large rock has a matching physics collider.
        for (int i = 0; i < 38; i++)
        {
            float x = Mathf.Sin(i * 13.7f) * 43;
            float z = Mathf.Cos(i * 8.9f) * 43;
            if (new Vector2(x, z).Length() < 10 || (x < -5 && x > -22 && z < 5 && z > -15))
                continue;
            CreateTree(new Vector3(x, 0, z), .8f + (i % 3) * .18f);
        }

        for (int i = 0; i < 25; i++)
        {
            float x = Mathf.Sin(i * 5.1f) * 34;
            float z = Mathf.Cos(i * 7.3f) * 34;
            if (new Vector2(x, z).Length() < 7)
                continue;
            CreateRock(new Vector3(x, 0, z), .65f + (i % 4) * .18f);
        }

        BuildOutpost();
        BuildLantern(new Vector3(-8.5f, 0, -2.5f));
        BuildLantern(new Vector3(-15.5f, 0, -2.5f));
        BuildFenceLine(new Vector3(-18, 0, -9), 12, true);
        BuildFenceLine(new Vector3(-18, 0, -9), 9, false);
        BuildAncientRuins();
        LoadImportedAdventureMap();
    }


    private void LoadImportedAdventureMap()
    {
        const string mapPath = "res://game_pirate_adventure_map.glb";
        if (!ResourceLoader.Exists(mapPath))
        {
            GD.PushError("MAP RESOURCE MISSING: " + mapPath);
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(mapPath);
        if (packed == null)
        {
            GD.PushError("MAP RESOURCE EXISTS BUT DID NOT IMPORT AS A PackedScene: " + mapPath);
            return;
        }

        var map = packed.Instantiate<Node3D>();
        map.Name = "ImportedPirateAdventureMap";
        AddChild(map);
        map.Visible = true;

        // Imported assets often have a distant origin or an unexpected authoring scale.
        // Measure all mesh bounds, fit the map around the player, and place its lowest
        // geometry at ground level so it is actually visible from the starting position.
        if (TryGetMeshBounds(map, out Aabb rawBounds))
        {
            float horizontal = Mathf.Max(rawBounds.Size.X, rawBounds.Size.Z);
            float scale = Mathf.Min(1.0f, 100.0f / Mathf.Max(horizontal, 0.01f));
            if (rawBounds.Size.Y > 55.0f)
                scale = Mathf.Min(scale, 55.0f / rawBounds.Size.Y);
            map.Scale = Vector3.One * scale;

            if (TryGetMeshBounds(map, out Aabb scaledBounds))
            {
                Vector3 center = scaledBounds.Position + scaledBounds.Size * 0.5f;
                map.GlobalPosition += new Vector3(-center.X, -scaledBounds.Position.Y, -center.Z);
                GD.Print($"Adventure map normalized. Mesh bounds: {scaledBounds.Size}, scale: {scale}");
            }
            AddImportedMeshColliders(map);
            GD.Print("Adventure map loaded and static mesh colliders generated.");
        }
        else
        {
            GD.PushError("Adventure map loaded but contains no MeshInstance3D geometry.");
        }
    }

    private bool TryGetMeshBounds(Node node, out Aabb bounds)
    {
        bool found = false;
        bounds = new Aabb();
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
        {
            Aabb worldBounds = meshInstance.GlobalTransform * meshInstance.GetAabb();
            bounds = found ? bounds.Merge(worldBounds) : worldBounds;
            found = true;
        }
        foreach (Node child in node.GetChildren())
        {
            if (TryGetMeshBounds(child, out Aabb childBounds))
            {
                bounds = found ? bounds.Merge(childBounds) : childBounds;
                found = true;
            }
        }
        return found;
    }

    private void AddImportedMeshColliders(Node node)
    {
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
        {
            var shape = meshInstance.Mesh.CreateTrimeshShape();
            if (shape != null)
            {
                var collider = new StaticBody3D { Name = meshInstance.Name + "_ImportedCollision" };
                AddChild(collider);
                collider.GlobalTransform = meshInstance.GlobalTransform;
                collider.AddChild(new CollisionShape3D { Shape = shape });
            }
        }
        foreach (Node child in node.GetChildren())
            AddImportedMeshColliders(child);
    }

    private void BuildAncientRuins()
    {
        // A small explorable landmark on the eastern ridge, with solid stone pillars.
        Vector3 center = new Vector3(23, 0, -22);
        var stone = Mat(new Color(.38f, .40f, .39f), .12f, .94f);
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Tau * i / 5f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * 3.2f, 0, Mathf.Sin(angle) * 3.2f);
            var pillar = new StaticBody3D { Name = "AncientRuinsPillar", Position = center + offset };
            pillar.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = .38f, BottomRadius = .52f, Height = 3.2f },
                Position = new Vector3(0, 1.6f, 0),
                MaterialOverride = stone
            });
            pillar.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = .5f, Height = 3.2f },
                Position = new Vector3(0, 1.6f, 0)
            });
            AddChild(pillar);
        }

        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 4.4f, BottomRadius = 4.4f, Height = .12f, RadialSegments = 12 },
            Position = center + new Vector3(0, .02f, 0),
            MaterialOverride = Mat(new Color(.27f, .28f, .26f), .04f, .98f)
        });
        AddChild(new Label3D
        {
            Text = "THE OLD WATCHSTONES",
            FontSize = 34,
            Position = center + new Vector3(0, 4.1f, 0),
            Modulate = new Color(.82f, .88f, .78f)
        });
    }

    private void CreateTree(Vector3 p, float scale)
    {
        var tree = new StaticBody3D { Name = "CollidableTree" };
        tree.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = .16f, BottomRadius = .29f, Height = 2.5f },
            Position = new Vector3(0, 1.25f * scale, 0),
            Scale = Vector3.One * scale,
            MaterialOverride = Mat(new Color(.27f, .15f, .085f), .02f, .9f)
        });
        tree.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = .29f * scale, Height = 2.5f * scale },
            Position = new Vector3(0, 1.25f * scale, 0)
        });
        tree.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.25f, Height = 2.3f },
            Position = new Vector3(0, 2.75f * scale, 0),
            Scale = new Vector3(1, 1.12f, 1) * scale,
            MaterialOverride = Mat(new Color(.075f, .27f, .14f), .02f, .96f)
        });
        tree.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = .95f * scale },
            Position = new Vector3(0, 2.75f * scale, 0)
        });
        tree.Position = p;
        AddChild(tree);
    }

    private void CreateRock(Vector3 p, float scale)
    {
        var rock = new StaticBody3D { Name = "SolidBoulder" };
        rock.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .9f, Height = 1.5f, RadialSegments = 10, Rings = 6 },
            Position = new Vector3(0, .43f * scale, 0),
            Scale = new Vector3(1.25f, .75f, 1f) * scale,
            MaterialOverride = Mat(new Color(.32f, .35f, .36f), .12f, .92f)
        });
        rock.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = .68f * scale },
            Position = new Vector3(0, .48f * scale, 0)
        });
        rock.Position = p;
        AddChild(rock);
    }

    private void BuildOutpost()
    {
        var house = new StaticBody3D { Name = "EldersOutpostCollision" };
        house.Position = new Vector3(-12, 0, -4);
        house.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(5, 3, 4) },
            Position = new Vector3(0, 1.5f, 0),
            MaterialOverride = Mat(new Color(.40f, .25f, .15f), .04f, .88f)
        });
        house.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(5, 3, 4) },
            Position = new Vector3(0, 1.5f, 0)
        });
        house.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(6, 2, 5) },
            Position = new Vector3(0, 4, 0),
            RotationDegrees = new Vector3(0, 0, 180),
            MaterialOverride = Mat(new Color(.20f, .095f, .075f), .03f, .82f)
        });
        house.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(6, .45f, 5) },
            Position = new Vector3(0, 3.15f, 0)
        });
        AddChild(house);

        AddChild(new Label3D
        {
            Text = "ELDER'S OUTPOST",
            FontSize = 42,
            Position = new Vector3(-12, 5.3f, -6.1f),
            Modulate = new Color(1, .85f, .55f)
        });

        var npc = new StaticBody3D { Name = "VillageElder", Position = elderPos };
        npc.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = .43f, Height = 1.8f },
            Position = new Vector3(0, .9f, 0)
        });
        npc.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = .38f, Height = 1.7f },
            Position = new Vector3(0, .85f, 0),
            MaterialOverride = Mat(new Color(.43f, .19f, .59f), .08f, .7f)
        });
        npc.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .3f, Height = .6f },
            Position = new Vector3(0, 1.85f, 0),
            MaterialOverride = Mat(new Color(.78f, .58f, .40f), 0, .75f)
        });
        AddChild(npc);
        AddChild(new Label3D
        {
            Text = "VILLAGE ELDER [E]",
            FontSize = 32,
            Position = elderPos + new Vector3(0, 2.7f, 0),
            Modulate = new Color(1, .9f, .65f)
        });
    }

    private void BuildLantern(Vector3 p)
    {
        var lamp = new StaticBody3D { Position = p, Name = "LanternPost" };
        lamp.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = .07f, BottomRadius = .1f, Height = 2.1f },
            Position = new Vector3(0, 1.05f, 0),
            MaterialOverride = Mat(new Color(.19f, .13f, .09f), .25f, .55f)
        });
        lamp.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = .11f, Height = 2.1f },
            Position = new Vector3(0, 1.05f, 0)
        });
        lamp.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .19f, Height = .38f },
            Position = new Vector3(0, 2.05f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, .66f, .25f),
                EmissionEnabled = true,
                Emission = new Color(1, .48f, .12f),
                EmissionEnergyMultiplier = 2.2f
            }
        });
        lamp.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 2.05f, 0),
            LightColor = new Color(1, .62f, .28f),
            LightEnergy = .75f,
            OmniRange = 5
        });
        AddChild(lamp);
    }

    private void BuildFenceLine(Vector3 start, int count, bool alongX)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 p = start + (alongX ? new Vector3(i * 1.05f, 0, 0) : new Vector3(0, 0, i * 1.05f));
            var post = new StaticBody3D { Position = p, Name = "FencePost" };
            post.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(.16f, 1.1f, .16f) },
                Position = new Vector3(0, .55f, 0),
                MaterialOverride = Mat(new Color(.27f, .16f, .095f), .02f, .92f)
            });
            post.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(.2f, 1.1f, .2f) },
                Position = new Vector3(0, .55f, 0)
            });
            AddChild(post);
        }
    }

    private void Spawn(Vector3 p)
    {
        var enemy = new Enemy { Position = p };
        AddChild(enemy);
        enemy.SetTarget(player);
        enemies.Add(enemy);
    }

    public void HitClosestEnemy(Vector3 position, float range, int damage)
    {
        Enemy? found = null;
        float best = range;
        foreach (Enemy enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Health <= 0)
                continue;
            float distance = position.DistanceTo(enemy.GlobalPosition);
            if (distance < best)
            {
                best = distance;
                found = enemy;
            }
        }
        if (found == null)
        {
            Tell("No enemy in range!");
            return;
        }
        found.TakeHit(swordPower);
        if (found.Health <= 0)
        {
            kills++;
            gold += 10;
            AddXp(25);
            Tell("Demogorgon defeated! +10 Gold, +25 XP");
            PersistGame(false);
        }
        RefreshHud();
    }

    public void Interact()
    {
        if (player.GlobalPosition.DistanceTo(elderPos) > 5)
        {
            Tell("Nobody nearby to talk to.");
            return;
        }
        if (!questAccepted)
        {
            questAccepted = true;
            Tell("Elder: Demogorgons threaten our vale. Defeat 3!");
        }
        else if (questComplete)
            Tell("Elder: You have our gratitude, hero.");
        else
            Tell($"Elder: Slimes defeated: {Mathf.Min(kills, 3)}/3.");
        RefreshHud();
    }

    private void AddXp(int amount)
    {
        xp += amount;
        while (xp >= level * 100)
        {
            xp -= level * 100;
            level++;
            gold += 20;
            Tell($"LEVEL UP! Level {level}. +20 bonus Gold!");
        }
    }

    private void BuyPotion()
    {
        if (gold < 25) { Tell("A potion costs 25 Gold."); return; }
        gold -= 25; potions++;
        Tell("Healing potion purchased."); RefreshHud(); SaveGame();
    }

    private void UsePotion()
    {
        if (potions <= 0) { Tell("No healing potions."); return; }
        if (player.Health == player.MaxHealth) { Tell("Health is already full."); return; }
        potions--; player.Heal(45);
        Tell("Potion used: restored up to 45 HP."); RefreshHud(); SaveGame();
    }

    private void UpgradeSword()
    {
        if (gold < 100) { Tell("Sword upgrade costs 100 Gold."); return; }
        if (swordPower >= 70) { Tell("Sword is fully upgraded."); return; }
        gold -= 100; swordPower += 15;
        Tell($"Sword upgraded! Damage: {swordPower}."); RefreshHud(); SaveGame();
    }

    private void SaveGame() => PersistGame(true);

    private void PersistGame(bool notify)
    {
        var config = new ConfigFile();
        config.SetValue("player", "gold", gold);
        config.SetValue("player", "xp", xp);
        config.SetValue("player", "level", level);
        config.SetValue("player", "health", player.Health);
        config.SetValue("player", "potions", potions);
        config.SetValue("player", "sword", swordPower);
        config.SetValue("player", "kills", kills);
        config.SetValue("quest", "accepted", questAccepted);
        config.SetValue("quest", "complete", questComplete);
        config.SetValue("world", "position", player.GlobalPosition);
        Error error = config.Save("user://eclipse_save.cfg");
        if (notify) Tell(error == Error.Ok ? "Game saved!" : "Save failed.");
    }

    private void LoadGame()
    {
        var config = new ConfigFile();
        if (config.Load("user://eclipse_save.cfg") != Error.Ok) return;
        gold = (int)config.GetValue("player", "gold", 50);
        xp = (int)config.GetValue("player", "xp", 0);
        level = (int)config.GetValue("player", "level", 1);
        potions = (int)config.GetValue("player", "potions", 0);
        swordPower = (int)config.GetValue("player", "sword", 35);
        kills = (int)config.GetValue("player", "kills", 0);
        questAccepted = (bool)config.GetValue("quest", "accepted", false);
        questComplete = (bool)config.GetValue("quest", "complete", false);
        player.RestoreHealth((int)config.GetValue("player", "health", 100));
        player.GlobalPosition = (Vector3)config.GetValue("world", "position", new Vector3(0, 1.1f, 0));
    }

    private void RefreshHud()
    {
        if (!GodotObject.IsInstanceValid(stats)) return;
        stats.Text = $"HP {player.Health}/100   LVL {level} ({xp}/{level * 100} XP)   GOLD {gold}   POTIONS {potions}   SWORD {swordPower}";
        questLabel.Text = !questAccepted ? "Quest: Talk to the Village Elder [E]" :
            questComplete ? "Quest complete: A Safer Vale" :
            $"Quest: Defeat 3 Demogorgons ({Mathf.Min(kills, 3)}/3)";
    }

    private void Tell(string text)
    {
        if (!GodotObject.IsInstanceValid(message)) return;
        message.Text = text;
        messageTimer = 4.5f;
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var title = new Label { Text = "PROJECT ECLIPSE  |  THE FORGOTTEN VALE", Position = new Vector2(18, 12) };
        title.AddThemeFontSizeOverride("font_size", 22);
        layer.AddChild(title);
        stats = new Label { Position = new Vector2(18, 44) };
        layer.AddChild(stats);
        questLabel = new Label { Position = new Vector2(18, 72) };
        layer.AddChild(questLabel);
        message = new Label { Position = new Vector2(18, 104) };
        message.AddThemeColorOverride("font_color", new Color(1, .88f, .55f));
        layer.AddChild(message);

        ActionButton(layer, "JUMP", -290, -105, -175, -30,
            () => Input.ActionPress("jump"), () => Input.ActionRelease("jump"), true);
        ActionButton(layer, "ATTACK", -165, -125, -20, -30, player.Attack, null, true);
        ActionButton(layer, "TALK", -165, -205, -20, -130, Interact, null, true);

        joystick = new MobileJoystick
        {
            Position = new Vector2(24, GetViewport().GetVisibleRect().Size.Y - 205)
        };
        layer.AddChild(joystick);

        var shop = new VBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -205, OffsetTop = 14, OffsetRight = -12 };
        layer.AddChild(shop);
        shop.AddChild(new Label { Text = "FIELD SHOP" });
        ShopButton(shop, "Potion • 25 G", BuyPotion);
        ShopButton(shop, "Sword upgrade • 100 G", UpgradeSword);
        ShopButton(shop, "Use potion", UsePotion);
        ShopButton(shop, "SAVE GAME", SaveGame);
        ShopButton(shop, "LOAD GAME", () => { LoadGame(); RefreshHud(); Tell("Save loaded."); });
        RefreshHud();
    }

    private static void ShopButton(VBoxContainer box, string label, Action action)
    {
        var button = new Button { Text = label, CustomMinimumSize = new Vector2(185, 34) };
        button.Pressed += action;
        box.AddChild(button);
    }

    private static void ActionButton(CanvasLayer layer, string label, float left, float top,
        float right, float bottom, Action down, Action? up, bool rightAligned)
    {
        var button = new Button
        {
            Text = label,
            AnchorLeft = rightAligned ? 1 : 0,
            AnchorTop = 1,
            AnchorRight = rightAligned ? 1 : 0,
            AnchorBottom = 1,
            OffsetLeft = left,
            OffsetTop = top,
            OffsetRight = right,
            OffsetBottom = bottom
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.ButtonDown += down;
        if (up != null) button.ButtonUp += up;
        layer.AddChild(button);
    }

    private static StandardMaterial3D Mat(Color color, float metallic = .0f, float roughness = .9f) =>
        new() { AlbedoColor = color, Metallic = metallic, Roughness = roughness };
}