using System;

namespace Headsetsniper.Godot.FSharp.Annotations
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AutoConnectAttribute : Attribute
    {
        public AutoConnectAttribute() { }
        public AutoConnectAttribute(string Path, string Signal)
        {
            this.Path = Path;
            this.Signal = Signal;
        }

        public string Path { get; set; } = string.Empty;
        public string Signal { get; set; } = string.Empty;
    }
}
