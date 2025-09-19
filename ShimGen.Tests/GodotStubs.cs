namespace Godot
{
    public class Node
    {
        public virtual void _Ready() { }
        public virtual void _Process(double delta) { }
    }
    public class Node2D : Node { }
    public class Control : Node { }
    public class GlobalClassAttribute : System.Attribute { }
    public class ExportAttribute : System.Attribute { }
}
