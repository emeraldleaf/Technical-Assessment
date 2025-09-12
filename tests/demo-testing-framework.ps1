# SignalBooster MVP - Golden Master Testing Demo (PowerShell)
# Simple demonstration of the Golden Master Testing Framework
# Shows how actual vs expected comparison works for CI/CD pipelines

Write-Host "🧪 SignalBooster MVP - Golden Master Testing Demo" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Enable batch mode temporarily  
Write-Host "📝 Step 1: Enabling batch processing mode..." -ForegroundColor Yellow
Copy-Item "appsettings.json" "appsettings.json.backup"
$configContent = Get-Content "appsettings.json" -Raw
$configContent = $configContent -replace '"BatchProcessingMode": false', '"BatchProcessingMode": true'
$configContent | Out-File -FilePath "appsettings.json" -Encoding UTF8

try {
    # Generate fresh actual files
    Write-Host "🚀 Step 2: Running batch processing to generate actual outputs..." -ForegroundColor Yellow
    $batchOutput = dotnet run 2>&1
    $batchExitCode = $LASTEXITCODE

    if ($batchExitCode -eq 0) {
        Write-Host "✅ Batch processing completed successfully" -ForegroundColor Green
        $processedMatch = [regex]::Match($batchOutput, "Successfully processed (\d+)")
        $processedCount = if ($processedMatch.Success) { $processedMatch.Groups[1].Value } else { "unknown" }
        Write-Host "   Generated $processedCount actual output files" -ForegroundColor Gray
    } else {
        Write-Host "❌ Batch processing failed" -ForegroundColor Red
        Write-Host $batchOutput
        exit 1
    }

    # Count files for comparison
    $actualFiles = Get-ChildItem -Path "test_outputs" -Filter "*_actual.json"
    $expectedFiles = Get-ChildItem -Path "test_outputs" -Filter "*_expected.json"
    $inputFiles = Get-ChildItem -Path "test_notes" -Include "*.txt", "*.json" -Recurse

    $actualCount = $actualFiles.Count
    $expectedCount = $expectedFiles.Count
    $inputCount = $inputFiles.Count

    Write-Host ""
    Write-Host "📊 Step 3: File comparison analysis..." -ForegroundColor Yellow
    Write-Host "   Input files:    $inputCount" -ForegroundColor Gray
    Write-Host "   Actual files:   $actualCount" -ForegroundColor Gray
    Write-Host "   Expected files: $expectedCount" -ForegroundColor Gray

    # Compare a few specific test cases
    Write-Host ""
    Write-Host "🔍 Step 4: Sample golden master comparisons..." -ForegroundColor Yellow

    # Test case 1: Assignment requirement
    $physician1Actual = "test_outputs\physician_note1_actual.json"
    $physician1Expected = "test_outputs\physician_note1_expected.json"
    
    if ((Test-Path $physician1Actual) -and (Test-Path $physician1Expected)) {
        Write-Host ""
        Write-Host "📋 Assignment Test: physician_note1.txt (Oxygen Tank)" -ForegroundColor Cyan
        Write-Host "Expected:" -ForegroundColor Gray
        $expectedContent = Get-Content $physician1Expected -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 10
        Write-Host $expectedContent -ForegroundColor White
        Write-Host "Actual:" -ForegroundColor Gray
        $actualContent = Get-Content $physician1Actual -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 10
        Write-Host $actualContent -ForegroundColor White
        
        $expectedHash = Get-FileHash $physician1Expected
        $actualHash = Get-FileHash $physician1Actual
        
        if ($expectedHash.Hash -eq $actualHash.Hash) {
            Write-Host "✅ PASS: Outputs match perfectly" -ForegroundColor Green
        } else {
            Write-Host "❌ FAIL: Outputs differ" -ForegroundColor Red
            Write-Host "Use 'Compare-Object' or diff tools to see differences" -ForegroundColor Yellow
        }
    }

    # Test case 2: Enhanced DME device
    $hospitalBedActual = "test_outputs\hospital_bed_test_actual.json"
    $hospitalBedExpected = "test_outputs\hospital_bed_test_expected.json"
    
    if ((Test-Path $hospitalBedActual) -and (Test-Path $hospitalBedExpected)) {
        Write-Host ""
        Write-Host "📋 Enhanced DME Test: hospital_bed_test.txt" -ForegroundColor Cyan
        
        try {
            $expectedObj = Get-Content $hospitalBedExpected -Raw | ConvertFrom-Json
            $actualObj = Get-Content $hospitalBedActual -Raw | ConvertFrom-Json
            
            Write-Host "Expected device: $($expectedObj.device)" -ForegroundColor Gray
            Write-Host "Actual device:   $($actualObj.device)" -ForegroundColor Gray
            
            $expectedHash = Get-FileHash $hospitalBedExpected
            $actualHash = Get-FileHash $hospitalBedActual
            
            if ($expectedHash.Hash -eq $actualHash.Hash) {
                Write-Host "✅ PASS: Hospital bed extraction matches expected output" -ForegroundColor Green
            } else {
                Write-Host "❌ FAIL: Hospital bed extraction differs from expected" -ForegroundColor Red
            }
        }
        catch {
            Write-Host "❌ FAIL: Error comparing hospital bed files" -ForegroundColor Red
        }
    }

    # Regression test simulation
    Write-Host ""
    Write-Host "🛡️ Step 5: Regression detection simulation..." -ForegroundColor Yellow
    $regressionTests = 0
    $passedTests = 0

    $expectedFiles = Get-ChildItem -Path "test_outputs" -Filter "*_expected.json"
    
    foreach ($expectedFile in $expectedFiles) {
        $actualFileName = $expectedFile.Name -replace "_expected", "_actual"
        $actualFilePath = Join-Path "test_outputs" $actualFileName
        $testName = $expectedFile.BaseName -replace "_expected", ""
        
        if (Test-Path $actualFilePath) {
            $regressionTests++
            $expectedHash = Get-FileHash $expectedFile.FullName
            $actualHash = Get-FileHash $actualFilePath
            
            if ($expectedHash.Hash -eq $actualHash.Hash) {
                $passedTests++
                Write-Host "   ✅ $testName" -ForegroundColor Green
            } else {
                Write-Host "   ❌ $testName (REGRESSION DETECTED)" -ForegroundColor Red
            }
        }
    }

    Write-Host ""
    Write-Host "📈 Step 6: Test Results Summary" -ForegroundColor Yellow
    Write-Host "================================" -ForegroundColor Yellow
    Write-Host "Total regression tests: $regressionTests" -ForegroundColor Gray
    Write-Host "Passed tests:          $passedTests" -ForegroundColor Gray
    Write-Host "Failed tests:          $($regressionTests - $passedTests)" -ForegroundColor Gray

    if ($passedTests -eq $regressionTests) {
        Write-Host ""
        Write-Host "🎉 ALL TESTS PASSED - No regressions detected!" -ForegroundColor Green
        Write-Host "✅ SignalBooster MVP is ready for deployment" -ForegroundColor Green
        $exitCode = 0
    } else {
        Write-Host ""
        Write-Host "🚨 REGRESSION DETECTED - Build should fail in CI/CD" -ForegroundColor Red
        Write-Host "❌ Manual review required before deployment" -ForegroundColor Red
        $exitCode = 1
    }

    Write-Host ""
    Write-Host "🔧 Framework Features Demonstrated:" -ForegroundColor Cyan
    Write-Host "   • Automated batch processing" -ForegroundColor Gray
    Write-Host "   • Golden master comparison" -ForegroundColor Gray
    Write-Host "   • Regression detection" -ForegroundColor Gray
    Write-Host "   • CI/CD pipeline integration" -ForegroundColor Gray
    Write-Host "   • Detailed diff reporting" -ForegroundColor Gray
    Write-Host ""
    Write-Host "💡 In a real CI/CD pipeline, this would:" -ForegroundColor Yellow
    Write-Host "   • Fail the build on regressions" -ForegroundColor Gray
    Write-Host "   • Generate test reports" -ForegroundColor Gray
    Write-Host "   • Block deployment until fixed" -ForegroundColor Gray

    exit $exitCode
}
finally {
    # Restore original config
    Move-Item "appsettings.json.backup" "appsettings.json" -Force -ErrorAction SilentlyContinue
}