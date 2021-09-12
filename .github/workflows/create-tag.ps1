param ([Parameter(Mandatory=$true)][string]$githubRef, [Parameter(Mandatory=$true)][string]$versionNumber)

if ( $githubRef.StartsWith("refs/tags/") ) {
    $t = $githubRef.replace('refs/tags/v','').replace('refs/tags/','')
} else {
    [VERSION]$vs = $versionNumber -replace '^.+((\d+\.){3}\d+).+', '$1'
    $t = '{0}.{1}.{2}' -f $vs.Major,$vs.Minor,$vs.Build
}
echo "::set-output name=tag::$t"
