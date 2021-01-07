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
            // �����¼��ۺ����Ƿ�Ϊ�ա�
            // Test if the Event Aggregator is null.
            if (ea == null)
            {
                MessageBox.Show("Event Aggregator is null, unable to start.", "Paisley Park", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                logger.Error("Event Aggregator is null");
                Application.Current.Shutdown();
            }

            // ���ð�ȫЭ�飬��Ҫ���Windows 7�û���
            // Set the security protocol, mainly for Windows 7 users.
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & SecurityProtocolType.Ssl3) |
                                                   (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 |
                                                    SecurityProtocolType.Tls12);

            // �洢���¼��ۺ��������á�
            // Store reference to the event aggregator.
            EventAggregator = ea;

            logger.Info("=== PAISLEY PARK ������ ===");

            // ɾ�����оɵĸ����ļ���
            // Deleting any old updater file.
            if (File.Exists(".PPU.old"))
                File.Delete(".PPU.old");

            try
            {
                // �ӳ��򼯻�ȡ�汾��
                // Get the version from the assembly.
                CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                logger.Debug($"����汾: {CurrentVersion}");

                // ���ô��ڱ��⡣
                // Set window title.
                WindowTitle = string.Format("Paisley Park {0}", CurrentVersion.VersionString());
            }
            catch (Exception ex)
            {
                logger.Error(ex, "�޷���ȡ����汾.");
                MessageBox.Show("�޷���ȡ����汾�����ñ���.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                WindowTitle = "Paisley Park";
            }

            // ��ȡ���¡�
            // Fetch an update.
            logger.Info("��ȡ����...");
            FetchUpdate();

            // ���������ļ���
            // Load the settings file.
            logger.Info("��������...");
            try
            {
                UserSettings = Settings.Load();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "���Լ��������ļ�ʱ���ִ���.");
                MessageBox.Show("�޷�������������ļ�!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            logger.Debug("�����¼�.");
            // ��REST����������waymark�¼���
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

            logger.Debug("���ļ���Ԥ���¼�.");
            try
            {
                // ��REST���������ļ���Ԥ���¼���
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

            logger.Debug("���ı���Ԥ���¼�.");
            try
            {
                // ��REST���������ı���Ԥ���¼���
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
                        MessageBox.Show("�޷�����Ԥ��.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
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

            logger.Debug("��������.");
            try
            {
                // �������
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

            // �������Ա仯��
            // Listen for property changed.
            UserSettings.PropertyChanged += OnPropertyChanged;

            logger.Info("��ʼ��...");
            // Prepare for new game launch.
            if (!Initialize())
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// ��ʼ��������Ҫ�����ݡ�
        /// Starts everything needed for this process.
        /// </summary>
        /// <returns>Successful initialization.</returns>
        private bool Initialize()
        {
            logger.Info("��ʼ�� Nhaama...");
            // ��ʼ��Nhaama
            // Initialize Nhaama.
            if (!InitializeNhaama())
                return false;

            //logger.Info("Injecting code...");
            // Inject our code.
            // InjectCode();

            logger.Info("�����˿ڼ���...");
            // ����Ƿ�Ϊ�Զ������������������HTTP����
            // Check autostart and start the HTTP server if it's true.
            if (UserSettings.HTTPAutoStart)
                OnStartServer();

            return true;
        }

        /// <summary>
        /// ��ȡ�����ƫ���������Ҽ���Ӧ�汾ƫ������
        /// Gets the offsets for the program, also checks for a new version for this game version.
        /// </summary>
        private void GetOffsets()
        {
            // ��ȡFFXIV��ȷ�а汾����ͨ��Nhaama��ȡ��
            // Get the current version of FFXIV.
            var gameDirectory = new DirectoryInfo(GameProcess.BaseProcess.MainModule.FileName);
            GameVersion = File.ReadAllText(Path.Combine(gameDirectory.Parent.FullName, "ffxivgame.ver"));

            logger.Debug($"��Ϸ�汾�� {GameVersion}");

            // ���������������б������Ϸ�汾��
            // Check the game version against what we have saved in settings.
            if (UserSettings.LatestGameVersion != GameVersion)
            {
                logger.Info($"��Ϸ�汾 {GameVersion} �����õ����°汾 {UserSettings.LatestGameVersion} ��ƥ��. �����µ�Offset.");
                // �����ͻ����Ի�ȡ���°汾��ƫ������
                // Create client to fetch latest version of offsets.
                try
                {
                    using (var client = new WebClient())
                    {
                        // ���ɶ�Ӧ�汾��ƫ����json�ļ���uri��ַ��
                        // Form the URI for the game version's offsets file.
                        var uri = new Uri(OffsetUrl, $"{GameVersion}.json");
                        // ���Ǳ���offsets.json�ļ���
                        // Write the JSON to the disk overwriting the Offsets.json file used locally.
                        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "Offsets.json"),
                            client.DownloadString(uri));
                        // �����°汾��Ϊ���صİ汾��
                        // Set the lateste version to the version downloaded.
                        UserSettings.LatestGameVersion = GameVersion;
                        // �����޸ġ�
                        // Save the settings.
                        Settings.Save(UserSettings);
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show("�޷��ҵ����ϵ�ǰ��Ϸ�汾��Offsets.  ��Ҳ��ᵼ��Ԥ��֮������Ⲣ�ұ�㹦�ܿ����޷���������.", "Paisley Park",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error(ex, "�޷��ӷ�������ȡ�򱣴�ƫ����!");
                }
            }

            // ��ȡoffsets.json�ļ���
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
                MessageBox.Show("�޷�����ƫ���ļ������ֶ�ѡ���ļ�.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = Environment.CurrentDirectory,
                    DefaultExt = ".json",
                    Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*"
                };

                // ��ʾ�Ի���
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
                        MessageBox.Show("�޷��򿪴�ƫ���ļ������򼴽��˳�.", "Paisley Park", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    }
                }
            }
        }

        /// <summary>
        /// ��ȡӦ�ó���ĸ��¡�
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
                    "�޷�����������.�Ƿ������ҳ�ֶ������£�",
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
        /// �û����ñ��
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
                    logger.Error(ex, "�޷���������");
                    MessageBox.Show("�޷��������ã�", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("�޷���ȡ���ջ���XIV��Nhaama����.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Enable raising events.
            GameProcess.BaseProcess.EnableRaisingEvents = true;

            // Listen to some stuff.
            GameProcess.BaseProcess.Exited += (_, e) =>
            {
                MessageBox.Show(
                    "���ջ���XIV�ƺ��Ѿ��رջ����.",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                logger.Info("FFXIV �رջ����!");

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
                MessageBox.Show("��ȡ�����Ϸ�汾ʱ����һ�����⣬�޷�������", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
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
                logger.Info("�û�û��ѡ�����.");
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
        /// ���ڶ�ȡ�ڴ�Ĺ���ѭ����
        /// Worker loop for reading memory.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWork(object sender, DoWorkEventArgs e)
        {
            // ��ʼ��ָ������Ҫ��ȡ���ڴ��ָ��͵�ַ��
            // Initialize pointers and addresses to the memory we're going to read.
            var ffxiv = GameProcess.BaseProcess.MainModule.BaseAddress;

            var WaymarkAddr = new IntPtr();

            // ���λ��ָ�롣
            // pointers for waymark positions
            var wayA = (ffxiv + Offsets.Waymarks + 0x00).ToUint64();
            logger.Debug("A���ַ��{0}", wayA.ToString("X4"));
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

                // ��ȡ������ľֲ�������
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
        /// ��һ�����д���ڴ沢��������
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

                    // д����״̬
                    // ps����仰��ֻ�����if����д����1����д����0���������bug���ر��������
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
                    "�˰汾��Paisley Parkֻ֧�ֱ���ģʽ.\n������Ϊ5.2��������˱�����, �޷���ս�����޸�.\n����ģʽ������, �ô�����޸ĵı��ֻ�����Լ��ܿ���, �����޷��������.",
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
                    MessageBox.Show("��ȴ���һ�μ������.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    "��ͼ����Ԥ��ʱ������һЩ���⣡",
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
                MessageBox.Show($"�޷��ڶ˿� {UserSettings.Port} ������HTTP����!", "Paisley Park", MessageBoxButton.OK,
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
                MessageBox.Show("ֹͣ����ʱ������һ������.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
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