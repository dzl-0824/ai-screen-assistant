param([switch]$SkipRecordingRuntime)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$deps=Join-Path $root '.deps'
$out=Join-Path $root 'dist'
New-Item -ItemType Directory -Force $deps,$out | Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
function Restore-Package([string]$name,[string]$version,[string]$folder){
    $target=Join-Path $deps $folder
    if(!(Test-Path $target)){
        $zip=Join-Path $deps "$folder.zip"
        Invoke-WebRequest -UseBasicParsing "https://api.nuget.org/v3-flatcontainer/$name/$version/$name.$version.nupkg" -OutFile $zip
        Expand-Archive -LiteralPath $zip -DestinationPath $target -Force
    }
    return $target
}
$naudio=Restore-Package 'naudio' '1.10.0' 'naudio'
$zxing=Restore-Package 'zxing.net' '0.16.11' 'zxing'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc=Join-Path $framework 'csc.exe'
$wpf=Join-Path $framework 'WPF'
if(!(Test-Path $csc)){throw '.NET Framework 4.x compiler was not found.'}
$argsList=@('/nologo','/target:winexe',('/out:"'+(Join-Path $out 'ScreenText.exe')+'"'),('/win32icon:"'+(Join-Path $root 'assets/app.ico')+'"'))
$resources=@{
    'ModernMain'='src/ModernMain.xaml'; 'ModernResult'='src/ModernResult.xaml';
    'AppIcon'='assets/app.ico'; 'DoneSound'='assets/done.wav';
    'RecordingOptions'='src/RecordingOptions.xaml'; 'RecordingControl'='src/RecordingControl.xaml';
    'ShotEditor'='src/ShotEditor.xaml'; 'ShotPin'='src/ShotPin.xaml';
    'QrLicense'='licenses/ZXing-LICENSE.txt'
}
foreach($name in $resources.Keys){$argsList+=('/resource:"'+(Join-Path $root $resources[$name])+'",'+$name)}
$argsList+=('/resource:"'+(Join-Path $naudio 'lib/net35/NAudio.dll')+'",RecordingAudioLibrary')
$argsList+=('/resource:"'+(Join-Path $zxing 'lib/net40/zxing.dll')+'",QrLibrary')
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','Microsoft.CSharp.dll','System.Data.dll',(Join-Path $naudio 'lib/net35/NAudio.dll'),(Join-Path $zxing 'lib/net40/zxing.dll'),(Join-Path $framework 'System.Xaml.dll'))
foreach($name in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','WindowsFormsIntegration.dll')){$refs+=Join-Path $wpf $name}
foreach($ref in $refs){$argsList+=('/reference:"'+$ref+'"')}
foreach($file in Get-ChildItem (Join-Path $root 'src') -Filter '*.cs'){$argsList+='"'+$file.FullName+'"'}
$rsp=Join-Path $deps 'build.rsp'
Set-Content -Encoding UTF8 $rsp $argsList
& $csc "@$rsp"
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
if(!$SkipRecordingRuntime){
    $runtime=Join-Path $out 'runtime'
    New-Item -ItemType Directory -Force $runtime | Out-Null
    $ffmpeg=Join-Path $deps 'ffmpeg/ffmpeg.exe'
    if(!(Test-Path $ffmpeg)){
        $zip=Join-Path $deps 'ffmpeg.zip'
        Invoke-WebRequest -UseBasicParsing 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zip
        $extract=Join-Path $deps 'ffmpeg-extracted'
        Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
        $binary=Get-ChildItem $extract -Filter 'ffmpeg.exe' -Recurse | Select-Object -First 1
        if(!$binary){throw 'FFmpeg executable was not found in the archive.'}
        New-Item -ItemType Directory -Force (Split-Path $ffmpeg) | Out-Null
        Copy-Item -LiteralPath $binary.FullName -Destination $ffmpeg
    }
    Copy-Item -LiteralPath $ffmpeg -Destination (Join-Path $runtime 'ffmpeg.exe')
    Copy-Item (Join-Path $root 'licenses/*.txt') -Destination $runtime
}
Write-Host "Built: $(Join-Path $out 'ScreenText.exe')"
