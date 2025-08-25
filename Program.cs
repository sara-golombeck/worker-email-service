using Amazon.SQS;
using Amazon.SimpleEmail;
using EmailWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add AWS services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonSimpleEmailService>();

// Add custom services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<EmailWorkerService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();

// Run the worker
await host.RunAsync();