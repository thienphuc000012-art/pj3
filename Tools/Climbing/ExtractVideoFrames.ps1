Add-Type -AssemblyName PresentationCore,WindowsBase
$playerMedia = New-Object System.Windows.Media.MediaPlayer
$playerMedia.Open([Uri]'C:/Users/Phuc/Videos/Screen Recordings/Screen Recording 2026-11-14 093104.mp4')
$playerMedia.Volume = 0
$playerMedia.ScrubbingEnabled = $true
$playerMedia.Play()
$timeLimit = [DateTime]::UtcNow.AddSeconds(8)
while (-not $playerMedia.NaturalDuration.HasTimeSpan -and [DateTime]::UtcNow -lt $timeLimit) {
    [System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
    Start-Sleep -Milliseconds 40
}
if (-not $playerMedia.NaturalDuration.HasTimeSpan) { throw 'Video did not open' }
$lengthSeconds = $playerMedia.NaturalDuration.TimeSpan.TotalSeconds
Write-Output ('duration=' + $lengthSeconds + ' width=' + $playerMedia.NaturalVideoWidth + ' height=' + $playerMedia.NaturalVideoHeight)
for ($frameIndex = 0; $frameIndex -lt 16; $frameIndex++) {
    $atSeconds = $frameIndex * ($lengthSeconds - 0.2) / 15
    $playerMedia.Position = [TimeSpan]::FromSeconds($atSeconds)
    $playerMedia.Play()
    $seekUntil = [DateTime]::UtcNow.AddMilliseconds(350)
    while ([DateTime]::UtcNow -lt $seekUntil) {
        [System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
        Start-Sleep -Milliseconds 10
    }
    $playerMedia.Pause()
    $visual = New-Object System.Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.DrawVideo($playerMedia, [System.Windows.Rect]::new(0,0,960,540))
    $drawing.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(960,540,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $fileName = 'Tools/Climbing/ReferenceFrames/frame-' + $frameIndex.ToString('00') + '-' + $atSeconds.ToString('F2') + '.png'
    $stream = [System.IO.File]::Create([System.IO.Path]::GetFullPath($fileName))
    $encoder.Save($stream)
    $stream.Dispose()
}
$playerMedia.Close()

