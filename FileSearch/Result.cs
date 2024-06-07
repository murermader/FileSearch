using System;
using System.IO;

namespace FileSearch;

public class Result
{
    public Result()
    {
        
    }
    
    public Result(string filePath)
    {
        FileInfo fileInfo = new(filePath);
        FilePath = fileInfo.DirectoryName;
        FileName = fileInfo.Name;

        try
        {
            // Do not calcualte sizes for directories
            if (File.Exists(filePath))
            {
                FileSize = Math.Round(new FileInfo(filePath).Length / 1024.0, 3);
                FileType = fileInfo.Extension.TrimStart('.').ToUpperInvariant();            
            }
        }
        catch (FileNotFoundException)
        {
            // File was deleted while we were adding it. It probably happened very fast.
            // We can ignore the exception, because the file will be removed as soon as the delete event
            // by the file watcher triggers.
        }
    }

    private string? _fullFilePath; 

    public string FileName { get; set; }
    
    public string FileType { get; set; }
    
    public string FilePath { get; set; }
    
    public double FileSize { get; set; }

    public string FullFilePath
    {
        get
        {
            if (_fullFilePath is not null)
            {
                return _fullFilePath;
            }
            _fullFilePath = Path.Combine(FilePath, FileName);
            return _fullFilePath;
        }
    }

    // public int RunCount { get; set; }
}