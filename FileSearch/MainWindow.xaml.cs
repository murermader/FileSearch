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
            AppConfig = LoadConfig();
            StartWatching();
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                ApplySearchBoxFilter();
            };

            // CTRL+F shortcut
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
            
            // Hide window with ESC
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
            
            Task.Run(() =>
            {
                try
                {
                    _mainWindowViewModel.IsLoading = true;
                    InitializeAllResults();
                    ApplySearchBoxFilter();
                }
                finally
                {
                    _mainWindowViewModel.IsLoading = false;
                }
            }).ConfigureAwait(false);

        }

        private FileSystemWatcher _fileSystemWatcher;
        private readonly DispatcherTimer _searchTimer;
        private List<Result> _allResults = new();
        private readonly MainWindowViewModel _mainWindowViewModel;

        public Config AppConfig { get; set; }

        public string ConfigPath { get; }

        public static void FocusSearchBoxCommand()
        {
            Console.WriteLine("test");
        }

        private void InitializeAllResults(CancellationToken ctsToken = default)
        {
            HashSet<string> uniqueFilePaths = new HashSet<string>();
            foreach (string folder in AppConfig.Folders)
            {
                Console.WriteLine($"Include folder: {folder}");

                foreach (string directory in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
                {
                    if (ctsToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    
                    if (!AppConfig.IncludeHiddenFiles &&
                        (new DirectoryInfo(directory).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        continue;
                    }

                    uniqueFilePaths.Add(directory);
                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        if (ctsToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }
                        
                        if (!AppConfig.IncludeHiddenFiles && (new FileInfo(file).Attributes & FileAttributes.Hidden) ==
                            FileAttributes.Hidden)
                        {
                            continue;
                        }

                        uniqueFilePaths.Add(file);
                    }
                }
            }

            _allResults = uniqueFilePaths.Select(filePath => new Result(filePath)).ToList();
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
                using StreamReader reader = File.OpenText(ConfigPath);
                Config config = deserializer.Deserialize<Config>(reader);
                return config;
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("FileNotFound.");
                Config config = new Config();
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

        private CancellationTokenSource cts = new CancellationTokenSource();
        
        public void StartWatching()
        {
            Console.WriteLine("StartWatching");
            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Filter = "Config.yaml",
                NotifyFilter = NotifyFilters.LastWrite
            };
            
            _fileSystemWatcher.Changed += async (sender, args) =>
            {
                cts.Cancel();
                cts = new CancellationTokenSource();

                _mainWindowViewModel.IsLoading = true;

                try
                {
                    await Task.Delay(100);
                    Console.WriteLine("Config Changed! Reloading...");
                    AppConfig = LoadConfig();
                    InitializeAllResults(cts.Token);
                    ApplySearchBoxFilter();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Cancelled");
                }
                finally
                {
                    _mainWindowViewModel.IsLoading = false;
                }
            };

            _fileSystemWatcher.EnableRaisingEvents = true;
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

        private void ApplySearchBoxFilter()
        {
            try
            {
                Helper.RunOnDispatcher(() =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    string currentText = SearchBox.Text.Trim();
                    UpdateResults(string.IsNullOrWhiteSpace(currentText) ? _allResults : FilterResults(currentText));
                });
            }
            finally
            {
                Helper.RunOnDispatcher(() => Mouse.OverrideCursor = null);
            }
        }
        
        private static T FindParent<T>(DependencyObject child) where T: DependencyObject
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
    }
}