param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath,

    [Parameter(Mandatory)]
    [string]$ImagePath,

    [int]$Runs = 10,

    [int]$TimeoutMilliseconds = 10000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class PicaStartupBenchmarkNativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern uint GetPixel(IntPtr hdc, int x, int y);
}
'@

$resolvedExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$resolvedImagePath = (Resolve-Path -LiteralPath $ImagePath).Path
$results = [System.Collections.Generic.List[object]]::new()

for ($run = 1; $run -le $Runs; $run++)
{
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process `
        -FilePath $resolvedExecutablePath `
        -ArgumentList @($resolvedImagePath) `
        -PassThru
    $windowHandle = [IntPtr]::Zero
    $windowHandleMilliseconds = $null
    $windowVisibleMilliseconds = $null
    $firstImageMilliseconds = $null
    $screenDeviceContext =
        [PicaStartupBenchmarkNativeMethods]::GetDC([IntPtr]::Zero)

    try
    {
        while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds)
        {
            $process.Refresh()

            if ($process.HasExited)
            {
                break
            }

            if (($windowHandle -eq [IntPtr]::Zero) -and
                ($process.MainWindowHandle -ne [IntPtr]::Zero))
            {
                $windowHandle = $process.MainWindowHandle
                $windowHandleMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }

            if ($windowHandle -ne [IntPtr]::Zero)
            {
                if (($null -eq $windowVisibleMilliseconds) -and
                    [PicaStartupBenchmarkNativeMethods]::IsWindowVisible(
                        $windowHandle))
                {
                    $windowVisibleMilliseconds =
                        $stopwatch.Elapsed.TotalMilliseconds
                }

                if (($null -ne $windowVisibleMilliseconds) -and
                    ($stopwatch.Elapsed.TotalMilliseconds -gt
                        $windowVisibleMilliseconds + 10))
                {
                    $windowRectangle =
                        [PicaStartupBenchmarkNativeMethods+RECT]::new()
                    [void][PicaStartupBenchmarkNativeMethods]::GetWindowRect(
                        $windowHandle,
                        [ref]$windowRectangle)
                    $centerX = [int](
                        ($windowRectangle.Left + $windowRectangle.Right) / 2)
                    $centerY = [int](
                        ($windowRectangle.Top + $windowRectangle.Bottom) / 2)
                    $color = [PicaStartupBenchmarkNativeMethods]::GetPixel(
                        $screenDeviceContext,
                        $centerX,
                        $centerY)

                    if (($color -ne 0) -and ($color -ne 0xFFFFFFFF))
                    {
                        $firstImageMilliseconds =
                            $stopwatch.Elapsed.TotalMilliseconds
                        break
                    }
                }
            }

            Start-Sleep -Milliseconds 2
        }
    }
    finally
    {
        [void][PicaStartupBenchmarkNativeMethods]::ReleaseDC(
            [IntPtr]::Zero,
            $screenDeviceContext)

        if (!$process.HasExited)
        {
            [void]$process.CloseMainWindow()

            if (!$process.WaitForExit(3000))
            {
                Stop-Process -Id $process.Id -Force
            }
        }
    }

    $results.Add([pscustomobject]@{
        Run = $run
        WindowHandleMilliseconds =
            [math]::Round($windowHandleMilliseconds, 1)
        WindowVisibleMilliseconds =
            [math]::Round($windowVisibleMilliseconds, 1)
        FirstImageMilliseconds =
            [math]::Round($firstImageMilliseconds, 1)
        ExitedBeforeWindow = $process.HasExited `
            -and ($null -eq $windowHandleMilliseconds)
    })

    Start-Sleep -Milliseconds 150
}

$summary = [ordered]@{
    ExecutablePath = $resolvedExecutablePath
    ImagePath = $resolvedImagePath
    Runs = $results
}

foreach ($propertyName in @(
    'WindowHandleMilliseconds',
    'WindowVisibleMilliseconds',
    'FirstImageMilliseconds'))
{
    $values = @(
        $results |
            ForEach-Object { $_.$propertyName } |
            Where-Object { $null -ne $_ } |
            Sort-Object)

    if ($values.Count -gt 0)
    {
        $summary["${propertyName}Median"] =
            $values[[math]::Floor($values.Count / 2)]
        $summary["${propertyName}Minimum"] = $values[0]
        $summary["${propertyName}Maximum"] = $values[-1]
    }
}

[pscustomobject]$summary | ConvertTo-Json -Depth 5
