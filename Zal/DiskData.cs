using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zal
{
     class DiskDataClass
    {
      static public List<DiskInfoCrystal> getData(String filePath)
        {
List<DiskInfoCrystal> hardwareList = new List<DiskInfoCrystal>();
            DiskInfoCrystal currentHardware = null;

            foreach (string line in File.ReadLines(filePath))
            {
                if (line.StartsWith(" (0"))
                {
                    // New hardware entry found
                    if (currentHardware != null)
                    {
                        hardwareList.Add(currentHardware);


                    }

                    currentHardware = new DiskInfoCrystal();
                    currentHardware.info = new Dictionary<string, dynamic>();
                }
                else if (currentHardware != null)
                {
                    if (line.Contains("Model :"))
                    {
                        currentHardware.info.Add("model", line.Split(':')[1].Trim());
                    }
                    if (line.Contains("Buffer Size :") && line.Contains("Unknown") == false)
                    {
                        string hoursString = Regex.Match(line.Split(':')[1].Trim(), @"\d+").Value;
                        if (!string.IsNullOrEmpty(hoursString))
                        {
                            currentHardware.info.Add("bufferSize", int.Parse(hoursString));
                        }
                    }
                    if (line.Contains("Transfer Mode :"))
                    {
                        string text = line.Split(':')[1];
                        
                            currentHardware.info.Add("transferMode", text.Split("|"));
                        
                    }
                    if (line.Contains("Queue Depth :"))
                    {
                        currentHardware.info.Add("queueDepth", line.Split(':')[1].Trim());
                    }
                    if (line.Contains("# Of Sectors :"))
                    {
                        currentHardware.info.Add("sectors", line.Split(':')[1].Trim());
                    }
                    if (line.Contains("Power On Hours :"))
                    {
                        string hoursString = Regex.Match(line.Split(':')[1].Trim(), @"\d+").Value;
                        if (!string.IsNullOrEmpty(hoursString))
                        {
                            currentHardware.info.Add("powerOnHours", int.Parse(hoursString));
                        }
                    }
                    if (line.Contains("Power On Count :"))
                    {
                        string hoursString = Regex.Match(line.Split(':')[1].Trim(), @"\d+").Value;
                        if (!string.IsNullOrEmpty(hoursString))
                        {
                            currentHardware.info.Add("powerOnCount", int.Parse(hoursString));
                        }
                    }
                    if (line.Contains("Health Status :"))
                    {
                        string hoursString = Regex.Match(line.Split(':')[1].Trim(), @"\d+").Value;
                        if (!string.IsNullOrEmpty(hoursString))
                        {
                            currentHardware.info.Add("healthPercentage", int.Parse(hoursString));
                        }

                        Regex regex = new Regex("[a-zA-Z]+");
                        Match match = regex.Match(line.Split(":")[1].Trim());
                        if (match.Success)
                        {
                            currentHardware.info.Add("healthText", match.Value);
                        }
                    }
                    if (line.Contains("Features :"))
                    {
                        currentHardware.info.Add("features", line.Split(':')[1].Trim().Split(",").ToList());
                    }
                    ////////////////////
                    ///////////////////
                    else if (line.Contains("ID Cur Wor Thr RawValues(6) Attribute Name"))
                    {
                        currentHardware.smartAttributes = new List<SmartAttribute>();
                        continue; // Skip header line
                    }
                    else if (line == "" || line.Contains("--") || line.Contains("     +0") || line.Contains("        0") || line.Contains(": "))
                    {
                        continue;
                    }
                    else if (currentHardware.smartAttributes != null)
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var attributeName = string.Join(" ", parts, 5, parts.Length - 5);
                        if (attributeName.Contains("Temperature"))
                        {
                            continue;
                        }
                        long rawValue = 0;
                        try
                        {
                            rawValue = long.Parse(parts[4], System.Globalization.NumberStyles.HexNumber);
                        }
                        catch (Exception c) { 
                        Console.WriteLine(c.Message);
                        }
                        SmartAttribute smartAttribute = new SmartAttribute
                        {
                            Id = parts[0],
                            currentValue = int.Parse(parts[1].Replace("_", "")),
                            worstValue = int.Parse(parts[2].Replace("_", "")),
                            threshold = int.Parse(parts[3].Replace("_", "")),
                            rawValue= rawValue,
                        attributeName = attributeName
                        };
                        currentHardware.smartAttributes.Add(smartAttribute);
                    }
                }
            }

            // Add the last hardware entry if present
            if (currentHardware != null)
            {
                hardwareList.Add(currentHardware);
            }
            hardwareList = hardwareList.Where(x => x.info.ContainsKey("model")).ToList();

            // Now you have a list of hardware entries with all SMART attributes
            return hardwareList;
        }
    }
    internal class DiskInfoCrystal
    {
        public Dictionary<String, dynamic> info { get; set; }
        public List<SmartAttribute> smartAttributes { get; set; }
    }
    internal class SmartAttribute
    {
        public string Id { get; set; }
        public int currentValue { get; set; }
        public int worstValue { get; set; }
        public int threshold { get; set; }
        public long rawValue { get; set; }
        public string attributeName { get; set; }
    }
}
