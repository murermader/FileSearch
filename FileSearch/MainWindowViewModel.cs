﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using DragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace FileSearch;

public class MainWindowViewModel : INotifyPropertyChanged, IDropTarget
{
    public MainWindowViewModel()
    {
        // For Designer
    }

    public MainWindowViewModel(Action onConfiugrationChanged, Action<string[]> doDragDrop)
    {
        _onConfiugrationChanged = onConfiugrationChanged;
        _doDragDrop = doDragDrop;
    }

    private readonly Action _onConfiugrationChanged;
    private readonly Action<string[]> _doDragDrop;
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
            Helper.RunOnDispatcher(() => Mouse.OverrideCursor = value ? Cursors.Wait : null);
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

    #region IDropTarget

    public void DragOver(IDropInfo dropInfo)
    {
        throw new NotImplementedException();
    }

    public void Drop(IDropInfo dropInfo)
    {
        // TODO: https://github.com/punker76/gong-wpf-dragdrop/pull/271/commits/064f11df52c56bea8d0e42663f915a7edf0c7216
        dropInfo.Effects = DragDropEffects.Move;
        
        List<string> fileList = new List<string>();
        if (dropInfo.Data is Result result)
        {
            fileList.Add(result.FullFilePath);
        }
        else if (dropInfo.Data is IEnumerable<Result> results)
        {
            fileList.AddRange(results.Select(r => r.FullFilePath));
        }
        
        _doDragDrop.Invoke(fileList.ToArray());
        
        // DataObject dragData = new(DataFormats.FileDrop, fileList.ToArray());
        // // DragDrop.DoDragDrop((DependencyObject)e.OriginalSource, dragData, DragDropEffects.Move);
        // System.Windows.DragDrop.DoDragDrop((DependencyObject)e.OriginalSource, dragData, DragDropEffects.Move);
    }

    #endregion
}