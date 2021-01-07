using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Nancy.Hosting.Self;
using Newtonsoft.Json;
using Nhaama.Memory;
using NLog;
using PaisleyPark.Common;
using PaisleyPark.Models;
using PaisleyPark.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace PaisleyPark.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public static IEventAggregator EventAggregator { get; private set; }
        private NhaamaProcess GameProcess { get; set; }
        private BackgroundWorker Worker;
        public static Memory GameMemory { get; set; } = new Memory();
        public Settings UserSettings { get; set; }
        public Preset CurrentPreset { get; set; }
        public string WindowTitle { get; set; } = "Paisley Park";
        public bool IsServerStarted { get; set; } = false;

        public bool IsServerStopped
        {
            get => !IsServerStarted;
        }

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private NancyHost Host;
        private Thread WaymarkThread;
        private Offsets Offsets;
        private readonly Version CurrentVersion;
        private string GameVersion;
        public string DiscordUri { get; private set; } = "https://discord.gg/hq3DnBa";

        private static readonly Uri OffsetUrl =
            new Uri("https://raw.githubusercontent.com/MadYeling/PaisleyPark/master/Offsets/");

#pragma warning disable IDE1006 // Naming Styles

        // Memory addresses for our injection.
        public ulong _newmem { get; private set; }
        public ulong _inject { get; private set; }

#pragma warning restore IDE1006 // Naming Styles

        public DelegateCommand ManagePreset { get; private set; }
        public DelegateCommand LoadPresetCommand { get; private set; }
        public DelegateCommand ManagePresetsCommand { get; private set; }
        public DelegateCommand ClosingCommand { get; private set; }
        public DelegateCommand StartServerCommand { get; private set; }
        public DelegateCommand StopServerCommand { get; private set; }
        public DelegateCommand DiscordCommand { get; private set; }

        public MainWindowViewModel(IEventAggregator ea)
        {
            // 测试事件聚合器是否为空。
            // Test if the Event Aggregator is null.
            if (ea == null)
            {
                MessageBox.Show("Event Aggregator is null, unable to start.", "Paisley Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                logger.Error("Event Aggregator is null");
                Application.Current.Shutdown();
            }

            // 设置安全协议，主要针对Windows 7用户。
            // Set the security protocol, mainly for Windows 7 users.
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & SecurityProtocolType.Ssl3) |
                                                   (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 |
                                                    SecurityProtocolType.Tls12);

            // 存储对事件聚合器的引用。
            // Store reference to the event aggregator.
            EventAggregator = ea;

            logger.Info("=== PAISLEY PARK 已启动 ===");

            // 删除所有旧的更新文件。
            // Deleting any old updater file.
            if (File.Exists(".PPU.old"))
                File.Delete(".PPU.old");

            try
            {
                // 从程序集获取版本。
                // Get the version from the assembly.
                CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                logger.Debug($"软件版本: {CurrentVersion}");

                // 设置窗口标题。
                // Set window title.
                WindowTitle = string.Format("Paisley Park {0}", CurrentVersion.VersionString());
            }
            catch (Exception ex)
            {
                logger.Error(ex, "无法获取软件版本.");
                MessageBox.Show("无法获取软件版本来设置标题.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                WindowTitle = "Paisley Park";
            }

            // 获取更新。
            // Fetch an update.
            logger.Info("获取更新...");
            FetchUpdate();

            // 加载设置文件。
            // Load the settings file.
            logger.Info("加载设置...");
            try
            {
                UserSettings = Settings.Load();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "尝试加载设置文件时出现错误.");
                MessageBox.Show("无法加载你的设置文件!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            logger.Debug("设置事件.");
            // 从REST服务器订阅waymark事件。
            // Subscribe to the waymark event from the REST server.
            EventAggregator.GetEvent<WaymarkEvent>().Subscribe(waymarks =>
            {
                WriteWaymark(waymarks.A, 0);
                WriteWaymark(waymarks.B, 1);
                WriteWaymark(waymarks.C, 2);
                WriteWaymark(waymarks.D, 3);
                WriteWaymark(waymarks.One, 4);
                WriteWaymark(waymarks.Two, 5);
                WriteWaymark(waymarks.Three, 6);
                WriteWaymark(waymarks.Four, 7);
            });

            logger.Debug("订阅加载预设事件.");
            try
            {
                // 从REST服务器订阅加载预设事件。
                // Subscribe to the load preset event from the REST server.
                var loadPresetEvent = EventAggregator.GetEvent<LoadPresetEvent>();
                if (loadPresetEvent == null)
                    throw new Exception("Couldn't get LoadPresetEvent");
                loadPresetEvent.Subscribe(name =>
                {
                    var preset = UserSettings.Presets.FirstOrDefault(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (preset == null)
                    {
                        logger.Info($"Unkown preset {name}.");
                        return;
                    }

                    WriteWaymark(preset.A, 0);
                    WriteWaymark(preset.B, 1);
                    WriteWaymark(preset.C, 2);
                    WriteWaymark(preset.D, 3);
                    WriteWaymark(preset.One, 4);
                    WriteWaymark(preset.Two, 5);
                    WriteWaymark(preset.Three, 6);
                    WriteWaymark(preset.Four, 7);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Couldn't subscribe to LoadPresetEvent.");
                MessageBox.Show("Couldn't subscribe to Load Preset event.", "Paisely Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            logger.Debug("订阅保存预设事件.");
            try
            {
                // 从REST服务器订阅保存预设事件。
                // Subscribe to the save preset event from the REST server.
                var savePresetEvent = EventAggregator.GetEvent<SavePresetEvent>();
                if (savePresetEvent == null)
                    throw new Exception("Couldn't get SavePresetEvent");
                savePresetEvent.Subscribe(name =>
                {
                    var preset = UserSettings.Presets.FirstOrDefault(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

                    try
                    {
                        if (preset == null)
                        {
                            preset = new Preset();
                            Application.Current.Dispatcher.Invoke(() => UserSettings.Presets.Add(preset));
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("无法保存预设.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                        logger.Error(ex, "Could not save preset");
                    }

                    preset.Name = name;
                    preset.A = GameMemory.A;
                    preset.B = GameMemory.B;
                    preset.C = GameMemory.C;
                    preset.D = GameMemory.D;
                    preset.One = GameMemory.One;
                    preset.Two = GameMemory.Two;
                    preset.Three = GameMemory.Three;
                    preset.Four = GameMemory.Four;

                    Settings.Save(UserSettings);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Couldn't subscribe to SavePresetEvent.");
                MessageBox.Show("Couldn't subscribe to Save Preset event.", "Paisley Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            logger.Debug("创建命令.");
            try
            {
                // 创建命令。
                // Create the commands.
                LoadPresetCommand = new DelegateCommand(LoadPreset);
                ClosingCommand = new DelegateCommand(OnClose);
                ManagePresetsCommand = new DelegateCommand(OnManagePresets);
                StartServerCommand = new DelegateCommand(OnStartServer).ObservesCanExecute(() => IsServerStopped);
                StopServerCommand = new DelegateCommand(OnStopServer).ObservesCanExecute(() => IsServerStarted);
                DiscordCommand = new DelegateCommand(OnDiscord);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Couldn't create a command.");
                MessageBox.Show("Couldn't create commands.", "Paisley Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            // 监听属性变化。
            // Listen for property changed.
            UserSettings.PropertyChanged += OnPropertyChanged;

            logger.Info("初始化...");
            // Prepare for new game launch.
            if (!Initialize())
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 初始化程序需要的内容。
        /// Starts everything needed for this process.
        /// </summary>
        /// <returns>Successful initialization.</returns>
        private bool Initialize()
        {
            logger.Info("初始化 Nhaama...");
            // 初始化Nhaama
            // Initialize Nhaama.
            if (!InitializeNhaama())
                return false;

            //logger.Info("Injecting code...");
            // Inject our code.
            // InjectCode();

            logger.Info("启动端口监听...");
            // 检查是否为自动启动，如果是则启动HTTP服务
            // Check autostart and start the HTTP server if it's true.
            if (UserSettings.HTTPAutoStart)
                OnStartServer();

            return true;
        }

        /// <summary>
        /// 获取程序的偏移量，并且检查对应版本偏移量。
        /// Gets the offsets for the program, also checks for a new version for this game version.
        /// </summary>
        private void GetOffsets()
        {
            // 获取FFXIV的确切版本。（通过Nhaama获取）
            // Get the current version of FFXIV.
            var gameDirectory = new DirectoryInfo(GameProcess.BaseProcess.MainModule.FileName);
            GameVersion = File.ReadAllText(Path.Combine(gameDirectory.Parent.FullName, "ffxivgame.ver"));

            logger.Debug($"游戏版本： {GameVersion}");

            // 对照我们在设置中保存的游戏版本。
            // Check the game version against what we have saved in settings.
            if (UserSettings.LatestGameVersion != GameVersion)
            {
                logger.Info($"游戏版本 {GameVersion} 与设置的最新版本 {UserSettings.LatestGameVersion} 不匹配. 下载新的Offset.");
                // 创建客户端以获取最新版本的偏移量。
                // Create client to fetch latest version of offsets.
                try
                {
                    using (var client = new WebClient())
                    {
                        // 生成对应版本的偏移量json文件的uri地址。
                        // Form the URI for the game version's offsets file.
                        var uri = new Uri(OffsetUrl, $"{GameVersion}.json");
                        // 覆盖本地offsets.json文件。
                        // Write the JSON to the disk overwriting the Offsets.json file used locally.
                        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "Offsets.json"),
                            client.DownloadString(uri));
                        // 将最新版本设为下载的版本。
                        // Set the lateste version to the version downloaded.
                        UserSettings.LatestGameVersion = GameVersion;
                        // 保存修改。
                        // Save the settings.
                        Settings.Save(UserSettings);
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show("无法找到符合当前游戏版本的Offsets.  这也许会导致预期之外的问题并且标点功能可能无法正常运作.", "Paisley Park",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error(ex, "无法从服务器获取或保存偏移量!");
                }
            }

            // 读取offsets.json文件。
            // Read the offsets.json file.
            try
            {
                using (var r = new StreamReader(Path.Combine(Environment.CurrentDirectory, "Offsets.json")))
                {
                    Offsets = JsonConvert.DeserializeObject<Offsets>(r.ReadToEnd());
                }
            }
            catch (Exception)
            {
                MessageBox.Show("无法加载偏移文件！请手动选择文件.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = Environment.CurrentDirectory,
                    DefaultExt = ".json",
                    Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*"
                };

                // 显示对话框。
                // Show dialog.
                var result = dlg.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        using (var r = new StreamReader(dlg.FileName))
                        {
                            Offsets = JsonConvert.DeserializeObject<Offsets>(r.ReadToEnd());
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("无法打开此偏移文件，程序即将退出.", "Paisley Park", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    }
                }
            }
        }

        /// <summary>
        /// 获取应用程序的更新。
        /// Fetch an update for the applicaton.
        /// </summary>
        private void FetchUpdate()
        {
            try
            {
                Process.Start("PaisleyParkUpdater.exe");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Updater didn't work.");
                var result = MessageBox.Show(
                    "无法启动更新器.是否浏览网页手动检查更新？",
                    "Paisley Park",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error
                );
                // Launch the web browser to the latest release.
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("https://github.com/MadYeling/PaisleyPark/releases/latest");
                }
            }
        }

        /// <summary>
        /// Clicking the Discord link.
        /// </summary>
        private void OnDiscord()
        {
            Process.Start(new ProcessStartInfo(DiscordUri));
        }

        /// <summary>
        /// 用户设置变更
        /// User Settings changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When specific properties change we save them immediately.
            if (e.PropertyName == "PlacementDelay" || e.PropertyName == "Port" || e.PropertyName == "HTTPAutoStart")
            {
                try
                {
                    // Save the settings file.
                    Settings.Save(UserSettings);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "无法保存设置");
                    MessageBox.Show("无法保存设置！", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Initialize Nhaama for use in the application.
        /// </summary>
        private bool InitializeNhaama()
        {
            // Get the processes of XIV.
            var procs = Process.GetProcessesByName("ffxiv_dx11");

            // More than one process.
            if (procs.Length > 1 || procs.Length == 0)
            {
                // Show the process selector window.
                if (!ShowProcessSelector(procs))
                    return false;
            }
            else
                // Get the Nhaama process from the first process that matches for XIV.
                GameProcess = procs[0].GetNhaamaProcess();

            if (GameProcess == null)
            {
                logger.Error("Couldn't get Nhaama process");
                MessageBox.Show("无法获取最终幻想XIV的Nhaama进程.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Enable raising events.
            GameProcess.BaseProcess.EnableRaisingEvents = true;

            // Listen to some stuff.
            GameProcess.BaseProcess.Exited += (_, e) =>
            {
                MessageBox.Show(
                    "最终幻想XIV似乎已经关闭或崩溃.",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                logger.Info("FFXIV 关闭或崩溃!");

                // Start the initialization process again.
                Application.Current.Dispatcher.Invoke(() => Initialize());
            };

            // Initialize as an empty string.
            string gameVersion = "";
            string ffxiv_folder = "";

            try
            {
                // Get FFXIV game folder.
                ffxiv_folder = Path.GetDirectoryName(GameProcess.BaseProcess.MainModule.FileName);
                // Read the version file.
                gameVersion = File.ReadAllLines(Path.Combine(ffxiv_folder, "ffxivgame.ver"))[0];
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"There is an error getting your FFXIV game version. {ffxiv_folder}");
                MessageBox.Show("获取你的游戏版本时出现一个问题，无法启动！", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            // Get offsets.
            GetOffsets();

            // Create new worker.
            Worker = new BackgroundWorker();
            // Set worker loop.
            Worker.DoWork += OnWork;
            // Support cancellation.
            Worker.WorkerSupportsCancellation = true;
            // Begin the loop.
            Worker.RunWorkerAsync();

            // Success!
            return true;
        }

        /// <summary>
        /// Show the process selector.
        /// </summary>
        private bool ShowProcessSelector(Process[] procs)
        {
            // Create a new process selector window.
            var ps = new ProcessSelector();
            // Get the view model.
            var vm = ps.DataContext as ProcessSelectorViewModel;
            // Set the settings.
            vm.UserSettings = UserSettings;
            // Set the process list.
            vm.ProcessList = new ObservableCollection<Process>(procs);

            // Show the dialog and if result comes back false we canceled the window.
            if (ps.ShowDialog() == false || vm.SelectedProcess == null)
            {
                logger.Info("用户没有选择进程.");
                Application.Current.Shutdown();

                // Failed to select process.
                return false;
            }

            // Set the selected process.
            GameProcess = vm.SelectedProcess.GetNhaamaProcess();

            // We did it.
            return true;
        }

        /// <summary>
        /// Injects code into the game.
        /// </summary>
        /*
		private void InjectCode()
		{
			// Ensure process is valid.
			if (GameProcess == null)
			{
				MessageBox.Show(
					"FINAL FANTASY XIV is not running or something bad happened!",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				logger.Error("FFXIV is not running during injection. This should never be seen!");
				Application.Current.Shutdown();
			}

			try
			{
				// Get xiv's base address.
				var ffxiv_dx11 = GameProcess.BaseProcess.MainModule.BaseAddress;
				// Waymark function address.
				// TODO: AoB!
				// 48 89 74 24 20 57 48 83 EC 30 8B F2 48 8B F9 83 FA 08
				var waymarkFunc = (ffxiv_dx11 + Offsets.WaymarkFunc).ToUint64();
				// Waymark class instance. (?)
				// 45 33 c0 8d 57 ff 48 8d 0d (lea rcx offset before call to function) 
				var waymarkClassPointer = (ffxiv_dx11 + Offsets.WaymarkClassPtr).ToUint64();

				logger.Debug("FFXIV Base Address: {0}", ffxiv_dx11.ToUint64().AsHex());
				logger.Debug("Waymark Function: {0}", waymarkFunc.AsHex());
				logger.Debug("Waymark Pointer: {0}", waymarkClassPointer.AsHex());

				// Allocate new memory for our function's data.
				_newmem = GameProcess.Alloc(14, ffxiv_dx11.ToUint64());

				logger.Info("_newmem: {0}", _newmem.AsHex());

				// Assembly instructions.
				string asm = string.Format(string.Join("\n", new string[]
				{
					"sub rsp, 40",          // give room in stack
                    "xor rdx, rdx",			// zero out rdx and r8
                    "xor r8, r8",
					"mov rax, {0}",			// memory allocated
                    "mov rbx, [rax+0xD]",	// active state
                    "mov dl, [rax+0xC]",	// waypoint ID
                    "test rbx, rbx",
					"jz skip",
					"lea r8, [rax]",		// waypoint x,y,z coordinates
                    "skip:",
					"mov rax, {1}",			// waymark class pointer
                    "lea rcx, [rax]",
					"mov rax, {2}",			// waymark function
                    "call rax",
					"add rsp, 40",          // move stack pointer back
                    "ret"
				}), _newmem.AsHex(), waymarkClassPointer.AsHex(), waymarkFunc.AsHex());

				// Get bytes from AsmjitCSharp.
				var bytes = AsmjitCSharp.Assemble(asm);

				// log bytes as hex
				logger.Debug("Bytes: {0}", BitConverter.ToString(bytes).Replace("-", " "));

				// Allocate bytes for our code injection near waymark function.
				_inject = GameProcess.Alloc((uint)bytes.LongLength, waymarkFunc);

				logger.Info("_inject: {0}", _inject.AsHex());

				// Write our injection bytes into the process.
				GameProcess.WriteBytes(_inject, bytes);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Something happened while injecting into FINAL FANTASY XIV!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				logger.Error(
					ex,
					"Injection Failed! newmem: {0}, inject: {1}",
					_newmem.AsHex(),
					_inject.AsHex()
				);
				OnClose();
				Application.Current.Shutdown();
			}
		}*/

        /// <summary>
        /// 用于读取内存的工作循环。
        /// Worker loop for reading memory.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWork(object sender, DoWorkEventArgs e)
        {
            // 初始化指向我们要读取的内存的指针和地址。
            // Initialize pointers and addresses to the memory we're going to read.
            var ffxiv = GameProcess.BaseProcess.MainModule.BaseAddress;

            var WaymarkAddr = new IntPtr();

            // 标点位置指针。
            // pointers for waymark positions
            var wayA = (ffxiv + Offsets.Waymarks + 0x00).ToUint64();
            logger.Debug("A点地址：{0}", wayA.ToString("X4"));
            var wayB = (ffxiv + Offsets.Waymarks + 0x20).ToUint64();
            var wayC = (ffxiv + Offsets.Waymarks + 0x40).ToUint64();
            var wayD = (ffxiv + Offsets.Waymarks + 0x60).ToUint64();
            var wayOne = (ffxiv + Offsets.Waymarks + 0x80).ToUint64();
            var wayTwo = (ffxiv + Offsets.Waymarks + 0xA0).ToUint64();
            var wayThree = (ffxiv + Offsets.Waymarks + 0xC0).ToUint64();
            var wayFour = (ffxiv + Offsets.Waymarks + 0xE0).ToUint64();

            // Worker loop runs indefinitely.
            while (true)
            {
                // Supporting cancellation.
                if (Worker.CancellationPending)
                    e.Cancel = true;

                // 读取多个标点的局部参数。
                // ReadWaymark local function to read multiple waymarks with.
                Waymark ReadWaymark(ulong addr, WaymarkID id) => new Waymark
                {
                    X = GameProcess.ReadFloat(addr),
                    Y = GameProcess.ReadFloat(addr + 0x4),
                    Z = GameProcess.ReadFloat(addr + 0x8),
                    Active = GameProcess.ReadByte(addr + 0x1C) == 1,
                    ID = id
                };

                try
                {
                    // Read waymarks in with our function.
                    GameMemory.A = ReadWaymark(wayA, WaymarkID.A);
                    GameMemory.B = ReadWaymark(wayB, WaymarkID.B);
                    GameMemory.C = ReadWaymark(wayC, WaymarkID.C);
                    GameMemory.D = ReadWaymark(wayD, WaymarkID.D);
                    GameMemory.One = ReadWaymark(wayOne, WaymarkID.One);
                    GameMemory.Two = ReadWaymark(wayTwo, WaymarkID.Two);
                    GameMemory.Three = ReadWaymark(wayThree, WaymarkID.Three);
                    GameMemory.Four = ReadWaymark(wayFour, WaymarkID.Four);

                    // Publish our event on the EventAggregator.
                    EventAggregator.GetEvent<GameMemoryUpdateEvent>().Publish(GameMemory);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception while reading game memory. Waymark Address: {0}",
                        WaymarkAddr.ToString("X4"));
                }

                // Sleep before next loop.
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 将一个标点写入内存并激活它。
        /// Write a waymark in memory and place it.
        /// </summary>
        /// <param name="waymark">Waymark to place.</param>
        private void WriteWaymark(Waymark waymark, int id = -1)
        {
            // Ensure the waymark isn't null.
            if (waymark == null)
                return;

            var wID = (id == -1 ? (byte) waymark.ID : id);

            // Initialize pointers and addresses to the memory we're going to read.
            var ffxiv = GameProcess.BaseProcess.MainModule.BaseAddress;

            // pointers for waymark positions
            var wayA = (ffxiv + Offsets.Waymarks + 0x00).ToUint64();
            var wayB = (ffxiv + Offsets.Waymarks + 0x20).ToUint64();
            var wayC = (ffxiv + Offsets.Waymarks + 0x40).ToUint64();
            var wayD = (ffxiv + Offsets.Waymarks + 0x60).ToUint64();
            var wayOne = (ffxiv + Offsets.Waymarks + 0x80).ToUint64();
            var wayTwo = (ffxiv + Offsets.Waymarks + 0xA0).ToUint64();
            var wayThree = (ffxiv + Offsets.Waymarks + 0xC0).ToUint64();
            var wayFour = (ffxiv + Offsets.Waymarks + 0xE0).ToUint64();

            if (UserSettings.LocalOnly)
            {
                ulong markAddr = 0;
                if (wID == (int) WaymarkID.A)
                    markAddr = wayA;
                else if (wID == (int) WaymarkID.B)
                    markAddr = wayB;
                else if (wID == (int) WaymarkID.C)
                    markAddr = wayC;
                else if (wID == (int) WaymarkID.D)
                    markAddr = wayD;
                else if (wID == (int) WaymarkID.One)
                    markAddr = wayOne;
                else if (wID == (int) WaymarkID.Two)
                    markAddr = wayTwo;
                else if (wID == (int) WaymarkID.Three)
                    markAddr = wayThree;
                else if (wID == (int) WaymarkID.Four)
                    markAddr = wayFour;

                if (waymark.Active)
                {
                    // Write the X, Y and Z coordinates
                    GameProcess.Write(markAddr, waymark.X);
                    GameProcess.Write(markAddr + 0x4, waymark.Y);
                    GameProcess.Write(markAddr + 0x8, waymark.Z);

                    GameProcess.Write(markAddr + 0x10, (int) (waymark.X * 1000));
                    GameProcess.Write(markAddr + 0x14, (int) (waymark.Y * 1000));
                    GameProcess.Write(markAddr + 0x18, (int) (waymark.Z * 1000));

                    // 写入标点状态
                    // ps：这句话如只在这个if里面写设置1，不写设置0，标点会产生bug，特别进行修正
                    // Write the active state
                    GameProcess.Write(markAddr + 0x1C, 1);
                }
                else
                {
                    GameProcess.Write(markAddr, 0);
                    GameProcess.Write(markAddr + 0x4, 0);
                    GameProcess.Write(markAddr + 0x8, 0);

                    GameProcess.Write(markAddr + 0x10, 0);
                    GameProcess.Write(markAddr + 0x14, 0);
                    GameProcess.Write(markAddr + 0x18, 0);

                    GameProcess.Write(markAddr + 0x1C, 0);
                }
                // Return out of this function
                return;
            }

            /*
			// Write the X, Y and Z coordinates.
			GameProcess.Write(_newmem, waymark.X);
			GameProcess.Write(_newmem + 0x4, waymark.Y);
			GameProcess.Write(_newmem + 0x8, waymark.Z);

			// Write the waymark ID.
			GameProcess.Write(_newmem + 0xC, (byte)(id == -1 ? (byte)waymark.ID : id));

			// Write the enable state
			GameProcess.Write(_newmem + 0xD, (byte)(waymark.Active ? 1 : 0));

			// Create a thread to call our injected function.
			var threadHandle = GameProcess.CreateRemoteThread(new IntPtr((long)_inject), out _);

			// Ensure the delay is at least 10 ms.
			var delay = Math.Max(UserSettings.PlacementDelay, 10);

			// Wait a selected number of ms
			Task.Delay(delay).Wait();

			// Wait for the thread.
			Kernel32.WaitForSingleObject(threadHandle, unchecked((uint)-1));

			// Close the thread handle.
			Kernel32.CloseHandle(threadHandle);*/
        }

        /// <summary>
        /// Loads the preset using our injected function.
        /// </summary>
        private void LoadPreset()
        {
            // Ensure that our injection and newmem addresses are set.
            /*if (_inject == 0 || _newmem == 0)
			{
				MessageBox.Show(
					"Code is not injected for placing waymarks!",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				logger.Error("Injection somehow failed yet wasn't caught by an earlier error. You should not see this!");
				OnClose();
				Application.Current.Shutdown();
			}*/

            if (!UserSettings.LocalOnly)
            {
                MessageBox.Show(
                    "此版本的Paisley Park只支持本地模式.\n这是因为5.2吉田更新了标点机制, 无法在战斗中修改.\n本地模式启动后, 用此软件修改的标点只有你自己能看见, 队友无法看见标点.",
                    "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // Ensure we have a preset selected.
            if (CurrentPreset == null)
                return;

            // Calls the waymark function for all our waymarks.
            try
            {
                if (WaymarkThread != null && WaymarkThread.IsAlive)
                {
                    MessageBox.Show("请等待上一次加载完成.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                WaymarkThread = new Thread(() =>
                {
                    WriteWaymark(CurrentPreset.A, 0);
                    WriteWaymark(CurrentPreset.B, 1);
                    WriteWaymark(CurrentPreset.C, 2);
                    WriteWaymark(CurrentPreset.D, 3);
                    WriteWaymark(CurrentPreset.One, 4);
                    WriteWaymark(CurrentPreset.Two, 5);
                    WriteWaymark(CurrentPreset.Three, 6);
                    WriteWaymark(CurrentPreset.Four, 7);
                });

                WaymarkThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "试图加载预设时发生了一些问题！",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                logger.Error(ex,
                    "An error occured while trying to call remote thread or writing waymarks into memory.");
                OnClose();
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Starts the HTTP server.
        /// <param name="alert">Alerts that the server started.</param>
        /// </summary>
        private void OnStartServer()
        {
            // Ignore if server is already started.
            if (IsServerStarted)
                return;

            // Initialize the host.
            Host = new NancyHost(new PaisleyParkBootstrapper(),
                new Uri($"http://localhost:{UserSettings.Port.ToString()}"));

            // Start the Nancy Host.
            try
            {
                Host.Start();
                IsServerStarted = true;
                StartServerCommand.RaiseCanExecuteChanged();
                StopServerCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not start Nancy host.");
                MessageBox.Show($"无法在端口 {UserSettings.Port} 上启动HTTP服务!", "Paisley Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// When the HTTP server stops.
        /// </summary>
        private void OnStopServer()
        {
            try
            {
                Host.Stop();
                IsServerStarted = false;
                StartServerCommand.RaiseCanExecuteChanged();
                StopServerCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error stopping server.");
                MessageBox.Show("停止服务时出现了一个错误.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Click to manage the presets.
        /// </summary>
        private void OnManagePresets()
        {
            // Create new preset manager window.
            var win = new PresetManager
            {
                // Set owner to main window.
                Owner = Application.Current.MainWindow
            };

            // Pull view model from window.
            var vm = win.DataContext as PresetManagerViewModel;

            // Populate the presets with our current presets as a new instance.
            vm.Presets = new ObservableCollection<Preset>(UserSettings.Presets);

            // Check if we're saving changes.
            if (win.ShowDialog() == true)
            {
                // Reassign presets in user settings to the ones managed by the window.
                UserSettings.Presets = vm.Presets;
                // Save the settings.
                Settings.Save(UserSettings);
            }
        }

        /// <summary>
        /// When the window is being closed.
        /// </summary>
        private void OnClose()
        {
            // Deallocate memory before closing.
            if (_inject != 0)
                GameProcess.Dealloc(_inject);
            if (_newmem != 0)
                GameProcess.Dealloc(_newmem);

            // Save the settings.
            Settings.Save(UserSettings);

            LogManager.Shutdown();
        }
    }
}