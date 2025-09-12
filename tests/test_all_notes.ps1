# SignalBooster MVP - Test All Notes (PowerShell)
# Process all physician notes in the test_notes directory

Write-Host "Testing all physician notes with SignalBooster MVP..." -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Create outputs directory if it doesn't exist
if (-not (Test-Path "test_outputs")) {
    New-Item -ItemType Directory -Path "test_outputs" -Force | Out-Null
}

# Test each file in test_notes directory
$testFiles = Get-ChildItem -Path "test_notes" -Include "*.txt", "*.json" -File

foreach ($file in $testFiles) {
    $filename = $file.Name
    $nameWithoutExtension = $file.BaseName
    
    Write-Host ""
    Write-Host "Processing: $filename" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Gray
    
    try {
        # Run the application and capture output
        $process = Start-Process -FilePath "dotnet" -ArgumentList "run", $file.FullName -NoNewWindow -Wait -PassThru -RedirectStandardOutput $null -RedirectStandardError $null
        
        # Check if output.json was generated
        if (Test-Path "output.json") {
            $outputPath = "test_outputs\${nameWithoutExtension}_actual.json"
            Copy-Item "output.json" $outputPath
            Write-Host "✅ Generated: $outputPath" -ForegroundColor Green
            
            # Show the extracted device info
            try {
                $outputContent = Get-Content "output.json" -Raw | ConvertFrom-Json
                $device = if ($outputContent.device) { $outputContent.device } else { "Unknown" }
                $patient = if ($outputContent.patient_name) { $outputContent.patient_name } else { "N/A" }
                Write-Host "📋 Device: $device | Patient: $patient" -ForegroundColor Cyan
            }
            catch {
                Write-Host "📋 Device info extraction failed" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "❌ Failed to generate output for $filename" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Error processing $filename`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Testing complete! Check test_outputs\ folder for results." -ForegroundColor Green
Write-Host "Expected outputs are in test_outputs\*_expected.json" -ForegroundColor Gray
Write-Host "Actual outputs are in test_outputs\*_actual.json" -ForegroundColor Gray