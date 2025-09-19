using Godot;
namespace Generated;
[GlobalClass]
public partial class Foo : Godot.Node2D
{
    private readonly Game.FooImpl _impl = new Game.FooImpl();
    [Export] public System.Int32 Speed { get => _impl.Speed; set => _impl.Speed = value; }
    public override void _Ready() => _impl.Ready();
    public override void _Process(double delta) => _impl.Process(delta);
}
