namespace Godot
{
    public class Node
    {
        public virtual void _Ready() { }
        public virtual void _Process(double delta) { }
        public virtual void _PhysicsProcess(double delta) { }
        public virtual void _Input(InputEvent @event) { }
        public virtual void _UnhandledInput(InputEvent @event) { }
        public virtual void _Notification(long what) { }
    }
    public class Node2D : Node { }
    public class Control : Node { }
    public class GlobalClassAttribute : System.Attribute { }
    public class ExportAttribute : System.Attribute { }
    public class SignalAttribute : System.Attribute { }

    public struct Vector2 { public float X, Y; public Vector2(float x, float y) { X = x; Y = y; } }
    public struct Vector3 { public float X, Y, Z; public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; } }
    public struct Color { public float R, G, B, A; public Color(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; } }
    public class InputEvent { }
}
