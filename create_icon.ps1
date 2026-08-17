Add-Type -AssemblyName System.Drawing

# Crear un bitmap de 256x256
$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Fondo azul oscuro
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(45, 45, 48))
$graphics.FillRectangle($bgBrush, 0, 0, 256, 256)

# Borde azul
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 120, 212), 8)
$graphics.DrawRectangle($borderPen, 10, 10, 236, 236)

# Dibujar letras "MSM" en el centro
$font = New-Object System.Drawing.Font("Segoe UI", 72, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$text = "MSM"

# Centrar el texto
$textSize = $graphics.MeasureString($text, $font)
$x = (256 - $textSize.Width) / 2
$y = (256 - $textSize.Height) / 2 - 10

$graphics.DrawString($text, $font, $textBrush, $x, $y)

# Dibujar icono de traducción (flechas)
$arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 120, 212), 6)
$arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor

# Flecha derecha
$graphics.DrawLine($arrowPen, 60, 180, 120, 180)

# Flecha izquierda
$arrowPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 120, 212), 6)
$arrowPen2.StartCap = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor
$graphics.DrawLine($arrowPen2, 196, 200, 136, 200)

# Guardar como PNG primero
$pngPath = "C:\Users\j-b-j\Documents\Traductor\icon.png"
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

Write-Host "Icono PNG creado: $pngPath"

# Crear ICO desde PNG
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$iconPath = "C:\Users\j-b-j\Documents\Traductor\icon.ico"
$fileStream = [System.IO.File]::Create($iconPath)
$icon.Save($fileStream)
$fileStream.Close()

Write-Host "Icono ICO creado: $iconPath"

$graphics.Dispose()
$bitmap.Dispose()
$bgBrush.Dispose()
$borderPen.Dispose()
$font.Dispose()
$textBrush.Dispose()
$arrowPen.Dispose()
$arrowPen2.Dispose()
