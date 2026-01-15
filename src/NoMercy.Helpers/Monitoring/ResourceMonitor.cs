using LibreHardwareMonitor.Hardware;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Helpers.Monitoring;

public class ResourceMonitor
{
    private static readonly Computer? Computer;

    static ResourceMonitor()
    {
        Logger.App("Initializing Resource Monitor");
        if (Computer is not null) return;
        Logger.App("Creating new computer instance");
        Computer = CreateComputer();
    }

    private static Computer CreateComputer()
    {
        Computer computer = new()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsStorageEnabled = false
        };

        computer.Open();
        return computer;
    }

    private class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware? subHardware in hardware.SubHardware) subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }

    public static Resource Monitor()
    {
        if (Computer is null) return new();
        Resource resource = new()
        {
            Cpu = new()
            {
                Core = []
            },
            _gpu = new(),
            Memory = new()
        };

        try
        {
            Computer.Accept(new UpdateVisitor());

            foreach (IHardware? hardware in Computer.Hardware)
            {
                if (hardware.HardwareType is not HardwareType.Cpu and not HardwareType.Memory
                    and not HardwareType.GpuIntel and not HardwareType.GpuNvidia and not HardwareType.GpuAmd
                   ) continue;

                try
                {
                    foreach (ISensor? sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType is not SensorType.Load && sensor.SensorType is not SensorType.Data)
                            continue;

                        // Logger.App($"Type: {sensor.Hardware}, Identifier: {sensor.Hardware.Identifier}, Sensor: {sensor.Name}, value: {sensor.Value}");
                        HardwareSensorType(sensor, resource);
                    }
                }
                catch
                {
                    throw new("Error while monitoring hardware");
                }
            }
        }
        catch (Exception)
        {
            //
        }

        return resource;
    }

    private static void HardwareSensorType(ISensor sensor, Resource resource)
    {
        switch (sensor.Hardware.HardwareType)
        {
            case HardwareType.Cpu:
                CpuSensor(sensor, resource);
                break;
            case HardwareType.Memory:
                MemorySensor(sensor, resource);
                break;
            case HardwareType.GpuNvidia:
                KeyValuePair<Identifier, Gpu> nvidiaGpu = resource._gpu
                    .FirstOrDefault(g => g.Key == sensor.Hardware.Identifier);

                NvidiaSensor(sensor, resource, nvidiaGpu);
                break;
            case HardwareType.GpuIntel:
                KeyValuePair<Identifier, Gpu> intelGpu = resource._gpu
                    .FirstOrDefault(g => g.Key == sensor.Hardware.Identifier);
                IntelGpuSensor(sensor, resource, intelGpu);
                break;
            case HardwareType.GpuAmd:
                KeyValuePair<Identifier, Gpu> amdGpu = resource._gpu
                    .FirstOrDefault(g => g.Key == sensor.Hardware.Identifier);
                AmdGpuSensor(sensor, resource, amdGpu);
                break;
            case HardwareType.Motherboard:
                break;
            case HardwareType.SuperIO:
                break;
            case HardwareType.Storage:
                break;
            case HardwareType.Network:
                break;
            case HardwareType.Cooler:
                break;
            case HardwareType.EmbeddedController:
                break;
            case HardwareType.Psu:
                break;
            case HardwareType.Battery:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void CpuSensor(ISensor sensor, Resource resource)
    {
        switch (sensor.Name)
        {
            case "CPU Total":
                resource.Cpu.Total = sensor.Value ?? 0.0;
                break;
            case "CPU Core Max":
                resource.Cpu.Max = sensor.Value ?? 0.0;
                break;
            default:
                resource.Cpu.Core.Add(new()
                {
                    Index = sensor.Index - 1,
                    Utilization = sensor.Value ?? 0.0
                });
                break;
        }
    }

    private static void MemorySensor(ISensor sensor, Resource resource)
    {
        switch (sensor.Name)
        {
            case "Memory Available":
                resource.Memory.Available = sensor.Value ?? 0.0;
                break;
            case "Memory Used":
                resource.Memory.Use = sensor.Value ?? 0.0;
                break;
            // case "Memory":
            case "Virtual Memory":
                resource.Memory.Total = sensor.Value ?? 0.0;
                break;
        }
    }

    private static void NvidiaSensor(ISensor sensor, Resource resource, KeyValuePair<Identifier, Gpu> gpu)
    {
        switch (sensor.Name)
        {
            case "GPU Video Engine":
                if (gpu.Value is not null)
                    gpu.Value.Core = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        Core = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
            case "D3D Video Decode":
                if (gpu.Value is not null)
                    gpu.Value.Decode = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        Decode = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
            case "D3D Video Encode":
                if (gpu.Value is not null)
                    gpu.Value.Encode = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        Encode = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
            case "D3D 3D":
                if (gpu.Value is not null)
                    gpu.Value.D3D = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        D3D = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
            case "GPU Memory":
                if (gpu.Value is not null)
                    gpu.Value.Memory = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        Memory = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
            case "GPU Power":
                if (gpu.Value is not null)
                    gpu.Value.Power = sensor.Value ?? 0;
                else
                    resource._gpu[sensor.Hardware.Identifier] = new()
                    {
                        Power = sensor.Value ?? 0,
                        Identifier = sensor.Hardware.Identifier
                    };
                break;
        }
    }
    
    private static void AmdGpuSensor(ISensor sensor, Resource resource, KeyValuePair<Identifier, Gpu> gpu)
    {
        
    }

    private static void IntelGpuSensor(ISensor sensor, Resource resource, KeyValuePair<Identifier, Gpu> gpu)
    {
        
    }

    public static void Start()
    {
        Computer?.Open();
    }

    public static void Stop()
    {
        Computer?.Close();
    }

    ~ResourceMonitor()
    {
        Computer?.Close();
    }
}