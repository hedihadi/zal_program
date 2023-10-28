using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zal.HelperFunctions;
namespace Zal
{
    public class FpsManager
    {
        private Task fpsTask;
        private Process _presentmonProcess;
        private bool shouldSendFpsData = false;
        private IList<int>? currentFocusedProcessId;
        private List<String> fpsData = new();
        private Action<string> sendDataFunction;
        private bool isDisposed = false;
        public FpsManager(Action<String> sendDataFunction)
        {
            this.sendDataFunction = sendDataFunction;
            //start presentmon
            startPresentmon();
            //run function to update current focused screen every 1 seconds
            Task.Run(() =>
            {

                while (true)
                {
                    var oldProcessId = currentFocusedProcessId;
                    currentFocusedProcessId = (new FocusedWindowGetter()).getFocusedWindowProcessId();

                 
                    Thread.Sleep(2000);
                }
            });

            //this function makes sure that presentmon is running. if not, it will attempt to run it
             Task.Run(() =>
            {
                while (true)
                {
                    if (isDisposed) break;
                    var process = Process.GetProcessesByName("presentmon");
                    if (process.Length == 0)
                    {
                        startPresentmon();
                    }
                    Thread.Sleep(1000);
                }
            });

        }
        private void startPresentmon()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "presentmon.exe");
            try
            {
                File.WriteAllBytes(path, Zal.Resources.presentmon);
            }
            catch (Exception ex) { }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = path, // Replace with the actual path
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "-output_stdout -stop_existing_session",
            };
            _presentmonProcess = new Process { StartInfo = startInfo };

            _presentmonProcess.Start();

            var fpsAction = (object obj) =>
            {
                StreamReader reader = _presentmonProcess.StandardOutput;

                while (!reader.EndOfStream)
                {
                    if (isDisposed) break;
                    Thread.Sleep(30);
                    if (currentFocusedProcessId!=null)
                    {
                        string line = reader.ReadLine();
                        var msBetweenPresents = "";
                        try
                        {
                            msBetweenPresents = line.Split(",")[9];
                        }
                        catch
                        {
                            continue;
                        }
                        
                        uint? processId = null;
                        String? processName = line.Split(",")[0];
                        try
                        {
                            processId = uint.Parse(line.Split(",")[1]);
                        }
                        catch {
                            
                        }
                        System.Diagnostics.Debug.WriteLine($"c: {string.Join(",", currentFocusedProcessId.ToArray())}, p: {processId} - {processName}");
                        if (processId!=null && currentFocusedProcessId != null && currentFocusedProcessId.Contains(((int)processId)))
                        {
                          
                            var time = getTimestamp();
                            Dictionary<String, String> data = new Dictionary<String, String>();
                            if (msBetweenPresents.Any(char.IsDigit))
                            {
                                data[processName] = msBetweenPresents;
                                //data[msBetweenPresents] = time;
                                //data["process"] = line.Split(",")[0];
                                fpsData.Add(JsonConvert.SerializeObject(data));

                                if (fpsData.Count > 10)
                                {
                                    var jsonString = JsonConvert.SerializeObject(fpsData);
                                    sendDataFunction.Invoke(jsonString);
                                    fpsData.Clear();
                                    
                                }
                            }
                        }






                    }




                }

            };
            fpsTask = new Task(fpsAction, "fpsTask");
            fpsTask.Start();
            
        }
        public void Dispose()
        {
            isDisposed = true;
            _presentmonProcess.Kill();
            _presentmonProcess.Dispose();
            
        }
        private static String getTimestamp()
        {

            return (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()).ToString();
        }
        public void clear()
        {
            fpsData.Clear();

        }
        public void setShouldSendFpsData(bool shouldSendFpsData)
        {
            clear();
            this.shouldSendFpsData=shouldSendFpsData;
        }
    }
}
