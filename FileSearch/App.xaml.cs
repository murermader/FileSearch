using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Input;
using GlobalHotKey;

namespace FileSearch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? _mainWindow = null;
        private HotKeyManager _hotKeyManager = null;
        private HotKey registeredHotKey = null;

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            _hotKeyManager = new HotKeyManager();
            registeredHotKey = _hotKeyManager.Register(Key.Space, ModifierKeys.Alt);
            _hotKeyManager.KeyPressed += HotKeyManagerPressed;
            _mainWindow = new MainWindow();
            _mainWindow.Closing += MainWindowOnClosing;
        }

        private void MainWindowOnClosing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            if (_mainWindow is null)
            {
                return;
            }
            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
        }

        private void HotKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            if (_mainWindow is null)
            {
                return;
            }
            
            if (e.HotKey.Key == Key.Space && e.HotKey.Modifiers == ModifierKeys.Alt)
            {
                if (_mainWindow.IsActive)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    if (_mainWindow.WindowState == WindowState.Minimized)
                    {
                        _mainWindow.WindowState = WindowState.Normal;
                    }
                    _mainWindow.Show();
                    
                    // Bring window to front
                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                    
                    // Focus the window
                    _mainWindow.Activate();
                    _mainWindow.Focus();
                }
            }
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            // Unregister Ctrl+Alt+F5 hotkey.
            _hotKeyManager.Unregister(registeredHotKey);

            // Dispose the hotkey manager.
            _hotKeyManager.Dispose();
        }
    }
}