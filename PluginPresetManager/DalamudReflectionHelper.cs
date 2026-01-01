using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PluginPresetManager;

// Uses reflection to access Dalamud's internal ProfileManager.
// This makes plugin states persist across game restarts.
// May break on Dalamud updates since it uses internal APIs.
public class DalamudReflectionHelper
{
    private readonly IPluginLog log;
    private readonly IDalamudPluginInterface pluginInterface;

    private Assembly? dalamudAssembly;
    private Type? serviceType;
    private Type? profileManagerType;
    private object? profileManager;
    private object? defaultProfile;

    private MethodInfo? addOrUpdateMethod;
    private PropertyInfo? workingPluginIdProperty;

    private bool initialized = false;
    private bool initializationFailed = false;

    public bool IsAvailable => initialized && !initializationFailed;

    public DalamudReflectionHelper(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
    }

    public bool TryInitialize()
    {
        if (initialized) return !initializationFailed;
        if (initializationFailed) return false;

        initialized = true;

        try
        {
            log.Info("Initializing reflection helper...");

            dalamudAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Dalamud");

            if (dalamudAssembly == null)
            {
                log.Warning("Dalamud assembly not found");
                initializationFailed = true;
                return false;
            }

            serviceType = dalamudAssembly.GetType("Dalamud.Service`1");
            if (serviceType == null)
            {
                log.Warning("Service<T> type not found");
                initializationFailed = true;
                return false;
            }

            profileManagerType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Profiles.ProfileManager");
            if (profileManagerType == null)
            {
                log.Warning("ProfileManager type not found");
                initializationFailed = true;
                return false;
            }

            var genericService = serviceType.MakeGenericType(profileManagerType);
            var getMethod = genericService.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getMethod == null)
            {
                log.Warning("Service<ProfileManager>.Get not found");
                initializationFailed = true;
                return false;
            }

            profileManager = getMethod.Invoke(null, null);
            if (profileManager == null)
            {
                log.Warning("ProfileManager is null");
                initializationFailed = true;
                return false;
            }

            var defaultProfileProp = profileManagerType.GetProperty("DefaultProfile");
            if (defaultProfileProp == null)
            {
                log.Warning("DefaultProfile property not found");
                initializationFailed = true;
                return false;
            }

            defaultProfile = defaultProfileProp.GetValue(profileManager);
            if (defaultProfile == null)
            {
                log.Warning("DefaultProfile is null");
                initializationFailed = true;
                return false;
            }

            var profileType = defaultProfile.GetType();
            addOrUpdateMethod = profileType.GetMethod("AddOrUpdateAsync",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Guid), typeof(string), typeof(bool), typeof(bool) },
                null);

            if (addOrUpdateMethod == null)
            {
                log.Warning("AddOrUpdateAsync method not found");
                initializationFailed = true;
                return false;
            }

            var localPluginType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin");
            if (localPluginType != null)
            {
                workingPluginIdProperty = localPluginType.GetProperty("EffectiveWorkingPluginId");
            }

            log.Info("Reflection helper ready");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize reflection helper");
            initializationFailed = true;
            return false;
        }
    }

    public async Task<bool> SetPluginStateAsync(IExposedPlugin plugin, bool enabled)
    {
        if (!IsAvailable)
        {
            log.Warning("Reflection helper not available");
            return false;
        }

        try
        {
            var workingId = GetWorkingPluginId(plugin);
            if (workingId == Guid.Empty)
            {
                log.Warning($"Could not get plugin ID for {plugin.InternalName}");
                return false;
            }

            var task = (Task?)addOrUpdateMethod!.Invoke(defaultProfile, new object[]
            {
                workingId,
                plugin.InternalName,
                enabled,
                true
            });

            if (task != null)
            {
                await task;
            }

            log.Info($"Set persistent state: {plugin.InternalName} = {enabled}");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to set state for {plugin.InternalName}");
            return false;
        }
    }

    private Guid GetWorkingPluginId(IExposedPlugin plugin)
    {
        try
        {
            // Try to get the LocalPlugin from the wrapper
            var exposedPluginType = plugin.GetType();

            var localPluginField = exposedPluginType.GetField("localPlugin", BindingFlags.Instance | BindingFlags.NonPublic);
            if (localPluginField == null)
            {
                localPluginField = exposedPluginType.GetField("plugin", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            object? localPlugin = null;
            if (localPluginField != null)
            {
                localPlugin = localPluginField.GetValue(plugin);
            }
            else
            {
                var localPluginProp = exposedPluginType.GetProperty("LocalPlugin", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (localPluginProp != null)
                {
                    localPlugin = localPluginProp.GetValue(plugin);
                }
            }

            if (localPlugin != null && workingPluginIdProperty != null)
            {
                var id = workingPluginIdProperty.GetValue(localPlugin);
                if (id is Guid guid)
                {
                    return guid;
                }
            }

            // Fall back to searching in PluginManager
            return GetWorkingPluginIdFromProfileManager(plugin.InternalName);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get plugin ID for {plugin.InternalName}");
            return Guid.Empty;
        }
    }

    private Guid GetWorkingPluginIdFromProfileManager(string internalName)
    {
        try
        {
            if (profileManager == null || dalamudAssembly == null)
                return Guid.Empty;

            var pluginManagerType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.PluginManager");
            if (pluginManagerType == null)
                return Guid.Empty;

            var genericService = serviceType!.MakeGenericType(pluginManagerType);
            var getMethod = genericService.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getMethod == null)
                return Guid.Empty;

            var pluginManager = getMethod.Invoke(null, null);
            if (pluginManager == null)
                return Guid.Empty;

            var installedPluginsProp = pluginManagerType.GetProperty("InstalledPlugins", BindingFlags.Instance | BindingFlags.Public);
            if (installedPluginsProp == null)
                return Guid.Empty;

            var installedPlugins = installedPluginsProp.GetValue(pluginManager) as System.Collections.IEnumerable;
            if (installedPlugins == null)
                return Guid.Empty;

            foreach (var localPlugin in installedPlugins)
            {
                var manifestProp = localPlugin.GetType().GetProperty("Manifest");
                if (manifestProp == null) continue;

                var manifest = manifestProp.GetValue(localPlugin);
                if (manifest == null) continue;

                var internalNameProp = manifest.GetType().GetProperty("InternalName");
                if (internalNameProp == null) continue;

                var pluginInternalName = internalNameProp.GetValue(manifest) as string;
                if (pluginInternalName == internalName)
                {
                    var idProp = localPlugin.GetType().GetProperty("EffectiveWorkingPluginId");
                    if (idProp != null)
                    {
                        var id = idProp.GetValue(localPlugin);
                        if (id is Guid guid)
                        {
                            return guid;
                        }
                    }
                }
            }

            return Guid.Empty;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get plugin ID from PluginManager for {internalName}");
            return Guid.Empty;
        }
    }
}
