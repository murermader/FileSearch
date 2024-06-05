using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Peter;
using YamlDotNet.Serialization;
using Path = System.IO.Path;

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
            _mainWindowViewModel = new MainWindowViewModel(ApplySearchBoxFilter);
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
            RoutedCommand focusSearchBoxCommand = new();
            CommandBinding focusSearchBoxCommandBinding = new(
                focusSearchBoxCommand,
                (sender, e) => SearchBox.Focus());
            CommandBindings.Add(focusSearchBoxCommandBinding);
            KeyBinding focusSearchBoxKeyBinding = new()
            {
                Command = focusSearchBoxCommand,
                Modifiers = ModifierKeys.Control,
                Key = Key.F
            };
            InputBindings.Add(focusSearchBoxKeyBinding);
            
            // Reload config
            RoutedCommand reloadConfigCommand = new();
            CommandBinding reloadConfigCommandBinding = new(
                reloadConfigCommand,
                async (sender, e) => await Task.Run(ReloadConfigAndResults).ConfigureAwait(false));
            CommandBindings.Add(reloadConfigCommandBinding);
            KeyBinding reloadConfigKeyBinding = new()
            {
                Command = reloadConfigCommand,
                Key = Key.F5
            };
            KeyBinding reloadConfigKeyBinding2 = new()
            {
                Command = reloadConfigCommand,
                Modifiers = ModifierKeys.Control,
                Key = Key.R
            };
            InputBindings.Add(reloadConfigKeyBinding);
            InputBindings.Add(reloadConfigKeyBinding2);

            // Hide window
            RoutedCommand hideWindowCommand = new();
            CommandBinding hideWindowCommandBinding = new(
                hideWindowCommand,
                (sender, e) => Hide());
            CommandBindings.Add(hideWindowCommandBinding);
            KeyBinding hideWindowKeyBinding = new()
            {
                Command = hideWindowCommand,
                Key = Key.Escape
            };
            InputBindings.Add(hideWindowKeyBinding);

            Task.Run(ReloadConfigAndResults).ConfigureAwait(false);
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

        private void InitializeAllResults(CancellationToken ctsToken = default)
        {
            foreach (string folder in AppConfig.Folders)
            {
                Console.WriteLine($"Include folder: {folder}");

                foreach (string directory in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
                {
                    if (ctsToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"InitializeAllResults cancelled at {folder}");
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

                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
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
                }
            }

            if (ctsToken.IsCancellationRequested)
            {
                // Cancelled. Do not update _allResults.
                Console.WriteLine($"InitializeAllResults cancelled");
                return;
            }

            lock (_allResultsFilePaths)
            {
                _allResults = _allResultsFilePaths.Select(filePath => new Result(filePath)).ToList();
            }

            Console.WriteLine($"Loaded {_allResults.Count} files.");
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

        private List<Result> FilterResults(string? searchText)
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
                        if (!result.FilePath.Contains(searchTerm, stringComparison) &&
                            !result.FileName.Contains(searchTerm, stringComparison))
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
                            if (_allResultsFilePaths.Remove(args.FullPath))
                            {
                                _allResults.Remove(_allResults.First(r => r.FullFilePath == args.FullPath));
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
    }
}