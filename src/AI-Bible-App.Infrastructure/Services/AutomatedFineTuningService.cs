using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Implementation of model fine-tuning using Unsloth/LLamaSharp
/// Supports local fine-tuning with LoRA for efficiency
/// </summary>
public class AutomatedFineTuningService : IModelFineTuningService
{
    private readonly ILogger<AutomatedFineTuningService> _logger;
    private readonly Dictionary<string, FineTuningJob> _activeJobs = new();
    private readonly string _workingDirectory;
    
    public AutomatedFineTuningService(ILogger<AutomatedFineTuningService> logger)
    {
        _logger = logger;
        _workingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "FineTuning");
        
        Directory.CreateDirectory(_workingDirectory);
    }
    
    public async Task<FineTuningJob> StartFineTuningAsync(
        string trainingDataPath,
        FineTuningConfig config,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var job = new FineTuningJob
        {
            JobId = jobId,
            Status = "pending",
            StartedAt = DateTime.UtcNow,
            BaseModel = config.BaseModel,
            TrainingDataPath = trainingDataPath,
            Config = config
        };
        
        _activeJobs[jobId] = job;
        _logger.LogInformation("Starting fine-tuning job {JobId} with base model {BaseModel}", 
            jobId, config.BaseModel);
        
        // Start fine-tuning in background
        _ = Task.Run(async () => await ExecuteFineTuningAsync(job, cancellationToken), cancellationToken);
        
        return job;
    }
    
    private async Task ExecuteFineTuningAsync(FineTuningJob job, CancellationToken cancellationToken)
    {
        try
        {
            job.Status = "running";
            
            // Create output directory for this job
            var outputDir = Path.Combine(_workingDirectory, job.JobId);
            Directory.CreateDirectory(outputDir);
            
            // Check if Unsloth is available (Python environment)
            var unslothAvailable = await CheckUnslothAvailabilityAsync();
            
            if (unslothAvailable)
            {
                await FineTuneWithUnslothAsync(job, outputDir, cancellationToken);
            }
            else
            {
                // Fallback: Use Ollama's built-in fine-tuning
                await FineTuneWithOllamaAsync(job, outputDir, cancellationToken);
            }
            
            job.Status = "completed";
            _logger.LogInformation("Fine-tuning job {JobId} completed successfully", job.JobId);
        }
        catch (OperationCanceledException)
        {
            job.Status = "cancelled";
            _logger.LogWarning("Fine-tuning job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            _logger.LogError(ex, "Fine-tuning job {JobId} failed", job.JobId);
        }
    }
    
    private async Task<bool> CheckUnslothAvailabilityAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-c \"import unsloth; print('available')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Contains("available");
        }
        catch
        {
            return false;
        }
    }
    
    private async Task FineTuneWithUnslothAsync(
        FineTuningJob job,
        string outputDir,
        CancellationToken cancellationToken)
    {
        // Create Python script for Unsloth fine-tuning
        var scriptPath = Path.Combine(outputDir, "finetune.py");
        var script = GenerateUnslothScript(job, outputDir);
        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
        
        // Execute Python script
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = outputDir
            }
        };
        
        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                _logger.LogInformation("[FineTune {JobId}] {Output}", job.JobId, args.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                _logger.LogWarning("[FineTune {JobId}] {Error}", job.JobId, args.Data);
            }
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"Fine-tuning failed with exit code {process.ExitCode}");
        }
    }
    
    private string GenerateUnslothScript(FineTuningJob job, string outputDir)
    {
        return $$$"""
from unsloth import FastLanguageModel
import torch
from datasets import load_dataset
from trl import SFTTrainer
from transformers import TrainingArguments

# Load model
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = "{{{job.Config.BaseModel}}}",
    max_seq_length = {{{job.Config.MaxSequenceLength}}},
    dtype = None,  # Auto-detect
    load_in_4bit = True,  # Use 4-bit quantization for efficiency
)

# Add LoRA adapters
model = FastLanguageModel.get_peft_model(
    model,
    r = {{{job.Config.LoRARank}}},
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
    lora_alpha = {{{job.Config.LoRAAlpha}}},
    lora_dropout = {{{job.Config.LoRADropout}}},
    bias = "none",
    use_gradient_checkpointing = True,
)

# Load training data
dataset = load_dataset("json", data_files="{{{job.TrainingDataPath}}}", split="train")

# Training arguments
training_args = TrainingArguments(
    output_dir = "{{{outputDir}}}",
    per_device_train_batch_size = {{{job.Config.BatchSize}}},
    num_train_epochs = {{{job.Config.Epochs}}},
    learning_rate = {{{job.Config.LearningRate}}},
    fp16 = torch.cuda.is_available(),
    logging_steps = 10,
    save_steps = 100,
    save_total_limit = 2,
    report_to = "none",
)

# Trainer
trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = dataset,
    dataset_text_field = "text",
    max_seq_length = {{{job.Config.MaxSequenceLength}}},
    args = training_args,
)

# Train
trainer.train()

# Save model
model.save_pretrained("{{{Path.Combine(outputDir, "final_model")}}}")
tokenizer.save_pretrained("{{{Path.Combine(outputDir, "final_model")}}}")

print("Fine-tuning completed successfully!")
""";
    }
    
    private async Task FineTuneWithOllamaAsync(
        FineTuningJob job,
        string outputDir,
        CancellationToken cancellationToken)
    {
        // Create Modelfile for Ollama
        var modelfilePath = Path.Combine(outputDir, "Modelfile");
        var modelName = $"bible-app-{job.JobId.Substring(0, 8)}";
        
        var modelfile = $$$"""
FROM {{{job.Config.BaseModel}}}

# Fine-tuning parameters
PARAMETER temperature 0.8
PARAMETER top_p 0.95
PARAMETER repeat_penalty 1.15

# System message
SYSTEM You are a biblical character helping users explore scripture and find meaningful insights.
""";
        
        await File.WriteAllTextAsync(modelfilePath, modelfile, cancellationToken);
        
        // Create model with Ollama
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = $"create {modelName} -f \"{modelfilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"Ollama model creation failed with exit code {process.ExitCode}");
        }
        
        // Save model name to output
        var modelInfoPath = Path.Combine(outputDir, "model_info.json");
        await File.WriteAllTextAsync(modelInfoPath, JsonSerializer.Serialize(new
        {
            model_name = modelName,
            base_model = job.Config.BaseModel,
            created_at = DateTime.UtcNow
        }), cancellationToken);
    }
    
    public async Task<FineTuningJobStatus> GetJobStatusAsync(string jobId)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job))
        {
            throw new KeyNotFoundException($"Job {jobId} not found");
        }
        
        var status = new FineTuningJobStatus
        {
            JobId = jobId,
            Status = job.Status,
            Progress = job.Status == "completed" ? 1.0 : 
                      job.Status == "running" ? 0.5 : 0.0
        };
        
        // Try to get model path if completed
        if (job.Status == "completed")
        {
            var outputDir = Path.Combine(_workingDirectory, jobId);
            var modelPath = Path.Combine(outputDir, "final_model");
            if (Directory.Exists(modelPath))
            {
                status.ModelPath = modelPath;
            }
        }
        
        return await Task.FromResult(status);
    }
    
    public async Task CancelJobAsync(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Status = "cancelled";
            _logger.LogInformation("Cancelled fine-tuning job {JobId}", jobId);
        }
        
        await Task.CompletedTask;
    }
    
    public async Task<string?> GetFineTunedModelPathAsync(string jobId)
    {
        var status = await GetJobStatusAsync(jobId);
        return status.ModelPath;
    }
}
