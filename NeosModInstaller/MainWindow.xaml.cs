using KANTANJson;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


namespace NeosModInstaller
{
    public class LatestMod : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Version { get; set; } = "";
        public string Url { get; set; } = "";
        public string? InstallLocation { get; set; }

        public bool Focusable { get; set ; } = true;

        private bool _Seleted = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public bool SeletedForce { get { return _Seleted; } set { _Seleted = value; } }
        public bool Selected { get { return _Seleted; } set { _Seleted = Focusable ? value : _Seleted; } }
        public bool Emphasis { get; set; }

    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        string NeosInstallPath = "";
        List<LatestMod> LatestMods = new List<LatestMod>();

        const string UninstallRegPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\";

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Uninstallディレクトリを開く
            RegistryKey? regkey = Registry.LocalMachine.OpenSubKey(UninstallRegPath, false);
            if (regkey == null)
            {
                MessageBox.Show("ERROR:RegKey not found");
                return;
            }

            //Uninstallディレクトリを総なめしIDを取得そのIDのディレクトリを開く
            foreach(string regSoftwareId in regkey.GetSubKeyNames())
            {
                RegistryKey? softwareKey = regkey.OpenSubKey(regSoftwareId, false);

                if (softwareKey == null)
                {
                    continue;
                }

                //表示名を確認
                string? displayName = (string?)softwareKey.GetValue("DisplayName");
                if(displayName == null)
                {
                    continue;
                }

                //表示名にNeos versionが有ればNeosのレジストリと認定
                if (displayName.IndexOf("Neos version") > -1)
                {
                    NeosInstallPath = (string)softwareKey.GetValue("Inno Setup: App Path");
                    NeosInstallPath = NeosInstallPath + @"\app\";
                    NeosBuildLabel.Content = "[OK Official Build]" + NeosInstallPath;
                    NeosOfficialLink.Visibility = Visibility.Hidden;
                    CreateLinks();
                    break;
                }
                else
                {
                    NeosBuildLabel.Content = "[Step 1] Download and install Neos from the official website.↓";
                }

            }
        }
        private void ModsTab_Loaded(object sender, RoutedEventArgs e)
        {
            WebClient wc = new WebClient();
            wc.DownloadFile("https://raw.githubusercontent.com/neos-modding-group/neos-mod-manifest/master/manifest.json", @".\neos-mod-manifest.json");
            wc.Dispose();

            string json = "";
            using (var reader = new StreamReader(@".\neos-mod-manifest.json"))
            {
                json = reader.ReadToEnd();
            }

            RootJson rootJson = RootJson.Deserialize(json);
            ObjectJson modsJson = (ObjectJson)((ObjectJson)rootJson.Content)["mods"];
            foreach (KeyValuePair<string, object> modDic in modsJson.ObjectDictionary)
            {

                LatestMod latestMod = new LatestMod();

                ObjectJson modObjectJson = (ObjectJson)modDic.Value;

                latestMod.Name = (string)modObjectJson["name"];

                if (latestMod.Name == "Neos Mod Loader" || latestMod.Name == "Harmony")
                {
                    latestMod.Focusable = false;
                    latestMod.SeletedForce = true;
                }

                latestMod.Description = (string)modObjectJson["description"];
                latestMod.Category = (string)modObjectJson["category"];

                ObjectJson modVersJson = modObjectJson.GetObjectJson("versions");
                string version = modVersJson.GetKeyByIndex(0);

                latestMod.Version = version;

                ArrayJson artifactsJson = modVersJson.GetObjectJson(0).GetArrayJson("artifacts");
                if (artifactsJson.Count > 0)
                {

                    ObjectJson modArt = artifactsJson.GetObjectJson(0);

                    latestMod.Url = (string)modArt["url"];


                    if (modArt.ObjectDictionary.ContainsKey("installLocation"))
                    {
                        latestMod.InstallLocation = (string)modArt["installLocation"];
                    }
                    else
                    {
                        latestMod.InstallLocation = null;
                    }

                    if (latestMod.InstallLocation == "" || latestMod.InstallLocation == null)
                    {
                        string filename = Path.GetFileName(latestMod.Url);
                        string path = latestMod.InstallLocation;
                        if (latestMod.InstallLocation == "" || latestMod.InstallLocation == null)
                        {
                            path = @"/nml_mods";
                        }

                        path = NeosInstallPath + "." + path.Replace("/", "\\") + "\\";

                        if (Directory.Exists(path))
                        {
                            if(File.Exists(path + filename))
                            {
                                latestMod.SeletedForce = true;
                            }
                        }
                    }

                    LatestMods.Add(latestMod);
                }
            }

            ModsList.ItemsSource = LatestMods;
        }

        private void NeosOfficialLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ProcessStartInfo p = new ProcessStartInfo()
            {
                FileName = @"https://neos.com/",
                UseShellExecute = true,
            };
            Process.Start(p);
        }

        private void DeleatMods()
        {
            if (Directory.Exists(NeosInstallPath + @"nml_mods\"))
            {
                Directory.Delete(NeosInstallPath + @"nml_mods\", true);
            }

            if (Directory.Exists(NeosInstallPath + @"nml_libs\"))
            {
                Directory.Delete(NeosInstallPath + @"nml_libs\", true);
            }

            if (File.Exists(NeosInstallPath + @"Libraries\NeosModLoader.dll"))
            {
                File.Delete(NeosInstallPath + @"Libraries\NeosModLoader.dll");
            }
        }

        private async void ModsApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            ModsApplyBtn.Content = "Processing...";
            ModsApplyBtn.IsEnabled = false;
            ModsRemoveBtn.IsEnabled = false;
            

            await Task.Delay(10);

            if (!Directory.Exists(NeosInstallPath + @"nml_mods\"))
            {
                Directory.CreateDirectory(NeosInstallPath + @"nml_mods\");
            }

            if (!Directory.Exists(NeosInstallPath + @"nml_libs\"))
            {
                Directory.CreateDirectory(NeosInstallPath + @"nml_libs\");
            }

            if (!Directory.Exists(@".\downloads\"))
            {
                Directory.CreateDirectory(@".\downloads\");
            }

            foreach (LatestMod mod in LatestMods)
            {
                if (mod.Selected)
                {
                    ModInstall(mod.Url, mod.InstallLocation);
                }
                else
                {
                    ModUninstall(mod.Url, mod.InstallLocation);
                }
            }

            ModsApplyBtn.IsEnabled = true;
            ModsRemoveBtn.IsEnabled = true;
            ModsApplyBtn.Content = "Apply";
        }

        private void DirectoryCopy(string path, string path2, bool force = false)
        {   
            DirectoryInfo rootdir = new DirectoryInfo(path);

            IEnumerable<FileInfo> files = rootdir.EnumerateFiles("*" , SearchOption.TopDirectoryOnly);

            foreach (FileInfo file in files)
            {
                if (!File.Exists(path2 + file.Name) || force)
                {
                    File.Copy(path + file.Name, path2 + file.Name);
                }
            }

            IEnumerable<DirectoryInfo> dirs = rootdir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

            foreach (DirectoryInfo dir in dirs)
            {
                if (!Directory.Exists(path2 + dir.Name))
                {
                    Directory.CreateDirectory(path2 + dir.Name);
                    
                }

                DirectoryCopy(path + dir.Name + "\\", path2 + dir.Name + "\\");
            }
        }



        private void ModInstall(string url, string? path)
        {
            string filename = Path.GetFileName(url);
            WebClient wc = new WebClient();
            wc.DownloadFile(url, @".\downloads\" + filename);
            if (path == "" || path == null)
            {
                path = @"/nml_mods";
            }
            
            File.Copy(@".\downloads\" + filename, NeosInstallPath + "." + path.Replace("/","\\") + "\\" + filename, true);
        }

        private void ModUninstall(string url, string? path)
        {
            string filename = Path.GetFileName(url);

            if (path == "" || path == null)
            {
                path = @"/nml_mods";
            }

            string filepath = NeosInstallPath + "." + path.Replace("/", "\\") + "\\" + filename;
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }
        }

        const string _SteamVR = "-SteamVR ";
        const string _RiftTouch = "-RiftTouch ";
        const string _Screen = "-Screen ";
        const string _LoadAssemblyOpts = "-LoadAssembly Libraries/NeosModLoader.dll ";
        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo p;
            if (SteamVRRbtn.IsChecked == true)
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _LoadAssemblyOpts + _SteamVR
                };
            }
            else if (OculusRbtn.IsChecked == true)
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _LoadAssemblyOpts + _RiftTouch
                };
            }
            else
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _LoadAssemblyOpts + _Screen
                };
            }

            Process.Start(p);
        }

        string LinksPath = @".\Links\";
        Dictionary<string, string> NameArgs = new Dictionary<string, string>()
        {
            {@"Neos.lnk", ""},
            {@"Neos Whith Mods.lnk", _LoadAssemblyOpts },
            {@"SteamVR.lnk", _SteamVR },
            {@"SteamVR Whith Mods.lnk", _SteamVR + _LoadAssemblyOpts },
            {@"OculusVR.lnk", _RiftTouch },
            {@"OculusVR Whith Mods.lnk", _RiftTouch + _LoadAssemblyOpts },
            {@"Desktop.lnk", _Screen },
            {@"Desktop Whith Mods.lnk", _Screen + _LoadAssemblyOpts }
        };

        private void CreateLinks()
        {
            
            if (!Directory.Exists(LinksPath))
            {
                Directory.CreateDirectory(LinksPath);
            }
            
            foreach(KeyValuePair<string,string> pair in NameArgs)
            {
                CreateLink(pair.Key, pair.Value);
            }

        }

        private void CreateLink(string linkName, string args)
        {
            string shortcutPath = LinksPath + linkName;
            string targetPath = NeosInstallPath + "Neos.exe";
            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = args;
            shortcut.WorkingDirectory = NeosInstallPath;
            shortcut.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        private void Launch_NoMods_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo p;
            if (SteamVRRbtn.IsChecked == true)
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _SteamVR
                };
            }
            else if (OculusRbtn.IsChecked == true)
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _RiftTouch
                };
            }
            else
            {
                p = new ProcessStartInfo()
                {
                    FileName = NeosInstallPath + "Neos.exe",
                    Arguments = _Screen
                };
            }

            Process.Start(p);
        }

        private void ModsRemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            string messageBoxText = "Do you want to remove it completely? (including mods installed manually)";
            string caption = "ModRemove";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result;

            result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            if(result == MessageBoxResult.Yes)
            {
                DeleatMods();
            }
            
        }

        int _SearchCount = 0;
        private void SearchMods_Click(object sender, RoutedEventArgs e)
        {

            LatestMod mod = ModFind(SearchText.Text.Replace(" ",""), _SearchCount);

            if (mod != null)
            {
                ModsList.SelectedItem = mod;
                ModsList.ScrollIntoView(mod);
                _SearchCount++;
            }
            else
            {
                MessageBox.Show("No more found.");
                _SearchCount = 0;
            }
            
        }
        private void SearchText_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _SearchCount = 0;
            LatestMod mod = ModFind(SearchText.Text.Replace(" ", ""), _SearchCount);

            if (mod != null)
            {
                ModsList.SelectedItem = mod;
                ModsList.ScrollIntoView(mod);
            }
        }

        private LatestMod ModFind(string search, int skipCount)
        {
            search = search.ToUpper();

            LatestMod selectMod = null;

            ModsList.ScrollIntoView(LatestMods[LatestMods.Count - 1]);

            foreach (LatestMod mod in LatestMods)
            {
            
                if (mod.Name.Replace(" ","").ToUpper().IndexOf(search) > -1 && search != "")
                {
                    mod.Emphasis = true;

                    skipCount--;

                    if (selectMod == null && skipCount < 0)
                    {
                        selectMod = mod;
                    }
                }
                else
                {
                    mod.Emphasis = false;
                }
            }

            ModsList.ItemsSource = null;
            ModsList.ItemsSource = LatestMods;

            return selectMod;
        }

        private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(NeosInstallPath + @"nml_mods\"))
            {
                Process.Start("EXPLORER.EXE", NeosInstallPath + @"nml_mods\");
            }
            else
            {
                MessageBox.Show("Folder Not Found.");
            }
        }
    }
}
