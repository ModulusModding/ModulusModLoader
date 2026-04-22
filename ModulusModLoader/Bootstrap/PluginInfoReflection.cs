using System.Reflection;
using BepInEx;

namespace ModulusModLoader;

internal static class PluginInfoReflection
{
    private static readonly PropertyInfo? TypeNameProperty =
        typeof(PluginInfo).GetProperty("TypeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly MethodInfo? LocationSetter =
        typeof(PluginInfo).GetProperty("Location", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetSetMethod(true);

    private static readonly MethodInfo? InstanceSetter =
        typeof(PluginInfo).GetProperty("Instance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetSetMethod(true);

    internal static string? GetTypeName(PluginInfo info) => TypeNameProperty?.GetValue(info) as string;

    internal static void SetLocation(PluginInfo info, string path) => LocationSetter?.Invoke(info, new object[] { path });

    internal static void SetInstance(PluginInfo info, BaseUnityPlugin? instance) =>
        InstanceSetter?.Invoke(info, new object[] { instance! });
}
