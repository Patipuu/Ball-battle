# Cắt + phóng to 1 vùng ảnh làm bằng chứng (ảnh "zoom" đặt trước trong cột Ảnh).
#   & crop.ps1 -Src after\R1-03.png -Dst after\R1-03z-label.png -X 900 -Y 140 -Wd 260 -Ht 60 [-S 2]
param([string]$Src, [string]$Dst, [int]$X, [int]$Y, [int]$Wd, [int]$Ht, [int]$S = 2)
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($Src)
$o = New-Object System.Drawing.Bitmap ($Wd * $S), ($Ht * $S)
$g = [System.Drawing.Graphics]::FromImage($o); $g.InterpolationMode = 'HighQualityBicubic'
$g.DrawImage($b, (New-Object System.Drawing.Rectangle 0, 0, ($Wd * $S), ($Ht * $S)), (New-Object System.Drawing.Rectangle $X, $Y, $Wd, $Ht), 'Pixel')
$o.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $o.Dispose(); $b.Dispose(); "OK $Dst"
