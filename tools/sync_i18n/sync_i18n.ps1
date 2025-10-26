<#
.SYNOPSIS
    Automatically synchronizes i18n resources using AI translation.

.DESCRIPTION
    This script reads the base Strings.resx file and compares it with localized
    Strings.xx.resx files, identifying missing resources and using AI to translate them.
    The script processes translations in batches to handle large resource sets efficiently.
    
    If sync_i18n_config.ps1 exists, parameters will be loaded from it automatically.

.PARAMETER BaseUrl
    The OpenAI-compatible API base URL. Optional if config file exists.

.PARAMETER ApiKey
    The API key for authentication. Optional if config file exists.

.PARAMETER ModelId
    The model ID to use for translation. Optional if config file exists.

.PARAMETER BatchSize
    Number of resources to translate in each batch. Default is 20.

.PARAMETER MaxRetries
    Maximum number of retries for failed batches. Default is 3.

.PARAMETER I18NPath
    Path to the I18N directory. Default is ../src/Everywhere/I18N.

.EXAMPLE
    .\sync_i18n.ps1

.EXAMPLE
    .\sync_i18n.ps1 -BaseUrl "https://api.openai.com/v1" -ApiKey "sk-..." -ModelId "gpt-4"
#>

param(
    [string]$BaseUrl,
    [string]$ApiKey,
    [string]$ModelId,
    [int]$BatchSize = 20,
    [int]$MaxRetries = 3,
    [string]$I18NPath = (Join-Path $PSScriptRoot "..\..\src\Everywhere\I18N")
)

# Try to load from config file if parameters not provided
$configPath = Join-Path $PSScriptRoot "sync_i18n_config.ps1"
if ((Test-Path $configPath) -and 
    ([string]::IsNullOrEmpty($BaseUrl) -or [string]::IsNullOrEmpty($ApiKey) -or [string]::IsNullOrEmpty($ModelId))) {
    Write-Host "Loading configuration from sync_i18n_config.ps1..." -ForegroundColor Cyan
    . $configPath
    if ([string]::IsNullOrEmpty($BaseUrl) -and (Get-Variable -Name BaseUrl -Scope Global -ErrorAction SilentlyContinue)) {
        $BaseUrl = $Global:BaseUrl
    }
    if ([string]::IsNullOrEmpty($ApiKey) -and (Get-Variable -Name ApiKey -Scope Global -ErrorAction SilentlyContinue)) {
        $ApiKey = $Global:ApiKey
    }
    if ([string]::IsNullOrEmpty($ModelId) -and (Get-Variable -Name ModelId -Scope Global -ErrorAction SilentlyContinue)) {
        $ModelId = $Global:ModelId
    }
    if ($BatchSize -eq 20 -and (Get-Variable -Name BatchSize -Scope Global -ErrorAction SilentlyContinue)) {
        $BatchSize = $Global:BatchSize
    }
    if ($MaxRetries -eq 3 -and (Get-Variable -Name MaxRetries -Scope Global -ErrorAction SilentlyContinue)) {
        $MaxRetries = $Global:MaxRetries
    }
}

# Validate required parameters
if ([string]::IsNullOrEmpty($BaseUrl) -or [string]::IsNullOrEmpty($ApiKey) -or [string]::IsNullOrEmpty($ModelId)) {
    Write-Host "Error: BaseUrl, ApiKey, and ModelId are required." -ForegroundColor Red
    Write-Host "Either provide them as parameters or create sync_i18n_config.ps1" -ForegroundColor Yellow
    exit 1
}

# Software introduction for AI context
$script:SoftwareIntroduction = @"
Everywhere is an interactive AI assistant with context-aware capabilities. It's a Windows desktop application that:
- Provides instant AI assistance anywhere on the screen
- Features a modern, sleek UI built with Avalonia
- Offers intelligent context understanding and perception
- Supports multiple languages and themes
- Includes features like web search, file system access, and system integration
- Allows users to interact with UI elements through AI

Key features:
- Quick invocation via keyboard shortcuts
- Visual element detection and interaction
- Multi-language support with i18n
- Customizable AI models and parameters
- Plugin system for extended functionality
"@

# Resources that should not be translated (language names)
$script:NoTranslatePatterns = @(
    'SettingsSelectionItem_Common_Language_*'
)

#region Helper Functions

function Write-ColorOutput {
    param(
        [string]$Message,
        [ConsoleColor]$ForegroundColor = [ConsoleColor]::White
    )
    $previousColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $ForegroundColor
    Write-Output $Message
    $Host.UI.RawUI.ForegroundColor = $previousColor
}

function Test-NoTranslate {
    param([string]$ResourceName)
    
    foreach ($pattern in $script:NoTranslatePatterns) {
        if ($ResourceName -like $pattern) {
            return $true
        }
    }
    return $false
}

function Get-ResxResources {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return @{}
    }

    [xml]$xml = Get-Content $FilePath -Encoding UTF8
    $resources = @{}
    
    foreach ($data in $xml.root.data) {
        if ($data.name) {
            $resources[$data.name] = $data.value
        }
    }
    
    return $resources
}

function Get-ResxLanguageComment {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $null
    }

    $content = Get-Content $FilePath -Raw -Encoding UTF8
    # Look for XML comment in first few lines (<!-- Language Name -->)
    if ($content -match '<!--\s*(.+?)\s*-->') {
        return $Matches[1].Trim()
    }
    
    return $null
}

function Get-ResxResourcesOrdered {
    <#
    .SYNOPSIS
    Get resources from resx file while preserving order
    #>
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return @()
    }

    [xml]$xml = Get-Content $FilePath -Encoding UTF8
    $orderedResources = [System.Collections.ArrayList]::new()
    
    foreach ($data in $xml.root.data) {
        if ($data.name) {
            $null = $orderedResources.Add(@{
                Name = $data.name
                Value = $data.value
            })
        }
    }
    
    return $orderedResources
}

function New-ResxDataElement {
    param(
        [xml]$XmlDoc,
        [string]$Name,
        [string]$Value
    )
    
    $dataElement = $XmlDoc.CreateElement('data')
    $dataElement.SetAttribute('name', $Name)
    $dataElement.SetAttribute('xml:space', 'preserve')
    
    $valueElement = $XmlDoc.CreateElement('value')
    $valueElement.InnerText = $Value
    $dataElement.AppendChild($valueElement) | Out-Null
    
    return $dataElement
}

function Update-ResxFile {
    <#
    .SYNOPSIS
    Update resx file with new translations and reorder according to base file
    #>
    param(
        [string]$FilePath,
        [hashtable]$Translations,
        [string]$BaseResxPath,
        [string]$LanguageComment
    )
    
    [xml]$xml = Get-Content $FilePath -Encoding UTF8
    [xml]$baseXml = Get-Content $BaseResxPath -Encoding UTF8
    
    # First, update or add new translations to existing structure
    foreach ($key in $Translations.Keys) {
        $existingData = $xml.root.data | Where-Object { $_.name -eq $key }
        
        if ($existingData) {
            $existingData.value = $Translations[$key]
        }
        else {
            $newData = New-ResxDataElement -XmlDoc $xml -Name $key -Value $Translations[$key]
            $xml.root.AppendChild($newData) | Out-Null
        }
    }
    
    # Now rebuild the file in the same order as base file
    $allCurrentData = @{}
    foreach ($data in $xml.root.data) {
        if ($data.name) {
            $allCurrentData[$data.name] = $data.value
        }
    }
    
    # Remove all existing data elements
    $dataElements = @($xml.root.data)
    foreach ($data in $dataElements) {
        $null = $xml.root.RemoveChild($data)
    }
    
    # Add back in base file order
    foreach ($baseData in $baseXml.root.data) {
        if ($baseData.name -and $allCurrentData.ContainsKey($baseData.name)) {
            $newData = New-ResxDataElement -XmlDoc $xml -Name $baseData.name -Value $allCurrentData[$baseData.name]
            $xml.root.AppendChild($newData) | Out-Null
        }
    }
    
    # Save with proper formatting
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = '    '
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.OmitXmlDeclaration = $true
    
    $stringWriter = New-Object System.IO.StringWriter
    $writer = [System.Xml.XmlWriter]::Create($stringWriter, $settings)
    try {
        $xml.Save($writer)
        $writer.Flush()
        
        # Get the XML content
        $content = $stringWriter.ToString()
        
        $content = '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + $content
        
        # Check if language comment already exists
        $hasComment = $false
        if ($LanguageComment) {
            $existingContent = Get-Content $FilePath -Raw -Encoding UTF8
            if ($existingContent -match "<!--\s*$([regex]::Escape($LanguageComment))\s*-->") {
                $hasComment = $true
            }
        }
        
        # Add language comment at the beginning only if it doesn't exist
        if ($LanguageComment -and -not $hasComment) {
            $content = "<!-- $LanguageComment -->`r`n" + $content
        }
        
        [System.IO.File]::WriteAllText($FilePath, $content, [System.Text.UTF8Encoding]::new($false))
    }
    finally {
        $writer.Close()
        $stringWriter.Close()
    }
}

function Invoke-AITranslation {
    param(
        [hashtable]$ResourcesToTranslate,
        [string]$TargetLanguage,
        [string]$LanguageComment,
        [int]$RetryCount = 0
    )
    
    # Prepare resources as JSON for AI (with proper escaping)
    $resourcesJson = $ResourcesToTranslate | ConvertTo-Json -Depth 10 -Compress
    
    $systemPrompt = @"
You are a professional translator for software localization. Your task is to translate UI strings for the "Everywhere" application.

Software Context:
$script:SoftwareIntroduction

Translation Guidelines:
1. Maintain the original meaning and tone
2. Keep placeholders intact (e.g., {0}, {1})
3. Preserve formatting characters (\n, \r, etc.)
4. Use natural, native expressions for the target language
5. Keep technical terms consistent
6. For language names (like "中文 (简体)"), DO NOT TRANSLATE - keep them as is
7. Ensure UI text is concise and clear
8. Consider the context of software UI

Target Language: $TargetLanguage
$(if ($LanguageComment) { "Language Hint: $LanguageComment" })

Please translate the following resources. Return ONLY a valid JSON object with the same keys and translated values.
Do NOT add any explanations or markdown formatting.
"@

    # Manually escape strings for JSON to avoid double-escaping issues
    # Escape the system prompt
    $escapedSystemPrompt = $systemPrompt -replace '\\', '\\\\' -replace '"', '\"' -replace "`n", '\n' -replace "`r", '\r' -replace "`t", '\t'
    
    # Escape the user prompt (which is already JSON, so we need to escape it as a string)
    $escapedUserPrompt = $resourcesJson -replace '\\', '\\\\' -replace '"', '\"' -replace "`n", '\n' -replace "`r", '\r' -replace "`t", '\t'
    
    # Build JSON request body manually to ensure proper escaping
    $requestBodyJson = @"
{
    "model": "$ModelId",
    "messages": [
        {
            "role": "system",
            "content": "$escapedSystemPrompt"
        },
        {
            "role": "user",
            "content": "$escapedUserPrompt"
        }
    ],
    "temperature": 0.3
}
"@

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/chat/completions" `
            -Method Post `
            -Headers @{
            'Content-Type'  = 'application/json; charset=utf-8'
            'Authorization' = "Bearer $ApiKey"
        } `
            -Body ([System.Text.Encoding]::UTF8.GetBytes($requestBodyJson)) `
            -TimeoutSec 120

        $translatedContent = $response.choices[0].message.content.Trim()
        
        # Remove markdown code blocks if present
        $translatedContent = $translatedContent -replace '^```json\s*', '' -replace '\s*```$', ''
        $translatedContent = $translatedContent.Trim()
        
        # Convert JSON to hashtable (PowerShell 5.1 compatible)
        $jsonObject = $translatedContent | ConvertFrom-Json
        $translatedResources = @{}
        foreach ($property in $jsonObject.PSObject.Properties) {
            $translatedResources[$property.Name] = $property.Value
        }
        
        return $translatedResources
    }
    catch {
        $errorMessage = $_.Exception.Message
        
        # Log problematic resources for debugging
        if ($RetryCount -eq 0) {
            Write-ColorOutput "  ! Error with batch containing keys: $($ResourcesToTranslate.Keys -join ', ')" -ForegroundColor DarkGray
        }
        
        # Check if it's a retriable error
        if ($RetryCount -lt $MaxRetries) {
            Write-ColorOutput "  ! Error (attempt $($RetryCount + 1)/$MaxRetries): $errorMessage" -ForegroundColor Yellow
            Write-ColorOutput "  Retrying in 2 seconds..." -ForegroundColor Gray
            Start-Sleep -Seconds 2
            
            return Invoke-AITranslation `
                -ResourcesToTranslate $ResourcesToTranslate `
                -TargetLanguage $TargetLanguage `
                -LanguageComment $LanguageComment `
                -RetryCount ($RetryCount + 1)
        }
        else {
            Write-ColorOutput "  Error calling AI API after $MaxRetries attempts: $errorMessage" -ForegroundColor Red
            
            throw
        }
    }
}

#endregion

#region Main Process

function Start-I18NSync {
    Write-ColorOutput "`n=== Everywhere i18n Synchronization ===" -ForegroundColor Cyan
    Write-ColorOutput "Base URL: $BaseUrl" -ForegroundColor Gray
    Write-ColorOutput "Model: $ModelId" -ForegroundColor Gray
    Write-ColorOutput "Batch Size: $BatchSize" -ForegroundColor Gray
    Write-ColorOutput "I18N Path: $I18NPath`n" -ForegroundColor Gray

    # Resolve absolute path
    $I18NPath = Resolve-Path $I18NPath -ErrorAction Stop

    # Read base resources
    $baseResxPath = Join-Path $I18NPath "Strings.resx"
    if (-not (Test-Path $baseResxPath)) {
        Write-ColorOutput "Error: Base resource file not found: $baseResxPath" -ForegroundColor Red
        exit 1
    }

    Write-ColorOutput "Reading base resources from Strings.resx..." -ForegroundColor Cyan
    $baseResources = Get-ResxResources -FilePath $baseResxPath
    Write-ColorOutput "Found $($baseResources.Count) base resources`n" -ForegroundColor Green

    # Find all localized resource files
    $localizedFiles = Get-ChildItem -Path $I18NPath -Filter "Strings.*.resx" | 
        Where-Object { $_.Name -ne 'Strings.Designer.resx' }

    foreach ($file in $localizedFiles) {
        $fileName = $file.Name
        $languageCode = $fileName -replace '^Strings\.(.+)\.resx$', '$1'
        
        # Read language comment from file
        $languageComment = Get-ResxLanguageComment -FilePath $file.FullName
        
        # Use comment as language name, or fall back to code
        $languageName = if ($languageComment) { $languageComment } else { $languageCode }

        Write-ColorOutput "Processing: $fileName ($languageName)" -ForegroundColor Cyan

        # Read language comment
        $languageComment = Get-ResxLanguageComment -FilePath $file.FullName
        
        # Read existing resources
        $existingResources = Get-ResxResources -FilePath $file.FullName

        # Find missing resources
        $missingResources = @{}
        foreach ($key in $baseResources.Keys) {
            if (-not $existingResources.ContainsKey($key)) {
                # Check if this resource should not be translated
                if (Test-NoTranslate -ResourceName $key) {
                    $missingResources[$key] = $baseResources[$key]
                }
                else {
                    $missingResources[$key] = ''
                }
            }
        }

        if ($missingResources.Count -eq 0) {
            Write-ColorOutput "  [OK] No missing resources" -ForegroundColor Green
            continue
        }

        Write-ColorOutput "  Found $($missingResources.Count) missing resources" -ForegroundColor Yellow

        # Separate no-translate and translate resources
        $noTranslateResources = @{}
        $toTranslateResources = @{}
        
        foreach ($key in $missingResources.Keys) {
            if (Test-NoTranslate -ResourceName $key) {
                $noTranslateResources[$key] = $baseResources[$key]
            }
            else {
                $toTranslateResources[$key] = $baseResources[$key]
            }
        }

        # Process no-translate resources
        if ($noTranslateResources.Count -gt 0) {
            Write-ColorOutput "  Adding $($noTranslateResources.Count) non-translatable resources..." -ForegroundColor Gray
            Update-ResxFile `
                -FilePath $file.FullName `
                -Translations $noTranslateResources `
                -BaseResxPath $baseResxPath `
                -LanguageComment $languageComment
        }

        # Process translation in batches
        if ($toTranslateResources.Count -gt 0) {
            $allKeys = @($toTranslateResources.Keys)
            $totalBatches = [Math]::Ceiling($allKeys.Count / $BatchSize)
            $currentBatch = 1
            $translatedCount = 0

            while ($translatedCount -lt $allKeys.Count) {
                $batchKeys = $allKeys | Select-Object -Skip $translatedCount -First $BatchSize
                $batchResources = @{}
                
                foreach ($key in $batchKeys) {
                    $batchResources[$key] = $toTranslateResources[$key]
                }

                Write-ColorOutput "  Translating batch $currentBatch/$totalBatches ($($batchResources.Count) items)..." -ForegroundColor Yellow

                try {
                    $translations = Invoke-AITranslation `
                        -ResourcesToTranslate $batchResources `
                        -TargetLanguage $languageName `
                        -LanguageComment $languageComment

                    if ($translations -and $translations.Count -gt 0) {
                        Update-ResxFile `
                            -FilePath $file.FullName `
                            -Translations $translations `
                            -BaseResxPath $baseResxPath `
                            -LanguageComment $languageComment
                        Write-ColorOutput "  [OK] Batch $currentBatch translated and saved" -ForegroundColor Green
                        $translatedCount += $batchKeys.Count
                    }
                    else {
                        Write-ColorOutput "  [x] No translations returned for batch $currentBatch" -ForegroundColor Red
                        # Don't break, continue to next batch
                    }
                }
                catch {
                    Write-ColorOutput "  [x] Failed to translate batch $currentBatch after $MaxRetries retries: $_" -ForegroundColor Red
                    # Don't break, continue to next batch
                }

                $currentBatch++
                
                # Small delay to avoid rate limiting
                if ($currentBatch -le $totalBatches) {
                    Start-Sleep -Milliseconds 500
                }
            }

            if ($translatedCount -eq $allKeys.Count) {
                Write-ColorOutput "  [OK] All translations completed for $fileName" -ForegroundColor Green
            }
            else {
                Write-ColorOutput "  ! Partial completion: $translatedCount/$($allKeys.Count) resources translated" -ForegroundColor Yellow
            }
        }

        Write-Output ""
    }

    Write-ColorOutput "=== Synchronization Complete ===" -ForegroundColor Cyan
}

# Run the sync process
Start-I18NSync

#endregion
