namespace Headsetsniper.Godot.FSharp.Annotations
{
    /// <summary>
    /// Implement on Godot tool scripts when constructor injection is unavailable.
    /// The generated shim will set this in _Ready() so editor-only logic can use the node instance.
    /// </summary>
    /// <typeparam name="T">The Godot base type of the shim (e.g. Godot.Node, Godot.Control).</typeparam>
    public interface IGdToolScript<T>
    {
        /// <summary>
        /// Reference to the backing Godot node instance (the shim itself), set by the shim in _Ready().
        /// </summary>
        T Node { get; set; }
    }
}
