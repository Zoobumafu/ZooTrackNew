# ASP.NET Project Tree Export Script
# This script creates a filtered tree view of your project showing only relevant file types

param(
    [string]$ProjectPath = ".",
    [string]$OutputFile = "project-tree.txt"
)

# Define the file extensions to include
$IncludeExtensions = @(
    "*.cs",      # C# source files
    "*.razor",   # Razor components
    "*.blazor",  # Blazor files
    "*.html",    # HTML files
    "*.css",     # CSS stylesheets
    "*.json",    # JSON configuration files
    "*.txt",     # Text files
    "*.sln",     # Solution files
    "*.csproj",  # C# project files
    "*.db",      # Database files
    "*.onnx",    # ONNX model files
    "*.js"       # JavaScript files
)

# Directories to exclude from the tree
$ExcludeDirectories = @(
    "bin",
    "obj", 
    "node_modules",
    ".vs",
    ".git",
    "packages",
    "TestResults",
    ".vscode"
)

function Get-ProjectTree {
    param(
        [string]$Path,
        [string]$Prefix = "",
        [bool]$IsLast = $true
    )
    
    $items = @()
    
    # Get directories first
    $directories = Get-ChildItem -Path $Path -Directory | Where-Object { 
        $_.Name -notin $ExcludeDirectories 
    } | Sort-Object Name
    
    # Get files with specified extensions
    $files = @()
    foreach ($ext in $IncludeExtensions) {
        $files += Get-ChildItem -Path $Path -File -Filter $ext
    }
    $files = $files | Sort-Object Name | Get-Unique -AsString
    
    $allItems = @($directories) + @($files)
    
    for ($i = 0; $i -lt $allItems.Count; $i++) {
        $item = $allItems[$i]
        $isLastItem = ($i -eq ($allItems.Count - 1))
        
        if ($isLastItem) {
            $currentPrefix = "+-- "
            $nextPrefix = $Prefix + "    "
        } else {
            $currentPrefix = "|-- "
            $nextPrefix = $Prefix + "|   "
        }
        
        $items += "$Prefix$currentPrefix$($item.Name)"
        
        # If it's a directory, recurse into it
        if ($item.PSIsContainer) {
            $items += Get-ProjectTree -Path $item.FullName -Prefix $nextPrefix -IsLast $isLastItem
        }
    }
    
    return $items
}

# Main execution
try {
    Write-Host "Generating ASP.NET project tree..." -ForegroundColor Green
    Write-Host "Project Path: $ProjectPath" -ForegroundColor Cyan
    Write-Host "Output File: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    
    # Resolve the full path
    $FullPath = Resolve-Path $ProjectPath -ErrorAction Stop
    $ProjectName = Split-Path $FullPath -Leaf
    
    # Generate the tree
    $treeContent = @()
    $treeContent += "ASP.NET Project Structure: $ProjectName"
    $treeContent += "Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $treeContent += "Path: $FullPath"
    $treeContent += ""
    $treeContent += "Included file types: $($IncludeExtensions -join ', ')"
    $treeContent += "Excluded directories: $($ExcludeDirectories -join ', ')"
    $treeContent += ""
    $treeContent += "$ProjectName/"
    $treeContent += Get-ProjectTree -Path $FullPath
    
    # Write to file
    $treeContent | Out-File -FilePath $OutputFile -Encoding UTF8
    
    # Display summary
    $fileCount = ($treeContent | Where-Object { $_ -match '\.(cs|razor|blazor|html|css|json|txt|sln|csproj|db|onnx|js)$' }).Count
    
    Write-Host "Tree generated successfully!" -ForegroundColor Green
    Write-Host "Total files included: $fileCount" -ForegroundColor Yellow
    Write-Host "Output saved to: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Preview of the tree structure:" -ForegroundColor Magenta
    Write-Host "=" * 50
    
    # Show first 20 lines as preview
    $preview = Get-Content $OutputFile | Select-Object -First 20
    $preview | ForEach-Object { Write-Host $_ }
    
    if ($treeContent.Count -gt 20) {
        Write-Host "... (truncated, see $OutputFile for complete tree)" -ForegroundColor Gray
    }
    
} catch {
    Write-Error "Error generating project tree: $($_.Exception.Message)"
    exit 1
}

# Usage examples in comments:
<#
Usage Examples:

1. Generate tree for current directory:
   .\project-tree.ps1

2. Generate tree for specific project path:
   .\project-tree.ps1 -ProjectPath "C:\Projects\MyApp" -OutputFile "MyApp-structure.txt"

3. Generate tree with custom output file:
   .\project-tree.ps1 -OutputFile "detailed-structure.txt"
#>