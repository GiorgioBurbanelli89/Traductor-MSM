Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Write-Host "=== Monitoreando UI de Traductor en tiempo real ===" -ForegroundColor Green
Write-Host "Presiona Ctrl+C para detener`n" -ForegroundColor Yellow

$lastPopupCount = 0
$lastWindowCount = 0

while ($true) {
    try {
        $root = [System.Windows.Automation.AutomationElement]::RootElement

        # Buscar ventana principal de Traductor
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            "Traductor"
        )
        $mainWindow = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)

        # Contar todas las ventanas de Traductor (incluyendo popups)
        $allWindows = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition
        )

        $traductorWindows = @()
        $popups = @()

        foreach ($window in $allWindows) {
            $name = $window.Current.Name
            $className = $window.Current.ClassName

            if ($name -eq "Traductor" -or $className -like "*Traductor*") {
                $traductorWindows += $window

                # Obtener información de la ventana
                $bounds = $window.Current.BoundingRectangle
                $isVisible = -not $window.Current.IsOffscreen

                Write-Host "`n[$(Get-Date -Format 'HH:mm:ss')] Ventana: $name" -ForegroundColor Cyan
                Write-Host "  ClassName: $className"
                Write-Host "  Visible: $isVisible"
                Write-Host "  Position: X=$($bounds.X), Y=$($bounds.Y)"
                Write-Host "  Size: W=$($bounds.Width), H=$($bounds.Height)"

                # Buscar elementos interactivos dentro
                $elements = $window.FindAll(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    [System.Windows.Automation.Condition]::TrueCondition
                )

                Write-Host "  Total elementos: $($elements.Count)" -ForegroundColor Gray

                # Mostrar elementos con texto
                $textBoxes = @()
                $buttons = @()

                foreach ($elem in $elements) {
                    $ctrlType = $elem.Current.ControlType.ProgrammaticName
                    $elemName = $elem.Current.Name
                    $automationId = $elem.Current.AutomationId

                    if ($ctrlType -eq "ControlType.Edit" -or $ctrlType -eq "ControlType.Document") {
                        try {
                            $valuePattern = $elem.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                            $text = $valuePattern.Current.Value
                            if ($text) {
                                $preview = if ($text.Length -gt 50) { $text.Substring(0, 50) + "..." } else { $text }
                                Write-Host "    [TextBox] $automationId : '$preview'" -ForegroundColor Yellow
                                $textBoxes += @{Id=$automationId; Text=$text}
                            }
                        } catch {}
                    }
                    elseif ($ctrlType -eq "ControlType.Button" -and $elemName) {
                        Write-Host "    [Button] $elemName" -ForegroundColor Green
                        $buttons += $elemName
                    }
                }

                Write-Host "  TextBoxes con contenido: $($textBoxes.Count)"
                Write-Host "  Botones: $($buttons.Count)"
            }
        }

        $currentWindowCount = $traductorWindows.Count

        if ($currentWindowCount -ne $lastWindowCount) {
            Write-Host "`n*** CAMBIO DETECTADO: Ventanas antes=$lastWindowCount, ahora=$currentWindowCount ***" -ForegroundColor Red
            $lastWindowCount = $currentWindowCount
        }

        Write-Host "`n--- Esperando cambios... (Total ventanas Traductor: $currentWindowCount) ---`n" -ForegroundColor DarkGray

    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }

    Start-Sleep -Milliseconds 500
}
