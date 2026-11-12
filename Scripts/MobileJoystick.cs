using Godot;

public partial class MobileJoystick : Control
{
    public Vector2 Direction { get; private set; } = Vector2.Zero;
    private const float Radius = 68f;
    private const float KnobRadius = 27f;
    private bool pressed;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(180, 180);
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = new Vector2(90, 90);
        DrawCircle(center, Radius, new Color(.08f, .09f, .13f, .38f));
        DrawArc(center, Radius, 0, Mathf.Tau, 64, new Color(.8f, .82f, .9f, .55f), 3);
        DrawCircle(center + Direction * Radius, KnobRadius, new Color(.55f, .58f, .68f, .78f));
        DrawArc(center + Direction * Radius, KnobRadius, 0, Mathf.Tau, 48, new Color(1, 1, 1, .8f), 2);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch)
        {
            pressed = touch.Pressed;
            Direction = pressed ? ToDirection(touch.Position) : Vector2.Zero;
            AcceptEvent();
            QueueRedraw();
        }
        else if (@event is InputEventScreenDrag drag && pressed)
        {
            Direction = ToDirection(drag.Position);
            AcceptEvent();
            QueueRedraw();
        }
        else if (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
        {
            pressed = mouse.Pressed;
            Direction = pressed ? ToDirection(mouse.Position) : Vector2.Zero;
            AcceptEvent();
            QueueRedraw();
        }
        else if (@event is InputEventMouseMotion motion && pressed)
        {
            Direction = ToDirection(motion.Position);
            AcceptEvent();
            QueueRedraw();
        }
    }

    private Vector2 ToDirection(Vector2 position)
    {
        Vector2 offset = position - new Vector2(90, 90);
        return offset.Length() > Radius ? offset.Normalized() : offset / Radius;
    }
}
