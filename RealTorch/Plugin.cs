using Dalamud.Plugin;

namespace Plugin;

// ReSharper disable once ClassNeverInstantiated.Global - instantiated by Dalamud
public sealed partial class Plugin : IDalamudPlugin
{
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<S>();

        LC = new LightController();
    }
    public LightController LC { get; init; }

    public void Dispose()
    {
        LC.Dispose();
    }
}
