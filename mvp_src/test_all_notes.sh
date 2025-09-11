#!/bin/bash

echo "Testing all physician notes with SignalBooster MVP..."
echo "=================================================="

# Create outputs directory if it doesn't exist
mkdir -p test_outputs

# Test each file in test_notes directory
for file in test_notes/*.txt test_notes/*.json; do
    if [ -f "$file" ]; then
        filename=$(basename "$file")
        name="${filename%.*}"
        
        echo ""
        echo "Processing: $filename"
        echo "----------------------------------------"
        
        # Run the application and capture output
        dotnet run "$file" > /dev/null 2>&1
        
        # Copy the generated output.json with a descriptive name
        if [ -f "output.json" ]; then
            cp output.json "test_outputs/${name}_actual.json"
            echo "✅ Generated: test_outputs/${name}_actual.json"
            
            # Show the extracted device info
            device=$(jq -r '.device // "Unknown"' output.json)
            patient=$(jq -r '.patient_name // "N/A"' output.json)
            echo "📋 Device: $device | Patient: $patient"
        else
            echo "❌ Failed to generate output for $filename"
        fi
    fi
done

echo ""
echo "Testing complete! Check test_outputs/ folder for results."
echo "Expected outputs are in test_outputs/*_expected.json"
echo "Actual outputs are in test_outputs/*_actual.json"