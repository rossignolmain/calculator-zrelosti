param([string]$OutputPath = (Join-Path $PSScriptRoot 'Калькулятор зрелости.exe'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Не найден компилятор .NET Framework 4. Установите .NET Framework 4.8 Developer Pack.' }
$refs = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Web.Extensions.dll', 'System.Drawing.dll', 'System.Xaml.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$sources = @('AssemblyInfo.cs','Data.cs','Model.cs','Reports.cs','App.cs','Verification.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /utf8output /target:winexe /platform:anycpu /optimize+ ('/out:' + $OutputPath) ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/win32icon:' + (Join-Path $PSScriptRoot 'app.ico')) $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибкой.' }
Write-Output ('Готово: ' + $OutputPath)
