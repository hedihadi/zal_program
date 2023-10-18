﻿using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace Zal
{
    class ComputerData
    {
      public  ComputerData()
        {
            cpuInfo = getCpuInfo();
            computer.Open();
        }
        private Computer computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true,
            IsStorageEnabled = true
        };
        private CpuInfo cpuInfo;
       
        public Dictionary<String, dynamic> getComputerData()
        {

            computer.Accept(new UpdateVisitor());
            var data = new Dictionary<string, dynamic>();
            data["cpu"] = new Dictionary<string, dynamic>();
            data["gpu"] = new Dictionary<string, dynamic>();
            data["ram"] = new Dictionary<string, dynamic>();
            data["motherboard"] = new Dictionary<string, dynamic>();
            data["storages"] = new Dictionary<string, dynamic>();
            foreach (IHardware hardware in computer.Hardware)
            {
                Console.WriteLine("Hardware: {0}", hardware.Name);
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    data["cpu"]["info"] = cpuInfo;
                    data["cpu"]["name"] = hardware.Name;
                    data["cpu"]["powers"] = new Dictionary<string, dynamic>();
                    data["cpu"]["loads"] = new Dictionary<string, dynamic>();
                    data["cpu"]["voltages"] = new Dictionary<string, dynamic>();
                    data["cpu"]["clocks"] = new Dictionary<string, dynamic>();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Power)
                        {
                            if (sensor.Name.Contains("Package"))
                            {
                                data["cpu"]["power"] = sensor.Value;
                            }
                            else
                            {
                                data["cpu"]["powers"][sensor.Name] = sensor.Value;
                            }
                        }
                        else if (sensor.SensorType == SensorType.Load)
                        {
                            if (sensor.Name == "CPU Total")
                            {
                                data["cpu"]["load"] = sensor.Value;
                            }
                            else
                            {
                                data["cpu"]["loads"][sensor.Name] = sensor.Value;
                            }
                        }
                        else if (sensor.SensorType == SensorType.Voltage)
                        {
                            data["cpu"]["voltages"][sensor.Name] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Clock)
                        {
                            data["cpu"]["clocks"][sensor.Name] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("(Tctl/Tdie)"))
                        {
                            data["cpu"]["temperature"] = sensor.Value;
                        }
                        else
                        {
                            var foundSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("(Tctl/Tdie)"));
                            if (foundSensor == null)
                            {
                                foundSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Average"));
                            }
                            if (foundSensor == null)
                            {
                                foundSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core #1"));
                            }
                            if (foundSensor != null)
                            {
                                data["cpu"]["temperature"] = foundSensor.Value;
                            }

                        }
                    }
                }
                else if (hardware.HardwareType.ToString().ToLower().Contains("gpu"))
                {
                    data["gpu"]["name"] = hardware.Name;

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Factor)
                        {
                            data["gpu"]["fps"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Clock && sensor.Name == "GPU Core")
                        {
                            data["gpu"]["coreSpeed"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Clock && sensor.Name == "GPU Memory")
                        {
                            data["gpu"]["memorySpeed"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Control && sensor.Name == "GPU Fan")
                        {
                            data["gpu"]["fanSpeedPercentage"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                        {
                            data["gpu"]["corePercentage"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Power && sensor.Name == "GPU Package")
                        {
                            data["gpu"]["power"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.SmallData && sensor.Name == "D3D Dedicated Memory Used")
                        {
                            data["gpu"]["dedicatedMemoryUsed"] = sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Voltage)
                        {
                            data["gpu"]["voltage"] = sensor.Value;


                        }
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            data["gpu"]["temperature"] = sensor.Value;
                        }
                    }


                }
                else if (hardware.HardwareType == HardwareType.Memory)
                {
                    data["ram"]["pieces"] = GetRamPiecesInfo();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used")
                        {
                            data["ram"]["memoryUsed"] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available")
                        {
                            data["ram"]["memoryAvailable"] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory")
                        {
                            data["ram"]["memoryUsedPercentage"] = sensor.Value;
                        }
                    }
                }
                else if (hardware.HardwareType == HardwareType.Motherboard)
                {
                    data["motherboard"]["name"] = hardware.Name;
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            data["motherboard"]["temperature"] = sensor.Value;
                        }

                    }
                }
                else if (hardware.HardwareType == HardwareType.Storage)
                {
                    data["storages"][hardware.Name] = new Dictionary<string, dynamic>();
                    var diskNumber = int.Parse(hardware.Identifier.ToString().Substring(hardware.Identifier.ToString().Length - 1));
                    data["storages"][hardware.Name]["diskNumber"] = diskNumber;
                    var diskInfo = GetDiskInfo(diskNumber);
                    data["storages"][hardware.Name]["size"] = diskInfo.TotalSize;
                    data["storages"][hardware.Name]["freeSpace"] = diskInfo.FreeSpace;
                    data["storages"][hardware.Name]["partitions"] = new List<String>();
                    data["storages"][hardware.Name]["mediaType"] = GetDiskMediaType(diskNumber);
                    foreach (var disk in diskInfo.Partitions)
                    {
                        data["storages"][hardware.Name]["partitions"].Add(disk.DriveLetter);
                    }
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            data["storages"][hardware.Name]["temperature"] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Throughput && sensor.Name == "Read Rate")
                        {
                            data["storages"][hardware.Name]["readRate"] = sensor.Value;
                        }
                        else if (sensor.SensorType == SensorType.Throughput && sensor.Name == "Write Rate")
                        {
                            data["storages"][hardware.Name]["writeRate"] = sensor.Value;
                        }

                    }
                }
            }
            data["monitors"] = getMonitorData();
            data["battery"] = getBatteryData();
            return data;
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
                foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
        private List<Dictionary<string, dynamic>> getMonitorData()
        {
            var result = new List<Dictionary<string, dynamic>>();
            Screen[] screens = Screen.AllScreens;

            foreach (Screen screen in screens)
            {
                var data = new Dictionary<string, dynamic>();
                data["name"] = screen.DeviceName;
                data["height"] = screen.Bounds.Height;
                data["width"] = screen.Bounds.Width;
                data["primary"] = screen.Primary;
                data["bitsPerPixel"] = screen.BitsPerPixel;
                result.Add(data);
            }
            return result;
        }
        private Dictionary<String, dynamic> getDiskData()
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM MSFT_PhysicalDisk");
            string type = "";
            scope.Connect();
            searcher.Scope = scope;
            var managementObjectDisks = searcher.Get();

            var disks = new Dictionary<string, dynamic>();

            PerformanceCounterCategory diskCategory = new PerformanceCounterCategory("PhysicalDisk");

            string[] instances = diskCategory.GetInstanceNames();

            foreach (string instance in instances)
            {
                if (instance.Equals("_Total", StringComparison.OrdinalIgnoreCase))
                    continue;

                float totalReadRateBytes = 0;
                float totalWriteRateBytes = 0;
                using (PerformanceCounter readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instance))
                using (PerformanceCounter writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instance))
                {
                    totalReadRateBytes = readCounter.NextValue();
                    totalWriteRateBytes = writeCounter.NextValue();
                    System.Threading.Thread.Sleep(500);
                    totalReadRateBytes = readCounter.NextValue();
                    totalWriteRateBytes = writeCounter.NextValue();

                }

                //extract the drive letters with regex
                var regex = new Regex(@"[a-zA-Z]+");
                var partitions = regex.Matches(instance);

                foreach (var partition in partitions)
                {
                    DriveInfo drive = new DriveInfo(partition.ToString());
                    System.Diagnostics.Debug.WriteLine("");
                }
                //get disk name
                String diskName = "";
                foreach (ManagementObject disk in managementObjectDisks)
                {
                    string deviceId = disk["DeviceID"].ToString();
                    if (deviceId.Contains(instance[0]))
                    {
                        BigInteger size = BigInteger.Parse(disk["Size"].ToString());
                        diskName = disk["Model"].ToString();
                        disks[diskName] = new Dictionary<string, dynamic>();
                        var diskNumber = int.Parse(instance.Substring(0, 1));
                        ulong allocatedSize = GetFreeSpaceForDisk(diskNumber);
                        disks[diskName]["freeSpace"] = allocatedSize;
                        disks[diskName]["diskNumber"] = deviceId;
                        disks[diskName]["partitions"] = partitions.Cast<Match>().Select(m => m.Value).ToArray();
                        //convert bytes size to gigabytes
                        disks[diskName]["size"] = size;
                        string mediaType = disk["MediaType"].ToString();

                        switch (Convert.ToInt16(disk["MediaType"]))
                        {
                            case 1:
                                disks[diskName]["mediaType"] = "Unspecified";
                                break;

                            case 3:
                                disks[diskName]["mediaType"] = "HDD";
                                break;

                            case 4:
                                disks[diskName]["mediaType"] = "SSD";
                                break;

                            case 5:
                                disks[diskName]["mediaType"] = "SCM";
                                break;

                            default:
                                disks[diskName]["mediaType"] = "Unspecified";
                                break;
                        }

                    }
                }
                disks[diskName]["readRate"] = totalReadRateBytes;
                disks[diskName]["writeRate"] = totalWriteRateBytes;

            }
            return disks;
        }
        private Dictionary<String, dynamic> getBatteryData()
        {

            PowerStatus p = System.Windows.Forms.SystemInformation.PowerStatus;

            int life = (int)(p.BatteryLifePercent * 100);

            var data = new Dictionary<string, dynamic>();
            data["hasBattery"] = p.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
            data["life"] = life;
            data["isCharging"] = p.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            data["lifeRemaining"] = p.BatteryLifeRemaining;

            return data;

        }
        static private string GetDiskMediaType(int diskNumber)
        {
            try
            {
                string queryString = $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceID = {diskNumber}";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\Microsoft\\Windows\\Storage", queryString);
                ManagementObjectCollection queryCollection = searcher.Get();

                foreach (ManagementObject disk in queryCollection)
                {
                    int mediaTypeCode = Convert.ToInt32(disk["MediaType"]);
                    switch (mediaTypeCode)
                    {
                        case 3:
                            return "HDD";
                        case 4:
                            return "SSD";
                        default:
                            return "Unknown";
                    }
                }
            }
            catch (ManagementException ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return "Unknown";
        }

        static private string GetDriveLetter(ManagementObject partition)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
            {
                foreach (ManagementObject logicalDisk in searcher.Get())
                {
                    return logicalDisk["DeviceID"].ToString();
                }
            }
            return "";
        }
        static private ulong GetFreeSpaceForDisk(int diskNumber)
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            scope.Connect();
            searcher.Scope = scope;

            var managementObjectDisks = searcher.Get();

            foreach (ManagementObject disk in managementObjectDisks)
            {
                var index = Convert.ToInt32(disk["Index"]);
                if (index == diskNumber)
                {
                    ManagementObjectSearcher partitionSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{disk["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    ulong totalFreeSpace = 0;

                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        ManagementObjectSearcher logicalDiskSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                        foreach (ManagementObject logicalDisk in logicalDiskSearcher.Get())
                        {
                            totalFreeSpace += Convert.ToUInt64(logicalDisk["FreeSpace"]);
                        }
                    }

                    return totalFreeSpace;
                }
            }

            return 0;
        }
        static private DiskInfo GetDiskInfo(int diskNumber)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_DiskDrive WHERE Index = {diskNumber}"))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    DiskInfo diskInfo = new DiskInfo();
                    diskInfo.DiskNumber = Convert.ToInt32(disk["Index"]);
                    diskInfo.TotalSize = Convert.ToInt64(disk["Size"]);

                    foreach (ManagementObject partition in disk.GetRelated("Win32_DiskPartition"))
                    {
                        PartitionInfo partitionInfo = new PartitionInfo();
                        partitionInfo.DriveLetter = GetDriveLetter(partition);
                        partitionInfo.Size = Convert.ToInt64(partition["Size"]);
                        diskInfo.Partitions.Add(partitionInfo);
                    }

                    diskInfo.FreeSpace = GetFreeSpaceForDisk(diskNumber);

                    return diskInfo;
                }
            }
            return null;
        }
        static private List<Dictionary<string, dynamic>> GetRamPiecesInfo()
        {
            var data = new List<Dictionary<string, dynamic>>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                var ramData = new Dictionary<string, dynamic>();
                ramData["capacity"] = obj["Capacity"];
                ramData["manufacturer"] = obj["Manufacturer"];
                ramData["partNumber"] = obj["PartNumber"];
                ramData["speed"] = obj["Speed"];
                data.Add(ramData);
            }
            return data;
        }
        private CpuInfo getCpuInfo()
        {

            var cpu =
                        new ManagementObjectSearcher("select * from Win32_Processor")
                                    .Get()
                                    .Cast<ManagementObject>()
                                    .First();
            CpuInfo cpuInfo = new CpuInfo();
            cpuInfo.socket = (string)cpu["SocketDesignation"];
            cpuInfo.name = (string)cpu["Name"];
            cpuInfo.speed = (uint)cpu["MaxClockSpeed"];
            cpuInfo.busSpeed = (uint)cpu["ExtClock"];
            cpuInfo.l2Cache = (uint)cpu["L2CacheSize"] * (ulong)1024;
            cpuInfo.l3Cache = (uint)cpu["L3CacheSize"] * (ulong)1024;
            cpuInfo.cores = (uint)cpu["NumberOfCores"];
            cpuInfo.threads = (uint)cpu["NumberOfLogicalProcessors"];

            cpuInfo.name =
               cpuInfo.name
              .Replace("(TM)", "™")
              .Replace("(tm)", "™")
              .Replace("(R)", "®")
              .Replace("(r)", "®")
              .Replace("(C)", "©")
              .Replace("(c)", "©")
              .Replace("    ", " ")
              .Replace("  ", " ");
            return cpuInfo;
        }
    }
}

class DiskInfo
{
    public int DiskNumber { get; set; }
    public long TotalSize { get; set; }
    public ulong FreeSpace { get; set; }
    public List<PartitionInfo> Partitions { get; } = new List<PartitionInfo>();
}

class PartitionInfo
{
    public string DriveLetter { get; set; }
    public long Size { get; set; }
}
class RamInfo
{
    public string partNumber { get; set; }
    public long capacity { get; set; }
    public string speed { get; set; }
    public string manufacturer { get; set; }
    public string memoryType { get; set; }





}
class CpuInfo
{
    public string name { get; set; }
    public string socket { get; set; }
    public uint speed { get; set; }
    public uint busSpeed { get; set; }
    public ulong l2Cache { get; set; }
    public ulong l3Cache { get; set; }
    public uint cores { get; set; }
    public uint threads { get; set; }


}
