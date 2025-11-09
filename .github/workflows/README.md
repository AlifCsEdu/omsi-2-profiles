# GitHub Actions Workflow

## What This Does

This workflow automatically builds your OMSI Profile Manager application on **Windows in the cloud** whenever you push code to GitHub.

## How to Use

### Automatic Builds

1. **Push your code** to GitHub:
   ```bash
   git push origin main
   ```

2. **Check the Actions tab** on GitHub:
   - Go to: `https://github.com/YOUR_USERNAME/omsi-2-profiles/actions`
   - You'll see the build running (usually takes 2-3 minutes)

3. **Download your built app**:
   - Click on the completed build
   - Scroll to "Artifacts" section at the bottom
   - Download: `OMSIProfileManager-Windows-x64-Release.zip`
   - Extract and run `OMSIProfileManager.exe`

### Manual Builds

You can also trigger builds manually:

1. Go to the Actions tab on GitHub
2. Click "Build OMSI Profile Manager" workflow
3. Click "Run workflow" button
4. Select branch and click "Run workflow"

## What Gets Built

The workflow creates two artifacts:

1. **Full publish folder** (with commit hash)
   - All runtime files
   - Self-contained (no .NET installation needed)
   - Expires after 30 days

2. **Release ZIP** 
   - Compressed archive of the full app
   - Ready to distribute
   - Expires after 90 days

## Requirements

- ✅ **Free** for public repositories (unlimited minutes)
- ✅ **Free** for private repositories (2,000 minutes/month)
- ✅ No setup needed - works automatically

## Troubleshooting

### Build fails?
- Check the build logs in Actions tab
- Look for error messages in the failed step
- Common issues: NuGet restore failures, XAML compilation errors

### Can't find artifacts?
- Artifacts only appear on **completed** builds
- Check the build finished successfully (green checkmark)
- Scroll to the bottom of the build page

### Build takes too long?
- Windows builds typically take 2-3 minutes
- First build may take longer (NuGet cache warming)
- Subsequent builds are faster

## Technical Details

- **Runner**: `windows-latest` (Windows Server 2022)
- **.NET Version**: 8.0.x
- **Build Configuration**: Release, win-x64, self-contained
- **Retention**: 30 days (full build), 90 days (release zip)
