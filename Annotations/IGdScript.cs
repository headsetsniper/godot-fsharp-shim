namespace Headsetsniper.Godot.FSharp.Annotations
{
    /// <summary>
    /// Implement on your F# script class to receive the Godot node instance
    /// that the generated C# shim derives from. The shim will set this in _Ready().
    /// </summary>
    /// <typeparam name="T">The Godot base type of the shim (e.g. Godot.Node, Godot.Node2D).</typeparam>
    public interface IGdScript<T>
    {
        /// <summary>
        /// Reference to the backing Godot node instance (the shim itself), set by the shim in _Ready().
        /// </summary>
        T Node { get; set; }
    }
}
