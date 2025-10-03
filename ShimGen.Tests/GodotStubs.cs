namespace Godot
{
    public class Node
    {
        public T? GetNodeOrNull<T>(NodePath path) where T : class => null;
        public virtual void _Ready() { }
        public virtual void _Process(double delta) { }
        public virtual void _PhysicsProcess(double delta) { }
        public virtual void _Input(InputEvent @event) { }
        public virtual void _UnhandledInput(InputEvent @event) { }
        public virtual void _Notification(long what) { }
        public virtual void _EnterTree() { }
        public virtual void _ExitTree() { }
    }
    public class Node2D : Node { }
    public class Control : Node { }
    public class Resource { }
    public class Texture2D : Resource { }
    public class PackedScene : Resource { }
    public class GlobalClassAttribute : System.Attribute { }
    public class IconAttribute : System.Attribute { public IconAttribute(string path) { } }
    public class ToolAttribute : System.Attribute { }
    public class ExportAttribute : System.Attribute
    {
        public ExportAttribute() { }
        public ExportAttribute(PropertyHint hint, string hintString) { }
    }
    public class SignalAttribute : System.Attribute { }
    public enum PropertyHint { None = 0, Range = 1, Flags = 2 }
    public class NodePath { public NodePath(string s) { } }
    public struct StringName { private readonly string _v; public StringName(string v) { _v = v; } }
    public struct RID { private readonly long _v; public RID(long v) { _v = v; } }
    public static class GD { public static void PushError(string s) { } }

    public struct Vector2 { public float X, Y; public Vector2(float x, float y) { X = x; Y = y; } }
    public struct Vector3 { public float X, Y, Z; public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; } }
    public struct Color { public float R, G, B, A; public Color(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; } }
    public struct Basis { }
    public struct Rect2 { }
    public struct Transform2D { }
    public struct Transform3D { }
    public class InputEvent { }
}
