# CI/CD Setup Guide

## Overview
This repository includes a complete GitHub Actions CI/CD pipeline that runs 424 comprehensive tests and generates coverage reports on every push and pull request.

## GitHub Secrets Configuration

### Required Secrets
To enable full CI/CD functionality including OpenAI-enhanced tests, you need to configure repository secrets.

### Step-by-Step Setup

1. **Navigate to Repository Settings**
   ```
   https://github.com/[your-org]/[your-repo]/settings/secrets/actions
   ```

2. **Add OpenAI API Key Secret**
   - Click `New repository secret`
   - **Name**: `OPENAI_API_KEY`
   - **Value**: `sk-proj-[your-openai-api-key]`
   - Click `Add secret`

3. **Verify Configuration**
   - The workflow automatically references: `${{ secrets.OPENAI_API_KEY }}`
   - No code changes needed - environment variable is already configured

## Test Execution Modes

### Without API Key Secret
- **Result**: 421/424 tests pass
- **Behavior**: AI tests gracefully skip with informative messages
- **Use Case**: Fork repositories, contributors without API access

### With API Key Secret  
- **Result**: All 424 tests execute
- **Behavior**: Full OpenAI integration testing
- **Use Case**: Main repository, production CI/CD

## Workflow Triggers

- **Push to main/develop**: Full test suite + deployment artifacts
- **Pull Requests to main**: Test validation only
- **Manual Dispatch**: On-demand pipeline execution

## Security Best Practices

✅ **Implemented**:
- No hardcoded API keys in source code
- Environment variables used throughout
- GitHub secrets management for sensitive data
- Tests designed to work without API keys

❌ **Avoided**:
- Hardcoded secrets in test files
- API keys in configuration files
- Committing sensitive data

## Monitoring

### Artifacts Generated
- **Test Results**: TRX format for detailed test reporting
- **Coverage Reports**: Cobertura XML for coverage analysis
- **Build Artifacts**: Published application ready for deployment

### Pipeline Health
- **Expected Duration**: ~2-3 minutes
- **Success Rate**: 424/424 tests (with API key) or 421/424 tests (without)
- **Coverage Target**: 73%+ line coverage maintained

## Troubleshooting

### Common Issues

**Issue**: AI tests failing with "no API key configured"
- **Cause**: Missing `OPENAI_API_KEY` repository secret
- **Solution**: Add secret following steps above

**Issue**: Tests timing out
- **Cause**: OpenAI API rate limiting or network issues
- **Solution**: Tests include retry logic and graceful degradation

**Issue**: Coverage below threshold
- **Cause**: New code without corresponding tests
- **Solution**: Add tests for new functionality

## Local Development

For local testing with OpenAI integration:

```bash
# Set environment variable (macOS/Linux)
export OPENAI_API_KEY="sk-proj-your-key"

# Run tests
dotnet test

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## Contact

For CI/CD issues, check:
1. Repository Actions tab for workflow logs
2. Test artifacts for detailed failure information
3. This documentation for common solutions