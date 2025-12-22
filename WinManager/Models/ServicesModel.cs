using Microsoft.Win32;
using System.Diagnostics;
using System.Management;

// dotnet add package System.ServiceProcess.ServiceController
using System.ServiceProcess;

namespace WinManager.Models
{
    public class ServicesModel : IDisposable
    {
        private List<ServiceInfo> _services;
        private readonly object _lock = new object();

        public ServicesModel()
        {
            _services = new List<ServiceInfo>();
            LoadServices();
        }

        // Load all services including hidden ones
        public void LoadServices()
        {
            lock (_lock)
            {
                _services.Clear();

                try
                {
                    // Use WMI - removed LoadOrderGroup from query
                    var query = "SELECT Name, DisplayName, Description, State, StartMode, ProcessId, PathName, StartName, ServiceType FROM Win32_Service";

                    using (var searcher = new ManagementObjectSearcher(query))
                    {
                        // Set timeout to avoid hanging
                        searcher.Options.Timeout = TimeSpan.FromSeconds(10);

                        foreach (ManagementObject service in searcher.Get())
                        {
                            try
                            {
                                var serviceInfo = new ServiceInfo
                                {
                                    Name = service["Name"]?.ToString() ?? "",
                                    DisplayName = service["DisplayName"]?.ToString() ?? "",
                                    Description = service["Description"]?.ToString() ?? "",
                                    Status = ParseServiceStatus(service["State"]?.ToString()),
                                    StartMode = ParseStartMode(service["StartMode"]?.ToString()),
                                    ProcessId = Convert.ToInt32(service["ProcessId"] ?? 0),
                                    PathName = service["PathName"]?.ToString() ?? "",
                                    StartName = service["StartName"]?.ToString() ?? "",
                                    ServiceType = service["ServiceType"]?.ToString() ?? "",
                                    IsEnriched = true
                                };

                                // Get LoadOrderGroup from Registry
                                serviceInfo.LoadOrderGroup = GetLoadOrderGroupFromRegistry(serviceInfo.Name);

                                _services.Add(serviceInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error parsing service: {ex.Message}");
                            }
                        }
                    }

                    Debug.WriteLine($"Loaded {_services.Count} services");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading services with WMI: {ex.Message}");

                    // Fallback to ServiceController if WMI fails
                    LoadServicesFromServiceController();
                }
            }
        }

        private void LoadServicesFromServiceController()
        {
            try
            {
                Debug.WriteLine("Using ServiceController fallback...");

                var serviceControllers = ServiceController.GetServices();

                foreach (var sc in serviceControllers)
                {
                    try
                    {
                        var serviceInfo = new ServiceInfo
                        {
                            Name = sc.ServiceName,
                            DisplayName = sc.DisplayName,
                            Status = ConvertStatus(sc.Status),
                            StartMode = GetStartMode(sc.ServiceName),
                            LoadOrderGroup = GetLoadOrderGroupFromRegistry(sc.ServiceName),
                            IsEnriched = false // Basic info only
                        };

                        _services.Add(serviceInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading service {sc.ServiceName}: {ex.Message}");
                    }
                    finally
                    {
                        sc.Dispose();
                    }
                }

                Debug.WriteLine($"Fallback loaded {_services.Count} services");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ServiceController fallback failed: {ex.Message}");
            }
        }

        // Get LoadOrderGroup from Registry
        private string GetLoadOrderGroupFromRegistry(string serviceName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    return key?.GetValue("Group")?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading LoadOrderGroup for {serviceName}: {ex.Message}");
                return "";
            }
        }

        private ServiceStatus ConvertStatus(System.ServiceProcess.ServiceControllerStatus status)
        {
            return status switch
            {
                System.ServiceProcess.ServiceControllerStatus.Running => ServiceStatus.Running,
                System.ServiceProcess.ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                System.ServiceProcess.ServiceControllerStatus.Paused => ServiceStatus.Paused,
                System.ServiceProcess.ServiceControllerStatus.StartPending => ServiceStatus.StartPending,
                System.ServiceProcess.ServiceControllerStatus.StopPending => ServiceStatus.StopPending,
                System.ServiceProcess.ServiceControllerStatus.ContinuePending => ServiceStatus.ContinuePending,
                System.ServiceProcess.ServiceControllerStatus.PausePending => ServiceStatus.PausePending,
                _ => ServiceStatus.Unknown
            };
        }

        private ServiceStartMode GetStartMode(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    return sc.StartType switch
                    {
                        System.ServiceProcess.ServiceStartMode.Automatic => ServiceStartMode.Automatic,
                        System.ServiceProcess.ServiceStartMode.Manual => ServiceStartMode.Manual,
                        System.ServiceProcess.ServiceStartMode.Disabled => ServiceStartMode.Disabled,
                        System.ServiceProcess.ServiceStartMode.Boot => ServiceStartMode.Boot,
                        System.ServiceProcess.ServiceStartMode.System => ServiceStartMode.System,
                        _ => ServiceStartMode.Unknown
                    };
                }
            }
            catch
            {
                return ServiceStartMode.Unknown;
            }
        }

        private void EnrichServiceInfoFromWMI(ServiceInfo serviceInfo)
        {
            try
            {
                // Optimized query - removed LoadOrderGroup
                var escapedName = serviceInfo.Name.Replace("\\", "\\\\").Replace("'", "''");
                var query = $"SELECT Description, ProcessId, PathName, StartName, ServiceType FROM Win32_Service WHERE Name = '{escapedName}'";

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    searcher.Options.Timeout = TimeSpan.FromMilliseconds(500);

                    foreach (ManagementObject service in searcher.Get())
                    {
                        serviceInfo.Description = service["Description"]?.ToString() ?? "";
                        serviceInfo.ProcessId = Convert.ToInt32(service["ProcessId"] ?? 0);
                        serviceInfo.PathName = service["PathName"]?.ToString() ?? "";
                        serviceInfo.StartName = service["StartName"]?.ToString() ?? "";
                        serviceInfo.ServiceType = service["ServiceType"]?.ToString() ?? "";
                        serviceInfo.LoadOrderGroup = GetLoadOrderGroupFromRegistry(serviceInfo.Name);
                        serviceInfo.IsEnriched = true;
                        break;
                    }
                }
            }
            catch (ManagementException)
            {
                // Silently ignore WMI errors
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error enriching service {serviceInfo.Name}: {ex.GetType().Name}");
            }
        }

        // Enrich a single service with detailed info from WMI (call on-demand)
        public void EnrichServiceInfo(ServiceInfo serviceInfo)
        {
            if (serviceInfo == null || serviceInfo.IsEnriched) return;
            EnrichServiceInfoFromWMI(serviceInfo);
        }

        // Enrich service by name
        public void EnrichServiceInfo(string serviceName)
        {
            var service = GetService(serviceName);
            if (service != null)
            {
                EnrichServiceInfo(service);
            }
        }

        // Enrich all services (slow - use sparingly)
        public void EnrichAllServices()
        {
            lock (_lock)
            {
                foreach (var service in _services)
                {
                    if (!service.IsEnriched)
                    {
                        EnrichServiceInfoFromWMI(service);
                    }
                }
            }
            Debug.WriteLine($"Enriched all services with detailed information");
        }

        private ServiceStatus ParseServiceStatus(string status)
        {
            return status?.ToLower() switch
            {
                "running" => ServiceStatus.Running,
                "stopped" => ServiceStatus.Stopped,
                "paused" => ServiceStatus.Paused,
                "start pending" => ServiceStatus.StartPending,
                "stop pending" => ServiceStatus.StopPending,
                "continue pending" => ServiceStatus.ContinuePending,
                "pause pending" => ServiceStatus.PausePending,
                _ => ServiceStatus.Unknown
            };
        }

        private ServiceStartMode ParseStartMode(string startMode)
        {
            return startMode?.ToLower() switch
            {
                "auto" => ServiceStartMode.Automatic,
                "manual" => ServiceStartMode.Manual,
                "disabled" => ServiceStartMode.Disabled,
                "boot" => ServiceStartMode.Boot,
                "system" => ServiceStartMode.System,
                _ => ServiceStartMode.Unknown
            };
        }

        // Get all services
        public List<ServiceInfo> GetAllServices()
        {
            lock (_lock)
            {
                return new List<ServiceInfo>(_services);
            }
        }

        // Get service by name
        public ServiceInfo GetService(string serviceName)
        {
            lock (_lock)
            {
                return _services.FirstOrDefault(s =>
                    s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Get services by status
        public List<ServiceInfo> GetServicesByStatus(ServiceStatus status)
        {
            lock (_lock)
            {
                return _services.Where(s => s.Status == status).ToList();
            }
        }

        // Get services by start mode
        public List<ServiceInfo> GetServicesByStartMode(ServiceStartMode startMode)
        {
            lock (_lock)
            {
                return _services.Where(s => s.StartMode == startMode).ToList();
            }
        }

        // Get running services
        public List<ServiceInfo> GetRunningServices()
        {
            return GetServicesByStatus(ServiceStatus.Running);
        }

        // Get stopped services
        public List<ServiceInfo> GetStoppedServices()
        {
            return GetServicesByStatus(ServiceStatus.Stopped);
        }

        // Search services
        public List<ServiceInfo> SearchServices(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllServices();

            lock (_lock)
            {
                query = query.ToLower();
                return _services.Where(s =>
                    s.Name.ToLower().Contains(query) ||
                    s.DisplayName.ToLower().Contains(query) ||
                    s.Description.ToLower().Contains(query)
                ).ToList();
            }
        }

        // Start a service
        public bool StartService(string serviceName, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        errorMessage = "Service is already running";
                        return true;
                    }

                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.StartPending)
                    {
                        errorMessage = "Service is already starting";
                        return true;
                    }

                    service.Start();
                    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                    // Update service info
                    RefreshService(serviceName);

                    Debug.WriteLine($"Service '{serviceName}' started successfully");
                    return true;
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                errorMessage = "Service start timed out (30 seconds)";
                Debug.WriteLine($"Timeout starting service '{serviceName}'");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = $"Cannot start service: {ex.Message}";
                Debug.WriteLine($"Error starting service '{serviceName}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Error starting service '{serviceName}': {ex.Message}");
                return false;
            }
        }

        // Stop a service
        public bool StopService(string serviceName, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                    {
                        errorMessage = "Service is already stopped";
                        return true;
                    }

                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.StopPending)
                    {
                        errorMessage = "Service is already stopping";
                        return true;
                    }

                    if (!service.CanStop)
                    {
                        errorMessage = "Service cannot be stopped";
                        return false;
                    }

                    service.Stop();
                    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

                    // Update service info
                    RefreshService(serviceName);

                    Debug.WriteLine($"Service '{serviceName}' stopped successfully");
                    return true;
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                errorMessage = "Service stop timed out (30 seconds)";
                Debug.WriteLine($"Timeout stopping service '{serviceName}'");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = $"Cannot stop service: {ex.Message}";
                Debug.WriteLine($"Error stopping service '{serviceName}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Error stopping service '{serviceName}': {ex.Message}");
                return false;
            }
        }

        // Restart a service
        public bool RestartService(string serviceName, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    var timeout = TimeSpan.FromSeconds(30);

                    // Stop if running
                    if (service.Status != System.ServiceProcess.ServiceControllerStatus.Stopped)
                    {
                        if (service.CanStop)
                        {
                            service.Stop();
                            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, timeout);
                        }
                        else
                        {
                            errorMessage = "Service cannot be stopped for restart";
                            return false;
                        }
                    }

                    // Start
                    service.Start();
                    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, timeout);

                    // Update service info
                    RefreshService(serviceName);

                    Debug.WriteLine($"Service '{serviceName}' restarted successfully");
                    return true;
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                errorMessage = "Service restart timed out (30 seconds)";
                Debug.WriteLine($"Timeout restarting service '{serviceName}'");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Error restarting service '{serviceName}': {ex.Message}");
                return false;
            }
        }

        // Pause a service
        public bool PauseService(string serviceName, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status == System.ServiceProcess.ServiceControllerStatus.Paused)
                    {
                        errorMessage = "Service is already paused";
                        return true;
                    }

                    if (!service.CanPauseAndContinue)
                    {
                        errorMessage = "Service cannot be paused";
                        return false;
                    }

                    service.Pause();
                    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Paused, TimeSpan.FromSeconds(30));

                    RefreshService(serviceName);
                    Debug.WriteLine($"Service '{serviceName}' paused successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Error pausing service '{serviceName}': {ex.Message}");
                return false;
            }
        }

        // Resume/Continue a service
        public bool ContinueService(string serviceName, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status != System.ServiceProcess.ServiceControllerStatus.Paused)
                    {
                        errorMessage = "Service is not paused";
                        return false;
                    }

                    service.Continue();
                    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                    RefreshService(serviceName);
                    Debug.WriteLine($"Service '{serviceName}' continued successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"Error continuing service '{serviceName}': {ex.Message}");
                return false;
            }
        }

        // Refresh single service info
        public void RefreshService(string serviceName)
        {
            try
            {
                lock (_lock)
                {
                    var existingService = _services.FirstOrDefault(s =>
                        s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                    if (existingService != null)
                    {
                        // Optimized query - only select status and PID
                        var escapedName = serviceName.Replace("\\", "\\\\").Replace("'", "''");
                        var query = $"SELECT State, ProcessId FROM Win32_Service WHERE Name = '{escapedName}'";

                        using (var searcher = new ManagementObjectSearcher(query))
                        {
                            searcher.Options.Timeout = TimeSpan.FromMilliseconds(500);

                            foreach (ManagementObject service in searcher.Get())
                            {
                                existingService.Status = ParseServiceStatus(service["State"]?.ToString());
                                existingService.ProcessId = Convert.ToInt32(service["ProcessId"] ?? 0);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing service '{serviceName}': {ex.Message}");
            }
        }

        // Refresh all services
        public void RefreshAllServices()
        {
            LoadServices();
        }

        // Get service statistics
        public ServiceStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new ServiceStatistics
                {
                    TotalServices = _services.Count,
                    RunningServices = _services.Count(s => s.Status == ServiceStatus.Running),
                    StoppedServices = _services.Count(s => s.Status == ServiceStatus.Stopped),
                    PausedServices = _services.Count(s => s.Status == ServiceStatus.Paused),
                    AutomaticServices = _services.Count(s => s.StartMode == ServiceStartMode.Automatic),
                    ManualServices = _services.Count(s => s.StartMode == ServiceStartMode.Manual),
                    DisabledServices = _services.Count(s => s.StartMode == ServiceStartMode.Disabled)
                };
            }
        }

        // Print all services info
        public void PrintServicesInfo()
        {
            var stats = GetStatistics();

            Debug.WriteLine("=== Services Information ===");
            Debug.WriteLine($"Total Services: {stats.TotalServices}");
            Debug.WriteLine($"Running: {stats.RunningServices}");
            Debug.WriteLine($"Stopped: {stats.StoppedServices}");
            Debug.WriteLine($"Paused: {stats.PausedServices}");
            Debug.WriteLine($"\nStart Mode:");
            Debug.WriteLine($"  Automatic: {stats.AutomaticServices}");
            Debug.WriteLine($"  Manual: {stats.ManualServices}");
            Debug.WriteLine($"  Disabled: {stats.DisabledServices}");
            Debug.WriteLine("\n=== Sample Services ===");

            var sampleServices = _services.Take(10);
            foreach (var service in sampleServices)
            {
                Debug.WriteLine($"\n{service.DisplayName} ({service.Name})");
                Debug.WriteLine($"  Status: {service.Status}");
                Debug.WriteLine($"  Start Mode: {service.StartMode}");
                Debug.WriteLine($"  PID: {service.ProcessId}");
                Debug.WriteLine($"  Group: {service.LoadOrderGroup}");
                Debug.WriteLine($"  Description: {service.Description}");
            }
            Debug.WriteLine("============================");
        }

        // Cleanup
        public void Dispose()
        {
            _services?.Clear();
        }
    }

    // Service status enumeration
    public enum ServiceStatus
    {
        Unknown,
        Running,
        Stopped,
        Paused,
        StartPending,
        StopPending,
        ContinuePending,
        PausePending
    }

    // Service start mode enumeration
    public enum ServiceStartMode
    {
        Unknown,
        Automatic,
        Manual,
        Disabled,
        Boot,
        System
    }

    // Service information class
    public class ServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ServiceStatus Status { get; set; }
        public ServiceStartMode StartMode { get; set; }
        public int ProcessId { get; set; }
        public string PathName { get; set; } = string.Empty;
        public string StartName { get; set; } = string.Empty; // Account running the service
        public string ServiceType { get; set; } = string.Empty;
        public string LoadOrderGroup { get; set; } = string.Empty; // Group from Registry

        // Additional properties
        public bool DesktopInteract { get; set; }

        public string ErrorControl { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();

        // Track if detailed info has been loaded
        public bool IsEnriched { get; set; } = false;

        // Helper properties
        public string StatusDisplay => Status switch
        {
            ServiceStatus.Running => "Running",
            ServiceStatus.Stopped => "Stopped",
            ServiceStatus.Paused => "Paused",
            ServiceStatus.StartPending => "Starting...",
            ServiceStatus.StopPending => "Stopping...",
            ServiceStatus.ContinuePending => "Continuing...",
            ServiceStatus.PausePending => "Pausing...",
            _ => "Unknown"
        };

        public string StartModeDisplay => StartMode switch
        {
            ServiceStartMode.Automatic => "Automatic",
            ServiceStartMode.Manual => "Manual",
            ServiceStartMode.Disabled => "Disabled",
            ServiceStartMode.Boot => "Boot",
            ServiceStartMode.System => "System",
            _ => "Unknown"
        };

        public bool IsRunning => Status == ServiceStatus.Running;
        public bool IsStopped => Status == ServiceStatus.Stopped;
        public bool IsPaused => Status == ServiceStatus.Paused;
    }

    // Service statistics class
    public class ServiceStatistics
    {
        public int TotalServices { get; set; }
        public int RunningServices { get; set; }
        public int StoppedServices { get; set; }
        public int PausedServices { get; set; }
        public int AutomaticServices { get; set; }
        public int ManualServices { get; set; }
        public int DisabledServices { get; set; }
    }
}