using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PluginPresetManager;

/// <summary>
/// Helper class to access Dalamud's internal ProfileManager via reflection.
/// This allows persistent plugin enable/disable states that survive game restarts.
/// WARNING: This uses internal APIs and may break on Dalamud updates.
/// </summary>
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
            log.Info("Attempting to initialize Dalamud reflection helper...");

            // Get Dalamud assembly
            dalamudAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Dalamud");

            if (dalamudAssembly == null)
            {
                log.Warning("Could not find Dalamud assembly");
                initializationFailed = true;
                return false;
            }

            // Get Service<T> type
            serviceType = dalamudAssembly.GetType("Dalamud.Service`1");
            if (serviceType == null)
            {
                log.Warning("Could not find Service<T> type");
                initializationFailed = true;
                return false;
            }

            // Get ProfileManager type
            profileManagerType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Profiles.ProfileManager");
            if (profileManagerType == null)
            {
                log.Warning("Could not find ProfileManager type");
                initializationFailed = true;
                return false;
            }

            // Get Service<ProfileManager>.Get()
            var genericService = serviceType.MakeGenericType(profileManagerType);
            var getMethod = genericService.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getMethod == null)
            {
                log.Warning("Could not find Service<ProfileManager>.Get method");
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

            // Get DefaultProfile property
            var defaultProfileProp = profileManagerType.GetProperty("DefaultProfile");
            if (defaultProfileProp == null)
            {
                log.Warning("Could not find DefaultProfile property");
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

            // Get AddOrUpdateAsync method from Profile
            var profileType = defaultProfile.GetType();
            addOrUpdateMethod = profileType.GetMethod("AddOrUpdateAsync",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Guid), typeof(string), typeof(bool), typeof(bool) },
                null);

            if (addOrUpdateMethod == null)
            {
                log.Warning("Could not find AddOrUpdateAsync method");
                initializationFailed = true;
                return false;
            }

            // Get EffectiveWorkingPluginId property from LocalPlugin
            var localPluginType = dalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin");
            if (localPluginType != null)
            {
                workingPluginIdProperty = localPluginType.GetProperty("EffectiveWorkingPluginId");
            }

            log.Info("Dalamud reflection helper initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize Dalamud reflection helper");
            initializationFailed = true;
            return false;
        }
    }

    public async Task<bool> SetPluginStateAsync(IExposedPlugin plugin, bool enabled)
    {
        if (!IsAvailable)
        {
            log.Warning("Reflection helper not available, cannot set persistent state");
            return false;
        }

        try
        {
            // Get the working plugin ID via reflection
            var workingId = GetWorkingPluginId(plugin);
            if (workingId == Guid.Empty)
            {
                log.Warning($"Could not get working plugin ID for {plugin.InternalName}");
                return false;
            }

            // Call AddOrUpdateAsync
            var task = (Task?)addOrUpdateMethod!.Invoke(defaultProfile, new object[]
            {
                workingId,
                plugin.InternalName,
                enabled,
                true // apply immediately
            });

            if (task != null)
            {
                await task;
            }

            log.Info($"Set persistent state for {plugin.InternalName}: enabled={enabled}");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to set persistent state for {plugin.InternalName}");
            return false;
        }
    }

    private Guid GetWorkingPluginId(IExposedPlugin plugin)
    {
        try
        {
            // IExposedPlugin is actually an ExposedPlugin wrapper that has a reference to LocalPlugin
            // We need to get the LocalPlugin from the ExposedPlugin wrapper
            var exposedPluginType = plugin.GetType();

            // Try to find the LocalPlugin field/property in ExposedPlugin
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
                // Try property
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

            // Alternative: try to find it in the ProfileManager's plugin list
            // This is more reliable as we can match by InternalName
            return GetWorkingPluginIdFromProfileManager(plugin.InternalName);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get working plugin ID for {plugin.InternalName}");
            return Guid.Empty;
        }
    }

    private Guid GetWorkingPluginIdFromProfileManager(string internalName)
    {
        try
        {
            if (profileManager == null || dalamudAssembly == null)
                return Guid.Empty;

            // Get PluginManager service
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

            // Get InstalledPlugins from PluginManager
            var installedPluginsProp = pluginManagerType.GetProperty("InstalledPlugins", BindingFlags.Instance | BindingFlags.Public);
            if (installedPluginsProp == null)
                return Guid.Empty;

            var installedPlugins = installedPluginsProp.GetValue(pluginManager) as System.Collections.IEnumerable;
            if (installedPlugins == null)
                return Guid.Empty;

            // Find the plugin by InternalName
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
                    // Found it! Get the EffectiveWorkingPluginId
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
            log.Error(ex, $"Failed to get working plugin ID from PluginManager for {internalName}");
            return Guid.Empty;
        }
    }
}
