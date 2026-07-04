param(
    [string]$AssetDirectory = (Join-Path $PSScriptRoot "..\src\PrismMonitor.App\Assets")
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$AssetDirectory = (Resolve-Path -LiteralPath $AssetDirectory).Path

function New-Color([byte]$A, [byte]$R, [byte]$G, [byte]$B) {
    return [System.Windows.Media.Color]::FromArgb($A, $R, $G, $B)
}

function New-Brush([byte]$A, [byte]$R, [byte]$G, [byte]$B) {
    $brush = [System.Windows.Media.SolidColorBrush]::new((New-Color $A $R $G $B))
    $brush.Freeze()
    return $brush
}

function New-Pen([System.Windows.Media.Brush]$Brush, [double]$Thickness) {
    $pen = [System.Windows.Media.Pen]::new($Brush, $Thickness)
    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
    $pen.Freeze()
    return $pen
}

function New-Geometry([string]$Data) {
    $geometry = [System.Windows.Media.Geometry]::Parse($Data)
    $geometry.Freeze()
    return $geometry
}

function Draw-PrismMonitorIcon([System.Windows.Media.DrawingContext]$Context, [double]$X, [double]$Y, [double]$Size) {
    $context.PushTransform([System.Windows.Media.TranslateTransform]::new($X, $Y))
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($Size / 1024.0, $Size / 1024.0))

    $background = [System.Windows.Media.LinearGradientBrush]::new(
        (New-Color 255 19 33 40),
        (New-Color 255 8 15 21),
        [System.Windows.Point]::new(0, 0),
        [System.Windows.Point]::new(1, 1))
    $background.Freeze()
    $context.DrawRoundedRectangle($background, $null, [System.Windows.Rect]::new(0, 0, 1024, 1024), 190, 190)

    $shine = [System.Windows.Media.LinearGradientBrush]::new(
        (New-Color 54 68 92 108),
        (New-Color 0 68 92 108),
        [System.Windows.Point]::new(0.2, 0),
        [System.Windows.Point]::new(1, 1))
    $shine.Freeze()
    $context.DrawRoundedRectangle($shine, $null, [System.Windows.Rect]::new(24, 24, 976, 492), 170, 170)

    $cyan = New-Brush 255 42 224 214
    $cyanDim = New-Brush 215 25 174 170
    $steel = New-Brush 255 176 192 202
    $chip = New-Brush 255 72 88 99
    $chipFace = New-Brush 255 95 113 124
    $amber = New-Brush 255 255 172 24
    $amberLight = New-Brush 255 255 236 162
    $darkCut = New-Brush 255 12 24 31

    $shieldFill = [System.Windows.Media.LinearGradientBrush]::new(
        (New-Color 255 18 36 44),
        (New-Color 255 8 19 26),
        [System.Windows.Point]::new(0, 0),
        [System.Windows.Point]::new(0.8, 1))
    $shieldFill.Freeze()
    $shield = New-Geometry "M512,158 L815,305 L730,792 L512,900 L294,792 L209,305 Z"
    $context.DrawGeometry($shieldFill, (New-Pen $steel 52), $shield)

    $innerShield = New-Geometry "M512,254 L744,362 L681,735 L512,819 L343,735 L280,362 Z"
    $context.DrawGeometry($null, (New-Pen $cyan 34), $innerShield)

    $gridPen = New-Pen $cyanDim 24
    foreach ($xLine in 432, 512, 592) {
        $context.DrawLine($gridPen, [System.Windows.Point]::new($xLine, 286), [System.Windows.Point]::new($xLine, 752))
    }
    foreach ($yLine in 448, 540, 632) {
        $context.DrawLine($gridPen, [System.Windows.Point]::new(284, $yLine), [System.Windows.Point]::new(740, $yLine))
    }

    $chipRect = [System.Windows.Rect]::new(382, 382, 260, 260)
    $context.DrawRoundedRectangle($chip, $null, $chipRect, 56, 56)
    $context.DrawRoundedRectangle($darkCut, (New-Pen $cyan 26), [System.Windows.Rect]::new(450, 450, 124, 124), 18, 18)

    foreach ($pinX in 432, 512, 592) {
        $context.DrawLine((New-Pen $chipFace 20), [System.Windows.Point]::new($pinX, 350), [System.Windows.Point]::new($pinX, 382))
        $context.DrawLine((New-Pen $chipFace 20), [System.Windows.Point]::new($pinX, 642), [System.Windows.Point]::new($pinX, 674))
    }
    foreach ($pinY in 432, 512, 592) {
        $context.DrawLine((New-Pen $chipFace 20), [System.Windows.Point]::new(350, $pinY), [System.Windows.Point]::new(382, $pinY))
        $context.DrawLine((New-Pen $chipFace 20), [System.Windows.Point]::new(642, $pinY), [System.Windows.Point]::new(674, $pinY))
    }

    $context.DrawEllipse($amber, $null, [System.Windows.Point]::new(778, 760), 86, 86)
    $context.DrawEllipse($amberLight, $null, [System.Windows.Point]::new(778, 760), 35, 35)
    $context.DrawGeometry($null, (New-Pen $amber 34), (New-Geometry "M844,560 C925,626 949,723 910,811"))
    $context.DrawGeometry($null, (New-Pen $amber 34), (New-Geometry "M879,509 C995,605 1026,740 972,858"))

    $context.Pop()
    $context.Pop()
}

function Save-IconPng([string]$Path, [int]$Width, [int]$Height, [double]$IconScale = 0.86) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    [System.Windows.Media.RenderOptions]::SetEdgeMode($visual, [System.Windows.Media.EdgeMode]::Unspecified)
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $visual.RenderOpen()
    try {
        $iconSize = [Math]::Min($Width, $Height) * $IconScale
        $x = ($Width - $iconSize) / 2
        $y = ($Height - $iconSize) / 2
        Draw-PrismMonitorIcon $context $x $y $iconSize
    } finally {
        $context.Close()
    }

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($Path)
    try {
        $encoder.Save($stream)
    } finally {
        $stream.Dispose()
    }
}

function New-PngBytes([int]$Width, [int]$Height, [double]$IconScale = 0.86) {
    $temp = [System.IO.Path]::GetTempFileName()
    try {
        Save-IconPng $temp $Width $Height $IconScale
        return ,([System.IO.File]::ReadAllBytes($temp))
    } finally {
        Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
    }
}

function Save-Ico([string]$Path, [int[]]$Sizes) {
    $images = foreach ($size in $Sizes) {
        [pscustomobject]@{
            Size = $size
            Bytes = New-PngBytes $size $size 0.9
        }
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$images.Count)

        $offset = 6 + (16 * $images.Count)
        foreach ($image in $images) {
            $entrySize = if ($image.Size -ge 256) { 0 } else { $image.Size }
            $writer.Write([byte]$entrySize)
            $writer.Write([byte]$entrySize)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write($image.Bytes)
        }
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

Save-IconPng (Join-Path $AssetDirectory "PrismMonitorIcon.generated.png") 2048 2048 0.92
Save-Ico (Join-Path $AssetDirectory "AppIcon.ico") @(16, 20, 24, 32, 40, 48, 64, 128, 256)

$scaleAssets = @(
    @{ Name = "Square44x44Logo"; Width = 44; Height = 44 },
    @{ Name = "Square150x150Logo"; Width = 150; Height = 150 },
    @{ Name = "StoreLogo"; Width = 50; Height = 50 },
    @{ Name = "LockScreenLogo"; Width = 24; Height = 24 },
    @{ Name = "Wide310x150Logo"; Width = 310; Height = 150 },
    @{ Name = "SplashScreen"; Width = 620; Height = 300 }
)
$scales = @(100, 125, 150, 200, 400)
foreach ($asset in $scaleAssets) {
    foreach ($scale in $scales) {
        $width = [int][Math]::Round($asset.Width * $scale / 100.0)
        $height = [int][Math]::Round($asset.Height * $scale / 100.0)
        $path = Join-Path $AssetDirectory "$($asset.Name).scale-$scale.png"
        $iconScale = if ($asset.Width -eq $asset.Height) { 0.86 } else { 0.44 }
        Save-IconPng $path $width $height $iconScale
    }
}

Copy-Item -LiteralPath (Join-Path $AssetDirectory "StoreLogo.scale-100.png") -Destination (Join-Path $AssetDirectory "StoreLogo.png") -Force

$targetSizes = @(16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256)
foreach ($size in $targetSizes) {
    foreach ($suffix in @("", "_altform-unplated", "_altform-lightunplated")) {
        Save-IconPng (Join-Path $AssetDirectory "Square44x44Logo.targetsize-$size$suffix.png") $size $size 0.9
    }
}

Write-Host "Generated Prism Monitor icon assets in $AssetDirectory"
