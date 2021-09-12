param ([Parameter(Mandatory=$true)][string]$versionInfoFile, [Parameter(Mandatory=$true)][string]$versionNumber)

$regex = [regex] '^\[assembly: (Assembly(File){0,1}Version)\("(.*)"\)\]$'
$file = Get-ChildItem 

(Get-Content -Path $versionInfoFile) |
ForEach-Object {
    if ($_ -Match $regex) {
        '[assembly: {0}("{1}")]' -f $matches[1], $versionNumber
    } else {
        $_
    }
} |
Out-File -Encoding utf8 -FilePath $versionInfoFile
