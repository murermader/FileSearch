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
        FileInfo fileInfo = new FileInfo(filePath);
        FilePath = fileInfo.DirectoryName;
        FileName = fileInfo.Name;
        
        // Do not calcualte sizes for directories
        if (File.Exists(filePath))
        {
            FileSize = Math.Round(new FileInfo(filePath).Length / 1024.0, 3);
            FileType = fileInfo.Extension.TrimStart('.').ToUpperInvariant();
        }
    }

    public string FileName { get; set; }
    
    public string FileType { get; set; }
    
    public string FilePath { get; set; }
    
    public double FileSize { get; set; }

    public string FullFilePath => Path.Combine(FilePath, FileName);

    // public int RunCount { get; set; }
}