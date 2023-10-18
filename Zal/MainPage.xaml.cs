using Firebase.Auth;
using Firebase.Auth.UI;
using H.Socket.IO;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using Path = System.IO.Path;
using Zal;
using System.Security.Principal;

namespace Zal
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    { 
        ComputerData computerData=new ComputerData();
        public SocketIoClient socketio = new SocketIoClient();
        public bool shouldSendFpsData = false;
        List<String> fpsData = new List<string>();
        bool isConnectedToServer = false;
        private Process _presentmonProcess;
        private Process _taskmanagerProcess;
        public bool areThereClientListeners = false;
        HashSet<String> presentmonProcessNames = new HashSet<String>();
        String? presentmonChosenProcessName = null;
        List<DiskInfoCrystal> diskInfos;

        //this variable is used to save computerdata,
        //we use it to immediately send a packet of data
        //when the user connects. to reduce waiting time on the app.
        Dictionary<String,dynamic>? data=null;
        Task fpsTask;

        public static String GetTimestamp()
        {

            return (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()).ToString();
        }
        public MainPage()
        {
            InitializeComponent();
            FirebaseUI.Instance.Client.AuthStateChanged += this.AuthStateChanged;
            socketConnectionChanged(false,isConnecting:true);
            checkForUpdates();
            updateCheckBoxes();
            startTaskmanager();
            setStartup();
            var user = FirebaseUI.Instance.Client.User;
            userName.Text = $"Welcome, {user.Info.DisplayName}.";

            socketio.On("room_clients", data =>
            {
                List<int> parsedData = JsonConvert.DeserializeObject<List<int>>(data);
                // if the data is 1, that means the client type is 1, which means this client is a phone
                if (parsedData.Contains(1)) 
                {
                    areThereClientListeners = true;
                    if (this.data != null)
                    {
                        var jsonString = JsonConvert.SerializeObject(this.data);
                        sendSocketData("pc_data", jsonString);
                    }
                    
                    addStringToListbox("mobile joined");
                    //send diskinfo
                    sendDiskData();
                   

                }
                else
                {
                   
                    areThereClientListeners = false;
                    addStringToListbox("mobile left");
                    presentmonProcessNames.Clear();
                    presentmonChosenProcessName = null;
                    shouldSendFpsData = false;
                    fpsData.Clear();

                }
            });
            socketio.On("kill_process", data =>
            {
                List<int> parsedData = JsonConvert.DeserializeObject<List<int>>(data);
                // if the data is 1, that means the client type is 1, which means this client is a phone
                System.Diagnostics.Debug.Write(parsedData);
                foreach(var pid in parsedData)
                {
                    try
                    {
                        Process proc = Process.GetProcessById(pid);
                        if (!proc.HasExited) proc.Kill();
                    }
                    catch (ArgumentException)
                    {
                        // Process already exited.
                    }
                }
            });
            socketio.On("restart_admin", () =>
            {
                string selfPath = Process.GetCurrentProcess().MainModule.FileName;
               
                var proc = new Process
                {
                    StartInfo =
        {
            FileName = selfPath,
            UseShellExecute = true,
            Verb = "runas"
        }
                };

                proc.Start();
                this.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();

                });


            });
            socketio.On("start_fps", data =>
            {
                var fpsAction = (object obj) =>
                {
                    StreamReader reader = _presentmonProcess.StandardOutput;

                    while (!reader.EndOfStream)
                    {
                        if (shouldSendFpsData == true && areThereClientListeners)
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
                            var processName = line.Split(",")[0];
                            if (presentmonProcessNames.Contains(processName) == false)
                            {
                                List<String> ignoredProcesses = new List<string>();
                                ignoredProcesses.Add("dwm.exe");
                                ignoredProcesses.Add("Application");
                                ignoredProcesses.Add("devenv.exe");
                                if (ignoredProcesses.Contains(processName) == false)
                                {
                                    presentmonProcessNames.Add(processName);
                                    var processNamesString = JsonConvert.SerializeObject(presentmonProcessNames);
                                    sendSocketData("fps_processes", processNamesString);
                                }
                                 ;
                            }

                            if (presentmonChosenProcessName != null && presentmonChosenProcessName == processName)
                            {
                                var time = GetTimestamp();
                                Dictionary<String, String> data = new Dictionary<String, String>();
                                if (msBetweenPresents.Any(char.IsDigit))
                                {
                                    data[processName] = msBetweenPresents;
                                    //data[msBetweenPresents] = time;
                                    //data["process"] = line.Split(",")[0];
                                    fpsData.Add(JsonConvert.SerializeObject(data));

                                    if (fpsData.Count > 1)
                                    {

                                        var jsonString = JsonConvert.SerializeObject(fpsData);
                                        sendSocketData("fps_data", jsonString);
                                        fpsData.Clear();
                                        addStringToListbox("sent fps data");
                                    }
                                }
                            }






                        }




                    }

                };
                fpsTask = new Task(fpsAction, "fpsTask");
                runPresentmon();
                shouldSendFpsData = true;
                fpsData.Clear();
                if (data != "")
                {
                    presentmonChosenProcessName = data;
                }
                if (fpsTask.Status != TaskStatus.Running)
                {

                    fpsTask.Start();

                }
            });
            socketio.On("stress_test", data =>
            {
                string unescapedJson = data.Replace("\\", "");
                Dictionary<string, dynamic> parsedData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(unescapedJson);
                extractAndRunStressTest(parsedData["type"], parsedData["seconds"]);

            });
            socketio.On("stop_fps", () =>
            {
                stopPresentmon();
                shouldSendFpsData = false;
                presentmonChosenProcessName = null;
                presentmonProcessNames.Clear();
            });
            socketio.Connected += (sender, args) =>
            {
                isConnectedToServer = true;
                socketConnectionChanged(true);
            };
            socketio.Disconnected += (sender, args) =>
            {
                isConnectedToServer = false;
                socketConnectionChanged(false);
                connectToServer();
            };
            connectToServer();

            



        }
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }
        private async void sendDiskData()
        {
            await runCrystalDiskInfo();
            if(diskInfos!=null) {
                sendSocketData("disk_data", Newtonsoft.Json.JsonConvert.SerializeObject(diskInfos));
            }
            

        }
        private async Task runCrystalDiskInfo()
        {
            if (IsAdministrator() == false)
            {
                //dont run if it's not running as adminstrator
                return;
            }
            string tempPath = Path.Combine(Path.GetTempPath(), "ZalDiskInfo");
            string zipResourceName = "DiskInfo.zip"; // Make sure this matches the name in your resources
            string executableName = "DiskInfo.exe";

            // Create the temporary directory if it doesn't exist
            Directory.CreateDirectory(tempPath);

            // Access the zip file from resources
            byte[] zipData = Zal.Resources.DiskInfo as byte[];
            if (zipData == null)
            {
                throw new Exception("Zip resource not found.");
            }

            // Write the zip data to a temporary file
            string tempZipPath = Path.Combine(tempPath, zipResourceName);
            File.WriteAllBytes(tempZipPath, zipData);

            // Extract the zip file to the temporary directory
            using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string extractPath = Path.Combine(tempPath, entry.FullName);

                    // If the entry is a directory, create the directory
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(extractPath);
                    }
                    else
                    {
                        entry.ExtractToFile(extractPath, true);
                    }
                }
            }

            // Construct the path to the executable
            string executablePath = Path.Combine(tempPath, executableName);


            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(tempPath, executableName), // Replace with the actual path
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "/CopyExit DiskInfo.txt",
            };
            var process = new Process { StartInfo = startInfo };

            process.Start();
             process.WaitForExit();
            string resultPath = Path.Combine(tempPath, "DiskInfo.txt");
            diskInfos=DiskDataClass.getData(resultPath);

        }
        private void startTaskmanager()
        {
            if (IsAdministrator()==false)
            {
                //dont run if it's not runnign as adminstrator, because psutil uses too much CPU if it's not runnign as adminstrator
               return;
            }
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "task_manager.exe");
            try
            {
                File.WriteAllBytes(path, Zal.Resources.task_manager);
            }
            catch (Exception ex) { }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = path, // Replace with the actual path
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "",
            };
            this._taskmanagerProcess = new Process { StartInfo = startInfo };

            this._taskmanagerProcess.Start();
        }
        private async void sendSocketData(string to, string uncompressedData)
        {
            var compressedBytes = CompressString(uncompressedData);
            Dictionary<String, String> map = new Dictionary<String, String>();
            map["data"] = compressedBytes;
            await socketio.Emit(to, map);
        }
        static string CompressString(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            using (MemoryStream outputStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }

                byte[] compressedBytes = outputStream.ToArray();
                return Convert.ToBase64String(compressedBytes);
            }
        }

        private void socketConnectionChanged(bool isConnected, bool isConnecting = false)
        {
            var bc = new BrushConverter();
            if (isConnecting)
               
            {
                this.Dispatcher.Invoke(() =>
                {
                    connectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#219ebc"));
                    connectionStateText.Text = "Connecting...";
                    
                });

            }
            else if (isConnected)
            {
                this.Dispatcher.Invoke(() =>
                {
                    connectionStateText.Text = "Connected to Server";
                    connectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#29bf12"));
                });
                
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    connectionStateText.Text = "Not Connected";
                    connectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e63946"));
                });
               

            }
        }

        public static void extractAndRunStressTest(String type, long seconds)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "StressTest");
            string zipResourceName = "stress_test.zip"; // Make sure this matches the name in your resources
            string executableName = "stress_test.exe";

            // Create the temporary directory if it doesn't exist
            Directory.CreateDirectory(tempPath);

            // Access the zip file from resources
            byte[] zipData = Zal.Resources.stress_test as byte[];
            if (zipData == null)
            {
                throw new Exception("Zip resource not found.");
            }

            // Write the zip data to a temporary file
            string tempZipPath = Path.Combine(tempPath, zipResourceName);
            File.WriteAllBytes(tempZipPath, zipData);

            // Extract the zip file to the temporary directory
            using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string extractPath = Path.Combine(tempPath, entry.FullName);

                    // If the entry is a directory, create the directory
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(extractPath);
                    }
                    else
                    {
                        entry.ExtractToFile(extractPath, true);
                    }
                }
            }

            // Construct the path to the executable
            string executablePath = Path.Combine(tempPath, executableName);

            // Check if the executable exists
            if (File.Exists(executablePath))
            {
                // Run the executable
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath, // Replace with the actual path
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"{type} {seconds}",
                };
                var process = new Process { StartInfo = startInfo };

                process.Start();
            }
            else
            {
                throw new FileNotFoundException("Executable not found in the extracted files.");
            }

        }


        private void AuthStateChanged(object sender, UserEventArgs e)
        {
            var user = e.User;

            Application.Current.Dispatcher.Invoke(() =>
            {
               
            });
        }
        private async void connectToServer()
        {
            await Task.Delay(new TimeSpan(0, 0, 1)).ContinueWith(async o => {

                socketConnectionChanged(false, isConnecting: true);
                var uid = FirebaseUI.Instance.Client.User.Uid;
                var idToken=await FirebaseUI.Instance.Client.User.GetIdTokenAsync();
               
                if (isConnectedToServer)
                {
                    await socketio.DisconnectAsync();
                    isConnectedToServer = false;
                    return;
                }
                try
                {

                    await socketio.ConnectAsync(new Uri($"https://api.zalapp.com?uid={uid}&idToken={idToken}&type=0"));
                    //await socketio.ConnectAsync(new Uri($"http://192.168.0.112:5000?uid={uid}&idToken={idToken}&type=0"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    connectToServer();
                }
            });

        }




        private void addStringToListbox(String text)
        {
           Application.Current.Dispatcher.Invoke(new MethodInvoker(delegate
            {
                while (ListBox.Items.Count > 3)
                {
                    ListBox.Items.RemoveAt(ListBox.Items.Count - 1);
                }
                TextBlock block=new TextBlock();
                block.Text = $"{DateTime.Now.ToString("h:mm:ss tt")} - {text}";
                ListBox.Items.Insert(0, block);
            }));
        }
        private void stopPresentmon()
        {
           
            if (_presentmonProcess != null && !_presentmonProcess.HasExited)
            {
                _presentmonProcess.Kill();
            }
        }
        private void runPresentmon()
        {
            stopPresentmon();


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
        }

        async static Task DownloadFileAsync(string url, string localFilePath)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        using (var fileStream = new FileStream(localFilePath, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fileStream);
                        }

                        Console.WriteLine("File downloaded successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to download the file. Status code: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }
        async void checkForUpdates()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string currentVersion = fvi.FileVersion;
            string fileUrl = "https://www.dropbox.com/scl/fi/sjmg8sy903uwrisrpe4ds/version.txt?rlkey=gpyvq077n7u7zgf8j4fv1187h&dl=1";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(fileUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string newVersion = await response.Content.ReadAsStringAsync();
                        if (newVersion != currentVersion)
                        {
                            var confirmResult = System.Windows.Forms.MessageBox.Show($"a new update is available! would you like to update?\ncurrent version: {currentVersion}, new version: {newVersion}",
                                     "Zal",
                                     System.Windows.Forms.MessageBoxButtons.YesNo);
                            if (confirmResult == System.Windows.Forms.DialogResult.Yes)
                            {
                                String filePath = $"{System.IO.Path.GetTempPath()}{new Random().Next(0, 99999999)}.msi";
                                await DownloadFileAsync("https://www.dropbox.com/scl/fi/5657vhq8tty7vz8wjyqj1/Zal.msi?rlkey=7kwtcnksg662d9dgtsfjpdizo&dl=1", filePath);

                                Process installerProcess = new Process();
                                ProcessStartInfo processInfo = new ProcessStartInfo();
                                processInfo.Arguments = $@"/i {filePath}";
                                processInfo.FileName = "msiexec";
                                installerProcess.StartInfo = processInfo;
                                installerProcess.Start();
                                installerProcess.WaitForExit();
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to retrieve the file. Status code: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private void updateCheckBoxes()
        {
            minimizeToTray.IsChecked = Zal.Settings.Default.minimizeToTray;
            runAtStartup.IsChecked = Zal.Settings.Default.runAtStartup;
            setStartup();
        }
        private void runAtStartup_Checked(object sender, RoutedEventArgs e)
        {
        
        }

        private void minimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
           
        }
        private void setStartup()
        {

            RegistryKey rk = Registry.CurrentUser.OpenSubKey
           ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (Zal.Settings.Default.runAtStartup)
                rk.SetValue("Zal", Process.GetCurrentProcess().MainModule.FileName);
            else
                rk.DeleteValue("Zal", false);
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            FirebaseUI.Instance.Client.SignOut();
        }

        private void minimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            Zal.Settings.Default.minimizeToTray = !Zal.Settings.Default.minimizeToTray;
            Zal.Settings.Default.Save();
            Zal.Settings.Default.Upgrade();
            updateCheckBoxes();
        }

        private void runAtStartup_Click(object sender, RoutedEventArgs e)
        {
            Zal.Settings.Default.runAtStartup = !Zal.Settings.Default.runAtStartup;
            Zal.Settings.Default.Save();
            Zal.Settings.Default.Upgrade();
            updateCheckBoxes();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {


        }
        private async void dispatcherTimer_Tick()
        {
            this.data = computerData.getComputerData();
            try
            {
                this.data["taskmanager"] = getTaskmanagerData();
            }
            catch
            {
                
            }
            if (areThereClientListeners == false)
            {
                stopPresentmon();
                return;
            }

            //send computer data 
            
            var jsonString = JsonConvert.SerializeObject(this.data);
            sendSocketData("pc_data", jsonString);
            addStringToListbox("sent hardware data");
        }
        public dynamic getTaskmanagerData()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zal_taskmanager_result.json");
            string contents = File.ReadAllText(path);
            return JsonConvert.DeserializeObject(contents);
            


        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += async (sender, e) =>
            {
                await Task.Run(() =>
                {
                    dispatcherTimer_Tick();
                });
            };
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(900);
            dispatcherTimer.Start();
        }
    }
}
