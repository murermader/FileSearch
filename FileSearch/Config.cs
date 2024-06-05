using System.Collections.Generic;

namespace FileSearch;

public class Config
{
    public bool IncludeHiddenFiles { get; set; } = false;
    
    public List<string> Folders { get; set; }
}