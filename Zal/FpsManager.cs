using MaterialDesignThemes.Wpf.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zal
{
    public class FpsManager
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private Task fpsTask;
        private Process _presentmonProcess;
        private bool shouldSendFpsData = false;
      private uint? currentFocusedProcessId;
        private List<String> fpsData = new();

        private Action<String> sendDataFunction;
        

      public FpsManager(Action<String> sendDataFunction)
        {
            this.sendDataFunction = sendDataFunction;

            //start presentmon
            startPresentmon();

            //run function to update current focused screen every 3 seconds
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += async (sender, e) =>
            {
                await Task.Run(() =>
                {
                    currentFocusedProcessId=GetForegroundProcessName();
                });
            };
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(3000);
            dispatcherTimer.Start();
            
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
                    if (shouldSendFpsData == true && currentFocusedProcessId!=null)
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
                        System.Diagnostics.Debug.WriteLine($"c: {currentFocusedProcessId}, p: {processId}");
                        if (processId!=null && currentFocusedProcessId != null && currentFocusedProcessId== processId)
                        {
                            var time = getTimestamp();
                            Dictionary<String, String> data = new Dictionary<String, String>();
                            if (msBetweenPresents.Any(char.IsDigit))
                            {
                                data[processName] = msBetweenPresents;
                                //data[msBetweenPresents] = time;
                                //data["process"] = line.Split(",")[0];
                                fpsData.Add(JsonConvert.SerializeObject(data));

                                if (fpsData.Count > 30)
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
        private static String getTimestamp()
        {

            return (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()).ToString();
        }
        public void clear()
        {
            fpsData.Clear();

        }
        private uint? GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();

            // The foreground window can be NULL in certain circumstances, 
            // such as when a window is losing activation.
            if (hwnd == null)
                return null;

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            System.Diagnostics.Debug.WriteLine(pid.ToString()); 
            return pid;
          
        }
   
        public void setShouldSendFpsData(bool shouldSendFpsData)
        {
            clear();
            this.shouldSendFpsData=shouldSendFpsData;
        }
    }
}
