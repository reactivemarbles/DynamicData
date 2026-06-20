param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$sourceRootPath = (Resolve-Path $SourceRoot).Path
$outputRootPath = [IO.Path]::GetFullPath($OutputRoot)
$mutex = New-Object System.Threading.Mutex($false, 'Local\DynamicDataReactiveSourceRewrite')
$mutexHeld = $false

function Get-SourceFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    Get-ChildItem -LiteralPath $Path -File -Filter '*.cs'

    Get-ChildItem -LiteralPath $Path -Directory |
        Where-Object { $_.Name -ne 'bin' -and $_.Name -ne 'obj' } |
        ForEach-Object { Get-SourceFile -Path $_.FullName }
}

function Write-GeneratedSource {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Text
    )

    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        try {
            [IO.File]::WriteAllText($Path, $Text)
            return
        }
        catch {
            if ($attempt -eq 4) {
                throw
            }

            Start-Sleep -Milliseconds (50 * ($attempt + 1))
        }
    }
}

try {
    $mutexHeld = $mutex.WaitOne([TimeSpan]::FromMinutes(5))
    if (-not $mutexHeld) {
        throw "Timed out waiting for DynamicData.Reactive source rewrite lock."
    }

    if ((Split-Path -Leaf $outputRootPath) -ne 'GeneratedReactiveSources') {
        throw "Refusing to clean unexpected DynamicData.Reactive generated source directory '$outputRootPath'."
    }

    if (Test-Path -LiteralPath $outputRootPath) {
        Remove-Item -LiteralPath $outputRootPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

    Get-SourceFile -Path $sourceRootPath |
        Where-Object {
            $_.FullName -notlike '*\Internal\ObservableEx.cs' -and
            $_.FullName -notlike '*\Internal\ReactiveCompatibility.cs'
        } |
        ForEach-Object {
            $relative = $_.FullName.Substring($sourceRootPath.Length).TrimStart('\', '/')
            $destination = Join-Path $outputRootPath $relative
            $destinationDirectory = Split-Path -Parent $destination
            New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

            $text = Get-Content -LiteralPath $_.FullName -Raw
            $text = [regex]::Replace($text, '\bnamespace DynamicData(?=[\s.;{])', 'namespace DynamicData.Reactive')
            $text = [regex]::Replace($text, '\busing DynamicData(?=[.;])', 'using DynamicData.Reactive')
            $text = [regex]::Replace($text, '\bOptional<', 'ReactiveUI.Primitives.Optional<')
            $text = [regex]::Replace($text, '\bOptional\.', 'ReactiveUI.Primitives.Optional.')
            $text = $text.Replace('Signal.Lazy', 'Observable.Defer')
            $text = $text.Replace('Signal.After', 'Observable.Timer')
            $text = $text.Replace('Signal.FromAsync', 'Observable.FromAsync')
            $text = $text.Replace('Signal.Create', 'Observable.Create')
            $text = $text.Replace('.SubscribeSafe(observer.OnError, observer.OnCompleted)', '.Subscribe(_ => { }, observer.OnError, observer.OnCompleted)')
            $text = $text.Replace('.SubscribeSafe(', '.Subscribe(')
            $text = $text.Replace('.SynchronizeObject(', '.Synchronize(')
            $text = $text.Replace('.Map(', '.Select(')
            $text = $text.Replace('.Tap(', '.Do(')
            $text = $text.Replace('.Keep(', '.Where(')
            $text = $text.Replace('Scope.Create', 'Disposable.Create')
            $text = $text.Replace('Scope.Empty', 'Disposable.Empty')
            $text = $text.Replace('MultipleDisposable', 'CompositeDisposable')
            $text = $text.Replace('OnceDisposable', 'SingleAssignmentDisposable')
            $text = $text.Replace('SwapDisposable', 'SerialDisposable')
            $text = $text.Replace('ReplaySignal<', 'ReplaySubject<')
            $text = $text.Replace('BehaviorSignal<', 'BehaviorSubject<')
            $text = $text.Replace('StateSignal<', 'BehaviorSubject<')
            $text = $text.Replace('ISignal<', 'ISubject<')
            $text = [regex]::Replace($text, '\bSignal<', 'Subject<')

            Write-GeneratedSource -Path $destination -Text $text
        }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Error.WriteLine($_.ScriptStackTrace)
    exit 1
}
finally {
    if ($mutexHeld) {
        $mutex.ReleaseMutex()
    }

    $mutex.Dispose()
}
