Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Write-Host "`n=== MONITOREANDO TRADUCTOR ===" -ForegroundColor Green
Write-Host "Ctrl+C para detener`n" -ForegroundColor Yellow

$lastState = ""

while ($true) {
    try {
        $root = [System.Windows.Automation.AutomationElement]::RootElement

        # Obtener TODAS las ventanas
        $allWindows = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition
        )

        $currentState = ""
        $traductorCount = 0

        foreach ($window in $allWindows) {
            $name = $window.Current.Name
            $className = $window.Current.ClassName

            # Detectar cualquier ventana relacionada con Traductor
            if ($name -match "Traductor" -or $className -match "Traductor" -or $name -eq "" -and $className -match "Window") {
                $traductorCount++
                $bounds = $window.Current.BoundingRectangle

                $currentState += "WIN#$traductorCount | Name: '$name' | Class: $className | Pos: ($($bounds.X),$($bounds.Y)) | Size: ($($bounds.Width)x$($bounds.Height))`n"

                # Buscar TextBoxes con contenido
                $textElements = $window.FindAll(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    [System.Windows.Automation.Condition]::TrueCondition
                )

                $textContent = @()
                foreach ($elem in $textElements) {
                    try {
                        $ctrlType = $elem.Current.ControlType.ProgrammaticName
                        if ($ctrlType -eq "ControlType.Edit" -or $ctrlType -eq "ControlType.Text" -or $ctrlType -eq "ControlType.Document") {
                            $valuePattern = $elem.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                            $text = $valuePattern.Current.Value
                            if ($text -and $text.Trim().Length -gt 0) {
                                $preview = if ($text.Length -gt 40) { $text.Substring(0, 40) + "..." } else { $text }
                                $textContent += "    TEXT: '$preview'"
                            }
                        }
                    } catch {}
                }

                if ($textContent.Count -gt 0) {
                    $currentState += ($textContent -join "`n") + "`n"
                }
            }
        }

        # Mostrar solo cuando hay cambios
        if ($currentState -ne $lastState) {
            Clear-Host
            Write-Host "`n=== ESTADO ACTUAL [$(Get-Date -Format 'HH:mm:ss')] ===" -ForegroundColor Cyan
            Write-Host "Total ventanas Traductor detectadas: $traductorCount`n" -ForegroundColor Yellow

            if ($currentState) {
                Write-Host $currentState
            } else {
                Write-Host "No hay ventanas de Traductor detectadas" -ForegroundColor Gray
            }

            Write-Host "`n--- Monitoreando cambios... ---`n" -ForegroundColor DarkGray
            $lastState = $currentState
        }

    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }

    Start-Sleep -Milliseconds 300
}
