Add-Type -AssemblyName PresentationCore,WindowsBase
function Pump([double]$seconds) {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromSeconds($seconds)
    $timer.Add_Tick({ $frame.Continue = $false; $timer.Stop() }.GetNewClosure())
    $timer.Start(); [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}
$media = New-Object System.Windows.Media.MediaPlayer
$media.ScrubbingEnabled = $true
$media.Volume = 0
$media.Open([Uri]'C:/Users/Phuc/Videos/Screen Recordings/Screen Recording 2026-11-18 083656.mp4')
for ($i=0; $i -lt 30 -and !$media.NaturalDuration.HasTimeSpan; $i++) { Pump .2 }
if (!$media.NaturalDuration.HasTimeSpan) { throw 'Video did not open' }
$duration = $media.NaturalDuration.TimeSpan.TotalSeconds
Write-Output ('Duration: ' + $duration + ', size: ' + $media.NaturalVideoWidth + 'x' + $media.NaturalVideoHeight)
$media.Play(); Pump .4; $media.Pause()
$visual = New-Object System.Windows.Media.DrawingVisual
$context = $visual.RenderOpen()
for ($i=0; $i -lt 8; $i++) {
    $time = [Math]::Min($duration-.1, .1 + ($duration-.2)*$i/7)
    $media.Position = [TimeSpan]::FromSeconds($time); Pump .5
    $frameVisual = New-Object System.Windows.Media.DrawingVisual
    $draw = $frameVisual.RenderOpen()
    $draw.DrawVideo($media, (New-Object System.Windows.Rect(0,0,640,360))); $draw.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(640,360,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($frameVisual); $bitmap.Freeze()
    $context.DrawImage($bitmap, (New-Object System.Windows.Rect(($i%2*640),([Math]::Floor($i/2)*360),640,360)))
}
$context.Close()
$sheet = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(1280,1440,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($sheet))
$stream = [IO.File]::Create('D:/New folder (4)/pj3/Tools/TerrainJump/Downhill-20261118.png')
try { $encoder.Save($stream) } finally { $stream.Dispose(); $media.Close() }

