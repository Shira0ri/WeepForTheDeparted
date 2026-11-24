using System;
using System.Reflection;

class Test {
    static void Main(string[] args) {
        var asm = Assembly.LoadFrom(@"G:\Terraria stuff\tModLoader\Libraries\FNA\1.0.0\FNA.dll");
        var t = asm.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch");
        Console.WriteLine("Type: " + t);
        foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)) {
            Console.WriteLine(f.Name + ": " + f.FieldType.Name);
        }
    }
}
