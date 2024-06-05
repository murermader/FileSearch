using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FileSearch;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel()
    {
        // For Designer
    }
    
    public MainWindowViewModel(Action onConfiugrationChanged)
    {
        _onConfiugrationChanged = onConfiugrationChanged;
    }

    private readonly Action _onConfiugrationChanged;   
    private bool _onlySearchFileNames;
    private ObservableCollection<Result> _results = new();
    private bool _matchCase;
    private Result? _selectedResult;
    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (value == _isLoading) return;
            Console.WriteLine($"IsLoading={value}");
            Mouse.OverrideCursor = value ? Cursors.Wait : null;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Result> Results
    {
        get => _results;
        set
        {
            if (Equals(value, _results)) return;
            _results = value;
            OnPropertyChanged();
        }
    }

    public Result? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (Equals(value, _selectedResult)) return;
            _selectedResult = value;
            OnPropertyChanged();
        }
    }

    public bool OnlySearchFileNames
    {
        get => _onlySearchFileNames;
        set
        {
            if (value == _onlySearchFileNames) return;
            _onlySearchFileNames = value;
            _onConfiugrationChanged.Invoke();
            OnPropertyChanged();
        }
    }

    public bool MatchCase
    {
        get => _matchCase;
        set
        {
            if (value == _matchCase) return;
            _matchCase = value;
            _onConfiugrationChanged.Invoke();
            OnPropertyChanged();
        }
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
    
}