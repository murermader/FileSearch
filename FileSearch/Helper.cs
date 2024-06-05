using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FileSearch;

public static class Helper
{
    private const long OneKb = 1024;
    private const long OneMb = OneKb * 1024;
    private const long OneGb = OneMb * 1024;
    private const long OneTb = OneGb * 1024;

    public static string ToPrettySize(int value, int decimalPlaces = 0)
    {
        return ToPrettySize((long)value, decimalPlaces);
    }

    public static string ToPrettySize(long value, int decimalPlaces = 0)
    {
        double asTb = Math.Round((double)value / OneTb, decimalPlaces);
        double asGb = Math.Round((double)value / OneGb, decimalPlaces);
        double asMb = Math.Round((double)value / OneMb, decimalPlaces);
        double asKb = Math.Round((double)value / OneKb, decimalPlaces);
        string chosenValue = asTb > 1 ? $"{asTb} TB"
            : asGb > 1 ? $"{asGb} GB"
            : asMb > 1 ? $"{asMb} MB"
            : asKb > 1 ? $"{asKb} KB"
            : $"{Math.Round((double)value, decimalPlaces)}B";
        return chosenValue;
    }

    public static void OpenPath(string filePath)
    {
        using Process fileopener = new Process();
        fileopener.StartInfo.FileName = "explorer";
        fileopener.StartInfo.Arguments = $"\"{filePath}\"";
        fileopener.Start();
    }

    public static DataGridCell GetCell(DataGrid dataGrid, int row, int column)
    {
        DataGridRow rowContainer = GetRow(dataGrid, row);
        if (rowContainer == null)
        {
            return null;
        }

        DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(rowContainer);

        // Try to get the cell but it may possibly be virtualized
        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
        if (cell == null)
        {
            // Now try to bring into view and retry
            dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
            cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
        }

        return cell;

    }

    public static DataGridRow GetRow(DataGrid dataGrid, int index)
    {
        DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
        if (row == null)
        {
            // May be virtualized, bring into view and try again
            dataGrid.ScrollIntoView(dataGrid.Items[index]);
            row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
        }

        return row;
    }

    public static T GetVisualChild<T>(Visual parent) where T : Visual
    {
        T child = default(T);

        int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < numVisuals; i++)
        {
            Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
            child = v as T;
            if (child == null)
                child = GetVisualChild<T>(v);
            if (child != null)
                break;
        }

        return child;
    }
}