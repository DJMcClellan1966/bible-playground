# Training Data Collection & Model Improvement

## Overview

This app can collect training data over time to improve the model's character responses. Unlike AlphaGo's self-play (which worked due to clear win/loss conditions), conversational AI requires human feedback or evaluation.

## Three Approaches to Model Learning

### 1. RAG-Enhanced Learning (Already Active)
The app uses **Retrieval-Augmented Generation (RAG)**:
- Retrieves relevant Bible verses for each question
- Provides contextual knowledge without changing the model
- Can be enhanced to retrieve past high-quality conversations

### 2. Synthetic Data Generation
Generate training examples programmatically:
```csharp
var generator = new SyntheticDataGenerator(_aiService, _characterRepository);
var conversations = await generator.GenerateSyntheticConversationsAsync(character, 1000);
```

This creates:
- Realistic user questions about common topics
- Character responses in their voice
- Labeled conversation pairs for fine-tuning

### 3. Real User Data Collection (with consent)
Collect actual user conversations:
- Users rate responses (thumbs up/down)
- High-rated conversations become training data
- Export to JSONL format for fine-tuning

## Feasibility Analysis

### ❌ What's NOT Feasible
- **Modifying the base model during runtime** - phi4 is frozen
- **True self-play like AlphaGo** - no objective "win" condition
- **Training without evaluation** - would reinforce bad patterns

### ✅ What IS Feasible

#### Short Term (Can do now)
1. **Collect conversation data** with user ratings
2. **Generate synthetic training examples** (1K-10K conversations)
3. **Export data** in fine-tuning format

#### Medium Term (Requires additional setup)
1. **Fine-tune with LoRA adapters** - lighter weight customization
   - Can create character-specific adapters
   - ~1GB per adapter vs. ~7GB for full model
   - Faster inference with character personality baked in

2. **Use Unsloth for efficient fine-tuning**
   - 2x faster training
   - 60% less memory
   - Works with consumer GPUs

#### Long Term (Requires infrastructure)
1. **Full model fine-tuning** on collected data
   - Requires GPU cluster or cloud resources
   - Creates custom phi4 variant
   - Can incorporate thousands of rated conversations

## Implementation Plan

### Phase 1: Data Collection (Week 1-2)
```bash
# Add services to MauiProgram.cs
builder.Services.AddSingleton<ITrainingDataRepository, TrainingDataRepository>();
builder.Services.AddSingleton<ISyntheticDataGenerator, SyntheticDataGenerator>();
```

### Phase 2: Synthetic Generation (Week 3-4)
Create admin page to generate synthetic data:
- Select character
- Choose number of conversations (100, 1000, 10000)
- Generate async in background
- Review and rate generated conversations

### Phase 3: Export & Fine-Tune (Month 2)
```python
# Export data
conversations = await repository.ExportTrainingDataAsync("training_data.json")

# Fine-tune with Unsloth (Python)
from unsloth import FastLanguageModel
model, tokenizer = FastLanguageModel.from_pretrained("microsoft/phi-4")
model = FastLanguageModel.get_peft_model(model, r=16, lora_alpha=16)
trainer.train()  # Train on exported conversations
```

### Phase 4: Deploy Custom Adapter (Month 3)
- Load LoRA adapter in Ollama
- Character-specific response improvements
- 10-20% quality improvement expected

## Storage Estimates

### Conversation Data
- 1 conversation ≈ 500 bytes (JSON)
- 1,000 conversations ≈ 500 KB
- 10,000 conversations ≈ 5 MB
- 1,000,000 conversations ≈ 500 MB

### LoRA Adapters
- Base phi4 model: ~7 GB
- LoRA adapter per character: ~500 MB - 1 GB
- 20 characters: ~10-20 GB total

## Quality Evaluation

### Automatic Scoring
```csharp
public async Task<double> EvaluateResponseQuality(string userQuestion, string response)
{
    var criteria = @"Rate this response 0.0-1.0 based on:
    - Directly addresses the question (not generic)
    - Uses character-specific knowledge
    - Compassionate and helpful
    - Appropriate length (not too long)
    Return only the number.";
    
    var score = await _aiService.GetChatResponseAsync(...);
    return double.Parse(score);
}
```

### Human Validation
- User ratings (thumbs up/down)
- Optional detailed feedback
- Flag responses for review

## Example Workflow

```csharp
// 1. User has conversation
var response = await chatViewModel.SendMessage("How do I deal with fear?");

// 2. User rates response
await chatViewModel.RateMessage(response, 1); // thumbs up

// 3. Auto-save high-rated conversations
if (response.Rating == 1) 
{
    var trainingConv = new TrainingConversation
    {
        CharacterId = character.Id,
        Messages = conversationHistory,
        QualityScore = 0.9, // high because user rated it
        IsHumanValidated = true
    };
    await trainingRepo.SaveTrainingConversationAsync(trainingConv);
}

// 4. Generate synthetic data (admin task)
var generator = new SyntheticDataGenerator(...);
foreach (var character in characters)
{
    var synthetic = await generator.GenerateSyntheticConversationsAsync(character, 1000);
    foreach (var conv in synthetic)
    {
        // Evaluate quality
        conv.QualityScore = await EvaluateResponseQuality(conv);
        if (conv.QualityScore > 0.7)
        {
            await trainingRepo.SaveTrainingConversationAsync(conv);
        }
    }
}

// 5. Export for fine-tuning
await trainingRepo.ExportTrainingDataAsync("phi4_bible_characters_training.json");

// 6. Fine-tune externally (Python script)
// python fine_tune.py --data phi4_bible_characters_training.json --output character_adapter

// 7. Load improved model back into app
// Update appsettings.json: "ModelName": "phi4-bible-characters:latest"
```

## Cost-Benefit Analysis

### Synthetic Generation
- **Time**: 1-2 seconds per conversation
- **1,000 conversations**: ~30 minutes
- **10,000 conversations**: ~5 hours
- **Cost**: Free (local Ollama)

### Fine-Tuning (LoRA)
- **Data needed**: 1,000-10,000 high-quality examples
- **GPU time**: 2-8 hours on RTX 4090
- **Cloud cost**: $10-50 on RunPod/Vast.ai
- **Expected improvement**: 10-20% better responses

### Full Fine-Tuning
- **Data needed**: 50,000-100,000+ examples
- **GPU time**: 20-50 hours
- **Cloud cost**: $200-500
- **Expected improvement**: 30-50% better responses

## Conclusion

**Yes, this is feasible!** But not quite like AlphaGo:

1. **Short-term**: Collect data from real users + generate synthetic examples
2. **Medium-term**: Fine-tune LoRA adapters for each character (~$50 cost)
3. **Long-term**: Full model fine-tuning as data accumulates

The app infrastructure I just created gives you:
- ✅ Training data collection
- ✅ Synthetic conversation generation
- ✅ Export format for fine-tuning
- ✅ Quality evaluation framework

Would you like me to create an admin page where you can start generating synthetic training data?
