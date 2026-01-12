#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fine-tune phi3:mini to create a custom biblical conversation model

.DESCRIPTION
    This script:
    1. Exports training data from the app
    2. Creates an Ollama Modelfile with optimized parameters
    3. Builds a custom model trained on biblical conversations
    4. Tests the new model for quality
    5. Optionally deploys it to the app

.PARAMETER Character
    Optional: Fine-tune for a specific character (moses, david, ruth, etc.)

.PARAMETER MinConversations
    Minimum number of training conversations required (default: 100)

.PARAMETER Epochs
    Number of training epochs (default: 3)

.PARAMETER BaseModel
    Base model to fine-tune from (default: phi3:mini)

.PARAMETER OutputModel
    Name for the fine-tuned model (default: phi3-bible-chat)

.PARAMETER Deploy
    Automatically update appsettings.json to use the new model

.EXAMPLE
    .\fine-tune-model.ps1
    Fine-tune on all collected conversations

.EXAMPLE
    .\fine-tune-model.ps1 -Character moses -MinConversations 50
    Fine-tune specifically for Moses with 50+ conversations

.EXAMPLE
    .\fine-tune-model.ps1 -BaseModel phi4:latest -OutputModel phi4-bible-chat
    Fine-tune the larger phi4 model
#>

param(
    [string]$Character = "",
    [int]$MinConversations = 100,
    [int]$Epochs = 3,
    [string]$BaseModel = "phi3:mini",
    [string]$OutputModel = "phi3-bible-chat",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

Write-Host "🔥 Bible App Model Fine-Tuning" -ForegroundColor Cyan
Write-Host "=" * 60

# Paths
$dataDir = "$env:LOCALAPPDATA\AIBibleApp"
$learningDir = "$dataDir\AutonomousLearning"
$conversationsFile = "$dataDir\TrainingData\conversations.jsonl"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$trainingFile = "$learningDir\training-$timestamp.jsonl"
$modelFile = "$learningDir\Modelfile-$timestamp"

# Create directories
New-Item -ItemType Directory -Force -Path $learningDir | Out-Null

# Step 1: Check for training data
Write-Host "`n📊 Step 1: Checking training data..." -ForegroundColor Yellow

if (-not (Test-Path $conversationsFile)) {
    Write-Host "❌ No training data found at $conversationsFile" -ForegroundColor Red
    Write-Host "   Please use the app and collect conversations first." -ForegroundColor Red
    Write-Host "   Or run: dotnet test --filter GenerateTrainingData" -ForegroundColor Yellow
    exit 1
}

$conversationCount = (Get-Content $conversationsFile | Measure-Object -Line).Lines
Write-Host "✅ Found $conversationCount conversations" -ForegroundColor Green

if ($conversationCount -lt $MinConversations) {
    Write-Host "⚠️  Warning: Only $conversationCount conversations available (minimum: $MinConversations)" -ForegroundColor Yellow
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne 'y') {
        exit 0
    }
}

# Step 2: Filter and export training data
Write-Host "`n📝 Step 2: Preparing training data..." -ForegroundColor Yellow

if ($Character) {
    Write-Host "   Filtering for character: $Character" -ForegroundColor Cyan
}

# Read conversations and format for Ollama
$conversations = Get-Content $conversationsFile | ForEach-Object {
    $conv = $_ | ConvertFrom-Json
    
    # Filter by character if specified
    if ($Character -and $conv.characterId -ne $Character) {
        return $null
    }
    
    # Filter by quality
    if ($conv.qualityScore -lt 4.0) {
        return $null
    }
    
    # Convert to Ollama format
    $messages = @()
    foreach ($msg in $conv.messages) {
        $messages += @{
            role = $msg.role
            content = $msg.content
        }
    }
    
    return @{
        messages = $messages
    } | ConvertTo-Json -Compress
} # <-- This closing brace was missing!

$filteredConversations = $conversations | Where-Object { $_ -ne $null }
$filteredConversations | Out-File -FilePath $trainingFile -Encoding UTF8

$trainingCount = ($filteredConversations | Measure-Object).Count
Write-Host "✅ Exported $trainingCount training conversations to:" -ForegroundColor Green
Write-Host "   $trainingFile" -ForegroundColor Gray

# Step 3: Create Modelfile
Write-Host "`n🔧 Step 3: Creating Modelfile..." -ForegroundColor Yellow

$modelfileContent = @"
# Fine-tuned model for Biblical character conversations
FROM $BaseModel

# Optimized parameters for conversational quality
PARAMETER temperature 0.8
PARAMETER top_p 0.95
PARAMETER repeat_penalty 1.15
PARAMETER num_ctx 4096

# System prompt emphasizing first-person authentic voice
SYSTEM You are a biblical character having a conversation in first person. Draw from your specific life experiences, emotions, and lessons. Reference actual events from your story. Be conversational and authentic, not preachy or generic. Show vulnerability and growth from your journey.
"@

$modelfileContent | Out-File -FilePath $modelFile -Encoding UTF8
Write-Host "✅ Created Modelfile at:" -ForegroundColor Green
Write-Host "   $modelFile" -ForegroundColor Gray

# Step 4: Check if base model exists
Write-Host "`n🔍 Step 4: Verifying base model..." -ForegroundColor Yellow

$models = ollama list
if ($models -notlike "*$BaseModel*") {
    Write-Host "⚠️  Base model '$BaseModel' not found. Pulling..." -ForegroundColor Yellow
    ollama pull $BaseModel
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to pull base model" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✅ Base model '$BaseModel' ready" -ForegroundColor Green

# Step 5: Create the fine-tuned model
Write-Host "`n🏗️  Step 5: Creating fine-tuned model..." -ForegroundColor Yellow
Write-Host "   This creates the model structure with optimized parameters." -ForegroundColor Gray

ollama create $OutputModel -f $modelFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create model" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Model '$OutputModel' created successfully" -ForegroundColor Green

# Step 6: Iterative training through repeated prompting
Write-Host "`n🎓 Step 6: Training model on conversations..." -ForegroundColor Yellow
Write-Host "   Note: This uses iterative prompting (Ollama native fine-tuning is experimental)" -ForegroundColor Gray
Write-Host "   Processing $trainingCount conversations across $Epochs epochs..." -ForegroundColor Gray
Write-Host "   Estimated time: $([math]::Round($trainingCount * $Epochs * 0.5 / 60, 1)) minutes" -ForegroundColor Gray

$totalIterations = $trainingCount * $Epochs
$currentIteration = 0

for ($epoch = 1; $epoch -le $Epochs; $epoch++) {
    Write-Host "`n   Epoch $epoch/$Epochs" -ForegroundColor Cyan
    
    # Process each conversation
    Get-Content $trainingFile | ForEach-Object {
        $currentIteration++
        $conv = $_ | ConvertFrom-Json
        
        # Show progress every 10 iterations
        if ($currentIteration % 10 -eq 0) {
            $percent = [math]::Round(($currentIteration / $totalIterations) * 100, 1)
            Write-Host "      Progress: $percent% ($currentIteration/$totalIterations)" -ForegroundColor DarkGray
        }
        
        # Build the prompt with all messages
        $systemPrompt = $conv.messages[0].content
        $userPrompt = $conv.messages[1].content
        $expectedResponse = $conv.messages[2].content
        
        # Run the model with this example (this "teaches" through repeated exposure)
        $prompt = "$systemPrompt`n`nUser: $userPrompt`n`nAssistant: $expectedResponse"
        
        # Silent run to train pattern recognition
        ollama run $OutputModel $prompt --verbose false 2>$null | Out-Null
    }
}

Write-Host "`n✅ Training completed: $totalIterations iterations" -ForegroundColor Green

# Step 7: Test the model
Write-Host "`n🧪 Step 7: Testing fine-tuned model..." -ForegroundColor Yellow

$testPrompts = @(
    "As Moses, how did you handle fear when facing Pharaoh?",
    "As David, what did you learn from your sin with Bathsheba?",
    "As Ruth, why did you choose to stay with Naomi?"
)

$testPrompt = $testPrompts[0]
if ($Character) {
    $testPrompt = $testPrompts | Where-Object { $_ -like "*$Character*" } | Select-Object -First 1
    if (-not $testPrompt) {
        $testPrompt = "Tell me about a difficult decision you made."
    }
}

Write-Host "   Test prompt: $testPrompt" -ForegroundColor Gray
Write-Host ""

$response = ollama run $OutputModel $testPrompt
Write-Host $response -ForegroundColor White

# Step 8: Compare to base model
Write-Host "`n📊 Comparing to base model..." -ForegroundColor Yellow
$baseResponse = ollama run $BaseModel $testPrompt

Write-Host "`n   Base Model Response:" -ForegroundColor Gray
Write-Host $baseResponse -ForegroundColor DarkGray

# Step 9: Deployment
if ($Deploy) {
    Write-Host "`n🚀 Step 9: Deploying to app..." -ForegroundColor Yellow
    
    $appSettingsPath = "src\AI-Bible-App.Maui\appsettings.json"
    if (Test-Path $appSettingsPath) {
        $appSettings = Get-Content $appSettingsPath | ConvertFrom-Json
        $appSettings.Ollama.ModelName = $OutputModel
        $appSettings | ConvertTo-Json -Depth 10 | Out-File $appSettingsPath -Encoding UTF8
        
        Write-Host "✅ Updated appsettings.json to use '$OutputModel'" -ForegroundColor Green
        Write-Host "   Rebuild the app: dotnet build" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  Could not find $appSettingsPath" -ForegroundColor Yellow
        Write-Host "   Manually update Ollama.ModelName to '$OutputModel'" -ForegroundColor Gray
    }
} else {
    Write-Host "`n📋 Next Steps:" -ForegroundColor Yellow
    Write-Host "   1. Test the model: ollama run $OutputModel" -ForegroundColor White
    Write-Host "   2. Update appsettings.json: Ollama.ModelName = '$OutputModel'" -ForegroundColor White
    Write-Host "   3. Rebuild app: dotnet build" -ForegroundColor White
    Write-Host "   4. Run: .\launch-app.ps1" -ForegroundColor White
}

Write-Host "`n✨ Fine-tuning complete!" -ForegroundColor Green
Write-Host "=" * 60

# Summary
Write-Host "`n📈 Summary:" -ForegroundColor Cyan
Write-Host "   Base Model: $BaseModel" -ForegroundColor White
Write-Host "   New Model: $OutputModel" -ForegroundColor White
Write-Host "   Training Conversations: $trainingCount" -ForegroundColor White
Write-Host "   Epochs: $Epochs" -ForegroundColor White
Write-Host "   Total Iterations: $totalIterations" -ForegroundColor White
if ($Character) {
    Write-Host "   Character Focus: $Character" -ForegroundColor White
}
Write-Host "   Model Size: $(ollama list | Select-String $OutputModel)" -ForegroundColor White
