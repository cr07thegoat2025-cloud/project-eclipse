using Godot;
public partial class Player : CharacterBody3D {
 public int Health {get;private set;}=100; public int MaxHealth=>100; private float cooldown;
 public override void _Ready(){
  AddChild(new CollisionShape3D{Shape=new CapsuleShape3D{Radius=.42f,Height=1.8f},Position=new Vector3(0,.9f,0)});
  AddChild(new MeshInstance3D{Mesh=new CapsuleMesh{Radius=.42f,Height=1.8f},Position=new Vector3(0,.9f,0),MaterialOverride=Mat(new Color(.16f,.29f,.78f))});
  AddChild(new MeshInstance3D{Mesh=new SphereMesh{Radius=.31f,Height=.62f},Position=new Vector3(0,1.95f,0),MaterialOverride=Mat(new Color(.94f,.72f,.5f))});
  var cam=new Camera3D{Position=new Vector3(0,5,8),Current=true,Fov=72}; AddChild(cam); cam.LookAt(new Vector3(0,1,0),Vector3.Up);
 }
 public override void _PhysicsProcess(double delta){
  float dt=(float)delta; cooldown=Mathf.Max(0,cooldown-dt);
  Vector2 v=Input.GetVector("move_left","move_right","move_forward","move_back"); Vector3 d=new Vector3(v.X,0,v.Y).Normalized();
  Velocity=new Vector3(d.X*6.2f,Velocity.Y,d.Z*6.2f); if(d.LengthSquared()>.01f) LookAt(GlobalPosition+d,Vector3.Up);
  if(!IsOnFloor()) Velocity=new Vector3(Velocity.X,Velocity.Y-20*dt,Velocity.Z); else if(Velocity.Y<0) Velocity=new Vector3(Velocity.X,0,Velocity.Z);
  if(IsOnFloor()&&Input.IsActionJustPressed("jump")) Velocity=new Vector3(Velocity.X,7,Velocity.Z);
  MoveAndSlide(); if(Input.IsActionJustPressed("attack")) Attack(); if(Input.IsActionJustPressed("interact")) GetParent<MainWorld>().Interact();
 }
 public void Attack(){if(cooldown>0)return;cooldown=.48f;GetParent<MainWorld>().HitClosestEnemy(GlobalPosition,3.25f,35);}
 public void Hurt(int n){Health=Mathf.Max(0,Health-n);if(Health==0){Health=100;GlobalPosition=new Vector3(0,1.1f,0);}}
 public void Heal(int n)=>Health=Mathf.Min(MaxHealth,Health+n);
 public void RestoreHealth(int n)=>Health=Mathf.Clamp(n,1,MaxHealth);
 public static StandardMaterial3D Mat(Color c)=>new(){AlbedoColor=c,Roughness=.9f};
}