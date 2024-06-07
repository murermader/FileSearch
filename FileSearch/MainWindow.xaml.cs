using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using Peter;
using YamlDotNet.Serialization;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace FileSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _mainWindowViewModel = new MainWindowViewModel(ApplySearchBoxFilter, (files) =>
            {
                DataObject dragData = new(DataFormats.FileDrop, files);
                DragDrop.DoDragDrop(ResultsDataGrid, dragData, DragDropEffects.Move);
            });
            DataContext = _mainWindowViewModel;

            string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ConfigPath = Path.Combine(userFolderPath, "Config.yaml");
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                ApplySearchBoxFilter();
            };

            // Focus search box
            SetupShortcut(() => SearchBox.Focus(), Key.F, ModifierKeys.Control);

            // Reload config
            SetupShortcut(() => Task.Run(ReloadConfigAndResults).ConfigureAwait(false), Key.F5);
            SetupShortcut(() => Task.Run(ReloadConfigAndResults).ConfigureAwait(false), Key.R, ModifierKeys.Control);

            // Copy selected item (configured in XAML for Datagrid)

            // Delete selected item

            // TODO
            // Hide window
            SetupShortcut(Hide, Key.Escape);
            Task.Run(ReloadConfigAndResults).ConfigureAwait(false);
        }

        private void SetupShortcut(Action action, Key key, ModifierKeys modifiers = default)
        {
            RoutedCommand routedCommand = new();
            CommandBinding commandBinding = new(
                routedCommand,
                (sender, e) => action());
            CommandBindings.Add(commandBinding);

            // Remove the system command for the provided keys and modifiers
            KeyBinding? existingBinding = InputBindings
                .OfType<KeyBinding>()
                .FirstOrDefault(kb => kb.Key == key && kb.Modifiers == modifiers);
            if (existingBinding != null)
            {
                Console.WriteLine($"Remove existing key binding for {modifiers}+{key}");
                InputBindings.Remove(existingBinding);
            }

            KeyBinding keyBinding = new()
            {
                Command = routedCommand,
                Key = key,
                Modifiers = modifiers
            };
            InputBindings.Add(keyBinding);
        }

        private FileSystemWatcher _fileSystemConfigWatcher;
        private readonly List<FileSystemWatcher> _fileSystemWatchersForFolders = new();
        private readonly DispatcherTimer _searchTimer;
        private List<Result> _allResults = new();
        private readonly HashSet<string> _allResultsFilePaths = new();
        private readonly MainWindowViewModel _mainWindowViewModel;
        private bool _reloadingConfig = false;

        public Config AppConfig { get; set; }

        public string ConfigPath { get; }

        public static void FocusSearchBoxCommand()
        {
            Console.WriteLine("test");
        }

        private void InitializeAllResults(CancellationToken ctsToken = default, Ignore.Ignore ignore = null)
        {
            foreach (string folder in AppConfig.Folders)
            {
                Console.WriteLine($"Include folder: {folder}");
                GetPathsFromDirectory(folder, ignore, new List<string>(), ctsToken);
            }

            if (ctsToken.IsCancellationRequested)
            {
                // Cancelled. Do not update _allResults.
                return;
            }

            lock (_allResultsFilePaths)
            {
                _allResults = _allResultsFilePaths.Select(filePath => new Result(filePath)).ToList();
            }

            Console.WriteLine($"Loaded {_allResults.Count} files.");
        }

        private void GetPathsFromDirectory(string rootPath, Ignore.Ignore? ignore, List<string> prependFolders,
            CancellationToken ctsToken = default)
        {
            string gitIgnorePath = Path.Combine(rootPath, ".gitignore");
            if (File.Exists(gitIgnorePath))
            {
                ignore = LoadIgnore(gitIgnorePath);
            }

            // Get all files from directory
            foreach (string file in Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly))
            {
                string ignorePath = "";
                foreach (string folder in prependFolders)
                {
                    ignorePath += $"{folder}/";
                }
                ignorePath += Path.GetDirectoryName(file);                
                if (ignore is not null && ignore.IsIgnored(ignorePath))
                {
                    // File is ignored by gitignore.
                    Console.WriteLine($"Ignored: {file}");
                    continue;
                }

                if (ctsToken.IsCancellationRequested)
                {
                    Console.WriteLine($"InitializeAllResults cancelled at {file}");
                    break;
                }

                if (!AppConfig.IncludeHiddenFiles && (new FileInfo(file).Attributes & FileAttributes.Hidden) ==
                    FileAttributes.Hidden)
                {
                    continue;
                }

                lock (_allResultsFilePaths)
                {
                    _allResultsFilePaths.Add(file);
                }
            }

            // Recurse down the directories
            foreach (string directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
            {
                string ignorePath = "";
                foreach (string folder in prependFolders)
                {
                    ignorePath += $"{folder}/";
                }
                ignorePath += Path.GetFileName(directory);
                
                if (ignore is not null && ignore.IsIgnored(ignorePath))
                {
                    // File is ignored by gitignore.
                    Console.WriteLine($"Ignored: {directory}");
                    continue;
                }

                if (ctsToken.IsCancellationRequested)
                {
                    break;
                }

                if (!AppConfig.IncludeHiddenFiles &&
                    (new DirectoryInfo(directory).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }

                lock (_allResultsFilePaths)
                {
                    _allResultsFilePaths.Add(directory);
                }

                if (ignore is not null)
                {
                    prependFolders.Add(Path.GetFileName(rootPath));
                }
                GetPathsFromDirectory(directory, ignore, prependFolders, ctsToken);
                if (ignore is not null)
                {
                    prependFolders.Remove(Path.GetFileName(rootPath));
                }
            }
        }

        private Ignore.Ignore LoadIgnore(string path)
        {
            string[] lines = File.ReadAllLines(path);
            List<string> rules = new List<string>();

            foreach (string line in lines)
            {
                string lineTrimmed = line.Trim().TrimStart('/');

                if (lineTrimmed.Length == 0)
                {
                    continue;
                }

                if (lineTrimmed.StartsWith("#"))
                {
                    continue;
                }

                if (!rules.Contains(lineTrimmed))
                {
                    rules.Add(lineTrimmed);
                }
            }

            Ignore.Ignore ignore = new();
            ignore.Add(rules);
            return ignore;
        }

        private void UpdateResults(List<Result> results)
        {
            Helper.RunOnDispatcher(() =>
            {
                _mainWindowViewModel.Results.Clear();
                foreach (Result result in results)
                {
                    _mainWindowViewModel.Results.Add(result);
                }
            });
        }

        private List<Result> FilterResults(string searchText)
        {
            List<Result> filteredResults = new List<Result>();
            string[] searchTerms = searchText.Split();

            foreach (Result result in _allResults)
            {
                // All search terms need to match
                bool includeInResults = true;
                foreach (string searchTerm in searchTerms)
                {
                    StringComparison stringComparison = _mainWindowViewModel.MatchCase
                        ? StringComparison.InvariantCulture
                        : StringComparison.InvariantCultureIgnoreCase;

                    if (_mainWindowViewModel.OnlySearchFileNames)
                    {
                        if (!result.FileName.Contains(searchTerm, stringComparison))
                        {
                            includeInResults = false;
                            break;
                        }
                    }
                    else
                    {
                        if (!result.FullFilePath.Contains(searchTerm, stringComparison))
                        {
                            includeInResults = false;
                            break;
                        }
                    }
                }

                if (includeInResults)
                {
                    filteredResults.Add(result);
                }
            }

            return filteredResults;
        }

        private Config LoadConfig()
        {
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();

                while (true)
                {
                    try
                    {
                        using StreamReader reader = File.OpenText(ConfigPath);
                        Config config = deserializer.Deserialize<Config>(reader);
                        return config;
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("IO exception. Try again...");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("FileNotFound.");
                Config config = new();
                SaveConfig(config);
                return config;
            }
        }

        private void SaveConfig(Config config)
        {
            ISerializer serializer = new SerializerBuilder()
                .Build();
            string yaml = serializer.Serialize(config);
            File.WriteAllText(ConfigPath, yaml);
        }

        private CancellationTokenSource? cts = new();
        private readonly object _allResultsLock = new();

        public void InitializeFileWatchersForFolders(CancellationToken ctsToken = default)
        {
            void CleanupFileWatchers()
            {
                // Clean up old file system watchers.
                foreach (FileSystemWatcher fileSystemWatcher in _fileSystemWatchersForFolders)
                {
                    fileSystemWatcher.Dispose();
                }

                _fileSystemWatchersForFolders.Clear();
            }

            CleanupFileWatchers();
            Console.WriteLine($"Initialize filesystem watchers for {AppConfig.Folders.Count}");


            foreach (string folder in AppConfig.Folders)
            {
                FileSystemWatcher fileSystemWatcherForFolder = new()
                {
                    Path = folder,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                };

                fileSystemWatcherForFolder.Created += (sender, args) =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        // Ignore event, the system watcher is now longer needed.
                        return;
                    }

                    Console.WriteLine($"Created: {args.FullPath}");
                    lock (_allResultsLock)
                    {
                        if (_allResultsFilePaths.Add(args.FullPath))
                        {
                            _allResults.Add(new Result(args.FullPath));
                        }
                    }

                    // _lastUpdate = DateTime.Now;
                    ApplySearchBoxFilter();
                };

                fileSystemWatcherForFolder.Deleted += (sender, args) =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        // Ignore event, the system watcher is now longer needed.
                        return;
                    }

                    Console.WriteLine($"Deleted: {args.FullPath}");
                    lock (_allResultsLock)
                    {
                        if (_allResultsFilePaths.Contains(args.FullPath))
                        {
                            bool isDirectory = Directory.Exists(args.FullPath);

                            if (_allResultsFilePaths.Remove(args.FullPath))
                            {
                                _allResults.Remove(_allResults.First(r => r.FullFilePath == args.FullPath));
                                if (isDirectory)
                                {
                                    _allResults.RemoveAll(r => r.FullFilePath.StartsWith(args.FullPath));
                                }
                            }
                        }
                    }

                    // _lastUpdate = DateTime.Now;
                    ApplySearchBoxFilter();
                };

                fileSystemWatcherForFolder.Renamed += (sender, args) =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        // Ignore event, the system watcher is now longer needed.
                        return;
                    }

                    Console.WriteLine($"Renamed: {args.OldFullPath} -> {args.FullPath}");
                    lock (_allResultsLock)
                    {
                        // Remove old filename
                        if (_allResultsFilePaths.Remove(args.OldFullPath))
                        {
                            _allResults.Remove(_allResults.First(r => r.FullFilePath == args.OldFullPath));
                        }

                        // Add new filename
                        if (_allResultsFilePaths.Add(args.FullPath))
                        {
                            _allResults.Add(new Result(args.FullPath));
                        }
                    }

                    // _lastUpdate = DateTime.Now;
                    ApplySearchBoxFilter();
                };

                _fileSystemWatchersForFolders.Add(fileSystemWatcherForFolder);
                Console.WriteLine($"Created FileSystemWatcher for directory: {folder}");

                if (cts.IsCancellationRequested)
                {
                    break;
                }
            }

            if (cts.IsCancellationRequested)
            {
                Console.WriteLine("InitializeFileWatchersForFolders cancelled. Cleanup file watchers");
                CleanupFileWatchers();
            }
        }

        private void ExitApp(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void EditConfig(object sender, RoutedEventArgs e)
        {
            Helper.OpenPath(ConfigPath);
        }

        private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private readonly object _applySearchBoxFilterLock = new();

        private void ApplySearchBoxFilter()
        {
            // bool doUpdate = false;
            // lock (_applySearchBoxFilterLock)
            // {
            //     doUpdate = _lastUpdate > _lastSearch;
            //     _lastSearch = DateTime.Now;
            // }
            //
            // if (!doUpdate)
            // {
            //     Console.WriteLine("");
            //     return;
            // }

            try
            {
                Helper.RunOnDispatcher(() =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    string currentText = SearchBox.Text.Trim();
                    UpdateResults(string.IsNullOrWhiteSpace(currentText) ? _allResults : FilterResults(currentText));
                    // _lastUpdate = DateTime.Now;    
                });
            }
            finally
            {
                Helper.RunOnDispatcher(() => Mouse.OverrideCursor = null);
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (true)
            {
                DependencyObject parentObject = VisualTreeHelper.GetParent(child);

                if (parentObject == null) return null;

                if (parentObject is T parent) return parent;

                child = parentObject;
            }
        }

        private void ResultsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGrid dataGrid = sender as DataGrid;
            if (dataGrid != null && dataGrid.SelectedItem != null && dataGrid.SelectedCells.Count > 0)
            {
                // DataGridCell cell = Helper.GetCell(dataGrid, dataGrid.SelectedIndex,
                //     dataGrid.CurrentCell.Column.DisplayIndex);
                DependencyObject source = (DependencyObject)e.OriginalSource;
                DataGridCell cell = source as DataGridCell ?? FindParent<DataGridCell>(source);

                Result selectedResult = dataGrid.SelectedItem as Result;
                string? header = cell.Column.Header as string;
                Helper.OpenPath(header == "Directory"
                    ? selectedResult.FilePath
                    : Path.Combine(selectedResult.FilePath, selectedResult.FileName));
            }
        }

        private void OpenSelectedResult(object sender, RoutedEventArgs e)
        {
            if (_mainWindowViewModel.SelectedResult is null)
            {
                return;
            }

            Helper.OpenPath(Path.Combine(
                _mainWindowViewModel.SelectedResult.FilePath,
                _mainWindowViewModel.SelectedResult.FileName));
        }

        private void OpenPathSelectedResult(object sender, RoutedEventArgs e)
        {
            if (_mainWindowViewModel.SelectedResult is null)
            {
                return;
            }

            Helper.OpenPath(_mainWindowViewModel.SelectedResult.FilePath);
        }

        private void CopyPathSelectedResult(object sender, RoutedEventArgs e)
        {
            if (_mainWindowViewModel.SelectedResult is null)
            {
                return;
            }

            Clipboard.SetText(Path.Combine(
                _mainWindowViewModel.SelectedResult.FilePath,
                _mainWindowViewModel.SelectedResult.FileName));
        }

        private void MainWindow_OnActivated(object? sender, EventArgs e)
        {
            SearchBox.Focus();
        }

        private void ResultsDataGrid_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindowViewModel.SelectedResult is null)
            {
                return;
            }

            ShellContextMenu ctxMnu = new ShellContextMenu();
            FileInfo[] arrFI = new FileInfo[1];
            arrFI[0] = new FileInfo(_mainWindowViewModel.SelectedResult.FullFilePath);
            ctxMnu.ShowContextMenu(arrFI,
                new System.Drawing.Point(System.Windows.Forms.Cursor.Position.X,
                    System.Windows.Forms.Cursor.Position.Y));
        }

        private void ResultsDataGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var result = VisualTreeHelper.HitTest(ResultsDataGrid, e.GetPosition(ResultsDataGrid));

            // walk up the visual tree to find DataGridRow
            DependencyObject visual = result.VisualHit;
            while (visual != null && !(visual is DataGridRow))
            {
                visual = VisualTreeHelper.GetParent(visual);
            }

            // if a DataGridRow was clicked, select it
            if (visual is DataGridRow row)
            {
                _mainWindowViewModel.SelectedResult = row.Item as Result;
                // row.IsSelected = true;
            }
        }

        private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                ResultsDataGrid.Focus();
            }
        }


        private void ReloadConfigAndResults()
        {
            if (_mainWindowViewModel.IsLoading)
            {
                // TODO: Would be nice if loading the config could be cancelled...
                MessageBox.Show("Please wait until the config has been reloaded, then try again.", "Error");
                return;
            }

            try
            {
                _mainWindowViewModel.IsLoading = true;
                AppConfig = LoadConfig();
                InitializeAllResults();
                InitializeFileWatchersForFolders();
                ApplySearchBoxFilter();
            }
            finally
            {
                _mainWindowViewModel.IsLoading = false;
            }
        }

        private async void ReloadConfigClicked(object sender, RoutedEventArgs e)
        {
            await Task.Run(ReloadConfigAndResults).ConfigureAwait(false);
        }

        private void ResultsDataGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is Result result)
            {
                // Initialize the drag & drop operation
                string[] files = { result.FullFilePath };
                DataObject dragData = new(DataFormats.FileDrop, files);
                System.Windows.DragDrop.DoDragDrop((DependencyObject)e.OriginalSource, dragData, DragDropEffects.Move);
            }
        }

        private void CopySelectedItem(object sender, ExecutedRoutedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is Result result)
            {
                StringCollection paths = new() { result.FullFilePath };
                Console.WriteLine($"SetFileDropList to {result.FullFilePath}");
                Clipboard.SetFileDropList(paths);
            }
        }

        private void DeleteSelectedResults(object sender, ExecutedRoutedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItems.Count <= 0)
            {
                return;
            }

            foreach (Result result in ResultsDataGrid.SelectedItems)
            {
                if (File.Exists(result.FullFilePath))
                {
                    FileSystem.DeleteFile(result.FullFilePath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }

                if (Directory.Exists(result.FullFilePath))
                {
                    FileSystem.DeleteDirectory(result.FullFilePath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
            }
        }
    }
}