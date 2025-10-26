# i18n Synchronization Configuration Template
# Copy this file to sync_i18n_config.ps1 and fill in your values

# OpenAI-compatible API configuration
$BaseUrl = "https://ark.cn-beijing.volces.com/api/v3"  # e.g., https://api.deepseek.com/v1, https://api.openai.com/v1
$ApiKey = "your-api-key-here"                          # Your API key
$ModelId = "doubao-seed-1-6-250615"                    # Model to use for translation

# Optional parameters
$BatchSize = 20                                        # Number of resources per batch (default: 20)
$MaxRetries = 3                                        # Maximum retries for failed batches (default: 3)