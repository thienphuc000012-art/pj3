// These minimal types isolate the real campaign data code from the Unity player.
namespace UnityEngine
{
    public class ScriptableObject { }
    public class Sprite { }
    public class CreateAssetMenuAttribute : System.Attribute { public string menuName; }
    public class TextAreaAttribute : System.Attribute { }
    public class MinAttribute : System.Attribute { public MinAttribute(float value) { } }
    public struct Vector3 { }
    public struct Quaternion { public static Quaternion identity => default; }
    public static class Mathf
    {
        public static int Max(int a, int b) => System.Math.Max(a, b);
        public static int Min(int a, int b) => System.Math.Min(a, b);
    }
}
public class BattleUnit { }
public class ActionData { public string name; }
