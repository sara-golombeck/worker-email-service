using Amazon.SQS;
using Amazon.SQS.Model;
using EmailWorker.Models;
using EmailWorker.Services;
using System.Text.Json;

namespace EmailWorker.Services
{
    public class EmailWorkerService : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailWorkerService> _logger;
        private readonly string _queueUrl;

        public EmailWorkerService(
            IAmazonSQS sqsClient,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<EmailWorkerService> logger)
        {
            _sqsClient = sqsClient;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _queueUrl = _configuration["AWS:SQS:QueueUrl"] ?? throw new InvalidOperationException("SQS Queue URL not configured");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Worker Service started. Queue URL: {QueueUrl}", _queueUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // קרא הודעות מהQueue
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20 // Long polling
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                    if (response.Messages.Count > 0)
                    {
                        _logger.LogInformation("Received {Count} messages from queue", response.Messages.Count);

                        foreach (var message in response.Messages)
                        {
                            await ProcessMessageAsync(message, stoppingToken);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No messages received, waiting...");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Email Worker Service is stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Email Worker Service main loop");
                    await Task.Delay(5000, stoppingToken); // Wait before retrying
                }
            }

            _logger.LogInformation("Email Worker Service stopped");
        }

        private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                // פרסר את ההודעה
                var emailMessage = JsonSerializer.Deserialize<EmailMessage>(message.Body);
                
                if (emailMessage == null || string.IsNullOrEmpty(emailMessage.Email))
                {
                    _logger.LogWarning("Invalid message format: {MessageBody}", message.Body);
                    await DeleteMessageAsync(message);
                    return;
                }

                _logger.LogInformation("Processing email for: {Email}", emailMessage.Email);

                // שלח מייל
                var result = await _emailService.SendLoginEmailAsync(emailMessage.Email);

                if (result.Success)
                {
                    _logger.LogInformation("Email sent successfully to: {Email}, MessageId: {MessageId}", 
                        emailMessage.Email, result.MessageId);
                    
                    // מחק הודעה מהQueue
                    await DeleteMessageAsync(message);
                }
                else
                {
                    _logger.LogError("Failed to send email to: {Email}, Error: {Error}", 
                        emailMessage.Email, result.ErrorMessage);
                    
                    // כאן יכול להיות retry logic או dead letter queue
                    // לעת עתה נמחק את ההודעה כדי לא ליצור infinite loop
                    await DeleteMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {MessageId}", message.MessageId);
                // לא מוחקים את ההודעה - תחזור לQueue לretry
            }
        }

        private async Task DeleteMessageAsync(Message message)
        {
            try
            {
                await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);
                _logger.LogDebug("Message deleted from queue: {MessageId}", message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message: {MessageId}", message.MessageId);
            }
        }
    }
}