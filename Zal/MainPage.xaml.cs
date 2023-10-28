using Firebase.Auth;
using Firebase.Auth.UI;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
using SocketIOClient;
using System.Security.Principal;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace Zal
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        ComputerData computerData = new ComputerData();
        public SocketIOClient.SocketIO socketio;
        //we use this list to cache task manager icons. we send each process icon only once and then we keep track of the processes here to
        //never send that specific process's icon again unless the user disconnects. using this method we reduce payload of 38kb to only 3kb.
        List<string> sentTaskmanagerProcessIcons = new List<string>();

        bool isConnectedToServer = false;
        private Process _taskmanagerProcess;
        public bool areThereClientListeners = false;
        List<DiskInfoCrystal> diskInfos;
        FpsManager? fpsManager;
        //this variable is used to save computerdata,
        //we use it to immediately send a packet of data
        //when the user connects. to reduce waiting time on the app.
        Dictionary<String, dynamic>? data = null;

        public MainPage()
        {
            InitializeComponent();
            FirebaseUI.Instance.Client.AuthStateChanged += this.AuthStateChanged;
            socketConnectionChanged(false, isConnecting: true);
            checkForUpdates();
            updateCheckBoxes();
            startTaskmanager();
            setStartup();
            var user = FirebaseUI.Instance.Client.User;
            userName.Text = $"Welcome, {user.Info.DisplayName}.";
            setupSocketio();
            Logger.Log("program started");

            


        }
       
        public void sendFpsData(String data)
        {
            sendSocketData("fps_data",data);
            addStringToListbox("sent fps data");
        }
       
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }
        private async void sendDiskData()
        {
            await runCrystalDiskInfo();
            if (diskInfos != null)
            {
                sendSocketData("disk_data", Newtonsoft.Json.JsonConvert.SerializeObject(diskInfos));
            }


        }
        private async Task runCrystalDiskInfo()
        {
            Logger.Log("running crystaldiskinfo");
            if (IsAdministrator() == false)
            {
                //dont run if it's not running as adminstrator, because crystaldiskinfo don't work without it
                Logger.Log("didn't run crystaldiskinfo, program isn't running as adminstrator");
                return;
            }
            string tempPath = Path.Combine(Path.GetTempPath(), "ZalDiskInfo");
            string zipResourceName = "DiskInfo.zip"; // Make sure this matches the name in your resources
            string executableName = "DiskInfo.exe";

            // Create the temporary directory if it doesn't exist
            Directory.CreateDirectory(tempPath);

            // Access the zip file from resources
            byte[] zipData = Zal.Resources.DiskInfo as byte[];

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
            diskInfos = DiskDataClass.getData(resultPath);
            Logger.Log(string.Format("diskinfo started {0}",diskInfos));
        }
        private void startTaskmanager()
        {
            if (IsAdministrator() == false)
            {
                //dont run because psutil uses too much CPU if it's not running as adminstrator
                Logger.Log("didn't run taskmanager, program isn't running as adminstrator");
                return;
            }
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "task_manager.exe");
            try
            {
                File.WriteAllBytes(path, Zal.Resources.task_manager);
            }
            catch (Exception ex) {
                Logger.Log($"exception writing task_manager {ex.Message} - {ex.StackTrace}");
            }

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
            try
            {
                await socketio.EmitAsync(to, map);
                
            }
            catch (Exception c)
            {
                addStringToListbox(c.Message);
            }
            
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
                Logger.Log("didn't run stress_test, file not found");
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
        private async void setupSocketio()
        {
            var uid = FirebaseUI.Instance.Client.User.Uid;
            var idToken = await FirebaseUI.Instance.Client.User.GetIdTokenAsync();
            socketio = new SocketIOClient.SocketIO($"http://192.168.0.107:5000",
            //socketio = new SocketIOClient.SocketIO($"https://api.zalapp.com",
                new SocketIOOptions
            {
                Query = new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("uid", uid),
        new KeyValuePair<string, string>("idToken", idToken),
         new KeyValuePair<string, string>("type","0")
    }
            });

            socketio.On("room_clients", response =>
            {
                
                List<int> parsedData = response.GetValue<List<int>>();
                Logger.Log($"socketio room_clients {string.Join<int>(",", parsedData)}");
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
                    sentTaskmanagerProcessIcons.Clear();
                    //send diskinfo
                    sendDiskData();


                }
                else
                {

                    areThereClientListeners = false;
                    addStringToListbox("mobile left");
                    sentTaskmanagerProcessIcons.Clear();
                    fpsManager?.Dispose();
                    fpsManager = null;


                }
            });
            socketio.On("change_primary_network", response =>
            {
               
                string networkName = response.GetValue<string>();
                Logger.Log($"changing primary_network {networkName}");
                Zal.Settings.Default.primaryNetworkInterface = networkName;
                Zal.Settings.Default.Save();
                Zal.Settings.Default.Reload();
                Zal.Settings.Default.Upgrade();
            });

            socketio.On("kill_process", response =>
            {
                List<int> parsedData = JsonConvert.DeserializeObject<List<int>>(response.GetValue<string>());
                Logger.Log($"killing process {string.Join<int>(",", parsedData)}");
                foreach (int pid in parsedData)
                {
                    var processToKill = Process.GetProcessById(pid);
                    processToKill.Kill();
                }
            });
            socketio.On("restart_admin", response =>
            {
                Logger.Log($"restarting as admin");
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
                Logger.Log($"starting fps");
                fpsManager = new FpsManager(data => sendFpsData(data));
            });
            socketio.On("stress_test", data =>
            {
                
                string unescapedJson = data.GetValue<String>().Replace("\\", "");
                Dictionary<string, dynamic> parsedData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(unescapedJson);
                Logger.Log($"starting stress_test {parsedData["type"]},{parsedData["seconds"]}");
                extractAndRunStressTest(parsedData["type"], parsedData["seconds"]);

            });
            socketio.On("stop_fps", response =>
            {
                Logger.Log($"stopping fps");
                fpsManager?.Dispose();
                fpsManager = null;

            });
            socketio.OnConnected += (sender, args) =>
            {
                isConnectedToServer = true;
                socketConnectionChanged(true);
            };
            socketio.OnDisconnected += (sender, args) =>
            {
                isConnectedToServer = false;
                socketConnectionChanged(false);
                connectToServer();
            };
            connectToServer();
        }
        private async void connectToServer()
        {
            socketConnectionChanged(false, isConnecting: true);
            

            if (isConnectedToServer)
            {
                await socketio.DisconnectAsync();
                isConnectedToServer = false;
                return;
            }
            try
            {

  socketio.ConnectAsync();

            }
            catch (Exception ex)
            {
                addStringToListbox(ex.Message);
                connectToServer();
            }

        }




        private void addStringToListbox(String text)
        {
            Application.Current.Dispatcher.Invoke(new MethodInvoker(delegate
            {
                while (ListBox.Items.Count > 3)
                {
                    ListBox.Items.RemoveAt(ListBox.Items.Count - 1);
                }
                TextBlock block = new TextBlock();
                block.Text = $"{DateTime.Now.ToString("h:mm:ss tt")} - {text}";
                ListBox.Items.Insert(0, block);
            }));
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
                        Logger.Log($"failed downloading update {response.StatusCode}");
                        Console.WriteLine($"Failed to download the file. Status code: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"error during update {ex.Message} - {ex.StackTrace}");
                    
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
                Logger.Log($"error looking for update {ex.Message} = {ex.StackTrace}");
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
        private void viewLogClicked(object sender, RoutedEventArgs e)
        {
           Process.Start("notepad.exe", Logger.GetLogFilePath());  
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
                var taskmanager = getTaskmanagerData();
                foreach(KeyValuePair<string, dynamic> entry in taskmanager)
                { 
                    if (sentTaskmanagerProcessIcons.Contains(entry.Key))
                    {
                        taskmanager[entry.Key].Remove("icon");
                    }
                    else
                    {
                        sentTaskmanagerProcessIcons.Add(entry.Key);
                    }
                    
                }
                
                this.data["taskmanager"] = taskmanager;

            }
            catch (Exception ex)
            {
                Logger.Log($"error during main tick {ex.Message} - {ex.StackTrace}");
            }
            if (areThereClientListeners == false)
            {
                fpsManager?.Dispose();
                fpsManager = null;
                return;
            }

            //send computer data 

            var jsonString = JsonConvert.SerializeObject(this.data);
            sendSocketData("pc_data", jsonString);
            addStringToListbox("sent hardware data");
        }
        public Dictionary<string, dynamic> getTaskmanagerData()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zal_taskmanager_result.json");
            string contents = File.ReadAllText(path);
            Dictionary<string, dynamic> parsedData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(contents);
            return parsedData;
            


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
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(1000);
            dispatcherTimer.Start();



        }
    }
}