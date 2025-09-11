#!/bin/bash

# Simple demonstration of the Golden Master Testing Framework
# Shows how actual vs expected comparison works for CI/CD pipelines

echo "🧪 SignalBooster MVP - Golden Master Testing Demo"
echo "=================================================="
echo ""

# Enable batch mode temporarily  
echo "📝 Step 1: Enabling batch processing mode..."
cp appsettings.json appsettings.json.backup
sed 's/"BatchProcessingMode": false/"BatchProcessingMode": true/' appsettings.json.backup > appsettings.json

# Generate fresh actual files
echo "🚀 Step 2: Running batch processing to generate actual outputs..."
dotnet run > batch_output.log 2>&1

if [ $? -eq 0 ]; then
    echo "✅ Batch processing completed successfully"
    PROCESSED_COUNT=$(grep "Successfully processed" batch_output.log | grep -o "[0-9]*" | head -1)
    echo "   Generated $PROCESSED_COUNT actual output files"
else
    echo "❌ Batch processing failed"
    cat batch_output.log
    exit 1
fi

# Count files for comparison
ACTUAL_COUNT=$(find test_outputs -name "*_actual.json" | wc -l | tr -d ' ')
EXPECTED_COUNT=$(find test_outputs -name "*_expected.json" | wc -l | tr -d ' ')

echo ""
echo "📊 Step 3: File comparison analysis..."
echo "   Input files:    $(find test_notes -name "*.txt" -o -name "*.json" | wc -l | tr -d ' ')"
echo "   Actual files:   $ACTUAL_COUNT"
echo "   Expected files: $EXPECTED_COUNT"

# Compare a few specific test cases
echo ""
echo "🔍 Step 4: Sample golden master comparisons..."

# Test case 1: Assignment requirement
if [ -f "test_outputs/physician_note1_actual.json" ] && [ -f "test_outputs/physician_note1_expected.json" ]; then
    echo ""
    echo "📋 Assignment Test: physician_note1.txt (Oxygen Tank)"
    echo "Expected:"
    cat test_outputs/physician_note1_expected.json | jq '.'
    echo "Actual:"
    cat test_outputs/physician_note1_actual.json | jq '.'
    
    if diff -q test_outputs/physician_note1_expected.json test_outputs/physician_note1_actual.json > /dev/null; then
        echo "✅ PASS: Outputs match perfectly"
    else
        echo "❌ FAIL: Outputs differ"
        echo "Diff:"
        diff test_outputs/physician_note1_expected.json test_outputs/physician_note1_actual.json
    fi
fi

# Test case 2: Enhanced DME device
if [ -f "test_outputs/hospital_bed_actual.json" ] && [ -f "test_outputs/hospital_bed_expected.json" ]; then
    echo ""
    echo "📋 Enhanced DME Test: hospital_bed_test.txt"
    echo "Expected device: $(cat test_outputs/hospital_bed_expected.json | jq -r '.device')"
    echo "Actual device:   $(cat test_outputs/hospital_bed_actual.json | jq -r '.device')"
    
    if diff -q test_outputs/hospital_bed_expected.json test_outputs/hospital_bed_actual.json > /dev/null; then
        echo "✅ PASS: Hospital bed extraction matches expected output"
    else
        echo "❌ FAIL: Hospital bed extraction differs from expected"
    fi
fi

# Regression test simulation
echo ""
echo "🛡️ Step 5: Regression detection simulation..."
REGRESSION_TESTS=0
PASSED_TESTS=0

for expected_file in test_outputs/*_expected.json; do
    if [ -f "$expected_file" ]; then
        actual_file="${expected_file/_expected/_actual}"
        test_name=$(basename "$expected_file" _expected.json)
        
        if [ -f "$actual_file" ]; then
            REGRESSION_TESTS=$((REGRESSION_TESTS + 1))
            if diff -q "$expected_file" "$actual_file" > /dev/null; then
                PASSED_TESTS=$((PASSED_TESTS + 1))
                echo "   ✅ $test_name"
            else
                echo "   ❌ $test_name (REGRESSION DETECTED)"
            fi
        fi
    fi
done

echo ""
echo "📈 Step 6: Test Results Summary"
echo "================================"
echo "Total regression tests: $REGRESSION_TESTS"
echo "Passed tests:          $PASSED_TESTS"
echo "Failed tests:          $((REGRESSION_TESTS - PASSED_TESTS))"

if [ $PASSED_TESTS -eq $REGRESSION_TESTS ]; then
    echo ""
    echo "🎉 ALL TESTS PASSED - No regressions detected!"
    echo "✅ SignalBooster MVP is ready for deployment"
    EXIT_CODE=0
else
    echo ""
    echo "🚨 REGRESSION DETECTED - Build should fail in CI/CD"
    echo "❌ Manual review required before deployment"
    EXIT_CODE=1
fi

# Restore original config
mv appsettings.json.backup appsettings.json
rm -f batch_output.log

echo ""
echo "🔧 Framework Features Demonstrated:"
echo "   • Automated batch processing"
echo "   • Golden master comparison"
echo "   • Regression detection"
echo "   • CI/CD pipeline integration"
echo "   • Detailed diff reporting"
echo ""
echo "💡 In a real CI/CD pipeline, this would:"
echo "   • Fail the build on regressions"
echo "   • Generate test reports"
echo "   • Block deployment until fixed"

exit $EXIT_CODE