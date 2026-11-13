using Godot;
public partial class Enemy : CharacterBody3D {
 public int Health{get;private set;}=70; private Player? target; private float cooldown;
 public override void _Ready(){
  AddChild(new CollisionShape3D{Shape=new CapsuleShape3D{Radius=.48f,Height=1.9f},Position=new Vector3(0,.95f,0)});
  var npcScene=GD.Load<PackedScene>("res://demogorgon_npc.glb");
  if(npcScene!=null){
   var model=npcScene.Instantiate<Node3D>();
   model.Name="DemogorgonModel";
   model.Scale=Vector3.One*.8f;
   AddChild(model);
   FindAndPlayAnimation(model);
  }else{
   GD.PushWarning("demogorgon_npc.glb did not import; using fallback enemy mesh.");
   AddChild(new MeshInstance3D{Mesh=new SphereMesh{Radius=.62f,Height=1.24f},Position=new Vector3(0,.58f,0),Scale=new Vector3(1.1f,.8f,1.1f),MaterialOverride=Player.Mat(new Color(.18f,.72f,.38f))});
  }
 }
 private void FindAndPlayAnimation(Node node){
  if(node is AnimationPlayer animator){
   var clips=animator.GetAnimationList();
   string? chosen=null;
   foreach(StringName clip in clips){string n=clip.ToString().ToLowerInvariant();if(n.Contains("idle")||n.Contains("walk")||n.Contains("run")){chosen=clip.ToString();break;}}
   if(chosen==null&&clips.Length>0)chosen=clips[0].ToString();
   if(chosen!=null)animator.Play(chosen);
  }
  foreach(Node child in node.GetChildren())FindAndPlayAnimation(child);
 }
 public void SetTarget(Player p)=>target=p;
 public override void _PhysicsProcess(double delta){
  if(target==null||!GodotObject.IsInstanceValid(target))return; float dt=(float)delta;cooldown-=dt;
  Vector3 off=target.GlobalPosition-GlobalPosition;float dist=off.Length();Vector3 d=new Vector3(off.X,0,off.Z).Normalized();
  if(dist>1.5f&&dist<25){Velocity=new Vector3(d.X*2.25f,Velocity.Y,d.Z*2.25f);if(d.LengthSquared()>.01f)LookAt(GlobalPosition+d,Vector3.Up);}else Velocity=new Vector3(0,Velocity.Y,0);
  if(!IsOnFloor())Velocity=new Vector3(Velocity.X,Velocity.Y-18*dt,Velocity.Z);else if(Velocity.Y<0)Velocity=new Vector3(Velocity.X,0,Velocity.Z);
  MoveAndSlide();if(dist<1.75f&&cooldown<=0){target.Hurt(8);cooldown=1.35f;}
 }
 public void TakeHit(int n){Health-=n;if(Health<=0)QueueFree();}
}